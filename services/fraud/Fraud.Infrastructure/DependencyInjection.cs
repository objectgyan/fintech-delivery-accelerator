using Fraud.Domain.Repositories;
using Fraud.Infrastructure.Data;
using Fraud.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fraud.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FraudDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("FraudDb")));

        services.AddScoped<IFraudDecisionRepository, FraudDecisionRepository>();

        return services;
    }
}
