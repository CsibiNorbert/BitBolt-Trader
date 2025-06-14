using BitcoinTradingBot.Modules.Strategy.Application.Services;
using BitcoinTradingBot.Modules.Strategy.Domain.Indicators;
using BitcoinTradingBot.Modules.Strategy.Domain.Strategies;
using BitcoinTradingBot.Modules.Strategy.Infrastructure.Calculations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BitcoinTradingBot.Modules.Strategy;

/// <summary>
/// Strategy module registration for dependency injection
/// </summary>
public static class StrategyModule
{
    /// <summary>
    /// Registers all Strategy module services
    /// </summary>
    public static IServiceCollection AddStrategyModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register indicator calculators
        services.AddScoped<IKeltnerChannelCalculator, KeltnerChannelCalculator>();
        services.AddScoped<IExponentialMovingAverageCalculator, ExponentialMovingAverageCalculator>();
        services.AddScoped<IAverageTrueRangeCalculator, AverageTrueRangeCalculator>();

        // Register strategy implementations
        services.AddScoped<IMultiTimeframeKeltnerStrategy, MultiTimeframeKeltnerStrategy>();

        // Register application services
        services.AddScoped<StrategyService>();

        // Register MediatR handlers (StrategyService handles NewCandleReceivedEvent)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(StrategyModule).Assembly));

        return services;
    }

    /// <summary>
    /// Configures Strategy module specific settings
    /// </summary>
    public static IServiceCollection ConfigureStrategySettings(this IServiceCollection services, IConfiguration configuration)
    {
        // Strategy configuration can be added here
        // For example: services.Configure<StrategySettings>(configuration.GetSection("Strategy"));
        
        return services;
    }
} 