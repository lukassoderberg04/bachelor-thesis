using FTD3XXWU_NET;

namespace pm1000_streamer_service;

/// <summary>
/// Contains all functionality for communicating using the FTDI dlls with FTDI devices.
/// </summary>
public static class FtdiService
{
    public const byte SEND_PIPE = 0x02;
    public const byte READ_PIPE = 0x82;

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

        var status = Ftdi.CreateDeviceInfoList(out UInt32 deviceCount);
        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError("Couldn't create the device list!");
        }
        Logger.LogInfo($"Created the device list containing {deviceCount} entries.");

        status = Ftdi.GetDeviceInfoList(out List<FTDI.FT_DEVICE_INFO> deviceInfoList);
        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError("Couldn't populate the device info list!");
        }
        Logger.LogInfo($"Successfully populated the device info list with {deviceInfoList.Count} entries.");

        foreach (var device in deviceInfoList)
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
        var status = Ftdi.Close();
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
    /// Writes to PM1000 device's pipe. Returns true if it was successful.
    /// </summary>
    public static bool WriteToPipe(byte pipe, byte[] buffer)
    {
        UInt32 bytesTransfered = 0;

        var status = Ftdi.WritePipe(pipe, buffer, (UInt32)buffer.Length, ref bytesTransfered);
        
        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"Something went wrong when writing to the pipe! Wrote: {bytesTransfered} bytes. Status: {status.ToString()}.");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads bytes from a pipe. Returns true if it was successful.
    /// </summary>
    public static bool ReadFromPipe(byte pipe, byte[] buffer, UInt32 bytesToRead)
    {
        UInt32 bytesRead = 0;

        var status = Ftdi.ReadPipe(pipe, buffer, bytesToRead, ref bytesRead);

        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"Something went wrong when reading from the pipe! Read: {bytesRead} bytes. Status: {status.ToString()}.");

            Ftdi.AbortPipe(pipe);

            return false;
        }

        Ftdi.AbortPipe(pipe);

        return true;
    }

    /// <summary>
    /// Configures and restarts the device using the specified configuration object.
    /// </summary>
    public static bool SetConfigurationAndRestart(FTDI.FT_60XCONFIGURATION config)
    {
        Logger.LogInfo("Setting configuration of device and restarting...");

        var status = Ftdi.SetChipConfiguration(config);

        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"Couldn't set the chip configuration! Status: {status.ToString()}.");

            return false;
        }

        Logger.LogInfo("Successfully set the chip config!");

        if (!CloseCommunication()) return false;

        if (!OpenConnectionUsingSerialNumber(config.SerialNumber)) return false;

        Logger.LogInfo("Successfully configured and restarted the device!");

        return true;
    }

    /// <summary>
    /// Retrieves the configuration for the device.
    /// </summary>
    public static bool GetConfiguration(FTDI.FT_60XCONFIGURATION config)
    {
        Logger.LogInfo("Tries to retrieve the configuration for the opened device...");

        var status = Ftdi.GetChipConfiguration(config);

        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"Couldn't retrieve the configuration from the device! Status: {status.ToString()}.");

            return false;
        }

        Logger.LogInfo("Successfully retrieved the configuration for the device!");

        return true;
    }

    /// <summary>
    /// Sets the notification callback! Make sure the notification callback are enabled in the config for the device!
    /// </summary>
    public static bool SetNotificationCallback(FTDI.FT_NOTIFICATION_CALLBACK_DATA callback)
    {
        Logger.LogInfo("Trying to set the notification callback of the device!");

        var status = Ftdi.SetNotificationCallback(callback, IntPtr.Zero);

        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"Couldn't Set the notification callback! Status: {status.ToString()}.");

            return false;
        }

        Logger.LogInfo("Successfully set the notification callback!");

        return true;
    }

    /// <summary>
    /// Gets all FTDI devices currently connected to the computer.
    /// </summary>
    public static UInt32 GetConnectedDevicesCount()
    {
        Logger.LogInfo("Fetching connected devices...");

        var status = Ftdi.GetNumberOfDevicesConnected(out UInt32 deviceCount);

        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"The FTDI did not return error, instead got: {status.ToString()}.");

            return deviceCount;
        }

        Logger.LogInfo($"Found devices: {deviceCount}.");

        return deviceCount;
    }
}