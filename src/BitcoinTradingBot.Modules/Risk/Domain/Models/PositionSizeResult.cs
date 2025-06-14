using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Modules.Risk.Domain.Models;

/// <summary>
/// Result of position size calculation with detailed breakdown
/// </summary>
public record PositionSizeResult
{
    /// <summary>
    /// Recommended position size
    /// </summary>
    public required Quantity PositionSize { get; init; }

    /// <summary>
    /// Kelly Criterion optimal size percentage
    /// </summary>
    public decimal KellyOptimalSize { get; init; }

    /// <summary>
    /// Risk amount in base currency
    /// </summary>
    public required Price RiskAmount { get; init; }

    /// <summary>
    /// Risk percentage of total equity
    /// </summary>
    public decimal RiskPercentage { get; init; }

    /// <summary>
    /// Volatility adjustment applied
    /// </summary>
    public decimal VolatilityAdjustment { get; init; }

    /// <summary>
    /// Drawdown adjustment applied
    /// </summary>
    public decimal DrawdownAdjustment { get; init; }

    /// <summary>
    /// Final adjustment multiplier applied
    /// </summary>
    public decimal FinalAdjustment { get; init; }

    /// <summary>
    /// Whether position size meets minimum requirements
    /// </summary>
    public bool IsValidSize { get; init; }

    /// <summary>
    /// Reason if position size is invalid
    /// </summary>
    public string? InvalidReason { get; init; }

    /// <summary>
    /// Expected profit at 1R, 2R, 3R levels
    /// </summary>
    public Dictionary<string, Price> ExpectedProfits { get; init; } = new();

    /// <summary>
    /// Risk-reward ratios at different levels
    /// </summary>
    public Dictionary<string, decimal> RiskRewardRatios { get; init; } = new();

    /// <summary>
    /// Calculation metadata
    /// </summary>
    public Dictionary<string, object> CalculationMetadata { get; init; } = new();

    /// <summary>
    /// Timestamp when calculation was performed
    /// </summary>
    public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Create successful position size result
    /// </summary>
    public static PositionSizeResult Success(
        Quantity positionSize,
        Price riskAmount,
        decimal riskPercentage,
        decimal kellyOptimalSize = 0,
        decimal volatilityAdjustment = 1.0m,
        decimal drawdownAdjustment = 1.0m)
    {
        return new PositionSizeResult
        {
            PositionSize = positionSize,
            RiskAmount = riskAmount,
            RiskPercentage = riskPercentage,
            KellyOptimalSize = kellyOptimalSize,
            VolatilityAdjustment = volatilityAdjustment,
            DrawdownAdjustment = drawdownAdjustment,
            FinalAdjustment = volatilityAdjustment * drawdownAdjustment,
            IsValidSize = true
        };
    }

    /// <summary>
    /// Create failed position size result
    /// </summary>
    public static PositionSizeResult Failure(string reason)
    {
        return new PositionSizeResult
        {
            PositionSize = Quantity.Zero(),
            RiskAmount = Price.Zero(),
            RiskPercentage = 0,
            IsValidSize = false,
            InvalidReason = reason
        };
    }
} 