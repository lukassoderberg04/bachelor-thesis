using FTD3XXWU_NET;

namespace pm1000_streamer_service;

/// <summary>
/// Sets the contract for communicating with the FTDI API.
/// </summary>
public interface IFTDI
{
    // Properties:
    bool IsOpen { get; }
    List<FTDI.FT_PIPE_INFORMATION> DataPipeInformation { get; }

    // Methods:
    FTDI.FT_STATUS OpenBySerialNumber(string serialNumber);
    FTDI.FT_STATUS Close();
    FTDI.FT_STATUS CreateDeviceInfoList(out uint deviceCount);
    FTDI.FT_STATUS GetDeviceInfoList(out List<FTDI.FT_DEVICE_INFO> deviceInfoList);
    FTDI.FT_STATUS SetPipeTimeout(byte pipeId, uint timeoutMs);
    FTDI.FT_STATUS FlushPipe(byte pipe);
    FTDI.FT_STATUS AbortPipe(byte pipe);
    FTDI.FT_STATUS WritePipe(byte pipe, byte[] buffer, uint bytesToWrite, ref uint bytesWritten);
    FTDI.FT_STATUS ReadPipe(byte pipe, byte[] buffer, uint bytesToRead, ref uint bytesRead);
    FTDI.FT_STATUS GetChipConfiguration(FTDI.FT_60XCONFIGURATION config);
    FTDI.FT_STATUS SetChipConfiguration(FTDI.FT_60XCONFIGURATION config);
    FTDI.FT_STATUS SetNotificationCallback(FTDI.FT_NOTIFICATION_CALLBACK_DATA callback, IntPtr userData);
    FTDI.FT_STATUS GetNumberOfDevicesConnected(out uint numberOfDevicesConnected);
}
