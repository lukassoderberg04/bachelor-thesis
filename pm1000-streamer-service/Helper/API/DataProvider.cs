using pm1000_streamer_service.PM1000;

namespace pm1000_streamer_service.API;

/// <summary>
/// Will provide the producers with a way to fill data (in a thread safe way)
/// that the consumers will read, and hence consume.
/// </summary>
public static class DataProvider
{
    private static StokesSnapshotPacket _stokesPacket = StokesSnapshotPacket.Default();
    private static AudioSnapshotPacket _audioPacket   = AudioSnapshotPacket.Default();

    public static StokesSnapshotPacket StokesPacket 
    {
        get => Volatile.Read(ref _stokesPacket);
        set => Volatile.Write(ref _stokesPacket, value);
    }

    public static AudioSnapshotPacket AudioPacket
    {
        get => Volatile.Read(ref _audioPacket);
        set => Volatile.Write(ref _audioPacket, value);
    }
}