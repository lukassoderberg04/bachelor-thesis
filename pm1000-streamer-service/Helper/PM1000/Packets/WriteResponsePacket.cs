namespace pm1000_streamer_service.PM1000;

/// <summary>
/// After sending a write packet to the PM1000, this is the response packet.
/// </summary>
public class WriteResponsePacket : Packet
{
    public WriteResponsePacket(byte[] bytes) : base(0, PacketType.NotImplemented)
    {
        throw new NotImplementedException();
    }
}