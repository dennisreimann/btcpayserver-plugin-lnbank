using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BTCPayServer.Lightning;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LNbank.Data.Models;

public class Transaction
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string TransactionId { get; set; }
    public string InvoiceId { get; set; }
    public string WalletId { get; set; }

    [Required]
    public LightMoney Amount { get; set; }
    [DisplayName("Settled amount")]
    public LightMoney AmountSettled { get; set; }
    public LightMoney RoutingFee { get; set; }
    public string Description { get; set; }

    [DisplayName("Payment Request")]
    [Required]
    public string PaymentRequest { get; set; }

    [DisplayName("Payment Hash")]
    public string PaymentHash { get; set; }

    [DisplayName("Creation date")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [DisplayName("Expiry")]
    public DateTimeOffset ExpiresAt { get; set; }

    [DisplayName("Payment date")]
    public DateTimeOffset? PaidAt { get; set; }
    public Wallet Wallet { get; set; }
    public string ExplicitStatus { get; set; }

    public const string StatusSettled = "settled";
    public const string StatusPaid = "paid";
    public const string StatusUnpaid = "unpaid";
    public const string StatusExpired = "expired";
    public const string StatusPending = "pending";
    public const string StatusCancelled = "cancelled";
    public const string StatusInvalid = "invalid";

    public string Status
    {
        get
        {
            if (!string.IsNullOrEmpty(ExplicitStatus))
            {
                return ExplicitStatus;
            }
            if (AmountSettled != null)
            {
                return PaidAt == null ? StatusPaid : StatusSettled;
            }
            if (ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return StatusExpired;
            }
            return StatusUnpaid;
        }
    }

    public LightningInvoiceStatus LightningInvoiceStatus
    {
        get => Status switch
        {
            StatusSettled => LightningInvoiceStatus.Paid,
            StatusPaid => LightningInvoiceStatus.Paid,
            StatusUnpaid => LightningInvoiceStatus.Unpaid,
            StatusPending => LightningInvoiceStatus.Unpaid,
            StatusExpired => LightningInvoiceStatus.Expired,
            StatusInvalid => LightningInvoiceStatus.Expired,
            StatusCancelled => LightningInvoiceStatus.Expired,
            _ => throw new NotSupportedException($"'{Status}' cannot be mapped to any LightningInvoiceStatus")
        };
    }

    public LightningPaymentStatus LightningPaymentStatus
    {
        get => Status switch
        {
            StatusSettled => LightningPaymentStatus.Complete,
            StatusPaid => LightningPaymentStatus.Complete,
            StatusUnpaid => LightningPaymentStatus.Pending,
            StatusPending => LightningPaymentStatus.Pending,
            StatusExpired => LightningPaymentStatus.Failed,
            StatusInvalid => LightningPaymentStatus.Failed,
            StatusCancelled => LightningPaymentStatus.Failed,
            _ => throw new NotSupportedException($"'{Status}' cannot be mapped to any LightningPaymentStatus")
        };
    }

    public bool IsSettled => Status == StatusSettled;
    public bool IsPaid => Status == StatusPaid || IsSettled;
    public bool IsUnpaid => !IsPaid;
    public bool IsExpired => Status == StatusExpired;
    public bool IsPending => Status == StatusPending;
    public bool IsCancelled => Status == StatusCancelled;
    public bool IsInvalid => Status == StatusInvalid;

    public DateTimeOffset Date => PaidAt ?? CreatedAt;

    public bool SetCancelled()
    {
        if (!CanTerminate)
            return false;
        AmountSettled = null;
        RoutingFee = null;
        PaidAt = null;
        ExplicitStatus = StatusCancelled;
        return true;
    }

    public bool SetInvalid()
    {
        if (!CanTerminate)
            return false;
        AmountSettled = null;
        RoutingFee = null;
        PaidAt = null;
        ExplicitStatus = StatusInvalid;
        return true;
    }

    public bool SetExpired()
    {
        if (!CanTerminate)
            return false;
        ExplicitStatus = StatusExpired;
        return true;
    }

    public bool SetSettled(LightMoney amount, LightMoney amountSettled, LightMoney routingFee, DateTimeOffset date)
    {
        if (IsSettled)
            return false;
        Amount = amount;
        AmountSettled = amountSettled;
        RoutingFee = routingFee;
        PaidAt = date;
        ExplicitStatus = null;
        return true;
    }

    public bool HasRoutingFee => RoutingFee != null && RoutingFee > 0;

    private bool CanTerminate => IsUnpaid || IsPending;

    internal static void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<Transaction>()
            .HasIndex(o => o.InvoiceId);

        builder
            .Entity<Transaction>()
            .HasIndex(o => o.WalletId);

        builder
            .Entity<Transaction>()
            .HasIndex(o => o.PaymentRequest);

        builder
            .Entity<Transaction>()
            .HasIndex(o => o.PaymentHash);

        builder
            .Entity<Transaction>()
            .HasOne(o => o.Wallet)
            .WithMany(w => w.Transactions)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Entity<Transaction>()
            .Property(e => e.Amount)
            .HasConversion(
                v => v.MilliSatoshi,
                v => new LightMoney(v));

        builder
            .Entity<Transaction>()
            .Property(e => e.AmountSettled)
            .HasConversion(
                v => v.MilliSatoshi,
                v => new LightMoney(v));

        builder
            .Entity<Transaction>()
            .Property(e => e.RoutingFee)
            .HasConversion(
                v => v.MilliSatoshi,
                v => new LightMoney(v));
    }
}
