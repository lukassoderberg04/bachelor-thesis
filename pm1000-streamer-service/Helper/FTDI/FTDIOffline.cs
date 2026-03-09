using FTD3XXWU_NET;

namespace pm1000_streamer_service;

/// <summary>
/// Uses an active FTDI device to gather data.
/// </summary>
public class FTDIOffline : IFTDI
{
    public bool IsOpen => throw new NotImplementedException();

    public List<FTDI.FT_PIPE_INFORMATION> DataPipeInformation => throw new NotImplementedException();

    public FTDI.FT_STATUS AbortPipe(byte pipe)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS Close()
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS CreateDeviceInfoList(out uint deviceCount)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS FlushPipe(byte pipe)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS GetChipConfiguration(FTDI.FT_60XCONFIGURATION config)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS GetDeviceInfoList(out List<FTDI.FT_DEVICE_INFO> deviceInfoList)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS GetNumberOfDevicesConnected(out uint numberOfDevicesConnected)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS OpenBySerialNumber(string serialNumber)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS ReadPipe(byte pipe, byte[] buffer, uint bytesToRead, ref uint bytesRead)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS SetChipConfiguration(FTDI.FT_60XCONFIGURATION config)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS SetNotificationCallback(FTDI.FT_NOTIFICATION_CALLBACK_DATA callback, nint userData)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS SetPipeTimeout(byte pipeId, uint timeoutMs)
    {
        throw new NotImplementedException();
    }

    public FTDI.FT_STATUS WritePipe(byte pipe, byte[] buffer, uint bytesToWrite, ref uint bytesWritten)
    {
        throw new NotImplementedException();
    }
}