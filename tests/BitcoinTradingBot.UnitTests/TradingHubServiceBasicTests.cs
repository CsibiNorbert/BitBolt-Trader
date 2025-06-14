using BitcoinTradingBot.Web.Services;
using BitcoinTradingBot.Web.Hubs;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BitcoinTradingBot.UnitTests;

public class TradingHubServiceBasicTests
{
    private readonly IHubContext<TradingHub> _hubContext;
    private readonly ILogger<TradingHubService> _logger;
    private readonly TradingHubService _service;

    public TradingHubServiceBasicTests()
    {
        _hubContext = Substitute.For<IHubContext<TradingHub>>();
        _logger = Substitute.For<ILogger<TradingHubService>>();

        _service = new TradingHubService(_hubContext, _logger);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldSucceed()
    {
        // Act & Assert
        Assert.NotNull(_service);
    }

    [Fact]
    public void Constructor_WithNullHubContext_ShouldThrowArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => 
            new TradingHubService(null!, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => 
            new TradingHubService(_hubContext, null!));
    }

    [Fact]
    public async Task BroadcastSystemStatusAsync_ShouldNotThrow()
    {
        // Act & Assert - Should not throw
        await _service.BroadcastSystemStatusAsync("TEST", "Test message");
    }

    [Fact]
    public async Task BroadcastMarketDataAsync_ShouldNotThrow()
    {
        // Arrange
        var marketData = new MarketDataSnapshot(
            Symbol: new Symbol("BTCUSDT"),
            Price: Price.Create(45000m),
            Volume: 1000000m,
            Change24h: 2.5m,
            ChangePercent24h: 5.5m,
            High24h: 46000m,
            Low24h: 44000m,
            Timestamp: DateTime.UtcNow
        );

        // Act & Assert - Should not throw
        await _service.BroadcastMarketDataAsync(marketData);
    }

    [Fact]
    public async Task BroadcastTradeAsync_ShouldNotThrow()
    {
        // Arrange
        var trade = new Trade(
            TradeId: "trade-1",
            Symbol: new Symbol("BTCUSDT"),
            Side: OrderSide.Buy,
            EntryPrice: Price.Create(45000m),
            ExitPrice: Price.Create(45500m),
            Quantity: Quantity.Create(0.001m),
            EntryTime: DateTime.UtcNow.AddMinutes(-10),
            ExitTime: DateTime.UtcNow,
            PnL: 0.5m,
            PnLPercentage: 1.11m,
            Fee: 0.01m,
            ExitReason: "Take Profit"
        );

        // Act & Assert - Should not throw
        await _service.BroadcastTradeAsync(trade);
    }

    [Fact]
    public async Task BroadcastPerformanceUpdateAsync_ShouldNotThrow()
    {
        // Arrange
        var metrics = new PerformanceMetrics(
            TotalReturn: 150.5m,
            TotalReturnPercentage: 15.05m,
            WinRate: 0.7m,
            AverageWin: 30.2m,
            AverageLoss: -15.1m,
            AverageWinLossRatio: 2.0m,
            ProfitFactor: 2.0m,
            SharpeRatio: 1.8m,
            SortinoRatio: 2.2m,
            MaxDrawdown: -25.3m,
            TotalTrades: 10,
            WinningTrades: 7,
            LosingTrades: 3,
            PeriodStart: DateTime.UtcNow.AddDays(-30),
            PeriodEnd: DateTime.UtcNow
        );

        // Act & Assert - Should not throw
        await _service.BroadcastPerformanceUpdateAsync(metrics);
    }
} 