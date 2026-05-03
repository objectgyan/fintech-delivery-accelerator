using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payment.Application.Providers;
using Payment.Domain.Repositories;
using Payment.Infrastructure.Data;
using Payment.Infrastructure.Providers;
using Payment.Infrastructure.Repositories;

namespace Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Payments")));

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentProvider, StripePaymentProvider>();

        return services;
    }
}
