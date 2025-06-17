using BitcoinTradingBot.Web.Models;
using TradingView.Blazor.Models;

namespace BitcoinTradingBot.Web.Services;

/// <summary>
/// Service for managing TradingView chart data and integration
/// </summary>
public class TradingViewChartService
{
    private readonly ILogger<TradingViewChartService> _logger;
    private readonly List<CryptoCandlestickData> _chartData = new();
    private readonly List<TradingSignalMarker> _markers = new();
    private readonly object _lockObject = new();

    public TradingViewChartService(ILogger<TradingViewChartService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get chart data for TradingView
    /// </summary>
    public ChartData GetChartData()
    {
        lock (_lockObject)
        {
            return new ChartData
            {
                ChartEntries = new List<IChartEntry>(_chartData),
                MarkerData = new List<Marker>(_markers)
            };
        }
    }

    /// <summary>
    /// Add new candlestick data point
    /// </summary>
    public void AddCandlestickData(DateTime time, decimal open, decimal high, decimal low, decimal close, decimal volume)
    {
        lock (_lockObject)
        {
            var candlestick = new CryptoCandlestickData(time, open, high, low, close, volume);
            _chartData.Add(candlestick);

            // Keep only last 500 candles to prevent memory issues
            if (_chartData.Count > 500)
            {
                _chartData.RemoveAt(0);
            }

            _logger.LogDebug("Added candlestick data: {Time} O:{Open} H:{High} L:{Low} C:{Close} V:{Volume}",
                time, open, high, low, close, volume);
        }
    }

    /// <summary>
    /// Update latest candlestick with new price data
    /// </summary>
    public void UpdateLatestCandle(decimal price, decimal volume)
    {
        lock (_lockObject)
        {
            if (_chartData.Count > 0)
            {
                var latest = _chartData[^1];
                latest.Close = price;
                latest.High = Math.Max(latest.High, price);
                latest.Low = Math.Min(latest.Low, price);
                latest.Volume += volume;
            }
            else
            {
                // If no data exists, create initial candle
                var now = DateTime.UtcNow;
                AddCandlestickData(now, price, price, price, price, volume);
            }
        }
    }

    /// <summary>
    /// Add trading signal marker
    /// </summary>
    public void AddSignalMarker(DateTime time, decimal price, string signalType, string strategy, string text)
    {
        lock (_lockObject)
        {
            var marker = new TradingSignalMarker
            {
                Time = time,
                Text = text,
                SignalType = signalType,
                Strategy = strategy,
                Price = price
            };

            _markers.Add(marker);

            // Keep only last 100 markers
            if (_markers.Count > 100)
            {
                _markers.RemoveAt(0);
            }

            _logger.LogInformation("Added signal marker: {SignalType} at {Price} - {Text}", signalType, price, text);
        }
    }

    /// <summary>
    /// Generate sample data for testing
    /// </summary>
    public void GenerateSampleData(int candleCount = 100, decimal basePrice = 67000m)
    {
        lock (_lockObject)
        {
            _chartData.Clear();
            var random = new Random();
            var currentTime = DateTime.UtcNow.AddMinutes(-candleCount * 5); // 5-minute intervals
            var currentPrice = basePrice;

            for (int i = 0; i < candleCount; i++)
            {
                var timeStamp = currentTime.AddMinutes(i * 5);
                
                // Generate realistic price movement
                var priceChange = (decimal)(random.NextDouble() * 2000 - 1000); // +/- $1000
                var open = currentPrice;
                var close = open + priceChange * 0.1m; // Smaller movements
                var high = Math.Max(open, close) + (decimal)(random.NextDouble() * 500);
                var low = Math.Min(open, close) - (decimal)(random.NextDouble() * 500);
                var volume = (decimal)(random.NextDouble() * 1000 + 100);

                AddCandlestickData(timeStamp, open, high, low, close, volume);
                currentPrice = close;
            }

            // Add some sample signals
            AddSignalMarker(
                currentTime.AddMinutes(candleCount * 5 / 4),
                _chartData[candleCount / 4].Close,
                "BUY",
                "Keltner Channel",
                "KC Breakout Buy Signal"
            );

            AddSignalMarker(
                currentTime.AddMinutes(candleCount * 5 * 3 / 4),
                _chartData[candleCount * 3 / 4].Close,
                "SELL",
                "Keltner Channel",
                "KC Breakdown Sell Signal"
            );

            _logger.LogInformation("Generated {Count} sample candlesticks with base price {BasePrice}", candleCount, basePrice);
        }
    }

    /// <summary>
    /// Clear all chart data
    /// </summary>
    public void ClearData()
    {
        lock (_lockObject)
        {
            _chartData.Clear();
            _markers.Clear();
            _logger.LogInformation("Cleared all chart data");
        }
    }

    /// <summary>
    /// Get current data count
    /// </summary>
    public int GetDataCount()
    {
        lock (_lockObject)
        {
            return _chartData.Count;
        }
    }
} 