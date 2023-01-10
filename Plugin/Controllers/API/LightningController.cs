using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client.Models;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NBitcoin;
using Transaction = BTCPayServer.Plugins.LNbank.Data.Models.Transaction;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

[ApiController]
[Route("~/plugins/lnbank/api/[controller]")]
[Authorize(AuthenticationSchemes = LNbankAuthenticationSchemes.AccessKey)]
public class LightningController : ControllerBase
{
    private string UserId => User.Claims.First(c => c.Type == _identityOptions.CurrentValue.ClaimsIdentity.UserIdClaimType).Value;
    private string WalletId => User.Claims.First(c => c.Type == "WalletId").Value;
    private Wallet Wallet => (Wallet)ControllerContext.HttpContext.Items.TryGet("BTCPAY.LNBANK.WALLET");

    private readonly BTCPayService _btcpayService;
    private readonly WalletService _walletService;
    private readonly WalletRepository _walletRepository;
    private readonly IOptionsMonitor<IdentityOptions> _identityOptions;

    public LightningController(
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

    // --- Custom methods ---

    [HttpPost("invoice")]
    public async Task<IActionResult> CreateLightningInvoice(CreateLightningInvoiceRequest req)
    {
        if (Wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        try
        {
            var memo = !req.DescriptionHashOnly && !string.IsNullOrEmpty(req.Description) ? req.Description : null;
            var transaction = await _walletService.Receive(Wallet, req, memo);
            var data = ToLightningInvoiceData(transaction);
            return Ok(data);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    [HttpPost("pay")]
    public async Task<IActionResult> Pay(LightningInvoicePayRequest req)
    {
        if (Wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        var (bolt11, lnurlPay) = await _walletService.GetPaymentRequests(req.PaymentRequest);
        var amountRequired = bolt11 != null
            ? bolt11.MinimumAmount == LightMoney.Zero
            : lnurlPay.MinSendable != lnurlPay.MaxSendable;
        var amount = amountRequired ? req.Amount : null;
        if (amountRequired && amount == null)
        {
            ModelState.AddModelError(nameof(req.Amount), "Amount is required to pay an invoice with unspecified amount");
            return this.CreateValidationError(ModelState);
        }

        try
        {
            // if not present, resolve BOLT11 from LNURLPay request
            bolt11 ??= await _walletService.GetBolt11(lnurlPay, amount, req.Comment);

            // load wallet including transactions to do the balance check
            var wallet = await GetWalletWithTransactions(Wallet.WalletId);
            var description = req.Description ?? bolt11.ShortDescription;
            var transaction = await _walletService.Send(wallet, bolt11, description, amount);
            var details = transaction.IsSettled
                ? new PayDetails { TotalAmount = transaction.Amount, FeeAmount = transaction.RoutingFee }
                : null;
            var response = new PayResponse(PayResult.Ok, details);
            return Ok(response);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetLightningNodeBalance()
    {
        if (Wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        try
        {
            // load wallet including transactions to see the balance
            var wallet = await GetWalletWithTransactions(Wallet.WalletId);
            var offchain = new OffchainBalance { Local = wallet.Balance };
            var balance = new LightningNodeBalance(null, offchain);
            return Ok(balance);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    // ---- General methods ---

    [HttpGet("info")]
    public async Task<ActionResult<LightningNodeInformationData>> GetLightningNodeInfo()
    {
        var info = await _btcpayService.GetLightningNodeInfo();
        return Ok(info);
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> ListLightningInvoices(
        [FromQuery(Name = "pending_only")] bool? pendingOnly,
        [FromQuery(Name = "offset_index")] long? offsetIndex)
    {
        try
        {
            var onlyPending = pendingOnly is true;
            var query = new TransactionsQuery
            {
                UserId = UserId,
                WalletId = WalletId,
                IncludingPending = true,
                IncludingPaid = !onlyPending,
                IncludingExpired = !onlyPending,
                IncludingInvalid = !onlyPending,
                IncludingCancelled = !onlyPending,
                Type = TransactionType.Invoice
            };
            var offset = Convert.ToInt32(offsetIndex);
            var transactions = (await _walletRepository.GetTransactions(query)).Skip(offset);

            var invoices = transactions.Select(ToLightningInvoiceData);
            return Ok(invoices);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    [HttpGet("invoice/{invoiceId}")]
    public async Task<IActionResult> GetLightningInvoice(string invoiceId)
    {
        try
        {
            var transaction = await _walletRepository.GetTransaction(new TransactionQuery
            {
                UserId = UserId,
                WalletId = WalletId,
                InvoiceId = invoiceId
            });
            if (transaction == null)
                return this.CreateAPIError(404, "invoice-not-found", "The invoice was not found");

            var invoice = ToLightningInvoiceData(transaction);
            return Ok(invoice);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    [HttpGet("payments")]
    public async Task<IActionResult> ListLightningPayments(
        [FromQuery(Name = "include_pending")] bool? includePending,
        [FromQuery(Name = "offset_index")] long? offsetIndex)
    {
        try
        {
            var includingPending = includePending is true;
            var query = new TransactionsQuery
            {
                UserId = UserId,
                WalletId = WalletId,
                IncludingPending = includingPending,
                IncludingPaid = true,
                IncludingExpired = true,
                IncludingInvalid = true,
                IncludingCancelled = true,
                Type = TransactionType.Payment
            };
            var offset = Convert.ToInt32(offsetIndex);
            var transactions = (await _walletRepository.GetTransactions(query)).Skip(offset);

            var payments = transactions.Select(ToLightningPaymentData);
            return Ok(payments);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    [HttpGet("payment/{paymentHash}")]
    public async Task<IActionResult> GetLightningPayment(string paymentHash)
    {
        try
        {
            var payment = await _btcpayService.GetLightningPayment(paymentHash);
            if (payment == null)
                return this.CreateAPIError(404, "payment-not-found", "The payment was not found");

            return Ok(payment);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    [HttpDelete("invoice/{invoiceId}")]
    public async Task<ActionResult<LightningInvoiceData>> CancelLightningInvoice(string invoiceId)
    {
        await _walletService.Cancel(invoiceId);
        return Ok();
    }

    [HttpGet("channels")]
    public async Task<ActionResult<IEnumerable<LightningChannelData>>> ListLightningChannels()
    {
        var list = await _btcpayService.ListLightningChannels();
        return Ok(list);
    }

    [HttpPost("channels")]
    public async Task<ActionResult<string>> OpenLightningChannel(OpenLightningChannelRequest req)
    {
        await _btcpayService.OpenLightningChannel(req);
        return Ok();
    }

    [HttpPost("connect")]
    public async Task<ActionResult> ConnectToLightningNode(ConnectToNodeRequest req)
    {
        await _btcpayService.ConnectToLightningNode(req);
        return Ok();
    }

    [HttpPost("deposit-address")]
    public async Task<ActionResult<string>> GetLightningDepositAddress()
    {
        var address = await _btcpayService.GetLightningDepositAddress();
        return Ok(address);
    }

    private LightningInvoiceData ToLightningInvoiceData(Transaction transaction) =>
        new()
        {
            Amount = transaction.Amount,
            Id = transaction.InvoiceId,
            Status = transaction.LightningInvoiceStatus,
            AmountReceived = transaction.AmountSettled,
            PaidAt = transaction.PaidAt,
            BOLT11 = transaction.PaymentRequest,
            ExpiresAt = transaction.ExpiresAt
        };

    private LightningPaymentData ToLightningPaymentData(Transaction transaction) =>
        new()
        {
            Id = transaction.InvoiceId,
            PaymentHash = transaction.PaymentHash,
            Status = transaction.LightningPaymentStatus,
            BOLT11 = transaction.PaymentRequest,
            Preimage = _walletService.ParsePaymentRequest(transaction.PaymentRequest).PaymentSecret?.ToString(),
            CreatedAt = transaction.PaidAt,
            TotalAmount = transaction.AmountSettled,
            FeeAmount = transaction.RoutingFee,
        };

    private async Task<Wallet> GetWalletWithTransactions(string walletId)
    {
        return await _walletRepository.GetWallet(new WalletsQuery
        {
            WalletId = new[] { walletId },
            IncludeTransactions = true
        });
    }
}
