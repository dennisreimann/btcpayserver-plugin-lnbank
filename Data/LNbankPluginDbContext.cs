using System;
using System.Linq;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LNbank.Data;

public class LNbankPluginDbContext : DbContext
{
    private readonly bool _designTime;

    public LNbankPluginDbContext(DbContextOptions<LNbankPluginDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<AccessKey> AccessKeys { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.LNbank");
        if (Database.IsSqlite() && !_designTime)
        {
            // SQLite does not have proper support for DateTimeOffset via Entity Framework Core, see the limitations
            // here: https://docs.microsoft.com/en-us/ef/core/providers/sqlite/limitations#query-limitations
            // To work around this, when the Sqlite database provider is used, all model properties of type DateTimeOffset
            // use the DateTimeOffsetToBinaryConverter
            // Based on: https://github.com/aspnet/EntityFrameworkCore/issues/10784#issuecomment-415769754
            // This only supports millisecond precision, but should be sufficient for most use cases.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.ClrType.GetProperties()
                    .Where(p => p.PropertyType == typeof(DateTimeOffset));
                foreach (var property in properties)
                {
                    modelBuilder
                        .Entity(entityType.Name)
                        .Property(property.Name)
                        .HasConversion(
                            new Microsoft.EntityFrameworkCore.Storage.ValueConversion.
                                DateTimeOffsetToBinaryConverter());
                }
            }
        }

        AccessKey.OnModelCreating(modelBuilder);
        Transaction.OnModelCreating(modelBuilder);
        Wallet.OnModelCreating(modelBuilder);
    }
}
