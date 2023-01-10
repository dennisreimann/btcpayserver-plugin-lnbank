using System;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;

namespace BTCPayServer.Plugins.LNbank.Pages;

public static class Helpers
{
    public static string Sats(LightMoney amount)
    {
        return $"{Math.Round(amount.ToUnit(LightMoneyUnit.Satoshi))} sats";
    }

    public static string Millisats(LightMoney amount)
    {
        return $"{amount.ToUnit(LightMoneyUnit.MilliSatoshi)} millisats";
    }

    public static string TransactionStateClass(Transaction transaction)
    {
        if (transaction.IsPaid || transaction.IsSettled)
            return transaction.AmountSettled > 0 ? "success" : "danger";

        return transaction.IsExpired || transaction.IsCancelled ? "info" : "warning";
    }
}
