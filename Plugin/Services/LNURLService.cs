#nullable enable
using System;
using System.Net.Http;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Exceptions;
using LNURL;
using Microsoft.Extensions.Hosting;
using MimeKit;
using NBitcoin;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LNURLService
{
    // see LightningClientFactoryService
    private const string HttpHandlerOnionNamedClient = "lightning.onion";
    private const string HttpHandlerClearnetNamedClient = "lightning.clearnet";
    private const string LnurlPayRequestTag = "payRequest";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _isDevEnv;
    private readonly Network _network;

    public LNURLService(
        IHttpClientFactory httpClientFactory,
        BTCPayNetworkProvider networkProvider,
        IHostEnvironment env)
    {
        _httpClientFactory = httpClientFactory;
        _network = networkProvider.GetNetwork<BTCPayNetwork>(BTCPayService.CryptoCode).NBitcoinNetwork;

        // This is to allow for local testing of LNURL payments
        _isDevEnv = networkProvider.NetworkType == ChainName.Regtest && env.IsDevelopment();
    }

    public async Task<LNURLPayRequest> GetPaymentRequest(string destination)
    {
        (Uri lnurl, string? lnurlTag) = GetLNURL(destination);
        return await ResolveLNURL(lnurl, lnurlTag, destination);
    }

    public async Task<BOLT11PaymentRequest> GetBolt11(LNURLPayRequest payRequest, LightMoney? amount = null,
        string? comment = null)
    {
        if (payRequest.Tag != LnurlPayRequestTag)
            throw new PaymentRequestValidationException(
                $"Expected LNURL \"{LnurlPayRequestTag}\" type, got \"{payRequest.Tag}\".");

        HttpClient httpClient = CreateClient(payRequest.Callback);
        LNURLPayRequest.LNURLPayRequestCallbackResponse? payResponse =
            await payRequest.SendRequest(amount ?? payRequest.MinSendable, _network, httpClient, comment);
        BOLT11PaymentRequest? bolt11 = payResponse.GetPaymentRequest(_network);

        return bolt11;
    }

    private async Task<LNURLPayRequest> ResolveLNURL(Uri lnurl, string? lnurlTag, string destination)
    {
        string type = IsLightningAddress(destination) ? "Lightning Address" : "LNURL";
        try
        {
            if (lnurlTag is null)
            {
                HttpClient httpClient = CreateClient(lnurl);
                LNURLPayRequest? info = (LNURLPayRequest)await LNURL.LNURL.FetchInformation(lnurl, httpClient);
                lnurlTag = info.Tag;
            }

            if (lnurlTag.Equals(LnurlPayRequestTag, StringComparison.InvariantCultureIgnoreCase))
            {
                HttpClient httpClient = CreateClient(lnurl);
                LNURLPayRequest? payRequest =
                    (LNURLPayRequest)await LNURL.LNURL.FetchInformation(lnurl, lnurlTag, httpClient);
                return payRequest;
            }
        }
        catch (HttpRequestException ex) when (_isDevEnv &&
                                              ex.Message.StartsWith("The SSL connection could not be established"))
        {
            var lnurlBuilder = new UriBuilder(lnurl) { Scheme = Uri.UriSchemeHttp };
            return await ResolveLNURL(lnurlBuilder.Uri, lnurlTag, destination);
        }
        catch (Exception ex)
        {
            throw new ResolveLNURLException(destination, $"Resolving {type} {destination} failed: {ex.Message}");
        }

        throw new ResolveLNURLException(destination, $"Resolving {type} {destination} failed");
    }

    private (Uri lnurl, string? lnurlTag) GetLNURL(string destination)
    {
        Uri lnurl;
        string? lnurlTag = null;
        bool isLnAddress = IsLightningAddress(destination);
        try
        {
            lnurl = isLnAddress
                ? LNURL.LNURL.ExtractUriFromInternetIdentifier(destination)
                : LNURL.LNURL.Parse(destination, out lnurlTag);
        }
        catch (Exception ex)
        {
            string type = isLnAddress ? "Lightning Address" : "LNURL";
            throw new ResolveLNURLException(destination, $"Parsing {type} failed: {ex.Message}");
        }

        return (lnurl, lnurlTag);
    }

    private bool IsLightningAddress(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;
        if (_isDevEnv)
            return email.Contains('@');

        ParserOptions? options = ParserOptions.Default.Clone();
        options.AllowAddressesWithoutDomain = false;
        return MailboxAddress.TryParse(options, email, out MailboxAddress? mailboxAddress) &&
               mailboxAddress is not null;
    }

    private HttpClient CreateClient(Uri uri)
    {
        return _httpClientFactory.CreateClient(uri.IsOnion()
            ? HttpHandlerOnionNamedClient
            : HttpHandlerClearnetNamedClient);
    }
}
