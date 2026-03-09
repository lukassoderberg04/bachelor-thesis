using FTD3XXWU_NET;

namespace pm1000_streamer_service;

/// <summary>
/// Uses an active FTDI device to gather data.
/// </summary>
public class FTDIOnline : IFTDI
{
    private readonly FTDI ftdi = new FTDI();

    public bool IsOpen => ftdi.IsOpen;

    public List<FTDI.FT_PIPE_INFORMATION> DataPipeInformation => ftdi.DataPipeInformation;

    public FTDI.FT_STATUS AbortPipe(byte pipe)
    {
        return ftdi.AbortPipe(pipe);
    }

    public FTDI.FT_STATUS Close()
    {
        return ftdi.Close();
    }

    public FTDI.FT_STATUS CreateDeviceInfoList(out uint deviceCount)
    {
        return ftdi.CreateDeviceInfoList(out deviceCount);
    }

    public FTDI.FT_STATUS FlushPipe(byte pipe)
    {
        return ftdi.FlushPipe(pipe);
    }

    public FTDI.FT_STATUS GetChipConfiguration(FTDI.FT_60XCONFIGURATION config)
    {
        return ftdi.GetChipConfiguration(config);
    }

    public FTDI.FT_STATUS GetDeviceInfoList(out List<FTDI.FT_DEVICE_INFO> deviceInfoList)
    {
        return ftdi.GetDeviceInfoList(out deviceInfoList);
    }

    public FTDI.FT_STATUS GetNumberOfDevicesConnected(out uint numberOfDevicesConnected)
    {
        return ftdi.GetNumberOfDevicesConnected(out numberOfDevicesConnected);
    }

    public FTDI.FT_STATUS OpenBySerialNumber(string serialNumber)
    {
        return ftdi.OpenBySerialNumber(serialNumber);
    }

    public FTDI.FT_STATUS ReadPipe(byte pipe, byte[] buffer, uint bytesToRead, ref uint bytesRead)
    {
        return ftdi.ReadPipe(pipe, buffer, bytesToRead, ref bytesRead);
    }

    public FTDI.FT_STATUS SetChipConfiguration(FTDI.FT_60XCONFIGURATION config)
    {
        return ftdi.SetChipConfiguration(config);
    }

    public FTDI.FT_STATUS SetNotificationCallback(FTDI.FT_NOTIFICATION_CALLBACK_DATA callback, nint userData)
    {
        return ftdi.SetNotificationCallback(callback, userData);
    }

    public FTDI.FT_STATUS SetPipeTimeout(byte pipeId, uint timeoutMs)
    {
        return ftdi.SetPipeTimeout(pipeId, timeoutMs);
    }

    public FTDI.FT_STATUS WritePipe(byte pipe, byte[] buffer, uint bytesToWrite, ref uint bytesWritten)
    {
        return ftdi.WritePipe(pipe, buffer, bytesToWrite, ref bytesWritten);
    }
}
