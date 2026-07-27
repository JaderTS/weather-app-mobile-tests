# Weather App — Mobile Automation Framework

An Appium + C#/NUnit automation framework for the Weather App Android build
(`apps/weather-app.apk`), covering the four flows the challenge calls out by name:
**Registration, Login, Weather Search, Logout**.

> This README is the primary, evaluated document (English, per the brief). A Portuguese
> companion with equivalent content lives at [`README.pt-BR.md`](README.pt-BR.md).

## Technologies

- C#
- .NET 8
- NUnit 3
- Appium 2 (UiAutomator2 driver)
- Page Object Model + Component Objects
- Serilog (console + rolling file logging)
- Allure 2 (HTML test reports, with trend history across runs)
- Microsoft.Extensions.Configuration (layered JSON + environment variable config)
- GitHub Actions — used earlier for a BrowserStack-backed CI pipeline; both the
  workflows and the BrowserStack driver support were removed once the trial's testing
  minutes ran out (see Decisions)

## Architecture

The framework is intentionally small and flat — four layers, each with one job, no
layer that exists just to satisfy a pattern:

```
Tests  →  Page Objects  →  BasePage (Click / Type / GetText / IsAbsent)  →  AndroidDriver
                ↑                        ↓
          Components              WaitHelper (the only place waits are configured)
```

### Tests express behavior, nothing else

```csharp
[Test]
public void Login_WithValidCredentials_NavigatesToLandingScreen()
{
    var loginPage = RegisterFreshUser(out var user);

    var landingPage = loginPage.LoginWith(user.Email, user.Password);

    Assert.That(landingPage.IsDisplayedOnScreen(), Is.True,
        "Expected the landing screen (Get Started CTA) after a valid login.");
}
```

No locators, no `FindElement`, no waits, no driver setup. A test only answers "what is
being validated" — everything else is somebody else's job.

### Page Objects own one screen: locators + business actions

```csharp
public sealed class LoginPage : BasePage
{
    private static readonly By EmailInput =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.EditText\").instance(0)");

    private static readonly By LoginButton =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.Button\").text(\"Login\")");

    public LandingPage LoginWith(string email, string password)
    {
        EnterEmail(email);
        EnterPassword(password);
        return SubmitLogin();
    }
    // ...
}
```

`LoginPage` knows *what* login means for this app. It does not re-implement how to
wait, click, or type — that's inherited from `BasePage`.

### Component Objects model a reusable UI region, not a full screen

```csharp
public sealed class LocationResultList : BasePage
{
    private static By ResultRowAt(int index) =>
        MobileBy.AndroidUIAutomator(
            "new UiSelector().className(\"androidx.recyclerview.widget.RecyclerView\")" +
            $".childSelector(new UiSelector().clickable(true).instance({index}))");

    public ForecastPage SelectResultAt(int index) { Click(ResultRowAt(index)); return new ForecastPage(Driver, Wait); }

    public bool HasNoResults() => IsAbsent(SuggestionsList);
}
```

The autocomplete suggestions list has its own lifecycle (present vs. absent) that's
distinct from `SearchPage`'s job of owning the search box — folding the two together
would make the negative test (asserting absence) read oddly against a page object
whose other methods assume presence.

### BasePage is the only class that touches Selenium/Appium directly

```csharp
public abstract class BasePage
{
    protected readonly WebDriver Driver;
    protected readonly WaitHelper Wait;

    protected void Click(By locator) => Wait.WaitForClickable(locator).Click();

    protected void Type(By locator, string text)
    {
        var element = Wait.WaitForVisible(locator);
        element.Clear();
        element.SendKeys(text);
    }
}
```

Every Page Object and Component inherits this instead of composing a separate
"actions" object — one interaction surface for the whole framework, and exactly one
place (`WaitHelper`) where explicit waits are configured. There is no `Thread.Sleep`
call anywhere in this codebase. (`Click`/`Type` also retry once on a stale element —
see Decisions for why.)

### DriverFactory builds the session purely from config

```csharp
public static AndroidDriver CreateAndroidDriver()
{
    var settings = ConfigurationProvider.Settings.Appium;
    return CreateLocalDriver(settings);
}
```

Device, APK path, and timeouts are config, never a code change. This used to also
switch between a local emulator and a BrowserStack App Automate session behind the
same config flag — see [Decisions](#decisions) for why that branch was removed.

### TestBase controls the one lifecycle every fixture shares

```csharp
[SetUp]
public void BaseSetUp()
{
    Driver = DriverFactory.CreateAndroidDriver();
    Wait = new WaitHelper(Driver, ConfigurationProvider.Settings.Timeouts);
}

[TearDown]
public void BaseTearDown()
{
    if (failed) ScreenshotHelper.Capture(Driver, context.Test.Name);
    AllureReportWriter.StopAndWriteTestCase();
    Driver?.Quit();
}
```

One driver per **test**, not per fixture or per session — the app persists state
on-device, so a shared driver across tests would leak session state between them (this
was an actual, confirmed bug during development — see Decisions). The trade-off is
speed (~15–20s of app-reset overhead per test); the alternative was flaky.

### Folder structure

```
mobile-coding-challenge/
├── apps/
│   └── weather-app.apk
├── docs/                                # a generated Allure report, checked in for GitHub Pages (last real run before CI was removed - see Decisions)
├── src/WeatherApp.MobileTests/
│   ├── Config/       # AppiumSettings, TimeoutSettings, TestUserSettings, ConfigurationProvider
│   ├── Drivers/      # DriverFactory, AppiumServerManager
│   ├── Core/         # BasePage, WaitHelper
│   ├── Pages/        # LoginPage, RegisterPage, LandingPage, SearchPage, ForecastPage, SettingsPage
│   ├── Components/   # LocationResultList
│   ├── Support/      # LoggerConfigurator, ScreenshotHelper, TestUserFactory, AllureReportWriter
│   ├── Tests/        # TestBase + RegistrationTests, LoginTests, WeatherSearchTests, LogoutTests
│   ├── appsettings.json                 # committed, no secrets
│   ├── appsettings.local.json.example   # copy to appsettings.local.json (git-ignored) for local overrides
│   └── allureConfig.json
├── WeatherApp.MobileTests.sln
├── README.md
└── README.pt-BR.md
```

### What I deliberately did not add

Each of these would be a legitimate choice at larger scale, and each would be
over-engineering here:

- **A `Repository`/DI-container layer** — four screens and a handful of tests don't
  need indirection over their own indirection.
- **An interface per Page Object** (`ILoginPage`) — nothing swaps the implementation;
  an interface here would exist only to say "I used interfaces."
  **XPath as the primary locator strategy** — `AndroidUIAutomator`/`UiSelector` is the
  native, faster, Android-recommended mechanism; XPath is a fallback I never needed.
- **A separate `Actions` class per Page** (`LoginPageActions` next to `LoginPage`) —
  that split only pays off when a Page's action surface is huge; ours aren't.
- **Automatic retries on the whole suite or on a whole test** — masks real flakiness
  instead of surfacing it. The one retry this framework does have is narrower and
  documented in Decisions: a single re-locate-and-act retry inside `BasePage` for a
  `StaleElementReferenceException` specifically, not a general-purpose retry policy.
- **A global `AllLocators.cs`** — locators live as private constants next to the
  Page/Component that owns them, not in one giant, context-free file.

## How to Run

### Prerequisites

- **.NET 8 SDK**
- **Node.js** (for Appium) and **Java 17+** (for Android tooling — `avdmanager`
  specifically needs 17+; the Appium server itself runs fine on 11+)
- **Android SDK** with `platform-tools` (`adb`), `emulator`, and at least one AVD (this
  project was built/tested against a **Pixel 8 / API 35** AVD)
- **Appium 2.x** and the **UiAutomator2** driver:
  ```bash
  npm install -g appium@2
  appium driver install uiautomator2@4.2.9
  ```
  (Pin the driver version — the newest `uiautomator2` releases target Appium 3, not 2;
  4.2.9 is the last one that explicitly supports both.)
- **Allure commandline**:
  ```bash
  brew install allure        # macOS
  # or: scoop install allure # Windows
  # or download from https://github.com/allure-framework/allure2/releases
  ```

### 1. Start the emulator

```bash
emulator -avd Pixel_8_API_35
```

Wait for it to fully boot (`adb devices` should show it as `device`, not `offline`).

**If your AVD has a different name** (list yours with `emulator -list-avds`), you don't
need to recreate one to match - copy `appsettings.local.json.example` to
`appsettings.local.json` and set `Appium:DeviceName` to whatever yours is called:
```json
{ "Appium": { "DeviceName": "Your_AVD_Name_Here" } }
```
This file is git-ignored, so it's the intended place for exactly this kind of
per-machine override (see Configuration below).

### 2. Restore, build, test

```bash
dotnet restore
dotnet build
dotnet test
```

The framework **starts its own local Appium server** by default (see
`Appium:ManageServerLifecycle` in `appsettings.json`). If Appium is already running, or
you're pointing at a remote grid, set `"ManageServerLifecycle": false` in
`appsettings.local.json` instead.

A full local run takes a few minutes: every test resets the app (`noReset:false`) so
each one starts from the same Login screen regardless of what a previous test left
behind — see [Decisions](#decisions).

### 3. View the reports

```bash
cd src/WeatherApp.MobileTests/bin/Debug/net8.0
allure generate allure-results --clean -o allure-report
allure open allure-report
```

Logs land in `logs/test-run-<date>.log`, failure screenshots in `Screenshots/` — both
next to the test binaries, and both attached automatically to failed tests in the
Allure report too.

### Configuration

All settings live in `appsettings.json` (committed, no secrets) and can be overridden
via `appsettings.local.json` (git-ignored) or environment variables prefixed
`WEATHERAPP_` (e.g. `WEATHERAPP_TestUser__Password`). Nothing — device name, timeouts,
APK path, test account password — is hard-coded in a class.

## Scope of Automation (QA Perspective)

The brief lists Registration, Login, Weather Search and Logout as example flows for a
generic app — this app happens to implement exactly those four. The suite covers each
with 4 positive and 4 negative cases (32 tests, across 4 fixtures). Every case below —
including which validation rules exist and what error text they produce — was
confirmed by hand against the real app (`adb`/`uiautomator`) *before* being written as
a test; none of it is guessed from the UI alone.

| Fixture | Positive cases | Negative cases |
|---|---|---|
| `RegistrationTests` | valid new user · immediate login after registering · minimum valid password length (6 chars, boundary) · special characters in full name | empty fields · mismatched password/confirm · duplicate email · password below minimum length (boundary) |
| `LoginTests` | valid credentials · case-insensitive email · re-login after logout · correct identity shown in Settings | wrong password · non-existent email · empty fields · only email filled |
| `WeatherSearchTests` | valid city · lowercase query · selecting a non-first suggestion · two sequential independent searches | nonsense query · empty query · whitespace-only query · numeric-only query |
| `LogoutTests` | returns to Login · re-login afterwards · register a different account afterwards · works after a deep navigation (search) | wrong password still rejected · non-existent email still rejected · account still exists (re-registering it fails) · session doesn't survive app restart |

**Why 32 and not the "4 well-justified tests" the brief itself suggests?** The brief's
own wording — "the goal is not to maximize the number of tests" — is right, and I take
it seriously: every one of these 32 is a distinct, real, confirmed behavior of the app,
not padding to hit a number. The four "positive" cases per flow are boundary/robustness
variants (minimum password length, case-insensitive email, non-first suggestion
selection), not four copies of the same happy path; the four "negative" cases are four
different rejected inputs, not one assertion repeated. If the brief's philosophy is
weighed more heavily than the specific count, the honest minimum defensible scope is
**one positive + one negative per flow** (8 tests) — every fixture's first listed case
in each column is that minimum, and the rest are additive, not required.

A few scope decisions worth calling out:

- **Registration has no "invalid email format" negative test** because the app has
  none — a malformed address (`not-an-email`) is accepted. Confirmed by hand; asserting
  an error that never appears would be a permanently failing test, not a meaningful one.
- **Weather Search's negative cases assert absence, not an error message** — an
  unmatched, empty, whitespace, or numeric query all produce the same real behavior:
  the suggestions list simply never appears. There is no error string to assert on.
- **Logout's "negative" cases are regression/boundary checks, not input validation.**
  Logout takes no user input, so unlike the other three flows there's nothing invalid
  to submit. These four instead confirm logout doesn't leave the app subtly broken:
  auth still rejects bad credentials afterwards, the account's data is still intact
  (it's a session action, not a data wipe), and the session doesn't silently persist
  across an app restart (verified by actually terminating and relaunching the app
  mid-test via Appium, not just navigating back in-app).

Each test creates its own throwaway user via `TestUserFactory` (unique email per test),
so every test is independent and repeatable with no shared fixture data and no need to
reset the app's local database between runs.

## Decisions

### Why not AccessibilityId — and what I used instead

The generic outline for this kind of README assumes elements are located by
accessibility id. I checked first: dumping the UI hierarchy (`adb shell uiautomator
dump`) on every screen — Login, Register, Search, Settings — showed that **no
interactive element has a `resource-id` or `content-desc`**. Only framework-level
containers (`nav_host`, `action_bar_root`) have ids; every button, input, and label a
test would touch has neither. `AccessibilityId` simply isn't available here — using it
would mean the framework doesn't work against the actual app under test.

I also found, by filling in the Login/Register forms by hand before writing any code,
that `EditText` elements report their **hint text as their `text` value only while
empty** — the instant you type into one, `text` becomes what you typed. That rules out
locating inputs by their visible label too, since the locator would stop matching the
moment the test does the thing it exists to do.

What I used instead, per element type:
- **Buttons and links** — static text via `UiSelector().text(...)`. Safe, because a
  button's label doesn't change while the user interacts with it.
- **Text inputs** — `className("android.widget.EditText").instance(n)`, i.e. position
  among inputs on that screen. Fragile in the abstract, stable in practice for a fixed,
  small form — the only option that survives the app actually being used.
- **The autocomplete suggestions list** — rows located by
  `childSelector(new UiSelector().clickable(true).instance(n))`, so it never hardcodes
  a city name and can select any row by position.
- **Validation/status messages** (Login, Register) — both screens render whichever
  message currently applies into the *same* conditional slot (confirmed via dump:
  always the 3rd `TextView`), so one `instance(2)` locator per screen covers every
  validation case instead of a separate locator per message.

In a real project, my first move would be to get `AutomationProperties.AutomationId`
set in the app's XAML (this is a .NET MAUI app, so that's a small, idiomatic change for
the mobile team) — that would move the input-locator strategy to `AccessibilityId` and
remove the "locate by position" fragility entirely.

### Why one driver per test, not per fixture

The app persists login state on-device. I tried sharing state more aggressively first
(`noReset:true`, faster — skips the ~15-20s app-reset cost per test) and hit a real,
reproducible bug: a test that leaves the user logged in (everything except Logout)
causes the *next* test's session to cold-launch straight past the Login screen,
breaking every Page Object's assumption about where it starts. `noReset:false` costs
speed but guarantees determinism, which matters more here.

### Why Click/Type retry once on a stale element

A screen transition that follows a submit (e.g. Register auto-navigating to Login)
can recreate the view tree in the moment between `WaitForVisible` confirming an
element and the next line acting on it — the element was real and visible at the
check, then went stale before `SendKeys`/`Click` executed. This is a genuine race
against Android's own UI, not a bad locator: it surfaced during a full local run
(`Logout_FromSettings_ReturnsToLoginScreen`, once, non-reproducing on re-run).
`BasePage.Click`/`Type` now retry exactly once by re-running the whole
locate-and-act step against the settled screen — a real locator or app bug still
fails on the second attempt. This is deliberately narrower than a test-level retry
(see Limitations/Future Improvements below): it only ever re-locates a single
element, never re-runs a whole test.

### Why Allure results are written manually instead of via `[AllureNUnit]`

The `Allure.NUnit` package's `[AllureNUnit]` action attribute also manages a "test
container" concept for grouping fixture-level setup/teardown. In this exact
combination — NUnit 3.14, `NUnit3TestAdapter` 4.5, .NET 8, `Allure.NUnit` 2.15.0 — that
container stack gets torn down twice (once per fixture, once for the assembly's own
implicit outer suite), crashing the whole test host with `InvalidOperationException:
No container context is active` **after** every test had already finished and
passed/failed correctly. `Support/AllureReportWriter.cs` calls
`AllureLifecycle`'s `StartTestCase`/`StopTestCase`/`WriteTestCase` directly and never
opens a container, producing the identical `allure-results` JSON without the crash —
isolated in its own class so the one integration that already broke once lives in a
single, replaceable place.

### Why BrowserStack was removed entirely, not just the CI trigger

This project did have a working two-tier CI pipeline pointed at BrowserStack App
Automate instead of a local emulator: `DriverFactory` branched on
`Appium:UseBrowserStack` and built a `bstack:options` capability set instead of a
local-emulator one for exactly this reason, `AppiumServerManager` skipped starting a
local server in that mode, the APK was uploaded to BrowserStack fresh each run via
their REST API instead of being checked in anywhere, and `TestBase` reported the real
NUnit pass/fail outcome back to BrowserStack via their `browserstack_executor` API
(`Drivers/BrowserStackReporter.cs`) — without that call BrowserStack only knows the
Appium session didn't crash, not whether the assertions actually passed. Two
workflows split this into a fast tier (4 core tests, on demand) and a slow tier (the
full 32, once a day) — the same fast/slow split any CI/CD setup ends up wanting.

It worked: the GitHub Pages report's trend history (visible on the live site) is real
accumulated data from actual BrowserStack runs during development, not staged. The
free trial's App Automate plan carries a fixed, one-time 100-minute total budget
shared across every run — not a renewing monthly allowance — and normal iteration
(re-running to fix the flaky-relaunch and stale-element issues documented elsewhere in
this README) used it up. `BROWSERSTACK_TESTING_TIME_LIMIT_EXHAUSTED` is the exact
error BrowserStack returns once that budget hits zero, with no free path back to more
minutes.

First I removed just the two workflow files, keeping the `DriverFactory`/
`BrowserStackReporter` branch in the codebase as an opt-in local capability. On
reflection that was the wrong call: this project already has a documented principle
(see "What I deliberately did not add") of not keeping code around for a capability
nothing currently exercises — an untested, unreachable branch is exactly that, not a
convenience. With no BrowserStack account able to run it and no near-term plan to pay
for one, the entire integration is now removed: `DriverFactory` only ever builds a
local driver, `AppiumSettings` has no `BrowserStack*` properties,
`BrowserStackReporter.cs` is deleted, and `TestBase` no longer reports results
anywhere but Allure. The fully validated path is local execution against the emulator
(`dotnet test`, 32/32 passing — see Scope of Automation).

Bringing cloud-device execution back later is a rebuild, not a config flip — but a
small one: git history has the exact `DriverFactory`/`BrowserStackReporter`/workflow
code to start from, whether pointed back at BrowserStack under a paid plan or at a
different device cloud (Sauce Labs, etc. — see Future Improvements).

## What Wasn't Implemented / Limitations

- **There is no CI at all right now.** A working BrowserStack-backed CI pipeline
  existed and ran successfully during development (see Decisions and the report
  history on GitHub Pages) until the free trial's App Automate minutes were fully
  exhausted; the workflows were removed rather than left permanently failing. Running
  an Android emulator directly on GitHub-hosted runners (KVM/nested virtualization,
  cold-boot time, flakiness) is real, separate work that was never attempted, since
  BrowserStack was the CI-friendly substitute for exactly that problem.
- **Single device profile.** Everything is validated against one AVD (Pixel 8 / API
  35). No device farm, no matrix of screen sizes/OS versions.
- **Input locators are position-based, not id-based** — a real fix requires a change
  in the app itself (adding `AutomationId`), outside this repo's control.
- **No test-level retry policy.** A genuinely flaky test fails rather than re-running,
  by design — I'd rather see a flake than hide it. (`BasePage` does retry once on a
  `StaleElementReferenceException` specifically — see Decisions — but that's a narrow
  interaction-level safeguard, not a general retry policy.)
- **`noReset:false` makes the suite slower than it has to be** (~15-20s of app-reset
  overhead per test) in exchange for determinism — see Decisions for the bug that made
  this the safer default.
- **Allure result writing bypasses `Allure.NUnit`'s attribute pipeline** (see
  Decisions) — same report output, but if `Allure.NUnit` fixes the underlying
  container bug in a future release, this could likely be simplified back.
- **GitHub Pages is currently frozen on its last real CI run** — with no workflow
  publishing to it anymore, the live report reflects the last BrowserStack execution
  before the trial ran out, not the current state of the code (which is verified
  locally instead — see How to Run).

## Future Improvements

- **Restore CI and cloud-device execution** — rebuild the `DriverFactory`/
  `BrowserStackReporter`/workflow code from git history, pointed at either a paid
  BrowserStack plan (or a fresh trial) or a different device cloud (Sauce Labs, etc.)
  — see Decisions for why it was removed.
- **Run the full 32-test suite on every PR**, not just once a day, once CI is
  restored and a higher-capacity plan removes the quota concern.
- **Parallel execution** — fixtures are already independent (unique users, no shared
  state), so enabling NUnit's parallelism should be low-risk once the
  one-Appium-server-per-run assumption is revisited (a grid can host multiple
  concurrent sessions; the current single-local-server model would need one server per
  worker).
- **Retry strategy** — a bounded, logged retry for genuinely environment-flaky
  failures, distinguished from real assertion failures.
- **Test Data Builder** — `TestUserFactory` is a simple factory today; if test data
  needs grow (multiple user shapes, edge-case strings), a builder would keep that
  readable.
- **API integration** — if this app ever exposes a backend API, seeding/verifying
  through it directly would be faster and more reliable than always going through the
  UI for setup.
- **Accessibility testing** — an axe-based or similar scan per screen, especially
  relevant given the app currently has no accessibility identifiers at all.
