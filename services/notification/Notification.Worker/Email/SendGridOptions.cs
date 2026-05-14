namespace Notification.Worker.Email;

public sealed class SendGridOptions
{
    public string ApiKey { get; init; } = string.Empty;
    public string FromEmail { get; init; } = "noreply@fintech-demo.com";
    public string FromName { get; init; } = "FinTech Demo";
}
