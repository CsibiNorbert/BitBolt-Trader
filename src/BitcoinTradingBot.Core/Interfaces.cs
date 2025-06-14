using BitcoinTradingBot.Core.Events;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Core.Interfaces;

/// <summary>
/// Interface for publishing domain events
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : DomainEvent;
}

/// <summary>
/// Interface for market data services
/// </summary>
public interface IMarketDataService
{
    Task<IReadOnlyList<Candle>> GetHistoricalCandlesAsync(Symbol symbol, TimeFrame timeFrame, int count, CancellationToken cancellationToken = default);
    Task SubscribeToRealtimeDataAsync(Symbol symbol, TimeFrame timeFrame, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Candle> GetRealtimeCandles(Symbol symbol, TimeFrame timeFrame, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for trading strategy services
/// </summary>
public interface ITradingStrategy
{
    Task<TradingSignalData> EvaluateSignalAsync(Symbol symbol, CancellationToken cancellationToken = default);
    string StrategyName { get; }
    bool IsEnabled { get; set; }
}

/// <summary>
/// Interface for risk management services
/// </summary>
public interface IRiskManager
{
    Task<PositionSizing> CalculatePositionSizeAsync(Symbol symbol, Price entryPrice, Price stopLoss, CancellationToken cancellationToken = default);
    Task<RiskMetrics> GetCurrentRiskMetricsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsTradeAllowedAsync(Symbol symbol, TradingSignalData signal, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for order execution services
/// </summary>
public interface IOrderExecutor
{
    Task<string> PlaceOrderAsync(Symbol symbol, OrderSide side, OrderType type, Quantity quantity, Price? price = null, CancellationToken cancellationToken = default);
    Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);
    Task<OrderInfo> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for portfolio management services
/// </summary>
public interface IPortfolioService
{
    Task<decimal> GetAccountBalanceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken cancellationToken = default);
    Task<Position?> GetPositionAsync(Symbol symbol, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for analytics services
/// </summary>
public interface IAnalyticsService
{
    Task<PerformanceMetrics> GetPerformanceMetricsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<decimal> GetWinRateAsync(Symbol? symbol = null, CancellationToken cancellationToken = default);
    Task<decimal> GetAverageWinLossRatioAsync(Symbol? symbol = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for notification services
/// </summary>
public interface INotificationService
{
    Task SendAlertAsync(string message, AlertLevel level = AlertLevel.Info, CancellationToken cancellationToken = default);
    Task SendTradingSignalAlertAsync(TradingSignalData signal, CancellationToken cancellationToken = default);
    Task SendSystemHealthAlertAsync(string component, bool isHealthy, string? message = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for system health monitoring
/// </summary>
public interface IHealthMonitor
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> GetHealthMetricsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for configuration management
/// </summary>
public interface IConfigurationService
{
    T GetValue<T>(string key, T defaultValue = default!);
    Task SetValueAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alert level enumeration
/// </summary>
public enum AlertLevel
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
/// Interface for strategy services
/// </summary>
public interface IStrategyService
{
    Task<TradingSignalData?> GenerateSignalAsync(Symbol symbol, CancellationToken cancellationToken = default);
    Task InitializeAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
    bool IsInitialized { get; }
    string StrategyName { get; }
}

/// <summary>
/// Interface for risk management services
/// </summary>
public interface IRiskManagementService
{
    Task<PositionSizing> CalculatePositionSizeAsync(PositionSizeRequest request, CancellationToken cancellationToken = default);
    Task<RiskMetrics> GetCurrentRiskMetricsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsTradeAllowedAsync(TradingSignalData signal, CancellationToken cancellationToken = default);
    Task InitializeAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
    bool IsInitialized { get; }
}

/// <summary>
/// Interface for order execution services
/// </summary>
public interface IOrderExecutionService
{
    Task<string> PlaceOrderAsync(Symbol symbol, OrderSide side, OrderType type, Quantity quantity, Price? price = null, CancellationToken cancellationToken = default);
    Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);
    Task<OrderInfo> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for performance analytics services
/// </summary>
public interface IPerformanceAnalyticsService
{
    Task<PerformanceMetrics> GetPerformanceMetricsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trade>> GetTradesAsync(DateTime? from = null, DateTime? to = null, int? limit = null, CancellationToken cancellationToken = default);
    Task<decimal> GetCurrentDrawdownAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetWinRateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for trading hub services (SignalR)
/// </summary>
public interface ITradingHubService
{
    Task BroadcastSystemStatusAsync(string status, string message);
    Task BroadcastMarketDataAsync(MarketDataSnapshot snapshot);
    Task BroadcastTradeAsync(Trade trade);
    Task BroadcastPerformanceUpdateAsync(PerformanceMetrics metrics);
} 