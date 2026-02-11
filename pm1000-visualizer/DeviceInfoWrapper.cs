using FTD2XX_NET;

namespace pm1000_visualizer
{
    public class DeviceInfoWrapper
    {
        public FTDI.FT_DEVICE_INFO_NODE DeviceInfoObj { get; private set; }

        public string SerialNumber { get; set; }

        public string Description { get; set; }

        public DeviceInfoWrapper(FTDI.FT_DEVICE_INFO_NODE deviceInfo)
        {
            this.DeviceInfoObj = deviceInfo;

            this.SerialNumber = deviceInfo.SerialNumber;

            this.Description  = deviceInfo.Description;
        }
    }
}
