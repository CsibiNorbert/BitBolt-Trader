using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core;
using BitcoinTradingBot.Modules.MarketData.Infrastructure.Exchanges;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using BitcoinTradingBot.Web.Hubs;
using System.Timers;
using Timer = System.Timers.Timer;

namespace BitcoinTradingBot.Web.Services;

/// <summary>
/// Service that fetches market data from Binance and distributes it via SignalR
/// </summary>
public class RealTimeMarketDataService : BackgroundService
{
    private readonly ILogger<RealTimeMarketDataService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Timer _marketDataTimer;
    private readonly Timer _performanceTimer;
    
    private const string Symbol = "BTCUSDT";
    private const int MarketDataIntervalMs = 3000; // 3 seconds
    private const int PerformanceIntervalMs = 5000; // 5 seconds

    public RealTimeMarketDataService(
        ILogger<RealTimeMarketDataService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        
        _marketDataTimer = new Timer(MarketDataIntervalMs);
        _marketDataTimer.Elapsed += async (sender, e) => 
        {
            try
            {
                await FetchAndBroadcastMarketData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in market data timer event handler");
            }
        };
        
        _performanceTimer = new Timer(PerformanceIntervalMs);
        _performanceTimer.Elapsed += async (sender, e) => 
        {
            try
            {
                await BroadcastPerformanceMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in performance timer event handler");
            }
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RealTimeMarketDataService starting...");
        
        try
        {
            // Initial delay to let services initialize
            await Task.Delay(5000, stoppingToken);
            
            _marketDataTimer.Start();
            _performanceTimer.Start();
            
            _logger.LogInformation("RealTimeMarketDataService started successfully");
            
            // Keep service running until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when service is stopping
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when service is stopping
            _logger.LogInformation("RealTimeMarketDataService cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RealTimeMarketDataService encountered an error");
            throw;
        }
        finally
        {
            _marketDataTimer?.Stop();
            _performanceTimer?.Stop();
            _logger.LogInformation("RealTimeMarketDataService stopped");
        }
    }

    private async Task FetchAndBroadcastMarketData()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var binanceDataProvider = scope.ServiceProvider.GetRequiredService<IBinanceDataProvider>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TradingHub>>();
            
            // Fetch real-time ticker data
            var ticker = await binanceDataProvider.Get24HrTickerAsync(Symbol);
            if (ticker != null)
            {
                var marketData = new
                {
                    Symbol = Symbol,
                    Price = ticker.Price,
                    Volume = ticker.Volume,
                    Change24h = ticker.PriceChange,
                    ChangePercent24h = ticker.PriceChangePercent,
                    High24h = ticker.HighPrice,
                    Low24h = ticker.LowPrice,
                    Timestamp = DateTime.UtcNow
                };
                
                // Broadcast to all connected clients
                await hubContext.Clients.All.SendAsync("MarketDataUpdate", marketData);
                
                _logger.LogDebug("Broadcasted market data: Price={Price}, Change={Change}%", 
                    ticker.Price, ticker.PriceChangePercent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching and broadcasting market data");
        }
    }

    private async Task BroadcastPerformanceMetrics()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var botControlService = scope.ServiceProvider.GetRequiredService<ITradingBotControlService>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TradingHub>>();
            
            // Get bot status and recent trades
            var status = await botControlService.GetStatusAsync();
            var recentTrades = await botControlService.GetRecentTradesAsync(100);
            
            // Calculate performance metrics
            var totalTrades = recentTrades.Count;
            var winningTrades = recentTrades.Count(t => t.PnL > 0);
            var winRate = totalTrades > 0 ? (decimal)winningTrades / totalTrades * 100 : 0;
            var totalPnL = recentTrades.Sum(t => t.PnL);
            
            var performanceData = new
            {
                TotalReturn = totalPnL,
                WinRate = winRate,
                TotalTrades = totalTrades,
                MaxDrawdown = CalculateMaxDrawdown(recentTrades),
                BotStatus = status?.State.ToString() ?? "Unknown",
                LastUpdate = DateTime.UtcNow
            };
            
            await hubContext.Clients.All.SendAsync("PerformanceUpdate", performanceData);
            
            _logger.LogDebug("Broadcasted performance metrics: Trades={Trades}, WinRate={WinRate}%", 
                totalTrades, winRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting performance metrics");
        }
    }

    private decimal CalculateMaxDrawdown(IReadOnlyList<Trade> trades)
    {
        if (trades.Count == 0) return 0;
        
        decimal peak = 0;
        decimal maxDrawdown = 0;
        decimal runningPnL = 0;
        
        foreach (var trade in trades.OrderBy(t => t.ExitTime))
        {
            runningPnL += trade.PnL;
            if (runningPnL > peak)
            {
                peak = runningPnL;
            }
            
            var drawdown = peak > 0 ? (peak - runningPnL) / peak * 100 : 0;
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }
        
        return maxDrawdown;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RealTimeMarketDataService stopping...");
        
        _marketDataTimer?.Stop();
        _marketDataTimer?.Dispose();
        
        _performanceTimer?.Stop();
        _performanceTimer?.Dispose();
        
        return base.StopAsync(cancellationToken);
    }
} 