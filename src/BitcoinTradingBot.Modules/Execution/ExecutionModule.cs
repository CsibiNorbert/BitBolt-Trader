using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BitcoinTradingBot.Modules.Execution;

/// <summary>
/// Execution module registration for dependency injection
/// </summary>
public static class ExecutionModule
{
    /// <summary>
    /// Registers all Execution module services
    /// </summary>
    public static IServiceCollection AddExecutionModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register MediatR handlers for this module
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ExecutionModule).Assembly));

        // Module services will be added here as we implement them
        // services.AddScoped<IOrderExecutor, OrderExecutor>();
        // services.AddScoped<ITradeManager, TradeManager>();

        return services;
    }
} 