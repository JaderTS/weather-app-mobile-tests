using Allure.Net.Commons;

namespace WeatherApp.MobileTests.Support;

/// <summary>
/// The only place that touches Allure.Net.Commons' AllureLifecycle API directly.
/// Isolating it here - instead of inlining StartTestCase/StopTestCase/WriteTestCase
/// calls in TestBase - means the one Allure-specific integration this framework
/// depends on (which already broke once this session, see TestBase's remarks on why
/// [AllureNUnit] isn't used) lives in a single, small, replaceable class.
///
/// Split into Start/StopAndWrite rather than one call because the failure screenshot
/// must be captured *between* them to be attached to the right test case (AllureApi's
/// attachment call operates on whichever test case is currently open).
/// </summary>
public static class AllureReportWriter
{
    public static void StartTestCase(
        string suiteName,
        string testName,
        string fullName,
        bool failed,
        string? failureMessage,
        string? stackTrace,
        long startedAtEpochMs)
    {
        var testResult = new TestResult
        {
            uuid = Guid.NewGuid().ToString(),
            name = testName,
            fullName = fullName,
            historyId = fullName,
            status = failed ? Status.failed : Status.passed,
            statusDetails = new StatusDetails { message = failureMessage, trace = stackTrace },
            start = startedAtEpochMs,
            stop = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            labels = [Label.Suite(suiteName)],
        };

        AllureLifecycle.Instance.StartTestCase(testResult);
    }

    public static void StopAndWriteTestCase()
    {
        AllureLifecycle.Instance.StopTestCase();
        AllureLifecycle.Instance.WriteTestCase();
    }
}
