using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using WeatherApp.MobileTests.Core;

namespace WeatherApp.MobileTests.Pages;

public sealed class RegisterPage : BasePage
{
    private static readonly By FullNameInput =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.EditText\").instance(0)");

    private static readonly By EmailInput =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.EditText\").instance(1)");

    private static readonly By PasswordInput =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.EditText\").instance(2)");

    private static readonly By ConfirmPasswordInput =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.EditText\").instance(3)");

    private static readonly By RegisterButton =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.Button\").text(\"Register\")");

    private static readonly By LoginLink =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.TextView\").text(\"Login\")");

    /// <summary>
    /// The single conditional status-line slot on this screen - covers all four
    /// validation messages ("Please fill in all fields.", "Passwords do not match.",
    /// "An account with this email already exists.", "Password must be at least 6
    /// characters."). Confirmed via uiautomator dump that it's always the 3rd
    /// TextView (index 2) on this screen for every one of those four cases.
    /// </summary>
    private static readonly By StatusMessage =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.TextView\").instance(2)");

    public RegisterPage(WebDriver driver, WaitHelper wait) : base(driver, wait)
    {
    }

    private void FillForm(string fullName, string email, string password, string confirmPassword)
    {
        Type(FullNameInput, fullName);
        Type(EmailInput, email);
        Type(PasswordInput, password);
        Type(ConfirmPasswordInput, confirmPassword);
    }

    /// <summary>
    /// Registers a user and returns the LoginPage the app redirects to on success
    /// (with the "Account created successfully! Please login." toast).
    /// </summary>
    public LoginPage Register(string fullName, string email, string password)
    {
        FillForm(fullName, email, password, password);
        Click(RegisterButton);
        return new LoginPage(Driver, Wait);
    }

    /// <summary>Submits Register expecting the app to reject it and stay on this screen (negative path).</summary>
    public void RegisterExpectingFailure(string fullName, string email, string password, string confirmPassword)
    {
        FillForm(fullName, email, password, confirmPassword);
        Click(RegisterButton);
    }

    public LoginPage GoToLogin()
    {
        Click(LoginLink);
        return new LoginPage(Driver, Wait);
    }

    public string GetStatusMessage() => GetText(StatusMessage);
}
