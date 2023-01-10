using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace BTCPayServer.Plugins.LNbank.Data.Models;

public enum AccessLevel
{
    [Display(Name = "Read-only")]
    ReadOnly,

    [Display(Name = "Create invoices")]
    Invoice,

    [Display(Name = "Send money")]
    Send,
    Admin
}

public class AccessKey
{
    [Key] public string Key { get; set; } = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));

    // Relations
    [DisplayName("Wallet ID")]
    public string WalletId { get; set; }
    public Wallet Wallet { get; set; }

    [DisplayName("User ID")]
    public string UserId { get; set; }

    // Properties
    public AccessLevel Level { get; set; }

    internal static void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<AccessKey>()
            .HasIndex(o => new { o.WalletId, o.UserId })
            .IsUnique();

        builder
            .Entity<AccessKey>()
            .HasOne(o => o.Wallet)
            .WithMany(w => w.AccessKeys)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AccessKey>()
            .Property(e => e.Level)
            .HasConversion<string>();
    }
}
