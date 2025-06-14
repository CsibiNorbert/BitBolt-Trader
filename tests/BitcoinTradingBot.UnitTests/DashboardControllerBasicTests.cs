using BitcoinTradingBot.Web.Controllers;
using BitcoinTradingBot.Web.Services;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Core;
using BitcoinTradingBot.Modules.MarketData.Infrastructure.Exchanges;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BitcoinTradingBot.UnitTests;

public class DashboardControllerBasicTests
{
    private readonly ITradingBotControlService _controlService;
    private readonly IBinanceDataProvider _binanceDataProvider;
    private readonly ILogger<DashboardController> _logger;
    private readonly DashboardController _controller;

    public DashboardControllerBasicTests()
    {
        _controlService = Substitute.For<ITradingBotControlService>();
        _binanceDataProvider = Substitute.For<IBinanceDataProvider>();
        _logger = Substitute.For<ILogger<DashboardController>>();

        _controller = new DashboardController(_controlService, _binanceDataProvider, _logger);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldSucceed()
    {
        // Act & Assert
        Assert.NotNull(_controller);
    }

    [Fact]
    public void Constructor_WithNullControlService_ShouldThrowArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => 
            new DashboardController(null!, _binanceDataProvider, _logger));
    }

    [Fact]
    public void Constructor_WithNullBinanceDataProvider_ShouldThrowArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => 
            new DashboardController(_controlService, null!, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => 
            new DashboardController(_controlService, _binanceDataProvider, null!));
    }

    [Fact]
    public async Task GetStatus_ShouldReturnOkResult()
    {
        // Arrange
        var expectedStatus = new TradingBotStatus
        {
            State = TradingBotState.Stopped,
            Message = "Bot stopped",
            StartedAt = null,
            LastUpdateAt = DateTime.UtcNow
        };
        _controlService.GetStatusAsync().Returns(expectedStatus);

        // Act
        var result = await _controller.GetStatus();

        // Assert
        Assert.IsType<ActionResult<TradingBotStatus>>(result);
        var actionResult = result.Result as OkObjectResult;
        Assert.NotNull(actionResult);
        Assert.Equal(200, actionResult.StatusCode);
    }

    [Fact]
    public async Task StartBot_ShouldReturnOkResult_WhenSuccessful()
    {
        // Arrange
        var config = new TradingBotConfiguration
        {
            Symbol = "BTCUSDT",
            PositionSize = 0.01m,
            RiskPerTrade = 2.0m,
            PaperTradingMode = true
        };
        _controlService.StartBotAsync(config).Returns(true);

        // Act
        var result = await _controller.StartBot(config);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task StopBot_ShouldReturnOkResult_WhenSuccessful()
    {
        // Arrange
        _controlService.StopBotAsync().Returns(true);

        // Act
        var result = await _controller.StopBot();

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task EmergencyStop_ShouldReturnOkResult_WhenSuccessful()
    {
        // Arrange
        _controlService.EmergencyStopAsync().Returns(true);

        // Act
        var result = await _controller.EmergencyStop();

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task GetMarketData_ShouldReturnOkResult()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var mockTicker = new BitcoinTradingBot.Modules.MarketData.Infrastructure.Exchanges.BinanceTicker
        {
            Price = 50000m,
            PriceChange = 1000m,
            PriceChangePercent = 2.0m,
            Volume = 1000m,
            QuoteVolume = 50000000m,
            HighPrice = 51000m,
            LowPrice = 49000m,
            CloseTime = DateTime.UtcNow
        };
        _binanceDataProvider.Get24HrTickerAsync(symbol).Returns(mockTicker);

        // Act
        var result = await _controller.GetMarketData(symbol);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task GetHealth_ShouldReturnOkResult()
    {
        // Arrange
        _binanceDataProvider.TestConnectivityAsync(Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _controller.GetHealth();

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult);
        Assert.Equal(200, okResult.StatusCode);
    }
} 