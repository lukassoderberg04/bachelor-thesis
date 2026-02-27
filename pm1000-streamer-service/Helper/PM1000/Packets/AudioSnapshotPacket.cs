using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Contains the amplitude of the audio signal together with a time.
/// </summary>
public class AudioSnapshotPacket : Packet
{
    public readonly float Amplitude;

    public readonly UInt32 Time;

    public AudioSnapshotPacket(float amplitude) : base(4, PacketType.StokesSnapshot)
    {
        // Creates a view of bytes since the payload is of type UInt32.
        var byteViewOfPayload = MemoryMarshal.AsBytes(Payload.AsSpan());

        Amplitude = amplitude;
        Time      = Clock.GetMillisecondsFromStart();

        writeFloat(byteViewOfPayload, 0, Amplitude);
        BinaryPrimitives.WriteUInt32LittleEndian(byteViewOfPayload.Slice(4), Time);
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
    /// Returns the default packet.
    /// </summary>
    public static AudioSnapshotPacket Default() { return new AudioSnapshotPacket(0); }
}