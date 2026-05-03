using Microsoft.Extensions.Logging;
using Payment.Application.Events;

namespace Payment.Infrastructure.Messaging;

internal sealed class NullEventPublisher : IEventPublisher
{
    private readonly ILogger<NullEventPublisher> _logger;

    public NullEventPublisher(ILogger<NullEventPublisher> logger) => _logger = logger;

    public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogWarning("ServiceBus not configured — event {Type} dropped", typeof(T).Name);
        return Task.CompletedTask;
    }
}
