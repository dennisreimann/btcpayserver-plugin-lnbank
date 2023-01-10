using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class IndexModel : BasePageModel
{
    public IEnumerable<Wallet> Wallets { get; set; }
    public LightMoney TotalBalance { get; set; }

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public async Task<IActionResult> OnGetAsync(string walletId)
    {
        Wallets = await WalletRepository.GetWallets(new WalletsQuery
        {
            UserId = new[] { UserId },
            IncludeTransactions = true
        });

        var list = Wallets.ToList();
        if (!list.Any())
        {
            return RedirectToPage("./Create");
        }

        TotalBalance = Wallets
            .Select(w => w.Balance)
            .Aggregate((res, current) => res + current);

        return Page();
    }
}
