using Fraud.Application.Services;
using Fraud.Application.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fraud.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var fraudSettings = configuration.GetSection("Fraud").Get<FraudSettings>() ?? new FraudSettings();
        services.AddSingleton(fraudSettings);
        services.AddSingleton<FraudRuleEngine>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
