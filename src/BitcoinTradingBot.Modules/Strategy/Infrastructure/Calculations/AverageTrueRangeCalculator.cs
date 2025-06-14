using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Strategy.Domain.Indicators;
using BitcoinTradingBot.Modules.Strategy.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Strategy.Infrastructure.Calculations;

/// <summary>
/// High-performance Average True Range calculator for volatility measurement
/// </summary>
public class AverageTrueRangeCalculator : IAverageTrueRangeCalculator
{
    private readonly ILogger<AverageTrueRangeCalculator> _logger;
    private readonly Dictionary<string, AtrState> _cachedStates;

    public AverageTrueRangeCalculator(ILogger<AverageTrueRangeCalculator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cachedStates = new Dictionary<string, AtrState>();
    }

    /// <inheritdoc />
    AverageTrueRange IAverageTrueRangeCalculator.Calculate(IReadOnlyList<Candle> candles, int period)
    {
        var result = CalculateInternal(candles, period);
        if (result.Count == 0)
            return new AverageTrueRange(0m, period, DateTime.UtcNow);
        
        var latest = result.Last();
        return new AverageTrueRange(latest.Value, period, latest.Timestamp);
    }

    /// <inheritdoc />
    public IReadOnlyList<AverageTrueRange> CalculateSeries(IReadOnlyList<Candle> candles, int period = 14)
    {
        var atrValues = CalculateInternal(candles, period);
        return atrValues.Select(a => new AverageTrueRange(a.Value, period, a.Timestamp)).ToList();
    }

    /// <inheritdoc />
    public decimal CalculateTrueRange(Candle currentCandle, Candle previousCandle)
    {
        return CalculateTrueRangeInternal(currentCandle, previousCandle);
    }

    /// <inheritdoc />
    public AverageTrueRange UpdateRolling(AverageTrueRange previousAtr, decimal newTrueRange, int period)
    {
        var alpha = 1.0m / period;
        var newValue = (alpha * newTrueRange) + ((1 - alpha) * previousAtr.Value);
        return new AverageTrueRange(newValue, period, DateTime.UtcNow);
    }

    /// <inheritdoc />
    public decimal CalculateNormalizedAtr(AverageTrueRange atr, Price price)
    {
        if (price <= 0) return 0;
        return (atr.Value / price) * 100m;
    }

    /// <inheritdoc />
    public VolatilityRegime DetermineVolatilityRegime(AverageTrueRange currentAtr, IReadOnlyList<AverageTrueRange> historicalAtr)
    {
        if (historicalAtr.Count < 20) return VolatilityRegime.Normal;

        var recentAtr = historicalAtr.TakeLast(20).ToList();
        var avgAtr = recentAtr.Average(a => a.Value);
        var stdDev = CalculateStandardDeviation(recentAtr.Select(a => a.Value));

        var normalized = (currentAtr.Value - avgAtr) / stdDev;

        return normalized switch
        {
            < -1.0m => VolatilityRegime.Low,
            >= -1.0m and < 1.0m => VolatilityRegime.Normal,
            >= 1.0m and < 2.0m => VolatilityRegime.High,
            _ => VolatilityRegime.Extreme
        };
    }

    /// <inheritdoc />
    public bool IsVolatilityNormal(AverageTrueRange currentAtr, IReadOnlyList<AverageTrueRange> historicalAtr, decimal threshold = 2.0m)
    {
        if (historicalAtr.Count == 0) return true;
        
        var avgAtr = historicalAtr.Average(a => a.Value);
        return currentAtr.Value <= avgAtr * threshold;
    }

    private static decimal CalculateStandardDeviation(IEnumerable<decimal> values)
    {
        var valuesList = values.ToList();
        if (valuesList.Count <= 1) return 0;

        var avg = valuesList.Average();
        var sumOfSquares = valuesList.Sum(v => (v - avg) * (v - avg));
        return (decimal)Math.Sqrt((double)(sumOfSquares / (valuesList.Count - 1)));
    }

    /// <summary>
    /// Internal method for calculating ATR values
    /// </summary>
    public IReadOnlyList<AtrValue> CalculateInternal(IReadOnlyList<Candle> candles, int period)
    {
        ArgumentNullException.ThrowIfNull(candles);
        
        if (period <= 0)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be positive");

        if (candles.Count == 0)
        {
            _logger.LogDebug("Empty candle collection provided for ATR calculation");
            return Array.Empty<AtrValue>();
        }

        if (candles.Count < period + 1) // +1 because we need previous candle for True Range
        {
            _logger.LogDebug("Insufficient candles for ATR calculation. Required: {Required}, Available: {Available}",
                period + 1, candles.Count);
            return Array.Empty<AtrValue>();
        }

        try
        {
            var atrValues = CalculateAtrValues(candles, period);
            
            _logger.LogTrace("Calculated {Count} ATR values for period {Period}", atrValues.Count, period);
            
            return atrValues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating ATR for period {Period} with {CandleCount} candles", 
                period, candles.Count);
            throw;
        }
    }

    /// <summary>
    /// Calculates the latest ATR value
    /// </summary>
    public AtrValue CalculateLatest(IReadOnlyList<Candle> candles, int period)
    {
        var result = CalculateInternal(candles, period);
        
        if (result.Count == 0)
        {
            return new AtrValue { Period = 0 };
        }

        return result.Last();
    }

    /// <inheritdoc />
    public AtrValue CalculateIncremental(Candle newCandle, Candle? previousCandle, int period, AtrValue? previousAtr = null)
    {
        ArgumentNullException.ThrowIfNull(newCandle);
        
        if (period <= 0)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be positive");

        try
        {
            // Calculate True Range for the new candle
            var trueRange = CalculateTrueRangeInternal(newCandle, previousCandle);
            
            decimal atrValue;
            if (previousAtr == null || previousAtr.Period == 0)
            {
                atrValue = trueRange;
            }
            else
            {
                var alpha = CalculateSmoothingFactor(period);
                atrValue = (trueRange * alpha) + (previousAtr.Value * (1 - alpha));
            }

            return new AtrValue {
                Timestamp = newCandle.CloseTime,
                Value = atrValue,
                Period = period,
                TrueRange = trueRange,
                HighLowRange = newCandle.High - newCandle.Low,
                HighClosePrevRange = previousCandle != null ? Math.Abs(newCandle.High - previousCandle.Close) : 0,
                LowClosePrevRange = previousCandle != null ? Math.Abs(newCandle.Low - previousCandle.Close) : 0,
                VolatilityPercentage = newCandle.Close > 0 ? (atrValue / newCandle.Close) * 100 : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating incremental ATR");
            throw;
        }
    }

    /// <inheritdoc />
    public bool ValidateInput(IReadOnlyList<Candle> candles, int period)
    {
        if (candles == null || period <= 0)
            return false;

        if (candles.Count < period + 1)
            return false;

        // Validate candle data integrity
        return ValidateCandleData(candles);
    }

    /// <summary>
    /// Calculates ATR values for the entire candle series
    /// </summary>
    private List<AtrValue> CalculateAtrValues(IReadOnlyList<Candle> candles, int period)
    {
        var atrValues = new List<AtrValue>();
        var trueRanges = new List<decimal>();

        // Calculate True Range for each candle (starting from index 1)
        for (int i = 1; i < candles.Count; i++)
        {
            var currentCandle = candles[i];
            var previousCandle = candles[i - 1];
            
            var trueRange = CalculateTrueRangeInternal(currentCandle, previousCandle);
            trueRanges.Add(trueRange);
        }

        // Calculate initial ATR using Simple Moving Average of True Ranges
        if (trueRanges.Count >= period)
        {
            var initialAtr = trueRanges.Take(period).Average();
            
            var firstAtrValue = CreateAtrValue(candles[period], candles[period - 1], initialAtr, trueRanges[period - 1], period);
            atrValues.Add(firstAtrValue);

            // Calculate subsequent ATR values using Wilder's smoothing
            var alpha = 1.0m / period;
            
            for (int i = period; i < trueRanges.Count; i++)
            {
                var currentTrueRange = trueRanges[i];
                var previousAtr = atrValues.Last();
                
                var atrValue = (alpha * currentTrueRange) + ((1 - alpha) * previousAtr.Value);
                
                var atr = CreateAtrValue(candles[i + 1], candles[i], atrValue, currentTrueRange, period);
                atrValues.Add(atr);
            }
        }

        return atrValues;
    }

    /// <summary>
    /// Calculates True Range for a single candle
    /// </summary>
    private decimal CalculateTrueRangeInternal(Candle currentCandle, Candle? previousCandle)
    {
        if (previousCandle == null)
        {
            // For the first candle, True Range = High - Low
            return currentCandle.High - currentCandle.Low;
        }

        // True Range = max(High - Low, |High - PreviousClose|, |Low - PreviousClose|)
        var highLow = currentCandle.High - currentCandle.Low;
        var highClosePrev = Math.Abs(currentCandle.High - previousCandle.Close);
        var lowClosePrev = Math.Abs(currentCandle.Low - previousCandle.Close);

        return Math.Max(highLow, Math.Max(highClosePrev, lowClosePrev));
    }

    /// <summary>
    /// Creates an ATR value with all computed metrics
    /// </summary>
    private AtrValue CreateAtrValue(Candle currentCandle, Candle? previousCandle, decimal atrValue, decimal trueRange, int period)
    {
        return new AtrValue
        {
            Timestamp = currentCandle.OpenTime,
            Value = atrValue,
            Period = period,
            TrueRange = trueRange,
            HighLowRange = currentCandle.High - currentCandle.Low,
            HighClosePrevRange = previousCandle != null ? Math.Abs(currentCandle.High - previousCandle.Close) : 0,
            LowClosePrevRange = previousCandle != null ? Math.Abs(currentCandle.Low - previousCandle.Close) : 0,
            VolatilityPercentage = currentCandle.Close > 0 ? (atrValue / currentCandle.Close) * 100 : 0
        };
    }

    /// <summary>
    /// Validates candle data integrity
    /// </summary>
    private bool ValidateCandleData(IReadOnlyList<Candle> candles)
    {
        for (int i = 0; i < candles.Count; i++)
        {
            var candle = candles[i];
            
            // Check for valid OHLC relationships
            if (candle.High < candle.Low || 
                candle.High < candle.Open || 
                candle.High < candle.Close ||
                candle.Low > candle.Open || 
                candle.Low > candle.Close)
            {
                _logger.LogWarning("Invalid OHLC data at index {Index}: O={Open}, H={High}, L={Low}, C={Close}",
                    i, candle.Open, candle.High, candle.Low, candle.Close);
                return false;
            }

            // Check for reasonable values (not zero or negative)
            if (candle.Open <= 0 || candle.High <= 0 || candle.Low <= 0 || candle.Close <= 0)
            {
                _logger.LogWarning("Invalid price values at index {Index}: O={Open}, H={High}, L={Low}, C={Close}",
                    i, candle.Open, candle.High, candle.Low, candle.Close);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets or creates cached ATR state for performance optimization
    /// </summary>
    public AtrState GetOrCreateCachedState(string symbol, int period)
    {
        var key = $"{symbol}_{period}";
        
        if (!_cachedStates.TryGetValue(key, out var state))
        {
            state = new AtrState
            {
                Symbol = symbol,
                Period = period,
                LastValue = null,
                LastCandle = null,
                LastTimestamp = DateTime.MinValue
            };
            
            _cachedStates[key] = state;
            
            _logger.LogDebug("Created new ATR state cache for {Symbol} period {Period}", symbol, period);
        }

        return state;
    }

    /// <summary>
    /// Updates cached ATR state
    /// </summary>
    public void UpdateCachedState(string symbol, int period, AtrValue atrValue, Candle currentCandle)
    {
        var state = GetOrCreateCachedState(symbol, period);
        
        state.LastValue = atrValue;
        state.LastCandle = currentCandle;
        state.LastTimestamp = atrValue.Timestamp;
        
        _logger.LogTrace("Updated ATR cache for {Symbol} period {Period}: {Value} at {Timestamp}",
            symbol, period, atrValue.Value, atrValue.Timestamp);
    }

    /// <summary>
    /// Clears cached states (useful for testing or memory management)
    /// </summary>
    public void ClearCache()
    {
        var cacheCount = _cachedStates.Count;
        _cachedStates.Clear();
        
        _logger.LogDebug("Cleared ATR cache ({Count} entries)", cacheCount);
    }

    /// <summary>
    /// Calculates volatility classification based on ATR
    /// </summary>
    public VolatilityLevel ClassifyVolatility(AtrValue atrValue, IReadOnlyList<AtrValue> historicalAtr)
    {
        if (historicalAtr.Count < 20) // Need sufficient history
            return VolatilityLevel.Normal;

        var recentAtr = historicalAtr.TakeLast(20).Select(a => a.Value).Average();
        var longerAtr = historicalAtr.TakeLast(50).Select(a => a.Value).Average();

        var currentToRecent = atrValue.Value / recentAtr;
        var recentToLonger = recentAtr / longerAtr;

        if (currentToRecent > 1.5m || recentToLonger > 1.3m)
            return VolatilityLevel.High;
        else if (currentToRecent < 0.7m || recentToLonger < 0.8m)
            return VolatilityLevel.Low;
        else
            return VolatilityLevel.Normal;
    }

    private static decimal CalculateSmoothingFactor(int period) => 2m / (period + 1);
}

/// <summary>
/// Cached ATR state for performance optimization
/// </summary>
public class AtrState
{
    public required string Symbol { get; init; }
    public required int Period { get; init; }
    public AtrValue? LastValue { get; set; }
    public Candle? LastCandle { get; set; }
    public DateTime LastTimestamp { get; set; }
}

/// <summary>
/// ATR calculation result value
/// </summary>
public class AtrValue : ITimestampedValue
{
    public DateTime Timestamp { get; init; }
    public decimal Value { get; init; }
    public int Period { get; init; }
    public decimal TrueRange { get; init; }
    public decimal HighLowRange { get; init; }
    public decimal HighClosePrevRange { get; init; }
    public decimal LowClosePrevRange { get; init; }
    public decimal VolatilityPercentage { get; init; }

    public static readonly AtrValue Empty = new()
    {
        Timestamp = DateTime.MinValue,
        Value = 0m,
        Period = 0,
        TrueRange = 0m,
        HighLowRange = 0m,
        HighClosePrevRange = 0m,
        LowClosePrevRange = 0m,
        VolatilityPercentage = 0m
    };

    public override string ToString()
    {
        return $"ATR({Period})={Value:F8} ({VolatilityPercentage:F2}%) @ {Timestamp:yyyy-MM-dd HH:mm:ss}";
    }
}

/// <summary>
/// Volatility level classification
/// </summary>
public enum VolatilityLevel
{
    Low,
    Normal,
    High
} 