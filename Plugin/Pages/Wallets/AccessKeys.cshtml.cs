using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
public class AccessKeysModel : BasePageModel
{
    public Wallet Wallet { get; set; }

    [BindProperty]
    public AccessKeyViewModel AccessKey { get; set; }
    public List<AccessKeyViewModel> AccessKeys { get; set; }

    public class AccessKeyViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        public string Key { get; set; }
        public AccessLevel Level { get; set; } = AccessLevel.ReadOnly;
    }

    public AccessKeysModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public async Task<IActionResult> OnGetAsync(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();

        AccessKeys = await GetAccessKeyVMs(Wallet.AccessKeys);

        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();

        AccessKeys = await GetAccessKeyVMs(Wallet.AccessKeys);
        if (!ModelState.IsValid)
            return Page();

        var user = await UserManager.FindByEmailAsync(AccessKey.Email);
        if (user == null)
        {
            ModelState.AddModelError(nameof(AccessKey.Email), "User not found");
            return Page();
        }

        if (!Enum.IsDefined(AccessKey.Level))
        {
            ModelState.AddModelError(nameof(AccessKey.Level), "Invalid access level");
            return Page();
        }

        await WalletRepository.AddOrUpdateAccessKey(Wallet.WalletId, user.Id, AccessKey.Level);
        TempData[WellKnownTempData.SuccessMessage] = "Access key added successfully.";
        return RedirectToPage("./AccessKeys", new { walletId });
    }

    public async Task<IActionResult> OnPostRemoveAsync(string walletId, string key)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();

        try
        {
            await WalletRepository.DeleteAccessKey(Wallet.WalletId, key);

            TempData[WellKnownTempData.SuccessMessage] = "Access key removed successfully.";
            return RedirectToPage("./AccessKeys", new { walletId });
        }
        catch (Exception)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Failed to remove user.";
        }

        AccessKeys = await GetAccessKeyVMs(Wallet.AccessKeys);
        return Page();
    }

    private async Task<List<AccessKeyViewModel>> GetAccessKeyVMs(ICollection<AccessKey> accessKeys)
    {
        var list = new List<AccessKeyViewModel>();
        foreach (var accessKey in accessKeys)
        {
            var user = await UserManager.FindByIdAsync(accessKey.UserId);
            list.Add(new AccessKeyViewModel
            {
                Key = accessKey.Key,
                Email = user.Email,
                Level = accessKey.Level
            });
        }
        return list;
    }
}
