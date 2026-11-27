using Microsoft.Extensions.Options;

namespace Atya.Hosting.BackgroundServices;

internal sealed class PeriodicBackgroundServiceOptionsValidator : IValidateOptions<PeriodicBackgroundServiceOptions>
{
    private readonly string _name;

    public PeriodicBackgroundServiceOptionsValidator(string name)
    {
        _name = name;
    }

    public ValidateOptionsResult Validate(string? name, PeriodicBackgroundServiceOptions options)
    {
        if (!string.Equals(name, _name, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        var failures = new List<string>();
        if (options.Interval <= TimeSpan.Zero)
        {
            failures.Add($"{_name}: {nameof(options.Interval)} must be greater than zero.");
        }

        if (options.FailureDelay < TimeSpan.Zero)
        {
            failures.Add($"{_name}: {nameof(options.FailureDelay)} must not be negative.");
        }

        if (options.MaxConsecutiveFailures is < 1)
        {
            failures.Add($"{_name}: {nameof(options.MaxConsecutiveFailures)} must be null (never stop) or at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
