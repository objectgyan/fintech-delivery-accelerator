using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payment.Domain.Repositories;

namespace Payment.Infrastructure.Messaging;

internal sealed class OutboxRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceBusClient _client;
    private readonly string _topicName;
    private readonly ILogger<OutboxRelayService> _logger;

    private const int BatchSize = 10;
    private const int MaxRetries = 5;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public OutboxRelayService(
        IServiceScopeFactory scopeFactory,
        ServiceBusClient client,
        IConfiguration configuration,
        ILogger<OutboxRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _client = client;
        _topicName = configuration["ServiceBus:TopicName"] ?? "payment-authorized";
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxRelay started — polling every {Interval}s", PollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxRelay: unhandled error in relay loop");
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OutboxRelay stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

        var messages = await repository.GetPendingOutboxMessagesAsync(BatchSize, cancellationToken);
        if (messages.Count == 0) return;

        _logger.LogInformation("OutboxRelay: processing {Count} pending message(s)", messages.Count);

        var sender = _client.CreateSender(_topicName);
        await using var _ = sender.ConfigureAwait(false);

        foreach (var outboxMessage in messages)
        {
            try
            {
                var sbMessage = new ServiceBusMessage(outboxMessage.PayloadJson)
                {
                    ContentType = "application/json",
                    MessageId = outboxMessage.Id.ToString(),
                    Subject = outboxMessage.EventType
                };

                await sender.SendMessageAsync(sbMessage, cancellationToken);
                outboxMessage.MarkPublished();
                _logger.LogInformation("OutboxRelay: published {EventType} {MessageId}", outboxMessage.EventType, outboxMessage.Id);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                outboxMessage.IncrementRetry();
                if (outboxMessage.RetryCount >= MaxRetries)
                {
                    outboxMessage.MarkFailed();
                    _logger.LogError(ex, "OutboxRelay: message {MessageId} failed permanently after {MaxRetries} retries", outboxMessage.Id, MaxRetries);
                }
                else
                {
                    _logger.LogWarning(ex, "OutboxRelay: message {MessageId} retry {RetryCount}/{MaxRetries}", outboxMessage.Id, outboxMessage.RetryCount, MaxRetries);
                }
            }

            await repository.UpdateOutboxMessageAsync(outboxMessage, cancellationToken);
        }
    }
}
