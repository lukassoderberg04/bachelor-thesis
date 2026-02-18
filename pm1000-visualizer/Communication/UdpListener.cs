using System.Net;
using System.Net.Sockets;
using pm1000_visualizer;

namespace pm1000_visualizer.Communication;

/// <summary>
/// Listens for incoming UDP data and fires events when packets arrive.
///
///   Port 5000 → Stokes raw data   (from Lukas streamer-service)
///   Port 5001 → Processed audio   (from Ludwig signal processing)
///
/// Usage:
///   var listener = new UdpListener();
///   listener.StokesReceived += packet => { /* update Poincaré plot */ };
///   listener.AudioReceived  += packet => { /* update audio waveform */ };
///   listener.Start();
///   ...  
///   listener.Stop();
/// </summary>
public class UdpListener : IDisposable
{
    public const int STOKES_PORT = 5000;
    public const int AUDIO_PORT = 5001;

    private UdpClient? _stokesClient;
    private UdpClient? _audioClient;
    private bool _running = false;

    public event Action<StokesPacket>? StokesReceived;
    public event Action<AudioPacket>? AudioReceived;
    public event Action<uint, uint>? PacketDropped; // (expected, got)

    private uint _lastStokesSeq = uint.MaxValue;
    private uint _lastAudioSeq = uint.MaxValue;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _stokesClient = new UdpClient(new IPEndPoint(IPAddress.Any, STOKES_PORT));
        _audioClient = new UdpClient(new IPEndPoint(IPAddress.Any, AUDIO_PORT));
        Task.Run(() => ReceiveLoop(_stokesClient, isStokes: true));
        Task.Run(() => ReceiveLoop(_audioClient, isStokes: false));
        Logger.LogInfo($"UDP listener started (Stokes:{STOKES_PORT}, Audio:{AUDIO_PORT}).");
    }

    public void Stop()
    {
        _running = false;
        _stokesClient?.Close();
        _audioClient?.Close();
        Logger.LogInfo("UDP listener stopped.");
    }

    private async Task ReceiveLoop(UdpClient client, bool isStokes)
    {
        while (_running)
        {
            try
            {
                var data = (await client.ReceiveAsync()).Buffer;
                if (isStokes)
                {
                    var packet = PacketDeserializer.TryDeserializeStokes(data);
                    if (packet == null) continue;
                    CheckSequence(packet.SequenceNr, ref _lastStokesSeq);
                    StokesReceived?.Invoke(packet);
                }
                else
                {
                    var packet = PacketDeserializer.TryDeserializeAudio(data);
                    if (packet == null) continue;
                    CheckSequence(packet.SequenceNr, ref _lastAudioSeq);
                    AudioReceived?.Invoke(packet);
                }
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { Logger.LogError($"UDP receive error: {ex.Message}"); }
        }
    }

    private void CheckSequence(uint received, ref uint last)
    {
        if (last != uint.MaxValue && received != last + 1)
            PacketDropped?.Invoke(last + 1, received);
        last = received;
    }

    public void Dispose() => Stop();
}
