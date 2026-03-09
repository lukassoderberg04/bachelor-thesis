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
    /// Unbounded options which are used for stokes and audio channels.
    /// The single reader and single writer makes sure that the channels
    /// doesn't add locking mechanisms that would be required if multiple
    /// threads would access them... but in this case it will just work as
    /// a thread safe queue.
    /// </summary>
    private static readonly UnboundedChannelOptions unboundedOptions = new()
    {
        SingleReader = true,
        SingleWriter = true
    };

    /// <summary>
    /// Bounded options specifically for the stokes to filter channel.
    /// Since this is written to when also writing to the stokes channel,
    /// it will drop in case of it being full since the filtering may not
    /// be used.
    /// </summary>
    private static readonly BoundedChannelOptions boundedOptions = new(100)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = true
    };

    /// <summary>
    /// The thread safe FIFO queue to be used for storing stokes packets.
    /// </summary>
    private static readonly Channel<StokesSnapshotPacket> stokesChannel = Channel.CreateUnbounded<StokesSnapshotPacket>(unboundedOptions);

    /// <summary>
    /// Thread safe FIFO queue to be used for storing stokes packet that will be used in filtering.
    /// Has to be bounded due to it maybe not being used.
    /// </summary>
    private static readonly Channel<StokesSnapshotPacket> stokesToFilterChannel = Channel.CreateBounded<StokesSnapshotPacket>(boundedOptions);

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