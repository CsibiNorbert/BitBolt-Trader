using BitcoinTradingBot.Modules.Risk.Application.Services;
using BitcoinTradingBot.Modules.Risk.Domain.Interfaces;
using BitcoinTradingBot.Modules.Risk.Infrastructure.Calculations;
using BitcoinTradingBot.Modules.Risk.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace BitcoinTradingBot.Modules.Risk;

/// <summary>
/// Risk management module registration for dependency injection
/// </summary>
public static class RiskModule
{
    /// <summary>
    /// Register all Risk module services with the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddRiskModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register infrastructure implementations
        services.AddTransient<IPositionSizingCalculator, PositionSizingCalculator>();
        services.AddTransient<IOrderExecutionValidator, OrderExecutionValidator>();
        
        // Register application services
        services.AddTransient<IRiskManager, RiskService>();
        
        return services;
    }
} 