using System.ComponentModel.DataAnnotations;
using BTCPayServer.Client.JsonConverters;
using BTCPayServer.Lightning;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LightningInvoicePayRequest
{
    [Required] public string PaymentRequest { get; set; }

    public float? MaxFeePercent { get; set; }

    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney Amount { get; set; }

    public string Description { get; set; }

    // For LNURL
    public string Comment { get; set; }
}
