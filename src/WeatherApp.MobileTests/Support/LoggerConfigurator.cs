using Serilog;
using Serilog.Events;

namespace WeatherApp.MobileTests.Support;

/// <summary>
/// Configures the global Serilog logger once per test run: console output for live
/// feedback while a suite runs, plus a rolling file under /logs so a run's full history
/// survives after the terminal is gone (required by the "logs saved to file" checklist
/// item). Call Configure() once from the assembly-level SetUpFixture and Shutdown()
/// when the run ends so the file sink flushes.
/// </summary>
public static class LoggerConfigurator
{
    private static bool _configured;
    private static readonly object Gate = new();

    public static void Configure()
    {
        lock (Gate)
        {
            if (_configured)
            {
                return;
            }

            var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    Path.Combine(logsDirectory, "test-run-.log"),
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate:
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _configured = true;
            Log.Information("Logger initialized. Writing to {LogsDirectory}", logsDirectory);
        }
    }

    public static void Shutdown() => Log.CloseAndFlush();
}
