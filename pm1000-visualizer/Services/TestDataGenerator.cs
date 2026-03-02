using System.IO;
using System.Net;
using System.Net.Sockets;
using pm1000_visualizer.Communication;

namespace pm1000_visualizer.Services;

/// <summary>
/// Generates synthetic Stokes and audio UDP packets for testing the full pipeline
/// without a real PM1000 device.
///
/// Sends to localhost on the configured ports so UdpListener picks them up normally.
///
/// Stokes: smooth orbit on the Poincaré sphere, S0 ≈ 15 µW, DOP ≈ 97 %
/// Raw audio:       noisy multi-tone signal (fundamental + harmonics + noise + bursts)
/// Processed audio: cleaner version with less noise — simulates filtering
/// </summary>
public class TestDataGenerator : IDisposable
{
    private System.Threading.Timer? _timer;
    private uint _sequence;
    private double _angle;
    private double _rawAudioPhase;
    private double _processedAudioPhase;
    private readonly Random _rng = new();

    // Slowly drifting parameters to make the signal evolve over time
    private double _fundamentalHz = 440;
    private double _amplitudeMod;
    private double _burstPhase;

    private readonly UdpClient _stokesSender = new();
    private readonly UdpClient _rawAudioSender = new();
    private readonly UdpClient _processedAudioSender = new();

    private readonly IPEndPoint _stokesEp;
    private readonly IPEndPoint _rawAudioEp;
    private readonly IPEndPoint _processedAudioEp;

    private const uint SAMPLE_RATE = 16000;
    private const int TICK_MS = 50;                // 20 packets/sec
    private const int AUDIO_BLOCK = 800;           // 50 ms at 16 kHz
    private const int STOKES_BLOCK = 16;           // 16 samples per packet

    public TestDataGenerator(int stokesPort = 5000, int rawAudioPort = 5001, int processedAudioPort = 5002)
    {
        _stokesEp = new IPEndPoint(IPAddress.Loopback, stokesPort);
        _rawAudioEp = new IPEndPoint(IPAddress.Loopback, rawAudioPort);
        _processedAudioEp = new IPEndPoint(IPAddress.Loopback, processedAudioPort);
    }

    public void Start()
    {
        _timer = new System.Threading.Timer(SendPackets, null, 0, TICK_MS);
        Logger.LogInfo("Test data generator started — sending to localhost.");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        Logger.LogInfo("Test data generator stopped.");
    }

    // ── Packet generation ──────────────────────────────────────────────────────

    private void SendPackets(object? state)
    {
        try
        {
            _angle += 0.05;
            _sequence++;

            // Slowly drift the fundamental frequency between ~300 and ~600 Hz
            _fundamentalHz += (_rng.NextDouble() - 0.5) * 8.0;
            _fundamentalHz = Math.Clamp(_fundamentalHz, 300, 600);

            // Amplitude modulation drifts slowly
            _amplitudeMod += 0.03;
            _burstPhase += 0.07;

            SendStokes();
            SendRawAudio();
            SendProcessedAudio();
        }
        catch (Exception ex)
        {
            Logger.LogError($"TestDataGenerator error: {ex.Message}");
        }
    }

    private void SendStokes()
    {
        using var ms = new MemoryStream(PacketDeserializer.HEADER_SIZE + STOKES_BLOCK * 20);
        using var w = new BinaryWriter(ms);

        w.Write(_sequence);
        w.Write(SAMPLE_RATE);
        w.Write((ushort)STOKES_BLOCK);

        for (int i = 0; i < STOKES_BLOCK; i++)
        {
            double a = _angle + (i / (double)STOKES_BLOCK) * Math.PI * 0.1;
            double s1 = Math.Sin(a) * Math.Cos(_angle * 0.3);
            double s2 = Math.Cos(a) * Math.Sin(_angle * 0.3);
            double s3 = Math.Sin(a * 0.5) * Math.Cos(_angle * 0.7);
            double len = Math.Sqrt(s1 * s1 + s2 * s2 + s3 * s3);

            float s0 = 15.2f + (float)(Math.Sin(_angle * 0.1) * 2.0); // 13–17 µW

            w.Write(s0);
            w.Write((float)(s1 / len));
            w.Write((float)(s2 / len));
            w.Write((float)(s3 / len));
            w.Write(0.972f); // DOP
        }

        var data = ms.ToArray();
        _stokesSender.Send(data, data.Length, _stokesEp);
    }

    private void SendRawAudio()
    {
        using var ms = new MemoryStream(PacketDeserializer.HEADER_SIZE + AUDIO_BLOCK * 4);
        using var w = new BinaryWriter(ms);

        w.Write(_sequence);
        w.Write(SAMPLE_RATE);
        w.Write((ushort)AUDIO_BLOCK);

        // Amplitude envelope: slow fade in/out with occasional bursts
        double envelope = 0.5 + 0.3 * Math.Sin(_amplitudeMod) + 0.2 * Math.Max(0, Math.Sin(_burstPhase * 3.7));

        for (int i = 0; i < AUDIO_BLOCK; i++)
        {
            // Fundamental + 2nd/3rd harmonics with varying mix
            double fundamental = Math.Sin(_rawAudioPhase);
            double harmonic2 = 0.35 * Math.Sin(_rawAudioPhase * 2.02);  // slight detune
            double harmonic3 = 0.15 * Math.Sin(_rawAudioPhase * 3.01);
            double noise = (_rng.NextDouble() * 2 - 1) * 0.15;          // white noise floor

            double sample = (fundamental + harmonic2 + harmonic3 + noise) * envelope;
            sample = Math.Clamp(sample, -1.0, 1.0);

            w.Write((float)sample);
            _rawAudioPhase += 2 * Math.PI * _fundamentalHz / SAMPLE_RATE;
            if (_rawAudioPhase > 2 * Math.PI) _rawAudioPhase -= 2 * Math.PI;
        }

        var data = ms.ToArray();
        _rawAudioSender.Send(data, data.Length, _rawAudioEp);
    }

    private void SendProcessedAudio()
    {
        using var ms = new MemoryStream(PacketDeserializer.HEADER_SIZE + AUDIO_BLOCK * 4);
        using var w = new BinaryWriter(ms);

        w.Write(_sequence);
        w.Write(SAMPLE_RATE);
        w.Write((ushort)AUDIO_BLOCK);

        // Processed = cleaner version: just fundamental + light harmonic, less noise
        double envelope = 0.6 + 0.25 * Math.Sin(_amplitudeMod * 1.1);

        for (int i = 0; i < AUDIO_BLOCK; i++)
        {
            double fundamental = Math.Sin(_processedAudioPhase);
            double harmonic2 = 0.12 * Math.Sin(_processedAudioPhase * 2.0);
            double noise = (_rng.NextDouble() * 2 - 1) * 0.03;  // much less noise

            double sample = (fundamental + harmonic2 + noise) * envelope;
            sample = Math.Clamp(sample, -1.0, 1.0);

            w.Write((float)sample);
            _processedAudioPhase += 2 * Math.PI * (_fundamentalHz * 1.0) / SAMPLE_RATE;
            if (_processedAudioPhase > 2 * Math.PI) _processedAudioPhase -= 2 * Math.PI;
        }

        var data = ms.ToArray();
        _processedAudioSender.Send(data, data.Length, _processedAudioEp);
    }

    public void Dispose()
    {
        Stop();
        _stokesSender.Dispose();
        _rawAudioSender.Dispose();
        _processedAudioSender.Dispose();
    }
}
