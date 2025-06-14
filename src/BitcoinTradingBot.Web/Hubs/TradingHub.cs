using Microsoft.AspNetCore.SignalR;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Core.Events;
using MediatR;

namespace BitcoinTradingBot.Web.Hubs;

/// <summary>
/// SignalR hub for real-time trading data updates
/// </summary>
public class TradingHub : Hub
{
    private readonly ILogger<TradingHub> _logger;

    public TradingHub(ILogger<TradingHub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        
        // Send initial data to the newly connected client
        await Clients.Caller.SendAsync("Connected", new { 
            ConnectionId = Context.ConnectionId,
            ConnectedAt = DateTime.UtcNow,
            Message = "Connected to Trading Hub"
        });
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, Exception: {Exception}", 
            Context.ConnectionId, exception?.Message);
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows clients to join a specific trading pair group
    /// </summary>
    public async Task JoinTradingPair(string symbol)
    {
        var groupName = $"trading_{symbol.ToUpper()}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogDebug("Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
        
        await Clients.Caller.SendAsync("JoinedGroup", new { 
            Symbol = symbol,
            GroupName = groupName 
        });
    }

    /// <summary>
    /// Allows clients to leave a specific trading pair group
    /// </summary>
    public async Task LeaveTradingPair(string symbol)
    {
        var groupName = $"trading_{symbol.ToUpper()}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogDebug("Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
        
        await Clients.Caller.SendAsync("LeftGroup", new { 
            Symbol = symbol,
            GroupName = groupName 
        });
    }

    /// <summary>
    /// Handles bot control commands from clients
    /// </summary>
    public async Task SendBotCommand(string command, object? parameters = null)
    {
        _logger.LogInformation("Bot command received from {ConnectionId}: {Command}", 
            Context.ConnectionId, command);

        // Validate command
        var validCommands = new[] { "start", "stop", "emergency_stop", "update_parameters" };
        if (!validCommands.Contains(command.ToLower()))
        {
            await Clients.Caller.SendAsync("CommandError", new { 
                Error = "Invalid command",
                Command = command 
            });
            return;
        }

        // Broadcast command to all clients for transparency
        await Clients.All.SendAsync("BotCommandReceived", new { 
            Command = command,
            Parameters = parameters,
            Timestamp = DateTime.UtcNow,
            Source = Context.ConnectionId
        });

        // Acknowledge command receipt
        await Clients.Caller.SendAsync("CommandAcknowledged", new { 
            Command = command,
            Status = "Received"
        });
    }

    /// <summary>
    /// Handles strategy parameter updates
    /// </summary>
    public async Task UpdateStrategyParameters(object parameters)
    {
        _logger.LogInformation("Strategy parameters update from {ConnectionId}: {Parameters}", 
            Context.ConnectionId, parameters);

        // Broadcast parameter update to all clients
        await Clients.All.SendAsync("StrategyParametersUpdated", new { 
            Parameters = parameters,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = Context.ConnectionId
        });
    }
}

/// <summary>
/// Additional service for broadcasting extended trading data to SignalR clients
/// This provides additional methods beyond the core ITradingHubService interface
/// </summary>
public interface IExtendedTradingHubService
{
    Task BroadcastPriceUpdateAsync(string symbol, decimal price, decimal change, decimal volume);
    Task BroadcastSignalAsync(string symbol, string signal, string reason, decimal confidence);
    Task BroadcastIndicatorUpdateAsync(string symbol, string timeframe, object indicators);
    Task BroadcastTradeExecutedAsync(string symbol, string side, decimal quantity, decimal price);
}

/// <summary>
/// Implementation of extended trading hub service for broadcasting updates
/// </summary>
public class ExtendedTradingHubService : IExtendedTradingHubService
{
    private readonly IHubContext<TradingHub> _hubContext;
    private readonly ILogger<ExtendedTradingHubService> _logger;

    public ExtendedTradingHubService(IHubContext<TradingHub> hubContext, ILogger<ExtendedTradingHubService> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Broadcasts price updates to all connected clients
    /// </summary>
    public async Task BroadcastPriceUpdateAsync(string symbol, decimal price, decimal change, decimal volume)
    {
        try
        {
            var priceData = new
            {
                Symbol = symbol,
                Price = price,
                Change = change,
                ChangePercent = change,
                Volume = volume,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("UpdatePrice", priceData);
            await _hubContext.Clients.Group($"trading_{symbol.ToUpper()}")
                .SendAsync("PriceUpdate", priceData);

            _logger.LogTrace("Broadcasted price update for {Symbol}: {Price}", symbol, price);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting price update for {Symbol}", symbol);
        }
    }

    /// <summary>
    /// Broadcasts trading signals to all connected clients
    /// </summary>
    public async Task BroadcastSignalAsync(string symbol, string signal, string reason, decimal confidence)
    {
        try
        {
            var signalData = new
            {
                Symbol = symbol,
                Signal = signal,
                Reason = reason,
                Confidence = confidence,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("UpdateSignal", signalData);
            await _hubContext.Clients.Group($"trading_{symbol.ToUpper()}")
                .SendAsync("SignalUpdate", signalData);

            _logger.LogInformation("Broadcasted signal for {Symbol}: {Signal} ({Confidence}%)", 
                symbol, signal, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting signal for {Symbol}", symbol);
        }
    }

    /// <summary>
    /// Broadcasts indicator updates to all connected clients
    /// </summary>
    public async Task BroadcastIndicatorUpdateAsync(string symbol, string timeframe, object indicators)
    {
        try
        {
            var indicatorData = new
            {
                Symbol = symbol,
                Timeframe = timeframe,
                Indicators = indicators,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("UpdateIndicators", indicatorData);
            await _hubContext.Clients.Group($"trading_{symbol.ToUpper()}")
                .SendAsync("IndicatorUpdate", indicatorData);

            _logger.LogTrace("Broadcasted indicator update for {Symbol} {Timeframe}", symbol, timeframe);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting indicators for {Symbol}", symbol);
        }
    }

    /// <summary>
    /// Broadcasts trade execution notifications
    /// </summary>
    public async Task BroadcastTradeExecutedAsync(string symbol, string side, decimal quantity, decimal price)
    {
        try
        {
            var tradeData = new
            {
                Symbol = symbol,
                Side = side,
                Quantity = quantity,
                Price = price,
                Value = quantity * price,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("TradeExecuted", tradeData);
            
            _logger.LogInformation("Broadcasted trade execution: {Side} {Quantity} {Symbol} @ {Price}", 
                side, quantity, symbol, price);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting trade execution for {Symbol}", symbol);
        }
    }


} 