using System.IO;
using pm1000_visualizer.Models;

namespace pm1000_visualizer.Services;

/// <summary>
/// Saves measurement session data to disk.
///
/// File layout in timestamped folder:
///   2026-02-20_14-35-22/
///   ├── stokes.csv          (100 ms intervals: timestamp_ms, S0_uW, S1, S2, S3, DOP)
///   ├── audio_raw.wav       (PCM 16-bit mono WAV)
///   └── audio_processed.wav (PCM 16-bit mono WAV)
/// </summary>
public static class FileSaver
{
    /// <summary>
    /// Saves all session data into a timestamped sub-folder under <paramref name="basePath"/>.
    /// Returns the full path of the created folder.
    /// </summary>
    public static string SaveAll(MeasurementSession session, string basePath)
    {
        string folder = Path.Combine(basePath, session.StartTime.ToString("yyyy-MM-dd_HH-mm-ss"));
        Directory.CreateDirectory(folder);

        SaveStokesCsv(session, Path.Combine(folder, "stokes.csv"));
        SaveWav(session.RawAudioSamples, session.RawAudioSampleRate, Path.Combine(folder, "audio_raw.wav"));
        SaveWav(session.ProcessedAudioSamples, session.ProcessedAudioSampleRate, Path.Combine(folder, "audio_processed.wav"));

        Logger.LogInfo($"All files saved to {folder}");
        return folder;
    }

    /// <summary>
    /// Saves Stokes parameters to CSV, down-sampled to ~100 ms intervals.
    /// </summary>
    public static void SaveStokesCsv(MeasurementSession session, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("timestamp_ms,S0_uW,S1,S2,S3,DOP");

        long lastTimestamp = -100;
        foreach (var entry in session.StokesData)
        {
            if (entry.TimestampMs - lastTimestamp >= 100)
            {
                writer.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4}",
                    entry.TimestampMs, entry.Sample.S0, entry.Sample.S1,
                    entry.Sample.S2, entry.Sample.S3, entry.Sample.Dop));
                lastTimestamp = entry.TimestampMs;
            }
        }

        Logger.LogInfo($"Stokes CSV saved: {path} ({session.StokesData.Count} total samples)");
    }

    /// <summary>
    /// Writes a PCM 16-bit mono WAV file from float samples (expected range -1..+1).
    /// </summary>
    public static void SaveWav(List<float> samples, uint sampleRate, string path)
    {
        if (samples.Count == 0)
        {
            Logger.LogWarning($"No audio samples to save for {path}");
            return;
        }

        using var fs = new FileStream(path, FileMode.Create);
        using var w = new BinaryWriter(fs);

        int bitsPerSample = 16;
        int bytesPerSample = bitsPerSample / 8;
        int dataSize = samples.Count * bytesPerSample;

        // RIFF header
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataSize);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);                               // chunk size
        w.Write((short)1);                         // PCM format
        w.Write((short)1);                         // mono
        w.Write((int)sampleRate);                  // sample rate
        w.Write((int)(sampleRate * bytesPerSample));// byte rate
        w.Write((short)bytesPerSample);            // block align
        w.Write((short)bitsPerSample);             // bits per sample

        // data chunk
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);
        foreach (var sample in samples)
        {
            short pcm = (short)(Math.Clamp(sample, -1f, 1f) * 32767);
            w.Write(pcm);
        }

        Logger.LogInfo($"WAV saved: {path} ({samples.Count} samples, {sampleRate} Hz)");
    }
}
