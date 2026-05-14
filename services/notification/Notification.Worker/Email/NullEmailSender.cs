namespace Notification.Worker.Email;

public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger) => _logger = logger;

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("NullEmailSender — would send to {To}: {Subject}", message.To, message.Subject);
        return Task.FromResult(new EmailSendResult(true));
    }
}
