using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Models;
using BinanceKlineInterval = Binance.Net.Enums.KlineInterval;

namespace BitcoinTradingBot.Modules.MarketData.Infrastructure.Exchanges;

/// <summary>
/// Interface for Binance exchange data provider with real-time and historical data access
/// </summary>
public interface IBinanceDataProvider : IAsyncDisposable
{
    /// <summary>
    /// Establishes WebSocket connection for real-time data streaming
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., BTCUSDT)</param>
    /// <param name="interval">Kline interval</param>
    /// <param name="onDataReceived">Callback for received market data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeToKlineUpdatesAsync(string symbol, BinanceKlineInterval interval, 
        Action<Candle> onDataReceived, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical kline/candlestick data
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="interval">Kline interval</param>
    /// <param name="limit">Number of candles to retrieve (max 1000)</param>
    /// <param name="startTime">Start time for historical data</param>
    /// <param name="endTime">End time for historical data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of historical candles</returns>
    Task<IReadOnlyList<Candle>> GetHistoricalKlinesAsync(
        string symbol, 
        BinanceKlineInterval interval, 
        int limit = 500,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical candles using Symbol and TimeFrame types
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="timeFrame">Time frame</param>
    /// <param name="count">Number of candles to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of historical candles</returns>
    Task<IReadOnlyList<Candle>> GetHistoricalCandlesAsync(
        Symbol symbol, 
        TimeFrame timeFrame, 
        int count, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time data updates
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="timeFrame">Time frame</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeToRealtimeDataAsync(
        Symbol symbol, 
        TimeFrame timeFrame, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets real-time candle stream
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="timeFrame">Time frame</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of real-time candles</returns>
    IAsyncEnumerable<Candle> GetRealtimeCandlesAsync(
        Symbol symbol, 
        TimeFrame timeFrame, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest ticker price for a symbol
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current price information</returns>
    Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets 24hr ticker statistics
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>24hr ticker statistics</returns>
    Task<BinanceTicker> Get24HrTickerAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to Binance API
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection is healthy</returns>
    Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current ticker data for a symbol
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., BTCUSDT)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current ticker data or null if failed</returns>
    Task<BinanceTicker?> GetCurrentTickerAsync(string symbol, CancellationToken cancellationToken = default);


}

/// <summary>
/// Binance ticker information
/// </summary>
public class BinanceTicker
{
    public decimal Price { get; set; }
    public decimal PriceChange { get; set; }
    public decimal PriceChangePercent { get; set; }
    public decimal Volume { get; set; }
    public decimal QuoteVolume { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public DateTime CloseTime { get; set; }
} 