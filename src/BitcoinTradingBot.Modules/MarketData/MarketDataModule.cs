using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BitcoinTradingBot.Core.Interfaces;

namespace BitcoinTradingBot.Modules.MarketData;

/// <summary>
/// MarketData module registration for dependency injection
/// </summary>
public static class MarketDataModule
{
    /// <summary>
    /// Registers MarketData module services
    /// </summary>
    public static IServiceCollection AddMarketDataModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Application Services
        services.AddScoped<IMarketDataService, Application.Services.MarketDataService>();
        
        // Infrastructure Services - Changed to Singleton to prevent disposal issues
        services.AddSingleton<Infrastructure.Exchanges.IBinanceDataProvider, Infrastructure.Exchanges.BinanceDataProvider>();
        
        // Background Services
        services.AddHostedService<Infrastructure.BackgroundServices.MarketDataBackgroundService>();
        
        // Memory Caching
        services.AddMemoryCache();
        
        return services;
    }
} 