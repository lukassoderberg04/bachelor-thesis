using System.Text;

namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Packet for writing data to registers on the PM1000.
/// </summary>
public class WritePacket : Packet
{
    private static readonly UInt16 W_ASCII = (UInt16)Encoding.ASCII.GetBytes("W")[0];

    public readonly UInt16 Address;

    public readonly UInt16 Data;

    public readonly UInt16 Crc;

    public WritePacket(UInt16 address, UInt16 data) : base(4, PacketType.Write)
    {
        this.Address = (UInt16)(address & 0xFFF);
        this.Data    = data;

        Payload[0] = W_ASCII;
        Payload[1] = this.Address;
        Payload[2] = this.Data;

        this.Crc = CRC.CalculateRedundancyCheck(Payload, 3);

        Payload[3] = this.Crc;
    }
}