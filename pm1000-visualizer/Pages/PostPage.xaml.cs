using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using pm1000_visualizer.Communication;
using pm1000_visualizer.Models;
using pm1000_visualizer.Services;

namespace pm1000_visualizer.Pages;

public partial class PostPage : UserControl
{
    private readonly MeasurementSession _session;
    private string? _tempFolder;

    // Poincaré playback
    private const double SPHERE_RADIUS = 5.0;
    private const int PLAYBACK_TRAIL = 40;
    private ModelVisual3D? _lightsVisual;
    private Model3D? _sphereModel;
    private DispatcherTimer? _playbackTimer;
    private DateTime _playbackStartWall;
    private long _playbackStartMs;
    private int _playbackIndex;
    private bool _isPlaying;
    private bool _sliderDragging = false; // reserved for future drag-to-seek

    /// <summary>Raised when the user wants to start a new measurement.</summary>
    public event Action? NewMeasurementRequested;

    public PostPage(MeasurementSession session)
    {
        _session = session;
        InitializeComponent();
        Loaded += PostPage_Loaded;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void PostPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Summary labels
        DurationText.Text = _session.Duration.ToString(@"m\:ss");
        StokesCountText.Text = _session.StokesData.Count.ToString("N0");
        RawCountText.Text = _session.RawAudioSamples.Count.ToString("N0");
        ProcessedCountText.Text = _session.ProcessedAudioSamples.Count.ToString("N0");

        // Save temp WAVs for playback
        _tempFolder = Path.Combine(Path.GetTempPath(), "PM1000", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);

        var rawPath = Path.Combine(_tempFolder, "audio_raw.wav");
        var procPath = Path.Combine(_tempFolder, "audio_processed.wav");
        FileSaver.SaveWav(_session.RawAudioSamples, _session.RawAudioSampleRate, rawPath);
        FileSaver.SaveWav(_session.ProcessedAudioSamples, _session.ProcessedAudioSampleRate, procPath);

        if (File.Exists(rawPath))
        {
            RawMedia.Source = new Uri(rawPath);
            RawStatusText.Text = $"{_session.RawAudioSamples.Count:N0} samples";
        }
        if (File.Exists(procPath))
        {
            ProcessedMedia.Source = new Uri(procPath);
            ProcessedStatusText.Text = $"{_session.ProcessedAudioSamples.Count:N0} samples";
        }

        // Build sphere
        _sphereModel = BuildSphere();
        _lightsVisual = new ModelVisual3D { Content = BuildLights() };

        // Playback slider range
        if (_session.StokesData.Count > 0)
        {
            PlaybackSlider.Maximum = _session.StokesData[^1].TimestampMs;
            var totalMs = _session.StokesData[^1].TimestampMs;
            PlaybackTimeText.Text = $"0:00 / {TimeSpan.FromMilliseconds(totalMs):m\\:ss}";
        }

        RenderSphereAt(0);
    }

    // ── Poincaré sphere helpers ────────────────────────────────────────────────

    private void RenderSphereAt(int centerIndex)
    {
        if (_session.StokesData.Count == 0) return;
        centerIndex = Math.Clamp(centerIndex, 0, _session.StokesData.Count - 1);

        var scene = new Model3DGroup();
        scene.Children.Add(_sphereModel!);

        int start = Math.Max(0, centerIndex - PLAYBACK_TRAIL);
        for (int i = start; i <= centerIndex; i++)
        {
            var s = _session.StokesData[i].Sample;
            double len = Math.Sqrt(s.S1 * s.S1 + s.S2 * s.S2 + s.S3 * s.S3);
            if (len < 0.001) continue;

            int age = centerIndex - i;
            double t = (double)age / PLAYBACK_TRAIL;
            var color = System.Windows.Media.Color.FromRgb(
                (byte)(255 * (1 - t)),
                (byte)(100 * t),
                (byte)(100 * t));
            double size = 0.08 * (1 - t * 0.5);

            var mesh = new MeshBuilder();
            mesh.AddSphere(new Point3D(s.S1 / len * SPHERE_RADIUS,
                                        s.S2 / len * SPHERE_RADIUS,
                                        s.S3 / len * SPHERE_RADIUS), size, 10, 10);
            scene.Children.Add(new GeometryModel3D(mesh.ToMesh(),
                new DiffuseMaterial(new SolidColorBrush(color))));
        }

        PlaybackView.Children.Clear();
        PlaybackView.Children.Add(_lightsVisual!);
        PlaybackView.Children.Add(new ModelVisual3D { Content = scene });
    }

    private static Model3D BuildSphere()
    {
        var mesh = new MeshBuilder();
        mesh.AddSphere(new Point3D(0, 0, 0), SPHERE_RADIUS, 32, 32);
        var mat = new DiffuseMaterial(new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(90, 160, 160, 160)));
        var model = new GeometryModel3D(mesh.ToMesh(), mat);
        model.BackMaterial = mat;
        return model;
    }

    private static Model3DGroup BuildLights()
    {
        var g = new Model3DGroup();
        g.Children.Add(new AmbientLight(Colors.White));
        g.Children.Add(new DirectionalLight(Colors.White, new Vector3D(1, 1, 1)));
        return g;
    }

    // ── Playback controls ──────────────────────────────────────────────────────

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_session.StokesData.Count == 0) return;

        if (_isPlaying)
        {
            PausePlayback();
            return;
        }

        _isPlaying = true;
        PlayBtn.Content = "⏸  Pause";
        _playbackStartWall = DateTime.Now;
        _playbackStartMs = (long)PlaybackSlider.Value;

        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _playbackTimer.Tick += PlaybackTick;
        _playbackTimer.Start();
    }

    private void PlaybackTick(object? sender, EventArgs e)
    {
        if (_session.StokesData.Count == 0) return;

        double elapsed = (DateTime.Now - _playbackStartWall).TotalMilliseconds;
        long targetMs = _playbackStartMs + (long)elapsed;

        // Find closest index
        while (_playbackIndex < _session.StokesData.Count - 1 &&
               _session.StokesData[_playbackIndex].TimestampMs < targetMs)
            _playbackIndex++;

        if (_playbackIndex >= _session.StokesData.Count - 1)
        {
            StopPlaybackInternal();
            return;
        }

        RenderSphereAt(_playbackIndex);

        if (!_sliderDragging)
            PlaybackSlider.Value = _session.StokesData[_playbackIndex].TimestampMs;

        long curMs = _session.StokesData[_playbackIndex].TimestampMs;
        long totalMs = _session.StokesData[^1].TimestampMs;
        PlaybackTimeText.Text = $"{TimeSpan.FromMilliseconds(curMs):m\\:ss} / {TimeSpan.FromMilliseconds(totalMs):m\\:ss}";
    }

    private void PausePlayback()
    {
        _isPlaying = false;
        _playbackTimer?.Stop();
        PlayBtn.Content = "▶  Play";
    }

    private void StopPlaybackInternal()
    {
        _isPlaying = false;
        _playbackTimer?.Stop();
        _playbackIndex = 0;
        PlaybackSlider.Value = 0;
        PlayBtn.Content = "▶  Play";
        RenderSphereAt(0);
    }

    private void StopPlayback_Click(object sender, RoutedEventArgs e) => StopPlaybackInternal();

    private void PlaybackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isPlaying) return; // timer controls the view while playing
        if (_session.StokesData.Count == 0) return;

        long ms = (long)e.NewValue;
        // Binary-search-ish: find closest index
        int idx = 0;
        for (int i = 0; i < _session.StokesData.Count; i++)
        {
            if (_session.StokesData[i].TimestampMs >= ms) { idx = i; break; }
            idx = i;
        }

        _playbackIndex = idx;
        RenderSphereAt(idx);

        long totalMs = _session.StokesData[^1].TimestampMs;
        PlaybackTimeText.Text = $"{TimeSpan.FromMilliseconds(ms):m\\:ss} / {TimeSpan.FromMilliseconds(totalMs):m\\:ss}";
    }

    // ── Audio playback ─────────────────────────────────────────────────────────

    private void PlayProcessed_Click(object sender, RoutedEventArgs e)
    {
        try { ProcessedMedia.Play(); ProcessedStatusText.Text = "Playing…"; }
        catch (Exception ex) { ProcessedStatusText.Text = $"Error: {ex.Message}"; }
    }

    private void StopProcessed_Click(object sender, RoutedEventArgs e)
    {
        ProcessedMedia.Stop();
        ProcessedStatusText.Text = "Stopped";
    }

    private void PlayRaw_Click(object sender, RoutedEventArgs e)
    {
        try { RawMedia.Play(); RawStatusText.Text = "Playing…"; }
        catch (Exception ex) { RawStatusText.Text = $"Error: {ex.Message}"; }
    }

    private void StopRaw_Click(object sender, RoutedEventArgs e)
    {
        RawMedia.Stop();
        RawStatusText.Text = "Stopped";
    }

    // ── Save handlers ──────────────────────────────────────────────────────────

    private void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose folder for measurement data" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            string folder = FileSaver.SaveAll(_session, dlg.FolderName);
            ShowSaveStatus($"All files saved to {folder}");
        }
        catch (Exception ex) { ShowSaveStatus($"Error: {ex.Message}"); }
    }

    private void SaveStokes_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = "stokes.csv",
            Filter = "CSV files (*.csv)|*.csv",
            Title = "Save Stokes data"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            FileSaver.SaveStokesCsv(_session, dlg.FileName);
            ShowSaveStatus($"Stokes CSV saved: {dlg.FileName}");
        }
        catch (Exception ex) { ShowSaveStatus($"Error: {ex.Message}"); }
    }

    private void SaveRawAudio_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = "audio_raw.wav",
            Filter = "WAV files (*.wav)|*.wav",
            Title = "Save raw audio"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            FileSaver.SaveWav(_session.RawAudioSamples, _session.RawAudioSampleRate, dlg.FileName);
            ShowSaveStatus($"Raw audio saved: {dlg.FileName}");
        }
        catch (Exception ex) { ShowSaveStatus($"Error: {ex.Message}"); }
    }

    private void SaveProcessedAudio_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = "audio_processed.wav",
            Filter = "WAV files (*.wav)|*.wav",
            Title = "Save processed audio"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            FileSaver.SaveWav(_session.ProcessedAudioSamples, _session.ProcessedAudioSampleRate, dlg.FileName);
            ShowSaveStatus($"Processed audio saved: {dlg.FileName}");
        }
        catch (Exception ex) { ShowSaveStatus($"Error: {ex.Message}"); }
    }

    private void ShowSaveStatus(string msg)
    {
        SaveStatusText.Text = msg;
        SaveStatusText.Visibility = Visibility.Visible;
    }

    // ── New measurement ────────────────────────────────────────────────────────

    private void NewMeasurement_Click(object sender, RoutedEventArgs e)
    {
        _playbackTimer?.Stop();
        ProcessedMedia.Stop();
        RawMedia.Stop();
        NewMeasurementRequested?.Invoke();
    }
}
