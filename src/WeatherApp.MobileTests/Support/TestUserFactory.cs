using WeatherApp.MobileTests.Config;

namespace WeatherApp.MobileTests.Support;

public sealed record TestUser(string FullName, string Email, string Password);

/// <summary>
/// Generates a brand-new, throwaway user for each test. Since the app under test has
/// no exposed API or DB reset hook, tests can't rely on a single fixed seeded account
/// without risking collisions/flakiness across repeated runs (the app persists
/// registered accounts on-device). A unique email per test keeps every test fully
/// independent and repeatable with zero setup, and keeps the only "credential" -
/// the password - out of source control (see TestUserSettings/appsettings.local.json).
/// </summary>
public static class TestUserFactory
{
    public static TestUser CreateUniqueUser()
    {
        var settings = ConfigurationProvider.Settings.TestUser;
        var uniqueSuffix = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid():N}"[..20];

        return new TestUser(
            FullName: "QA Automation",
            Email: $"qa.{uniqueSuffix}@{settings.EmailDomain}",
            Password: settings.Password);
    }
}
