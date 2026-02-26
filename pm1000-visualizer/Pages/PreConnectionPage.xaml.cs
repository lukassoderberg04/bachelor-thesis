using System.Windows;
using System.Windows.Controls;
using pm1000_visualizer.Models;

namespace pm1000_visualizer.Pages;

public partial class PreConnectionPage : UserControl
{
    /// <summary>Raised when the user clicks Connect with valid settings.</summary>
    public event Action<ConnectionSettings>? ConnectRequested;

    public PreConnectionPage()
    {
        InitializeComponent();
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        // Validate inputs
        if (!int.TryParse(ApiPortBox.Text, out int apiPort) || apiPort < 1 || apiPort > 65535)
        {
            ShowError("REST API Port must be a valid port number (1–65535).");
            return;
        }
        if (!int.TryParse(StokesPortBox.Text, out int stokesPort) || stokesPort < 1 || stokesPort > 65535)
        {
            ShowError("Stokes Port must be a valid port number.");
            return;
        }
        if (!int.TryParse(RawPortBox.Text, out int rawPort) || rawPort < 1 || rawPort > 65535)
        {
            ShowError("Raw Audio Port must be a valid port number.");
            return;
        }
        if (!int.TryParse(ProcPortBox.Text, out int procPort) || procPort < 1 || procPort > 65535)
        {
            ShowError("Processed Audio Port must be a valid port number.");
            return;
        }

        bool isFixed = FixedRadio.IsChecked == true;
        int durationSec = 30;
        if (isFixed && (!int.TryParse(DurationBox.Text, out durationSec) || durationSec < 1))
        {
            ShowError("Duration must be a positive number of seconds.");
            return;
        }

        var settings = new ConnectionSettings
        {
            StreamerIp = IpBox.Text.Trim(),
            ApiPort = apiPort,
            StokesPort = stokesPort,
            RawAudioPort = rawPort,
            ProcessedAudioPort = procPort,
            IsTestMode = TestModeCheck.IsChecked == true,
            IsIndefinite = !isFixed,
            DurationSeconds = durationSec
        };

        ConnectRequested?.Invoke(settings);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
