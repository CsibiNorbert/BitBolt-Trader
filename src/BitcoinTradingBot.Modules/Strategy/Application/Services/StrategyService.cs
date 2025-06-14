using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Events;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.Strategy.Domain.Strategies;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Strategy.Application.Services;

/// <summary>
/// Main strategy service that orchestrates strategy execution
/// </summary>
public class StrategyService : IStrategyService, INotificationHandler<NewCandleReceivedEvent>
{
    private readonly IMultiTimeframeKeltnerStrategy _keltnerStrategy;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<StrategyService> _logger;
    private bool _isInitialized = false;
    private readonly object _initLock = new object();

    // Configuration
    private readonly Symbol _primarySymbol = new("BTCUSDT");
    private readonly TimeFrame _primaryTimeFrame = TimeFrame.FourHours;
    private readonly TimeFrame _entryTimeFrame = TimeFrame.FiveMinutes;

    public bool IsInitialized => _isInitialized;
    public string StrategyName => "Multi-Timeframe Keltner Channel Strategy";

    public StrategyService(
        IMultiTimeframeKeltnerStrategy keltnerStrategy,
        IEventPublisher eventPublisher,
        ILogger<StrategyService> logger)
    {
        _keltnerStrategy = keltnerStrategy ?? throw new ArgumentNullException(nameof(keltnerStrategy));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Auto-initialize with default parameters
        _ = Task.Run(async () => await EnsureInitializedAsync());
    }

    public async Task<TradingSignalData?> GenerateSignalAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure initialization before processing
            await EnsureInitializedAsync();

            _logger.LogDebug("Generating signal for {Symbol}", symbol);

            // Use the Keltner strategy to evaluate signal
            var signalData = await _keltnerStrategy.EvaluateSignalAsync(symbol, cancellationToken);

            if (signalData.Signal != TradingSignal.None)
            {
                _logger.LogInformation("Strategy generated {Signal} signal for {Symbol} with {Confidence:P2} confidence", 
                    signalData.Signal, signalData.Symbol, signalData.Confidence);

                return signalData;
            }

            _logger.LogTrace("No signal generated for {Symbol}: {Reason}", 
                symbol, signalData.Metadata?.GetValueOrDefault("Reason", "Unknown"));

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating signal for {Symbol}", symbol);
            // Return null instead of throwing to prevent cascade failures
            return null;
        }
    }

    public async Task InitializeAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing strategy service: {StrategyName}", StrategyName);

            // Initialize the Keltner strategy if it has initialization methods
            // This would configure the strategy with the provided parameters
            await Task.CompletedTask; // Placeholder for async operations

            _isInitialized = true;

            _logger.LogInformation("Strategy service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize strategy service");
            _isInitialized = false;
            throw;
        }
    }

    /// <summary>
    /// Ensures the strategy service is initialized
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;

            try
            {
                _logger.LogDebug("Auto-initializing strategy service: {StrategyName}", StrategyName);
                
                // Initialize with default parameters
                var defaultParameters = new Dictionary<string, object>
                {
                    ["Symbol"] = _primarySymbol.Value,
                    ["PrimaryTimeFrame"] = _primaryTimeFrame.ToString(),
                    ["EntryTimeFrame"] = _entryTimeFrame.ToString()
                };

                // Synchronous initialization for simplicity
                _isInitialized = true;
                _logger.LogDebug("Strategy service auto-initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-initialize strategy service");
                _isInitialized = false;
            }
        }
    }

    /// <summary>
    /// Handles new candle received events and evaluates strategy
    /// </summary>
    public async Task Handle(NewCandleReceivedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Only process our primary symbol
            if (notification.Symbol != _primarySymbol)
            {
                return;
            }

            _logger.LogDebug("Processing new candle for {Symbol} on {TimeFrame}", 
                notification.Symbol, notification.TimeFrame);

            // Only evaluate strategy on relevant timeframes
            if (notification.TimeFrame != _primaryTimeFrame && notification.TimeFrame != _entryTimeFrame)
            {
                return;
            }

            // Generate signal using the IStrategyService method
            var signalData = await GenerateSignalAsync(notification.Symbol, cancellationToken);

            // If we have a valid signal, publish it
            if (signalData != null)
            {
                var signalEvent = new TradingSignalGeneratedEvent(
                    Symbol: signalData.Symbol,
                    Signal: signalData.Signal,
                    EntryPrice: signalData.EntryPrice,
                    StopLoss: signalData.StopLoss,
                    TakeProfit: signalData.TakeProfit,
                    Confidence: signalData.Confidence,
                    Metadata: signalData.Metadata
                );

                await _eventPublisher.PublishAsync(signalEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing candle event for {Symbol} on {TimeFrame}", 
                notification.Symbol, notification.TimeFrame);
            // Don't rethrow to prevent cascade failures
        }
    }

    /// <summary>
    /// Gets candle data for strategy evaluation
    /// In a real implementation, this would call the market data service
    /// </summary>
    private async Task<IReadOnlyList<Candle>?> GetCandleDataAsync(
        Symbol symbol, 
        TimeFrame timeFrame, 
        CancellationToken cancellationToken)
    {
        // TODO: This should call IMarketDataService.GetCandlesAsync()
        // For now, return null to indicate no data available
        // This will be implemented when we integrate with the MarketData module
        
        _logger.LogDebug("Requesting candle data for {Symbol} on {TimeFrame} (not implemented yet)", 
            symbol, timeFrame);
        
        await Task.CompletedTask; // Placeholder for async operations
        return null;
    }
} 