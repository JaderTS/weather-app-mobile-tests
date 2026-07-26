namespace WeatherApp.MobileTests.Config;

/// <summary>
/// Central timing configuration for WaitHelper. Every explicit wait in the framework
/// reads from here instead of hard-coding a duration inline.
/// </summary>
public sealed class TimeoutSettings
{
    public int DefaultWaitSeconds { get; set; } = 15;

    public int PollingIntervalMilliseconds { get; set; } = 250;

    /// <summary>
    /// Short wait for asserting an element never appears (negative scenarios).
    /// Kept separate from DefaultWaitSeconds so "proving absence" doesn't force
    /// every positive wait to also be slow.
    /// </summary>
    public int AbsenceWaitSeconds { get; set; } = 6;
}
