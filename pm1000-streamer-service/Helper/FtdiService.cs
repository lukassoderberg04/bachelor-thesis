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
    /// Configures a connected device.
    /// </summary>
    public static bool ConfigureDeviceSetting()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Writes to PM1000 device's pipe. Returns true if it was successful.
    /// </summary>
    public static bool WriteToPipe(byte pipe, byte[] buffer)
    {
        UInt32 bytesTransfered = 0;

        Logger.LogInfo($"Writing bytes to pipe: 0x{pipe:X2}.");

        var status = Ftdi.WritePipe(pipe, buffer, (UInt32)buffer.Length, ref bytesTransfered);
        
        if (status != FTDI.FT_STATUS.FT_OK)
        {
            Logger.LogError($"Something went wrong when writing to the pipe! Wrote: {bytesTransfered} bytes.");

            return false;
        }

        Logger.LogInfo("Successfully transmitted all bytes!");

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