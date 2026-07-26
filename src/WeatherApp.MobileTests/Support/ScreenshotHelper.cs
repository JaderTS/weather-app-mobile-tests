using Allure.Net.Commons;
using OpenQA.Selenium;
using Serilog;

namespace WeatherApp.MobileTests.Support;

/// <summary>
/// Captures a screenshot on test failure, saves it under /Screenshots for local
/// inspection, and attaches it to the Allure report so a failure can be diagnosed
/// straight from the report without re-running anything.
/// </summary>
public static class ScreenshotHelper
{
    public static string Capture(WebDriver driver, string testName)
    {
        var screenshotsDirectory = Path.Combine(AppContext.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(screenshotsDirectory);

        var safeName = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var filePath = Path.Combine(screenshotsDirectory, fileName);

        var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
        screenshot.SaveAsFile(filePath);

        Log.Warning("Captured failure screenshot: {FilePath}", filePath);

        AllureApi.AddAttachment(filePath, "image/png");

        return filePath;
    }
}
