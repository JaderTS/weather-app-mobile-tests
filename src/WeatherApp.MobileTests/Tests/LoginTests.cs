using WeatherApp.MobileTests.Pages;
using WeatherApp.MobileTests.Support;

namespace WeatherApp.MobileTests.Tests;

/// <summary>
/// Login is the auth gate for the entire app. Each test registers its own fresh user
/// first (via the UI, in the same driver session) rather than depending on a
/// pre-seeded account, since the app has no exposed API/DB reset hook and persists
/// accounts on-device between runs. Every scenario here (case-insensitive email,
/// partial-empty fields, non-existent email) was confirmed by hand against the real
/// app before being written as a test.
/// </summary>
public class LoginTests : TestBase
{
    private LoginPage RegisterFreshUser(out TestUser user)
    {
        user = TestUserFactory.CreateUniqueUser();
        return new LoginPage(Driver, Wait)
            .GoToRegister()
            .Register(user.FullName, user.Email, user.Password);
    }

    // ---- Positive ----

    [Test]
    public void Login_WithValidCredentials_NavigatesToLandingScreen()
    {
        var loginPage = RegisterFreshUser(out var user);

        var landingPage = loginPage.LoginWith(user.Email, user.Password);

        Assert.That(landingPage.IsDisplayedOnScreen(), Is.True,
            "Expected the landing screen (Get Started CTA) after a valid login.");
    }

    [Test]
    public void Login_WithEmailInDifferentCase_Succeeds()
    {
        var loginPage = RegisterFreshUser(out var user);

        var landingPage = loginPage.LoginWith(user.Email.ToUpperInvariant(), user.Password);

        Assert.That(landingPage.IsDisplayedOnScreen(), Is.True,
            "Email matching should be case-insensitive - confirmed on the real app before writing this test.");
    }

    [Test]
    public void Login_AfterLogout_WithSameCredentials_Succeeds()
    {
        var loginPage = RegisterFreshUser(out var user);
        var loginPageAfterLogout = loginPage
            .LoginWith(user.Email, user.Password)
            .GoToSettings()
            .Logout();

        var landingPage = loginPageAfterLogout.LoginWith(user.Email, user.Password);

        Assert.That(landingPage.IsDisplayedOnScreen(), Is.True,
            "An account should remain usable for repeated login/logout cycles, not just once.");
    }

    [Test]
    public void Login_WithValidCredentials_DisplaysCorrectUserNameInSettings()
    {
        var loginPage = RegisterFreshUser(out var user);

        var settingsPage = loginPage.LoginWith(user.Email, user.Password).GoToSettings();

        Assert.That(settingsPage.GetLoggedInAsText(), Is.EqualTo($"Logged in as {user.FullName}"),
            "The identity carried through login should match the account that was registered, not just any account.");
    }

    // ---- Negative ----

    [Test]
    public void Login_WithInvalidPassword_ShowsInvalidCredentialsError()
    {
        var loginPage = RegisterFreshUser(out var user);

        loginPage.EnterEmail(user.Email);
        loginPage.EnterPassword("clearly-the-wrong-password");
        loginPage.SubmitLoginExpectingFailure();

        Assert.That(loginPage.GetStatusMessage(), Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public void Login_WithNonExistentEmail_ShowsInvalidCredentialsError()
    {
        var loginPage = new LoginPage(Driver, Wait);
        var neverRegisteredEmail = $"never.{Guid.NewGuid():N}@mobiletests.example.com";

        loginPage.EnterEmail(neverRegisteredEmail);
        loginPage.EnterPassword("SomePassword1!");
        loginPage.SubmitLoginExpectingFailure();

        Assert.That(loginPage.GetStatusMessage(), Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public void Login_WithEmptyFields_ShowsValidationError()
    {
        var loginPage = new LoginPage(Driver, Wait);

        loginPage.SubmitLoginExpectingFailure();

        Assert.That(loginPage.GetStatusMessage(), Is.EqualTo("Please fill in all fields."));
    }

    [Test]
    public void Login_WithOnlyEmailFilled_ShowsValidationError()
    {
        var loginPage = new LoginPage(Driver, Wait);

        loginPage.EnterEmailOnly("someone@example.com");
        loginPage.SubmitLoginExpectingFailure();

        Assert.That(loginPage.GetStatusMessage(), Is.EqualTo("Please fill in all fields."));
    }
}
