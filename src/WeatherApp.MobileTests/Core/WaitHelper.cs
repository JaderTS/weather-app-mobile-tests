using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using WeatherApp.MobileTests.Config;

namespace WeatherApp.MobileTests.Core;

/// <summary>
/// The single place explicit waits are configured and performed. No page object or
/// test constructs its own WebDriverWait or - critically - calls Thread.Sleep; every
/// wait goes through here so timeout/polling behavior is consistent and changeable
/// from one place (TimeoutSettings).
/// </summary>
public sealed class WaitHelper
{
    private readonly WebDriver _driver;
    private readonly WebDriverWait _wait;
    private readonly WebDriverWait _absenceWait;

    public WaitHelper(WebDriver driver, TimeoutSettings timeouts)
    {
        _driver = driver;

        _wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeouts.DefaultWaitSeconds))
        {
            PollingInterval = TimeSpan.FromMilliseconds(timeouts.PollingIntervalMilliseconds),
        };
        _wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        _absenceWait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeouts.AbsenceWaitSeconds))
        {
            PollingInterval = TimeSpan.FromMilliseconds(timeouts.PollingIntervalMilliseconds),
        };
        _absenceWait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
    }

    public IWebElement WaitForVisible(By locator) =>
        _wait.Until(drv =>
        {
            var element = drv.FindElement(locator);
            return element.Displayed ? element : null;
        }) ?? throw new WebDriverTimeoutException($"Element never became visible: {locator}");

    public IWebElement WaitForClickable(By locator) =>
        _wait.Until(drv =>
        {
            var element = drv.FindElement(locator);
            return element.Displayed && element.Enabled ? element : null;
        }) ?? throw new WebDriverTimeoutException($"Element never became clickable: {locator}");

    /// <summary>
    /// Bounded wait that proves an element does NOT appear. Used for negative
    /// scenarios (e.g. no autocomplete suggestions for a non-existent place) where
    /// absence, not an error message, is the correct app behavior to assert on.
    /// </summary>
    public bool WaitForAbsence(By locator)
    {
        try
        {
            return _absenceWait.Until(drv => drv.FindElements(locator).Count == 0);
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }
}
