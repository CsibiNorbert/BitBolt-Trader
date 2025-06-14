using MediatR;
using BitcoinTradingBot.Core.Models;

namespace BitcoinTradingBot.Core.Events;

/// <summary>
/// Base class for all domain events in the system
/// </summary>
public abstract record DomainEvent(DateTime OccurredAt = default) : INotification
{
    public DateTime OccurredAt { get; } = OccurredAt == default ? DateTime.UtcNow : OccurredAt;
    public Guid EventId { get; } = Guid.NewGuid();
}

/// <summary>
/// Base class for integration events that cross module boundaries
/// </summary>
public abstract record IntegrationEvent(DateTime OccurredAt = default) : DomainEvent(OccurredAt);

/// <summary>
/// Event published when a new market data candle is received
/// </summary>
public record NewCandleReceivedEvent(
    Symbol Symbol,
    TimeFrame TimeFrame,
    DateTime OpenTime,
    Price Open,
    Price High,
    Price Low,
    Price Close,
    Quantity Volume
) : IntegrationEvent;

/// <summary>
/// Event published when a candle is received - alias for background service compatibility
/// </summary>
public record CandleReceivedEvent(
    Symbol Symbol,
    TimeFrame TimeFrame,
    Candle Candle
) : IntegrationEvent
{
    /// <summary>
    /// Creates a CandleReceivedEvent from a candle object
    /// </summary>
    public static CandleReceivedEvent FromCandle(Candle candle) =>
        new(candle.Symbol, candle.TimeFrame, candle);
}

/// <summary>
/// Event published when a trading signal is generated
/// </summary>
public record TradingSignalGeneratedEvent(
    Symbol Symbol,
    TradingSignal Signal,
    Price EntryPrice,
    Price? StopLoss,
    Price? TakeProfit,
    decimal Confidence,
    Dictionary<string, object> Metadata
) : IntegrationEvent;

/// <summary>
/// Event published when an order is placed
/// </summary>
public record OrderPlacedEvent(
    string OrderId,
    Symbol Symbol,
    OrderSide Side,
    OrderType Type,
    Quantity Quantity,
    Price? Price,
    DateTime PlacedAt
) : IntegrationEvent;

/// <summary>
/// Event published when an order is executed/filled
/// </summary>
public record OrderExecutedEvent(
    string OrderId,
    Symbol Symbol,
    OrderSide Side,
    Quantity ExecutedQuantity,
    Price ExecutedPrice,
    DateTime ExecutedAt,
    decimal Fee
) : IntegrationEvent;

/// <summary>
/// Event published when a position is opened
/// </summary>
public record PositionOpenedEvent(
    string PositionId,
    Symbol Symbol,
    OrderSide Side,
    Quantity Size,
    Price EntryPrice,
    Price? StopLoss,
    Price? TakeProfit,
    DateTime OpenedAt
) : IntegrationEvent;

/// <summary>
/// Event published when a position is closed
/// </summary>
public record PositionClosedEvent(
    string PositionId,
    Symbol Symbol,
    Price ExitPrice,
    decimal PnL,
    DateTime ClosedAt,
    string Reason
) : IntegrationEvent;

/// <summary>
/// Event published when system health status changes
/// </summary>
public record SystemHealthChangedEvent(
    string Component,
    bool IsHealthy,
    string? Message,
    Dictionary<string, object>? Metrics
) : IntegrationEvent;

/// <summary>
/// Event published when risk limits are breached
/// </summary>
public record RiskLimitBreachedEvent(
    string LimitType,
    decimal CurrentValue,
    decimal LimitValue,
    Symbol? Symbol,
    string Description
) : IntegrationEvent; 