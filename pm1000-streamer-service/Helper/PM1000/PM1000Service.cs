namespace pm1000_streamer_service.PM1000;

/// <summary>
/// This class takes care of the communication to and from the PM1000 device.
/// </summary>
public static class PM1000Service
{
    public static Transmitter Transmitter { get; } = new();

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
}