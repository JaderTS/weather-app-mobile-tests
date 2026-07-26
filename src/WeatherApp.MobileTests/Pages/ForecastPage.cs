using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using WeatherApp.MobileTests.Core;

namespace WeatherApp.MobileTests.Pages;

public sealed class ForecastPage : BasePage
{
    /// <summary>
    /// Matched by containing a comma ("City, Region, Country") rather than by plain
    /// instance(0). During the search-to-forecast transition the previous screen's
    /// search icon TextView briefly remains in the tree and would otherwise be picked
    /// up as "the first TextView" - confirmed by hand (it's disabled/greyed but still
    /// reports Displayed=true). The comma is unique to the location header among
    /// everything visible at that point, so waiting on it also waits out the
    /// transition instead of grabbing a stale element.
    /// </summary>
    private static readonly By LocationHeader =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.TextView\").textContains(\",\")");

    private static readonly By BackButton =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.Button\").text(\"‹\")");

    public ForecastPage(WebDriver driver, WaitHelper wait) : base(driver, wait)
    {
    }

    /// <summary>e.g. "London, England, United Kingdom" - the first line rendered on this screen.</summary>
    public string GetLocationHeaderText() => GetText(LocationHeader);

    /// <summary>Returns to the Search screen (which retains the previous query) - confirmed by hand.</summary>
    public SearchPage GoBackToSearch()
    {
        Click(BackButton);
        return new SearchPage(Driver, Wait);
    }
}
