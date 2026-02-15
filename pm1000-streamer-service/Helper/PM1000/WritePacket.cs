using System.Text;

namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Packet for writing data to registers on the PM1000.
/// </summary>
public class WritePacket : IPacket
{
    private static readonly UInt16 W_ASCII = (UInt16)Encoding.ASCII.GetBytes("W")[0];

    private const int PACKET_SIZE_BYTES = 8;

    private readonly UInt16[] packet; 

    public WritePacket(UInt16 address, UInt16 data)
    {
        packet = [
            W_ASCII,
            (UInt16)(address & 0xFFF),
            data,
            0
        ];

        packet[3] = CRC.CalculateRedundancyCheck(packet, 3);
    }

    public byte[] GetBytes()
    {
        // Creates a buffer and copies the bytes created from the packet.
        byte[] buffer = new byte[packet.Length * 2];
        Buffer.BlockCopy(packet, 0, buffer, 0, buffer.Length);

        return buffer;
    }

    public int GetPacketSizeInBytes()
    {
        return PACKET_SIZE_BYTES;
    }
}