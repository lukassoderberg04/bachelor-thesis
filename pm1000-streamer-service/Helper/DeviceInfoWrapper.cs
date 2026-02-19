using FTD3XXWU_NET;
using System.Text;

namespace pm1000_streamer_service;

/// <summary>
/// Wraps the FTDI device info class to something more easily manageable.
/// </summary>
public class DeviceInfoWrapper
{
    public FTDI.FT_DEVICE_INFO DeviceInfoObj { get; private set; }

    public string SerialNumber { get; private set; }

    public string Description { get; private set; }

    public DeviceInfoWrapper(FTDI.FT_DEVICE_INFO deviceInfo)
    {
        this.DeviceInfoObj = deviceInfo;

        this.SerialNumber = Encoding.ASCII.GetString(deviceInfo.SerialNumber, 0, deviceInfo.SerialNumber.Length);

        this.Description  = Encoding.ASCII.GetString(deviceInfo.Description, 0, deviceInfo.Description.Length);
    }
}
