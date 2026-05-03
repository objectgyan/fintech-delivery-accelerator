using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Payment.Application.Events;

namespace Payment.Infrastructure.Messaging;

internal sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly string _topicName;
    private readonly ILogger<ServiceBusEventPublisher> _logger;

    public ServiceBusEventPublisher(
        ServiceBusClient client,
        string topicName,
        ILogger<ServiceBusEventPublisher> logger)
    {
        _client = client;
        _topicName = topicName;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class
    {
        var sender = _client.CreateSender(_topicName);
        await using var _ = sender.ConfigureAwait(false);

        var json = JsonSerializer.Serialize(@event, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json"
        };

        if (@event is PaymentAuthorizedEvent pae)
        {
            message.MessageId = pae.EventId.ToString();
            message.Subject = pae.EventType;
        }

        await sender.SendMessageAsync(message, cancellationToken);
        _logger.LogInformation("Published {EventType} to {Topic}", message.Subject, _topicName);
    }

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();
}
