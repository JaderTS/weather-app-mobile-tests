using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using WeatherApp.MobileTests.Core;

namespace WeatherApp.MobileTests.Pages;

public sealed class SettingsPage : BasePage
{
    private static readonly By LogoutButton =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.Button\").text(\"Logout\")");

    private static readonly By LoggedInAsLabel =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.TextView\").textContains(\"Logged in as\")");

    public SettingsPage(WebDriver driver, WaitHelper wait) : base(driver, wait)
    {
    }

    public LoginPage Logout()
    {
        Click(LogoutButton);
        return new LoginPage(Driver, Wait);
    }

    /// <summary>e.g. "Logged in as Jane Doe" - used to verify the correct account identity carried through login.</summary>
    public string GetLoggedInAsText() => GetText(LoggedInAsLabel);
}
