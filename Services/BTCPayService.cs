using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;

namespace BTCPayServer.Plugins.LNbank.Services;

public class BTCPayService
{
    public const string CryptoCode = "BTC";
    private readonly IBTCPayServerClientFactory _clientFactory;

    public BTCPayService(IBTCPayServerClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<LightningInvoiceData> CreateLightningInvoice(LightningInvoiceCreateRequest req)
    {
        var client = await Client();
        return await client.CreateLightningInvoice(CryptoCode, new CreateLightningInvoiceRequest
        {
            Amount = req.Amount,
            Description = req.Description,
            DescriptionHash = req.DescriptionHash,
            Expiry = req.Expiry,
            PrivateRouteHints = req.PrivateRouteHints
        });
    }
        
    public async Task<LightningPaymentData> PayLightningInvoice(PayLightningInvoiceRequest req, CancellationToken cancellationToken = default)
    {
        var client = await Client();
        return await client.PayLightningInvoice(CryptoCode, req, cancellationToken);
    }

    public async Task<LightningPaymentData> GetLightningPayment(string paymentHash, CancellationToken cancellationToken = default)
    {
        var client = await Client();
        return await client.GetLightningPayment(CryptoCode, paymentHash, cancellationToken);
    }

    public async Task<LightningInvoiceData> GetLightningInvoice(string invoiceId, CancellationToken cancellationToken = default)
    {
        var client = await Client();
        return await client.GetLightningInvoice(CryptoCode, invoiceId, cancellationToken);
    }

    public async Task<LightningNodeInformationData> GetLightningNodeInfo(CancellationToken cancellationToken = default)
    {
        var client = await Client();
        return await client.GetLightningNodeInfo(CryptoCode, cancellationToken);
    }

    public async Task<LightningNodeBalanceData> GetLightningNodeBalance(CancellationToken cancellationToken = default)
    {
        var client = await Client();
        return await client.GetLightningNodeBalance(CryptoCode, cancellationToken);
    }

    public async Task<IEnumerable<LightningChannelData>> ListLightningChannels(CancellationToken cancellationToken = default)
    {
        var client = await Client();
        return await client.GetLightningNodeChannels(CryptoCode, cancellationToken);
    }

    public async Task<string> GetLightningDepositAddress(CancellationToken cancellationToken = default)
    {
        var client = await Client();
        var addr = await client.GetLightningDepositAddress(CryptoCode, cancellationToken);
        return addr;
    }

    public async Task OpenLightningChannel(OpenLightningChannelRequest req, CancellationToken cancellationToken = default)
    {
        var client = await Client();
        await client.OpenLightningChannel(CryptoCode, req, cancellationToken);
    }

    public async Task ConnectToLightningNode(ConnectToNodeRequest req, CancellationToken cancellationToken = default)
    {
        var client = await Client();
        await client.ConnectToLightningNode(CryptoCode, req, cancellationToken);
    }

    private async Task<BTCPayServerClient> Client()
    {
        return await _clientFactory.Create(null, new string[0]);
    }
}
