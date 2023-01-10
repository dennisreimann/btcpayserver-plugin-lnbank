using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AuthenticationSchemes = BTCPayServer.Abstractions.Constants.AuthenticationSchemes;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageWallet)]
public class DeleteModel : BasePageModel
{
    public Wallet Wallet { get; set; }

    public DeleteModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public async Task<IActionResult> OnGetAsync(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();

        try
        {
            await WalletRepository.RemoveWallet(Wallet);

            TempData[WellKnownTempData.SuccessMessage] = "Wallet removed.";
            return RedirectToPage("./Index");
        }
        catch (Exception e)
        {
            TempData[WellKnownTempData.ErrorMessage] = e.Message;
            return Page();
        }
    }
}
