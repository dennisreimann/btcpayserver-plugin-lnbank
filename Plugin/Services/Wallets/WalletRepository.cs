using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Plugins.LNbank.Data;
using BTCPayServer.Plugins.LNbank.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class WalletRepository
{
    private readonly LNbankPluginDbContextFactory _dbContextFactory;

    public WalletRepository(LNbankPluginDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IEnumerable<Wallet>> GetWallets(WalletsQuery query)
    {
        await using LNbankPluginDbContext dbContext = _dbContextFactory.CreateContext();
        List<Wallet> wallets = await FilterWallets(dbContext.Wallets.AsQueryable(), query).ToListAsync();
        return wallets.Select(wallet =>
        {
            AccessKey key = wallet.AccessKeys.FirstOrDefault(ak => query.UserId.Contains(ak.UserId));
            if (key != null)
                wallet.AccessLevel = key.Level;
            else if (query.UserId.Contains(wallet.UserId))
                wallet.AccessLevel = AccessLevel.Admin;
            return wallet;
        });
    }

    private IQueryable<Wallet> FilterWallets(IQueryable<Wallet> queryable, WalletsQuery query)
    {
        if (query.UserId != null)
            queryable = queryable
                .Include(w => w.AccessKeys).AsNoTracking()
                .Where(w =>
                    // Owner
                    query.UserId.Contains(w.UserId) ||
                    // Access key holder
                    w.AccessKeys.Any(ak => query.UserId.Contains(ak.UserId)));

        if (query.AccessKey != null)
            queryable = queryable
                .Include(w => w.AccessKeys).AsNoTracking()
                .Where(w => w.AccessKeys.Any(key => query.AccessKey.Contains(key.Key)));

        if (query.WalletId != null)
            queryable = queryable.Where(wallet => query.WalletId.Contains(wallet.WalletId));

        if (query.IncludeTransactions)
            queryable = queryable.Include(w => w.Transactions).AsNoTracking();

        if (query.IncludeAccessKeys)
            queryable = queryable.Include(w => w.AccessKeys).AsNoTracking();

        return queryable;
    }

    public async Task<Wallet> GetWallet(WalletsQuery query)
    {
        await using LNbankPluginDbContext dbContext = _dbContextFactory.CreateContext();
        Wallet wallet = await FilterWallets(dbContext.Wallets.AsQueryable(), query).FirstOrDefaultAsync();
        if (wallet == null)
            return null;

        if (query.UserId != null)
        {
            AccessKey key = wallet.AccessKeys.FirstOrDefault(ak => query.UserId.Contains(ak.UserId));
            if (key != null)
                wallet.AccessLevel = key.Level;
            else if (query.UserId.Contains(wallet.UserId))
                wallet.AccessLevel = AccessLevel.Admin;
        }
        else if (query.AccessKey != null)
        {
            AccessKey key = wallet.AccessKeys.FirstOrDefault(ak => query.AccessKey.Contains(ak.Key));
            if (key != null)
                wallet.AccessLevel = key.Level;
        }

        return wallet;
    }

    public async Task<Wallet> AddOrUpdateWallet(Wallet wallet)
    {
        await using LNbankPluginDbContext dbContext = _dbContextFactory.CreateContext();

        EntityEntry entry;
        if (string.IsNullOrEmpty(wallet.WalletId))
        {
            wallet.AccessKeys ??= new List<AccessKey>();
            wallet.AccessKeys.Add(new AccessKey { UserId = wallet.UserId, Level = AccessLevel.Admin });
            entry = await dbContext.Wallets.AddAsync(wallet);
        }
        else
        {
            entry = dbContext.Update(wallet);
        }

        await dbContext.SaveChangesAsync();

        return (Wallet)entry.Entity;
    }

    public async Task<AccessKey> AddOrUpdateAccessKey(string walletId, string userId, AccessLevel level)
    {
        await using LNbankPluginDbContext dbContext = _dbContextFactory.CreateContext();
        AccessKey accessKey =
            await dbContext.AccessKeys.FirstOrDefaultAsync(a => a.WalletId == walletId && a.UserId == userId);

        if (accessKey == null)
        {
            accessKey = new AccessKey { UserId = userId, WalletId = walletId, Level = level };
            await dbContext.AccessKeys.AddAsync(accessKey);
        }
        else if (accessKey.Level != level)
        {
            accessKey.Level = level;
            dbContext.Update(accessKey);
        }

        await dbContext.SaveChangesAsync();

        return accessKey;
    }

    public async Task DeleteAccessKey(string walletId, string key)
    {
        await using LNbankPluginDbContext dbContext = _dbContextFactory.CreateContext();
        AccessKey accessKey = await dbContext.AccessKeys.FirstAsync(a => a.WalletId == walletId && a.Key == key);

        dbContext.AccessKeys.Remove(accessKey);
        await dbContext.SaveChangesAsync();
    }

    public async Task RemoveWallet(Wallet wallet)
    {
        if (wallet.Balance > 0)
            throw new Exception("This wallet still has a balance.");

        wallet.IsSoftDeleted = true;
        await AddOrUpdateWallet(wallet);
    }

    public async Task<IEnumerable<Transaction>> GetPendingTransactions()
    {
        return await GetTransactions(new TransactionsQuery
        {
            IncludingPending = true,
            IncludingExpired = false,
            IncludingInvalid = false,
            IncludingCancelled = false,
            IncludingPaid = false
        });
    }

    public async Task<Transaction> GetTransaction(TransactionQuery query)
    {
        await using LNbankPluginDbContext dbContext = _dbContextFactory.CreateContext();
        IQueryable<Transaction> queryable = dbContext.Transactions.AsQueryable();

        if (query.WalletId != null)
        {
            WalletsQuery walletQuery = new WalletsQuery
            {
                WalletId = new[] { query.WalletId },
                IncludeTransactions = true
            };

            if (query.UserId != null)
                walletQuery.UserId = new[] { query.UserId };

            Wallet wallet = await GetWallet(walletQuery);

            if (wallet == null)
                return null;

            queryable = wallet.Transactions.AsQueryable();
        }

        if (query.InvoiceId != null)
            queryable = queryable.Where(t => t.InvoiceId == query.InvoiceId);
        else if (query.HasInvoiceId)
            queryable = queryable.Where(t => t.InvoiceId != null);

        if (query.TransactionId != null)
            queryable = queryable.Where(t => t.TransactionId == query.TransactionId);

        if (query.PaymentRequest != null)
            queryable = queryable.Where(t => t.PaymentRequest == query.PaymentRequest);

        if (query.PaymentHash != null)
            queryable = queryable.Where(t => t.PaymentHash == query.PaymentHash);

        return queryable.FirstOrDefault();
    }

    public async Task<Transaction> UpdateTransaction(Transaction transaction)
    {
        await using LNbankPluginDbContext dbContext = _dbContextFactory.CreateContext();
        EntityEntry<Transaction> entry = dbContext.Entry(transaction);
        entry.State = EntityState.Modified;

        await dbContext.SaveChangesAsync();

        return entry.Entity;
    }

    public async Task<IEnumerable<Transaction>> GetTransactions(TransactionsQuery query)
    {
        await using LNbankPluginDbContext dbContext = _dbContextFactory.CreateContext();
        IQueryable<Transaction> queryable = dbContext.Transactions.AsQueryable();

        if (query.UserId != null)
            query.IncludeWallet = true;

        if (query.WalletId != null)
            queryable = queryable.Where(t => t.WalletId == query.WalletId);

        if (query.IncludeWallet)
            queryable = queryable.Include(t => t.Wallet).AsNoTracking();

        if (query.UserId != null)
            queryable = queryable.Where(t => t.Wallet.UserId == query.UserId);

        if (!query.IncludingPaid)
            queryable = queryable.Where(t => t.PaidAt == null);

        if (!query.IncludingPending)
            queryable = queryable.Where(t => t.PaidAt != null);

        if (!query.IncludingCancelled)
            queryable = queryable.Where(t => t.ExplicitStatus != Transaction.StatusCancelled);

        if (!query.IncludingInvalid)
            queryable = queryable.Where(t => t.ExplicitStatus != Transaction.StatusInvalid);

        if (!query.IncludingExpired)
            queryable = queryable.Where(t => t.ExplicitStatus != Transaction.StatusExpired);

        queryable = query.Type switch
        {
            TransactionType.Invoice => queryable.Where(t => t.InvoiceId != null),
            TransactionType.Payment => queryable.Where(t => t.InvoiceId == null),
            _ => queryable
        };

        return await queryable.ToListAsync();
    }
}
