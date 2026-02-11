using System.Text;
using FTD2XX_NET;

namespace pm1000_visualizer;

/// <summary>
/// Handles all functions related to operating the PM1000 device for our cause.
/// </summary>
public static class PM1000Service
{
    public static bool HasCommunicationBeenInitialized { get; private set; } = false;

    #region Device Specific Constants:

    private const UInt32 BAUD_RATE = 230400;
    private const byte   LATENCY   = 2; // In ms.

    private const UInt32 RegisterBaseAddr = 512;

    private const UInt32 S0uWU = RegisterBaseAddr + 10; // Input power in µW, integer part.
    private const UInt32 S0uWL = RegisterBaseAddr + 11; // Input power in µW, fractional part. Updated at last read of S0uWU.

    private const UInt32 S1uWU = RegisterBaseAddr + 12; // S1 of Stokes vector normalized to 1 µW, integer part. Offset=2^15.
    private const UInt32 S1uWL = RegisterBaseAddr + 13; // S1 of Stokes vector normalized to 1 µW, fractional part. Updated at last read of S1uWU.

    private const UInt32 S2uWU = RegisterBaseAddr + 14; // S2 of Stokes vector normalized to 1 µW, integer part. Offset=2^15.
    private const UInt32 S2uWL = RegisterBaseAddr + 15; // S2 of Stokes vector normalized to 1 µW, fractional part. Updated at last read of S2uWU.

    private const UInt32 S3uWU = RegisterBaseAddr + 16; // S3 of Stokes vector normalized to 1 µW, integer part. Offset=2^15.
    private const UInt32 S3uWL = RegisterBaseAddr + 17; // S3 of Stokes vector normalized to 1 µW, fractional part. Updated at last read of S3uWU.

    private const UInt32 DOPSt = RegisterBaseAddr + 24; // Degree of polarization (DOP), 16 bit unsigned, 15 fractional bits.

    #endregion

    private const byte CR = 0x0D; // "Carriage Return" character.

    private const UInt32 REQ_PAC_LEN = 9; // Request package length, in bytes.

    /// <summary>
    /// Opens up communication with the PM1000 and initializes parameters for communication.
    /// </summary>
    public static void InitializeCommunication(DeviceInfoWrapper pm1000DeviceInfo)
    {
        Logger.LogInfo("Tries to initialize communication with PM1000...");

        if(!FtdiService.CloseCommunication()) return;

        if(!FtdiService.OpenConnectionUsingSerialNumber(pm1000DeviceInfo.SerialNumber)) return;

        if(!FtdiService.SetBaudRate(BAUD_RATE)) return;

        if(!FtdiService.SetLatency(LATENCY)) return;

        Logger.LogInfo("Successfully initialized the PM1000 device!");
        HasCommunicationBeenInitialized = true;
    }

    /// <summary>
    /// Reads data from one of the registers defined in the PM1000 guide.
    /// </summary>
    public static UInt32 ReadDataRegister(UInt32 addr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Converts two registers, one containing the integral and one the fractional,
    /// into a double. Is device specific since it uses a specific offset.
    /// </summary>
    public static double ConvertIntegerAndFractionalToDouble(UInt16 integral, UInt16 fractional)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a request packet to be sent to the FTDI device. Always 9 bytes long.
    /// </summary>
    private static byte[] createRequestPacket(UInt32 addr)
    {
        // Request packet is of size 9 bytes.
        byte[] requestPacket =
        [
            // 'R' in ASCII indicates request packet.
            Encoding.ASCII.GetBytes("R")[0],

            // Contains the address of which to read.
            (byte)((addr >> 16) & 0xFF),
            (byte)((addr >> 8) & 0xFF),
            (byte)(addr & 0xFF),

            Encoding.ASCII.GetBytes("0")[0],
            Encoding.ASCII.GetBytes("0")[0],
            Encoding.ASCII.GetBytes("0")[0],
            Encoding.ASCII.GetBytes("0")[0],

            CR,
        ];

        return requestPacket;
    }
}