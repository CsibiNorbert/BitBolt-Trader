using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Analytics.Application.Services;

/// <summary>
/// Service for calculating and providing trading performance analytics
/// </summary>
public class PerformanceAnalyticsService : IPerformanceAnalyticsService
{
    private readonly ILogger<PerformanceAnalyticsService> _logger;
    private readonly List<Trade> _trades = new();

    public PerformanceAnalyticsService(ILogger<PerformanceAnalyticsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var filteredTrades = FilterTrades(from, to);
            
            if (!filteredTrades.Any())
            {
                return new PerformanceMetrics(
                    TotalReturn: 0m,
                    TotalReturnPercentage: 0m,
                    WinRate: 0m,
                    AverageWin: 0m,
                    AverageLoss: 0m,
                    AverageWinLossRatio: 0m,
                    ProfitFactor: 0m,
                    SharpeRatio: 0m,
                    SortinoRatio: 0m,
                    MaxDrawdown: 0m,
                    TotalTrades: 0,
                    WinningTrades: 0,
                    LosingTrades: 0,
                    PeriodStart: from ?? DateTime.UtcNow.AddMonths(-1),
                    PeriodEnd: to ?? DateTime.UtcNow
                );
            }

            var totalReturn = filteredTrades.Sum(t => t.PnL);
            var winningTrades = filteredTrades.Where(t => t.IsWinner).ToList();
            var losingTrades = filteredTrades.Where(t => !t.IsWinner).ToList();
            
            var averageWin = winningTrades.Any() ? winningTrades.Average(t => t.PnL) : 0m;
            var averageLoss = losingTrades.Any() ? Math.Abs(losingTrades.Average(t => t.PnL)) : 0m;
            var winRate = (decimal)winningTrades.Count / filteredTrades.Count * 100m;
            
            var grossProfit = winningTrades.Sum(t => t.PnL);
            var grossLoss = Math.Abs(losingTrades.Sum(t => t.PnL));
            var profitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? 999m : 1m;

            return new PerformanceMetrics(
                TotalReturn: totalReturn,
                TotalReturnPercentage: CalculateReturnPercentage(totalReturn),
                WinRate: winRate,
                AverageWin: averageWin,
                AverageLoss: averageLoss,
                AverageWinLossRatio: averageLoss > 0 ? averageWin / averageLoss : averageWin > 0 ? 999m : 1m,
                ProfitFactor: profitFactor,
                SharpeRatio: CalculateSharpeRatio(filteredTrades),
                SortinoRatio: CalculateSortinoRatio(filteredTrades),
                MaxDrawdown: CalculateMaxDrawdown(filteredTrades),
                TotalTrades: filteredTrades.Count,
                WinningTrades: winningTrades.Count,
                LosingTrades: losingTrades.Count,
                PeriodStart: from ?? filteredTrades.Min(t => t.EntryTime),
                PeriodEnd: to ?? filteredTrades.Max(t => t.ExitTime)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate performance metrics");
            throw;
        }
    }

    public async Task<IReadOnlyList<Trade>> GetTradesAsync(DateTime? from = null, DateTime? to = null, int? limit = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var filteredTrades = FilterTrades(from, to);
            
            if (limit.HasValue)
            {
                filteredTrades = filteredTrades.Take(limit.Value).ToList();
            }

            return filteredTrades;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trades");
            throw;
        }
    }

    public async Task<decimal> GetCurrentDrawdownAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_trades.Any()) return 0m;

            var equity = CalculateEquityCurve(_trades);
            var peak = equity.Max();
            var current = equity.Last();
            
            return peak > 0 ? ((peak - current) / peak) * 100m : 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate current drawdown");
            throw;
        }
    }

    public async Task<decimal> GetWinRateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_trades.Any()) return 0m;

            var winningTrades = _trades.Count(t => t.IsWinner);
            return ((decimal)winningTrades / _trades.Count) * 100m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate win rate");
            throw;
        }
    }

    private List<Trade> FilterTrades(DateTime? from, DateTime? to)
    {
        var query = _trades.AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(t => t.EntryTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(t => t.ExitTime <= to.Value);
        }

        return query.OrderByDescending(t => t.ExitTime).ToList();
    }

    private static decimal CalculateReturnPercentage(decimal totalReturn)
    {
        // Simplified calculation - in real implementation, use starting capital
        const decimal startingCapital = 10000m;
        return (totalReturn / startingCapital) * 100m;
    }

    private static decimal CalculateSharpeRatio(List<Trade> trades)
    {
        if (trades.Count < 2) return 0m;

        var returns = trades.Select(t => t.PnLPercentage).ToList();
        var avgReturn = returns.Average();
        var stdDev = Math.Sqrt(returns.Sum(r => Math.Pow((double)(r - avgReturn), 2)) / (returns.Count - 1));
        
        return stdDev > 0 ? (decimal)(avgReturn / (decimal)stdDev) : 0m;
    }

    private static decimal CalculateSortinoRatio(List<Trade> trades)
    {
        if (trades.Count < 2) return 0m;

        var returns = trades.Select(t => t.PnLPercentage).ToList();
        var avgReturn = returns.Average();
        var negativeReturns = returns.Where(r => r < 0).ToList();
        
        if (!negativeReturns.Any()) return 999m; // Very high ratio when no negative returns
        
        var downstdDev = Math.Sqrt(negativeReturns.Sum(r => Math.Pow((double)r, 2)) / negativeReturns.Count);
        
        return downstdDev > 0 ? (decimal)(avgReturn / (decimal)downstdDev) : 0m;
    }

    private static decimal CalculateMaxDrawdown(List<Trade> trades)
    {
        if (!trades.Any()) return 0m;

        var equity = CalculateEquityCurve(trades);
        var maxDrawdown = 0m;
        var peak = equity[0];

        foreach (var value in equity)
        {
            if (value > peak)
            {
                peak = value;
            }
            else
            {
                var drawdown = ((peak - value) / peak) * 100m;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
        }

        return maxDrawdown;
    }

    private static List<decimal> CalculateEquityCurve(List<Trade> trades)
    {
        const decimal startingEquity = 10000m;
        var equity = new List<decimal> { startingEquity };
        var currentEquity = startingEquity;

        foreach (var trade in trades.OrderBy(t => t.ExitTime))
        {
            currentEquity += trade.PnL;
            equity.Add(currentEquity);
        }

        return equity;
    }
} 