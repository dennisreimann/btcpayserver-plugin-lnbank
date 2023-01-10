using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.LNbank.Extensions;

public static class AuthExtensions
{
    public static void AddAppAuthentication(this IServiceCollection services)
    {
        var builder = new AuthenticationBuilder(services);
        builder.AddScheme<LNbankAuthenticationOptions, LNbankAuthenticationHandler>(LNbankAuthenticationSchemes.AccessKey,
            _ => { });
    }

    public static void AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
        {
            foreach (var policy in LNbankPolicies.AllPolicies)
            {
                opts.AddPolicy(policy, policyBuilder => policyBuilder
                    .AddRequirements(new PolicyRequirement(policy)));
            }
        });
    }
}
