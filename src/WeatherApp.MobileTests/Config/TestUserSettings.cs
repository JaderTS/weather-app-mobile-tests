namespace WeatherApp.MobileTests.Config;

/// <summary>
/// Settings for the synthetic test accounts created by TestUserFactory. The password
/// is the one piece of "credential-shaped" data the framework needs; it is never
/// committed - see appsettings.local.json.example and the README "Decisions" section.
/// </summary>
public sealed class TestUserSettings
{
    public string Password { get; set; } = "ChangeMe123!";

    public string EmailDomain { get; set; } = "mobiletests.example.com";
}
