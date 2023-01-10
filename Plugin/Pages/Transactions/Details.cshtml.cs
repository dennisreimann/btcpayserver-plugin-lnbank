using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Transactions;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanViewWallet)]
public class DetailsModel : BasePageModel
{
    public Wallet Wallet { get; set; }
    public Transaction Transaction { get; set; }

    public DetailsModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public async Task<IActionResult> OnGetAsync(string walletId, string transactionId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null)
            return NotFound();

        Transaction = Wallet.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);

        if (Transaction == null)
            return NotFound();

        return Page();
    }
}
