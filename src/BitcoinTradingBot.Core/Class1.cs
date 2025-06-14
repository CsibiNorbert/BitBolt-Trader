using System.ComponentModel.DataAnnotations;

namespace BitcoinTradingBot.Core;

/// <summary>
/// Represents a trading symbol (e.g., "BTCUSDT")
/// </summary>
public record Symbol(string Value)
{
    public static implicit operator string(Symbol symbol) => symbol.Value;
    public static implicit operator Symbol(string value) => new(value);
    
    public override string ToString() => Value;
}

/// <summary>
/// Represents a price value with validation
/// </summary>
public record Price(decimal Value)
{
    public static Price Create(decimal value)
    {
        if (value < 0)
            throw new ArgumentException("Price cannot be negative", nameof(value));
        return new Price(value);
    }
    
    public static Price Zero() => new(0m);
    
    public static implicit operator decimal(Price price) => price.Value;
    public static implicit operator Price(decimal value) => Create(value);
    
    public override string ToString() => Value.ToString("F8");
}

/// <summary>
/// Represents a quantity/volume value with validation
/// </summary>
public record Quantity(decimal Value)
{
    public static Quantity Create(decimal value)
    {
        if (value < 0)
            throw new ArgumentException("Quantity cannot be negative", nameof(value));
        return new Quantity(value);
    }
    
    public static Quantity Zero() => new(0m);
    
    public static implicit operator decimal(Quantity quantity) => quantity.Value;
    public static implicit operator Quantity(decimal value) => Create(value);
    
    public override string ToString() => Value.ToString("F8");
}

/// <summary>
/// Trading signal enumeration
/// </summary>
public enum TradingSignal
{
    None = 0,
    Buy = 1,
    Sell = 2
}

/// <summary>
/// Time frame enumeration for candle data
/// </summary>
public enum TimeFrame
{
    OneMinute = 1,
    FiveMinutes = 5,
    FifteenMinutes = 15,
    ThirtyMinutes = 30,
    OneHour = 60,
    FourHours = 240,
    OneDay = 1440
}

/// <summary>
/// Order side enumeration
/// </summary>
public enum OrderSide
{
    Buy = 1,
    Sell = 2
}

/// <summary>
/// Order status enumeration
/// </summary>
public enum OrderStatus
{
    New = 1,
    PartiallyFilled = 2,
    Filled = 3,
    Canceled = 4,
    Rejected = 5,
    Expired = 6
}

/// <summary>
/// Order type enumeration
/// </summary>
public enum OrderType
{
    Market = 1,
    Limit = 2,
    StopLoss = 3,
    TakeProfit = 4
}



/// <summary>
/// Kline/Candlestick interval enumeration for Binance API
/// </summary>
public enum KlineInterval
{
    OneMinute,
    ThreeMinutes,
    FiveMinutes,
    FifteenMinutes,
    ThirtyMinutes,
    OneHour,
    TwoHours,
    FourHours,
    SixHours,
    EightHours,
    TwelveHours,
    OneDay,
    ThreeDays,
    OneWeek,
    OneMonth
}

/// <summary>
/// Represents a Binance kline/candlestick data interface
/// </summary>
public interface IBinanceKline
{
    DateTime OpenTime { get; }
    decimal Open { get; }
    decimal High { get; }
    decimal Low { get; }
    decimal Close { get; }
    decimal Volume { get; }
    DateTime CloseTime { get; }
    decimal QuoteVolume { get; }
    int TradeCount { get; }
    decimal TakerBuyBaseVolume { get; }
    decimal TakerBuyQuoteVolume { get; }
}

/// <summary>
/// Represents a WebSocket update subscription
/// </summary>
public interface UpdateSubscription : IDisposable
{
    string Id { get; }
    bool IsActive { get; }
    Task CloseAsync();
}

/// <summary>
/// Represents ticker information from Binance
/// </summary>
public record BinanceTicker(
    Symbol Symbol,
    Price LastPrice,
    Price PriceChange,
    decimal PriceChangePercent,
    Price WeightedAveragePrice,
    Price PrevClosePrice,
    Price BidPrice,
    Quantity BidQuantity,
    Price AskPrice,
    Quantity AskQuantity,
    Price OpenPrice,
    Price HighPrice,
    Price LowPrice,
    Quantity Volume,
    Quantity QuoteVolume,
    DateTime OpenTime,
    DateTime CloseTime,
    long FirstTradeId,
    long LastTradeId,
    int TradeCount
);

/// <summary>
/// Helper class for converting between TimeFrame and KlineInterval
/// </summary>
public static class TimeFrameExtensions
{
    /// <summary>
    /// Converts TimeFrame to KlineInterval
    /// </summary>
    public static KlineInterval ToKlineInterval(this TimeFrame timeFrame)
    {
        return timeFrame switch
        {
            TimeFrame.OneMinute => KlineInterval.OneMinute,
            TimeFrame.FiveMinutes => KlineInterval.FiveMinutes, 
            TimeFrame.FifteenMinutes => KlineInterval.FifteenMinutes,
            TimeFrame.ThirtyMinutes => KlineInterval.ThirtyMinutes,
            TimeFrame.OneHour => KlineInterval.OneHour,
            TimeFrame.FourHours => KlineInterval.FourHours,
            TimeFrame.OneDay => KlineInterval.OneDay,
            _ => throw new ArgumentException($"Unsupported timeframe: {timeFrame}")
        };
    }
    
    /// <summary>
    /// Converts KlineInterval to TimeFrame
    /// </summary>
    public static TimeFrame ToTimeFrame(this KlineInterval interval)
    {
        return interval switch
        {
            KlineInterval.OneMinute => TimeFrame.OneMinute,
            KlineInterval.FiveMinutes => TimeFrame.FiveMinutes,
            KlineInterval.FifteenMinutes => TimeFrame.FifteenMinutes,
            KlineInterval.ThirtyMinutes => TimeFrame.ThirtyMinutes,
            KlineInterval.OneHour => TimeFrame.OneHour,
            KlineInterval.FourHours => TimeFrame.FourHours,
            KlineInterval.OneDay => TimeFrame.OneDay,
            _ => throw new ArgumentException($"Unsupported interval: {interval}")
        };
    }
}
