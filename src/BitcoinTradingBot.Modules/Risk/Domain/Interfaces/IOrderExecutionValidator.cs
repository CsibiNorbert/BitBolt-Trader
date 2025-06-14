using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Risk.Domain.Models;

namespace BitcoinTradingBot.Modules.Risk.Domain.Interfaces;

/// <summary>
/// Interface for validating orders and managing execution quality with slippage protection
/// </summary>
public interface IOrderExecutionValidator
{
    /// <summary>
    /// Validate order parameters before execution
    /// </summary>
    /// <param name="order">Order to validate</param>
    /// <param name="marketConditions">Current market conditions</param>
    /// <param name="accountState">Current account state</param>
    /// <returns>Order validation result</returns>
    Task<OrderValidationResult> ValidateOrderAsync(
        Order order, 
        MarketConditions marketConditions, 
        AccountState accountState);

    /// <summary>
    /// Calculate maximum acceptable slippage for an order
    /// </summary>
    /// <param name="order">Order to calculate slippage for</param>
    /// <param name="marketLiquidity">Current market liquidity</param>
    /// <param name="volatility">Current volatility</param>
    /// <returns>Maximum acceptable slippage</returns>
    Task<SlippageCalculation> CalculateMaxSlippageAsync(
        Order order, 
        decimal marketLiquidity, 
        decimal volatility);

    /// <summary>
    /// Determine optimal order type based on market conditions
    /// </summary>
    /// <param name="signal">Trading signal</param>
    /// <param name="marketConditions">Current market conditions</param>
    /// <param name="urgency">Order urgency level</param>
    /// <returns>Recommended order type and parameters</returns>
    Task<OrderTypeRecommendation> RecommendOrderTypeAsync(
        TradingSignalData signal, 
        MarketConditions marketConditions, 
        OrderUrgency urgency);

    /// <summary>
    /// Validate order execution result and detect issues
    /// </summary>
    /// <param name="order">Original order</param>
    /// <param name="executionResult">Execution result from exchange</param>
    /// <param name="expectedPrice">Expected execution price</param>
    /// <returns>Execution quality analysis</returns>
    Task<ExecutionQualityResult> AnalyzeExecutionQualityAsync(
        Order order, 
        OrderExecutionResult executionResult, 
        Price expectedPrice);

    /// <summary>
    /// Check if order should be cancelled due to changed market conditions
    /// </summary>
    /// <param name="pendingOrder">Pending order to check</param>
    /// <param name="currentConditions">Current market conditions</param>
    /// <param name="originalConditions">Market conditions when order was placed</param>
    /// <returns>Order cancellation recommendation</returns>
    Task<OrderCancellationResult> ShouldCancelOrderAsync(
        Order pendingOrder, 
        MarketConditions currentConditions, 
        MarketConditions originalConditions);
} 