using Azure.Messaging.ServiceBus;
using Fintech.Observability.Logging;
using Fintech.Observability.Telemetry;
using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;
using Notification.Worker.Email;
using Notification.Worker.Workers;
using SendGrid;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddFintechJsonLogging("notification", builder.Configuration);
builder.Services.AddFintechTelemetry("notification", builder.Configuration);

builder.Services.AddApplicationInsightsTelemetryWorkerService();

// Service Bus
builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection("ServiceBus"));
var sbConnectionString = builder.Configuration["ServiceBus:ConnectionString"];
if (string.IsNullOrWhiteSpace(sbConnectionString))
    throw new InvalidOperationException("ServiceBus:ConnectionString is required.");
builder.Services.AddSingleton(new ServiceBusClient(sbConnectionString));

// Database
var notificationConnStr = builder.Configuration.GetConnectionString("Notifications");
if (string.IsNullOrWhiteSpace(notificationConnStr))
    throw new InvalidOperationException("ConnectionStrings:Notifications is required.");
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(notificationConnStr));

// Email sender
var sendGridApiKey = builder.Configuration["SendGrid:ApiKey"];
if (!string.IsNullOrWhiteSpace(sendGridApiKey))
{
    builder.Services.AddSingleton<ISendGridClient>(new SendGridClient(sendGridApiKey));
    var sgOptions = builder.Configuration.GetSection("SendGrid").Get<SendGridOptions>() ?? new SendGridOptions();
    builder.Services.AddSingleton(sgOptions);
    builder.Services.AddSingleton<IEmailSender, SendGridEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
}

builder.Services.AddHostedService<PaymentAuthorizedConsumer>();

var host = builder.Build();

// Auto-migrate on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
