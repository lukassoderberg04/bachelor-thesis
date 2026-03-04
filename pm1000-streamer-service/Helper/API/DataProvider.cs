using System.Threading.Channels;
using pm1000_streamer_service.PM1000;

namespace pm1000_streamer_service.API;

/// <summary>
/// Thread-safe producer/consumer queues between the Retriever (producers)
/// and the Streamer tasks (consumers).
///
/// Each item written to a channel is sent exactly once over UDP.
/// No more tight-loop duplicate broadcasts.
/// </summary>
public static class DataProvider
{
    /// <summary>
    /// Audio samples from the microphone (8 000 Hz, raw Int16 cast to float).
    /// Producer: Retriever.DataAvailable callback.
    /// Consumer: AudioStreamer.
    /// </summary>
    public static readonly Channel<AudioSnapshotPacket> AudioChannel =
        Channel.CreateUnbounded<AudioSnapshotPacket>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    /// <summary>
    /// Stokes measurement snapshots from the PM1000.
    /// Producer: Retriever.retrieveRegisters loop.
    /// Consumer: StokesStreamer.
    /// </summary>
    public static readonly Channel<StokesSnapshotPacket> StokesChannel =
        Channel.CreateUnbounded<StokesSnapshotPacket>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
}