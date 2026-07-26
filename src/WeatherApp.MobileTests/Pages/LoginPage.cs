using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using WeatherApp.MobileTests.Core;

namespace WeatherApp.MobileTests.Pages;

public sealed class LoginPage : BasePage
{
    private static readonly By EmailInput =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.EditText\").instance(0)");

    private static readonly By PasswordInput =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.EditText\").instance(1)");

    private static readonly By LoginButton =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.Button\").text(\"Login\")");

    private static readonly By RegisterLink =
        MobileBy.AndroidUIAutomator("new UiSelector().text(\"Register\")");

    /// <summary>
    /// The single conditional status-line slot on this screen: it renders whichever
    /// message applies ("Please fill in all fields." or "Invalid email or password.").
    /// Confirmed via uiautomator dump that it's always the 3rd TextView (index 2) on
    /// this screen regardless of which message is showing, so one locator covers both
    /// instead of a separate textContains(...) locator per message.
    /// </summary>
    private static readonly By StatusMessage =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.TextView\").instance(2)");

    public LoginPage(WebDriver driver, WaitHelper wait) : base(driver, wait)
    {
    }

    public void EnterEmail(string email) => Type(EmailInput, email);

    public void EnterPassword(string password) => Type(PasswordInput, password);

    public LandingPage SubmitLogin()
    {
        Click(LoginButton);
        return new LandingPage(Driver, Wait);
    }

    /// <summary>Submits Login expecting the app to reject it and stay on this screen (negative path).</summary>
    public void SubmitLoginExpectingFailure() => Click(LoginButton);

    public RegisterPage GoToRegister()
    {
        Click(RegisterLink);
        return new RegisterPage(Driver, Wait);
    }

    public LandingPage LoginWith(string email, string password)
    {
        EnterEmail(email);
        EnterPassword(password);
        return SubmitLogin();
    }

    /// <summary>Fills only the email field, leaving password blank - for partial-empty-field negative tests.</summary>
    public void EnterEmailOnly(string email) => EnterEmail(email);

    public string GetStatusMessage() => GetText(StatusMessage);

    public bool IsDisplayedOnScreen() => IsDisplayed(LoginButton);
}
