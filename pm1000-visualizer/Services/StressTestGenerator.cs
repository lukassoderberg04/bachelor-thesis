using System.IO;
using System.Net;
using System.Net.Sockets;
using pm1000_visualizer.Communication;

namespace pm1000_visualizer.Services;

/// <summary>
/// High-volume stress test that uses the standard header+payload wire format:
///   Header (10 bytes): uint32 sequence_nr, uint32 sample_rate_hz, uint16 block_size
///   Stokes sample (20 bytes): float S0, S1, S2, S3, DOP
///   Audio sample   (4 bytes): float amplitude
///
/// This is the same format as TestDataGenerator, so it exercises the exact same
/// parsing path (TryDeserializeStokes / TryDeserializeAudio) including sequence-number
/// checking and packet-loss detection.
///
/// One sample per datagram (block_size = 1) so the packet rate equals the sample rate,
/// maximising scheduler and queue pressure on the visualizer.
/// </summary>
public class StressTestGenerator : IDisposable
{
    private readonly int _stokesPort;
    private readonly int _rawAudioPort;
    private readonly int _stokesPerSecond;
    private readonly int _audioPerSecond;

    private UdpClient? _stokesSender;
    private UdpClient? _audioSender;
    private CancellationTokenSource? _cts;
    private Task? _stokesTask;
    private Task? _audioTask;

    private readonly Random _rng = new();

    /// <summary>
    /// Creates a new stress-test generator.
    /// </summary>
    /// <param name="stokesPort">UDP port for Stokes data (default 5000).</param>
    /// <param name="rawAudioPort">UDP port for raw audio data (default 5001).</param>
    /// <param name="stokesPerSecond">Stokes samples (packets) per second.</param>
    /// <param name="audioPerSecond">Audio samples (packets) per second.</param>
    public StressTestGenerator(int stokesPort, int rawAudioPort, int stokesPerSecond, int audioPerSecond)
    {
        _stokesPort = stokesPort;
        _rawAudioPort = rawAudioPort;
        _stokesPerSecond = stokesPerSecond;
        _audioPerSecond = audioPerSecond;
    }

    public void Start()
    {
        _stokesSender = new UdpClient();
        _audioSender = new UdpClient();
        _cts = new CancellationTokenSource();

        _stokesTask = Task.Run(() => SendLoop(
            _stokesSender,
            new IPEndPoint(IPAddress.Loopback, _stokesPort),
            _stokesPerSecond,
            BuildStokesDatagram,
            _cts.Token));

        _audioTask = Task.Run(() => SendLoop(
            _audioSender,
            new IPEndPoint(IPAddress.Loopback, _rawAudioPort),
            _audioPerSecond,
            BuildAudioDatagram,
            _cts.Token));

        Logger.LogInfo($"Stress test started — Stokes: {_stokesPerSecond}/s → :{_stokesPort}, " +
                       $"Audio: {_audioPerSecond}/s → :{_rawAudioPort}");
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _stokesTask?.Wait(500); } catch { }
        try { _audioTask?.Wait(500); } catch { }
        _stokesSender?.Dispose();
        _audioSender?.Dispose();
        _stokesSender = null;
        _audioSender = null;
        Logger.LogInfo("Stress test stopped.");
    }

    public void Dispose() => Stop();

    // ── Send loop ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends packets at the target rate using a spin-yield timing approach.
    /// Groups sends into small bursts per millisecond for efficiency.
    /// </summary>
    private static async Task SendLoop(
        UdpClient sender,
        IPEndPoint ep,
        int packetsPerSecond,
        Func<uint, double, byte[]> buildPacket,
        CancellationToken ct)
    {
        if (packetsPerSecond <= 0) return;

        uint time = 0;
        double phase = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long sentCount = 0;

        while (!ct.IsCancellationRequested)
        {
            // How many packets should have been sent by now?
            double elapsedSec = sw.Elapsed.TotalSeconds;
            long targetCount = (long)(elapsedSec * packetsPerSecond);

            if (sentCount >= targetCount)
            {
                // We're ahead — yield briefly
                await Task.Delay(1, ct).ConfigureAwait(false);
                continue;
            }

            // Send a burst to catch up (cap burst to avoid huge spikes on resume)
            int burst = (int)Math.Min(targetCount - sentCount, 200);
            for (int i = 0; i < burst; i++)
            {
                var data = buildPacket(time++, phase);
                try { sender.Send(data, data.Length, ep); }
                catch (ObjectDisposedException) { return; }
                catch { /* best effort */ }

                phase += 2 * Math.PI / packetsPerSecond * 20; // ~20 Hz visible motion
                if (phase > 2 * Math.PI) phase -= 2 * Math.PI;
                sentCount++;
            }
        }
    }

    // ── Packet builders (standard header+payload format) ─────────────────────
    //
    // Wire layout — same as TestDataGenerator, same as what TryDeserializeStokes
    // and TryDeserializeAudio expect:
    //
    //   Header  (10 bytes): uint32 seq, uint32 sampleRateHz, uint16 blockSize
    //   Stokes  (20 bytes): float S0, S1, S2, S3, DOP   (blockSize = 1)
    //   Audio   ( 4 bytes): float amplitude              (blockSize = 1)
    //
    // Using blockSize=1 means 1 sample per datagram, so packet rate == sample rate
    // and every datagram has its own sequence number — maximising pressure on the
    // UdpListener's CheckSequence and the visualizer's queue.

    private const uint STOKES_SAMPLE_RATE = 500;   // Hz — reported in header
    private const uint AUDIO_SAMPLE_RATE = 16000; // Hz — reported in header

    /// <summary>
    /// 30 bytes: 10-byte header + 20-byte Stokes sample (blockSize = 1)
    /// </summary>
    private byte[] BuildStokesDatagram(uint seq, double phase)
    {
        // Orbit on the Poincaré sphere with light noise
        double a = phase;
        double s1 = Math.Sin(a) * Math.Cos(a * 0.37);
        double s2 = Math.Cos(a) * Math.Sin(a * 0.53);
        double s3 = Math.Sin(a * 0.71);
        double len = Math.Sqrt(s1 * s1 + s2 * s2 + s3 * s3);
        if (len < 1e-6) len = 1;

        float S0 = 15.0f + (float)(_rng.NextDouble() * 2 - 1);
        float S1 = (float)(s1 / len + (_rng.NextDouble() - 0.5) * 0.02);
        float S2 = (float)(s2 / len + (_rng.NextDouble() - 0.5) * 0.02);
        float S3 = (float)(s3 / len + (_rng.NextDouble() - 0.5) * 0.02);
        float DOP = 0.97f + (float)(_rng.NextDouble() * 0.02 - 0.01);

        // Header (10 bytes)
        using var ms = new System.IO.MemoryStream(PacketDeserializer.HEADER_SIZE + 20);
        using var w = new System.IO.BinaryWriter(ms);
        w.Write(seq);             // uint32 sequence_nr
        w.Write(STOKES_SAMPLE_RATE); // uint32 sample_rate_hz
        w.Write((ushort)1);       // uint16 block_size = 1
        // Sample (20 bytes)
        w.Write(S0); w.Write(S1); w.Write(S2); w.Write(S3); w.Write(DOP);
        return ms.ToArray();
    }

    private double _audioEnvelope = 8000;     // starts at ~quarter of Int16 max
    private double _envelopeDir = 1;

    /// <summary>
    /// 14 bytes: 10-byte header + 4-byte audio sample (blockSize = 1).
    /// Amplitude is in Int16 range (−32768 to +32767), matching the real streamer.
    /// Envelope drifts slowly so the waveform visibly grows and shrinks over time.
    /// </summary>
    private byte[] BuildAudioDatagram(uint seq, double phase)
    {
        // Drift envelope between ~500 and ~30000 so you can see it change on the plot
        _audioEnvelope += _envelopeDir * 15;
        if (_audioEnvelope > 30000) { _audioEnvelope = 30000; _envelopeDir = -1; }
        else if (_audioEnvelope < 500) { _audioEnvelope = 500; _envelopeDir = 1; }

        // Sine wave in raw Int16 range, like the real streamer (float cast of Int16)
        float amplitude = (float)(Math.Sin(phase) * _audioEnvelope
                          + (_rng.NextDouble() - 0.5) * 200);

        // Header (10 bytes)
        using var ms = new System.IO.MemoryStream(PacketDeserializer.HEADER_SIZE + 4);
        using var w = new System.IO.BinaryWriter(ms);
        w.Write(seq);               // uint32 sequence_nr
        w.Write(AUDIO_SAMPLE_RATE); // uint32 sample_rate_hz
        w.Write((ushort)1);         // uint16 block_size = 1
        // Sample (4 bytes)
        w.Write(amplitude);
        return ms.ToArray();
    }
}
