using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BitcoinTradingBot.Web.Services;

/// <summary>
/// Service for broadcasting real-time updates to connected clients via SignalR
/// </summary>
public class TradingHubService : BitcoinTradingBot.Core.Interfaces.ITradingHubService
{
    private readonly IHubContext<TradingHub> _hubContext;
    private readonly ILogger<TradingHubService> _logger;

    public TradingHubService(
        IHubContext<TradingHub> hubContext,
        ILogger<TradingHubService> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task BroadcastSystemStatusAsync(string status, string message)
    {
        try
        {
            _logger.LogDebug("Broadcasting system status: {Status} - {Message}", status, message);

            await _hubContext.Clients.All.SendAsync("SystemStatusUpdate", new
            {
                Status = status,
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast system status");
        }
    }

    public async Task BroadcastMarketDataAsync(MarketDataSnapshot snapshot)
    {
        try
        {
            _logger.LogDebug("Broadcasting market data for {Symbol}", snapshot.Symbol);

            await _hubContext.Clients.All.SendAsync("MarketDataUpdate", new
            {
                Symbol = snapshot.Symbol.Value,
                Price = snapshot.Price.Value,
                Volume = snapshot.Volume,
                Change24h = snapshot.Change24h,
                ChangePercent24h = snapshot.ChangePercent24h,
                High24h = snapshot.High24h,
                Low24h = snapshot.Low24h,
                Timestamp = snapshot.Timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast market data for {Symbol}", snapshot.Symbol);
        }
    }

    public async Task BroadcastTradeAsync(Trade trade)
    {
        try
        {
            _logger.LogDebug("Broadcasting trade: {TradeId}", trade.TradeId);

            await _hubContext.Clients.All.SendAsync("TradeUpdate", new
            {
                TradeId = trade.TradeId,
                Symbol = trade.Symbol.Value,
                Side = trade.Side.ToString(),
                EntryPrice = trade.EntryPrice.Value,
                ExitPrice = trade.ExitPrice.Value,
                Quantity = trade.Quantity.Value,
                EntryTime = trade.EntryTime,
                ExitTime = trade.ExitTime,
                PnL = trade.PnL,
                PnLPercentage = trade.PnLPercentage,
                Fee = trade.Fee,
                ExitReason = trade.ExitReason,
                IsWinner = trade.IsWinner
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast trade: {TradeId}", trade.TradeId);
        }
    }

    public async Task BroadcastPerformanceUpdateAsync(PerformanceMetrics metrics)
    {
        try
        {
            _logger.LogDebug("Broadcasting performance metrics for period {Start} to {End}", 
                metrics.PeriodStart, metrics.PeriodEnd);

            await _hubContext.Clients.All.SendAsync("PerformanceUpdate", new
            {
                TotalReturn = metrics.TotalReturn,
                TotalReturnPercentage = metrics.TotalReturnPercentage,
                WinRate = metrics.WinRate,
                AverageWin = metrics.AverageWin,
                AverageLoss = metrics.AverageLoss,
                AverageWinLossRatio = metrics.AverageWinLossRatio,
                ProfitFactor = metrics.ProfitFactor,
                SharpeRatio = metrics.SharpeRatio,
                SortinoRatio = metrics.SortinoRatio,
                MaxDrawdown = metrics.MaxDrawdown,
                TotalTrades = metrics.TotalTrades,
                WinningTrades = metrics.WinningTrades,
                LosingTrades = metrics.LosingTrades,
                PeriodStart = metrics.PeriodStart,
                PeriodEnd = metrics.PeriodEnd
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast performance metrics");
        }
    }
} 