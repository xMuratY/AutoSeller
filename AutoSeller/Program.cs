
using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Logging;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

var targetAsset = "BUSD";
var coins = new string[] { "BTC", "LTC" };

var loggerinst = LoggerFactory.Create(x => x.AddNLog()).CreateLogger("Binance");
 
var binanceClient = new BinanceClient(new BinanceClientOptions()
{
    ApiCredentials = new ApiCredentials(Environment.GetEnvironmentVariable("BINANCE_KEY")!, Environment.GetEnvironmentVariable("BINANCE_SECRET")!),
    LogLevel = LogLevel.Information,
    LogWriters = new List<ILogger> { loggerinst }
});

var _exchangeInfo = await binanceClient.SpotApi.ExchangeData.GetExchangeInfoAsync();
var _symbolData = _exchangeInfo.Data.Symbols.Where(s => coins.Contains(s.BaseAsset) && s.QuoteAsset == targetAsset);

Dictionary<string,ClampData> clampDatas = _symbolData.ToDictionary(x => x.BaseAsset, y => ClampData.Create(y));

loggerinst.LogTrace($"Initialized!");

while (true)
{
    var bal = await binanceClient.SpotApi.Account.GetBalancesAsync();

    if (bal.Success)
    {
        var coinlist = bal.Data.ToList().Where(x => coins.Contains(x.Asset) && x.Available > clampDatas[x.Asset].min);
        if (coinlist.Any())
        {
            coinlist.ToList().ForEach(async (x) =>
            {
                var sellQty = clampDatas[x.Asset].ClampQuantity(x.Available);

                loggerinst.LogTrace($"{sellQty} of {x.Available} {x.Asset} found for sale!");
                var placeOrder = await binanceClient.SpotApi.Trading.PlaceOrderAsync(x.Asset + targetAsset, OrderSide.Sell, SpotOrderType.Market, quantity: sellQty);
                if (placeOrder.Success)
                {
                    loggerinst.LogTrace($"[{placeOrder.Data.CreateTime}] ({placeOrder.Data.Symbol}) Order Placed for {placeOrder.Data.Price} {placeOrder.Data.Quantity}");
                }
                else
                {
                    loggerinst.LogTrace($"[{placeOrder.Error!.Code}] PlaceOrder failed: {placeOrder.Error.Message}");
                }
            });
        }
    }

    await Task.Delay(10000);
}

public class ClampData
{
    public static ClampData Create(BinanceSymbol binanceSymbol)
    {
        ClampData clampData = new ClampData();
        clampData.min = binanceSymbol.LotSizeFilter!.MinQuantity;
        clampData.max = binanceSymbol.LotSizeFilter!.MaxQuantity;
        clampData.step = binanceSymbol.LotSizeFilter!.StepSize;
        return clampData;
    }
    public decimal ClampQuantity(decimal quantiy)
    {
        return BinanceHelpers.ClampQuantity(min, max, step, quantiy);
    }
    public decimal min { get; set; }
    public decimal max { get; set; }
    public decimal step { get; set; }
};