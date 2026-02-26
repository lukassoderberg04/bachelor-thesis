using System.Windows;
using pm1000_visualizer.Models;
using pm1000_visualizer.Pages;

namespace pm1000_visualizer;

/// <summary>
/// Shell window — hosts a single ContentControl that swaps between three pages:
///   PreConnectionPage → LivePage → PostPage → (back to Pre)
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowPreConnectionPage();
    }

    // ── Navigation ─────────────────────────────────────────────────────────────

    private void ShowPreConnectionPage()
    {
        var page = new PreConnectionPage();
        page.ConnectRequested += OnConnectRequested;
        PageContent.Content = page;
        Title = "PM1000 Visualizer";
    }

    private void OnConnectRequested(ConnectionSettings settings)
    {
        var session = new MeasurementSession
        {
            StreamerIp = settings.StreamerIp,
            ApiPort = settings.ApiPort,
            IsIndefinite = settings.IsIndefinite,
            DurationSeconds = settings.DurationSeconds,
        };

        var page = new LivePage(settings, session);
        page.StopRequested += () => ShowPostPage(session);
        PageContent.Content = page;
        Title = "PM1000 Visualizer — Live";
    }

    private void ShowPostPage(MeasurementSession session)
    {
        var page = new PostPage(session);
        page.NewMeasurementRequested += ShowPreConnectionPage;
        PageContent.Content = page;
        Title = "PM1000 Visualizer — Post";
    }

    // ── Window cleanup ─────────────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // If a LivePage is active it will be unloaded, triggering its own cleanup
    }
}
