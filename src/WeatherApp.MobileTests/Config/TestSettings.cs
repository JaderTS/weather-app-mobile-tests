namespace WeatherApp.MobileTests.Config;

/// <summary>
/// Root of the strongly-typed configuration tree. One instance is built once per test
/// run by ConfigurationProvider and shared by every fixture.
/// </summary>
public sealed class TestSettings
{
    public AppiumSettings Appium { get; set; } = new();

    public TimeoutSettings Timeouts { get; set; } = new();

    public TestUserSettings TestUser { get; set; } = new();
}
