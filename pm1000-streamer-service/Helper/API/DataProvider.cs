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
    /// Allows only 10 packets to be filled in the thread safe FIFO queue before having to wait for consumers to consume.
    /// When the channel is full, the producer will wait until it's ready to populate it again before continuing.
    /// Since there's only one service writing to any given channel and also one service reading from any given channel
    /// the two settings below are used. This disables locking mechanisms that could worsen performance, especially if
    /// it doesn't have to be thread safe since only one thread is accessing them.
    /// </summary>
    private static readonly BoundedChannelOptions boundedOptions = new(10)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true
    };

    /// <summary>
    /// The thread safe FIFO queue to be used for storing stokes packets.
    /// </summary>
    private static readonly Channel<StokesSnapshotPacket> stokesChannel = Channel.CreateBounded<StokesSnapshotPacket>(boundedOptions);

    /// <summary>
    /// The thread safe FIFO queue to be used for storing audio packets.
    /// </summary>
    private static readonly Channel<AudioSnapshotPacket> audioChannel = Channel.CreateBounded<AudioSnapshotPacket>(boundedOptions);

    /// <summary>
    /// Adds stokes packet to the channel. Waits for the queue to be consumed if full.
    /// </summary>
    public static async Task AddStokesPacketAsync(StokesSnapshotPacket packet, CancellationToken token = default)
    {
        await stokesChannel.Writer.WriteAsync(packet, token);
    }

    /// <summary>
    /// Tries to write a stokes packet to the FIFO queue, if it's full it will return false, else true.
    /// </summary>
    public static bool TryAddStokesPacket(StokesSnapshotPacket packet)
    {
        return stokesChannel.Writer.TryWrite(packet);
    }

    /// <summary>
    /// Wait until a stokes packet is avaible and reads it from the FIFO queue.
    /// </summary>
    public static async Task<StokesSnapshotPacket> GetStokesPacket(CancellationToken token = default)
    {
        return await stokesChannel.Reader.ReadAsync(token);
    }

    /// <summary>
    /// Adds audio packet to the channel. Waits for the queue to be consumed if full.
    /// </summary>
    public static async Task AddAudioPacketAsync(AudioSnapshotPacket packet, CancellationToken token = default)
    {
        await audioChannel.Writer.WriteAsync(packet, token);
    }

    /// <summary>
    /// Tries to write a audio packet to the FIFO queue, if it's full it will return false, else true.
    /// </summary>
    public static bool TryAddAudioPacket(AudioSnapshotPacket packet)
    {
        return audioChannel.Writer.TryWrite(packet);
    }

    /// <summary>
    /// Wait until a audio packet is avaible and reads it from the FIFO queue.
    /// </summary>
    public static async Task<AudioSnapshotPacket> GetAudioPacket(CancellationToken token = default)
    {
        return await audioChannel.Reader.ReadAsync(token);
    }
}