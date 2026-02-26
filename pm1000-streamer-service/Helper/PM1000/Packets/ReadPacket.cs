using System.Text;

namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Packet for reading registers from PM1000.
/// </summary>
public class ReadPacket : Packet
{
    private static readonly UInt16 R_ASCII = (UInt16)Encoding.ASCII.GetBytes("R")[0];

    public readonly UInt16 Address;

    public readonly UInt16 Crc;

    public ReadPacket(UInt16 address) : base(4, PacketType.Read)
    {
        this.Address = (UInt16)(address & 0xFFF);

        Payload[0] = R_ASCII;
        Payload[1] = this.Address;
        Payload[2] = 0;

        this.Crc = CRC.CalculateRedundancyCheck(Payload, 3);

        Payload[3] = this.Crc;


    }
}