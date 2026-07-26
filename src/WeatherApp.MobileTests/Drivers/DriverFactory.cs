using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using Serilog;
using WeatherApp.MobileTests.Config;

namespace WeatherApp.MobileTests.Drivers;

/// <summary>
/// Builds the AndroidDriver session from configuration. Nothing about the device,
/// app path, or capabilities is hard-coded in a test or page object - it all flows
/// from TestSettings so switching emulator/device or APK location is a config change,
/// not a code change. That includes switching targets entirely: Appium:UseBrowserStack
/// swaps the local-emulator path below for a BrowserStack App Automate session, used
/// by the scheduled CI workflow so a recurring run doesn't need a local emulator kept
/// alive indefinitely.
/// </summary>
public static class DriverFactory
{
    /// <summary>
    /// Computed once when this type is first touched (i.e. once per `dotnet test`
    /// process), not per session - so every test in the same run shares one BrowserStack
    /// "build" instead of getting split across a different build per minute (each test
    /// opens its own driver/session, and a per-call timestamp would otherwise change
    /// build-to-build mid-run).
    /// </summary>
    private static readonly string BrowserStackBuildName =
        $"Weather App Mobile Tests - {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC";

    public static AndroidDriver CreateAndroidDriver()
    {
        var settings = ConfigurationProvider.Settings.Appium;

        return settings.UseBrowserStack
            ? CreateBrowserStackDriver(settings)
            : CreateLocalDriver(settings);
    }

    private static AndroidDriver CreateLocalDriver(AppiumSettings settings)
    {
        var apkPath = ResolveAppPath(settings.AppPath);

        var options = new AppiumOptions
        {
            PlatformName = settings.PlatformName,
            AutomationName = settings.AutomationName,
            DeviceName = settings.DeviceName,
            App = apkPath,
        };

        if (!string.IsNullOrWhiteSpace(settings.PlatformVersion))
        {
            options.PlatformVersion = settings.PlatformVersion;
        }

        options.AddAdditionalAppiumOption("appium:noReset", settings.NoReset);
        options.AddAdditionalAppiumOption("appium:autoGrantPermissions", settings.AutoGrantPermissions);

        Log.Information(
            "Creating local AndroidDriver session (device={Device}, app={App}, server={Server})",
            settings.DeviceName, apkPath, settings.ServerUrl);

        var driver = new AndroidDriver(new Uri(settings.ServerUrl), options, TimeSpan.FromMinutes(3));
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;

        return driver;
    }

    private static AndroidDriver CreateBrowserStackDriver(AppiumSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BrowserStackUsername) ||
            string.IsNullOrWhiteSpace(settings.BrowserStackAccessKey) ||
            string.IsNullOrWhiteSpace(settings.BrowserStackAppUrl))
        {
            throw new InvalidOperationException(
                "Appium:UseBrowserStack is true but BrowserStackUsername/BrowserStackAccessKey/BrowserStackAppUrl " +
                "are not set. These must come from environment variables (WEATHERAPP_Appium__BrowserStack...), " +
                "never appsettings.json.");
        }

        var options = new AppiumOptions
        {
            PlatformName = settings.PlatformName,
            AutomationName = settings.AutomationName,
            App = settings.BrowserStackAppUrl,
        };

        options.AddAdditionalAppiumOption("bstack:options", new Dictionary<string, object>
        {
            ["userName"] = settings.BrowserStackUsername,
            ["accessKey"] = settings.BrowserStackAccessKey,
            ["deviceName"] = settings.BrowserStackDeviceName,
            ["osVersion"] = settings.BrowserStackOsVersion,
            ["projectName"] = "Weather App Mobile Tests",
            ["buildName"] = BrowserStackBuildName,
            ["sessionName"] = TestContext.CurrentContext?.Test?.Name ?? "Weather App test",
        });

        Log.Information(
            "Creating BrowserStack AndroidDriver session (device={Device}, osVersion={OsVersion})",
            settings.BrowserStackDeviceName, settings.BrowserStackOsVersion);

        var driver = new AndroidDriver(new Uri(settings.BrowserStackServerUrl), options, TimeSpan.FromMinutes(3));
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;

        return driver;
    }

    private static string ResolveAppPath(string configuredPath)
    {
        var path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(ConfigurationProvider.RepoRoot, configuredPath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Configured APK path does not exist: '{path}'. Check Appium:AppPath in appsettings.json.", path);
        }

        return path;
    }
}
