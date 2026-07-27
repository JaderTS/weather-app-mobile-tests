using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using Serilog;
using WeatherApp.MobileTests.Config;

namespace WeatherApp.MobileTests.Drivers;

/// <summary>
/// Builds the AndroidDriver session from configuration. Nothing about the device,
/// app path, or capabilities is hard-coded in a test or page object - it all flows
/// from TestSettings so switching emulator/device or APK location is a config change,
/// not a code change.
/// </summary>
public static class DriverFactory
{
    public static AndroidDriver CreateAndroidDriver()
    {
        var settings = ConfigurationProvider.Settings.Appium;
        return CreateLocalDriver(settings);
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
