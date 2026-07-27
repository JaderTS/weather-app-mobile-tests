using System.Diagnostics;
using Serilog;
using WeatherApp.MobileTests.Config;

namespace WeatherApp.MobileTests.Drivers;

/// <summary>
/// Optionally starts and stops a local Appium server for the test run, so contributors
/// don't have to remember to run `appium` in a separate terminal. Controlled by
/// Appium:ManageServerLifecycle - set it to false in appsettings.local.json if you'd
/// rather point ServerUrl at an Appium server you already manage yourself (e.g. in CI,
/// or a remote/cloud grid).
///
/// This spawns the `appium` process directly and polls its REST /status endpoint for
/// readiness, rather than using the Appium .NET client's built-in AppiumLocalService.
/// That built-in helper detects readiness by matching specific text in the server's
/// stdout, and that text changed between the Appium version it was written against and
/// Appium 2.19 (used here) - it never matched, so startup always timed out. Polling the
/// documented /status endpoint is the version-stable way to ask "is the server up".
/// </summary>
public static class AppiumServerManager
{
    private static Process? _process;
    private static readonly object Gate = new();

    public static void StartIfConfigured()
    {
        var settings = ConfigurationProvider.Settings.Appium;

        if (!settings.ManageServerLifecycle)
        {
            Log.Information("Appium server lifecycle is externally managed; skipping auto-start.");
            return;
        }

        lock (Gate)
        {
            if (_process is { HasExited: false })
            {
                return;
            }

            var serverUri = new Uri(settings.ServerUrl);
            Log.Information("Starting local Appium server on {Host}:{Port}", serverUri.Host, serverUri.Port);

            var startInfo = new ProcessStartInfo
            {
                FileName = "appium",
                Arguments = $"--port {serverUri.Port} --address {serverUri.Host}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            EnsureAndroidSdkEnvironment(startInfo);

            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) Log.Debug("[appium] {Line}", e.Data);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) Log.Debug("[appium] {Line}", e.Data);
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            WaitForServerReady(settings.ServerUrl, TimeSpan.FromSeconds(settings.ServerStartupTimeoutSeconds));

            Log.Information("Appium server started.");
        }
    }

    /// <summary>
    /// The UiAutomator2 driver needs ANDROID_HOME/ANDROID_SDK_ROOT to locate adb and
    /// the emulator tools. A machine with Android Studio installed usually has these
    /// exported in the interactive shell profile, but a spawned child process only
    /// inherits what's actually in the current process's environment - which can be
    /// empty even though `adb`/`emulator` work fine on PATH (as was the case here).
    /// Fall back to the conventional per-OS install location so the framework works
    /// out of the box; appsettings.local.json can still override PATH-level setup.
    /// </summary>
    private static void EnsureAndroidSdkEnvironment(ProcessStartInfo startInfo)
    {
        var alreadySet = Environment.GetEnvironmentVariable("ANDROID_HOME")
            ?? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");

        var sdkPath = alreadySet ?? GetDefaultSdkPathForCurrentOs();

        if (sdkPath is null || !Directory.Exists(sdkPath))
        {
            Log.Warning(
                "Could not locate an Android SDK (ANDROID_HOME/ANDROID_SDK_ROOT not set and no default " +
                "SDK folder found). The Appium server will likely fail to start a session.");
            return;
        }

        startInfo.Environment["ANDROID_HOME"] = sdkPath;
        startInfo.Environment["ANDROID_SDK_ROOT"] = sdkPath;

        if (alreadySet is null)
        {
            Log.Information("ANDROID_HOME was not exported; defaulting to {SdkPath} for the Appium server process.", sdkPath);
        }
    }

    private static string? GetDefaultSdkPathForCurrentOs()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Android", "sdk");
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Android", "Sdk");
        }

        return Path.Combine(home, "Android", "Sdk");
    }

    private static void WaitForServerReady(string serverUrl, TimeSpan timeout)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var statusUrl = new Uri(new Uri(serverUrl), "status");
        var deadline = DateTime.UtcNow + timeout;
        var pollingInterval = TimeSpan.FromMilliseconds(500);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = httpClient.GetAsync(statusUrl).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception)
            {
                // Server socket not accepting connections yet - keep polling until the deadline.
            }

            Thread.Sleep(pollingInterval);
        }

        throw new TimeoutException(
            $"Appium server at {statusUrl} did not become ready within {timeout.TotalSeconds}s.");
    }

    public static void StopIfStarted()
    {
        lock (Gate)
        {
            if (_process is null)
            {
                return;
            }

            Log.Information("Stopping local Appium server.");

            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }

            _process.Dispose();
            _process = null;
        }
    }
}
