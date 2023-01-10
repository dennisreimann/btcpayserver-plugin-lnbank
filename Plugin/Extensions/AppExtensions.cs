using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.LNbank.Extensions;

public static class AppExtensions
{
    public static void AddAppServices(this IServiceCollection services)
    {
        services.AddHostedService<LNbankPluginMigrationRunner>();
        services.AddHostedService<LightningInvoiceWatcher>();
        services.AddSingleton<BTCPayService>();
        services.AddSingleton<LNURLService>();
        services.AddSingleton<WalletService>();
        services.AddSingleton<WalletRepository>();
        services.AddSingleton<ISwaggerProvider, LNbankSwaggerProvider>();
    }
}
