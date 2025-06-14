using Microsoft.Extensions.Logging.Abstractions;
using BitcoinTradingBot.Web.Services;
using NSubstitute;
using Xunit;

namespace BitcoinTradingBot.UnitTests;

public class RealTimeMarketDataServiceBasicTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var logger = new NullLogger<RealTimeMarketDataService>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        // Act
        var service = new RealTimeMarketDataService(logger, serviceProvider);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task ExecuteAsync_StartsSuccessfully()
    {
        // Arrange
        var logger = new NullLogger<RealTimeMarketDataService>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var service = new RealTimeMarketDataService(logger, serviceProvider);
        
        using var cts = new CancellationTokenSource();
        
        // Act
        var executeTask = service.StartAsync(cts.Token);
        cts.CancelAfter(100); // Cancel after 100ms
        
        // Assert
        await executeTask;
        Assert.True(executeTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopAsync_StopsGracefully()
    {
        // Arrange
        var logger = new NullLogger<RealTimeMarketDataService>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var service = new RealTimeMarketDataService(logger, serviceProvider);
        
        // Act
        var startTask = service.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(startTask.IsCompletedSuccessfully);
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var logger = new NullLogger<RealTimeMarketDataService>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var service = new RealTimeMarketDataService(logger, serviceProvider);

        // Act & Assert - Should not throw
        service.Dispose();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new RealTimeMarketDataService(null!, serviceProvider));
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new NullLogger<RealTimeMarketDataService>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new RealTimeMarketDataService(logger, null!));
    }
} 