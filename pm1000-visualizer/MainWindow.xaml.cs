using FTD2XX_NET;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Ink;

namespace pm1000_visualizer
{
    public partial class MainWindow : Window
    {
        public FTDI Ftdi { get; set; } = new();

        public ObservableCollection<DeviceInfoWrapper> Devices { get; set; } = new();

        private DeviceInfoWrapper? selectedDevice { get; set; } = null;

        // Device specific constants.
        private const UInt32 BAUD_RATE = 230400;
        private const byte   LATENCY = 2;

        private const UInt32 RegisterBaseAddr = 512;

        private const UInt32 S0uWU = RegisterBaseAddr + 10; // Input power in µW, integer part.
        private const UInt32 S0uWL = RegisterBaseAddr + 11; // Input power in µW, fractional part. Updated at last read of S0uWU.

        private const UInt32 S1uWU = RegisterBaseAddr + 12; // S1 of Stokes vector normalized to 1 µW, integer part. Offset=2^15.
        private const UInt32 S1uWL = RegisterBaseAddr + 13; // S1 of Stokes vector normalized to 1 µW, fractional part. Updated at last read of S1uWU.

        private const UInt32 S2uWU = RegisterBaseAddr + 14; // S2 of Stokes vector normalized to 1 µW, integer part. Offset=2^15.
        private const UInt32 S2uWL = RegisterBaseAddr + 15; // S2 of Stokes vector normalized to 1 µW, fractional part. Updated at last read of S2uWU.

        private const UInt32 S3uWU = RegisterBaseAddr + 16; // S3 of Stokes vector normalized to 1 µW, integer part. Offset=2^15.
        private const UInt32 S3uWL = RegisterBaseAddr + 17; // S3 of Stokes vector normalized to 1 µW, fractional part. Updated at last read of S3uWU.

        private const UInt32 DOPSt = RegisterBaseAddr + 24; // Degree of polarization (DOP), 16 bit unsigned, 15 fractional bits.

        private const byte CarriageReturn = 0x0D; // Carriage Return character.

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshBtn.IsEnabled = false;

            Devices.Clear();

            UInt32 deviceCount = 0;

            var status = Ftdi.GetNumberOfDevices(ref deviceCount);

            if (status != FTDI.FT_STATUS.FT_OK || deviceCount == 0) { RefreshBtn.IsEnabled = true; return; }

            FTDI.FT_DEVICE_INFO_NODE[] deviceList = new FTDI.FT_DEVICE_INFO_NODE[deviceCount]; // Allocate memory for device info list.

            try
            {
                status = Ftdi.GetDeviceList(deviceList);
            }
            catch { }

            if (status != FTDI.FT_STATUS.FT_OK) { RefreshBtn.IsEnabled = true; return; }

            foreach (var device in deviceList)
            {
                Devices.Add(new DeviceInfoWrapper(device));
            }

            RefreshBtn.IsEnabled = true;
        }

        private void DeviceSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RecordBtn.IsEnabled = false;
            StopRecordBtn.IsEnabled = false;

            var obj = e.AddedItems[0];

            selectedDevice = (DeviceInfoWrapper?)obj;

            RecordBtn.IsEnabled = true;
        }

        private void RecordBtn_Click(object sender, RoutedEventArgs e)
        {
            RecordBtn.IsEnabled = false;

            if (selectedDevice == null) { MessageBox.Show("No selected device!"); return; }

            var status = Ftdi.OpenBySerialNumber(selectedDevice.SerialNumber); // Open the device.

            if (status != FTDI.FT_STATUS.FT_OK)
            {
                MessageBox.Show($"Couldn't connect to device: {selectedDevice.SerialNumber}");
            }

            MessageBox.Show($"Connected to device: {selectedDevice.SerialNumber}");

            var baudStatus = Ftdi.SetBaudRate(BAUD_RATE);

            var latStatus  = Ftdi.SetLatency(LATENCY);

            if (baudStatus != FTDI.FT_STATUS.FT_OK && latStatus != FTDI.FT_STATUS.FT_OK) { Ftdi.Close(); return; }

            
        }

        private void StopRecordBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private double ConvertIntegerAndFractionalToDouble(UInt16 integral, UInt16 fractional)
        {

        }
        
        private UInt32 ReadDataRegister(UInt32 addr)
        {
            byte[] requestPacket = new byte[9];

            requestPacket[0] = Encoding.ASCII.GetBytes("R")[0];
            requestPacket[1] = (byte)((addr >> 16) & 0xFF);
            requestPacket[2] = (byte)((addr >> 8) & 0xFF);
            requestPacket[3] = (byte)(addr & 0xFF);
            requestPacket[4] = Encoding.ASCII.GetBytes("0")[0];
            requestPacket[5] = Encoding.ASCII.GetBytes("0")[0];
            requestPacket[6] = Encoding.ASCII.GetBytes("0")[0];
            requestPacket[7] = Encoding.ASCII.GetBytes("0")[0];
            requestPacket[8] = CarriageReturn;

            if (!Ftdi.IsOpen) return 0;

            UInt32 bytesWritten = 0;

            Ftdi.Write(requestPacket, requestPacket.Length, ref bytesWritten);

            UInt32 rxQueue = 0;

            Ftdi.GetRxBytesAvailable(ref rxQueue);

            if (rxQueue == 0) return 0;

            byte[] readBuffer = new byte[5];

            UInt32 bytesRead = 0;

            Ftdi.Read(readBuffer, (UInt32)readBuffer.Length, ref bytesRead);
            
            UInt32 result = ((UInt32)readBuffer[0] << 24) |
                            ((UInt32)readBuffer[0] << 16) |
                            ((UInt32)readBuffer[0] << 8) |
                            (UInt32)readBuffer[0];

            return result;
        }
    }
}