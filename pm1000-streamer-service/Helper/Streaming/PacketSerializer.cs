namespace pm1000_streamer_service.Streaming;

/// <summary>
/// Serializes PM1000 Stokes data into UDP payloads (port 5000).
/// Audio comes from Ludwig's signal processing – not from this service.
///
/// PACKET FORMAT (little-endian)
///   Header – 10 bytes:
///     uint32  sequence_nr    – wraps at 2^32, used to detect dropped packets
///     uint32  sample_rate_hz – e.g. 16000
///     uint16  block_size     – number of samples in this packet
///
///   Stokes sample – 20 bytes × block_size:
///     float32 S0, S1, S2, S3, DOP
/// </summary>
public static class PacketSerializer
{
    public const int HEADER_SIZE = 10;
    public const int STOKES_SAMPLE_SIZE = 20;

    public static byte[] SerializeStokes(StokeSample[] samples, uint sequenceNr, uint sampleRateHz)
    {
        using var ms = new System.IO.MemoryStream(HEADER_SIZE + samples.Length * STOKES_SAMPLE_SIZE);
        using var writer = new System.IO.BinaryWriter(ms);

        writer.Write(sequenceNr);
        writer.Write(sampleRateHz);
        writer.Write((ushort)samples.Length);

        foreach (var s in samples)
        {
            writer.Write(s.S0);
            writer.Write(s.S1);
            writer.Write(s.S2);
            writer.Write(s.S3);
            writer.Write(s.Dop);
        }

        return ms.ToArray();
    }
}
