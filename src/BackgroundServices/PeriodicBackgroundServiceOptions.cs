namespace Atya.Hosting.BackgroundServices;

/// <summary>
/// Configures execution behavior for <see cref="PeriodicBackgroundService"/> and
/// <see cref="ScopedPeriodicBackgroundService"/>. This type is a binding surface only; each worker reads
/// its own named instance once, at construction.
/// </summary>
public sealed class PeriodicBackgroundServiceOptions
{
    /// <summary>
    /// Gets or sets the delay after a successful iteration. Must be greater than zero.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the delay after a failed iteration that will be retried. Must not be negative.
    /// </summary>
    public TimeSpan FailureDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the number of consecutive failures that are tolerated. Exactly this many consecutive
    /// failures are retried; the next one is rethrown to the host. <see langword="null"/> tolerates
    /// failures forever. Otherwise the value must be at least 1.
    /// </summary>
    public int? MaxConsecutiveFailures { get; set; } = 3;
}
