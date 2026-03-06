using pm1000_streamer_service.PM1000;
using System.Net;
using System.Net.Sockets;

namespace pm1000_streamer_service.API;

/// <summary>
/// Will serve the audio as an amplitude together with time.
/// Port 5001 --> serves audio: 1 * float32 + 1 * UInt32 (AMPLITUDE, TIME) aka 8 bytes.
/// </summary>
public static class AudioStreamer
{
    public static readonly int AUDIO_PORT = 5001;

    public static readonly IPEndPoint Endpoint = new IPEndPoint(IPAddress.Loopback, AUDIO_PORT);

    /// <summary>
    /// Start the Audio streamer service.
    /// </summary>
    public static Task Start(CancellationToken token)
    {
        Logger.LogInfo("Starting Audio server on a different thread...");

        return Task.Run(() => runAudioServer(token));
    }

    /// <summary>
    /// Configures the Audio udp client and start sending data. Only runs when
    /// there's packets to be sent.
    /// </summary>
    private async static Task runAudioServer(CancellationToken token)
    {
        UdpClient client = new();

        await foreach (var packet in DataProvider.GetAllAudioPacketsAsync())
        {
            var buffer = packet.GetBytes();

            await client.SendAsync(buffer, buffer.Length, Endpoint);
        }
    }
}