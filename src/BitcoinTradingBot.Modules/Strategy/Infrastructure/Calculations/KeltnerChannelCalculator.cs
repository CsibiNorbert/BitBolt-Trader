using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Strategy.Domain.Indicators;
using BitcoinTradingBot.Modules.Strategy.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Strategy.Infrastructure.Calculations;

/// <summary>
/// High-performance Keltner Channel calculator with validation and optimization
/// </summary>
public class KeltnerChannelCalculator : IKeltnerChannelCalculator
{
    private readonly ILogger<KeltnerChannelCalculator> _logger;
    private readonly IExponentialMovingAverageCalculator _emaCalculator;
    private readonly IAverageTrueRangeCalculator _atrCalculator;

    public KeltnerChannelCalculator(
        ILogger<KeltnerChannelCalculator> logger,
        IExponentialMovingAverageCalculator emaCalculator,
        IAverageTrueRangeCalculator atrCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emaCalculator = emaCalculator ?? throw new ArgumentNullException(nameof(emaCalculator));
        _atrCalculator = atrCalculator ?? throw new ArgumentNullException(nameof(atrCalculator));
    }

    /// <summary>
    /// Internal method for calculating Keltner Channel with settings
    /// </summary>
    public KeltnerChannelResult CalculateInternal(IReadOnlyList<Candle> candles, KeltnerChannelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(settings);

        if (candles.Count == 0)
        {
            _logger.LogDebug("Empty candle collection provided for Keltner Channel calculation");
            return KeltnerChannelResult.Empty;
        }

        var requiredPeriods = Math.Max(settings.EmaPeriod, settings.AtrPeriod);
        if (candles.Count < requiredPeriods)
        {
            _logger.LogDebug("Insufficient candles for Keltner Channel calculation. Required: {Required}, Available: {Available}",
                requiredPeriods, candles.Count);
            return KeltnerChannelResult.Empty;
        }

        try
        {
            // Calculate EMA (middle band)
            var emaValues = _emaCalculator.CalculateSeries(candles, settings.EmaPeriod);
            if (emaValues.Count == 0)
            {
                _logger.LogWarning("EMA calculation returned no values for period {Period}", settings.EmaPeriod);
                return KeltnerChannelResult.Empty;
            }

            // Calculate ATR for channel width
            var atrValues = _atrCalculator.CalculateSeries(candles, settings.AtrPeriod);
            if (atrValues.Count == 0)
            {
                _logger.LogWarning("ATR calculation returned no values for period {Period}", settings.AtrPeriod);
                return KeltnerChannelResult.Empty;
            }

            // Calculate channel bands
            var channels = CalculateChannelBands(emaValues, atrValues, settings, candles);

            // Validate results
            var validatedChannels = ValidateAndFilterResults(channels, candles);

            _logger.LogTrace("Calculated {Count} Keltner Channel values with settings: EMA={EmaPeriod}, ATR={AtrPeriod}, Multiplier={Multiplier}",
                validatedChannels.Count, settings.EmaPeriod, settings.AtrPeriod, settings.Multiplier);

            return new KeltnerChannelResult
            {
                Values = validatedChannels,
                Settings = settings,
                IsValid = validatedChannels.Count > 0,
                CalculatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Keltner Channels with settings: {@Settings}", settings);
            throw;
        }
    }

    /// <summary>
    /// Calculates the latest Keltner Channel value
    /// </summary>
    public KeltnerChannelValue CalculateLatest(IReadOnlyList<Candle> candles, KeltnerChannelSettings settings)
    {
        var result = CalculateInternal(candles, settings);
        
        if (!result.IsValid || result.Values.Count == 0)
        {
            // Return a default value if not valid
            return new KeltnerChannelValue(DateTime.UtcNow, Price.Create(0), Price.Create(0), Price.Create(0), 0m, 0m);
        }

        return result.Values.Last();
    }

    /// <inheritdoc />
    public bool ValidateInput(IReadOnlyList<Candle> candles, KeltnerChannelSettings settings)
    {
        if (candles == null || settings == null)
            return false;

        if (candles.Count == 0)
            return false;

        var requiredPeriods = Math.Max(settings.EmaPeriod, settings.AtrPeriod);
        if (candles.Count < requiredPeriods)
            return false;

        // Validate settings
        if (settings.EmaPeriod <= 0 || settings.AtrPeriod <= 0 || settings.Multiplier <= 0)
            return false;

        // Check for data integrity
        return ValidateCandleData(candles);
    }

    /// <summary>
    /// Calculates the upper and lower channel bands
    /// </summary>
    private List<KeltnerChannelValue> CalculateChannelBands(
        IReadOnlyList<ExponentialMovingAverage> emaValues, 
        IReadOnlyList<AverageTrueRange> atrValues, 
        KeltnerChannelSettings settings,
        IReadOnlyList<Candle> candles)
    {
        var channels = new List<KeltnerChannelValue>();
        var startIndex = Math.Max(settings.EmaPeriod - 1, settings.AtrPeriod - 1);

        for (int i = startIndex; i < candles.Count; i++)
        {
            var emaIndex = FindValueIndex(emaValues, candles[i].OpenTime);
            var atrIndex = FindValueIndex(atrValues, candles[i].OpenTime);

            if (emaIndex >= 0 && atrIndex >= 0)
            {
                var ema = emaValues[emaIndex].Value;
                var atr = atrValues[atrIndex].Value;
                var channelWidth = atr * settings.Multiplier;
                var upperBand = ema + channelWidth;
                var lowerBand = ema - channelWidth;
                var channelValue = new KeltnerChannelValue(
                    candles[i].OpenTime,
                    Price.Create(ema),
                    Price.Create(upperBand),
                    Price.Create(lowerBand),
                    atr,
                    settings.Multiplier,
                    BandTouchType.None
                );
                channels.Add(channelValue);
            }
        }
        return channels;
    }

    /// <summary>
    /// Validates and filters calculation results
    /// </summary>
    private List<KeltnerChannelValue> ValidateAndFilterResults(
        List<KeltnerChannelValue> channels, 
        IReadOnlyList<Candle> candles)
    {
        var validChannels = new List<KeltnerChannelValue>();

        foreach (var channel in channels)
        {
            // Basic validation
            if (channel.UpperBand <= channel.MiddleBand || 
                channel.MiddleBand <= channel.LowerBand)
            {
                _logger.LogWarning("Invalid channel values at {Timestamp}: Upper={Upper}, Middle={Middle}, Lower={Lower}",
                    channel.Timestamp, channel.UpperBand, channel.MiddleBand, channel.LowerBand);
                continue;
            }
            validChannels.Add(channel);
        }
        return validChannels;
    }

    /// <summary>
    /// Calculates the price position within the channel (0 = lower band, 1 = upper band)
    /// </summary>
    private decimal CalculatePricePosition(decimal price, decimal lowerBand, decimal upperBand)
    {
        if (upperBand <= lowerBand)
            return 0.5m;

        var position = (price - lowerBand) / (upperBand - lowerBand);
        return Math.Max(0m, Math.Min(1m, position)); // Clamp between 0 and 1
    }

    /// <summary>
    /// Determines if price is touching or breaking through bands
    /// </summary>
    private BandTouchType DetermineBandTouch(Candle candle, decimal lowerBand, decimal upperBand, decimal middleBand)
    {
        const decimal TouchThreshold = 0.001m; // 0.1% threshold for "touching"

        // Check for band breaks (close beyond band)
        if (candle.Close > upperBand)
            return BandTouchType.UpperBreak;
        if (candle.Close < lowerBand)
            return BandTouchType.LowerBreak;

        // Check for band touches (high/low touches band within threshold)
        var upperTouchThreshold = upperBand * (1 - TouchThreshold);
        var lowerTouchThreshold = lowerBand * (1 + TouchThreshold);

        if (candle.High >= upperTouchThreshold && candle.High <= upperBand * (1 + TouchThreshold))
            return BandTouchType.UpperTouch;
        if (candle.Low <= lowerTouchThreshold && candle.Low >= lowerBand * (1 - TouchThreshold))
            return BandTouchType.LowerTouch;

        // Check middle band interaction
        var middleTouchThreshold = middleBand * TouchThreshold;
        if (Math.Abs(candle.Close - middleBand) <= middleTouchThreshold)
            return BandTouchType.MiddleTouch;

        return BandTouchType.None;
    }

    /// <summary>
    /// Finds the index of a value with matching timestamp
    /// </summary>
    private static int FindValueIndex<T>(IReadOnlyList<T> values, DateTime timestamp) where T : ITimestampedValue
    {
        // Binary search for better performance with large datasets
        int left = 0, right = values.Count - 1;
        
        while (left <= right)
        {
            int mid = (left + right) / 2;
            var comparison = values[mid].Timestamp.CompareTo(timestamp);
            
            if (comparison == 0)
                return mid;
            else if (comparison < 0)
                left = mid + 1;
            else
                right = mid - 1;
        }

        // If exact match not found, return the closest earlier value
        return right >= 0 ? right : -1;
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

            // Check volume (should be non-negative)
            if (candle.Volume < 0)
            {
                _logger.LogWarning("Negative volume at index {Index}: {Volume}", i, candle.Volume);
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    KeltnerChannel IKeltnerChannelCalculator.Calculate(
        IReadOnlyList<Candle> candles,
        int emaPeriod,
        int atrPeriod,
        decimal multiplier)
    {
        var settings = new KeltnerChannelSettings(emaPeriod, atrPeriod, multiplier);
        var result = CalculateInternal(candles, settings);
        
        if (!result.IsValid || result.Values.Count == 0)
            return new KeltnerChannel(Price.Create(0), Price.Create(0), Price.Create(0), 0m, DateTime.UtcNow);
        
        var latest = result.Values.Last();
        return new KeltnerChannel(
            Price.Create(latest.MiddleBand), 
            Price.Create(latest.UpperBand), 
            Price.Create(latest.LowerBand), 
            latest.Atr, 
            latest.Timestamp);
    }

    /// <inheritdoc />
    public IReadOnlyList<KeltnerChannel> CalculateSeries(
        IReadOnlyList<Candle> candles,
        int emaPeriod,
        int atrPeriod,
        decimal multiplier)
    {
        var settings = new KeltnerChannelSettings(emaPeriod, atrPeriod, multiplier);
        var result = CalculateInternal(candles, settings);
        
        return result.Values.Select(v => new KeltnerChannel(
            Price.Create(v.MiddleBand),
            Price.Create(v.UpperBand),
            Price.Create(v.LowerBand),
            v.Atr,
            v.Timestamp)).ToList();
    }

    /// <inheritdoc />
    public decimal CalculateDynamicMultiplier(
        IReadOnlyList<Candle> candles,
        decimal baseMultiplier = 2.0m,
        decimal minMultiplier = 1.5m,
        decimal maxMultiplier = 2.5m)
    {
        if (candles.Count < 20) return baseMultiplier;

        // Calculate recent volatility
        var recentCandles = candles.TakeLast(20).ToList();
        var volatility = CalculateVolatility(recentCandles);
        var avgVolatility = 0.02m; // Baseline 2% volatility
        
        var volatilityRatio = volatility / avgVolatility;
        var dynamicMultiplier = baseMultiplier * volatilityRatio;
        
        return Math.Max(minMultiplier, Math.Min(maxMultiplier, dynamicMultiplier));
    }

    /// <inheritdoc />
    public bool ValidateAccuracy(KeltnerChannel calculated, KeltnerChannel reference, decimal tolerance = 0.0001m)
    {
        var upperDiff = Math.Abs(calculated.UpperBand - reference.UpperBand) / reference.UpperBand;
        var middleDiff = Math.Abs(calculated.MiddleBand - reference.MiddleBand) / reference.MiddleBand;
        var lowerDiff = Math.Abs(calculated.LowerBand - reference.LowerBand) / reference.LowerBand;
        
        return upperDiff <= tolerance && middleDiff <= tolerance && lowerDiff <= tolerance;
    }

    private decimal CalculateVolatility(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 2) return 0;
        
        var returns = new List<decimal>();
        for (int i = 1; i < candles.Count; i++)
        {
            var returnValue = (candles[i].Close - candles[i-1].Close) / candles[i-1].Close;
            returns.Add(returnValue);
        }
        
        var avg = returns.Average();
        var variance = returns.Sum(r => (r - avg) * (r - avg)) / (returns.Count - 1);
        return (decimal)Math.Sqrt((double)variance);
    }
}

/// <summary>
/// Keltner Channel calculation result
/// </summary>
public class KeltnerChannelResult
{
    public IReadOnlyList<KeltnerChannelValue> Values { get; init; } = Array.Empty<KeltnerChannelValue>();
    public KeltnerChannelSettings Settings { get; init; } = null!;
    public bool IsValid { get; init; }
    public DateTime CalculatedAt { get; init; }

    public static readonly KeltnerChannelResult Empty = new()
    {
        Values = Array.Empty<KeltnerChannelValue>(),
        IsValid = false,
        CalculatedAt = DateTime.UtcNow
    };
} 