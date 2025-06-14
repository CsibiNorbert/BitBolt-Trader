using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Strategy.Domain.Indicators;

namespace BitcoinTradingBot.Modules.Strategy.Domain.Models;

/// <summary>
/// Result of primary timeframe (4H) condition evaluation
/// </summary>
public record PrimaryConditionResult(
    bool IsValid,
    KeltnerChannel KeltnerChannel,
    ExponentialMovingAverage Ema20,
    bool PriceTouchedUpperBand,
    bool PriceInRetracementZone,
    bool EmaPositionValid,
    bool NoBreaksBelowEma,
    bool VolumeConfirmation,
    decimal VolumeRatio,
    DateTime EvaluatedAt,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Result of entry timeframe (5M) condition evaluation
/// </summary>
public record EntryConditionResult(
    bool IsValid,
    KeltnerChannel KeltnerChannel,
    ExponentialMovingAverage Ema20,
    bool PriceCrossedAboveEma,
    bool MomentumConfirmation,
    decimal RsiValue,
    bool NoConflictingSignals,
    bool VolatilityFilter,
    VolatilityRegime VolatilityRegime,
    DateTime EvaluatedAt,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Result of confluence validation across timeframes
/// </summary>
public record ConfluenceResult(
    bool IsValid,
    bool MultiTimeframeEmaAlignment,
    bool SupportResistanceConfirmation,
    bool MarketStructureValid,
    decimal ConfidenceScore,
    string[] ValidatedFactors,
    string[] FailedFactors,
    DateTime EvaluatedAt
);

/// <summary>
/// Risk management levels for trade execution
/// </summary>
public record RiskManagementLevels(
    Price InitialStopLoss,
    Price SecondaryStopLoss,
    Price TakeProfit1,
    Price TakeProfit2,
    Price TakeProfit3,
    Price TrailingStopReference,
    decimal RiskRewardRatio,
    string StopLossReason,
    string TakeProfitReason
);

/// <summary>
/// Strategy configuration parameters
/// </summary>
public record StrategyConfiguration(
    Symbol TradingSymbol,
    TimeFrame PrimaryTimeFrame,
    TimeFrame EntryTimeFrame,
    int PrimaryEmaPeriod,
    int PrimaryAtrPeriod,
    int EntryEmaPeriod,
    decimal KeltnerMultiplier,
    decimal MaxRiskPerTrade,
    decimal MaxPositionSize,
    bool IsEnabled,
    Dictionary<string, object> CustomParameters
);

/// <summary>
/// Signal confidence scoring details
/// </summary>
public record SignalConfidence(
    decimal OverallScore,
    decimal TechnicalScore,
    decimal VolumeScore,
    decimal TrendScore,
    decimal MomentumScore,
    Dictionary<string, decimal> ComponentScores,
    string[] PositiveFactors,
    string[] NegativeFactors
);

/// <summary>
/// Market condition assessment
/// </summary>
public record MarketCondition(
    VolatilityRegime VolatilityRegime,
    TrendDirection TrendDirection,
    decimal TrendStrength,
    decimal MarketSentiment,
    bool IsHighLiquidity,
    DateTime AssessedAt
);

/// <summary>
/// Trend direction enumeration
/// </summary>
public enum TrendDirection
{
    StrongBearish = -2,
    Bearish = -1,
    Sideways = 0,
    Bullish = 1,
    StrongBullish = 2
} 