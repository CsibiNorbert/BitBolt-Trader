using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Risk.Domain.Interfaces;
using BitcoinTradingBot.Modules.Risk.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Risk.Infrastructure.Services;

/// <summary>
/// Implementation of order validation and execution quality management
/// </summary>
public class OrderExecutionValidator : IOrderExecutionValidator
{
    private readonly ILogger<OrderExecutionValidator> _logger;

    public OrderExecutionValidator(ILogger<OrderExecutionValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrderValidationResult> ValidateOrderAsync(
        Order order, 
        MarketConditions marketConditions, 
        AccountState accountState)
    {
        try
        {
            _logger.LogDebug("Validating order {OrderId} for {Symbol}", order.Id, order.Symbol.Value);

            var errors = new List<string>();
            var warnings = new List<string>();
            var riskScore = 0m;

            // Basic order validation
            if (order.Quantity.Value <= 0)
                errors.Add("Order quantity must be greater than zero");

            if (order.Price?.Value <= 0)
                errors.Add("Order price must be greater than zero");

            // Account balance validation
            var requiredBalance = order.Quantity.Value * (order.Price?.Value ?? 0);
            if (requiredBalance > accountState.AvailableBalance.Value)
                errors.Add($"Insufficient balance. Required: {requiredBalance}, Available: {accountState.AvailableBalance.Value}");

            // Position limits validation
            if (accountState.OpenPositionCount >= 10) // Max positions
                errors.Add("Maximum number of positions reached");

            // Market conditions validation
            if (!marketConditions.IsSuitableForTrading())
            {
                errors.Add("Market conditions not suitable for trading");
                riskScore += 50m;
            }

            // Volatility check
            if (marketConditions.VolatilityRegime == Strategy.Domain.Indicators.VolatilityRegime.Extreme)
            {
                warnings.Add("Extreme volatility detected");
                riskScore += 30m;
            }

            // Liquidity check
            if (marketConditions.Liquidity < 50)
            {
                warnings.Add("Low liquidity conditions");
                riskScore += 20m;
            }

            // Spread check
            if (marketConditions.BidAskSpread > 0.01m) // 1% spread
            {
                warnings.Add("Wide bid-ask spread detected");
                riskScore += 15m;
            }

            // Determine risk level
            var riskLevel = riskScore switch
            {
                < 20 => RiskLevel.Low,
                < 40 => RiskLevel.Medium,
                < 70 => RiskLevel.High,
                _ => RiskLevel.VeryHigh
            };

            if (errors.Any())
            {
                _logger.LogWarning("Order validation failed for {OrderId}: {Errors}", order.Id, string.Join(", ", errors));
                return OrderValidationResult.Failure(errors, riskLevel);
            }

            _logger.LogInformation("Order {OrderId} validated successfully with risk level {RiskLevel}", order.Id, riskLevel);
            return OrderValidationResult.Success(riskLevel, riskScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating order {OrderId}", order.Id);
            return OrderValidationResult.Failure(new List<string> { $"Validation error: {ex.Message}" });
        }
    }

    /// <inheritdoc />
    public async Task<SlippageCalculation> CalculateMaxSlippageAsync(
        Order order, 
        decimal marketLiquidity, 
        decimal volatility)
    {
        try
        {
            _logger.LogDebug("Calculating slippage for order {OrderId}, Liquidity: {Liquidity}, Volatility: {Volatility}",
                order.Id, marketLiquidity, volatility);

            var slippageFactors = new Dictionary<string, decimal>();
            var baseSlippage = 0.0005m; // 0.05% base slippage

            // Liquidity factor
            var liquidityFactor = marketLiquidity switch
            {
                >= 80 => 1.0m,
                >= 60 => 1.5m,
                >= 40 => 2.0m,
                >= 20 => 3.0m,
                _ => 5.0m
            };
            slippageFactors["liquidity"] = liquidityFactor;

            // Volatility factor
            var volatilityFactor = volatility switch
            {
                <= 0.01m => 1.0m,
                <= 0.02m => 1.2m,
                <= 0.05m => 1.5m,
                <= 0.1m => 2.0m,
                _ => 3.0m
            };
            slippageFactors["volatility"] = volatilityFactor;

            // Order size factor (assume larger orders have more slippage)
            var orderSizeFactor = order.Quantity.Value switch
            {
                <= 0.1m => 1.0m,
                <= 1.0m => 1.1m,
                <= 10.0m => 1.3m,
                _ => 1.5m
            };
            slippageFactors["orderSize"] = orderSizeFactor;

            // Time of day factor (assume some times have higher slippage)
            var hour = DateTime.UtcNow.Hour;
            var timeOfDayFactor = hour switch
            {
                >= 8 and <= 16 => 1.0m, // Active trading hours
                >= 0 and <= 6 => 1.5m,  // Low activity
                _ => 1.2m
            };
            slippageFactors["timeOfDay"] = timeOfDayFactor;

            // Calculate final slippage
            var totalFactor = liquidityFactor * volatilityFactor * orderSizeFactor * timeOfDayFactor;
            var maxSlippage = Math.Min(baseSlippage * totalFactor, 0.05m); // Cap at 5%
            var expectedSlippage = maxSlippage * 0.6m; // Expected is typically 60% of max
            var worstCaseSlippage = maxSlippage * 1.5m; // Worst case is 150% of max

            // Calculate recommended limit price (entry price adjusted for expected slippage)
            var recommendedLimitPrice = order.Side == OrderSide.Buy
                ? Price.Create((order.Price?.Value ?? 0) * (1 + expectedSlippage))
                : Price.Create((order.Price?.Value ?? 0) * (1 - expectedSlippage));

            var result = new SlippageCalculation
            {
                MaxAcceptableSlippage = maxSlippage,
                ExpectedSlippage = expectedSlippage,
                WorstCaseSlippage = worstCaseSlippage,
                RecommendedLimitPrice = recommendedLimitPrice,
                PrimaryReason = GetPrimarySlippageReason(slippageFactors),
                SlippageFactors = slippageFactors
            };

            _logger.LogInformation("Slippage calculated for order {OrderId}: Max {Max:P2}, Expected {Expected:P2}",
                order.Id, maxSlippage, expectedSlippage);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating slippage for order {OrderId}", order.Id);
            return new SlippageCalculation
            {
                MaxAcceptableSlippage = 0.01m, // Default 1%
                ExpectedSlippage = 0.005m,
                WorstCaseSlippage = 0.015m,
                RecommendedLimitPrice = order.Price ?? Price.Zero()
            };
        }
    }

    /// <inheritdoc />
    public async Task<OrderTypeRecommendation> RecommendOrderTypeAsync(
        TradingSignalData signal, 
        MarketConditions marketConditions, 
        OrderUrgency urgency)
    {
        try
        {
            _logger.LogDebug("Recommending order type for signal {SignalId}, Urgency: {Urgency}",
                signal.Id, urgency);

            var alternatives = new List<OrderTypeAlternative>();
            OrderType recommendedType;
            Price? limitPrice = null;
            Price? stopPrice = null;
            var timeInForce = TimeInForce.GoodTillCanceled;
            var reasoning = "";
            var confidence = 75m;

            // Determine best order type based on conditions
            if (urgency == OrderUrgency.Critical || marketConditions.Volatility > 0.05m)
            {
                // High urgency or high volatility - use market order
                recommendedType = OrderType.Market;
                reasoning = "Market order recommended due to high urgency or volatility";
                confidence = 90m;

                alternatives.Add(new OrderTypeAlternative
                {
                    Type = OrderType.Limit,
                    Description = "Limit order with aggressive pricing",
                    Score = 60m,
                    Pros = new List<string> { "Price control", "Reduced slippage" },
                    Cons = new List<string> { "May not fill", "Timing risk" }
                });
            }
            else if (marketConditions.Liquidity < 50)
            {
                // Low liquidity - use limit order
                recommendedType = OrderType.Limit;
                limitPrice = signal.EntryPrice;
                reasoning = "Limit order recommended due to low liquidity";
                confidence = 85m;

                alternatives.Add(new OrderTypeAlternative
                {
                    Type = OrderType.Market,
                    Description = "Market order for immediate execution",
                    Score = 40m,
                    Pros = new List<string> { "Guaranteed execution" },
                    Cons = new List<string> { "High slippage risk", "Poor execution price" }
                });
            }
            else
            {
                // Normal conditions - use limit order
                recommendedType = OrderType.Limit;
                limitPrice = signal.EntryPrice;
                reasoning = "Limit order recommended for normal market conditions";
                confidence = 80m;

                alternatives.Add(new OrderTypeAlternative
                {
                    Type = OrderType.Market,
                    Description = "Market order for speed",
                    Score = 70m,
                    Pros = new List<string> { "Fast execution", "High fill rate" },
                    Cons = new List<string> { "Slippage risk" }
                });
            }

            var recommendation = new OrderTypeRecommendation
            {
                RecommendedType = recommendedType,
                LimitPrice = limitPrice,
                StopPrice = stopPrice,
                TimeInForce = timeInForce,
                Reasoning = reasoning,
                ConfidenceLevel = confidence,
                Alternatives = alternatives
            };

            _logger.LogInformation("Order type recommendation: {Type} with confidence {Confidence:P0}",
                recommendedType, confidence / 100);

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recommending order type");
            return new OrderTypeRecommendation
            {
                RecommendedType = OrderType.Limit,
                LimitPrice = signal.EntryPrice,
                TimeInForce = TimeInForce.GoodTillCanceled,
                Reasoning = "Default recommendation due to error",
                ConfidenceLevel = 50m
            };
        }
    }

    /// <inheritdoc />
    public async Task<ExecutionQualityResult> AnalyzeExecutionQualityAsync(
        Order order, 
        OrderExecutionResult executionResult, 
        Price expectedPrice)
    {
        try
        {
            _logger.LogDebug("Analyzing execution quality for order {OrderId}", order.Id);

            var actualSlippage = Math.Abs(executionResult.AveragePrice.Value - expectedPrice.Value) / expectedPrice.Value;
            var slippageCost = actualSlippage * order.Quantity.Value * expectedPrice.Value;
            
            var quality = actualSlippage switch
            {
                <= 0.001m => ExecutionQuality.Excellent, // ≤0.1%
                <= 0.005m => ExecutionQuality.Good,      // ≤0.5%
                <= 0.01m => ExecutionQuality.Average,    // ≤1%
                <= 0.02m => ExecutionQuality.Poor,       // ≤2%
                _ => ExecutionQuality.Terrible           // >2%
            };

            var qualityIssues = new List<string>();
            if (actualSlippage > 0.01m)
                qualityIssues.Add($"High slippage: {actualSlippage:P2}");

            if (executionResult.ExecutionTime > TimeSpan.FromMinutes(5))
                qualityIssues.Add($"Slow execution: {executionResult.ExecutionTime.TotalMinutes:F1} minutes");

            var result = new ExecutionQualityResult
            {
                IsGoodExecution = quality <= ExecutionQuality.Good,
                ActualSlippage = actualSlippage,
                SlippageCost = (decimal)slippageCost,
                ExecutionPrice = executionResult.AveragePrice,
                ExecutionTime = executionResult.ExecutionTime,
                Quality = quality,
                QualityIssues = qualityIssues
            };

            _logger.LogInformation("Execution quality analyzed for order {OrderId}: {Quality} (slippage: {Slippage:P2})",
                order.Id, quality, actualSlippage);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing execution quality for order {OrderId}", order.Id);
            return new ExecutionQualityResult
            {
                IsGoodExecution = false,
                Quality = ExecutionQuality.Terrible,
                QualityIssues = new List<string> { $"Analysis error: {ex.Message}" }
            };
        }
    }

    /// <inheritdoc />
    public async Task<OrderCancellationResult> ShouldCancelOrderAsync(
        Order pendingOrder, 
        MarketConditions currentConditions, 
        MarketConditions originalConditions)
    {
        try
        {
            _logger.LogDebug("Evaluating order cancellation for {OrderId}", pendingOrder.Id);

            var considerations = new List<string>();
            var shouldCancel = false;
            var reason = CancellationReason.MarketConditionsChanged;
            var urgency = 50m;

            // Check volatility change
            var volatilityChange = Math.Abs(currentConditions.Volatility - originalConditions.Volatility);
            if (volatilityChange > 0.02m) // 2% volatility increase
            {
                considerations.Add($"Volatility increased by {volatilityChange:P1}");
                urgency += 20m;
                
                if (volatilityChange > 0.05m) // 5% is critical
                {
                    shouldCancel = true;
                    reason = CancellationReason.VolatilityTooHigh;
                }
            }

            // Check liquidity change
            var liquidityChange = originalConditions.Liquidity - currentConditions.Liquidity;
            if (liquidityChange > 20) // 20 point drop
            {
                considerations.Add($"Liquidity decreased by {liquidityChange} points");
                urgency += 15m;
                
                if (currentConditions.Liquidity < 30)
                {
                    shouldCancel = true;
                    reason = CancellationReason.LiquidityTooLow;
                }
            }

            // Check spread widening
            var spreadChange = currentConditions.BidAskSpread - originalConditions.BidAskSpread;
            if (spreadChange > 0.005m) // 0.5% spread increase
            {
                considerations.Add($"Bid-ask spread widened by {spreadChange:P1}");
                urgency += 10m;
            }

            // Check order age
            var orderAge = DateTime.UtcNow - pendingOrder.CreatedAt;
            if (orderAge > TimeSpan.FromMinutes(30))
            {
                considerations.Add($"Order is {orderAge.TotalMinutes:F0} minutes old");
                urgency += 5m;
                
                if (orderAge > TimeSpan.FromHours(2))
                {
                    shouldCancel = true;
                    reason = CancellationReason.TimeoutReached;
                }
            }

            var description = shouldCancel 
                ? $"Order should be cancelled due to {reason}"
                : "Order can remain active";

            var result = shouldCancel
                ? OrderCancellationResult.CancelOrder(reason, description, urgency)
                : OrderCancellationResult.KeepOrder(description);

            result = result with { Considerations = considerations };

            _logger.LogInformation("Order cancellation evaluation for {OrderId}: {ShouldCancel} (urgency: {Urgency})",
                pendingOrder.Id, shouldCancel, urgency);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating order cancellation for {OrderId}", pendingOrder.Id);
            return OrderCancellationResult.KeepOrder($"Evaluation error: {ex.Message}");
        }
    }

    private static SlippageReason GetPrimarySlippageReason(Dictionary<string, decimal> factors)
    {
        var maxFactor = factors.MaxBy(kvp => kvp.Value);
        
        return maxFactor.Key switch
        {
            "liquidity" => SlippageReason.LowLiquidity,
            "volatility" => SlippageReason.HighVolatility,
            "orderSize" => SlippageReason.LargeOrderSize,
            "timeOfDay" => SlippageReason.TimeOfDay,
            _ => SlippageReason.MarketConditions
        };
    }
} 