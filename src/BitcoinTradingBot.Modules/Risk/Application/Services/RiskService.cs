using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Risk.Domain.Interfaces;
using BitcoinTradingBot.Modules.Risk.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace BitcoinTradingBot.Modules.Risk.Application.Services;

/// <summary>
/// Main risk management application service that orchestrates all risk management activities
/// </summary>
public class RiskService : IRiskManager
{
    private readonly IPositionSizingCalculator _positionSizingCalculator;
    private readonly IOrderExecutionValidator _orderExecutionValidator;
    private readonly ILogger<RiskService> _logger;
    private readonly RiskParameters _riskParameters;

    public RiskService(
        IPositionSizingCalculator positionSizingCalculator,
        IOrderExecutionValidator orderExecutionValidator,
        ILogger<RiskService> logger,
        IConfiguration configuration)
    {
        _positionSizingCalculator = positionSizingCalculator;
        _orderExecutionValidator = orderExecutionValidator;
        _logger = logger;
        _riskParameters = LoadRiskParameters(configuration);
    }

    /// <inheritdoc />
    public async Task<RiskValidationResult> ValidateTradeAsync(TradingSignalData signal, AccountState currentAccount)
    {
        try
        {
            _logger.LogDebug("Validating trade for signal {SignalId} on {Symbol}", signal.Id, signal.Symbol.Value);

            var riskChecks = new Dictionary<string, RiskCheckResult>();
            var failures = new List<string>();
            var actions = new List<string>();
            var totalRiskScore = 0m;

            // Check if risk management is enabled
            if (!_riskParameters.RiskManagementEnabled)
            {
                _logger.LogWarning("Risk management is disabled - allowing trade");
                return RiskValidationResult.Success(0, RiskLevel.Low, 100m);
            }

            // 1. Account equity check
            var equityCheck = ValidateAccountEquity(currentAccount);
            riskChecks["AccountEquity"] = equityCheck;
            totalRiskScore += equityCheck.Score;
            if (!equityCheck.Passed) failures.Add("Insufficient account equity");

            // 2. Position limits check  
            var positionLimitCheck = ValidatePositionLimits(currentAccount);
            riskChecks["PositionLimits"] = positionLimitCheck;
            totalRiskScore += positionLimitCheck.Score;
            if (!positionLimitCheck.Passed) failures.Add("Position limits exceeded");

            // 3. Drawdown check
            var drawdownCheck = ValidateDrawdown(currentAccount);
            riskChecks["Drawdown"] = drawdownCheck;
            totalRiskScore += drawdownCheck.Score;
            if (!drawdownCheck.Passed) failures.Add("Drawdown limits exceeded");

            // 4. Daily loss check
            var dailyLossCheck = ValidateDailyLoss(currentAccount);
            riskChecks["DailyLoss"] = dailyLossCheck;
            totalRiskScore += dailyLossCheck.Score;
            if (!dailyLossCheck.Passed) failures.Add("Daily loss limits exceeded");

            // 5. Trade frequency check
            var frequencyCheck = ValidateTradeFrequency(currentAccount);
            riskChecks["TradeFrequency"] = frequencyCheck;
            totalRiskScore += frequencyCheck.Score;
            if (!frequencyCheck.Passed) failures.Add("Trading too frequently");

            // 6. Signal quality check
            var signalQualityCheck = ValidateSignalQuality(signal);
            riskChecks["SignalQuality"] = signalQualityCheck;
            totalRiskScore += signalQualityCheck.Score;
            if (!signalQualityCheck.Passed) failures.Add("Signal quality insufficient");

            // Calculate risk level
            var riskLevel = totalRiskScore switch
            {
                < 20 => RiskLevel.VeryLow,
                < 40 => RiskLevel.Low,
                < 60 => RiskLevel.Medium,
                < 80 => RiskLevel.High,
                _ => RiskLevel.VeryHigh
            };

            // Calculate max recommended position size
            var maxPositionSize = CalculateMaxRecommendedPositionSize(currentAccount, signal, totalRiskScore);

            // Generate recommended actions
            if (riskLevel >= RiskLevel.High)
                actions.Add("Consider reducing position size");
            if (currentAccount.CurrentDrawdown > _riskParameters.MaxIntradayDrawdown * 0.8m)
                actions.Add("Monitor drawdown closely");
            if (totalRiskScore > 70)
                actions.Add("Consider waiting for better market conditions");

            var result = failures.Any()
                ? RiskValidationResult.Failure(failures, riskLevel, totalRiskScore)
                : RiskValidationResult.Success(totalRiskScore, riskLevel, maxPositionSize);

            result = result with
            {
                RiskChecks = riskChecks,
                RecommendedActions = actions
            };

            _logger.LogInformation("Trade validation completed for signal {SignalId}: {IsValid} (risk level: {RiskLevel}, score: {Score})",
                signal.Id, result.IsValid, riskLevel, totalRiskScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating trade for signal {SignalId}", signal.Id);
            return RiskValidationResult.Failure(
                new List<string> { $"Validation error: {ex.Message}" },
                RiskLevel.VeryHigh);
        }
    }

    /// <inheritdoc />
    public async Task<PositionSizeResult> CalculatePositionSizeAsync(
        TradingSignalData signal, 
        Price accountEquity, 
        RiskParameters riskParameters)
    {
        try
        {
            _logger.LogDebug("Calculating position size for signal {SignalId}", signal.Id);

            // Calculate volatility multiplier based on market conditions
            var volatilityMultiplier = CalculateVolatilityMultiplier(signal);

            // Use the position sizing calculator
            var result = await _positionSizingCalculator.CalculatePositionSizeAsync(
                accountEquity,
                riskParameters.MaxRiskPerTrade,
                signal.EntryPrice,
                signal.StopLoss ?? Price.Create(signal.EntryPrice.Value * 0.98m), // Default 2% stop
                volatilityMultiplier);

            // Apply Kelly Criterion if enabled
            if (riskParameters.KellyCriterionEnabled && result.IsValidSize)
            {
                // For demonstration, using placeholder values - in real implementation,
                // these would come from historical performance tracking
                var kellyOptimal = await _positionSizingCalculator.CalculateKellyOptimalSizeAsync(
                    0.65m, // 65% win rate
                    1000m,  // Average win
                    500m,   // Average loss  
                    accountEquity);

                // Apply Kelly multiplier
                var adjustedKellySize = kellyOptimal * riskParameters.KellyMultiplier;
                
                // Use the more conservative of fixed risk or Kelly sizing
                if (adjustedKellySize < result.RiskPercentage)
                {
                    var kellyResult = await _positionSizingCalculator.CalculatePositionSizeAsync(
                        accountEquity,
                        adjustedKellySize,
                        signal.EntryPrice,
                        signal.StopLoss ?? Price.Create(signal.EntryPrice.Value * 0.98m),
                        volatilityMultiplier);

                    _logger.LogInformation("Using Kelly-adjusted position size: {Kelly:P2} vs fixed risk: {Fixed:P2}",
                        adjustedKellySize, result.RiskPercentage);

                    return kellyResult;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating position size for signal {SignalId}", signal.Id);
            return PositionSizeResult.Failure($"Position size calculation error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<CircuitBreakerResult> CheckCircuitBreakersAsync(
        AccountState currentAccount, 
        MarketConditions marketConditions)
    {
        try
        {
            _logger.LogDebug("Checking circuit breakers");

            if (!_riskParameters.CircuitBreakersEnabled)
            {
                return CircuitBreakerResult.Normal();
            }

            var triggers = new List<CircuitBreakerTrigger>();

            // Check drawdown circuit breaker
            if (currentAccount.CurrentDrawdown > _riskParameters.MaxIntradayDrawdown)
            {
                triggers.Add(new CircuitBreakerTrigger
                {
                    Name = "MaxDrawdown",
                    Severity = CircuitBreakerSeverity.Critical,
                    Description = $"Current drawdown {currentAccount.CurrentDrawdown:P2} exceeds limit {_riskParameters.MaxIntradayDrawdown:P2}",
                    TriggerValue = currentAccount.CurrentDrawdown,
                    ThresholdValue = _riskParameters.MaxIntradayDrawdown
                });
            }

            // Check daily loss circuit breaker
            var dailyLossPercent = Math.Abs(currentAccount.DailyRealizedPnL.Value) / currentAccount.TotalEquity.Value;
            if (dailyLossPercent > _riskParameters.MaxDailyLoss)
            {
                triggers.Add(new CircuitBreakerTrigger
                {
                    Name = "MaxDailyLoss",
                    Severity = CircuitBreakerSeverity.High,
                    Description = $"Daily loss {dailyLossPercent:P2} exceeds limit {_riskParameters.MaxDailyLoss:P2}",
                    TriggerValue = dailyLossPercent,
                    ThresholdValue = _riskParameters.MaxDailyLoss
                });
            }

            // Check market volatility circuit breaker
            if (marketConditions.VolatilityRegime == Strategy.Domain.Indicators.VolatilityRegime.Extreme)
            {
                triggers.Add(new CircuitBreakerTrigger
                {
                    Name = "ExtremeVolatility",
                    Severity = CircuitBreakerSeverity.Medium,
                    Description = "Extreme market volatility detected",
                    TriggerValue = marketConditions.Volatility,
                    ThresholdValue = 0.1m // 10% volatility threshold
                });
            }

            // Check liquidity circuit breaker
            if (marketConditions.Liquidity < 20)
            {
                triggers.Add(new CircuitBreakerTrigger
                {
                    Name = "LowLiquidity",
                    Severity = CircuitBreakerSeverity.Medium,
                    Description = $"Market liquidity {marketConditions.Liquidity} below threshold",
                    TriggerValue = marketConditions.Liquidity,
                    ThresholdValue = 20m
                });
            }

            if (triggers.Any())
            {
                _logger.LogWarning("Circuit breakers triggered: {Count} breakers", triggers.Count);
                return CircuitBreakerResult.Triggered(triggers, _riskParameters.CircuitBreakerCooldown);
            }

            return CircuitBreakerResult.Normal();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking circuit breakers");
            return CircuitBreakerResult.Normal(); // Fail safe
        }
    }

    /// <inheritdoc />
    public async Task<StopLossLevels> CalculateStopLossLevelsAsync(
        Price entryPrice, 
        TradingSignalData signal, 
        RiskParameters riskParameters)
    {
        try
        {
            _logger.LogDebug("Calculating stop loss levels for entry price {EntryPrice}", entryPrice.Value);

            // Initial stop loss (from signal or percentage-based)
            var initialStopLoss = signal.StopLoss ?? 
                Price.Create(entryPrice.Value * (1 - riskParameters.InitialStopLossPercentage));

            // Breakeven level (1% profit)
            var breakevenLevel = Price.Create(entryPrice.Value * 1.01m);

            // Emergency stop (double the normal stop distance)
            var stopDistance = Math.Abs(entryPrice.Value - initialStopLoss.Value);
            var emergencyStop = Price.Create(entryPrice.Value - (stopDistance * 2));

            // Profit targets at 1R, 2R, 3R
            var riskAmount = stopDistance;
            var profitTargets = new List<ProfitTarget>
            {
                new() { TargetPrice = Price.Create(entryPrice.Value + riskAmount), PercentageToClose = 25m, RiskRewardRatio = 1m, Description = "1R - Partial profit" },
                new() { TargetPrice = Price.Create(entryPrice.Value + riskAmount * 2), PercentageToClose = 50m, RiskRewardRatio = 2m, Description = "2R - Main target" },
                new() { TargetPrice = Price.Create(entryPrice.Value + riskAmount * 3), PercentageToClose = 25m, RiskRewardRatio = 3m, Description = "3R - Extended target" }
            };

            var calculationDetails = new StopLossCalculationDetails
            {
                Method = signal.StopLoss != null ? "Signal-Based" : "Percentage-Based",
                CalculationInput = signal.StopLoss != null ? 0 : riskParameters.InitialStopLossPercentage,
                Reasoning = "Initial stop loss based on risk parameters and signal data",
                ConfidenceLevel = 85m
            };

            var stopLossLevels = new StopLossLevels
            {
                InitialStopLoss = initialStopLoss,
                BreakevenLevel = breakevenLevel,
                EmergencyStop = emergencyStop,
                ProfitTargets = profitTargets,
                CalculationDetails = calculationDetails,
                IsTrailingActive = riskParameters.TrailingStopsEnabled,
                TrailingDistance = riskParameters.TrailingStopDistance,
                TrailingActivationThreshold = riskParameters.TrailingStopActivation,
                RiskPerUnit = Price.Create(riskAmount),
                MaxAcceptableRisk = Price.Create(riskAmount * 1.5m) // 50% buffer
            };

            _logger.LogInformation("Stop loss levels calculated - Initial: {Initial}, Breakeven: {Breakeven}",
                initialStopLoss.Value, breakevenLevel.Value);

            return stopLossLevels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating stop loss levels");
            
            // Return safe defaults
            var defaultStop = Price.Create(entryPrice.Value * 0.98m);
            return new StopLossLevels
            {
                InitialStopLoss = defaultStop,
                BreakevenLevel = Price.Create(entryPrice.Value * 1.01m),
                RiskPerUnit = Price.Create(entryPrice.Value * 0.02m),
                CalculationDetails = new StopLossCalculationDetails
                {
                    Method = "Default",
                    Reasoning = "Error in calculation, using safe defaults"
                }
            };
        }
    }

    /// <inheritdoc />
    public async Task<Position> UpdateTrailingStopsAsync(
        Position position, 
        Price currentPrice, 
        MarketData marketData)
    {
        try
        {
            if (!_riskParameters.TrailingStopsEnabled || position.StopLossLevels == null)
                return position;

            _logger.LogDebug("Updating trailing stops for position {PositionId}", position.PositionId);

            var isLong = position.Side == OrderSide.Buy;
            var updatedStopLevels = position.StopLossLevels.UpdateTrailingStop(
                currentPrice, 
                position.EntryPrice, 
                isLong);

            if (updatedStopLevels.TrailingStop != position.StopLossLevels.TrailingStop)
            {
                _logger.LogInformation("Trailing stop updated for position {PositionId}: {OldStop} -> {NewStop}",
                    position.PositionId, 
                    position.StopLossLevels.TrailingStop?.Value ?? 0,
                    updatedStopLevels.TrailingStop?.Value ?? 0);

                return position with { StopLossLevels = updatedStopLevels };
            }

            return position;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating trailing stops for position {PositionId}", position.PositionId);
            return position; // Return unchanged position on error
        }
    }

    /// <inheritdoc />
    public async Task<PositionClosureResult> ShouldClosePositionAsync(
        Position position, 
        AccountState currentAccount, 
        MarketConditions marketConditions)
    {
        try
        {
            _logger.LogDebug("Evaluating position closure for position {PositionId}", position.PositionId);

            var riskFactors = new List<string>();

            // Check stop loss hit
            if (position.StopLossLevels != null)
            {
                var effectiveStop = position.StopLossLevels.GetEffectiveStopLoss(
                    marketConditions.CurrentPrice, 
                    position.Side == OrderSide.Buy);

                var isLong = position.Side == OrderSide.Buy;
                var stopHit = isLong 
                    ? marketConditions.CurrentPrice.Value <= effectiveStop.Value
                    : marketConditions.CurrentPrice.Value >= effectiveStop.Value;

                if (stopHit)
                {
                    return PositionClosureResult.Close(
                        ClosureReason.StopLossHit,
                        $"Stop loss hit at {effectiveStop.Value}",
                        urgency: ClosureUrgency.High);
                }
            }

            // Check drawdown protection
            if (currentAccount.CurrentDrawdown > _riskParameters.MaxIntradayDrawdown * 0.9m)
            {
                riskFactors.Add($"Account drawdown approaching limit: {currentAccount.CurrentDrawdown:P2}");
                
                if (currentAccount.CurrentDrawdown > _riskParameters.MaxIntradayDrawdown)
                {
                    return PositionClosureResult.Close(
                        ClosureReason.DrawdownProtection,
                        "Maximum drawdown exceeded",
                        urgency: ClosureUrgency.Emergency);
                }
            }

            // Check extreme market conditions
            if (marketConditions.VolatilityRegime == Strategy.Domain.Indicators.VolatilityRegime.Extreme)
            {
                riskFactors.Add("Extreme market volatility detected");
                
                return PositionClosureResult.Close(
                    ClosureReason.VolatilitySpike,
                    "Extreme volatility - protective closure",
                    50m, // Close 50% of position
                    ClosureUrgency.High);
            }

            // Check time-based closure (if position is very old)
            var positionAge = DateTime.UtcNow - position.OpenedAt;
            if (positionAge > TimeSpan.FromDays(7))
            {
                riskFactors.Add($"Position age: {positionAge.TotalDays:F1} days");
                
                return PositionClosureResult.Close(
                    ClosureReason.TimeStop,
                    "Position held too long",
                    urgency: ClosureUrgency.Normal);
            }

            // No closure needed
            return PositionClosureResult.KeepOpen("All risk checks passed") with
            {
                RiskFactors = riskFactors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating position closure for position {PositionId}", position.PositionId);
            return PositionClosureResult.KeepOpen($"Evaluation error: {ex.Message}");
        }
    }

    private RiskCheckResult ValidateAccountEquity(AccountState account)
    {
        var passed = account.TotalEquity.Value > 1000m; // Minimum $1000
        return new RiskCheckResult
        {
            Passed = passed,
            CheckName = "AccountEquity",
            Score = passed ? 0 : 30m,
            Message = passed ? "Sufficient equity" : "Insufficient account equity"
        };
    }

    private RiskCheckResult ValidatePositionLimits(AccountState account)
    {
        var passed = account.OpenPositionCount < _riskParameters.MaxOpenPositions &&
                    account.TotalExposurePercentage < _riskParameters.MaxPortfolioExposure;
        
        return new RiskCheckResult
        {
            Passed = passed,
            CheckName = "PositionLimits",
            Score = passed ? 0 : 25m,
            Message = passed ? "Within position limits" : "Position limits exceeded"
        };
    }

    private RiskCheckResult ValidateDrawdown(AccountState account)
    {
        var passed = account.CurrentDrawdown <= _riskParameters.MaxIntradayDrawdown;
        var score = passed ? 0 : Math.Min(50m, account.CurrentDrawdown * 1000); // Scale up for scoring
        
        return new RiskCheckResult
        {
            Passed = passed,
            CheckName = "Drawdown",
            Score = score,
            Message = passed ? "Drawdown within limits" : $"Drawdown {account.CurrentDrawdown:P2} exceeds limit"
        };
    }

    private RiskCheckResult ValidateDailyLoss(AccountState account)
    {
        var dailyLossPercent = Math.Abs(account.DailyRealizedPnL.Value) / account.TotalEquity.Value;
        var passed = dailyLossPercent <= _riskParameters.MaxDailyLoss;
        
        return new RiskCheckResult
        {
            Passed = passed,
            CheckName = "DailyLoss",
            Score = passed ? 0 : 20m,
            Message = passed ? "Daily loss within limits" : $"Daily loss {dailyLossPercent:P2} exceeds limit"
        };
    }

    private RiskCheckResult ValidateTradeFrequency(AccountState account)
    {
        var timeSinceLastTrade = account.LastTradeTime.HasValue 
            ? (DateTime.UtcNow - account.LastTradeTime.Value).TotalSeconds
            : double.MaxValue;
            
        var passed = timeSinceLastTrade >= _riskParameters.MinTimeBetweenTrades;
        
        return new RiskCheckResult
        {
            Passed = passed,
            CheckName = "TradeFrequency",
            Score = passed ? 0 : 15m,
            Message = passed ? "Trade frequency acceptable" : "Trading too frequently"
        };
    }

    private RiskCheckResult ValidateSignalQuality(TradingSignalData signal)
    {
        // Simple quality check based on confidence
        var confidence = signal.Metadata.TryGetValue("confidence", out var conf) 
            ? Convert.ToDecimal(conf) 
            : 50m;
            
        var passed = confidence >= 70m; // Require 70% confidence
        
        return new RiskCheckResult
        {
            Passed = passed,
            CheckName = "SignalQuality",
            Score = passed ? 0 : (100 - confidence) / 5, // Convert to risk score
            Message = passed ? $"Signal confidence: {confidence:F0}%" : $"Low signal confidence: {confidence:F0}%"
        };
    }

    private decimal CalculateMaxRecommendedPositionSize(AccountState account, TradingSignalData signal, decimal riskScore)
    {
        var baseSize = _riskParameters.MaxRiskPerTrade;
        var riskAdjustment = 1.0m - (riskScore / 200m); // Reduce size as risk increases
        var adjustedSize = baseSize * Math.Max(0.1m, riskAdjustment); // Minimum 10% of base
        
        return Math.Min(adjustedSize, 0.05m); // Cap at 5%
    }

    private decimal CalculateVolatilityMultiplier(TradingSignalData signal)
    {
        // Extract volatility info from signal metadata if available
        if (signal.Metadata.TryGetValue("volatilityRegime", out var volRegime))
        {
            return volRegime.ToString() switch
            {
                "Low" => 0.8m,
                "Normal" => 1.0m,
                "High" => 1.3m,
                "Extreme" => 1.8m,
                _ => 1.0m
            };
        }
        
        return 1.0m; // Default multiplier
    }

    private static RiskParameters LoadRiskParameters(IConfiguration configuration)
    {
        var riskSection = configuration.GetSection("Risk");
        
        return new RiskParameters
        {
            MaxRiskPerTrade = riskSection.GetValue<decimal>("MaxRiskPerTrade", 0.02m),
            MaxPortfolioExposure = riskSection.GetValue<decimal>("MaxPortfolioExposure", 0.15m),
            MaxDailyLoss = riskSection.GetValue<decimal>("MaxDailyLoss", 0.05m),
            MaxIntradayDrawdown = riskSection.GetValue<decimal>("MaxIntradayDrawdown", 0.05m),
            KellyMultiplier = riskSection.GetValue<decimal>("KellyMultiplier", 0.25m),
            MinKellyCriterion = riskSection.GetValue<decimal>("MinKellyCriterion", 0.05m),
            MaxKellyCriterion = riskSection.GetValue<decimal>("MaxKellyCriterion", 0.25m),
            VolatilityAdjustmentFactor = riskSection.GetValue<decimal>("VolatilityAdjustmentFactor", 1.5m),
            MaxSlippage = riskSection.GetValue<decimal>("MaxSlippage", 0.001m),
            InitialStopLossPercentage = riskSection.GetValue<decimal>("InitialStopLossPercentage", 0.02m),
            TrailingStopActivation = riskSection.GetValue<decimal>("TrailingStopActivation", 0.01m),
            TrailingStopDistance = riskSection.GetValue<decimal>("TrailingStopDistance", 0.005m),
            MaxOpenPositions = riskSection.GetValue<int>("MaxOpenPositions", 3),
            MaxPositionCorrelation = riskSection.GetValue<decimal>("MaxPositionCorrelation", 0.7m),
            MinTimeBetweenTrades = riskSection.GetValue<int>("MinTimeBetweenTrades", 300),
            CircuitBreakerCooldown = riskSection.GetValue<int>("CircuitBreakerCooldown", 60),
            RiskManagementEnabled = riskSection.GetValue<bool>("RiskManagementEnabled", true),
            CircuitBreakersEnabled = riskSection.GetValue<bool>("CircuitBreakersEnabled", true),
            KellyCriterionEnabled = riskSection.GetValue<bool>("KellyCriterionEnabled", true),
            TrailingStopsEnabled = riskSection.GetValue<bool>("TrailingStopsEnabled", true)
        };
    }
} 