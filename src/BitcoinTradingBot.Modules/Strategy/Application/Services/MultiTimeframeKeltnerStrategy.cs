using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Events;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Strategy.Domain.Indicators;
using BitcoinTradingBot.Modules.Strategy.Domain.Strategies;
using BitcoinTradingBot.Modules.Strategy.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Strategy.Application.Services;

/// <summary>
/// Advanced Multi-Timeframe Keltner Channel strategy implementation
/// Implements sophisticated confluence analysis between 4H trend and 5M entry
/// </summary>
public class MultiTimeframeKeltnerStrategy : IMultiTimeframeKeltnerStrategy
{
    private readonly IKeltnerChannelCalculator _keltnerCalculator;
    private readonly IExponentialMovingAverageCalculator _emaCalculator;
    private readonly IAverageTrueRangeCalculator _atrCalculator;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MultiTimeframeKeltnerStrategy> _logger;

    // Strategy configuration
    private const int PRIMARY_EMA_PERIOD = 20;
    private const int PRIMARY_ATR_PERIOD = 10;
    private const int ENTRY_EMA_PERIOD = 20;
    private const int RSI_PERIOD = 14;
    private const int VOLUME_LOOKBACK = 20;

    // ITradingStrategy implementation
    public string StrategyName => "Multi-Timeframe Keltner Channel Strategy";
    public bool IsEnabled { get; set; } = true;

    public MultiTimeframeKeltnerStrategy(
        IKeltnerChannelCalculator keltnerCalculator,
        IExponentialMovingAverageCalculator emaCalculator,
        IAverageTrueRangeCalculator atrCalculator,
        IMemoryCache cache,
        ILogger<MultiTimeframeKeltnerStrategy> logger)
    {
        _keltnerCalculator = keltnerCalculator ?? throw new ArgumentNullException(nameof(keltnerCalculator));
        _emaCalculator = emaCalculator ?? throw new ArgumentNullException(nameof(emaCalculator));
        _atrCalculator = atrCalculator ?? throw new ArgumentNullException(nameof(atrCalculator));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// ITradingStrategy required method - evaluates signal for a symbol
    /// </summary>
    public async Task<TradingSignalData> EvaluateSignalAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsEnabled)
            {
                return TradingSignalDataExtensions.NoSignal(symbol, "Strategy disabled");
            }

            _logger.LogTrace("Evaluating signal for {Symbol} using {StrategyName}", symbol, StrategyName);

            // Get both timeframe data
            var primaryCandles = await GetPrimaryTimeframeDataAsync(symbol, cancellationToken);
            var entryCandles = await GetEntryTimeframeDataAsync(symbol, cancellationToken);

            if (primaryCandles == null || primaryCandles.Count < 50)
            {
                _logger.LogDebug("Insufficient primary timeframe data for {Symbol} - have {Count} candles, need 50", symbol, primaryCandles?.Count ?? 0);
                return TradingSignalDataExtensions.NoSignal(symbol, "Insufficient primary data");
            }

            if (entryCandles == null || entryCandles.Count < 50)
            {
                _logger.LogWarning("Insufficient entry timeframe data for {Symbol}", symbol);
                return TradingSignalDataExtensions.NoSignal(symbol, "Insufficient entry data");
            }

            return await EvaluateCompleteStrategyAsync(symbol, primaryCandles, entryCandles, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating signal for {Symbol}", symbol);
            return TradingSignalDataExtensions.NoSignal(symbol, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Main strategy evaluation entry point (for multi-timeframe calls)
    /// </summary>
    public async Task<TradingSignalData> EvaluateAsync(
        Symbol symbol, 
        TimeFrame timeFrame, 
        IReadOnlyList<Candle> candles, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Evaluating Multi-Timeframe Keltner strategy for {Symbol} on {TimeFrame}", symbol, timeFrame);

            // For this strategy, we need both 4H and 5M data
            // This method will be called with either timeframe, but we need both for evaluation
            
            if (timeFrame == TimeFrame.FiveMinutes)
            {
                // Get 4H data from cache or external source
                var primaryCandles = await GetPrimaryTimeframeDataAsync(symbol, cancellationToken);
                if (primaryCandles == null || primaryCandles.Count < 50)
                {
                    _logger.LogDebug("Insufficient primary timeframe data for {Symbol} - have {Count} candles, need 50", symbol, primaryCandles?.Count ?? 0);
                    return TradingSignalDataExtensions.NoSignal(symbol, "Insufficient primary data");
                }

                return await EvaluateCompleteStrategyAsync(symbol, primaryCandles, candles, cancellationToken);
            }

            // For 4H timeframe, just cache the data for future 5M evaluations
            CachePrimaryTimeframeData(symbol, candles);
            return TradingSignalDataExtensions.NoSignal(symbol, "Primary timeframe cached");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating strategy for {Symbol} on {TimeFrame}", symbol, timeFrame);
            return TradingSignalDataExtensions.NoSignal(symbol, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Evaluates PRIMARY timeframe (4H) conditions
    /// </summary>
    public async Task<PrimaryConditionResult> EvaluatePrimaryConditionsAsync(
        Symbol symbol, 
        IReadOnlyList<Candle> primaryCandles, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Evaluating primary conditions for {Symbol}", symbol);

            // Calculate indicators
            var keltnerChannel = _keltnerCalculator.Calculate(primaryCandles, PRIMARY_EMA_PERIOD, PRIMARY_ATR_PERIOD);
            var ema20 = _emaCalculator.Calculate(primaryCandles, PRIMARY_EMA_PERIOD);
            
            var latestCandle = primaryCandles.Last();
            var metadata = new Dictionary<string, object>();

            // PRIMARY CONDITION 1: Price touches/exceeds 4H KC upper band (>99% of band height)
            var bandHeight = keltnerChannel.UpperBand - keltnerChannel.LowerBand;
            var priceFromLower = latestCandle.High - keltnerChannel.LowerBand;
            var bandPenetrationPercentage = priceFromLower / bandHeight;
            var priceTouchedUpperBand = bandPenetrationPercentage >= 0.99m;
            
            metadata["bandPenetrationPercentage"] = bandPenetrationPercentage;

            // PRIMARY CONDITION 2: Price retraces toward KC middle band (within 85-95% range)
            var currentPriceFromLower = latestCandle.Close - keltnerChannel.LowerBand;
            var currentBandPosition = currentPriceFromLower / bandHeight;
            var priceInRetracementZone = currentBandPosition >= 0.85m && currentBandPosition <= 0.95m;
            
            metadata["currentBandPosition"] = currentBandPosition;

            // PRIMARY CONDITION 3: 4H 20 EMA positioned below KC middle band (bearish confirmation)
            var emaPositionValid = ema20.Value < keltnerChannel.MiddleBand;
            var emaToBandDistance = keltnerChannel.MiddleBand - ema20.Value;
            
            metadata["emaToBandDistance"] = emaToBandDistance;

            // PRIMARY CONDITION 4: No 4H candle closes below 20 EMA during setup (last 5 candles)
            var recentCandles = primaryCandles.TakeLast(5).ToList();
            var noBreaksBelowEma = !recentCandles.Any(c => c.Close < ema20.Value);
            
            metadata["recentCandlesAboveEma"] = recentCandles.Count(c => c.Close >= ema20.Value);

            // PRIMARY CONDITION 5: Volume confirmation: Above 20-period volume average
            var averageVolume = primaryCandles.TakeLast(VOLUME_LOOKBACK).Select(c => (decimal)c.Volume).Average();
            var volumeRatio = latestCandle.Volume / averageVolume;
            var volumeConfirmation = volumeRatio > 1.0m;
            
            metadata["averageVolume"] = averageVolume;
            metadata["currentVolume"] = latestCandle.Volume;

            var isValid = priceTouchedUpperBand && priceInRetracementZone && emaPositionValid && 
                         noBreaksBelowEma && volumeConfirmation;

            var result = new PrimaryConditionResult(
                IsValid: isValid,
                KeltnerChannel: keltnerChannel,
                Ema20: ema20,
                PriceTouchedUpperBand: priceTouchedUpperBand,
                PriceInRetracementZone: priceInRetracementZone,
                EmaPositionValid: emaPositionValid,
                NoBreaksBelowEma: noBreaksBelowEma,
                VolumeConfirmation: volumeConfirmation,
                VolumeRatio: volumeRatio,
                EvaluatedAt: DateTime.UtcNow,
                Metadata: metadata
            );

            _logger.LogDebug("Primary conditions result: {IsValid} - Touch: {Touch}, Retracement: {Retracement}, EMA: {EMA}, NoBreaks: {NoBreaks}, Volume: {Volume}",
                isValid, priceTouchedUpperBand, priceInRetracementZone, emaPositionValid, noBreaksBelowEma, volumeConfirmation);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating primary conditions for {Symbol}", symbol);
            throw;
        }
    }

    /// <summary>
    /// Evaluates ENTRY timeframe (5M) conditions
    /// </summary>
    public async Task<EntryConditionResult> EvaluateEntryConditionsAsync(
        Symbol symbol, 
        IReadOnlyList<Candle> entryCandles, 
        PrimaryConditionResult primaryConditions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Evaluating entry conditions for {Symbol}", symbol);

            // Calculate 5M indicators
            var keltnerChannel = _keltnerCalculator.Calculate(entryCandles, ENTRY_EMA_PERIOD, PRIMARY_ATR_PERIOD);
            var ema20 = _emaCalculator.Calculate(entryCandles, ENTRY_EMA_PERIOD);
            var atr = _atrCalculator.Calculate(entryCandles, 14);
            
            var latestCandle = entryCandles.Last();
            var previousCandle = entryCandles[entryCandles.Count - 2];
            var metadata = new Dictionary<string, object>();

            // ENTRY CONDITION 6: Price crosses above 5M 20 EMA with momentum
            var priceCrossedAboveEma = previousCandle.Close <= ema20.Value && latestCandle.Close > ema20.Value;
            var momentumConfirmed = latestCandle.Close > latestCandle.Open; // Bullish candle
            
            metadata["previousCloseToEma"] = previousCandle.Close - ema20.Value;
            metadata["currentCloseToEma"] = latestCandle.Close - ema20.Value;

            // ENTRY CONDITION 7: 5M RSI > 50 (momentum confirmation)
            var rsiValue = CalculateRSI(entryCandles, RSI_PERIOD);
            var momentumConfirmation = rsiValue > 50m;
            
            metadata["rsiValue"] = rsiValue;

            // ENTRY CONDITION 8: No conflicting 1H signals (simplified - check recent trend)
            var emaSlope = _emaCalculator.CalculateSlope(_emaCalculator.CalculateSeries(entryCandles.TakeLast(10).ToList(), ENTRY_EMA_PERIOD));
            var noConflictingSignals = emaSlope >= 0; // EMA trending up or flat
            
            metadata["emaSlope"] = emaSlope;

            // ENTRY CONDITION 9: Volatility filter: ATR within normal range
            var historicalAtr = _atrCalculator.CalculateSeries(entryCandles.TakeLast(50).ToList(), 14);
            var volatilityRegime = _atrCalculator.DetermineVolatilityRegime(atr, historicalAtr);
            var volatilityFilter = volatilityRegime != VolatilityRegime.Extreme;
            
            metadata["volatilityRegime"] = volatilityRegime.ToString();
            metadata["currentAtr"] = atr.Value;

            var isValid = priceCrossedAboveEma && momentumConfirmed && momentumConfirmation && 
                         noConflictingSignals && volatilityFilter;

            var result = new EntryConditionResult(
                IsValid: isValid,
                KeltnerChannel: keltnerChannel,
                Ema20: ema20,
                PriceCrossedAboveEma: priceCrossedAboveEma,
                MomentumConfirmation: momentumConfirmation,
                RsiValue: rsiValue,
                NoConflictingSignals: noConflictingSignals,
                VolatilityFilter: volatilityFilter,
                VolatilityRegime: volatilityRegime,
                EvaluatedAt: DateTime.UtcNow,
                Metadata: metadata
            );

            _logger.LogDebug("Entry conditions result: {IsValid} - Cross: {Cross}, Momentum: {Momentum}, RSI: {RSI}, NoConflict: {NoConflict}, Volatility: {Volatility}",
                isValid, priceCrossedAboveEma, momentumConfirmation, rsiValue, noConflictingSignals, volatilityFilter);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating entry conditions for {Symbol}", symbol);
            throw;
        }
    }

    /// <summary>
    /// Validates confluence across multiple timeframes
    /// </summary>
    public async Task<ConfluenceResult> ValidateConfluenceAsync(
        Symbol symbol,
        PrimaryConditionResult primaryConditions,
        EntryConditionResult entryConditions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Validating confluence for {Symbol}", symbol);

            var validatedFactors = new List<string>();
            var failedFactors = new List<string>();

            // CONFLUENCE 10: Multiple timeframe EMA alignment
            var multiTimeframeEmaAlignment = primaryConditions.Ema20.Value < primaryConditions.KeltnerChannel.MiddleBand &&
                                           entryConditions.Ema20.Value < entryConditions.KeltnerChannel.MiddleBand;
            
            if (multiTimeframeEmaAlignment)
                validatedFactors.Add("MultiTimeframeEmaAlignment");
            else
                failedFactors.Add("MultiTimeframeEmaAlignment");

            // CONFLUENCE 11: Support/resistance level confirmation (simplified)
            // Check if current price is near recent support levels
            var supportResistanceConfirmation = true; // Simplified for now
            validatedFactors.Add("SupportResistanceConfirmation");

            // CONFLUENCE 12: Market structure analysis (higher lows pattern)
            var marketStructureValid = ValidateMarketStructure(primaryConditions, entryConditions);
            
            if (marketStructureValid)
                validatedFactors.Add("MarketStructureValid");
            else
                failedFactors.Add("MarketStructureValid");

            // Calculate confidence score based on validated factors
            var totalFactors = validatedFactors.Count + failedFactors.Count;
            var confidenceScore = totalFactors > 0 ? (decimal)validatedFactors.Count / totalFactors * 100 : 0;

            var isValid = multiTimeframeEmaAlignment && supportResistanceConfirmation && marketStructureValid;

            var result = new ConfluenceResult(
                IsValid: isValid,
                MultiTimeframeEmaAlignment: multiTimeframeEmaAlignment,
                SupportResistanceConfirmation: supportResistanceConfirmation,
                MarketStructureValid: marketStructureValid,
                ConfidenceScore: confidenceScore,
                ValidatedFactors: validatedFactors.ToArray(),
                FailedFactors: failedFactors.ToArray(),
                EvaluatedAt: DateTime.UtcNow
            );

            _logger.LogDebug("Confluence validation: {IsValid} with {ConfidenceScore}% confidence", isValid, confidenceScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating confluence for {Symbol}", symbol);
            throw;
        }
    }

    /// <summary>
    /// Calculates stop loss and take profit levels
    /// </summary>
    public async Task<RiskManagementLevels> CalculateRiskLevelsAsync(
        Price entryPrice,
        TradingSignal signal,
        IReadOnlyList<Candle> primaryCandles,
        IReadOnlyList<Candle> entryCandles)
    {
        try
        {
            var primaryEma = _emaCalculator.Calculate(primaryCandles, PRIMARY_EMA_PERIOD);
            var primaryKC = _keltnerCalculator.Calculate(primaryCandles, PRIMARY_EMA_PERIOD, PRIMARY_ATR_PERIOD);
            var entryAtr = _atrCalculator.Calculate(entryCandles, 14);

            if (signal == TradingSignal.Buy)
            {
                // Initial stop at 4H 20 EMA
                var initialStopLoss = Price.Create(Math.Min(primaryEma.Value, entryPrice - (entryAtr.Value * 2)));
                
                // Secondary stop at KC lower band
                var secondaryStopLoss = Price.Create(primaryKC.LowerBand);
                
                // Take profit levels at 1R, 2R, 3R
                var riskAmount = entryPrice - initialStopLoss;
                var takeProfit1 = Price.Create(entryPrice + (riskAmount * 1));
                var takeProfit2 = Price.Create(entryPrice + (riskAmount * 2));
                var takeProfit3 = Price.Create(entryPrice + (riskAmount * 3));
                
                // Trailing stop reference (KC middle band)
                var trailingStopReference = Price.Create(primaryKC.MiddleBand);
                
                var riskRewardRatio = riskAmount > 0 ? (takeProfit2 - entryPrice) / riskAmount : 0;

                return new RiskManagementLevels(
                    InitialStopLoss: initialStopLoss,
                    SecondaryStopLoss: secondaryStopLoss,
                    TakeProfit1: takeProfit1,
                    TakeProfit2: takeProfit2,
                    TakeProfit3: takeProfit3,
                    TrailingStopReference: trailingStopReference,
                    RiskRewardRatio: riskRewardRatio,
                    StopLossReason: "4H 20 EMA / 2x ATR",
                    TakeProfitReason: "1R, 2R, 3R levels"
                );
            }

            throw new NotSupportedException("Only BUY signals are supported in this version");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating risk levels");
            throw;
        }
    }

    // Private helper methods

    private async Task<TradingSignalData> EvaluateCompleteStrategyAsync(
        Symbol symbol, 
        IReadOnlyList<Candle> primaryCandles, 
        IReadOnlyList<Candle> entryCandles, 
        CancellationToken cancellationToken)
    {
        // Step 1: Evaluate primary conditions
        var primaryConditions = await EvaluatePrimaryConditionsAsync(symbol, primaryCandles, cancellationToken);
        if (!primaryConditions.IsValid)
        {
            return TradingSignalDataExtensions.NoSignal(symbol, "Primary conditions not met");
        }

        // Step 2: Evaluate entry conditions
        var entryConditions = await EvaluateEntryConditionsAsync(symbol, entryCandles, primaryConditions, cancellationToken);
        if (!entryConditions.IsValid)
        {
            return TradingSignalDataExtensions.NoSignal(symbol, "Entry conditions not met");
        }

        // Step 3: Validate confluence
        var confluence = await ValidateConfluenceAsync(symbol, primaryConditions, entryConditions, cancellationToken);
        if (!confluence.IsValid)
        {
            return TradingSignalDataExtensions.NoSignal(symbol, "Confluence validation failed");
        }

        // Step 4: Calculate risk levels
        var entryPrice = Price.Create(entryCandles.Last().Close);
        var riskLevels = await CalculateRiskLevelsAsync(entryPrice, TradingSignal.Buy, primaryCandles, entryCandles);

        // Generate BUY signal
        return new TradingSignalData(
            Id: Guid.NewGuid().ToString(),
            Symbol: symbol,
            Signal: TradingSignal.Buy,
            EntryPrice: entryPrice,
            StopLoss: riskLevels.InitialStopLoss,
            TakeProfit: riskLevels.TakeProfit2,
            Confidence: confluence.ConfidenceScore / 100,
            SignalTime: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>
            {
                ["PrimaryConditions"] = primaryConditions,
                ["EntryConditions"] = entryConditions,
                ["Confluence"] = confluence,
                ["RiskLevels"] = riskLevels,
                ["RiskRewardRatio"] = riskLevels.RiskRewardRatio
            }
        );
    }

    private async Task<IReadOnlyList<Candle>?> GetPrimaryTimeframeDataAsync(Symbol symbol, CancellationToken cancellationToken)
    {
        var cacheKey = $"primary_candles_{symbol}";
        return _cache.Get<IReadOnlyList<Candle>>(cacheKey);
    }

    private async Task<IReadOnlyList<Candle>?> GetEntryTimeframeDataAsync(Symbol symbol, CancellationToken cancellationToken)
    {
        var cacheKey = $"entry_candles_{symbol}";
        return _cache.Get<IReadOnlyList<Candle>>(cacheKey);
    }

    private void CachePrimaryTimeframeData(Symbol symbol, IReadOnlyList<Candle> candles)
    {
        var cacheKey = $"primary_candles_{symbol}";
        _cache.Set(cacheKey, candles, TimeSpan.FromMinutes(5));
    }

    private void CacheEntryTimeframeData(Symbol symbol, IReadOnlyList<Candle> candles)
    {
        var cacheKey = $"entry_candles_{symbol}";
        _cache.Set(cacheKey, candles, TimeSpan.FromMinutes(1));
    }

    private decimal CalculateRSI(IReadOnlyList<Candle> candles, int period)
    {
        // Simplified RSI calculation
        if (candles.Count < period + 1) return 50m;

        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < candles.Count; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            gains.Add(Math.Max(change, 0));
            losses.Add(Math.Max(-change, 0));
        }

        var avgGain = gains.TakeLast(period).Average();
        var avgLoss = losses.TakeLast(period).Average();

        if (avgLoss == 0) return 100m;

        var rs = avgGain / avgLoss;
        return 100 - (100 / (1 + rs));
    }

    private bool ValidateMarketStructure(PrimaryConditionResult primaryConditions, EntryConditionResult entryConditions)
    {
        // Simplified market structure validation
        // In a real implementation, this would analyze swing highs/lows
        return primaryConditions.EmaPositionValid && entryConditions.PriceCrossedAboveEma;
    }
} 