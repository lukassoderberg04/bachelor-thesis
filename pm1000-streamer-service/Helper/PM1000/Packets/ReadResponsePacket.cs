namespace pm1000_streamer_service.PM1000;

/// <summary>
/// When sending a read packet to the PM1000, this is the response.
/// </summary>
public class ReadResponsePacket : Packet
{
    public readonly UInt16 FifoLength, PmCrc, Data, FifoCrc;

    public ReadResponsePacket(byte[] bytes) : base(4, PacketType.ReadResponse)
    {
        if (bytes.Length != 8) throw new Exception($"Read response packet has to be 8 bytes long, not: {bytes.Length}!");

        Payload[0] = BitConverter.ToUInt16(bytes, 0);
        Payload[1] = BitConverter.ToUInt16(bytes, 2);
        Payload[2] = BitConverter.ToUInt16(bytes, 4);
        Payload[3] = BitConverter.ToUInt16(bytes, 6);

        FifoLength = Payload[0];
        PmCrc      = Payload[1];
        Data       = Payload[2];
        FifoCrc    = Payload[3];
    }
}