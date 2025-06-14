using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Risk.Domain.Models;

namespace BitcoinTradingBot.Modules.Risk.Domain.Interfaces;

/// <summary>
/// Interface for calculating optimal position sizes using Kelly Criterion and risk management principles
/// </summary>
public interface IPositionSizingCalculator
{
    /// <summary>
    /// Calculate position size using Kelly Criterion with volatility adjustment
    /// </summary>
    /// <param name="accountEquity">Current account equity</param>
    /// <param name="riskPercentage">Risk percentage per trade (e.g., 0.02 for 2%)</param>
    /// <param name="entryPrice">Planned entry price</param>
    /// <param name="stopLossPrice">Stop loss price</param>
    /// <param name="volatilityMultiplier">Volatility adjustment multiplier (0.5 to 2.0)</param>
    /// <returns>Calculated position size</returns>
    Task<PositionSizeResult> CalculatePositionSizeAsync(
        Price accountEquity,
        decimal riskPercentage,
        Price entryPrice,
        Price stopLossPrice,
        decimal volatilityMultiplier = 1.0m);

    /// <summary>
    /// Calculate Kelly Criterion optimal position size based on historical performance
    /// </summary>
    /// <param name="winRate">Historical win rate (0.0 to 1.0)</param>
    /// <param name="averageWin">Average winning trade amount</param>
    /// <param name="averageLoss">Average losing trade amount</param>
    /// <param name="accountEquity">Current account equity</param>
    /// <returns>Kelly-optimized position size percentage</returns>
    Task<decimal> CalculateKellyOptimalSizeAsync(
        decimal winRate,
        decimal averageWin,
        decimal averageLoss,
        Price accountEquity);

    /// <summary>
    /// Adjust position size based on current drawdown
    /// </summary>
    /// <param name="basePositionSize">Base position size</param>
    /// <param name="currentDrawdown">Current drawdown percentage</param>
    /// <param name="maxDrawdownThreshold">Maximum drawdown threshold</param>
    /// <returns>Adjusted position size</returns>
    Task<Quantity> AdjustForDrawdownAsync(
        Quantity basePositionSize,
        decimal currentDrawdown,
        decimal maxDrawdownThreshold);
} 