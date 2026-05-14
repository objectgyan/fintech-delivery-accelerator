namespace Notification.Worker.Email;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

public sealed record EmailSendResult(bool Success, string? ErrorMessage = null);
