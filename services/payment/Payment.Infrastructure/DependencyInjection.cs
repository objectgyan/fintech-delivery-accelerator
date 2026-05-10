using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Payment.Application.Events;
using Payment.Application.Providers;
using Payment.Domain.Repositories;
using Payment.Infrastructure.Data;
using Payment.Infrastructure.Messaging;
using Payment.Infrastructure.Providers;
using Payment.Infrastructure.Repositories;

namespace Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Payments")));

        services.AddScoped<IPaymentRepository, PaymentRepository>();

        if (isDevelopment && configuration.GetValue<bool>("Payment:SimulateTransientFailures"))
        {
            var failCount = configuration.GetValue<int>("Payment:SimulateFailCount", 2);
            services.AddScoped<IPaymentProvider>(sp => new SimulatedPaymentProvider(
                new StripePaymentProvider(configuration),
                sp.GetRequiredService<ILogger<SimulatedPaymentProvider>>(),
                failCount));
        }
        else
        {
            services.AddScoped<IPaymentProvider, StripePaymentProvider>();
        }

        var sbConnectionString = configuration["ServiceBus:ConnectionString"];
        var topicName = configuration["ServiceBus:TopicName"] ?? "payment-authorized";

        if (!string.IsNullOrWhiteSpace(sbConnectionString))
        {
            services.AddSingleton(new ServiceBusClient(sbConnectionString));
            services.AddScoped<IEventPublisher>(sp =>
                new ServiceBusEventPublisher(
                    sp.GetRequiredService<ServiceBusClient>(),
                    topicName,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ServiceBusEventPublisher>>()));
            services.AddHostedService<OutboxRelayService>();
        }
        else
        {
            services.AddScoped<IEventPublisher, NullEventPublisher>();
        }

        services.AddHostedService<OutboxCleanupService>();

        return services;
    }
}
