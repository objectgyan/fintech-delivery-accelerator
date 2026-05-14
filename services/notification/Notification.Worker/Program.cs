using Azure.Messaging.ServiceBus;
using Fintech.Observability.Logging;
using Fintech.Observability.Telemetry;
using Notification.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddFintechJsonLogging("notification", builder.Configuration);
builder.Services.AddFintechTelemetry("notification", builder.Configuration);
builder.Services.Configure<ServiceBusOptions>(
    builder.Configuration.GetSection("ServiceBus"));

var sbConnectionString = builder.Configuration["ServiceBus:ConnectionString"];
if (string.IsNullOrWhiteSpace(sbConnectionString))
    throw new InvalidOperationException("ServiceBus:ConnectionString is required.");

builder.Services.AddSingleton(new ServiceBusClient(sbConnectionString));
builder.Services.AddHostedService<PaymentAuthorizedConsumer>();

var host = builder.Build();
host.Run();
