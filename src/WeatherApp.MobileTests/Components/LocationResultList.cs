using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using WeatherApp.MobileTests.Core;
using WeatherApp.MobileTests.Pages;

namespace WeatherApp.MobileTests.Components;

/// <summary>
/// Component Object for the live geocoding autocomplete suggestions list on the
/// Search screen. It's modeled separately from SearchPage because it's a distinct,
/// self-contained UI region (a RecyclerView) with its own wait/assert semantics
/// (present vs. absent) - folding it into SearchPage would mix "type a query" concerns
/// with "read/interact with results" concerns in one class.
/// </summary>
public sealed class LocationResultList : BasePage
{
    private static readonly By SuggestionsList =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"androidx.recyclerview.widget.RecyclerView\")");

    public LocationResultList(WebDriver driver, WaitHelper wait) : base(driver, wait)
    {
    }

    private static By ResultRowAt(int index) =>
        MobileBy.AndroidUIAutomator(
            "new UiSelector().className(\"androidx.recyclerview.widget.RecyclerView\")" +
            $".childSelector(new UiSelector().clickable(true).instance({index}))");

    /// <summary>Selects the top suggestion regardless of query text and follows the app to the forecast screen.</summary>
    public ForecastPage SelectFirstResult() => SelectResultAt(0);

    /// <summary>
    /// Selects the suggestion at the given zero-based position (each row is its own
    /// clickable item in the RecyclerView) and follows the app to the forecast screen.
    /// Used to prove the list isn't just wired up for the first row.
    /// </summary>
    public ForecastPage SelectResultAt(int index)
    {
        Click(ResultRowAt(index));
        return new ForecastPage(Driver, Wait);
    }

    /// <summary>True if the suggestions list never appears - the app's behavior for an unmatched query.</summary>
    public bool HasNoResults() => IsAbsent(SuggestionsList);
}
