namespace Notification.Worker.Workers;

public sealed class ServiceBusOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string TopicName { get; set; } = "payment-authorized";
    public string SubscriptionName { get; set; } = "notification";
}
