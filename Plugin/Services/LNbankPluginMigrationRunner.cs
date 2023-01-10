using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LNbankPluginMigrationRunner : IHostedService
{
    private readonly LNbankPluginDbContextFactory _dbContextFactory;
    private readonly ISettingsRepository _settingsRepository;
    private readonly WalletService _walletService;

    public LNbankPluginMigrationRunner(
        LNbankPluginDbContextFactory testPluginDbContextFactory,
        ISettingsRepository settingsRepository,
        WalletService walletService)
    {
        _dbContextFactory = testPluginDbContextFactory;
        _settingsRepository = settingsRepository;
        _walletService = walletService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LNbankPluginDataMigrationHistory settings =
            await _settingsRepository.GetSettingAsync<LNbankPluginDataMigrationHistory>() ??
            new LNbankPluginDataMigrationHistory();

        await using LNbankPluginDbContext ctx = _dbContextFactory.CreateContext();
        await using LNbankPluginDbContext dbContext = _dbContextFactory.CreateContext();
        await ctx.Database.MigrateAsync(cancellationToken);

        if (!settings.ExtendedAccessKeysWithUserId)
        {
            List<AccessKey> accessKeys = await dbContext.AccessKeys
                .Include(a => a.Wallet)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            foreach (AccessKey accessKey in accessKeys)
            {
                accessKey.UserId = accessKey.Wallet?.UserId;
                dbContext.Update(accessKey);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            settings.ExtendedAccessKeysWithUserId = true;
            await _settingsRepository.UpdateSetting(settings);
        }

        if (!settings.ExtendedTransactionsWithPaymentHash)
        {
            List<Transaction> transactions = await dbContext.Transactions.ToListAsync(cancellationToken);
            foreach (Transaction transaction in transactions)
            {
                BOLT11PaymentRequest bolt11 = _walletService.ParsePaymentRequest(transaction.PaymentRequest);
                transaction.PaymentHash = bolt11.PaymentHash?.ToString();
                dbContext.Update(transaction);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            settings.ExtendedTransactionsWithPaymentHash = true;
            await _settingsRepository.UpdateSetting(settings);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public class LNbankPluginDataMigrationHistory
    {
        public bool ExtendedAccessKeysWithUserId { get; set; }
        public bool ExtendedTransactionsWithPaymentHash { get; set; }
    }
}
