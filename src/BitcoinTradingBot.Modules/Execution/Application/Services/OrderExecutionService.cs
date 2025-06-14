using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Execution.Application.Services;

/// <summary>
/// Service for executing orders with proper risk management and error handling
/// </summary>
public class OrderExecutionService : IOrderExecutionService
{
    private readonly ILogger<OrderExecutionService> _logger;
    private readonly Dictionary<string, OrderInfo> _orders = new();
    private readonly List<Position> _positions = new();

    public OrderExecutionService(ILogger<OrderExecutionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> PlaceOrderAsync(Symbol symbol, OrderSide side, OrderType type, Quantity quantity, Price? price = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var orderId = Guid.NewGuid().ToString();
            var order = new OrderInfo(
                orderId,
                symbol,
                side,
                type,
                quantity,
                Quantity.Zero(),
                price,
                OrderStatus.New,
                DateTime.UtcNow,
                null
            );

            _orders[orderId] = order;

            _logger.LogInformation("Order placed: {OrderId} - {Symbol} {Side} {Quantity} @ {Price}",
                orderId, symbol, side, quantity, price);

            // Simulate order execution (in real implementation, this would interact with exchange)
            await Task.Delay(100, cancellationToken); // Simulate network delay

            // For demo purposes, mark order as filled
            var filledOrder = order with 
            { 
                Status = OrderStatus.Filled, 
                ExecutedQuantity = quantity, 
                UpdatedAt = DateTime.UtcNow 
            };
            _orders[orderId] = filledOrder;

            // Create position
            var position = new Position(
                Guid.NewGuid().ToString(),
                symbol,
                side,
                quantity,
                price ?? Price.Create(50000m), // Default price for demo
                null,
                null,
                DateTime.UtcNow,
                null,
                0m,
                0m
            );
            _positions.Add(position);

            return orderId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place order: {Symbol} {Side} {Quantity}", symbol, side, quantity);
            throw;
        }
    }

    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_orders.TryGetValue(orderId, out var order))
            {
                _logger.LogWarning("Order not found for cancellation: {OrderId}", orderId);
                return false;
            }

            if (order.Status != OrderStatus.New && order.Status != OrderStatus.PartiallyFilled)
            {
                _logger.LogWarning("Cannot cancel order in status {Status}: {OrderId}", order.Status, orderId);
                return false;
            }

            var cancelledOrder = order with 
            { 
                Status = OrderStatus.Canceled, 
                UpdatedAt = DateTime.UtcNow 
            };
            _orders[orderId] = cancelledOrder;

            _logger.LogInformation("Order cancelled: {OrderId}", orderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order: {OrderId}", orderId);
            return false;
        }
    }

    public async Task<OrderInfo> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_orders.TryGetValue(orderId, out var order))
            {
                return order;
            }

            throw new InvalidOperationException($"Order not found: {orderId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order status: {OrderId}", orderId);
            throw;
        }
    }

    public async Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return _positions.Where(p => p.IsOpen).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get open positions");
            throw;
        }
    }
} 