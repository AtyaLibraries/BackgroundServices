using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Atya.Hosting.BackgroundServices.UnitTests;

public sealed class RegistrationTests
{
    [Fact]
    public void Defaults_Are_Documented_Values()
    {
        var options = new PeriodicBackgroundServiceOptions();

        options.Interval.Should().Be(TimeSpan.FromMinutes(1));
        options.FailureDelay.Should().Be(TimeSpan.FromSeconds(5));
        options.MaxConsecutiveFailures.Should().Be(3);
    }

    [Fact]
    public void Option_Name_Is_The_Worker_Full_Name()
    {
        PeriodicBackgroundServiceOptionNames.For<ScriptedWorker<TagA>>().Should().Be(typeof(ScriptedWorker<TagA>).FullName);
    }

    [Fact]
    public void Null_Services_Throw()
    {
        IServiceCollection services = null!;

        Action periodic = () => services.AddAtyaBackgroundService<ScriptedWorker<TagA>>();
        Action scoped = () => services.AddAtyaScopedBackgroundService<ScopedScriptedWorker<TagA>>();

        periodic.Should().Throw<ArgumentNullException>().WithParameterName(nameof(services));
        scoped.Should().Throw<ArgumentNullException>().WithParameterName(nameof(services));
    }

    [Fact]
    public void Registration_Adds_The_Hosted_Service_And_Default_TimeProvider()
    {
        var services = new ServiceCollection();

        services.AddAtyaBackgroundService<ScriptedWorker<TagA>>();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
        services.Should().Contain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void Registration_Does_Not_Replace_A_Registered_TimeProvider()
    {
        var fake = new ObservingTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(fake);

        services.AddAtyaScopedBackgroundService<ScopedScriptedWorker<TagA>>();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(fake);
    }

    [Fact]
    public void Configuration_Reaches_Only_The_Named_Worker()
    {
        var services = new ServiceCollection();
        services.AddAtyaBackgroundService<ScriptedWorker<TagA>>(o => o.Interval = TimeSpan.FromSeconds(7));
        services.AddAtyaBackgroundService<ScriptedWorker<TagB>>();
        using ServiceProvider provider = services.BuildServiceProvider();
        IOptionsMonitor<PeriodicBackgroundServiceOptions> monitor = provider.GetRequiredService<IOptionsMonitor<PeriodicBackgroundServiceOptions>>();

        monitor.Get(PeriodicBackgroundServiceOptionNames.For<ScriptedWorker<TagA>>()).Interval.Should().Be(TimeSpan.FromSeconds(7));
        monitor.Get(PeriodicBackgroundServiceOptionNames.For<ScriptedWorker<TagB>>()).Interval.Should().Be(TimeSpan.FromMinutes(1));
    }
}
