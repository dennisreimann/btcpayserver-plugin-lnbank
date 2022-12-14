using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Transactions;

[AllowAnonymous]
public class ShareModel : BasePageModel
{
    public Transaction Transaction { get; set; }

    public ShareModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public async Task<IActionResult> OnGetAsync(string transactionId)
    {
        Transaction = await WalletRepository.GetTransaction(new TransactionQuery
        {
            TransactionId = transactionId
        });

        if (Transaction == null || Transaction.IsExpired)
            return NotFound();

        return Page();
    }
}
