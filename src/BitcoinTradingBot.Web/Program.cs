using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using BitcoinTradingBot.Web.Data;
using BitcoinTradingBot.Web.Hubs;
using BitcoinTradingBot.Web.Services;
using BitcoinTradingBot.Core;
using BitcoinTradingBot.Core.Interfaces;
using BitcoinTradingBot.Core.Models;
using BitcoinTradingBot.Modules.MarketData;
using BitcoinTradingBot.Modules.Strategy;
using BitcoinTradingBot.Modules.Strategy.Application.Services;
using BitcoinTradingBot.Modules.Risk;
using BitcoinTradingBot.Modules.Risk.Application.Services;
using BitcoinTradingBot.Modules.Execution;
using BitcoinTradingBot.Modules.Execution.Application.Services;
using BitcoinTradingBot.Modules.Analytics;
using BitcoinTradingBot.Modules.Analytics.Application.Services;
using BitcoinTradingBot.Modules.Notifications;
using MediatR;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with enhanced configuration
builder.Host.UseSerilog((context, config) =>
{
    config.WriteTo.Console()
          .WriteTo.File("logs/trading-bot-.txt", rollingInterval: RollingInterval.Day)
          .MinimumLevel.Information()
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Application", "BitcoinTradingBot")
          .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);
});

// Add Blazor services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

// Add API controllers for dashboard endpoints
builder.Services.AddControllers();

// Add legacy service temporarily (will be removed when dashboard is implemented)
builder.Services.AddSingleton<WeatherForecastService>();

// Add core infrastructure services
builder.Services.AddScoped<BitcoinTradingBot.Core.Interfaces.IEventPublisher, BitcoinTradingBot.Core.Services.EventPublisher>();

// Add SignalR services for real-time dashboard updates
builder.Services.AddScoped<BitcoinTradingBot.Core.Interfaces.ITradingHubService, TradingHubService>();

// Add new Phase 3 services for enhanced dashboard functionality
builder.Services.AddHostedService<RealTimeMarketDataService>();
builder.Services.AddScoped<ITradingBotControlService, TradingBotControlService>();

// Add module-specific services with proper interfaces
builder.Services.AddScoped<IStrategyService, StrategyService>();
builder.Services.AddScoped<IRiskManagementService, RiskManagementService>();
builder.Services.AddScoped<IOrderExecutionService, OrderExecutionService>();
builder.Services.AddScoped<IPerformanceAnalyticsService, PerformanceAnalyticsService>();

// Add HttpClient for exchange integrations
builder.Services.AddHttpClient();

// Add HttpClient for Blazor components
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.Configuration["BaseUrl"] ?? "https://localhost:5001/") });

// Add MediatR for inter-module communication
builder.Services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssemblyContaining<BitcoinTradingBot.Core.Events.NewCandleReceivedEvent>();
});

// Register all modules using modular architecture pattern
builder.Services.AddMarketDataModule(builder.Configuration);
builder.Services.AddStrategyModule(builder.Configuration);
builder.Services.AddRiskModule(builder.Configuration);
builder.Services.AddExecutionModule(builder.Configuration);
builder.Services.AddAnalyticsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

// Add configuration binding for trading parameters
builder.Services.Configure<TradingConfiguration>(builder.Configuration.GetSection("Trading"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Configure SignalR hubs for real-time updates
app.MapBlazorHub();
app.MapHub<TradingHub>("/tradinghub");

// Map API controllers
app.MapControllers();

app.MapFallbackToPage("/_Host");

try 
{
    Log.Information("Starting Bitcoin Trading Bot - {Environment}", app.Environment.EnvironmentName);
    Log.Information("Modules registered successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
