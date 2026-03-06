using System.Threading.Channels;
using pm1000_streamer_service.PM1000;

namespace pm1000_streamer_service.API;

/// <summary>
/// Will provide the producers with a way to fill data (in a thread safe way)
/// that the consumers will read, and hence consume.
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
    private static readonly UnboundedChannelOptions unboundedOptions = new()
    {
        SingleReader = true,
        SingleWriter = true
    };

    /// <summary>
    /// The thread safe FIFO queue to be used for storing stokes packets.
    /// </summary>
    private static readonly Channel<StokesSnapshotPacket> stokesChannel = Channel.CreateUnbounded<StokesSnapshotPacket>(unboundedOptions);

    /// <summary>
    /// The thread safe FIFO queue to be used for storing audio packets.
    /// </summary>
    private static readonly Channel<AudioSnapshotPacket> audioChannel = Channel.CreateUnbounded<AudioSnapshotPacket>(unboundedOptions);

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
    public static async Task<StokesSnapshotPacket> GetStokesPacketAsync(CancellationToken token = default)
    {
        return await stokesChannel.Reader.ReadAsync(token);
    }

    /// <summary>
    /// Recieves the a async list of all stokes packets currently in the channel.
    /// </summary>
    public static IEnumerable<StokesSnapshotPacket> GetAllStokesPacketsAsync(CancellationToken token = default)
    {
        return stokesChannel.Reader.ReadAllAsync(token).ToBlockingEnumerable(token);
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
    public static async Task<AudioSnapshotPacket> GetAudioPacketAsync(CancellationToken token = default)
    {
        return await audioChannel.Reader.ReadAsync(token);
    }

    /// <summary>
    /// Recieves the a async list of all audio packets currently in the channel.
    /// </summary>
    public static IEnumerable<AudioSnapshotPacket> GetAllAudioPacketsAsync(CancellationToken token = default)
    {
        return audioChannel.Reader.ReadAllAsync(token).ToBlockingEnumerable(token);
    }
}