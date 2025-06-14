using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Risk.Domain.Interfaces;
using BitcoinTradingBot.Modules.Risk.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Risk.Infrastructure.Calculations;

/// <summary>
/// Advanced position sizing calculator with Kelly Criterion and volatility-based adjustments
/// </summary>
public class PositionSizingCalculator : IPositionSizingCalculator
{
    private readonly ILogger<PositionSizingCalculator> _logger;

    // Risk parameters
    private const decimal MaxPositionSizePercentage = 0.25m; // 25% max position size
    private const decimal DefaultRiskPercentage = 0.02m; // 2% default risk per trade
    private const decimal MaxRiskPercentage = 0.05m; // 5% maximum risk per trade
    private const decimal MinPositionSize = 10m; // Minimum position size in USDT
    private const decimal MaxKellyFraction = 0.25m; // Maximum Kelly fraction to prevent over-leveraging

    public PositionSizingCalculator(ILogger<PositionSizingCalculator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public PositionSizeResult CalculatePositionSize(PositionSizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        try
        {
            _logger.LogDebug("Calculating position size for {Symbol} with account balance {Balance} and risk {RiskPercentage}%",
                request.Symbol, request.AccountBalance, request.RiskPercentage * 100);

            // Calculate base position size using fixed percentage risk
            var fixedRiskSize = CalculateFixedRiskSize(request);

            // Calculate Kelly Criterion optimal size if historical data is available
            var kellySize = request.HistoricalPerformance != null 
                ? CalculateKellyOptimalSize(request, fixedRiskSize)
                : fixedRiskSize;

            // Apply volatility adjustment
            var volatilityAdjustedSize = ApplyVolatilityAdjustment(kellySize, request);

            // Apply account exposure limits
            var exposureLimitedSize = ApplyExposureLimits(volatilityAdjustedSize, request);

            // Apply minimum size requirements
            var finalSize = ApplyMinimumSize(exposureLimitedSize, request);

            // Calculate actual risk amount
            var actualRiskAmount = CalculateActualRisk(finalSize, request);
            var riskPercentage = actualRiskAmount / request.AccountBalance;
            var kellyOptimal = request.WinRate.HasValue && request.AverageWinLossRatio.HasValue 
                ? CalculateKellyOptimal(request.WinRate.Value, request.AverageWinLossRatio.Value)
                : 0m;

            // Create result using the Success factory method
            var result = PositionSizeResult.Success(
                positionSize: Quantity.Create(finalSize),
                riskAmount: Price.Create(actualRiskAmount),
                riskPercentage: riskPercentage,
                kellyOptimalSize: kellyOptimal,
                volatilityAdjustment: request.VolatilityMultiplier ?? 1.0m,
                drawdownAdjustment: request.CurrentDrawdown.HasValue ? (1 - request.CurrentDrawdown.Value) : 1.0m
            );

            _logger.LogInformation("Position size calculated for {Symbol}: {Size} USDT (Risk: {RiskAmount} USDT, {RiskPercent:F2}%)",
                request.Symbol, finalSize, actualRiskAmount, riskPercentage * 100);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating position size for {Symbol}", request.Symbol);
            throw;
        }
    }

    /// <inheritdoc />
    public decimal CalculateOptimalKellyFraction(IReadOnlyList<TradeResult> tradeHistory)
    {
        ArgumentNullException.ThrowIfNull(tradeHistory);

        if (tradeHistory.Count < 10)
        {
            _logger.LogWarning("Insufficient trade history for Kelly calculation. Need at least 10 trades, got {Count}", tradeHistory.Count);
            return 0;
        }

        try
        {
            var wins = tradeHistory.Where(t => t.ProfitLoss > 0).ToList();
            var losses = tradeHistory.Where(t => t.ProfitLoss < 0).ToList();

            if (losses.Count == 0)
            {
                // All wins - Kelly would suggest very high fraction, but we cap it
                _logger.LogWarning("No losses in trade history - capping Kelly fraction at maximum");
                return MaxKellyFraction;
            }

            var winRate = (decimal)wins.Count / tradeHistory.Count();
            var avgWin = wins.Any() ? wins.Average(w => Math.Abs(w.ProfitLoss)) : 0;
            var avgLoss = losses.Average(l => Math.Abs(l.ProfitLoss));

            // Kelly formula: f = (bp - q) / b
            // where: b = ratio of win to loss, p = probability of win, q = probability of loss
            var winLossRatio = avgWin / avgLoss;
            var kellyFraction = (winLossRatio * winRate - (1 - winRate)) / winLossRatio;

            // Apply safety limits
            kellyFraction = Math.Max(0, Math.Min(kellyFraction, MaxKellyFraction));

            _logger.LogDebug("Kelly Criterion calculated: {Kelly:F4} (Win Rate: {WinRate:F2}%, Avg Win: {AvgWin}, Avg Loss: {AvgLoss})",
                kellyFraction, winRate * 100, avgWin, avgLoss);

            return kellyFraction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating Kelly fraction");
            return 0;
        }
    }

    /// <inheritdoc />
    public bool ValidatePositionSize(decimal positionSize, PositionSizeRequest request)
    {
        if (positionSize <= 0 || request == null)
            return false;

        try
        {
            // Check maximum position size
            var maxAllowed = request.AccountBalance * MaxPositionSizePercentage;
            if (positionSize > maxAllowed)
            {
                _logger.LogWarning("Position size {Size} exceeds maximum allowed {Max} for account {Balance}",
                    positionSize, maxAllowed, request.AccountBalance);
                return false;
            }

            // Check minimum position size
            if (positionSize < MinPositionSize)
            {
                _logger.LogWarning("Position size {Size} below minimum required {Min}",
                    positionSize, MinPositionSize);
                return false;
            }

            // Check risk percentage
            var actualRisk = CalculateActualRisk(positionSize, request);
            var riskPercentage = actualRisk / request.AccountBalance;
            
            if (riskPercentage > MaxRiskPercentage)
            {
                _logger.LogWarning("Position size {Size} results in risk {Risk:F2}% which exceeds maximum {Max:F2}%",
                    positionSize, riskPercentage * 100, MaxRiskPercentage * 100);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating position size {Size}", positionSize);
            return false;
        }
    }

    /// <summary>
    /// Calculates position size using fixed percentage risk method
    /// </summary>
    private decimal CalculateFixedRiskSize(PositionSizeRequest request)
    {
        var riskAmount = request.AccountBalance * request.RiskPercentage;
        var stopLossDistance = Math.Abs(request.EntryPrice - request.StopLoss);
        
        if (stopLossDistance <= 0)
        {
            _logger.LogWarning("Invalid stop loss distance for {Symbol}: Entry={Entry}, StopLoss={StopLoss}",
                request.Symbol, request.EntryPrice, request.StopLoss);
            return MinPositionSize;
        }

        var positionSize = riskAmount / stopLossDistance;
        
        _logger.LogTrace("Fixed risk calculation: Risk Amount={RiskAmount}, Stop Distance={StopDistance}, Position Size={Size}",
            riskAmount, stopLossDistance, positionSize);

        return Math.Max(positionSize, MinPositionSize);
    }

    /// <summary>
    /// Calculates Kelly Criterion optimal position size
    /// </summary>
    private decimal CalculateKellyOptimalSize(PositionSizeRequest request, decimal baseSize)
    {
        if (!request.WinRate.HasValue || !request.AverageWinLossRatio.HasValue)
            return baseSize;

        var kellyFraction = CalculateKellyOptimal(request.WinRate.Value, request.AverageWinLossRatio.Value);
        var kellySize = request.AccountBalance * kellyFraction;

        // Use the more conservative of Kelly size and base size
        var recommendedSize = Math.Min(kellySize, baseSize * 1.5m); // Cap at 150% of base size

        _logger.LogTrace("Kelly Criterion: Fraction={Fraction:F4}, Size={Size}, Base={Base}",
            kellyFraction, recommendedSize, baseSize);

        return recommendedSize;
    }

    /// <summary>
    /// Applies volatility-based adjustment to position size
    /// </summary>
    private decimal ApplyVolatilityAdjustment(decimal baseSize, PositionSizeRequest request)
    {
        if (!request.VolatilityMultiplier.HasValue)
            return baseSize;

        var volatilityMultiplier = request.VolatilityMultiplier.Value;
        
        // Reduce position size during high volatility periods
        decimal adjustment;
        if (volatilityMultiplier > 1.5m) // High volatility
        {
            adjustment = 0.7m; // Reduce by 30%
        }
        else if (volatilityMultiplier > 1.2m) // Medium volatility  
        {
            adjustment = 0.85m; // Reduce by 15%
        }
        else if (volatilityMultiplier < 0.8m) // Low volatility
        {
            adjustment = 1.1m; // Increase by 10%
        }
        else
        {
            adjustment = 1.0m; // Normal volatility
        }

        var adjustedSize = baseSize * adjustment;

        _logger.LogTrace("Volatility adjustment: Multiplier={Multiplier:F2}, Adjustment={Adjustment:F2}, Size={Size}",
            volatilityMultiplier, adjustment, adjustedSize);

        return adjustedSize;
    }

    /// <summary>
    /// Applies account exposure and concentration limits
    /// </summary>
    private decimal ApplyExposureLimits(decimal baseSize, PositionSizeRequest request)
    {
        var maxPositionSize = request.AccountBalance * MaxPositionSizePercentage;
        var limitedSize = Math.Min(baseSize, maxPositionSize);

        // Apply existing exposure limits if provided
        if (request.CurrentExposure.HasValue)
        {
            var remainingCapacity = request.AccountBalance * 0.5m - request.CurrentExposure.Value; // Max 50% total exposure
            limitedSize = Math.Min(limitedSize, Math.Max(remainingCapacity, MinPositionSize));
        }

        _logger.LogTrace("Exposure limits applied: Max Position={Max}, Limited Size={Limited}",
            maxPositionSize, limitedSize);

        return limitedSize;
    }

    /// <summary>
    /// Ensures minimum position size requirements are met
    /// </summary>
    private decimal ApplyMinimumSize(decimal calculatedSize, PositionSizeRequest request)
    {
        // Consider exchange minimum order sizes
        var exchangeMinimum = request.ExchangeMinimumOrderSize ?? MinPositionSize;
        var finalSize = Math.Max(calculatedSize, exchangeMinimum);

        // Ensure we don't exceed account balance
        finalSize = Math.Min(finalSize, request.AccountBalance * 0.95m); // Leave 5% buffer

        return finalSize;
    }

    /// <summary>
    /// Calculates actual risk amount for the position
    /// </summary>
    private decimal CalculateActualRisk(decimal positionSize, PositionSizeRequest request)
    {
        var stopLossDistance = Math.Abs(request.EntryPrice - request.StopLoss);
        return positionSize * stopLossDistance;
    }

    /// <summary>
    /// Calculates risk-reward ratio for the trade
    /// </summary>
    private decimal CalculateRiskRewardRatio(PositionSizeRequest request)
    {
        if (request.TakeProfit == null)
            return 0;

        var riskDistance = Math.Abs(request.EntryPrice - request.StopLoss);
        var rewardDistance = Math.Abs(request.TakeProfit.Value - request.EntryPrice);

        return riskDistance == 0 ? 0 : rewardDistance / riskDistance;
    }

    /// <summary>
    /// Calculates confidence level for the position size recommendation
    /// </summary>
    private decimal CalculateConfidenceLevel(PositionSizeRequest request)
    {
        decimal confidence = 0.5m; // Base confidence

        // Increase confidence based on available data quality
        if (request.WinRate.HasValue)
        {
            var winRate = request.WinRate.Value;
            if (winRate > 0.6m) confidence += 0.2m;
            else if (winRate > 0.5m) confidence += 0.1m;
        }

        if (request.AverageWinLossRatio.HasValue && request.AverageWinLossRatio.Value > 1.5m)
        {
            confidence += 0.15m;
        }

        if (request.RiskRewardRatio.HasValue && request.RiskRewardRatio.Value > 2.0m)
        {
            confidence += 0.15m;
        }

        return Math.Min(confidence, 1.0m);
    }

    /// <summary>
    /// Generates warnings for the position size calculation
    /// </summary>
    private List<string> GenerateWarnings(PositionSizeRequest request, decimal calculatedSize)
    {
        var warnings = new List<string>();

        // Check if position size is at minimum
        if (calculatedSize <= MinPositionSize * 1.1m)
        {
            warnings.Add("Position size is at or near minimum threshold");
        }

        // Check if position size is very large
        var maxRecommended = request.AccountBalance * 0.1m; // 10% of account
        if (calculatedSize > maxRecommended)
        {
            warnings.Add($"Position size exceeds 10% of account balance");
        }

        // Check stop loss distance
        var stopLossDistance = Math.Abs(request.EntryPrice - request.StopLoss);
        var distancePercentage = stopLossDistance / request.EntryPrice;
        if (distancePercentage > 0.05m) // 5%
        {
            warnings.Add("Stop loss distance is greater than 5% - consider tighter stop");
        }

        // Check risk percentage
        if (request.RiskPercentage > 0.03m) // 3%
        {
            warnings.Add("Risk percentage is above 3% - consider reducing position size");
        }

        return warnings;
    }

    /// <summary>
    /// Validates the position size request
    /// </summary>
    private void ValidateRequest(PositionSizeRequest request)
    {
        if (request.AccountBalance <= 0)
            throw new ArgumentException("Account balance must be positive", nameof(request));

        if (request.EntryPrice <= 0)
            throw new ArgumentException("Entry price must be positive", nameof(request));

        if (request.StopLoss <= 0)
            throw new ArgumentException("Stop loss price must be positive", nameof(request));

        if (request.RiskPercentage <= 0 || request.RiskPercentage > MaxRiskPercentage)
            throw new ArgumentException($"Risk percentage must be between 0 and {MaxRiskPercentage:P2}", nameof(request));

        if (request.EntryPrice == request.StopLoss)
            throw new ArgumentException("Entry price cannot equal stop loss price", nameof(request));
    }

    /// <inheritdoc />
    public async Task<PositionSizeResult> CalculatePositionSizeAsync(
        Price accountEquity,
        decimal riskPercentage,
        Price entryPrice,
        Price stopLossPrice,
        decimal volatilityMultiplier = 1.0m)
    {
        return await Task.FromResult(CalculatePositionSize(new PositionSizeRequest(
            Symbol: new Symbol("BTC/USDT"), // Default symbol
            EntryPrice: entryPrice,
            StopLoss: stopLossPrice,
            TakeProfit: null,
            AccountBalance: accountEquity,
            RiskPercentage: riskPercentage,
            WinRate: null,
            AverageWinLossRatio: null,
            CurrentDrawdown: null,
            VolatilityMultiplier: volatilityMultiplier,
            AdditionalData: null
        )));
    }

    /// <inheritdoc />
    public async Task<decimal> CalculateKellyOptimalSizeAsync(
        decimal winRate,
        decimal averageWin,
        decimal averageLoss,
        Price accountEquity)
    {
        return await Task.FromResult(CalculateKellyOptimal(winRate, averageWin / averageLoss));
    }

    /// <inheritdoc />
    public async Task<Quantity> AdjustForDrawdownAsync(
        Quantity basePositionSize,
        decimal currentDrawdown,
        decimal maxDrawdownThreshold)
    {
        return await Task.FromResult(AdjustForDrawdown(basePositionSize, currentDrawdown, maxDrawdownThreshold));
    }

    /// <summary>
    /// Helper method for Kelly Criterion calculation
    /// </summary>
    private decimal CalculateKellyOptimal(decimal winRate, decimal averageWinLossRatio)
    {
        // Kelly formula: f = (bp - q) / b
        // where: b = win/loss ratio, p = win rate, q = loss rate (1-p)
        var lossRate = 1 - winRate;
        var kellyFraction = (averageWinLossRatio * winRate - lossRate) / averageWinLossRatio;
        
        // Apply safety limits
        return Math.Max(0, Math.Min(kellyFraction, MaxKellyFraction));
    }

    /// <summary>
    /// Helper method for drawdown adjustment
    /// </summary>
    private Quantity AdjustForDrawdown(Quantity basePositionSize, decimal currentDrawdown, decimal maxDrawdownThreshold)
    {
        if (currentDrawdown <= 0 || maxDrawdownThreshold <= 0)
            return basePositionSize;

        var drawdownRatio = Math.Min(currentDrawdown / maxDrawdownThreshold, 1.0m);
        var reductionFactor = 1.0m - (drawdownRatio * 0.5m); // Reduce by up to 50% at max drawdown
        
        return Quantity.Create(basePositionSize * Math.Max(reductionFactor, 0.1m)); // Minimum 10% of base size
    }
} 