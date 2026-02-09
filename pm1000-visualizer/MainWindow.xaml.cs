using System.Collections.ObjectModel;
using System.Windows;
using FTD2XX_NET;

namespace pm1000_visualizer
{
    public partial class MainWindow : Window
    {
        public FTDI Ftdi { get; set; } = new();

        public ObservableCollection<DeviceInfoWrapper> Devices { get; set; } = new();

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
            
        }

        private void RecordBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void StopRecordBtn_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}