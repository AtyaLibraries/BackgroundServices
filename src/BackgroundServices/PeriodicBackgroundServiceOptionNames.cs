namespace Atya.Hosting.BackgroundServices;

/// <summary>
/// Provides the option name under which a worker's <see cref="PeriodicBackgroundServiceOptions"/> are registered.
/// </summary>
public static class PeriodicBackgroundServiceOptionNames
{
    /// <summary>
    /// Gets the option name for a worker type, which is the full name of <typeparamref name="TWorker"/>.
    /// </summary>
    /// <typeparam name="TWorker">The worker type.</typeparam>
    /// <returns>The named-options key.</returns>
    public static string For<TWorker>() => For(typeof(TWorker));

    internal static string For(Type workerType) => workerType.FullName ?? workerType.Name;
}
