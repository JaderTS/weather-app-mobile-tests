using Microsoft.Extensions.Configuration;

namespace WeatherApp.MobileTests.Config;

/// <summary>
/// Loads test settings from appsettings.json, then layers appsettings.local.json
/// (git-ignored, for machine-specific overrides such as a different AVD name or the
/// test account password) and finally environment variables, so CI can override any
/// value without touching a file. Keeping this in one place is what "configuration
/// outside the classes" means in practice: no page object or test ever hard-codes a
/// timeout, device name, or path.
/// </summary>
public static class ConfigurationProvider
{
    private static readonly Lazy<TestSettings> LazySettings = new(Build);

    public static TestSettings Settings => LazySettings.Value;

    /// <summary>
    /// Repository root, resolved by walking up from the test assembly's output folder
    /// until a directory containing the solution file is found. Used to turn the
    /// configured relative APK path into an absolute one regardless of which machine
    /// or working directory the tests run from.
    /// </summary>
    public static string RepoRoot { get; } = FindRepoRoot(AppContext.BaseDirectory);

    private static TestSettings Build()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "WEATHERAPP_")
            .Build();

        var settings = new TestSettings();
        configuration.Bind(settings);
        return settings;
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            if (current.GetFiles("WeatherApp.MobileTests.sln").Length > 0)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate WeatherApp.MobileTests.sln above '{startDirectory}'. " +
            "The repo root is required to resolve the APK path.");
    }
}
