using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.LNbank.Authentication;

public class LNbankAuthenticationOptions : AuthenticationSchemeOptions
{
}

public class LNbankAuthenticationHandler : AuthenticationHandler<LNbankAuthenticationOptions>
{
    private readonly IOptionsMonitor<IdentityOptions> _identityOptions;
    private readonly WalletRepository _walletRepository;

    public LNbankAuthenticationHandler(
        IOptionsMonitor<IdentityOptions> identityOptions,
        IOptionsMonitor<LNbankAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        WalletRepository walletRepository) : base(options, logger, encoder, clock)
    {
        _identityOptions = identityOptions;
        _walletRepository = walletRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string authHeader = Context.Request.Headers["Authorization"];
        if (authHeader == null || !authHeader.StartsWith("Bearer ", StringComparison.InvariantCultureIgnoreCase))
            return AuthenticateResult.NoResult();

        var apiKey = authHeader.Substring("Bearer ".Length);
        var wallet = await _walletRepository.GetWallet(new WalletsQuery
        {
            AccessKey = new[] { apiKey },
            IncludeTransactions = false
        });
        if (wallet is null)
        {
            return AuthenticateResult.Fail("Incorrect wallet key");
        }

        var accessKey = wallet.AccessKeys.First(a => a.Key == apiKey);
        var claims = new List<Claim>
        {
            new(_identityOptions.CurrentValue.ClaimsIdentity.UserIdClaimType, wallet.UserId),
            new("WalletId", wallet.WalletId),
            new("AccessKey", accessKey.Key),
            new("AccessLevel", accessKey.Level.ToString())
        };
        var claimsIdentity = new ClaimsIdentity(claims, LNbankAuthenticationSchemes.AccessKey);
        var principal = new ClaimsPrincipal(claimsIdentity);
        var ticket = new AuthenticationTicket(principal, LNbankAuthenticationSchemes.AccessKey);
        Context.Items.Add("BTCPAY.LNBANK.WALLET", wallet);
        return AuthenticateResult.Success(ticket);
    }
}
