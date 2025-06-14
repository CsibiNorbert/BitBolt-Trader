using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BitcoinTradingBot.Modules.Notifications;

/// <summary>
/// Notifications module registration for dependency injection
/// </summary>
public static class NotificationsModule
{
    /// <summary>
    /// Registers all Notifications module services
    /// </summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register MediatR handlers for this module
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(NotificationsModule).Assembly));

        // Module services will be added here as we implement them
        // services.AddScoped<ITelegramNotifier, TelegramNotifier>();
        // services.AddScoped<IEmailNotifier, EmailNotifier>();
        // services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
} 