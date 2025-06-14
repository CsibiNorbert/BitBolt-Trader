using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Web.Hubs;
using BitcoinTradingBot.Modules.Strategy.Application.Services;
using BitcoinTradingBot.Modules.Risk.Application.Services;
using BitcoinTradingBot.Modules.Execution.Application.Services;
using BitcoinTradingBot.Modules.Analytics.Application.Services;
using System.Collections.Concurrent;
using CoreTradingHubService = BitcoinTradingBot.Core.Interfaces.ITradingHubService;

namespace BitcoinTradingBot.Web.Services;

/// <summary>
/// Trading bot control service that manages bot lifecycle and coordinates all trading modules
/// </summary>
public interface ITradingBotControlService
{
    Task<bool> StartBotAsync(TradingBotConfiguration config);
    Task<bool> StopBotAsync();
    Task<bool> EmergencyStopAsync();
    Task<bool> UpdateConfigurationAsync(TradingBotConfiguration config);
    Task<TradingBotStatus> GetStatusAsync();
    Task<IReadOnlyList<Trade>> GetRecentTradesAsync(int count = 10);
    bool IsRunning { get; }
    TradingBotConfiguration CurrentConfiguration { get; }
}

public class TradingBotControlService : ITradingBotControlService, IDisposable
{
    private readonly IStrategyService _strategyService;
    private readonly IRiskManagementService _riskService;
    private readonly IOrderExecutionService _executionService;
    private readonly IPerformanceAnalyticsService _analyticsService;
    private readonly CoreTradingHubService _hubService;
    private readonly ILogger<TradingBotControlService> _logger;

    private readonly ConcurrentQueue<Trade> _recentTrades = new();
    private readonly Timer _statusUpdateTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private bool _isRunning = false;
    private bool _emergencyStop = false;
    private bool _disposed = false;
    private TradingBotConfiguration _currentConfig = new();
    private TradingBotStatus _currentStatus = new();

    public bool IsRunning => _isRunning && !_emergencyStop;
    public TradingBotConfiguration CurrentConfiguration => _currentConfig;

    public TradingBotControlService(
        IStrategyService strategyService,
        IRiskManagementService riskService,
        IOrderExecutionService executionService,
        IPerformanceAnalyticsService analyticsService,
        CoreTradingHubService hubService,
        ILogger<TradingBotControlService> logger)
    {
        _strategyService = strategyService ?? throw new ArgumentNullException(nameof(strategyService));
        _riskService = riskService ?? throw new ArgumentNullException(nameof(riskService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _hubService = hubService ?? throw new ArgumentNullException(nameof(hubService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize status update timer
        _statusUpdateTimer = new Timer(UpdateStatusCallback, null, Timeout.Infinite, Timeout.Infinite);
        
        // Initialize default configuration
        _currentConfig = CreateDefaultConfiguration();
        _currentStatus = new TradingBotStatus
        {
            State = TradingBotState.Stopped,
            Message = "Bot initialized",
            StartedAt = null,
            LastUpdateAt = DateTime.UtcNow
        };
    }

    public async Task<bool> StartBotAsync(TradingBotConfiguration config)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Trading bot is already running");
            return false;
        }

        try
        {
            _logger.LogInformation("Starting trading bot with configuration: {Config}", config);

            // Validate configuration
            if (!ValidateConfiguration(config))
            {
                _logger.LogError("Invalid trading bot configuration");
                return false;
            }

            // Update configuration
            _currentConfig = config;
            _emergencyStop = false;

            // Market data service runs as a background service automatically

            // Initialize strategy service
            await _strategyService.InitializeAsync(config.StrategyParameters);

            // Initialize risk management
            await _riskService.InitializeAsync(config.RiskParameters);

            // Start status updates
            _statusUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5));

            // Update status
            _currentStatus = new TradingBotStatus
            {
                State = TradingBotState.Running,
                Message = $"Bot started in {(config.PaperTradingMode ? "paper trading" : "live trading")} mode",
                StartedAt = DateTime.UtcNow,
                LastUpdateAt = DateTime.UtcNow,
                Configuration = config
            };

            _isRunning = true;

            // Broadcast status update
            await _hubService.BroadcastSystemStatusAsync("STARTED", _currentStatus.Message);
            
            _logger.LogInformation("Trading bot started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start trading bot");
            
            _currentStatus = new TradingBotStatus
            {
                State = TradingBotState.Error,
                Message = $"Failed to start: {ex.Message}",
                StartedAt = null,
                LastUpdateAt = DateTime.UtcNow,
                Error = ex.Message
            };

            await _hubService.BroadcastSystemStatusAsync("ERROR", _currentStatus.Message);
            return false;
        }
    }

    public async Task<bool> StopBotAsync()
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Trading bot is not running");
            return false;
        }

        try
        {
            _logger.LogInformation("Stopping trading bot...");

            // Stop status updates
            _statusUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Cancel any pending operations
            _cancellationTokenSource.Cancel();

            // Close any open positions if configured to do so
            if (_currentConfig.ClosePositionsOnStop)
            {
                await CloseAllPositionsAsync();
            }

            // Update status
            _currentStatus = new TradingBotStatus
            {
                State = TradingBotState.Stopped,
                Message = "Bot stopped by user",
                StartedAt = _currentStatus.StartedAt,
                StoppedAt = DateTime.UtcNow,
                LastUpdateAt = DateTime.UtcNow
            };

            _isRunning = false;

            // Broadcast status update
            await _hubService.BroadcastSystemStatusAsync("STOPPED", _currentStatus.Message);
            
            _logger.LogInformation("Trading bot stopped successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping trading bot");
            
            _currentStatus.State = TradingBotState.Error;
            _currentStatus.Message = $"Error stopping bot: {ex.Message}";
            _currentStatus.Error = ex.Message;
            _currentStatus.LastUpdateAt = DateTime.UtcNow;

            await _hubService.BroadcastSystemStatusAsync("ERROR", _currentStatus.Message);
            return false;
        }
    }

    public async Task<bool> EmergencyStopAsync()
    {
        try
        {
            _logger.LogWarning("Emergency stop activated!");

            _emergencyStop = true;
            
            // Immediately stop all trading activities
            _statusUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _cancellationTokenSource.Cancel();

            // Close all positions immediately
            await CloseAllPositionsAsync();

            // Update status
            _currentStatus = new TradingBotStatus
            {
                State = TradingBotState.EmergencyStop,
                Message = "EMERGENCY STOP ACTIVATED - All positions closed",
                StartedAt = _currentStatus.StartedAt,
                StoppedAt = DateTime.UtcNow,
                LastUpdateAt = DateTime.UtcNow,
                IsEmergencyStop = true
            };

            _isRunning = false;

            // Broadcast emergency stop
            await _hubService.BroadcastSystemStatusAsync("EMERGENCY_STOP", _currentStatus.Message);
            
            _logger.LogWarning("Emergency stop completed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during emergency stop");
            await _hubService.BroadcastSystemStatusAsync("ERROR", $"Emergency stop error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateConfigurationAsync(TradingBotConfiguration config)
    {
        try
        {
            if (!ValidateConfiguration(config))
            {
                _logger.LogError("Invalid configuration update");
                return false;
            }

            // Apply new configuration
            _currentConfig = config;

            // Since the services don't have UpdateParametersAsync methods,
            // we'll reinitialize them with the new parameters instead
            if (_isRunning)
            {
                await _strategyService.InitializeAsync(config.StrategyParameters);
                await _riskService.InitializeAsync(config.RiskParameters);
            }

            _currentStatus.Configuration = config;
            _currentStatus.LastUpdateAt = DateTime.UtcNow;

            await _hubService.BroadcastSystemStatusAsync("CONFIG_UPDATED", "Configuration updated successfully");
            
            _logger.LogInformation("Configuration updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update configuration");
            return false;
        }
    }

    public async Task<TradingBotStatus> GetStatusAsync()
    {
        try
        {
            // Get current performance metrics
            var performanceMetrics = await _analyticsService.GetPerformanceMetricsAsync();
            
            // Update status with current metrics
            _currentStatus.PerformanceMetrics = new Dictionary<string, decimal>
            {
                ["TotalReturn"] = performanceMetrics.TotalReturn,
                ["WinRate"] = performanceMetrics.WinRate,
                ["TotalTrades"] = performanceMetrics.TotalTrades,
                ["MaxDrawdown"] = performanceMetrics.MaxDrawdown
            };

            _currentStatus.Runtime = _currentStatus.StartedAt.HasValue 
                ? DateTime.UtcNow - _currentStatus.StartedAt.Value 
                : null;

            return _currentStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bot status");
            return _currentStatus;
        }
    }

    public async Task<IReadOnlyList<Trade>> GetRecentTradesAsync(int count = 10)
    {
        try
        {
            return await _analyticsService.GetTradesAsync(limit: count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent trades");
            return Array.Empty<Trade>();
        }
    }

    private async void UpdateStatusCallback(object? state)
    {
        if (_cancellationTokenSource.Token.IsCancellationRequested || !_isRunning)
            return;

        try
        {
            var status = await GetStatusAsync();
            await _hubService.BroadcastSystemStatusAsync(status.State.ToString(), status.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in status update callback");
        }
    }

    private async Task CloseAllPositionsAsync()
    {
        try
        {
            _logger.LogInformation("Closing all open positions...");
            
            // Get all open positions and close them
            var openPositions = await _executionService.GetOpenPositionsAsync();
            
            foreach (var position in openPositions)
            {
                try
                {
                    // Place a market order to close the position
                    var closeSide = position.Side == BitcoinTradingBot.Core.OrderSide.Buy ? BitcoinTradingBot.Core.OrderSide.Sell : BitcoinTradingBot.Core.OrderSide.Buy;
                    await _executionService.PlaceOrderAsync(
                        position.Symbol, 
                        closeSide, 
                        BitcoinTradingBot.Core.OrderType.Market, 
                        position.Quantity
                    );
                    
                    _logger.LogInformation("Closed position for {Symbol}: {Quantity} @ {Price}", 
                        position.Symbol, position.Quantity, position.EntryPrice);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to close position for {Symbol}", position.Symbol);
                }
            }
            
            _logger.LogInformation("All positions processed for closure");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close all positions");
        }
    }

    private static bool ValidateConfiguration(TradingBotConfiguration config)
    {
        if (config == null) return false;
        if (string.IsNullOrEmpty(config.Symbol)) return false;
        if (config.PositionSize <= 0) return false;
        if (config.RiskPerTrade <= 0 || config.RiskPerTrade > 10) return false;
        
        return true;
    }

    private static TradingBotConfiguration CreateDefaultConfiguration()
    {
        return new TradingBotConfiguration
        {
            Symbol = "BTCUSDT",
            PositionSize = 0.01m,
            RiskPerTrade = 2.0m,
            PaperTradingMode = true,
            ClosePositionsOnStop = true,
            StrategyParameters = new Dictionary<string, object>
            {
                ["KeltnerChannelPeriod"] = 20,
                ["KeltnerChannelMultiplier"] = 2.0,
                ["EmaPeriod"] = 20,
                ["AtrPeriod"] = 14
            },
            RiskParameters = new Dictionary<string, object>
            {
                ["MaxDrawdown"] = 5.0,
                ["MaxPositions"] = 3,
                ["StopLossPercent"] = 2.0,
                ["TakeProfitRatio"] = 2.0
            }
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cancellationTokenSource.Cancel();
        _statusUpdateTimer?.Dispose();
        _cancellationTokenSource?.Dispose();
        
        _disposed = true;
    }
}

/// <summary>
/// Trading bot configuration
/// </summary>
public class TradingBotConfiguration
{
    public string Symbol { get; set; } = "BTCUSDT";
    public decimal PositionSize { get; set; } = 0.01m;
    public decimal RiskPerTrade { get; set; } = 2.0m;
    public bool PaperTradingMode { get; set; } = true;
    public bool ClosePositionsOnStop { get; set; } = true;
    public Dictionary<string, object> StrategyParameters { get; set; } = new();
    public Dictionary<string, object> RiskParameters { get; set; } = new();
}

/// <summary>
/// Trading bot status information
/// </summary>
public class TradingBotStatus
{
    public TradingBotState State { get; set; } = TradingBotState.Stopped;
    public string Message { get; set; } = "";
    public DateTime? StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public DateTime LastUpdateAt { get; set; } = DateTime.UtcNow;
    public TimeSpan? Runtime { get; set; }
    public bool IsEmergencyStop { get; set; } = false;
    public string? Error { get; set; }
    public TradingBotConfiguration? Configuration { get; set; }
    public Dictionary<string, decimal> PerformanceMetrics { get; set; } = new();
}

/// <summary>
/// Trading bot states
/// </summary>
public enum TradingBotState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    EmergencyStop,
    Error
} 