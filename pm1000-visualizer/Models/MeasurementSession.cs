using pm1000_visualizer.Communication;

namespace pm1000_visualizer.Models;

/// <summary>
/// Holds all data recorded during a measurement session.
/// </summary>
public class MeasurementSession
{
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }

    // Connection info
    public string StreamerIp { get; set; } = "";
    public int ApiPort { get; set; }
    public double? FrequencyHz { get; set; }
    public uint SampleRateHz { get; set; } = 16000;

    // Duration config
    public bool IsIndefinite { get; set; } = true;
    public int DurationSeconds { get; set; } = 30;

    // Recorded Stokes data (every sample, for playback)
    public List<TimestampedStokes> StokesData { get; } = new();

    // Recorded audio (all samples, for WAV export)
    public List<float> RawAudioSamples { get; } = new();
    public List<float> ProcessedAudioSamples { get; } = new();

    public uint RawAudioSampleRate { get; set; } = 16000;
    public uint ProcessedAudioSampleRate { get; set; } = 16000;

    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;
}

public record struct TimestampedStokes(long TimestampMs, StokeSample Sample);
