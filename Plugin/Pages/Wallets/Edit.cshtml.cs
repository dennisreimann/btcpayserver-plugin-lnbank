using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageWallet)]
public class EditModel : BasePageModel
{
    public Wallet Wallet { get; set; }

    public EditModel(
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
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();

        if (await TryUpdateModelAsync(Wallet, "wallet", w => w.Name))
        {
            await WalletRepository.AddOrUpdateWallet(Wallet);
            TempData[WellKnownTempData.SuccessMessage] = "Wallet successfully updated.";
            return RedirectToPage("./Wallet", new { walletId });
        }

        return Page();
    }
}
