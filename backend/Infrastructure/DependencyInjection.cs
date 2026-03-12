using Application.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddHttpClient<INaturalLanguageProcessor, LlmNaturalLanguageProcessor>();
        services.AddScoped<IValidationEngine, ValidationEngine>();
        services.AddScoped<ISqlBuilder, PostgresSqlBuilder>();
        services.AddScoped<IQueryExecutionService, QueryExecutionService>();

        return services;
    }
}