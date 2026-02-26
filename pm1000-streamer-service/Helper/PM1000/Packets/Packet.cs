namespace pm1000_streamer_service.PM1000;

/// <summary>
/// The base class for the packets.
/// </summary>
public abstract class Packet
{
    protected readonly int PACKET_SIZE_BYTES;

    protected readonly UInt16[] Payload;

    protected readonly PacketType Type;

    public Packet(int size, PacketType type)
    {
        PACKET_SIZE_BYTES = size * 2;
        Payload           = new UInt16[PACKET_SIZE_BYTES];
        Type              = type;
    }

    /// <summary>
    /// Should return the packets size in bytes.
    /// </summary>
    public int GetPacketSizeInBytes()
    {
        return PACKET_SIZE_BYTES;
    }

    /// <summary>
    /// Should return the bytes that is the packet.
    /// </summary>
    public byte[] GetBytes()
    {
        // Creates a buffer and copies the bytes created from the packet.
        byte[] buffer = new byte[PACKET_SIZE_BYTES];
        Buffer.BlockCopy(Payload, 0, buffer, 0, PACKET_SIZE_BYTES);

        return buffer;
    }

    /// <summary>
    /// Return the type of this packet.
    /// </summary>
    public PacketType GetPacketType()
    {
        return this.Type;
    }
}