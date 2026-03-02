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

    /// <summary>Size of the streamer-service raw Stokes packet: 5 × float32 + 1 × uint32 = 24 bytes.</summary>
    public const int STREAMER_STOKES_SIZE = 24;

    /// <summary>Size of the streamer-service raw audio packet: 1 × float32 + 1 × uint32 = 8 bytes.</summary>
    public const int STREAMER_AUDIO_SIZE = 8;

    // ── Header+payload format (TestDataGenerator, future block-based senders) ───

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

    // ── Streamer-service raw format (single sample per datagram, no header) ────

    /// <summary>
    /// Deserializes a single Stokes sample from the streamer-service raw format.
    /// Wire layout (24 bytes, all little-endian):
    ///   float32 S0, float32 S1, float32 S2, float32 S3, float32 DOP, uint32 Time
    /// </summary>
    public static StokeSample? TryDeserializeStreamerStokes(byte[] data)
    {
        if (data.Length < STREAMER_STOKES_SIZE) return null;

        using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(data));

        float s0 = reader.ReadSingle();
        float s1 = reader.ReadSingle();
        float s2 = reader.ReadSingle();
        float s3 = reader.ReadSingle();
        float dop = reader.ReadSingle();
        // uint32 timestamp is discarded — DataRecorder timestamps locally.

        return new StokeSample(s0, s1, s2, s3, dop);
    }

    /// <summary>
    /// Deserializes a single audio amplitude from the streamer-service raw format.
    /// Wire layout (8 bytes, all little-endian):
    ///   float32 Amplitude, uint32 Time
    /// </summary>
    public static float? TryDeserializeStreamerAudio(byte[] data)
    {
        if (data.Length < STREAMER_AUDIO_SIZE) return null;

        using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(data));

        float amplitude = reader.ReadSingle();
        // uint32 timestamp is discarded.

        return amplitude;
    }
}

public record StokesPacket(uint SequenceNr, uint SampleRateHz, StokeSample[] Samples);
public record AudioPacket(uint SequenceNr, uint SampleRateHz, float[] Samples);
