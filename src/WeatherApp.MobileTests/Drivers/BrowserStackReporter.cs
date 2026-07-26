using System.Text.Json;
using OpenQA.Selenium;

namespace WeatherApp.MobileTests.Drivers;

/// <summary>
/// Reports the NUnit assertion outcome back to BrowserStack via their
/// `browserstack_executor` custom command. Without this call, BrowserStack only knows
/// whether the Appium session completed without a WebDriver-level crash - it has no
/// visibility into whether the test's assertions actually passed, so every session
/// shows up with an unmarked/zeroed-out status in their dashboard regardless of the
/// real result (confirmed by hand: a passing NUnit test still showed "0 passed" there
/// until this was added).
/// </summary>
public static class BrowserStackReporter
{
    public static void ReportTestResult(WebDriver driver, bool failed, string? reason)
    {
        var payload = new
        {
            action = "setSessionStatus",
            arguments = new
            {
                status = failed ? "failed" : "passed",
                reason = string.IsNullOrWhiteSpace(reason)
                    ? (failed ? "Assertion failed" : "All assertions passed")
                    : reason,
            },
        };

        var script = "browserstack_executor: " + JsonSerializer.Serialize(payload);
        ((IJavaScriptExecutor)driver).ExecuteScript(script);
    }
}
