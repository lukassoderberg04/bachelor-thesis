using System.Text;

namespace pm1000_streamer_service.PM1000;

/// <summary>
/// A packet used when wanting to do a SDRAM data transfer.
/// </summary>
public class TransferPacket : Packet
{
    private static readonly UInt16 F_ASCII = (UInt16)Encoding.ASCII.GetBytes("F")[0];

    public TransferPacket(UInt16 address) : base(4, PacketType.Transfer)
    {
        Payload[0] = F_ASCII;
        Payload[1] = 0;
        Payload[2] = 0;
        Payload[3] = CRC.CalculateRedundancyCheck(Payload, 3);
    }
}
