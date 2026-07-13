using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atya.Hosting.BackgroundServices.UnitTests.DependencyInjection;

public sealed class BackgroundServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAtyaBackgroundService_With_Null_Services_Throws()
    {
        IServiceCollection services = null!;

        Action action = () => services.AddAtyaBackgroundService<TestHostedService>();

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName(nameof(services));
    }

    [Fact]
    public void AddAtyaBackgroundService_Registers_HostedService()
    {
        var services = new ServiceCollection();

        services.AddAtyaBackgroundService<TestHostedService>();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetServices<IHostedService>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<TestHostedService>();
    }

    [Fact]
    public void AddAtyaBackgroundService_Applies_Options_Configuration()
    {
        var services = new ServiceCollection();

        services.AddAtyaBackgroundService<TestHostedService>(options => options.Interval = TimeSpan.FromSeconds(7));
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PeriodicBackgroundServiceOptions>>()
            .Value
            .Interval
            .Should()
            .Be(TimeSpan.FromSeconds(7));
    }

    private sealed class TestHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
