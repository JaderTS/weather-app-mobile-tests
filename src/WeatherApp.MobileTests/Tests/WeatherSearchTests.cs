using WeatherApp.MobileTests.Pages;
using WeatherApp.MobileTests.Support;

namespace WeatherApp.MobileTests.Tests;

/// <summary>
/// Weather search is the app's core value proposition. Every scenario here was
/// confirmed by hand against the real (live) geocoding API before being written as a
/// test: lowercase queries resolve, a non-first suggestion resolves to its own
/// distinct place, and empty/whitespace/numeric queries all degrade the same way a
/// nonsense query does (no suggestions list appears at all, no crash, no stale
/// results) - so the negative cases assert *absence*, not an error message that
/// doesn't exist for this screen.
/// </summary>
public class WeatherSearchTests : TestBase
{
    private LandingPage LoginAsFreshUser()
    {
        var user = TestUserFactory.CreateUniqueUser();
        var loginPage = new LoginPage(Driver, Wait)
            .GoToRegister()
            .Register(user.FullName, user.Email, user.Password);

        return loginPage.LoginWith(user.Email, user.Password);
    }

    // ---- Positive ----

    [Test]
    public void Search_ValidCity_DisplaysForecastForSelectedLocation()
    {
        var searchPage = LoginAsFreshUser().GoToSearch();

        var forecastPage = searchPage.SearchFor("London").SelectFirstResult();

        Assert.That(forecastPage.GetLocationHeaderText(), Does.Contain("London"));
    }

    [Test]
    public void Search_WithLowercaseQuery_StillResolvesResults()
    {
        var searchPage = LoginAsFreshUser().GoToSearch();

        var forecastPage = searchPage.SearchFor("london").SelectFirstResult();

        Assert.That(forecastPage.GetLocationHeaderText(), Does.Contain("London"),
            "Search matching should be case-insensitive - confirmed on the real app before writing this test.");
    }

    [Test]
    public void Search_SelectingNonFirstSuggestion_ResolvesToThatLocation()
    {
        var searchPage = LoginAsFreshUser().GoToSearch();

        // "London" reliably returns London, England as the 1st suggestion and several
        // other "Londo*" places after it - selecting the 2nd proves the list resolves
        // whichever row is tapped, not just the first one.
        var forecastPage = searchPage.SearchFor("London").SelectResultAt(1);

        Assert.That(forecastPage.GetLocationHeaderText(), Does.Not.Contain("United Kingdom"),
            "Selecting the 2nd suggestion should resolve to a different place than the 1st (London, UK).");
    }

    [Test]
    public void Search_SequentialSearches_EachResolvesIndependently()
    {
        var searchPage = LoginAsFreshUser().GoToSearch();

        var firstForecast = searchPage.SearchFor("London").SelectFirstResult();
        Assert.That(firstForecast.GetLocationHeaderText(), Does.Contain("London"));

        var secondSearchPage = firstForecast.GoBackToSearch();
        var secondForecast = secondSearchPage.SearchFor("Paris").SelectFirstResult();

        Assert.That(secondForecast.GetLocationHeaderText(), Does.Contain("Paris"),
            "A second, different search in the same session should resolve to its own place, not the first one's.");
    }

    // ---- Negative ----

    [Test]
    public void Search_NonExistentPlace_ShowsNoSuggestions()
    {
        var searchPage = LoginAsFreshUser().GoToSearch();

        var results = searchPage.SearchFor("zzqxxnotarealplacezz");

        Assert.That(results.HasNoResults(), Is.True,
            "Expected no autocomplete suggestions for a nonsense query.");
    }

    [Test]
    public void Search_WithEmptyQuery_ShowsNoSuggestions()
    {
        var searchPage = LoginAsFreshUser().GoToSearch();

        var results = searchPage.SearchFor(string.Empty);

        Assert.That(results.HasNoResults(), Is.True,
            "An empty query should show no suggestions rather than crashing or showing stale results.");
    }

    [Test]
    public void Search_WithWhitespaceOnlyQuery_ShowsNoSuggestions()
    {
        var searchPage = LoginAsFreshUser().GoToSearch();

        var results = searchPage.SearchFor("   ");

        Assert.That(results.HasNoResults(), Is.True,
            "Whitespace is not a place name and should resolve to no suggestions.");
    }

    [Test]
    public void Search_WithNumericQuery_ShowsNoSuggestions()
    {
        var searchPage = LoginAsFreshUser().GoToSearch();

        var results = searchPage.SearchFor("12345");

        Assert.That(results.HasNoResults(), Is.True,
            "A purely numeric query is not a place name and should resolve to no suggestions.");
    }
}
