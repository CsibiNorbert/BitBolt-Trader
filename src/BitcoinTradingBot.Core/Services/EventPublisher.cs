using BitcoinTradingBot.Core.Events;
using BitcoinTradingBot.Core.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Core.Services;

/// <summary>
/// Event publisher implementation using MediatR
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IMediator mediator, ILogger<EventPublisher> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Publishes a domain event using MediatR
    /// </summary>
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : DomainEvent
    {
        try
        {
            _logger.LogDebug("Publishing event: {EventType} with ID: {EventId}", 
                typeof(T).Name, @event.EventId);

            await _mediator.Publish(@event, cancellationToken);

            _logger.LogTrace("Successfully published event: {EventType} with ID: {EventId}", 
                typeof(T).Name, @event.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event: {EventType} with ID: {EventId}", 
                typeof(T).Name, @event.EventId);
            throw;
        }
    }
} 