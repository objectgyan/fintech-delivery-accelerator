using Microsoft.Extensions.DependencyInjection;

namespace Fraud.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
