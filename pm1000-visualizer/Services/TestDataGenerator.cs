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
/// Raw audio:       440 Hz sine (A4)
/// Processed audio: 880 Hz sine (A5) — distinct from raw so you can see the difference
/// </summary>
public class TestDataGenerator : IDisposable
{
    private System.Threading.Timer? _timer;
    private uint _sequence;
    private double _angle;
    private double _rawAudioPhase;
    private double _processedAudioPhase;

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
            SendStokes();
            SendAudio(_rawAudioSender, _rawAudioEp, 440, ref _rawAudioPhase);
            SendAudio(_processedAudioSender, _processedAudioEp, 880, ref _processedAudioPhase);
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

    private void SendAudio(UdpClient client, IPEndPoint ep, double frequency, ref double phase)
    {
        using var ms = new MemoryStream(PacketDeserializer.HEADER_SIZE + AUDIO_BLOCK * 4);
        using var w = new BinaryWriter(ms);

        w.Write(_sequence);
        w.Write(SAMPLE_RATE);
        w.Write((ushort)AUDIO_BLOCK);

        for (int i = 0; i < AUDIO_BLOCK; i++)
        {
            w.Write((float)Math.Sin(phase));
            phase += 2 * Math.PI * frequency / SAMPLE_RATE;
            if (phase > 2 * Math.PI) phase -= 2 * Math.PI;
        }

        var data = ms.ToArray();
        client.Send(data, data.Length, ep);
    }

    public void Dispose()
    {
        Stop();
        _stokesSender.Dispose();
        _rawAudioSender.Dispose();
        _processedAudioSender.Dispose();
    }
}
