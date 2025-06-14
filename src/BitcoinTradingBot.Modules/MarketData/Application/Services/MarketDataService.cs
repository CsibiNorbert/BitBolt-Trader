using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.MarketData.Application.Services;

/// <summary>
/// Market data service implementation with caching and real-time capabilities
/// </summary>
public class MarketDataService : IMarketDataService
{
    private readonly Infrastructure.Exchanges.IBinanceDataProvider _binanceProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MarketDataService> _logger;

    private const int DefaultCandleCount = 500;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(1);

    public MarketDataService(
        Infrastructure.Exchanges.IBinanceDataProvider binanceProvider,
        IMemoryCache cache,
        ILogger<MarketDataService> logger)
    {
        _binanceProvider = binanceProvider ?? throw new ArgumentNullException(nameof(binanceProvider));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets historical candles with caching
    /// </summary>
    public async Task<IReadOnlyList<Candle>> GetHistoricalCandlesAsync(
        Symbol symbol, 
        TimeFrame timeFrame, 
        int count = DefaultCandleCount, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"candles_{symbol}_{timeFrame}_{count}";
            
            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Candle>? cachedCandles))
            {
                _logger.LogDebug("Returning cached candles for {Symbol} {TimeFrame}", symbol, timeFrame);
                return cachedCandles!;
            }

            _logger.LogInformation("Fetching historical candles for {Symbol} {TimeFrame}, count: {Count}", 
                symbol, timeFrame, count);

            var candles = await _binanceProvider.GetHistoricalCandlesAsync(symbol, timeFrame, count, cancellationToken);
            
            // Cache the result
            _cache.Set(cacheKey, candles, CacheExpiry);
            
            _logger.LogInformation("Successfully fetched {Count} candles for {Symbol} {TimeFrame}", 
                candles.Count, symbol, timeFrame);

            return candles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching historical candles for {Symbol} {TimeFrame}", symbol, timeFrame);
            throw;
        }
    }

    /// <summary>
    /// Subscribes to real-time market data updates
    /// </summary>
    public async Task SubscribeToRealtimeDataAsync(
        Symbol symbol, 
        TimeFrame timeFrame, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Subscribing to real-time data for {Symbol} {TimeFrame}", symbol, timeFrame);
            await _binanceProvider.SubscribeToRealtimeDataAsync(symbol, timeFrame, cancellationToken);
            _logger.LogInformation("Successfully subscribed to real-time data for {Symbol} {TimeFrame}", symbol, timeFrame);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to real-time data for {Symbol} {TimeFrame}", symbol, timeFrame);
            throw;
        }
    }

    /// <summary>
    /// Gets real-time candle stream
    /// </summary>
    public async IAsyncEnumerable<Candle> GetRealtimeCandles(
        Symbol symbol, 
        TimeFrame timeFrame, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting real-time candle stream for {Symbol} {TimeFrame}", symbol, timeFrame);
        
        await foreach (var candle in _binanceProvider.GetRealtimeCandlesAsync(symbol, timeFrame, cancellationToken))
        {
            // Invalidate cache when new candle arrives
            var cacheKey = $"candles_{symbol}_{timeFrame}_*";
            _logger.LogDebug("New candle received for {Symbol} {TimeFrame}, invalidating cache", symbol, timeFrame);
            
            yield return candle;
        }
    }
} 