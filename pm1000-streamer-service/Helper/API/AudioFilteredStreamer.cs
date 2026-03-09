using pm1000_streamer_service.Filter;
using System.Net;
using System.Net.Sockets;

namespace pm1000_streamer_service.API;

/// <summary>
/// Will serve the audio as an amplitude together with time. The audio will be converted
/// from the stokes vectors to audio packets.
/// Port 5003 --> serves audio: 1 * float32 + 1 * UInt32 (AMPLITUDE, TIME) aka 8 bytes.
/// </summary>
public static class AudioFilteredStreamer
{
    public static readonly int AUDIO_PORT = 5003;

    public static readonly IPEndPoint Endpoint = new IPEndPoint(IPAddress.Loopback, AUDIO_PORT);

    private static IFilter filter = new NoFilter();

    /// <summary>
    /// Start the Audio Filtered streamer service.
    /// </summary>
    public static Task Start(IFilter filter, CancellationToken token)
    {
        Logger.LogInfo("Starting Audio Filtered server on a different thread...");

        return Task.Run(() => runAudioFilteredServer(token));
    }

    /// <summary>
    /// Configures the Audio Filtered udp client and start sending data. Only runs when
    /// there's packets to be sent.
    /// </summary>
    private async static Task runAudioFilteredServer(CancellationToken token)
    {
        UdpClient client = new();

        await foreach (var packet in DataProvider.GetAllStokesToFilterPacketsAsync())
        {
            var audioPacket = filter.ProcessStokesPacket(packet);

            var buffer = audioPacket.GetBytes();

            await client.SendAsync(buffer, buffer.Length, Endpoint);
        }
    }
}