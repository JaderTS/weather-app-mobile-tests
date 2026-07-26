namespace WeatherApp.MobileTests.Config;

/// <summary>
/// Capabilities and server connection details for the Appium/UiAutomator2 session.
/// Bound from the "Appium" section of appsettings.json (see ConfigurationProvider).
/// </summary>
public sealed class AppiumSettings
{
    public string ServerUrl { get; set; } = "http://127.0.0.1:4723/";

    public bool ManageServerLifecycle { get; set; } = true;

    public int ServerStartupTimeoutSeconds { get; set; } = 30;

    public string PlatformName { get; set; } = "Android";

    public string AutomationName { get; set; } = "UiAutomator2";

    public string DeviceName { get; set; } = "Pixel_8_API_35";

    public string? PlatformVersion { get; set; }

    /// <summary>
    /// Path to the APK. Relative paths are resolved against the repository root
    /// (three levels up from the test assembly's output directory) so the same
    /// config works from any machine that clones the repo.
    /// </summary>
    public string AppPath { get; set; } = "apps/weather-app.apk";

    /// <summary>
    /// false by default: the app persists its logged-in session on disk, so with
    /// noReset:true a session left logged-in by one test (every test except Logout)
    /// causes the NEXT test's session to cold-launch straight past the Login screen -
    /// confirmed by hand, not a hypothetical. Resetting app state each session costs
    /// ~15-20s per test but guarantees every test starts from the same Login screen,
    /// which matters more here than raw speed. See README Limitations for the
    /// documented speed/determinism trade-off and how to make this faster later.
    /// </summary>
    public bool NoReset { get; set; } = false;

    public bool AutoGrantPermissions { get; set; } = true;

    /// <summary>
    /// When true, DriverFactory builds a session against BrowserStack App Automate
    /// instead of a local emulator - used by the scheduled CI run so the recurring
    /// suite doesn't require keeping a local emulator running indefinitely. All
    /// BrowserStack values below come from environment variables in CI
    /// (WEATHERAPP_Appium__...) - never hard-coded, never committed.
    /// </summary>
    public bool UseBrowserStack { get; set; } = false;

    public string BrowserStackServerUrl { get; set; } = "https://hub-cloud.browserstack.com/wd/hub";

    public string? BrowserStackUsername { get; set; }

    public string? BrowserStackAccessKey { get; set; }

    /// <summary>The "bs://..." reference returned by BrowserStack's app upload API - set per CI run, not committed.</summary>
    public string? BrowserStackAppUrl { get; set; }

    public string BrowserStackDeviceName { get; set; } = "Google Pixel 8";

    public string BrowserStackOsVersion { get; set; } = "14.0";
}
