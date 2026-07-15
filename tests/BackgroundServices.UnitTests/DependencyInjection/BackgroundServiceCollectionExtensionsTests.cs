using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atya.Hosting.BackgroundServices.UnitTests.DependencyInjection;

public sealed class BackgroundServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAtyaBackgroundService_With_Null_Services_Throws()
    {
        IServiceCollection services = null!;

        Action action = () => services.AddAtyaBackgroundService<TestPeriodicHostedService>();

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName(nameof(services));
    }

    [Fact]
    public void AddAtyaBackgroundService_Registers_HostedService()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILogger<TestPeriodicHostedService>>(NullLogger<TestPeriodicHostedService>.Instance);
        services.AddAtyaBackgroundService<TestPeriodicHostedService>();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetServices<IHostedService>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<TestPeriodicHostedService>();
    }

    [Fact]
    public void AddAtyaBackgroundService_Applies_Options_Configuration()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILogger<TestPeriodicHostedService>>(NullLogger<TestPeriodicHostedService>.Instance);
        services.AddAtyaBackgroundService<TestPeriodicHostedService>(options => options.Interval = TimeSpan.FromSeconds(7));
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PeriodicBackgroundServiceOptions>>()
            .Value
            .Interval
            .Should()
            .Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void AddAtyaScopedBackgroundService_Registers_HostedService()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILogger<TestScopedPeriodicHostedService>>(NullLogger<TestScopedPeriodicHostedService>.Instance);
        services.AddAtyaScopedBackgroundService<TestScopedPeriodicHostedService>();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetServices<IHostedService>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<TestScopedPeriodicHostedService>();
    }

    [Fact]
    public void AddAtyaScopedBackgroundService_Applies_Options_Configuration()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILogger<TestScopedPeriodicHostedService>>(NullLogger<TestScopedPeriodicHostedService>.Instance);
        services.AddAtyaScopedBackgroundService<TestScopedPeriodicHostedService>(options => options.FailureDelay = TimeSpan.FromSeconds(9));
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PeriodicBackgroundServiceOptions>>()
            .Value
            .FailureDelay
            .Should()
            .Be(TimeSpan.FromSeconds(9));
    }

    private sealed class TestPeriodicHostedService : PeriodicBackgroundService
    {
        public TestPeriodicHostedService(ILogger<TestPeriodicHostedService> logger)
            : base(logger)
        {
        }

        protected override Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestScopedPeriodicHostedService : ScopedPeriodicBackgroundService
    {
        public TestScopedPeriodicHostedService(
            ILogger<TestScopedPeriodicHostedService> logger,
            IServiceScopeFactory scopeFactory)
            : base(logger, scopeFactory)
        {
        }

        protected override Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
