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
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddHttpClient<INaturalLanguageProcessor, LlmNaturalLanguageProcessor>();
        services.AddScoped<IValidationEngine, ValidationEngine>();
        services.AddScoped<ISqlBuilder, PostgresSqlBuilder>();
        services.AddScoped<IQueryExecutionService, QueryExecutionService>();
        services.AddScoped<IDatabaseSchemaService, DatabaseSchemaService>();
        services.AddScoped<IQueryHistoryService, QueryHistoryService>();
        services.AddScoped<IQueryOrchestratorService, QueryOrchestratorService>();

        return services;
    }
}