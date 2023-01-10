using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using LNURL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AuthenticationSchemes = BTCPayServer.Abstractions.Constants.AuthenticationSchemes;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanSendMoney)]
public class SendModel : BasePageModel
{
    private readonly ILogger _logger;

    public Wallet Wallet { get; set; }
    public BOLT11PaymentRequest Bolt11 { get; set; }
    public LNURLPayRequest LnurlPay { get; set; }

    [BindProperty]
    [DisplayName("Payment Request, LNURL or Lightning Address")]
    [Required]
    public string Destination { get; set; }

    [BindProperty]
    [DisplayName("BOLT11 Payment Request")]
    public string PaymentRequest { get; set; } // this is set from the parsed Destination

    [BindProperty]
    [DisplayName("Amount")]
    [Range(1, 2100000000000)]
    public long? ExplicitAmount { get; set; }

    [BindProperty]
    public string Description { get; set; }

    [BindProperty]
    public string Comment { get; set; }

    public SendModel(
        ILogger<SendModel> logger,
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService)
    {
        _logger = logger;
    }

    public async Task<IActionResult> OnGet(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostDecodeAsync(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();
        if (!ModelState.IsValid)
            return Page();

        try
        {
            Destination = Destination.Trim();
            (Bolt11, LnurlPay) = await WalletService.GetPaymentRequests(Destination);

            if (Bolt11 != null)
            {
                Description = Bolt11.ShortDescription;
                PaymentRequest = Bolt11.ToString();

                if (Bolt11.MinimumAmount == LightMoney.Zero)
                {
                    ExplicitAmount = 1;
                }
            }
            else
            {
                Description = GetLnurlMetadata("text/plain");

                var isDefinedAmount = LnurlPay.MinSendable == LnurlPay.MaxSendable;
                var isCommentAllowed = LnurlPay.CommentAllowed is > 0;

                if (!isDefinedAmount)
                {
                    ExplicitAmount = (long)LnurlPay.MinSendable.ToUnit(LightMoneyUnit.Satoshi);
                }

                // no further interaction required, get the BOLT11
                if (isDefinedAmount && !isCommentAllowed)
                {
                    Bolt11 = await WalletService.GetBolt11(LnurlPay);
                    PaymentRequest = Bolt11.ToString();
                }
            }

            // Payment request is not present in LNURL case with further interaction required
            if (PaymentRequest != null)
            {
                await WalletService.ValidatePaymentRequest(PaymentRequest);
            }
        }
        catch (Exception exception)
        {
            TempData[WellKnownTempData.ErrorMessage] = exception.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();
        if (!ModelState.IsValid)
            return Page();

        if (string.IsNullOrEmpty(PaymentRequest))
        {
            // LNURL case with further interaction required 
            try
            {
                Destination = Destination.Trim();
                (Bolt11, LnurlPay) = await WalletService.GetPaymentRequests(Destination);

                if (Bolt11 == null)
                {
                    var isDefinedAmount = LnurlPay.MinSendable == LnurlPay.MaxSendable;
                    var explicitAmount = ExplicitAmount.HasValue ? LightMoney.Satoshis(ExplicitAmount.Value) : null;
                    var amount = isDefinedAmount ? LnurlPay.MinSendable : explicitAmount;

                    if (amount == null)
                    {
                        ModelState.AddModelError(nameof(ExplicitAmount), "Amount must be defined");
                    }
                    else
                    {
                        // Everything is ok, get the BOLT11
                        Bolt11 = await WalletService.GetBolt11(LnurlPay, amount, Comment);
                        PaymentRequest = Bolt11.ToString();
                    }
                }
            }
            catch (Exception exception)
            {
                TempData[WellKnownTempData.ErrorMessage] = exception.Message;
            }
        }

        // Abort if there's still no payment request - from here on we require a BOLT11
        if (string.IsNullOrEmpty(PaymentRequest))
        {
            TempData[WellKnownTempData.ErrorMessage] ??= "A valid BOLT11 Payment Request is required";
            return Page();
        }

        Bolt11 ??= WalletService.ParsePaymentRequest(PaymentRequest!);

        try
        {
            var explicitAmount = ExplicitAmount.HasValue ? LightMoney.Satoshis(ExplicitAmount.Value) : null;
            var transaction = await WalletService.Send(Wallet, Bolt11, Description, explicitAmount);
            TempData[WellKnownTempData.SuccessMessage] = transaction.IsPending
                ? "Payment successfully sent, awaiting settlement."
                : "Payment successfully sent and settled.";
            return RedirectToPage("./Wallet", new { walletId });
        }
        catch (Exception exception)
        {
            _logger.LogError("Payment failed: {Message}", exception.Message);

            TempData[WellKnownTempData.ErrorMessage] = $"Payment failed: {exception.Message}";
        }

        return Page();
    }

    internal string GetLnurlMetadata(string key)
    {
        var pair = LnurlPay?.ParsedMetadata
            .Find(pair => pair.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
        return pair?.Value;
    }
}
