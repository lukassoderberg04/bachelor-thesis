namespace pm1000_streamer_service.PM1000;

/// <summary>
/// An arbitrary packet has no fixed size, just contains the payload given.
/// </summary>
public class ArbitraryPacket : Packet
{
    public ArbitraryPacket(byte[] bytes) : base(bytes.Length, PacketType.Unknown)
    {
        if (bytes.Length % 2 != 0) throw new Exception("The arbitrary packet must have it's bytes array length divisible by 2.");

        Buffer.BlockCopy(bytes, 0, Payload, 0, bytes.Length);
    }
}