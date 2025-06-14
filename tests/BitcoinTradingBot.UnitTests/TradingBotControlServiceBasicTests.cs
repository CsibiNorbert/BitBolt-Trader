using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Web.Services;
using BitcoinTradingBot.Modules.Strategy.Application.Services;
using BitcoinTradingBot.Modules.Risk.Application.Services;
using BitcoinTradingBot.Modules.Execution.Application.Services;
using BitcoinTradingBot.Modules.Analytics.Application.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BitcoinTradingBot.UnitTests;

public class TradingBotControlServiceBasicTests
{
    private readonly IStrategyService _strategyService;
    private readonly IRiskManagementService _riskService;
    private readonly IOrderExecutionService _executionService;
    private readonly IPerformanceAnalyticsService _analyticsService;
    private readonly ITradingHubService _hubService;
    private readonly ILogger<TradingBotControlService> _logger;
    private readonly TradingBotControlService _service;

    public TradingBotControlServiceBasicTests()
    {
        _strategyService = Substitute.For<IStrategyService>();
        _riskService = Substitute.For<IRiskManagementService>();
        _executionService = Substitute.For<IOrderExecutionService>();
        _analyticsService = Substitute.For<IPerformanceAnalyticsService>();
        _hubService = Substitute.For<ITradingHubService>();
        _logger = Substitute.For<ILogger<TradingBotControlService>>();

        _service = new TradingBotControlService(
            _strategyService,
            _riskService,
            _executionService,
            _analyticsService,
            _hubService,
            _logger);
    }

    [Fact]
    public void Constructor_WithAllValidParameters_ShouldSucceed()
    {
        // Act & Assert - Constructor should not throw
        Assert.NotNull(_service);
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public void IsRunning_InitialState_ShouldBeFalse()
    {
        // Assert
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public void CurrentConfiguration_ShouldReturnConfiguration()
    {
        // Act
        var config = _service.CurrentConfiguration;

        // Assert
        Assert.NotNull(config);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReturnStatus()
    {
        // Act
        var status = await _service.GetStatusAsync();

        // Assert
        Assert.NotNull(status);
        Assert.Equal(TradingBotState.Stopped, status.State);
    }

    [Fact]
    public async Task GetRecentTradesAsync_ShouldReturnTradesList()
    {
        // Act
        var trades = await _service.GetRecentTradesAsync();

        // Assert
        Assert.NotNull(trades);
    }

    [Fact]
    public void Constructor_WithNullStrategyService_ShouldThrowArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new TradingBotControlService(
            null!,
            _riskService,
            _executionService,
            _analyticsService,
            _hubService,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new TradingBotControlService(
            _strategyService,
            _riskService,
            _executionService,
            _analyticsService,
            _hubService,
            null!));
    }
} 