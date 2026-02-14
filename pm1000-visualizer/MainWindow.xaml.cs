using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace pm1000_visualizer;

public partial class MainWindow : Window
{
    public ObservableCollection<DeviceInfoWrapper> Devices { get; private set; } = new();

    public MainWindow()
    {
        InitializeComponent();

        this.DataContext = this;
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        RefreshBtn.IsEnabled = false;

        var deviceInfo = FtdiService.GetConnectedDevicesInfo();

        deviceInfo.ForEach(info => Devices.Add(info));

        RefreshBtn.IsEnabled = true;
    }

    private void DeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void RecordBtn_Click(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void StopRecordBtn_Click(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }
}