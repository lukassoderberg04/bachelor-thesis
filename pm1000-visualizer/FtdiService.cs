using FTD2XX_NET;

namespace pm1000_visualizer;

/// <summary>
/// Contains FTDI wrapper and helper functions that could be useful.
/// </summary>
public static class FtdiService
{
    /// <summary>
    /// The FTDI instance.
    /// </summary>
    public static FTDI Ftdi { get; } = new();

    /// <summary>
    /// Gets all the connected FTDI devices info as a list.
    /// </summary>
    public static List<DeviceInfoWrapper> GetConnectedDevicesInfo()
    {
        List<DeviceInfoWrapper> devices = new();

        Logger.LogInfo("Trying to get information for all connected devices...");

        var deviceCount = FtdiService.GetConnectedDevicesCount();

        Logger.LogInfo("Allocating memory for device info!");
        FTDI.FT_DEVICE_INFO_NODE[] deviceList = new FTDI.FT_DEVICE_INFO_NODE[deviceCount];

        FTDI.FT_STATUS status = FTDI.FT_STATUS.FT_OK;
        try
        {
            status = Ftdi.GetDeviceList(deviceList);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Caught exception when trying to fetch info from all connected devices: {ex.Message}.");
        }

        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"Calling the FTDI library didn't yield success, instead got: {status.ToString()}.");

            return devices;
        }

        foreach (var device in deviceList)
        {
            devices.Add(new DeviceInfoWrapper(device));
        }

        if (devices.Count != deviceCount)
        {
            Logger.LogWarning("Got a device list with info but some info got lost!");
        }
        else
        {
            Logger.LogInfo("Successfully fetched the whole device list!");
        }

        return devices;
    }

    /// <summary>
    /// Tries to close the currently open FTDI device. Returns true if it succeeded.
    /// </summary>
    public static bool CloseCommunication()
    {
        var status = FtdiService.Ftdi.Close();
        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError("Failed to close open connection to FTDI device!");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Tries to open a connection using serial number. Returns true if it succeeded.
    /// </summary>
    public static bool OpenConnectionUsingSerialNumber(string serialNumber)
    {
        var status = Ftdi.OpenBySerialNumber(serialNumber);
        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"Failed to open FTDI device with serial number: {serialNumber}!");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Sets the baud rate of the device.
    /// </summary>
    public static bool SetBaudRate(UInt32 baudRate)
    {
        var status = Ftdi.SetBaudRate(baudRate);
        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError("Failed to set the baud rate of the device!");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Sets the latency of the device in ms.
    /// </summary>
    public static bool SetLatency(byte latency)
    {
        var status = Ftdi.SetLatency(latency);
        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError("Failed to set the latency of the device!");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets all FTDI devices currently connected to the computer.
    /// </summary>
    private static UInt32 GetConnectedDevicesCount()
    {
        Logger.LogInfo("Fetching connected devices...");

        UInt32 deviceCount = 0;

        var status = Ftdi.GetNumberOfDevices(ref deviceCount);

        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"The FTDI did not return error, instead got: {status.ToString()}.");

            return deviceCount;
        }

        Logger.LogInfo($"Found devices: {deviceCount}.");

        return deviceCount;
    }
}
