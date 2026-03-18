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
    /// Bounded options specifically for the stokes to filter channel.
    /// Since this is written to when also writing to the stokes channel,
    /// it will drop in case of it being full since the filtering may not
    /// be used.
    /// </summary>
    private static readonly BoundedChannelOptions boundedOptionsToFilter = new(1_000)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = true
    };

    /// <summary>
    /// Makes sure that the threads wait when the channel is full... this indicates that the
    /// streamer service needs to consume some packets and send them over the "network".
    /// Since all of the channels are only operated by one producer and one consumer,
    /// you don't have to implement costly locking mechanism and therefore the single reader
    /// and writer are enabled.
    /// </summary>
    private static readonly BoundedChannelOptions boundedOptionsDefault = new(10_000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true
    };

    /// <summary>
    /// The thread safe FIFO queue to be used for storing stokes packets.
    /// </summary>
    private static readonly Channel<StokesSnapshotPacket> stokesChannel = Channel.CreateBounded<StokesSnapshotPacket>(boundedOptionsDefault);

    /// <summary>
    /// Thread safe FIFO queue to be used for storing stokes packet that will be used in filtering.
    /// Has to be bounded due to it maybe not being used.
    /// </summary>
    private static readonly Channel<StokesSnapshotPacket> stokesToFilterChannel = Channel.CreateBounded<StokesSnapshotPacket>(boundedOptionsToFilter);

    /// <summary>
    /// The thread safe FIFO queue to be used for storing audio packets.
    /// </summary>
    private static readonly Channel<AudioSnapshotPacket> audioChannel = Channel.CreateBounded<AudioSnapshotPacket>(boundedOptionsDefault);

    /// <summary>
    /// Adds stokes packet to the channel. Waits for the queue to be consumed if full.
    /// </summary>
    public static async Task AddStokesPacketAsync(StokesSnapshotPacket packet, CancellationToken token = default)
    {
        await stokesChannel.Writer.WriteAsync(packet, token);
        await AddStokesToFilterPacketAsync(packet, token); // Adds to the stokes to filter in the mean time.
    }

    /// <summary>
    /// Tries to write a stokes packet to the FIFO queue, if it's full it will return false, else true.
    /// </summary>
    public static bool TryAddStokesPacket(StokesSnapshotPacket packet)
    {
        return stokesChannel.Writer.TryWrite(packet) && stokesToFilterChannel.Writer.TryWrite(packet);
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
    public static IAsyncEnumerable<StokesSnapshotPacket> GetAllStokesPacketsAsync(CancellationToken token = default)
    {
        return stokesChannel.Reader.ReadAllAsync(token);
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
    public static IAsyncEnumerable<AudioSnapshotPacket> GetAllAudioPacketsAsync(CancellationToken token = default)
    {
        return audioChannel.Reader.ReadAllAsync(token);
    }

    /// <summary>
    /// Adds stokes packet to the channel. Waits for the queue to be consumed if full.
    /// </summary>
    public static async Task AddStokesToFilterPacketAsync(StokesSnapshotPacket packet, CancellationToken token = default)
    {
        await stokesToFilterChannel.Writer.WriteAsync(packet, token);
    }

    /// <summary>
    /// Wait until a stokes packet is avaible and reads it from the FIFO queue.
    /// </summary>
    public static async Task<StokesSnapshotPacket> GetStokesToFilterPacketAsync(CancellationToken token = default)
    {
        return await stokesToFilterChannel.Reader.ReadAsync(token);
    }

    /// <summary>
    /// Recieves the a async list of all stokes packets currently in the channel.
    /// </summary>
    public static IAsyncEnumerable<StokesSnapshotPacket> GetAllStokesToFilterPacketsAsync(CancellationToken token = default)
    {
        return stokesToFilterChannel.Reader.ReadAllAsync(token);
    }
}