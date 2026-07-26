using Allure.Net.Commons;
using NUnit.Framework.Interfaces;
using OpenQA.Selenium.Appium.Android;
using Serilog;
using WeatherApp.MobileTests.Config;
using WeatherApp.MobileTests.Core;
using WeatherApp.MobileTests.Drivers;
using WeatherApp.MobileTests.Support;

namespace WeatherApp.MobileTests.Tests;

/// <summary>
/// Common per-test lifecycle for every fixture: fresh driver session in SetUp,
/// failure screenshot + driver teardown + Allure result in TearDown. Fixtures inherit
/// this instead of each re-implementing driver creation/disposal and failure handling.
///
/// Allure results are written by calling Allure.Net.Commons' AllureLifecycle directly
/// (StartTestCase/StopTestCase/WriteTestCase) rather than through the Allure.NUnit
/// package's [AllureNUnit] action attribute. That attribute also manages a "test
/// container" for grouping fixture-level setup/teardown, and in this exact
/// NUnit 3.14 / NUnit3TestAdapter 4.5 / .NET 8 combination its container stack gets
/// closed twice - once per fixture and once for the assembly's outer implicit suite -
/// crashing the whole test host with "No container context is active" after every test
/// had already finished. Driving AllureLifecycle's test-case API directly (no
/// containers) writes the exact same allure-results JSON without going through that
/// code path.
/// </summary>
public abstract class TestBase
{
    protected AndroidDriver Driver { get; private set; } = null!;
    protected WaitHelper Wait { get; private set; } = null!;

    private long _testStartedAtEpochMs;

    /// <summary>Runs exactly once for the whole assembly, the first time any fixture touches this class.</summary>
    static TestBase()
    {
        LoggerConfigurator.Configure();
        AppiumServerManager.StartIfConfigured();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            AppiumServerManager.StopIfStarted();
            LoggerConfigurator.Shutdown();
        };
    }

    [SetUp]
    public void BaseSetUp()
    {
        _testStartedAtEpochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Driver = DriverFactory.CreateAndroidDriver();
        Wait = new WaitHelper(Driver, ConfigurationProvider.Settings.Timeouts);
    }

    [TearDown]
    public void BaseTearDown()
    {
        var context = TestContext.CurrentContext;
        var result = context.Result;
        var failed = result.Outcome.Status == TestStatus.Failed;

        if (failed)
        {
            Log.Error("Test failed: {TestName} - {Message}", context.Test.Name, result.Message);
        }

        var testResult = new TestResult
        {
            uuid = Guid.NewGuid().ToString(),
            name = context.Test.Name,
            fullName = context.Test.FullName,
            historyId = context.Test.FullName,
            status = failed ? Status.failed : Status.passed,
            statusDetails = new StatusDetails { message = result.Message, trace = result.StackTrace },
            start = _testStartedAtEpochMs,
            stop = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            labels = [Label.Suite(GetType().Name)],
        };

        AllureLifecycle.Instance.StartTestCase(testResult);

        if (failed && Driver is not null)
        {
            try
            {
                ScreenshotHelper.Capture(Driver, context.Test.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to capture failure screenshot");
            }
        }

        AllureLifecycle.Instance.StopTestCase();
        AllureLifecycle.Instance.WriteTestCase();

        Driver?.Quit();
        Driver?.Dispose();
    }
}
