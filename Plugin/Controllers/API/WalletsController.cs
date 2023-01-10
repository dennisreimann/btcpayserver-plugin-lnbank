using System;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.API;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WalletData = BTCPayServer.Plugins.LNbank.Data.API.WalletData;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

[ApiController]
[Route("~/api/v1/lnbank/[controller]")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
public class WalletsController : ControllerBase
{
    private readonly WalletRepository _walletRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public WalletsController(UserManager<ApplicationUser> userManager, WalletRepository walletRepository)
    {
        _userManager = userManager;
        _walletRepository = walletRepository;
    }

    [HttpGet("")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyProfile)]
    public async Task<IActionResult> GetWallets()
    {
        var wallets = await _walletRepository.GetWallets(new WalletsQuery
        {
            UserId = new[] { GetUserId() },
            IncludeTransactions = true
        });

        return Ok(wallets.Select(FromModel));
    }

    [HttpPost("")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyProfile)]
    public async Task<IActionResult> CreateWallet(EditWalletRequest request)
    {
        var validationResult = Validate(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        var wallet = new Wallet
        {
            UserId = GetUserId(),
            Name = request.Name
        };

        var entry = await _walletRepository.AddOrUpdateWallet(wallet);

        return Ok(FromModel(entry));
    }

    [HttpGet("{walletId}")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = LNbankPolicies.CanViewWallet)]
    public async Task<IActionResult> GetWallet(string walletId)
    {
        var wallet = await FetchWallet(walletId);
        return wallet == null
            ? this.CreateAPIError(404, "wallet-not-found", "The wallet was not found")
            : Ok(FromModel(wallet));
    }

    [HttpPut("{walletId}")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = LNbankPolicies.CanManageWallet)]
    public async Task<IActionResult> UpdateWallet(string walletId, EditWalletRequest request)
    {
        var validationResult = Validate(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        var wallet = await _walletRepository.GetWallet(new WalletsQuery
        {
            UserId = new[] { GetUserId() },
            WalletId = new[] { walletId },
            IncludeTransactions = true,
            IncludeAccessKeys = true
        });

        if (wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        wallet.Name = request.Name;

        var entry = await _walletRepository.AddOrUpdateWallet(wallet);

        return Ok(FromModel(entry));
    }

    [HttpDelete("{walletId}")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = LNbankPolicies.CanManageWallet)]
    public async Task<IActionResult> DeleteWallet(string walletId)
    {
        var wallet = await FetchWallet(walletId);
        if (wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        try
        {
            await _walletRepository.RemoveWallet(wallet);
            return Ok();
        }
        catch (Exception e)
        {
            return this.CreateAPIError("wallet-not-empty", e.Message);
        }
    }

    private async Task<Wallet> FetchWallet(string walletId) =>
        await _walletRepository.GetWallet(new WalletsQuery
        {
            UserId = new[] { GetUserId() },
            WalletId = new[] { walletId },
            IncludeTransactions = true
        });

    private IActionResult Validate(EditWalletRequest request)
    {
        if (request is null)
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(request.Name))
            ModelState.AddModelError(nameof(request.Name), "Name is missing");
        else if (request.Name.Length is < 1 or > 50)
            ModelState.AddModelError(nameof(request.Name), "Name can only be between 1 and 50 characters");

        return !ModelState.IsValid ? this.CreateValidationError(ModelState) : null;
    }

    private WalletData FromModel(Wallet model) =>
        new()
        {
            Id = model.WalletId,
            Name = model.Name,
            CreatedAt = model.CreatedAt,
            Balance = model.Balance,
            AccessKey = model.AccessKeys.FirstOrDefault(ak => ak.UserId == GetUserId())?.Key
        };

    private string GetUserId() => _userManager.GetUserId(User);
}
