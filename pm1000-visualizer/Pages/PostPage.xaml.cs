using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using pm1000_visualizer.Communication;
using pm1000_visualizer.Models;
using pm1000_visualizer.Services;

namespace pm1000_visualizer.Pages;

/// <summary>
/// Post-measurement page with THREE independent seekable players:
///   1. Poincaré sphere replay
///   2. Processed audio
///   3. Raw reference audio
/// Each has play/pause, stop, skip ±5 s, and a seekable slider.
/// </summary>
public partial class PostPage : UserControl
{
    // ── Constants ───────────────────────────────────────────────────────────
    private const double SPHERE_RADIUS = 5.0;
    private const int PLAYBACK_TRAIL = 60;

    // ── Session ────────────────────────────────────────────────────────────
    private readonly MeasurementSession _session;
    private string? _tempFolder;

    // ── 3D scene (built once) ──────────────────────────────────────────────
    private ModelVisual3D? _lightsVisual;
    private Model3D? _sphereModel;
    private ModelVisual3D? _trailVisual;

    // ── Shared frozen mesh ─────────────────────────────────────────────────
    private static readonly MeshGeometry3D s_trailMesh;
    static PostPage()
    {
        var mb = new MeshBuilder();
        mb.AddSphere(new Point3D(0, 0, 0), 1.0, 6, 6);
        s_trailMesh = mb.ToMesh();
        s_trailMesh.Freeze();
    }

    // ── Sphere playback state ──────────────────────────────────────────────
    private bool _spherePlaying;
    private DateTime _sphereWallStart;
    private long _sphereStartMs;
    private int _sphereIndex;
    private long _sphereDurationMs;
    private bool _sphereDragging;
    private bool _suppressSphereSlider;

    // ── Processed audio state ──────────────────────────────────────────────
    private bool _procPlaying;
    private bool _procDragging;
    private bool _procReady;
    private TimeSpan _procDuration;
    private bool _suppressProcSlider;

    // ── Raw audio state ────────────────────────────────────────────────────
    private bool _rawPlaying;
    private bool _rawDragging;
    private bool _rawReady;
    private TimeSpan _rawDuration;
    private bool _suppressRawSlider;

    // ── Shared tick timer ──────────────────────────────────────────────────
    private DispatcherTimer? _tickTimer;

    /// <summary>Raised when the user wants to start a new measurement.</summary>
    public event Action? NewMeasurementRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // Construction
    // ═══════════════════════════════════════════════════════════════════════

    public PostPage(MeasurementSession session)
    {
        _session = session;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Summary
        DurationText.Text = _session.Duration.ToString(@"m\:ss");
        StokesCountText.Text = _session.StokesData.Count.ToString("N0");
        RawCountText.Text = _session.RawAudioSamples.Count.ToString("N0");
        ProcessedCountText.Text = _session.ProcessedAudioSamples.Count.ToString("N0");

        // ── Sphere duration from stokes timestamps ──────────────────────
        if (_session.StokesData.Count > 0)
            _sphereDurationMs = _session.StokesData[^1].TimestampMs;
        else
            _sphereDurationMs = (long)_session.Duration.TotalMilliseconds;

        SphereSlider.Maximum = Math.Max(_sphereDurationMs, 1);
        SphereTimeText.Text = $"0:00 / {FmtMs(_sphereDurationMs)}";

        // ── Save temp WAVs ──────────────────────────────────────────────
        _tempFolder = Path.Combine(Path.GetTempPath(), "PM1000", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);

        var rawPath = Path.Combine(_tempFolder, "audio_raw.wav");
        var procPath = Path.Combine(_tempFolder, "audio_processed.wav");

        Logger.LogInfo($"Saving temp WAVs — raw: {_session.RawAudioSamples.Count} samples @ {_session.RawAudioSampleRate} Hz, " +
                       $"proc: {_session.ProcessedAudioSamples.Count} samples @ {_session.ProcessedAudioSampleRate} Hz");

        FileSaver.SaveWav(_session.RawAudioSamples, _session.RawAudioSampleRate, rawPath);
        FileSaver.SaveWav(_session.ProcessedAudioSamples, _session.ProcessedAudioSampleRate, procPath);

        // Load raw audio
        if (File.Exists(rawPath) && new FileInfo(rawPath).Length > 44)
        {
            RawMedia.Source = new Uri(rawPath);
            RawMedia.Play();   // LoadedBehavior=Manual requires Play() to begin loading
            RawMedia.Pause();  // pause immediately; MediaOpened will fire once ready
            RawStatusText.Text = $"{_session.RawAudioSamples.Count:N0} samples @ {_session.RawAudioSampleRate} Hz";
        }
        else
        {
            RawStatusText.Text = "No audio";
        }

        // Load processed audio
        if (File.Exists(procPath) && new FileInfo(procPath).Length > 44)
        {
            ProcessedMedia.Source = new Uri(procPath);
            ProcessedMedia.Play();   // same — trigger load
            ProcessedMedia.Pause();
            ProcStatusText.Text = $"{_session.ProcessedAudioSamples.Count:N0} samples @ {_session.ProcessedAudioSampleRate} Hz";
        }
        else
        {
            ProcStatusText.Text = "No audio";
        }

        // ── Build 3D scene (once) ───────────────────────────────────────
        _sphereModel = BuildSphere();
        _lightsVisual = new ModelVisual3D { Content = BuildLights() };
        _trailVisual = new ModelVisual3D();

        PlaybackView.Children.Clear();
        PlaybackView.Children.Add(_lightsVisual);
        PlaybackView.Children.Add(new ModelVisual3D { Content = _sphereModel });
        PlaybackView.Children.Add(_trailVisual);
        PlaybackView.ZoomExtents(0);
        RenderSphereAt(0);

        // ── Single tick timer drives all 3 players ──────────────────────
        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // ~30 fps
        _tickTimer.Tick += Tick;
        _tickTimer.Start();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tick — updates all 3 players
    // ═══════════════════════════════════════════════════════════════════════

    private void Tick(object? sender, EventArgs e)
    {
        // ── Sphere ──────────────────────────────────────────────────────
        if (_spherePlaying && _session.StokesData.Count > 0 && !_sphereDragging)
        {
            double elapsed = (DateTime.Now - _sphereWallStart).TotalMilliseconds;
            long targetMs = _sphereStartMs + (long)elapsed;

            if (targetMs >= _sphereDurationMs)
            {
                StopSphere();
            }
            else
            {
                _sphereIndex = FindStokesIndex(targetMs);
                RenderSphereAt(_sphereIndex);
                _suppressSphereSlider = true;
                SphereSlider.Value = targetMs;
                _suppressSphereSlider = false;
                SphereTimeText.Text = $"{FmtMs(targetMs)} / {FmtMs(_sphereDurationMs)}";
            }
        }

        // ── Processed audio ─────────────────────────────────────────────
        if (_procPlaying && _procReady && !_procDragging)
        {
            try
            {
                var pos = ProcessedMedia.Position;
                if (_procDuration.TotalMilliseconds > 0 && pos >= _procDuration)
                {
                    StopProc();
                }
                else
                {
                    _suppressProcSlider = true;
                    ProcSlider.Value = pos.TotalMilliseconds;
                    _suppressProcSlider = false;
                    ProcTimeText.Text = $"{FmtTs(pos)} / {FmtTs(_procDuration)}";
                }
            }
            catch { /* MediaElement may not be ready */ }
        }

        // ── Raw audio ───────────────────────────────────────────────────
        if (_rawPlaying && _rawReady && !_rawDragging)
        {
            try
            {
                var pos = RawMedia.Position;
                if (_rawDuration.TotalMilliseconds > 0 && pos >= _rawDuration)
                {
                    StopRaw();
                }
                else
                {
                    _suppressRawSlider = true;
                    RawSlider.Value = pos.TotalMilliseconds;
                    _suppressRawSlider = false;
                    RawTimeText.Text = $"{FmtTs(pos)} / {FmtTs(_rawDuration)}";
                }
            }
            catch { /* MediaElement may not be ready */ }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPHERE PLAYER
    // ═══════════════════════════════════════════════════════════════════════

    private void SpherePlay_Click(object sender, RoutedEventArgs e)
    {
        if (_session.StokesData.Count == 0) return;
        if (_spherePlaying) { PauseSphere(); return; }

        _spherePlaying = true;
        SpherePlayBtn.Content = "⏸  Pause";
        _sphereWallStart = DateTime.Now;
        _sphereStartMs = (long)SphereSlider.Value;
    }

    private void SphereStop_Click(object sender, RoutedEventArgs e) => StopSphere();
    private void SphereBack_Click(object sender, RoutedEventArgs e) => SeekSphereRelative(-5000);
    private void SphereFwd_Click(object sender, RoutedEventArgs e) => SeekSphereRelative(5000);

    private void PauseSphere()
    {
        _spherePlaying = false;
        SpherePlayBtn.Content = "▶  Play";
    }

    private void StopSphere()
    {
        _spherePlaying = false;
        _sphereIndex = 0;
        SpherePlayBtn.Content = "▶  Play";
        SphereSlider.Value = 0;
        RenderSphereAt(0);
        SphereTimeText.Text = $"0:00 / {FmtMs(_sphereDurationMs)}";
    }

    private void SeekSphereRelative(long deltaMs)
    {
        long current = (long)SphereSlider.Value;
        long target = Math.Clamp(current + deltaMs, 0, _sphereDurationMs);
        SeekSphereTo(target);
    }

    private void SeekSphereTo(long ms)
    {
        ms = Math.Clamp(ms, 0, _sphereDurationMs);
        _sphereIndex = FindStokesIndex(ms);
        RenderSphereAt(_sphereIndex);
        SphereSlider.Value = ms;
        SphereTimeText.Text = $"{FmtMs(ms)} / {FmtMs(_sphereDurationMs)}";

        if (_spherePlaying)
        {
            _sphereWallStart = DateTime.Now;
            _sphereStartMs = ms;
        }
    }

    private bool _sphereWasPlayingBeforeDrag;

    private void SphereSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _sphereDragging = true;
        _sphereWasPlayingBeforeDrag = _spherePlaying;
        if (_spherePlaying) PauseSphere();
    }

    private void SphereSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _sphereDragging = false;
        SeekSphereTo((long)SphereSlider.Value);
        if (_sphereWasPlayingBeforeDrag)
        {
            _spherePlaying = true;
            SpherePlayBtn.Content = "⏸  Pause";
            _sphereWallStart = DateTime.Now;
            _sphereStartMs = (long)SphereSlider.Value;
        }
    }

    private void SphereSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSphereSlider || _spherePlaying) return;
        if (_session.StokesData.Count == 0) return;

        long ms = (long)e.NewValue;
        _sphereIndex = FindStokesIndex(ms);
        RenderSphereAt(_sphereIndex);
        SphereTimeText.Text = $"{FmtMs(ms)} / {FmtMs(_sphereDurationMs)}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROCESSED AUDIO PLAYER
    // ═══════════════════════════════════════════════════════════════════════

    private void ProcPlay_Click(object sender, RoutedEventArgs e)
    {
        if (!_procReady) return;
        if (_procPlaying) { PauseProc(); return; }

        _procPlaying = true;
        ProcPlayBtn.Content = "⏸  Pause";
        try { ProcessedMedia.Play(); } catch { }
    }

    private void ProcStop_Click(object sender, RoutedEventArgs e) => StopProc();
    private void ProcBack_Click(object sender, RoutedEventArgs e) => SeekProcRelative(-5000);
    private void ProcFwd_Click(object sender, RoutedEventArgs e) => SeekProcRelative(5000);

    private void PauseProc()
    {
        _procPlaying = false;
        ProcPlayBtn.Content = "▶  Play";
        try { ProcessedMedia.Pause(); } catch { }
    }

    private void StopProc()
    {
        _procPlaying = false;
        ProcPlayBtn.Content = "▶  Play";
        try { ProcessedMedia.Stop(); } catch { }
        ProcSlider.Value = 0;
        ProcTimeText.Text = $"0:00 / {FmtTs(_procDuration)}";
    }

    private void SeekProcRelative(long deltaMs)
    {
        if (!_procReady) return;
        double current = ProcSlider.Value;
        double target = Math.Clamp(current + deltaMs, 0, _procDuration.TotalMilliseconds);
        SeekProcTo(target);
    }

    private void SeekProcTo(double ms)
    {
        ms = Math.Clamp(ms, 0, _procDuration.TotalMilliseconds);
        try { ProcessedMedia.Position = TimeSpan.FromMilliseconds(ms); } catch { }
        ProcSlider.Value = ms;
        ProcTimeText.Text = $"{FmtTs(TimeSpan.FromMilliseconds(ms))} / {FmtTs(_procDuration)}";
    }

    private void ProcSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _procDragging = true;
        if (_procPlaying) try { ProcessedMedia.Pause(); } catch { }
    }

    private void ProcSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _procDragging = false;
        SeekProcTo(ProcSlider.Value);
        if (_procPlaying) try { ProcessedMedia.Play(); } catch { }
    }

    private void ProcSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressProcSlider || _procPlaying) return;
        if (!_procReady) return;
        SeekProcTo(e.NewValue);
    }

    private void ProcMedia_Opened(object sender, RoutedEventArgs e)
    {
        _procReady = true;
        if (ProcessedMedia.NaturalDuration.HasTimeSpan)
        {
            _procDuration = ProcessedMedia.NaturalDuration.TimeSpan;
            ProcSlider.Maximum = _procDuration.TotalMilliseconds;
            ProcTimeText.Text = $"0:00 / {FmtTs(_procDuration)}";
            Logger.LogInfo($"Processed media ready: {_procDuration}");
        }
    }

    private void ProcMedia_Ended(object sender, RoutedEventArgs e) => StopProc();

    private void ProcVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProcessedMedia != null) ProcessedMedia.Volume = e.NewValue;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RAW AUDIO PLAYER
    // ═══════════════════════════════════════════════════════════════════════

    private void RawPlay_Click(object sender, RoutedEventArgs e)
    {
        if (!_rawReady) return;
        if (_rawPlaying) { PauseRaw(); return; }

        _rawPlaying = true;
        RawPlayBtn.Content = "⏸  Pause";
        try { RawMedia.Play(); } catch { }
    }

    private void RawStop_Click(object sender, RoutedEventArgs e) => StopRaw();
    private void RawBack_Click(object sender, RoutedEventArgs e) => SeekRawRelative(-5000);
    private void RawFwd_Click(object sender, RoutedEventArgs e) => SeekRawRelative(5000);

    private void PauseRaw()
    {
        _rawPlaying = false;
        RawPlayBtn.Content = "▶  Play";
        try { RawMedia.Pause(); } catch { }
    }

    private void StopRaw()
    {
        _rawPlaying = false;
        RawPlayBtn.Content = "▶  Play";
        try { RawMedia.Stop(); } catch { }
        RawSlider.Value = 0;
        RawTimeText.Text = $"0:00 / {FmtTs(_rawDuration)}";
    }

    private void SeekRawRelative(long deltaMs)
    {
        if (!_rawReady) return;
        double current = RawSlider.Value;
        double target = Math.Clamp(current + deltaMs, 0, _rawDuration.TotalMilliseconds);
        SeekRawTo(target);
    }

    private void SeekRawTo(double ms)
    {
        ms = Math.Clamp(ms, 0, _rawDuration.TotalMilliseconds);
        try { RawMedia.Position = TimeSpan.FromMilliseconds(ms); } catch { }
        RawSlider.Value = ms;
        RawTimeText.Text = $"{FmtTs(TimeSpan.FromMilliseconds(ms))} / {FmtTs(_rawDuration)}";
    }

    private void RawSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _rawDragging = true;
        if (_rawPlaying) try { RawMedia.Pause(); } catch { }
    }

    private void RawSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _rawDragging = false;
        SeekRawTo(RawSlider.Value);
        if (_rawPlaying) try { RawMedia.Play(); } catch { }
    }

    private void RawSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressRawSlider || _rawPlaying) return;
        if (!_rawReady) return;
        SeekRawTo(e.NewValue);
    }

    private void RawMedia_Opened(object sender, RoutedEventArgs e)
    {
        _rawReady = true;
        if (RawMedia.NaturalDuration.HasTimeSpan)
        {
            _rawDuration = RawMedia.NaturalDuration.TimeSpan;
            RawSlider.Maximum = _rawDuration.TotalMilliseconds;
            RawTimeText.Text = $"0:00 / {FmtTs(_rawDuration)}";
            Logger.LogInfo($"Raw media ready: {_rawDuration}");
        }
    }

    private void RawMedia_Ended(object sender, RoutedEventArgs e) => StopRaw();

    private void RawVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RawMedia != null) RawMedia.Volume = e.NewValue;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Shared media error handler
    // ═══════════════════════════════════════════════════════════════════════

    private void Media_Failed(object sender, ExceptionRoutedEventArgs e)
    {
        Logger.LogError($"Media error: {e.ErrorException?.Message}");
        if (sender == ProcessedMedia) ProcStatusText.Text = "Error";
        else if (sender == RawMedia) RawStatusText.Text = "Error";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Sphere rendering (shared frozen mesh)
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderSphereAt(int centerIndex)
    {
        if (_session.StokesData.Count == 0) return;
        centerIndex = Math.Clamp(centerIndex, 0, _session.StokesData.Count - 1);

        var group = new Model3DGroup();
        int start = Math.Max(0, centerIndex - PLAYBACK_TRAIL);

        for (int i = start; i <= centerIndex; i++)
        {
            var s = _session.StokesData[i].Sample;
            double len = Math.Sqrt(s.S1 * s.S1 + s.S2 * s.S2 + s.S3 * s.S3);
            if (len < 0.001) continue;

            int age = centerIndex - i;
            double t = (double)age / PLAYBACK_TRAIL;
            var color = Color.FromRgb(
                (byte)(255 * (1 - t)),
                (byte)(100 * t),
                (byte)(100 * t));
            double size = 0.10 * (1 - t * 0.6);

            var pt = new Point3D(
                s.S1 / len * SPHERE_RADIUS,
                s.S2 / len * SPHERE_RADIUS,
                s.S3 / len * SPHERE_RADIUS);

            var model = new GeometryModel3D(s_trailMesh,
                new DiffuseMaterial(new SolidColorBrush(color)));
            var xform = new Transform3DGroup();
            xform.Children.Add(new ScaleTransform3D(size, size, size));
            xform.Children.Add(new TranslateTransform3D(pt.X, pt.Y, pt.Z));
            model.Transform = xform;
            group.Children.Add(model);
        }

        _trailVisual!.Content = group;
    }

    private static Model3D BuildSphere()
    {
        var mesh = new MeshBuilder();
        mesh.AddSphere(new Point3D(0, 0, 0), SPHERE_RADIUS, 32, 32);
        var mat = new DiffuseMaterial(new SolidColorBrush(
            Color.FromArgb(90, 160, 160, 160)));
        return new GeometryModel3D(mesh.ToMesh(), mat) { BackMaterial = mat };
    }

    private static Model3DGroup BuildLights()
    {
        var g = new Model3DGroup();
        g.Children.Add(new AmbientLight(Colors.White));
        g.Children.Add(new DirectionalLight(Colors.White, new Vector3D(1, 1, 1)));
        return g;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private int FindStokesIndex(long ms)
    {
        var data = _session.StokesData;
        if (data.Count == 0) return 0;
        int lo = 0, hi = data.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (data[mid].TimestampMs < ms) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static string FmtMs(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }

    private static string FmtTs(TimeSpan ts)
    {
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Save handlers
    // ═══════════════════════════════════════════════════════════════════════

    private void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose folder for measurement data" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            string folder = FileSaver.SaveAll(_session, dlg.FolderName);
            ShowSave($"All files saved to {folder}");
        }
        catch (Exception ex) { ShowSave($"Error: {ex.Message}"); }
    }

    private void SaveStokes_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { FileName = "stokes.csv", Filter = "CSV|*.csv", Title = "Save Stokes" };
        if (dlg.ShowDialog() != true) return;
        try { FileSaver.SaveStokesCsv(_session, dlg.FileName); ShowSave($"Saved: {dlg.FileName}"); }
        catch (Exception ex) { ShowSave($"Error: {ex.Message}"); }
    }

    private void SaveRawAudio_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { FileName = "audio_raw.wav", Filter = "WAV|*.wav", Title = "Save raw audio" };
        if (dlg.ShowDialog() != true) return;
        try { FileSaver.SaveWav(_session.RawAudioSamples, _session.RawAudioSampleRate, dlg.FileName); ShowSave($"Saved: {dlg.FileName}"); }
        catch (Exception ex) { ShowSave($"Error: {ex.Message}"); }
    }

    private void SaveProcessedAudio_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { FileName = "audio_processed.wav", Filter = "WAV|*.wav", Title = "Save processed audio" };
        if (dlg.ShowDialog() != true) return;
        try { FileSaver.SaveWav(_session.ProcessedAudioSamples, _session.ProcessedAudioSampleRate, dlg.FileName); ShowSave($"Saved: {dlg.FileName}"); }
        catch (Exception ex) { ShowSave($"Error: {ex.Message}"); }
    }

    private void ShowSave(string msg)
    {
        SaveStatusText.Text = msg;
        SaveStatusText.Visibility = Visibility.Visible;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // New measurement
    // ═══════════════════════════════════════════════════════════════════════

    private void NewMeasurement_Click(object sender, RoutedEventArgs e)
    {
        _tickTimer?.Stop();
        _spherePlaying = false;
        try { ProcessedMedia.Stop(); } catch { }
        try { RawMedia.Stop(); } catch { }
        NewMeasurementRequested?.Invoke();
    }
}
