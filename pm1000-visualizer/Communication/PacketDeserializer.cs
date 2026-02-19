namespace pm1000_visualizer.Communication;

/// <summary>
/// Deserializes raw UDP bytes into PM1000 data.
/// Must match PacketSerializer in pm1000-streamer-service exactly.
///
/// Header (10 bytes): uint32 sequence_nr, uint32 sample_rate_hz, uint16 block_size
/// Stokes sample (20 bytes): float S0, S1, S2, S3, DOP
/// Audio sample  (4 bytes):  float amplitude
/// </summary>
public static class PacketDeserializer
{
    public const int HEADER_SIZE = 10;

    public static StokesPacket? TryDeserializeStokes(byte[] data)
    {
        if (data.Length < HEADER_SIZE) return null;

        using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(data));

        uint sequenceNr = reader.ReadUInt32();
        uint sampleRate = reader.ReadUInt32();
        ushort blockSize = reader.ReadUInt16();

        var samples = new StokeSample[blockSize];
        for (int i = 0; i < blockSize; i++)
            samples[i] = new StokeSample(reader.ReadSingle(), reader.ReadSingle(),
                                          reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        return new StokesPacket(sequenceNr, sampleRate, samples);
    }

    public static AudioPacket? TryDeserializeAudio(byte[] data)
    {
        if (data.Length < HEADER_SIZE) return null;

        using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(data));

        uint sequenceNr = reader.ReadUInt32();
        uint sampleRate = reader.ReadUInt32();
        ushort blockSize = reader.ReadUInt16();

        var samples = new float[blockSize];
        for (int i = 0; i < blockSize; i++)
            samples[i] = reader.ReadSingle();

        return new AudioPacket(sequenceNr, sampleRate, samples);
    }
}

public record StokesPacket(uint SequenceNr, uint SampleRateHz, StokeSample[] Samples);
public record AudioPacket(uint SequenceNr, uint SampleRateHz, float[] Samples);
