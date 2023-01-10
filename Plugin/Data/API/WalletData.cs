using System;
using BTCPayServer.Client.JsonConverters;
using BTCPayServer.Lightning;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.API;

public class WalletData
{
    public string Id { get; set; }
    public string Name { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string AccessKey { get; set; }

    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney Balance { get; set; }

    [JsonConverter(typeof(NBitcoin.JsonConverters.DateTimeToUnixTimeConverter))]
    public DateTimeOffset CreatedAt { get; set; }
}
