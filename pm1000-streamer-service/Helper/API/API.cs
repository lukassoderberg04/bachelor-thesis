using System.Net;
using System.Net.Sockets;

namespace pm1000_streamer_service.API;

/// <summary>
/// The interface of which other application will use to configure this service
/// as well as retrieve information.
/// </summary>
public static class API
{
    /// <summary>
    /// Starts the API services specified.
    /// </summary>
    public static void Start(CancellationToken token, bool enableAudio = true, bool enableStokes = true, bool enableRest = true)
    {
        if (enableAudio)  AudioStreamer.Start(token);
        if (enableStokes) StokesStreamer.Start(token);
        if (enableRest)   RestServer.Start(token);
    }
}
