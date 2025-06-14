using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Modules.Risk.Domain.Models;

/// <summary>
/// Configuration parameters for risk management system
/// </summary>
public record RiskParameters
{
    /// <summary>
    /// Maximum risk percentage per trade (e.g., 0.02 for 2%)
    /// </summary>
    public decimal MaxRiskPerTrade { get; init; } = 0.02m;

    /// <summary>
    /// Maximum portfolio exposure percentage (e.g., 0.15 for 15%)
    /// </summary>
    public decimal MaxPortfolioExposure { get; init; } = 0.15m;

    /// <summary>
    /// Maximum daily loss threshold (e.g., 0.05 for 5%)
    /// </summary>
    public decimal MaxDailyLoss { get; init; } = 0.05m;

    /// <summary>
    /// Maximum intraday drawdown before circuit breaker triggers (e.g., 0.05 for 5%)
    /// </summary>
    public decimal MaxIntradayDrawdown { get; init; } = 0.05m;

    /// <summary>
    /// Kelly Criterion multiplier for position sizing (0.25 = 25% of Kelly)
    /// </summary>
    public decimal KellyMultiplier { get; init; } = 0.25m;

    /// <summary>
    /// Minimum Kelly criterion for trade validation (e.g., 0.05 for 5%)
    /// </summary>
    public decimal MinKellyCriterion { get; init; } = 0.05m;

    /// <summary>
    /// Maximum Kelly criterion to prevent over-leveraging (e.g., 0.25 for 25%)
    /// </summary>
    public decimal MaxKellyCriterion { get; init; } = 0.25m;

    /// <summary>
    /// Volatility adjustment factor (higher = more conservative during high volatility)
    /// </summary>
    public decimal VolatilityAdjustmentFactor { get; init; } = 1.5m;

    /// <summary>
    /// Maximum acceptable slippage percentage (e.g., 0.001 for 0.1%)
    /// </summary>
    public decimal MaxSlippage { get; init; } = 0.001m;

    /// <summary>
    /// Stop loss percentage from entry (e.g., 0.02 for 2%)
    /// </summary>
    public decimal InitialStopLossPercentage { get; init; } = 0.02m;

    /// <summary>
    /// Trailing stop activation threshold (e.g., 0.01 for 1% profit)
    /// </summary>
    public decimal TrailingStopActivation { get; init; } = 0.01m;

    /// <summary>
    /// Trailing stop distance percentage (e.g., 0.005 for 0.5%)
    /// </summary>
    public decimal TrailingStopDistance { get; init; } = 0.005m;

    /// <summary>
    /// Maximum number of open positions
    /// </summary>
    public int MaxOpenPositions { get; init; } = 3;

    /// <summary>
    /// Maximum correlation threshold between positions (e.g., 0.7 for 70%)
    /// </summary>
    public decimal MaxPositionCorrelation { get; init; } = 0.7m;

    /// <summary>
    /// Minimum time between trades in seconds (to prevent overtrading)
    /// </summary>
    public int MinTimeBetweenTrades { get; init; } = 300; // 5 minutes

    /// <summary>
    /// Circuit breaker cool-down period in minutes
    /// </summary>
    public int CircuitBreakerCooldown { get; init; } = 60;

    /// <summary>
    /// Enable/disable risk management system
    /// </summary>
    public bool RiskManagementEnabled { get; init; } = true;

    /// <summary>
    /// Enable/disable circuit breakers
    /// </summary>
    public bool CircuitBreakersEnabled { get; init; } = true;

    /// <summary>
    /// Enable/disable Kelly Criterion position sizing
    /// </summary>
    public bool KellyCriterionEnabled { get; init; } = true;

    /// <summary>
    /// Enable/disable trailing stops
    /// </summary>
    public bool TrailingStopsEnabled { get; init; } = true;

    /// <summary>
    /// Validate risk parameters for consistency
    /// </summary>
    public bool IsValid()
    {
        return MaxRiskPerTrade > 0 && MaxRiskPerTrade <= 0.1m && // Max 10% per trade
               MaxPortfolioExposure > 0 && MaxPortfolioExposure <= 1.0m &&
               MaxDailyLoss > 0 && MaxDailyLoss <= 0.2m && // Max 20% daily loss
               KellyMultiplier > 0 && KellyMultiplier <= 1.0m &&
               MinKellyCriterion >= 0 && MinKellyCriterion < MaxKellyCriterion &&
               MaxSlippage >= 0 && MaxSlippage <= 0.05m && // Max 5% slippage
               MaxOpenPositions > 0 && MaxOpenPositions <= 10;
    }
} 