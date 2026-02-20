using pm1000_streamer_service.API;
using pm1000_streamer_service.PM1000;

namespace pm1000_streamer_service;

/// <summary>
/// Main purpose is retrieving data from the PM1000 and making sure the API
/// has access to that specific data.
/// </summary>
public static class Retriever
{
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
        while (!token.IsCancellationRequested)
        {
            retrieveRegisters();
        }
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
}