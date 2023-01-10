using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.LNbank.Data;
using BTCPayServer.Plugins.LNbank.Extensions;
using BTCPayServer.Plugins.LNbank.Hooks;
using BTCPayServer.Plugins.LNbank.Hubs;
using BTCPayServer.Plugins.LNbank.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.LNbank;

public class LNbankPlugin : BaseBTCPayServerPlugin
{
    public override string Name { get; } = "LNbank";
    public override string Identifier { get; } = "BTCPayServer.Plugins.LNbank";

    public override string Description { get; } =
        "Use the BTCPay Server Lightning node in custodial mode and give users access via custodial layer 3 wallets.";

    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=1.7.3.0" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("LNbankNavExtension", "header-nav"));
        services.AddSingleton<IUIExtension>(new UIExtension("LNPaymentMethodSetupTabhead",
            "ln-payment-method-setup-tabhead"));
        services.AddSingleton<IUIExtension>(new UIExtension("LNPaymentMethodSetupTab", "ln-payment-method-setup-tab"));

        services.AddSingleton<IPluginHookFilter, AuthorizationRequirementHandler>();

        services.AddSingleton<LNbankPluginDbContextFactory>();
        services.AddDbContext<LNbankPluginDbContext>((provider, o) =>
        {
            LNbankPluginDbContextFactory factory = provider.GetRequiredService<LNbankPluginDbContextFactory>();
            factory.ConfigureBuilder(o);
        });
        services.AddAppServices();
        services.AddAppAuthentication();
        services.AddAppAuthorization();
    }

    public override void Execute(IApplicationBuilder applicationBuilder,
        IServiceProvider applicationBuilderApplicationServices)
    {
        base.Execute(applicationBuilder, applicationBuilderApplicationServices);

        applicationBuilder.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<TransactionHub>("/plugins/lnbank/hubs/transaction");
        });
    }
}
