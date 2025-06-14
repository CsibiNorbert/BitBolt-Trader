using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Strategy.Domain.Indicators;
using BitcoinTradingBot.Modules.Strategy.Domain.Models;
using Microsoft.Extensions.Logging;
using BitcoinTradingBot.Core;

namespace BitcoinTradingBot.Modules.Strategy.Infrastructure.Calculations;

/// <summary>
/// High-performance Exponential Moving Average calculator with optimizations for trading strategies
/// </summary>
public class ExponentialMovingAverageCalculator : IExponentialMovingAverageCalculator
{
    private readonly ILogger<ExponentialMovingAverageCalculator> _logger;
    private readonly Dictionary<string, EmaState> _cachedStates;

    public ExponentialMovingAverageCalculator(ILogger<ExponentialMovingAverageCalculator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cachedStates = new Dictionary<string, EmaState>();
    }

    /// <summary>
    /// Internal method for calculating EMA values
    /// </summary>
    public IReadOnlyList<EmaValue> CalculateInternal(IReadOnlyList<Candle> candles, int period)
    {
        ArgumentNullException.ThrowIfNull(candles);
        
        if (period <= 0)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be positive");

        if (candles.Count == 0)
        {
            _logger.LogDebug("Empty candle collection provided for EMA calculation");
            return Array.Empty<EmaValue>();
        }

        if (candles.Count < period)
        {
            _logger.LogDebug("Insufficient candles for EMA calculation. Required: {Required}, Available: {Available}",
                period, candles.Count);
            return Array.Empty<EmaValue>();
        }

        try
        {
            var emaValues = CalculateEmaValues(candles, period);
            
            _logger.LogTrace("Calculated {Count} EMA values for period {Period}", emaValues.Count, period);
            
            return emaValues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating EMA for period {Period} with {CandleCount} candles", 
                period, candles.Count);
            throw;
        }
    }

    /// <summary>
    /// Calculates the latest EMA value
    /// </summary>
    public EmaValue CalculateLatest(IReadOnlyList<Candle> candles, int period)
    {
        var result = CalculateInternal(candles, period);
        
        if (result.Count == 0)
        {
            return new EmaValue { Period = 0 };
        }

        return result.Last();
    }

    /// <inheritdoc />
    public EmaValue CalculateIncremental(Candle newCandle, int period, EmaValue? previousEma = null)
    {
        ArgumentNullException.ThrowIfNull(newCandle);
        
        if (period <= 0)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be positive");

        try
        {
            decimal emaValue;
            if (previousEma == null || previousEma.Period == 0)
            {
                emaValue = newCandle.Close;
            }
            else
            {
                var alpha = CalculateSmoothingFactor(period);
                emaValue = (newCandle.Close * alpha) + (previousEma.Value * (1 - alpha));
            }

            return new EmaValue {
                Timestamp = newCandle.CloseTime,
                Value = emaValue,
                Period = period,
                Price = newCandle.Close,
                Volume = newCandle.Volume
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating incremental EMA");
            throw;
        }
    }

    /// <inheritdoc />
    public bool ValidateInput(IReadOnlyList<Candle> candles, int period)
    {
        if (candles == null || period <= 0)
            return false;

        if (candles.Count < period)
            return false;

        // Validate candle data integrity
        return ValidateCandleData(candles);
    }

    /// <summary>
    /// Calculates EMA values for the entire candle series
    /// </summary>
    private List<EmaValue> CalculateEmaValues(IReadOnlyList<Candle> candles, int period)
    {
        var emaValues = new List<EmaValue>();
        var smoothingFactor = CalculateSmoothingFactorInternal(period);

        // Calculate initial SMA for the first EMA value
        var initialSma = CalculateInitialSma(candles, period);
        
        var firstEmaValue = new EmaValue
        {
            Timestamp = candles[period - 1].OpenTime,
            Value = initialSma,
            Period = period,
            Price = candles[period - 1].Close,
            Volume = candles[period - 1].Volume
        };
        
        emaValues.Add(firstEmaValue);

        // Calculate subsequent EMA values
        for (int i = period; i < candles.Count; i++)
        {
            var previousEma = emaValues.Last();
            var currentCandle = candles[i];
            
            var emaValue = (currentCandle.Close.Value * smoothingFactor) + (previousEma.Value * (1 - smoothingFactor));

            var ema = new EmaValue
            {
                Timestamp = currentCandle.OpenTime,
                Value = emaValue,
                Period = period,
                Price = currentCandle.Close,
                Volume = currentCandle.Volume
            };

            emaValues.Add(ema);
        }

        return emaValues;
    }

    /// <summary>
    /// Calculates the initial Simple Moving Average for EMA seed
    /// </summary>
    private decimal CalculateInitialSma(IReadOnlyList<Candle> candles, int period)
    {
        var sum = 0m;
        
        for (int i = 0; i < period; i++)
        {
            sum += candles[i].Close;
        }

        return sum / period;
    }

    /// <summary>
    /// Calculates the smoothing factor (alpha) for EMA
    /// </summary>
    private static decimal CalculateSmoothingFactorInternal(int period)
    {
        // Standard formula: 2 / (period + 1)
        return 2m / (period + 1);
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
    /// Gets or creates cached EMA state for performance optimization
    /// </summary>
    public EmaState GetOrCreateCachedState(string symbol, int period)
    {
        var key = $"{symbol}_{period}";
        
        if (!_cachedStates.TryGetValue(key, out var state))
        {
            state = new EmaState
            {
                Symbol = symbol,
                Period = period,
                LastValue = null,
                LastTimestamp = DateTime.MinValue
            };
            
            _cachedStates[key] = state;
            
            _logger.LogDebug("Created new EMA state cache for {Symbol} period {Period}", symbol, period);
        }

        return state;
    }

    /// <summary>
    /// Updates cached EMA state
    /// </summary>
    public void UpdateCachedState(string symbol, int period, EmaValue emaValue)
    {
        var state = GetOrCreateCachedState(symbol, period);
        
        state.LastValue = emaValue;
        state.LastTimestamp = emaValue.Timestamp;
        
        _logger.LogTrace("Updated EMA cache for {Symbol} period {Period}: {Value} at {Timestamp}",
            symbol, period, emaValue.Value, emaValue.Timestamp);
    }

    /// <summary>
    /// Clears cached states (useful for testing or memory management)
    /// </summary>
    public void ClearCache()
    {
        var cacheCount = _cachedStates.Count;
        _cachedStates.Clear();
        
        _logger.LogDebug("Cleared EMA cache ({Count} entries)", cacheCount);
    }

    /// <inheritdoc />
    ExponentialMovingAverage IExponentialMovingAverageCalculator.Calculate(IReadOnlyList<Candle> candles, int period)
    {
        var result = CalculateInternal(candles, period);
        if (result.Count == 0)
            return new ExponentialMovingAverage(0m, period, DateTime.UtcNow);
        
        var latest = result.Last();
        return new ExponentialMovingAverage(latest.Value, period, latest.Timestamp);
    }

    /// <inheritdoc />
    public IReadOnlyList<ExponentialMovingAverage> CalculateSeries(IReadOnlyList<Candle> candles, int period)
    {
        var emaValues = CalculateInternal(candles, period);
        return emaValues.Select(e => new ExponentialMovingAverage(e.Value, period, e.Timestamp)).ToList();
    }

    /// <inheritdoc />
    public ExponentialMovingAverage UpdateRolling(ExponentialMovingAverage previousEma, Price newPrice, int period)
    {
        var alpha = CalculateSmoothingFactor(period);
        var newValue = (newPrice.Value * alpha) + (previousEma.Value.Value * (1 - alpha));
        return new ExponentialMovingAverage(Price.Create(newValue), period, DateTime.UtcNow);
    }

    /// <inheritdoc />
    public decimal CalculateSmoothingFactor(int period)
    {
        return CalculateSmoothingFactorInternal(period);
    }

    /// <inheritdoc />
    public decimal CalculateSlope(IReadOnlyList<ExponentialMovingAverage> emaValues)
    {
        if (emaValues.Count < 2) return 0;

        var recent = emaValues.TakeLast(2).ToList();
        var previous = recent[0].Value;
        var current = recent[1].Value;
        
        return current - previous;
    }
}

/// <summary>
/// Cached EMA state for performance optimization
/// </summary>
public class EmaState
{
    public required string Symbol { get; init; }
    public required int Period { get; init; }
    public EmaValue? LastValue { get; set; }
    public DateTime LastTimestamp { get; set; }
}

/// <summary>
/// EMA calculation result value
/// </summary>
public class EmaValue : ITimestampedValue
{
    public DateTime Timestamp { get; init; }
    public decimal Value { get; init; }
    public int Period { get; init; }
    public decimal Price { get; init; }
    public decimal Volume { get; init; }

    public static readonly EmaValue Empty = new()
    {
        Timestamp = DateTime.MinValue,
        Value = 0m,
        Period = 0,
        Price = 0m,
        Volume = 0m
    };

    public override string ToString()
    {
        return $"EMA({Period})={Value:F8} @ {Timestamp:yyyy-MM-dd HH:mm:ss}";
    }
} 