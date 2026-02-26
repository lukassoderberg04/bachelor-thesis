using System.Net;
using System.Net.Sockets;

namespace pm1000_streamer_service.API;

/// <summary>
/// Will serve the stokes parameters and the DOP.
/// Port 5000 --> serves stokes vectors: 5 * float32 + 1 UInt32 (S0, S1, S2, S3, DOP, TIME) aka 24 bytes.
/// </summary>
public static class StokesStreamer
{
    public static readonly int STOKES_PORT = 5000;

    public static readonly IPEndPoint Endpoint = new IPEndPoint(IPAddress.Loopback, STOKES_PORT);

    /// <summary>
    /// Start the Stokes streamer service.
    /// </summary>
    public static Task Start(CancellationToken token)
    {
        Logger.LogInfo("Starting Stokes server on a different thread...");

        return Task.Run(() => runStokesServer(token));
    }

    /// <summary>
    /// Configures the Stokes udp client and start sending data.
    /// </summary>
    private async static Task runStokesServer(CancellationToken token)
    {
        UdpClient client = new();
        
        while (!token.IsCancellationRequested)
        {
            var buffer = DataProvider.StokesPacket.GetBytes();

            await client.SendAsync(buffer, buffer.Length, Endpoint);
        }
    }
}
