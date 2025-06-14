using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Risk.Domain.Models;

namespace BitcoinTradingBot.Modules.Risk.Domain.Interfaces;

/// <summary>
/// Main risk management interface that orchestrates position sizing, risk controls, and order validation
/// </summary>
public interface IRiskManager
{
    /// <summary>
    /// Validate if a trade meets all risk management criteria
    /// </summary>
    /// <param name="signal">Trading signal to validate</param>
    /// <param name="currentAccount">Current account state</param>
    /// <returns>Risk validation result</returns>
    Task<RiskValidationResult> ValidateTradeAsync(TradingSignalData signal, AccountState currentAccount);

    /// <summary>
    /// Calculate optimal position size for a trade
    /// </summary>
    /// <param name="signal">Trading signal</param>
    /// <param name="accountEquity">Current account equity</param>
    /// <param name="riskParameters">Risk management parameters</param>
    /// <returns>Position sizing result</returns>
    Task<PositionSizeResult> CalculatePositionSizeAsync(
        TradingSignalData signal, 
        Price accountEquity, 
        RiskParameters riskParameters);

    /// <summary>
    /// Check if circuit breaker conditions are triggered
    /// </summary>
    /// <param name="currentAccount">Current account state</param>
    /// <param name="marketConditions">Current market conditions</param>
    /// <returns>Circuit breaker status</returns>
    Task<CircuitBreakerResult> CheckCircuitBreakersAsync(
        AccountState currentAccount, 
        MarketConditions marketConditions);

    /// <summary>
    /// Calculate stop loss levels for a position
    /// </summary>
    /// <param name="entryPrice">Entry price</param>
    /// <param name="signal">Trading signal with context</param>
    /// <param name="riskParameters">Risk parameters</param>
    /// <returns>Stop loss levels</returns>
    Task<StopLossLevels> CalculateStopLossLevelsAsync(
        Price entryPrice, 
        TradingSignalData signal, 
        RiskParameters riskParameters);

    /// <summary>
    /// Monitor position and update trailing stops
    /// </summary>
    /// <param name="position">Current position</param>
    /// <param name="currentPrice">Current market price</param>
    /// <param name="marketData">Current market data</param>
    /// <returns>Updated position with new stop levels</returns>
    Task<Position> UpdateTrailingStopsAsync(
        Position position, 
        Price currentPrice, 
        MarketData marketData);

    /// <summary>
    /// Check if position should be closed due to risk management rules
    /// </summary>
    /// <param name="position">Position to evaluate</param>
    /// <param name="currentAccount">Current account state</param>
    /// <param name="marketConditions">Current market conditions</param>
    /// <returns>Position closure recommendation</returns>
    Task<PositionClosureResult> ShouldClosePositionAsync(
        Position position, 
        AccountState currentAccount, 
        MarketConditions marketConditions);
} 