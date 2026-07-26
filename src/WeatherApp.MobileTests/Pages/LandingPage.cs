using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using WeatherApp.MobileTests.Core;

namespace WeatherApp.MobileTests.Pages;

/// <summary>
/// The screen shown right after login: app title, "Get Started" CTA into search, and a
/// settings gear icon. The gear has no text/content-desc, so it's located as the first
/// Button on this screen - safe only because this Page Object is only ever used while
/// on the Landing screen (see the locator-strategy note in the README).
/// </summary>
public sealed class LandingPage : BasePage
{
    private static readonly By GetStartedButton =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.Button\").text(\"Get Started\")");

    private static readonly By SettingsGearButton =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.Button\").instance(0)");

    public LandingPage(WebDriver driver, WaitHelper wait) : base(driver, wait)
    {
    }

    public SearchPage GoToSearch()
    {
        Click(GetStartedButton);
        return new SearchPage(Driver, Wait);
    }

    public SettingsPage GoToSettings()
    {
        Click(SettingsGearButton);
        return new SettingsPage(Driver, Wait);
    }

    public bool IsDisplayedOnScreen() => IsDisplayed(GetStartedButton);
}
