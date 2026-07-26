using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using WeatherApp.MobileTests.Components;
using WeatherApp.MobileTests.Core;

namespace WeatherApp.MobileTests.Pages;

public sealed class SearchPage : BasePage
{
    private static readonly By SearchInput =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.EditText\").instance(0)");

    private static readonly By BackButton =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.Button\").text(\"‹\")");

    public SearchPage(WebDriver driver, WaitHelper wait) : base(driver, wait)
    {
    }

    public LocationResultList SearchFor(string query)
    {
        Type(SearchInput, query);
        return new LocationResultList(Driver, Wait);
    }

    public LandingPage GoBackToLanding()
    {
        Click(BackButton);
        return new LandingPage(Driver, Wait);
    }
}
