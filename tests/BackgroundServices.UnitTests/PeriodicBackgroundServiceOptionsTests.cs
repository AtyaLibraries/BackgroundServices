using Microsoft.Extensions.Logging.Abstractions;

namespace Atya.Hosting.BackgroundServices.UnitTests;

public sealed class PeriodicBackgroundServiceOptionsTests
{
    [Fact]
    public void Default_Returns_Valid_Options()
    {
        PeriodicBackgroundServiceOptions options = PeriodicBackgroundServiceOptions.Default;

        options.Interval.Should().Be(TimeSpan.FromMinutes(1));
        options.FailureDelay.Should().Be(TimeSpan.FromSeconds(5));
        options.MaxConsecutiveFailures.Should().Be(3);
    }

    [Fact]
    public void Validate_With_Negative_Interval_Throws()
    {
        var options = new PeriodicBackgroundServiceOptions
        {
            Interval = TimeSpan.FromMilliseconds(-1),
        };

        Action action = () => _ = new OptionsProbe(options);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(PeriodicBackgroundServiceOptions.Interval));
    }

    [Fact]
    public void Validate_With_Negative_FailureDelay_Throws()
    {
        var options = new PeriodicBackgroundServiceOptions
        {
            FailureDelay = TimeSpan.FromMilliseconds(-1),
        };

        Action action = () => _ = new OptionsProbe(options);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(PeriodicBackgroundServiceOptions.FailureDelay));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_With_NonPositive_MaxConsecutiveFailures_Throws(int maxConsecutiveFailures)
    {
        var options = new PeriodicBackgroundServiceOptions
        {
            MaxConsecutiveFailures = maxConsecutiveFailures,
        };

        Action action = () => _ = new OptionsProbe(options);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(PeriodicBackgroundServiceOptions.MaxConsecutiveFailures));
    }

    private sealed class OptionsProbe : PeriodicBackgroundService
    {
        public OptionsProbe(PeriodicBackgroundServiceOptions options)
            : base(NullLogger.Instance, options)
        {
        }

        protected override Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
