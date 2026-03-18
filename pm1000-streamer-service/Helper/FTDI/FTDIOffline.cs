using System.Text;
using FTD3XXWU_NET;

namespace pm1000_streamer_service;

/// <summary>
/// Uses an active FTDI device to gather data.
/// </summary>
public class FTDIOffline : IFTDI
{
    private static Random rnd = new Random();

    public bool IsOpen => false;

    public List<FTDI.FT_PIPE_INFORMATION> DataPipeInformation => new List<FTDI.FT_PIPE_INFORMATION>();

    public FTDI.FT_STATUS AbortPipe(byte pipe)
    {
        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS Close()
    {
        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS CreateDeviceInfoList(out uint deviceCount)
    {
        deviceCount = 3;

        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS FlushPipe(byte pipe)
    {
        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS GetChipConfiguration(FTDI.FT_60XCONFIGURATION config)
    {
        config = new FTDI.FT_60XCONFIGURATION();

        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS GetDeviceInfoList(out List<FTDI.FT_DEVICE_INFO> deviceInfoList)
    {
        deviceInfoList = new();

        FTDI.FT_DEVICE_INFO[] devices =
        {
            new FTDI.FT_DEVICE_INFO()
            {
                SerialNumber = Encoding.UTF8.GetBytes("51512312341412"),
                Description  = Encoding.UTF8.GetBytes("DEVICE NR 1")
            },
            new FTDI.FT_DEVICE_INFO()
            {
                SerialNumber = Encoding.UTF8.GetBytes("51234131231232"),
                Description  = Encoding.UTF8.GetBytes("DEVICE NR 2")
            },
            new FTDI.FT_DEVICE_INFO()
            {
                SerialNumber = Encoding.UTF8.GetBytes("51421412316161"),
                Description  = Encoding.UTF8.GetBytes("DEVICE NR 3")
            }
        };

        deviceInfoList.AddRange(devices);

        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS GetNumberOfDevicesConnected(out uint numberOfDevicesConnected)
    {
        numberOfDevicesConnected = 3;

        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS OpenBySerialNumber(string serialNumber)
    {
        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS ReadPipe(byte pipe, byte[] buffer, uint bytesToRead, ref uint bytesRead)
    {
        Thread.Sleep(1); // Simulate some delay.

        rnd.NextBytes(buffer);

        bytesRead = bytesToRead;

        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS SetChipConfiguration(FTDI.FT_60XCONFIGURATION config)
    {
        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS SetNotificationCallback(FTDI.FT_NOTIFICATION_CALLBACK_DATA callback, nint userData)
    {
        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS SetPipeTimeout(byte pipeId, uint timeoutMs)
    {
        return FTDI.FT_STATUS.FT_OK;
    }

    public FTDI.FT_STATUS WritePipe(byte pipe, byte[] buffer, uint bytesToWrite, ref uint bytesWritten)
    {
        return FTDI.FT_STATUS.FT_OK;
    }
}