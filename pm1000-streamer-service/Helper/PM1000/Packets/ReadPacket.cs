using System.Text;

namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Packet for reading registers from PM1000.
/// </summary>
public class ReadPacket : Packet
{
    private static readonly UInt16 R_ASCII = (UInt16)Encoding.ASCII.GetBytes("R")[0];

    public ReadPacket(UInt16 address) : base(4, PacketType.Read)
    {
        Payload[0] = R_ASCII;
        Payload[1] = (UInt16)(address & 0xFFF);
        Payload[2] = 0;
        Payload[3] = CRC.CalculateRedundancyCheck(Payload, 3);
    }
}