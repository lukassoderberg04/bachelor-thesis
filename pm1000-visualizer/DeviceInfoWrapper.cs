using FTD2XX_NET;

namespace pm1000_visualizer
{
    public class DeviceInfoWrapper
    {
        private FTDI.FT_DEVICE_INFO_NODE deviceInfo { get; set; }

        public string SerialNumber { get; set; }

        public string Description { get; set; }

        public DeviceInfoWrapper(FTDI.FT_DEVICE_INFO_NODE deviceInfo)
        {
            this.deviceInfo = deviceInfo;

            this.SerialNumber = deviceInfo.SerialNumber;

            this.Description  = deviceInfo.Description;
        }
    }
}
