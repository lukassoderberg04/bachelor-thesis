namespace pm1000_visualizer.Models;

/// <summary>
/// Settings gathered from the Pre-Connection page.
/// </summary>
public class ConnectionSettings
{
    public string StreamerIp { get; set; } = "127.0.0.1";
    public int StokesPort { get; set; } = 5000;
    public int RawAudioPort { get; set; } = 5001;
    public int ProcessedAudioPort { get; set; } = 5002;
    public bool IsTestMode { get; set; }
    public bool IsStressTest { get; set; }
    public int StressStokesPerSecond { get; set; } = 1000;
    public int StressAudioPerSecond { get; set; } = 16000;
    public bool IsIndefinite { get; set; } = true;
    public int DurationSeconds { get; set; } = 30;
}
