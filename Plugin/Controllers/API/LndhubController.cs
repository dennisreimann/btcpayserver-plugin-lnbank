using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Lightning.LNDhub.Models;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Exceptions;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NBitcoin;
using CreateInvoiceRequest = BTCPayServer.Lightning.LNDhub.Models.CreateInvoiceRequest;
using InvoiceData = BTCPayServer.Lightning.LNDhub.Models.InvoiceData;
using Transaction = BTCPayServer.Plugins.LNbank.Data.Models.Transaction;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

[ApiController]
[Route("~/plugins/lnbank/api/[controller]")]
[Authorize(AuthenticationSchemes = LNbankAuthenticationSchemes.AccessKey)]
public class LndhubController : ControllerBase
{
    private string UserId => User.Claims.First(c => c.Type == _identityOptions.CurrentValue.ClaimsIdentity.UserIdClaimType).Value;
    private string WalletId => User.Claims.First(c => c.Type == "WalletId").Value;
    private Wallet Wallet => (Wallet)ControllerContext.HttpContext.Items.TryGet("BTCPAY.LNBANK.WALLET");

    private readonly BTCPayService _btcpayService;
    private readonly WalletService _walletService;
    private readonly WalletRepository _walletRepository;
    private readonly IOptionsMonitor<IdentityOptions> _identityOptions;

    public LndhubController(
        BTCPayService btcpayService,
        WalletService walletService,
        WalletRepository walletRepository,
        IOptionsMonitor<IdentityOptions> identityOptions)
    {
        _btcpayService = btcpayService;
        _walletService = walletService;
        _walletRepository = walletRepository;
        _identityOptions = identityOptions;
    }

    // https://github.com/BlueWallet/LndHub/blob/master/doc/Send-requirements.md#post-authtypeauth
    [AllowAnonymous]
    [HttpPost("auth")]
    public async Task<IActionResult> Auth(AuthRequest req, [FromQuery] string type)
    {
        AuthResponse result = null;

        switch (type)
        {
            case "auth":
                var wallet = await _walletRepository.GetWallet(new WalletsQuery
                {
                    WalletId = new[] { req.Login },
                    AccessKey = new[] { req.Password }
                });

                if (wallet is { AccessLevel: AccessLevel.Admin })
                {
                    var accessKey = wallet.AccessKeys.First(a => a.Key == req.Password);
                    result = new AuthResponse { AccessToken = accessKey.Key, RefreshToken = accessKey.Key };
                }
                break;

            // fake this case as we don't do OAuth
            case "refresh_token":
                result = new AuthResponse { AccessToken = req.RefreshToken, RefreshToken = req.RefreshToken };
                break;
        }

        return Ok(result != null ? result : new ErrorResponse(1));
    }

    // https://github.com/BlueWallet/LndHub/blob/master/doc/Send-requirements.md#get-getinfo
    [HttpGet("getinfo")]
    public async Task<IActionResult> GetInfo()
    {
        var info = await _btcpayService.GetLightningNodeInfo();
        var result = new NodeInfoData
        {
            Uris = info.NodeURIs.Select(uri => uri.ToString()),
            IdentityPubkey = info.NodeURIs.First().NodeId.ToString(),
            BlockHeight = info.BlockHeight,
            Alias = $"LNbank: {Wallet.Name}",
            /* FIXME: Needs this PR merged https://github.com/btcpayserver/btcpayserver/pull/4167
            
            Color = info.Color,
            Version = info.Version,
            PeersCount = info.PeersCount.HasValue ? Convert.ToInt32(info.PeersCount.Value) : 0,
            ActiveChannelsCount = info.ActiveChannelsCount.HasValue ? Convert.ToInt32(info.ActiveChannelsCount.Value) : 0,
            InactiveChannelsCount = info.InactiveChannelsCount.HasValue ? Convert.ToInt32(info.InactiveChannelsCount.Value) : 0,
            PendingChannelsCount = info.PendingChannelsCount.HasValue ? Convert.ToInt32(info.PendingChannelsCount.Value) : 0
            */
        };
        return Ok(result);
    }

    // https://github.com/BlueWallet/LndHub/blob/master/doc/Send-requirements.md#get-gettxs
    [HttpGet("gettxs")]
    public async Task<IActionResult> GetTransactions([FromQuery] int? limit, [FromQuery] int? offset)
    {
        var wallet = await GetWalletWithTransactions();
        var transactions = wallet.Transactions
            .Where(t => string.IsNullOrEmpty(t.InvoiceId))
            .Select(ToTransactionData);
        return Ok(transactions);
    }

    // https://github.com/BlueWallet/LndHub/blob/master/doc/Send-requirements.md#get-getuserinvoices
    [HttpGet("getuserinvoices")]
    public async Task<IActionResult> GetUserInvoices()
    {
        var wallet = await GetWalletWithTransactions();
        var invoices = wallet.Transactions
            .Where(t => !string.IsNullOrEmpty(t.InvoiceId))
            .Select(ToInvoiceData);
        return Ok(invoices);
    }

    // https://github.com/BlueWallet/LndHub/blob/master/doc/Send-requirements.md#get-getbalance
    [HttpGet("balance")]
    public async Task<IActionResult> Balance()
    {
        var wallet = await GetWalletWithTransactions();
        var btc = new BtcBalance { AvailableBalance = wallet.Balance };
        var result = new BalanceData { BTC = btc };

        return Ok(result);
    }

    // https://github.com/BlueWallet/LndHub/blob/master/doc/Send-requirements.md#get-getpending
    [HttpGet("getpending")]
    public IActionResult GetPendingTransactions()
    {
        // There are no pending BTC transactions, so leave it as an empty implementation
        return Ok(new List<TransactionData>());
    }

    // https://github.com/BlueWallet/LndHub/blob/master/doc/Send-requirements.md#get-decodeinvoice
    [HttpGet("decodeinvoice")]
    public IActionResult DecodeInvoice([FromQuery] string invoice)
    {
        try
        {
            var bolt11 = _walletService.ParsePaymentRequest(invoice);
            var decoded = new DecodeInvoiceData
            {
                Destination = bolt11.GetPayeePubKey().ToString(),
                PaymentHash = bolt11.PaymentHash?.ToString(),
                Amount = bolt11.MinimumAmount,
                Timestamp = bolt11.Timestamp,
                Expiry = bolt11.ExpiryDate - bolt11.Timestamp,
                Description = bolt11.ShortDescription,
                DescriptionHash = bolt11.DescriptionHash
            };

            return Ok(decoded);
        }
        catch (Exception ex)
        {
            return Ok(new ErrorResponse(4, ex.Message));
        }
    }

    // https://github.com/getAlby/lightning-browser-extension/blob/f0b0ab9ad0b2dd6e60b864548fa39091ef81bbdc/src/extension/background-script/connectors/lndhub.ts#L249
    [HttpGet("checkpayment/{paymentHash}")]
    public async Task<IActionResult> CheckPayment(string paymentHash)
    {
        var result = new CheckPaymentResponse { Paid = false };
        try
        {
            result.Paid = await _walletService.IsPaid(paymentHash);
            return Ok(result);
        }
        catch (Exception)
        {
            return NotFound(result);
        }
    }

    // https://github.com/BlueWallet/LndHub/blob/master/doc/Send-requirements.md#post-addinvoice
    [HttpPost("addinvoice")]
    public async Task<IActionResult> AddInvoice(CreateInvoiceRequest request)
    {
        try
        {
            var req = new CreateLightningInvoiceRequest
            {
                Amount = request.Amount,
                Description = request.DescriptionHash != null ? request.DescriptionHash.ToString() : request.Memo,
                DescriptionHashOnly = request.DescriptionHash != null,
                Expiry = WalletService.ExpiryDefault,
                PrivateRouteHints = true
            };
            var transaction = await _walletService.Receive(Wallet, req, request.Memo);
            var invoice = ToInvoiceData(transaction);

            return Ok(invoice);
        }
        catch (Exception ex)
        {
            return Ok(new ErrorResponse(4, ex.Message));
        }
    }

    // https://github.com/BlueWallet/LndHub/blob/master/doc/Send-requirements.md#post-payinvoice
    [HttpPost("payinvoice")]
    public async Task<IActionResult> PayInvoice(PayInvoiceRequest request)
    {
        var wallet = await GetWalletWithTransactions();
        try
        {
            var transaction = await _walletService.Send(wallet, request.PaymentRequest);
            var result = ToPaymentResponse(transaction);

            return Ok(result);
        }
        catch (InsufficientBalanceException ex)
        {
            return Ok(new ErrorResponse(2, ex.Message));
        }
        catch (Exception ex)
        {
            return Ok(new ErrorResponse(4, ex.Message));
        }
    }

    private async Task<Wallet> GetWalletWithTransactions()
    {
        return await _walletRepository.GetWallet(new WalletsQuery
        {
            UserId = new[] { UserId },
            WalletId = new[] { WalletId },
            IncludeTransactions = true
        });
    }

    private TransactionData ToTransactionData(Transaction t)
    {
        var bolt11 = _walletService.ParsePaymentRequest(t.PaymentRequest);

        return new TransactionData
        {
            PaymentPreimage = bolt11.PaymentSecret?.ToString(),
            PaymentHash = bolt11.PaymentHash,
            Fee = t.RoutingFee,
            Value = t.AmountSettled.Abs(),
            Timestamp = t.CreatedAt,
            Memo = t.Description
        };
    }

    private PaymentResponse ToPaymentResponse(Transaction t)
    {
        var bolt11 = _walletService.ParsePaymentRequest(t.PaymentRequest);
        var error = t.Status switch
        {
            Transaction.StatusExpired => "Invoice expired",
            Transaction.StatusInvalid => "Invalid payment",
            Transaction.StatusCancelled => "Invoice cancelled",
            _ => "" // needs to be an empty string for compatibility across wallets
        };

        return new PaymentResponse
        {
            PaymentError = error,
            PaymentRequest = t.PaymentRequest,
            PaymentPreimage = bolt11.PaymentSecret,
            PaymentHash = bolt11.PaymentHash,
            Decoded = ToPaymentData(t),
            PaymentRoute = new PaymentRoute
            {
                Amount = t.AmountSettled.Abs(),
                Fee = t.RoutingFee
            }
        };
    }

    private PaymentData ToPaymentData(Transaction t)
    {
        var bolt11 = _walletService.ParsePaymentRequest(t.PaymentRequest);
        var expireTime = TimeSpan.FromSeconds((t.ExpiresAt - t.CreatedAt).TotalSeconds);
        var amount = t.AmountSettled.Abs();

        return new PaymentData
        {
            PaymentPreimage = bolt11.PaymentSecret,
            Destination = bolt11.GetPayeePubKey().ToString(),
            PaymentHash = bolt11.PaymentHash,
            Amount = amount,
            Description = t.Description,
            DescriptionHash = bolt11.DescriptionHash?.ToString(),
            ExpireTime = expireTime,
            Timestamp = t.CreatedAt
        };
    }

    private InvoiceData ToInvoiceData(Transaction t)
    {
        var bolt11 = _walletService.ParsePaymentRequest(t.PaymentRequest);
        var expireTime = TimeSpan.FromSeconds((t.ExpiresAt - t.CreatedAt).TotalSeconds);

        return new InvoiceData
        {
            Id = bolt11.Hash,
            Description = t.Description,
            AddIndex = Convert.ToInt32(t.CreatedAt.ToUnixTimeSeconds()), // fake it
            PaymentHash = bolt11.PaymentHash?.ToString(),
            PaymentRequest = t.PaymentRequest,
            IsPaid = t.IsPaid,
            ExpireTime = expireTime,
            Amount = t.AmountSettled ?? t.Amount,
            CreatedAt = t.CreatedAt,
        };
    }
}
