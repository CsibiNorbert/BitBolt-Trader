using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Risk.Application.Services;

/// <summary>
/// Risk management service that implements IRiskManagementService
/// </summary>
public class RiskManagementService : IRiskManagementService
{
    private readonly ILogger<RiskManagementService> _logger;
    private bool _isInitialized = false;
    private Dictionary<string, object> _parameters = new();

    public bool IsInitialized => _isInitialized;

    public RiskManagementService(ILogger<RiskManagementService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PositionSizing> CalculatePositionSizeAsync(PositionSizeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Risk management service is not initialized");
            }

            _logger.LogDebug("Calculating position size for {Symbol} with risk {RiskPercent}%", 
                request.Symbol, request.RiskPercentage);

            // Calculate basic position size based on risk per trade
            var riskAmount = request.AccountBalance * (request.RiskPercentage / 100m);
            var riskPerUnit = request.RiskPerUnit;

            if (riskPerUnit == 0)
            {
                _logger.LogWarning("Risk per unit is zero, cannot calculate position size");
                return new PositionSizing(
                    Quantity.Zero(),
                    0m,
                    0m,
                    0m,
                    "Risk calculation failed - zero risk per unit"
                );
            }

            var quantity = riskAmount / riskPerUnit;

            // Apply minimum order size constraints
            if (request.ExchangeMinimumOrderSize.HasValue && quantity < request.ExchangeMinimumOrderSize.Value)
            {
                quantity = request.ExchangeMinimumOrderSize.Value;
            }

            // Apply volatility adjustments
            if (request.VolatilityMultiplier.HasValue)
            {
                quantity *= request.VolatilityMultiplier.Value;
            }

            // Apply Kelly Criterion if historical performance is available
            if (request.WinRate.HasValue && request.AverageWinLossRatio.HasValue)
            {
                var kellyPercentage = CalculateKellyCriterion(request.WinRate.Value, request.AverageWinLossRatio.Value);
                var kellyAmount = request.AccountBalance * (kellyPercentage / 100m);
                var kellyQuantity = kellyAmount / riskPerUnit;

                // Use the smaller of Kelly and fixed risk sizing for safety
                quantity = Math.Min(quantity, kellyQuantity);
            }

            // Apply drawdown adjustments
            if (request.CurrentDrawdown.HasValue && request.CurrentDrawdown.Value > 0)
            {
                var drawdownReduction = Math.Min(request.CurrentDrawdown.Value / 10m, 0.5m); // Max 50% reduction
                quantity *= (1m - drawdownReduction);
            }

            var notionalValue = quantity * request.EntryPrice.Value;

            return new PositionSizing(
                Quantity.Create(quantity),
                riskAmount,
                request.RiskPercentage,
                notionalValue,
                "Standard risk-based sizing with adjustments"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate position size");
            throw;
        }
    }

    public async Task<RiskMetrics> GetCurrentRiskMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Risk management service is not initialized");
            }

            // In a real implementation, these would be calculated from actual positions and account data
            return new RiskMetrics(
                CurrentDrawdown: 0m,
                MaxDrawdown: 0m,
                TotalExposure: 0m,
                AvailableCapital: 10000m,
                VaR95: 0m,
                LastUpdated: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get risk metrics");
            throw;
        }
    }

    public async Task<bool> IsTradeAllowedAsync(TradingSignalData signal, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isInitialized)
            {
                return false;
            }

            _logger.LogDebug("Checking if trade is allowed for signal {SignalId}", signal.Id);

            // Basic risk checks
            if (signal.Confidence < 0.5m)
            {
                _logger.LogWarning("Trade not allowed - confidence too low: {Confidence}", signal.Confidence);
                return false;
            }

            if (signal.StopLoss == null)
            {
                _logger.LogWarning("Trade not allowed - no stop loss defined");
                return false;
            }

            var riskRewardRatio = signal.RiskRewardRatio;
            if (riskRewardRatio < 1.5m)
            {
                _logger.LogWarning("Trade not allowed - risk/reward ratio too low: {RiskReward}", riskRewardRatio);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if trade is allowed");
            return false;
        }
    }

    public async Task InitializeAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing risk management service");
            
            _parameters = parameters ?? new Dictionary<string, object>();
            _isInitialized = true;

            _logger.LogInformation("Risk management service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize risk management service");
            _isInitialized = false;
            throw;
        }
    }

    private static decimal CalculateKellyCriterion(decimal winRate, decimal averageWinLossRatio)
    {
        // Kelly Criterion: f = (bp - q) / b
        // where:
        // f = fraction of capital to wager
        // b = odds received on the wager (average win / average loss)
        // p = probability of winning (win rate)
        // q = probability of losing (1 - win rate)

        var p = winRate / 100m; // Convert percentage to decimal
        var q = 1m - p;
        var b = averageWinLossRatio;

        var kelly = (b * p - q) / b;
        
        // Cap Kelly at 25% for safety
        return Math.Max(0m, Math.Min(kelly * 100m, 25m));
    }
} 