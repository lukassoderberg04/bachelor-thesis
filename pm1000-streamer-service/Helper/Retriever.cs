using pm1000_streamer_service.API;
using pm1000_streamer_service.PM1000;
using NAudio.Wave;

namespace pm1000_streamer_service;

/// <summary>
/// Main purpose is retrieving data from the PM1000 and making sure the API
/// has access to that specific data.
/// </summary>
public static class Retriever
{
    /// <summary>
    /// Audio sample rate in Hz.
    /// </summary>
    private const int AUDIO_SAMPLE_RATE = 8000;

    /// <summary>
    /// The amount of bits per data measuring point.
    /// </summary>
    private const int BIT_DEPTH = 16;

    /// <summary>
    /// The size of the buffer in "milliseconds".
    /// </summary>
    private const int BUFFER_SIZE_IN_MS = 10;

    /// <summary>
    /// The configuration for the capture device!
    /// </summary>
    private static readonly WaveInEvent waveIn = new()
    {
        DeviceNumber       = 0, // Use the default mic device.
        WaveFormat         = new WaveFormat(AUDIO_SAMPLE_RATE, BIT_DEPTH, 1),
        BufferMilliseconds = BUFFER_SIZE_IN_MS // How many ms of audio the buffer will hold.
    };

    /// <summary>
    /// The packets to send over to the PM1000.
    /// </summary>
    private static readonly ReadPacket[] packetReadsToSend =
    {
        new ReadPacket(Register.S0uWU),
        new ReadPacket(Register.S0uWL),

        new ReadPacket(Register.S1uWU),
        new ReadPacket(Register.S1uWL),

        new ReadPacket(Register.S2uWU),
        new ReadPacket(Register.S2uWL),

        new ReadPacket(Register.S3uWU),
        new ReadPacket(Register.S3uWL),

        new ReadPacket(Register.DOPSt)
    };

    /// <summary>
    /// The responses from the packets to send are stored here.
    /// </summary>
    private static ReadResponsePacket[] readResponses = new ReadResponsePacket[packetReadsToSend.Length];

    /// <summary>
    /// Starts the retrieving process.
    /// </summary>
    public static void Start(CancellationToken token)
    {
        Logger.LogInfo("Starting the Retriever service on a different thread...");

        Task.Run(() => retrieveLoop(token));
    }

    /// <summary>
    /// This is where the retrieval of all data is done
    /// and also where the data is placed in reach of the
    /// API.
    /// </summary>
    private static void retrieveLoop(CancellationToken token)
    {
        startAudioRetrieval();

        while (!token.IsCancellationRequested)
        {
            retrieveRegisters();
        }

        stopAudioRetrieval();
    }

    /// <summary>
    /// Retrieves the responses from the packets sent.
    /// </summary>
    private static void retrieveRegisters()
    {
        bool successfullyReadAllRegisters = true;

        for (int i = 0; i < packetReadsToSend.Length; i++)
        {
            var packet = packetReadsToSend[i];

            var response = PM1000Service.SendPacket(packet);

            if (response == null || response.GetPacketType() != PacketType.ReadResponse) { successfullyReadAllRegisters = false; continue; }

            readResponses[i] = (ReadResponsePacket)response;
        }

        if (!successfullyReadAllRegisters) return;

        var s0 = PM1000Service.ConvertIntegerAndFractionalToFloat(readResponses[0].Data, readResponses[1].Data, 0);

        const UInt16 stokesOffset = 32768;
        var s1 = PM1000Service.ConvertIntegerAndFractionalToFloat(readResponses[2].Data, readResponses[3].Data, stokesOffset);
        var s2 = PM1000Service.ConvertIntegerAndFractionalToFloat(readResponses[4].Data, readResponses[5].Data, stokesOffset);
        var s3 = PM1000Service.ConvertIntegerAndFractionalToFloat(readResponses[6].Data, readResponses[7].Data, stokesOffset);

        // The resolution of the DOP register value. Resolution = 2^15. The 16 bit says if it's unsigned or signed.
        const float dopResolution = 32768f;
        var dop = (float)readResponses[8].Data / dopResolution;

        DataProvider.StokesPacket = new StokesSnapshotPacket(s0, s1, s2, s3, dop);
    }

    /// <summary>
    /// Retrieves the audio from the default mic.
    /// </summary>
    private static void startAudioRetrieval()
    {
        waveIn.DataAvailable += (s, e) =>
        {
            const float largestInt16Value = 32768f;

            if (e.BytesRecorded < 2) return;

            Int16 sample  = BitConverter.ToInt16(e.Buffer, 0);    // Read the first two bytes in the buffer as Int16.
            var amplitude = Math.Abs(sample / largestInt16Value); // 0.0 <= audio sample <= 1.0!

            DataProvider.AudioPacket = new AudioSnapshotPacket(amplitude);
        };

        waveIn.StartRecording();
    }

    /// <summary>
    /// Stops the audio retrieval.
    /// </summary>
    private static void stopAudioRetrieval()
    {
        waveIn.StopRecording();

        waveIn.Dispose();
    }
}