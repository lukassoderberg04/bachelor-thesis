using System.Net;
using System.Net.Sockets;

namespace pm1000_streamer_service.Streaming;

/// <summary>
/// Sends Stokes data from PM1000 over UDP to port 5000.
/// Audio (port 5001) is Ludwig's responsibility – not sent from here.
/// Call AddTarget() for each machine that should receive the data.
/// </summary>
public class UdpBroadcaster : IDisposable
{
    public const int STOKES_PORT = 5000;

    private readonly UdpClient _client = new();
    private readonly List<IPEndPoint> _targets = new();
    private uint _seq = 0;

    /// <summary>Adds a receiver. Stokes data will be sent to ip:5000.</summary>
    public void AddTarget(string ip)
    {
        _targets.Add(new IPEndPoint(IPAddress.Parse(ip), STOKES_PORT));
        Logger.LogInfo($"UDP target added: {ip}:{STOKES_PORT}");
    }

    public void SendStokes(StokeSample[] samples, uint sampleRateHz)
    {
        var payload = PacketSerializer.SerializeStokes(samples, _seq++, sampleRateHz);
        foreach (var target in _targets)
        {
            try { _client.Send(payload, payload.Length, target); }
            catch (Exception ex) { Logger.LogError($"UDP send failed to {target}: {ex.Message}"); }
        }
    }

    public void Dispose() => _client.Dispose();
}
