using SendGrid;
using SendGrid.Helpers.Mail;

namespace Notification.Worker.Email;

public sealed class SendGridEmailSender : IEmailSender
{
    private readonly ISendGridClient _client;
    private readonly SendGridOptions _options;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(ISendGridClient client, SendGridOptions options, ILogger<SendGridEmailSender> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var msg = new SendGridMessage
        {
            From = new EmailAddress(_options.FromEmail, _options.FromName),
            Subject = message.Subject,
            PlainTextContent = message.TextBody,
            HtmlContent = message.HtmlBody
        };
        msg.AddTo(new EmailAddress(message.To));

        var response = await _client.SendEmailAsync(msg, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Email sent to {To} — status={Status}", message.To, (int)response.StatusCode);
            return new EmailSendResult(true);
        }

        var body = await response.Body.ReadAsStringAsync(cancellationToken);
        // Truncate body — never log full SendGrid response in case of sensitive data
        var truncated = body.Length > 200 ? body[..200] : body;
        _logger.LogWarning("SendGrid rejected email to {To} — status={Status} body={Body}", message.To, (int)response.StatusCode, truncated);
        return new EmailSendResult(false, $"HTTP {(int)response.StatusCode}");
    }
}
