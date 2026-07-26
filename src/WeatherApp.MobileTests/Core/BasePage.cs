using OpenQA.Selenium;
using Serilog;

namespace WeatherApp.MobileTests.Core;

/// <summary>
/// Shared interaction layer for every Page Object. Tests and Page Objects never call
/// Selenium/Appium APIs directly - they call Click/Type/GetText here, which routes
/// through WaitHelper. This is what keeps waiting centralized: adding a new page never
/// requires re-deciding how to wait for an element.
/// </summary>
public abstract class BasePage
{
    protected readonly WebDriver Driver;
    protected readonly WaitHelper Wait;

    protected BasePage(WebDriver driver, WaitHelper wait)
    {
        Driver = driver;
        Wait = wait;
    }

    protected void Click(By locator)
    {
        Log.Information("Click -> {Locator}", locator);
        Wait.WaitForClickable(locator).Click();
    }

    protected void Type(By locator, string text)
    {
        Log.Information("Type '{Text}' -> {Locator}", text, locator);
        var element = Wait.WaitForVisible(locator);
        element.Clear();
        element.SendKeys(text);
    }

    protected string GetText(By locator) => Wait.WaitForVisible(locator).Text;

    protected bool IsDisplayed(By locator) => Wait.WaitForVisible(locator).Displayed;

    protected bool IsAbsent(By locator) => Wait.WaitForAbsence(locator);
}
