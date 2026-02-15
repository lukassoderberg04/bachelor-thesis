using System.Text;

namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Packet for writing data to registers on the PM1000.
/// </summary>
public class WritePacket : Packet
{
    private static readonly UInt16 W_ASCII = (UInt16)Encoding.ASCII.GetBytes("W")[0];

    public WritePacket(UInt16 address, UInt16 data) : base(4, PacketType.Write)
    {
        Payload[0] = W_ASCII;
        Payload[1] = (UInt16)(address & 0xFFF);
        Payload[2] = data;
        Payload[3] = CRC.CalculateRedundancyCheck(Payload, 3);
    }
}