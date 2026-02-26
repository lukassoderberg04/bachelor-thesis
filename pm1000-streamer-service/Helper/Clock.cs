using System.Diagnostics;

namespace pm1000_streamer_service;

/// <summary>
/// Timer class for handling time since start of the application.
/// </summary>
public static class Clock
{
    private static readonly Stopwatch stopwatch = Stopwatch.StartNew();

    /// <summary>
    /// This will return the amount of milli-seconds that has passed
    /// since the start of this application. Just keep in mind that
    /// it will turn over and start at 0 every ~71 minutes.
    /// </summary>
    public static UInt32 GetMillisecondsFromStart()
    {
        const long mikroPerSecond = 1_000_000L;

        long microseconds = (stopwatch.ElapsedTicks * mikroPerSecond / Stopwatch.Frequency);

        return (UInt32)microseconds;
    }
}
