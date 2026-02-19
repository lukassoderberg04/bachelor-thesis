namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Contains the amplitude of the audio signal together with a time.
/// </summary>
public class AudioSnapshotPacket : Packet
{
    public AudioSnapshotPacket(float amplitude, UInt32 time) : base(4, PacketType.StokesSnapshot)
    {

    }

    /// <summary>
    /// Returns the default packet.
    /// </summary>
    public static AudioSnapshotPacket Default() { return new AudioSnapshotPacket(0, 0); }
}