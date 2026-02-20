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
    /// Configures the Audio udp client and start sending data.
    /// </summary>
    private async static Task runAudioServer(CancellationToken token)
    {
        UdpClient client = new();

        while (!token.IsCancellationRequested)
        {
            var buffer = DataProvider.AudioPacket.GetBytes();

            await client.SendAsync(buffer, buffer.Length, Endpoint);
        }
    }
}