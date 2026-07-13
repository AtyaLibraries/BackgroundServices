using Atya.Foundation.Guards;

namespace Atya.Hosting.BackgroundServices;

/// <summary>
/// Configures execution behavior for <see cref="PeriodicBackgroundService"/>.
/// </summary>
public sealed class PeriodicBackgroundServiceOptions
{
    /// <summary>
    /// Gets the default options used when no explicit options are supplied.
    /// </summary>
    public static PeriodicBackgroundServiceOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the delay after a successful iteration.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the delay after a failed iteration that will be retried.
    /// </summary>
    public TimeSpan FailureDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the number of consecutive failures allowed before the exception is rethrown.
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 3;

    internal PeriodicBackgroundServiceOptions Validate()
    {
        if (Interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Interval), "Interval cannot be negative.");
        }

        if (FailureDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(FailureDelay), "Failure delay cannot be negative.");
        }

        MaxConsecutiveFailures = Guard.AgainstZeroOrNegative(MaxConsecutiveFailures);
        return this;
    }
}
