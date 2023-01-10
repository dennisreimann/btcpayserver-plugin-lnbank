using System.Collections.Generic;

namespace BTCPayServer.Plugins.LNbank.Authentication;

public class LNbankPolicies
{
    public const string CanViewWallet = "btcpay.plugin.lnbank.canviewwallet";
    public const string CanCreateInvoices = "btcpay.plugin.lnbank.cancreateinvoices";
    public const string CanSendMoney = "btcpay.plugin.lnbank.cansendmoney";
    public const string CanManageWallet = "btcpay.plugin.lnbank.canmanagewallet";

    public static IEnumerable<string> AllPolicies
    {
        get
        {
            yield return CanViewWallet;
            yield return CanCreateInvoices;
            yield return CanSendMoney;
            yield return CanManageWallet;
        }
    }
}
