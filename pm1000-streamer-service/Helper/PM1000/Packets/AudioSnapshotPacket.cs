namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Contains the amplitude of the audio signal together with a time.
/// </summary>
public class AudioSnapshotPacket : Packet
{
    public AudioSnapshotPacket(float amplitude) : base(4, PacketType.StokesSnapshot)
    {
        // Time value (4 bytes).
    }

    /// <summary>
    /// Returns the default packet.
    /// </summary>
    public static AudioSnapshotPacket Default() { return new AudioSnapshotPacket(0); }
}