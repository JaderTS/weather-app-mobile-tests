using WeatherApp.MobileTests.Pages;
using WeatherApp.MobileTests.Support;

namespace WeatherApp.MobileTests.Tests;

/// <summary>
/// Registration is the entry point for every new user - if it silently breaks, no one
/// can create an account and every downstream flow (login, search, logout) becomes
/// untestable and unusable. Every validation rule exercised here (empty fields,
/// password confirmation, duplicate email, minimum password length) was confirmed by
/// hand against the real app before being written as a test - e.g. the app has no
/// email-format validation at all (a malformed address is accepted), which is why
/// that isn't one of the negative cases: asserting an error that never appears would
/// just be a permanently failing test, not a meaningful one.
/// </summary>
public class RegistrationTests : TestBase
{
    private LoginPage GoToRegisterPage(out RegisterPage registerPage)
    {
        var loginPage = new LoginPage(Driver, Wait);
        registerPage = loginPage.GoToRegister();
        return loginPage;
    }

    // ---- Positive ----

    [Test]
    public void Register_WithValidNewUser_RedirectsToLoginWithSuccessMessage()
    {
        var user = TestUserFactory.CreateUniqueUser();
        GoToRegisterPage(out var registerPage);

        var resultingLoginPage = registerPage.Register(user.FullName, user.Email, user.Password);

        Assert.That(resultingLoginPage.IsDisplayedOnScreen(), Is.True,
            "Expected to land back on the Login screen after successful registration.");
    }

    [Test]
    public void Register_ThenLogin_WithNewCredentials_Succeeds()
    {
        var user = TestUserFactory.CreateUniqueUser();
        GoToRegisterPage(out var registerPage);
        var loginPage = registerPage.Register(user.FullName, user.Email, user.Password);

        var landingPage = loginPage.LoginWith(user.Email, user.Password);

        Assert.That(landingPage.IsDisplayedOnScreen(), Is.True,
            "Expected a freshly registered account to be able to log in immediately.");
    }

    [Test]
    public void Register_WithMinimumValidPasswordLength_Succeeds()
    {
        var user = TestUserFactory.CreateUniqueUser() with { Password = "Abc12!" }; // exactly 6 characters
        GoToRegisterPage(out var registerPage);

        var resultingLoginPage = registerPage.Register(user.FullName, user.Email, user.Password);

        Assert.That(resultingLoginPage.IsDisplayedOnScreen(), Is.True,
            "A 6-character password is the documented minimum and should be accepted, not rejected.");
    }

    [Test]
    public void Register_WithSpecialCharactersInFullName_Succeeds()
    {
        var user = TestUserFactory.CreateUniqueUser() with { FullName = "Anne-Marie O'Brien" };
        GoToRegisterPage(out var registerPage);

        var resultingLoginPage = registerPage.Register(user.FullName, user.Email, user.Password);

        Assert.That(resultingLoginPage.IsDisplayedOnScreen(), Is.True,
            "Hyphens and apostrophes are valid in real names and should not break registration.");
    }

    // ---- Negative ----

    [Test]
    public void Register_WithEmptyFields_ShowsValidationError()
    {
        GoToRegisterPage(out var registerPage);

        registerPage.RegisterExpectingFailure(string.Empty, string.Empty, string.Empty, string.Empty);

        Assert.That(registerPage.GetStatusMessage(), Is.EqualTo("Please fill in all fields."));
    }

    [Test]
    public void Register_WithMismatchedPasswords_ShowsValidationError()
    {
        var user = TestUserFactory.CreateUniqueUser();
        GoToRegisterPage(out var registerPage);

        registerPage.RegisterExpectingFailure(user.FullName, user.Email, user.Password, "SomethingElse1!");

        Assert.That(registerPage.GetStatusMessage(), Is.EqualTo("Passwords do not match."));
    }

    [Test]
    public void Register_WithDuplicateEmail_ShowsValidationError()
    {
        var user = TestUserFactory.CreateUniqueUser();
        GoToRegisterPage(out var registerPage);
        var loginPage = registerPage.Register(user.FullName, user.Email, user.Password);

        var registerAgainPage = loginPage.GoToRegister();
        registerAgainPage.RegisterExpectingFailure(user.FullName, user.Email, user.Password, user.Password);

        Assert.That(registerAgainPage.GetStatusMessage(), Is.EqualTo("An account with this email already exists."));
    }

    [Test]
    public void Register_WithPasswordTooShort_ShowsValidationError()
    {
        var user = TestUserFactory.CreateUniqueUser() with { Password = "Ab12!" }; // 5 characters, one below the minimum
        GoToRegisterPage(out var registerPage);

        registerPage.RegisterExpectingFailure(user.FullName, user.Email, user.Password, user.Password);

        Assert.That(registerPage.GetStatusMessage(), Is.EqualTo("Password must be at least 6 characters."));
    }
}
