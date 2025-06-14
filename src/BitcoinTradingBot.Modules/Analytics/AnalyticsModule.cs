using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BitcoinTradingBot.Modules.Analytics;

/// <summary>
/// Analytics module registration for dependency injection
/// </summary>
public static class AnalyticsModule
{
    /// <summary>
    /// Registers all Analytics module services
    /// </summary>
    public static IServiceCollection AddAnalyticsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register MediatR handlers for this module
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AnalyticsModule).Assembly));

        // Module services will be added here as we implement them
        // services.AddScoped<IPerformanceAnalyzer, PerformanceAnalyzer>();
        // services.AddScoped<ITradeStatistics, TradeStatistics>();

        return services;
    }
} 