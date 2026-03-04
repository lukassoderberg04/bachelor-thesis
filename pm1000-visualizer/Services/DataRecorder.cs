using pm1000_visualizer.Communication;
using pm1000_visualizer.Models;

namespace pm1000_visualizer.Services;

/// <summary>
/// Records incoming Stokes and audio data into a MeasurementSession.
/// Thread-safe — called from background UDP receive threads.
/// </summary>
public class DataRecorder
{
    private readonly MeasurementSession _session;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private readonly object _lock = new();
    private volatile bool _stopped;

    public DataRecorder(MeasurementSession session)
    {
        _session = session;
    }

    public void Start()
    {
        _session.StartTime = DateTime.Now;
        _stopwatch.Start();
    }

    public void Stop()
    {
        _stopped = true;   // block further Record calls FIRST
        _stopwatch.Stop();
        _session.EndTime = DateTime.Now;
    }

    public long ElapsedMs => _stopwatch.ElapsedMilliseconds;

    public void RecordStokes(StokesPacket packet)
    {
        if (_stopped) return;
        long ms = _stopwatch.ElapsedMilliseconds;
        lock (_lock)
        {
            _session.SampleRateHz = packet.SampleRateHz;
            foreach (var sample in packet.Samples)
                _session.StokesData.Add(new TimestampedStokes(ms, sample));
        }
    }

    public void RecordRawAudio(AudioPacket packet)
    {
        if (_stopped) return;
        lock (_lock)
        {
            _session.RawAudioSampleRate = packet.SampleRateHz;
            _session.RawAudioSamples.AddRange(packet.Samples);
        }
    }

    public void RecordProcessedAudio(AudioPacket packet)
    {
        if (_stopped) return;
        lock (_lock)
        {
            _session.ProcessedAudioSampleRate = packet.SampleRateHz;
            _session.ProcessedAudioSamples.AddRange(packet.Samples);
        }
    }
}
