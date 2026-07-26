using WeatherApp.MobileTests.Pages;
using WeatherApp.MobileTests.Support;

namespace WeatherApp.MobileTests.Tests;

/// <summary>
/// Logout is the other half of the auth boundary: if it silently no-ops, a shared or
/// borrowed device would stay signed in, which is a real, user-visible security
/// concern for a personal-data app like this one.
///
/// Logout itself takes no user input, so unlike the other three flows it has no
/// "invalid input" surface to probe - there's no form to fill in wrong. The four
/// "negative" cases below are therefore regression/boundary checks instead: they
/// confirm logout doesn't leave the app in a subtly broken state (auth still
/// correctly rejecting bad credentials afterwards, the account's data still intact,
/// the session not silently persisting across an app restart) rather than "invalid
/// input produces an error message". That distinction is intentional, not an
/// oversight - documented here and in the README.
/// </summary>
public class LogoutTests : TestBase
{
    private const string AppId = "com.companyname.weatherapp";

    private SettingsPage RegisterLoginAndReachSettings(out TestUser user)
    {
        user = TestUserFactory.CreateUniqueUser();
        var loginPage = new LoginPage(Driver, Wait)
            .GoToRegister()
            .Register(user.FullName, user.Email, user.Password);

        return loginPage.LoginWith(user.Email, user.Password).GoToSettings();
    }

    // ---- Positive ----

    [Test]
    public void Logout_FromSettings_ReturnsToLoginScreen()
    {
        var settingsPage = RegisterLoginAndReachSettings(out _);

        var loginPageAfterLogout = settingsPage.Logout();

        Assert.That(loginPageAfterLogout.IsDisplayedOnScreen(), Is.True,
            "Expected to land back on the Login screen after logging out.");
    }

    [Test]
    public void Logout_ThenLoginAgain_WithSameCredentials_Succeeds()
    {
        var settingsPage = RegisterLoginAndReachSettings(out var user);
        var loginPageAfterLogout = settingsPage.Logout();

        var landingPage = loginPageAfterLogout.LoginWith(user.Email, user.Password);

        Assert.That(landingPage.IsDisplayedOnScreen(), Is.True,
            "The same account should be able to log back in right after logging out.");
    }

    [Test]
    public void Logout_ThenRegisterNewAccount_Succeeds()
    {
        var settingsPage = RegisterLoginAndReachSettings(out _);
        var loginPageAfterLogout = settingsPage.Logout();

        var newUser = TestUserFactory.CreateUniqueUser();
        var resultingLoginPage = loginPageAfterLogout.GoToRegister()
            .Register(newUser.FullName, newUser.Email, newUser.Password);

        Assert.That(resultingLoginPage.IsDisplayedOnScreen(), Is.True,
            "The app should remain fully usable for a different account after a logout, not get stuck.");
    }

    [Test]
    public void Logout_AfterPerformingSearch_StillReturnsToLoginScreen()
    {
        var user = TestUserFactory.CreateUniqueUser();
        var landingPage = new LoginPage(Driver, Wait)
            .GoToRegister()
            .Register(user.FullName, user.Email, user.Password)
            .LoginWith(user.Email, user.Password);

        var landingPageAfterSearch = landingPage.GoToSearch()
            .SearchFor("London")
            .SelectFirstResult()
            .GoBackToSearch()
            .GoBackToLanding();
        var settingsPage = landingPageAfterSearch.GoToSettings();
        var loginPageAfterLogout = settingsPage.Logout();

        Assert.That(loginPageAfterLogout.IsDisplayedOnScreen(), Is.True,
            "Logout should work the same regardless of how deep the prior navigation went.");
    }

    // ---- Negative (regression/boundary checks - see class remarks) ----

    [Test]
    public void Logout_ThenLoginWithWrongPassword_StillFailsCorrectly()
    {
        var settingsPage = RegisterLoginAndReachSettings(out var user);
        var loginPageAfterLogout = settingsPage.Logout();

        loginPageAfterLogout.EnterEmail(user.Email);
        loginPageAfterLogout.EnterPassword("clearly-the-wrong-password");
        loginPageAfterLogout.SubmitLoginExpectingFailure();

        Assert.That(loginPageAfterLogout.GetStatusMessage(), Is.EqualTo("Invalid email or password."),
            "Logging out must not weaken subsequent credential validation.");
    }

    [Test]
    public void Logout_ThenLoginWithNonExistentEmail_StillFailsCorrectly()
    {
        var settingsPage = RegisterLoginAndReachSettings(out _);
        var loginPageAfterLogout = settingsPage.Logout();
        var neverRegisteredEmail = $"never.{Guid.NewGuid():N}@mobiletests.example.com";

        loginPageAfterLogout.EnterEmail(neverRegisteredEmail);
        loginPageAfterLogout.EnterPassword("SomePassword1!");
        loginPageAfterLogout.SubmitLoginExpectingFailure();

        Assert.That(loginPageAfterLogout.GetStatusMessage(), Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public void Logout_ThenRegisteringSameEmailAgain_ShowsAccountAlreadyExistsError()
    {
        var settingsPage = RegisterLoginAndReachSettings(out var user);
        var loginPageAfterLogout = settingsPage.Logout();

        var registerPage = loginPageAfterLogout.GoToRegister();
        registerPage.RegisterExpectingFailure(user.FullName, user.Email, user.Password, user.Password);

        Assert.That(registerPage.GetStatusMessage(), Is.EqualTo("An account with this email already exists."),
            "Logout is a session action, not a data reset - the account should still exist afterwards.");
    }

    [Test]
    public void Logout_ThenRelaunchApp_SessionDoesNotPersist_ShowsLoginScreen()
    {
        var settingsPage = RegisterLoginAndReachSettings(out _);
        settingsPage.Logout();

        Driver.TerminateApp(AppId);
        Driver.ActivateApp(AppId);
        var loginPageAfterRelaunch = new LoginPage(Driver, Wait);

        Assert.That(loginPageAfterRelaunch.IsDisplayedOnScreen(), Is.True,
            "The logged-out state must survive an app restart, not just an in-memory navigation.");
    }
}
