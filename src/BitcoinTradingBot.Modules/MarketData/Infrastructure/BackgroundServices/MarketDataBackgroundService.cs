using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Events;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.MarketData.Infrastructure.Exchanges;
using BinanceKlineInterval = Binance.Net.Enums.KlineInterval;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BitcoinTradingBot.Modules.MarketData.Infrastructure.BackgroundServices;

/// <summary>
/// Background service for continuous market data collection and distribution
/// </summary>
public class MarketDataBackgroundService : BackgroundService
{
    private readonly ILogger<MarketDataBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _subscriptions;
    private readonly ConcurrentDictionary<string, DateTime> _lastUpdateTimes;

    // Configuration constants
    private const string DefaultSymbol = "BTCUSDT";
    private const int HealthCheckIntervalMs = 30000; // 30 seconds
    private const int MaxMissedUpdatesBeforeReconnect = 5;
    private const int ResubscriptionDelayMs = 5000; // 5 seconds

    private readonly Dictionary<string, BinanceKlineInterval> _requiredTimeframes = new()
    {
        { "5M", BinanceKlineInterval.FiveMinutes },
        { "4H", BinanceKlineInterval.FourHour }
    };

    public MarketDataBackgroundService(
        ILogger<MarketDataBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _subscriptions = new ConcurrentDictionary<string, CancellationTokenSource>();
        _lastUpdateTimes = new ConcurrentDictionary<string, DateTime>();
    }

    /// <summary>
    /// Main execution loop for the background service
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataBackgroundService starting...");

        try
        {
            // Start WebSocket subscriptions for required timeframes
            await StartWebSocketSubscriptionsAsync(stoppingToken);

            // Start health monitoring task
            var healthCheckTask = PerformHealthChecksAsync(stoppingToken);

            // Wait for cancellation or health check completion
            await Task.WhenAny(healthCheckTask);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MarketDataBackgroundService stopping due to cancellation request");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "MarketDataBackgroundService encountered a critical error");
            throw;
        }
        finally
        {
            await CleanupSubscriptionsAsync();
        }
    }

    /// <summary>
    /// Starts WebSocket subscriptions for all required timeframes
    /// </summary>
    private async Task StartWebSocketSubscriptionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var binanceProvider = scope.ServiceProvider.GetRequiredService<IBinanceDataProvider>();

        foreach (var (timeframeName, interval) in _requiredTimeframes)
        {
            try
            {
                var subscriptionKey = $"{DefaultSymbol}_{timeframeName}";
                var subscriptionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                _subscriptions.TryAdd(subscriptionKey, subscriptionCts);

                _logger.LogInformation("Starting WebSocket subscription for {Symbol} {Timeframe}", 
                    DefaultSymbol, timeframeName);

                await binanceProvider.SubscribeToKlineUpdatesAsync(
                    DefaultSymbol,
                    interval,
                    candle => OnCandleReceived(candle, timeframeName),
                    subscriptionCts.Token);

                // Initialize last update time
                _lastUpdateTimes.TryAdd(subscriptionKey, DateTime.UtcNow);

                _logger.LogInformation("Successfully started WebSocket subscription for {Symbol} {Timeframe}", 
                    DefaultSymbol, timeframeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start WebSocket subscription for {Symbol} {Timeframe}", 
                    DefaultSymbol, timeframeName);
                throw;
            }

            // Small delay between subscriptions to avoid overwhelming the exchange
            await Task.Delay(1000, cancellationToken);
        }
    }

    /// <summary>
    /// Handles received candle data and publishes events
    /// </summary>
    private async void OnCandleReceived(Candle candle, string timeframeName)
    {
        try
        {
            var subscriptionKey = $"{candle.Symbol}_{timeframeName}";
            _lastUpdateTimes.TryUpdate(subscriptionKey, DateTime.UtcNow, _lastUpdateTimes.GetValueOrDefault(subscriptionKey));

            _logger.LogTrace("Received candle for {Symbol} {Timeframe}: O={Open}, H={High}, L={Low}, C={Close}, V={Volume}",
                candle.Symbol, timeframeName, candle.Open, candle.High, candle.Low, candle.Close, candle.Volume);

            // Publish event for strategy processing
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Convert timeframe string to TimeFrame enum
            var timeFrame = timeframeName switch
            {
                "5M" => TimeFrame.FiveMinutes,
                "4H" => TimeFrame.FourHours,
                _ => TimeFrame.FiveMinutes
            };

            var newCandleEvent = new NewCandleReceivedEvent(
                Symbol: candle.Symbol,
                TimeFrame: timeFrame,
                OpenTime: candle.OpenTime,
                Open: candle.Open,
                High: candle.High,
                Low: candle.Low,
                Close: candle.Close,
                Volume: candle.Volume
            );

            await mediator.Publish(newCandleEvent);

            _logger.LogDebug("Published CandleReceivedEvent for {Symbol} {Timeframe}", candle.Symbol, timeframeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received candle for {Symbol} {Timeframe}", 
                candle.Symbol, timeframeName);
        }
    }

    /// <summary>
    /// Performs periodic health checks on WebSocket connections
    /// </summary>
    private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting health check monitoring...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(HealthCheckIntervalMs, cancellationToken);

                foreach (var (subscriptionKey, lastUpdate) in _lastUpdateTimes.ToList())
                {
                    var timeSinceLastUpdate = DateTime.UtcNow - lastUpdate;
                    var maxExpectedInterval = GetMaxExpectedInterval(subscriptionKey);

                    if (timeSinceLastUpdate > TimeSpan.FromMilliseconds(maxExpectedInterval * MaxMissedUpdatesBeforeReconnect))
                    {
                        _logger.LogWarning("Subscription {SubscriptionKey} appears stale. Last update: {LastUpdate}, " +
                            "Time since: {TimeSince}ms", subscriptionKey, lastUpdate, timeSinceLastUpdate.TotalMilliseconds);

                        await ReconnectSubscriptionAsync(subscriptionKey, cancellationToken);
                    }
                }

                // Test connectivity
                using var scope = _serviceProvider.CreateScope();
                var binanceProvider = scope.ServiceProvider.GetRequiredService<IBinanceDataProvider>();
                
                var isConnected = await binanceProvider.TestConnectivityAsync(cancellationToken);
                if (!isConnected)
                {
                    _logger.LogWarning("Binance connectivity test failed. Attempting to reconnect all subscriptions...");
                    await ReconnectAllSubscriptionsAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
            }
        }

        _logger.LogInformation("Health check monitoring stopped");
    }

    /// <summary>
    /// Reconnects a specific subscription
    /// </summary>
    private async Task ReconnectSubscriptionAsync(string subscriptionKey, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Reconnecting subscription: {SubscriptionKey}", subscriptionKey);

            // Cancel existing subscription
            if (_subscriptions.TryGetValue(subscriptionKey, out var existingCts))
            {
                existingCts.Cancel();
                _subscriptions.TryRemove(subscriptionKey, out _);
            }

            // Wait before reconnecting
            await Task.Delay(ResubscriptionDelayMs, cancellationToken);

            // Parse subscription key to extract symbol and timeframe
            var parts = subscriptionKey.Split('_');
            if (parts.Length == 2)
            {
                var symbol = parts[0];
                var timeframeName = parts[1];

                if (_requiredTimeframes.TryGetValue(timeframeName, out var interval))
                {
                    using var scope = _serviceProvider.CreateScope();
                    var binanceProvider = scope.ServiceProvider.GetRequiredService<IBinanceDataProvider>();

                    var newCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    _subscriptions.TryAdd(subscriptionKey, newCts);

                    await binanceProvider.SubscribeToKlineUpdatesAsync(
                        symbol,
                        interval,
                        candle => OnCandleReceived(candle, timeframeName),
                        newCts.Token);

                    _lastUpdateTimes.TryUpdate(subscriptionKey, DateTime.UtcNow, 
                        _lastUpdateTimes.GetValueOrDefault(subscriptionKey));

                    _logger.LogInformation("Successfully reconnected subscription: {SubscriptionKey}", subscriptionKey);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconnect subscription: {SubscriptionKey}", subscriptionKey);
        }
    }

    /// <summary>
    /// Reconnects all subscriptions
    /// </summary>
    private async Task ReconnectAllSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var subscriptionKeys = _subscriptions.Keys.ToList();
        
        foreach (var subscriptionKey in subscriptionKeys)
        {
            await ReconnectSubscriptionAsync(subscriptionKey, cancellationToken);
            await Task.Delay(1000, cancellationToken); // Delay between reconnections
        }
    }

    /// <summary>
    /// Gets the maximum expected interval for a subscription in milliseconds
    /// </summary>
    private static int GetMaxExpectedInterval(string subscriptionKey)
    {
        return subscriptionKey switch
        {
            var key when key.Contains("5M") => 5 * 60 * 1000, // 5 minutes
            var key when key.Contains("4H") => 4 * 60 * 60 * 1000, // 4 hours
            _ => 5 * 60 * 1000 // Default to 5 minutes
        };
    }

    /// <summary>
    /// Cleans up all subscriptions on service shutdown
    /// </summary>
    private async Task CleanupSubscriptionsAsync()
    {
        _logger.LogInformation("Cleaning up WebSocket subscriptions...");

        var cleanupTasks = _subscriptions.Values.Select(cts =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        });

        await Task.WhenAll(cleanupTasks);

        _subscriptions.Clear();
        _lastUpdateTimes.Clear();

        _logger.LogInformation("WebSocket subscriptions cleanup completed");
    }

    /// <summary>
    /// Disposes the background service
    /// </summary>
    public override void Dispose()
    {
        try
        {
            CleanupSubscriptionsAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MarketDataBackgroundService disposal");
        }
        finally
        {
            base.Dispose();
        }
    }
}

/// <summary>
/// Event published when a new candle is received
/// </summary>
public class CandleReceivedEvent : INotification
{
    public required Candle Candle { get; init; }
    public required string Timeframe { get; init; }
    public DateTime ReceivedAt { get; init; }
} 