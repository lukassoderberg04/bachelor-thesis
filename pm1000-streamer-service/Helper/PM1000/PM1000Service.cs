namespace pm1000_streamer_service.PM1000;

/// <summary>
/// This class takes care of the communication to and from the PM1000 device.
/// </summary>
public static class PM1000Service
{
    /// <summary>
    /// Opens up communication with the PM1000 and initializes parameters for communication. Returns true if it was successful.
    /// </summary>
    public static bool InitializeCommunication(DeviceInfoWrapper pm1000DeviceInfo)
    {
        Logger.LogInfo("Tries to initialize communication with PM1000...");

        if (FtdiService.CheckIfConnectionIsAlreadyOpen())
        {
            FtdiService.CloseCommunication();
        }

        if (!FtdiService.OpenConnectionUsingSerialNumber(pm1000DeviceInfo.SerialNumber)) return false;

        Logger.LogInfo("Successfully initialized the PM1000 device!");

        return true;
    }

    /// <summary>
    /// Tries to send a packet using the FTDI open connection. Returns the packet if no timeout happened.
    /// </summary>
    public static Packet? SendPacket(Packet packet)
    {
        var sendBuffer = packet.GetBytes();

        if (!FtdiService.FlushPipe(FtdiService.READ_PIPE)) return null;

        if (!FtdiService.WriteToPipe(FtdiService.SEND_PIPE, sendBuffer)) return null;

        var readBuffer = new byte[8];

        if (!FtdiService.ReadFromPipe(FtdiService.READ_PIPE, readBuffer, (UInt32)readBuffer.Length)) return null;

        Packet readPacket;

        switch (packet.GetPacketType())
        {
            case PacketType.Read:
                readPacket = new ReadResponsePacket(readBuffer);
                break;

            case PacketType.Write:
                readPacket = new WriteResponsePacket(readBuffer);
                break;

            default:
                readPacket = new ArbitraryPacket(readBuffer);
                break;
        }

        return readPacket;
    }

    /// <summary>
    /// Converts an integer part and a fractional part into a 32 bit float. Can have an offset for negative values.
    /// </summary>
    public static float ConvertIntegerAndFractionalToFloat(UInt16 integer, UInt16 fractional, UInt16 offset)
    {
        // The resolution of which the fractional value will be able to calculate. 65536 = 2^16.
        const float fractionalResolution = 65536f;

        float value = (float)(integer - offset) + (fractional / fractionalResolution);

        return value;
    }
}