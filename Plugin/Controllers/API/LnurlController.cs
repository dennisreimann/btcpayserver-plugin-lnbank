using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client.Models;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using LNURL;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using Transaction = BTCPayServer.Plugins.LNbank.Data.Models.Transaction;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

[ApiController]
[Route("~/api/v1/lnbank/[controller]")]
public class LnurlController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly WalletRepository _walletRepository;

    private static readonly LightMoney _minSendable = new(1, LightMoneyUnit.Satoshi);
    private static readonly LightMoney _maxSendable = LightMoney.FromUnit(6.12m, LightMoneyUnit.BTC);
    private int _commentLength = 615;

    public LnurlController(WalletService walletService, WalletRepository walletRepository)
    {
        _walletService = walletService;
        _walletRepository = walletRepository;
    }

    [HttpGet("{walletId}/pay")]
    public async Task<IActionResult> LnurlPay(string walletId)
    {
        var wallet = await _walletRepository.GetWallet(new WalletsQuery { WalletId = new[] { walletId } });
        if (wallet == null)
        {
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");
        }

        var data = new List<string[]> { new[] { "text/plain", wallet.Name } };
        var meta = JsonConvert.SerializeObject(data);
        var payRequest = GetPayRequest(wallet.WalletId, meta);

        return Ok(payRequest);
    }

    [HttpGet("{walletId}/pay-callback")]
    public async Task<IActionResult> LnurlPayCallback(string walletId,
        [FromQuery] long? amount = null, string comment = null)
    {
        var wallet = await _walletRepository.GetWallet(new WalletsQuery { WalletId = new[] { walletId } });
        if (wallet == null)
        {
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");
        }

        var data = new List<string[]> { new[] { "text/plain", wallet.Name } };
        var meta = JsonConvert.SerializeObject(data);
        if (amount is null)
        {
            var payRequest = GetPayRequest(wallet.WalletId, meta);

            return Ok(payRequest);
        }

        comment = comment?.Truncate(_commentLength);

        if (amount < _minSendable || amount > _maxSendable)
        {
            return BadRequest(GetError("Amount is out of bounds"));
        }

        try
        {
            var req = new CreateLightningInvoiceRequest
            {
                Amount = amount.Value,
                Description = meta,
                DescriptionHashOnly = true,
                Expiry = WalletService.ExpiryDefault
            };
            Transaction transaction = await _walletService.Receive(wallet, req, comment);

            var paymentRequest = transaction.PaymentRequest;
            if (_walletService.ValidateDescriptionHash(paymentRequest, meta))
            {
                return Ok(new LNURLPayRequest.LNURLPayRequestCallbackResponse
                {
                    Disposable = true,
                    Routes = Array.Empty<string>(),
                    Pr = paymentRequest
                });
            }
            return BadRequest(GetError("LNbank could not generate invoice with a valid description hash"));
        }
        catch (Exception exception)
        {
            return BadRequest(GetError($"LNbank could not generate invoice: {exception.Message}"));
        }
    }

    private LNUrlStatusResponse GetError(string reason) => new()
    {
        Status = "ERROR",
        Reason = reason
    };

    private LNURLPayRequest GetPayRequest(string walletId, string metadata) => new()
    {
        Tag = "payRequest",
        Callback = new Uri($"{Request.Scheme}://{Request.Host.ToUriComponent()}{Request.PathBase.ToUriComponent()}/api/v1/lnbank/lnurl/{walletId}/pay-callback"),
        MinSendable = _minSendable,
        MaxSendable = _maxSendable,
        CommentAllowed = _commentLength,
        Metadata = metadata
    };
}
