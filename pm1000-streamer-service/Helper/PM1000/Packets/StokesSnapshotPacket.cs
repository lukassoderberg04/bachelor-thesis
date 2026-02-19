using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace pm1000_streamer_service.PM1000;

/// <summary>
/// A packet containing all the stokes vectors and DOP.
/// It contains S0, S1, S2, S3, DOP and the time it was constructed in some time unit.
/// </summary>
public class StokesSnapshotPacket : Packet
{
    public StokesSnapshotPacket(float s0, float s1, float s2, float s3, float dop, UInt32 time) : base(12, PacketType.StokesSnapshot)
    {
        // Creates a view of bytes since the payload is of type UInt32.
        var byteViewOfPayload = MemoryMarshal.AsBytes(Payload.AsSpan());

        writeFloat(byteViewOfPayload, 0, s0);
        writeFloat(byteViewOfPayload, 4, s1);
        writeFloat(byteViewOfPayload, 8, s2);
        writeFloat(byteViewOfPayload, 12, s3);
        writeFloat(byteViewOfPayload, 16, dop);
        BinaryPrimitives.WriteUInt32LittleEndian(byteViewOfPayload.Slice(20), time);
    }

    /// <summary>
    /// Writes the float to the payload buffer.
    /// </summary>
    private static void writeFloat(Span<byte> buffer, int offset, float value)
    {
        // Convert single float precision value to a UInt32.
        UInt32 bits = BitConverter.SingleToUInt32Bits(value);

        // Write the converted bits into the buffer as little endian.
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset), bits);
    }

    /// <summary>
    /// Returns a default value for this packet.
    /// </summary>
    public static StokesSnapshotPacket Default() { return new StokesSnapshotPacket(0, 0, 0, 0, 0, 0); }
}