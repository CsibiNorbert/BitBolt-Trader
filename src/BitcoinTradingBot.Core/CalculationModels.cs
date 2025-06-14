using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Core.Models;

/// <summary>
/// Represents the result of a Keltner Channel calculation
/// </summary>
public record KeltnerChannelResult(
    IReadOnlyList<KeltnerChannelValue> Values,
    KeltnerChannelSettings Settings,
    DateTime CalculatedAt
)
{
    /// <summary>
    /// Gets the latest calculated value
    /// </summary>
    public KeltnerChannelValue? Latest => Values.LastOrDefault();
    
    /// <summary>
    /// Gets the total count of calculated values
    /// </summary>
    public int Count => Values.Count;
    
    /// <summary>
    /// Indicates if the result contains any values
    /// </summary>
    public bool HasValues => Values.Count > 0;
    
    /// <summary>
    /// Gets values within a specific time range
    /// </summary>
    public IEnumerable<KeltnerChannelValue> GetRange(DateTime from, DateTime to)
    {
        return Values.Where(v => v.Timestamp >= from && v.Timestamp <= to);
    }
}

/// <summary>
/// Represents an EMA (Exponential Moving Average) value
/// </summary>
public record EmaValue(
    DateTime Timestamp,
    Price Value,
    int Period,
    decimal SmoothingFactor
) : ITimestampedValue
{
    /// <summary>
    /// Calculates the slope between this EMA and a previous one
    /// </summary>
    public decimal CalculateSlope(EmaValue previousEma)
    {
        var timeDiff = (decimal)(Timestamp - previousEma.Timestamp).TotalMinutes;
        if (timeDiff <= 0) return 0;
        
        var priceDiff = Value - previousEma.Value;
        return priceDiff / timeDiff;
    }
}

/// <summary>
/// Represents the state for EMA calculations to enable caching
/// </summary>
public record EmaState(
    int Period,
    decimal SmoothingFactor,
    Price LastValue,
    DateTime LastTimestamp
);

/// <summary>
/// Represents an ATR (Average True Range) value with additional volatility metrics
/// </summary>
public record AtrValue(
    DateTime Timestamp,
    decimal Value,
    decimal TrueRange,
    int Period,
    decimal VolatilityPercentage,
    VolatilityLevel VolatilityLevel
) : ITimestampedValue
{
    /// <summary>
    /// Indicates if the current volatility is above normal
    /// </summary>
    public bool IsHighVolatility => VolatilityLevel >= VolatilityLevel.High;
    
    /// <summary>
    /// Gets the normalized ATR value (ATR / Current Price)
    /// </summary>
    public decimal GetNormalizedValue(Price currentPrice) => currentPrice > 0 ? Value / currentPrice : 0;
}

/// <summary>
/// Represents volatility level classification
/// </summary>
public enum VolatilityLevel
{
    Low = 1,
    Normal = 2,
    High = 3,
    Extreme = 4
}

/// <summary>
/// Represents the state for ATR calculations to enable incremental updates
/// </summary>
public record AtrState(
    int Period,
    decimal CurrentAtr,
    Queue<decimal> TrueRangeHistory,
    DateTime LastTimestamp
); 