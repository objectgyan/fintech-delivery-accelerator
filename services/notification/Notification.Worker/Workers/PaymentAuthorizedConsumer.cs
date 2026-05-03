using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Notification.Worker.Events;

namespace Notification.Worker.Workers;

public sealed class PaymentAuthorizedConsumer : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusOptions _options;
    private readonly ILogger<PaymentAuthorizedConsumer> _logger;

    public PaymentAuthorizedConsumer(
        ServiceBusClient client,
        IOptions<ServiceBusOptions> options,
        ILogger<PaymentAuthorizedConsumer> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processor = _client.CreateProcessor(
            _options.TopicName,
            _options.SubscriptionName,
            new ServiceBusProcessorOptions { MaxConcurrentCalls = 1 });

        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += HandleErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation(
            "Notification consumer started — topic={Topic} subscription={Sub}",
            _options.TopicName, _options.SubscriptionName);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        await processor.StopProcessingAsync();
    }

    private Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var evt = JsonSerializer.Deserialize<PaymentAuthorizedEvent>(
                args.Message.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (evt is not null)
            {
                _logger.LogInformation(
                    "PaymentAuthorized received — eventId={EventId} paymentId={PaymentId} amount={Amount} {Currency} email={Email}",
                    evt.EventId, evt.PaymentId, evt.Amount, evt.Currency, evt.CustomerEmail);
            }
            else
            {
                _logger.LogWarning("Received null event — messageId={MessageId}", args.Message.MessageId);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message — messageId={MessageId}", args.Message.MessageId);
        }

        return args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus error — source={Source} entity={Entity}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }
}
