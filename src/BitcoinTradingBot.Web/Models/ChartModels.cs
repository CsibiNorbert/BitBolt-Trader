using TradingView.Blazor.Models;

namespace BitcoinTradingBot.Web.Models;

/// <summary>
/// OHLCV data model for TradingView candlestick chart
/// </summary>
public class CryptoCandlestickData : Ohlcv
{
    public CryptoCandlestickData()
    {
        Time = DateTime.UtcNow;
        Open = 0;
        High = 0;
        Low = 0;
        Close = 0;
        Volume = 0;
    }

    public CryptoCandlestickData(DateTime time, decimal open, decimal high, decimal low, decimal close, decimal volume)
    {
        Time = time;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }
}

/// <summary>
/// Trading signal marker for TradingView chart
/// </summary>
public class TradingSignalMarker : Marker
{
    public string SignalType { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public new decimal Price { get; set; }
}

/// <summary>
/// Real-time price update model
/// </summary>
public class PriceUpdate
{
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
    public string Symbol { get; set; } = string.Empty;
}

/// <summary>
/// Keltner Channel indicator data
/// </summary>
public class KeltnerChannelData
{
    public DateTime Time { get; set; }
    public decimal Upper { get; set; }
    public decimal Middle { get; set; }
    public decimal Lower { get; set; }
} 