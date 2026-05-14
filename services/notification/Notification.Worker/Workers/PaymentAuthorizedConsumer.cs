using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Worker.Data;
using Notification.Worker.Email;
using Notification.Worker.Events;

namespace Notification.Worker.Workers;

public sealed class PaymentAuthorizedConsumer : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusOptions _options;
    private readonly IEmailSender _emailSender;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentAuthorizedConsumer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PaymentAuthorizedConsumer(
        ServiceBusClient client,
        IOptions<ServiceBusOptions> options,
        IEmailSender emailSender,
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentAuthorizedConsumer> logger)
    {
        _client = client;
        _options = options.Value;
        _emailSender = emailSender;
        _scopeFactory = scopeFactory;
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

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        PaymentAuthorizedEvent? evt = null;
        try
        {
            evt = JsonSerializer.Deserialize<PaymentAuthorizedEvent>(args.Message.Body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message — messageId={MessageId}", args.Message.MessageId);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        if (evt is null)
        {
            _logger.LogWarning("Received null event — messageId={MessageId}", args.Message.MessageId);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        _logger.LogInformation(
            "PaymentAuthorized received — eventId={EventId} paymentId={PaymentId} amount={Amount} {Currency} email={Email}",
            evt.EventId, evt.PaymentId, evt.Amount, evt.Currency, evt.CustomerEmail);

        await SendEmailNotificationAsync(evt, args.CancellationToken);

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    private async Task SendEmailNotificationAsync(PaymentAuthorizedEvent evt, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        // Idempotency guard — skip if already sent for this event+channel
        var alreadySent = await db.NotificationLogs
            .AnyAsync(x => x.EventId == evt.EventId && x.Channel == "email", cancellationToken);
        if (alreadySent)
        {
            _logger.LogInformation("Skipping duplicate email — eventId={EventId}", evt.EventId);
            return;
        }

        var message = BuildEmail(evt);
        var result = await _emailSender.SendAsync(message, cancellationToken);

        var log = result.Success
            ? NotificationLog.Success(evt.EventId, evt.PaymentId, "email", evt.CustomerEmail)
            : NotificationLog.Failure(evt.EventId, evt.PaymentId, "email", evt.CustomerEmail, result.ErrorMessage ?? "unknown");

        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        if (!result.Success)
            _logger.LogWarning("Email delivery failed — eventId={EventId} error={Error}", evt.EventId, result.ErrorMessage);
    }

    private static EmailMessage BuildEmail(PaymentAuthorizedEvent evt)
    {
        var amountFormatted = $"{evt.Amount:F2} {evt.Currency.ToUpperInvariant()}";
        var subject = $"Payment of {amountFormatted} authorised";
        var text = $"Your payment of {amountFormatted} has been authorised. Reference: {evt.PaymentId}";
        var html = $"""
            <p>Your payment of <strong>{amountFormatted}</strong> has been authorised.</p>
            <p>Reference: <code>{evt.PaymentId}</code></p>
            """;
        return new EmailMessage(evt.CustomerEmail, subject, text, html);
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
