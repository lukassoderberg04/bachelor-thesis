using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using pm1000_visualizer.Communication;
using pm1000_visualizer.Models;
using pm1000_visualizer.Services;

namespace pm1000_visualizer.Pages;

/// <summary>
/// Live measurement page — performance-oriented rewrite.
///
/// Architecture:
///   • Background UDP threads write into thread-safe buffers (no Dispatcher calls).
///   • A single 30 fps DispatcherTimer drains all buffers and updates all UI.
///   • Trail points use a shared frozen mesh to avoid per-frame geometry allocation.
/// </summary>
public partial class LivePage : UserControl
{
    // ── Configuration ──────────────────────────────────────────────────────
    private const double SPHERE_RADIUS = 5.0;
    private const int MAX_TRAIL_POINTS = 80;
    private const int AUDIO_BUFFER_SIZE = 64000;    // ~4 s at 16 kHz
    private const int AUDIO_DISPLAY_SAMPLES = 2000; // ~125 ms visible window
    private const int RENDER_FPS = 30;

    // ── Injected state ─────────────────────────────────────────────────────
    private readonly ConnectionSettings _settings;
    private readonly MeasurementSession _session;

    // ── Infrastructure ─────────────────────────────────────────────────────
    private UdpListener? _listener;
    private DataRecorder? _recorder;
    private TestDataGenerator? _testGen;
    private StressTestGenerator? _stressGen;
    private DispatcherTimer? _renderTimer;   // THE single UI refresh clock
    private DispatcherTimer? _elapsedTimer;
    private DispatcherTimer? _durationTimer;

    // ── Stokes buffer (written by UDP thread, drained by render timer) ─────
    private readonly ConcurrentQueue<(Point3D pos, StokeSample sample)> _pendingStokes = new();
    private readonly Queue<Point3D> _trailQueue = new();
    private StokeSample _lastStokes;
    private volatile bool _hasNewStokes;
    private volatile bool _showTrail = true;

    // ── Audio buffers (written by UDP thread, snapshot by render timer) ────
    private readonly AudioRingBuffer _rawAudioRing = new(AUDIO_BUFFER_SIZE);
    private readonly AudioRingBuffer _processedAudioRing = new(AUDIO_BUFFER_SIZE);
    private long _lastRawTotal;
    private long _lastProcTotal;
    private bool _showOverlay;

    // ── 3D scene objects (created once in OnLoaded, never rebuilt) ──────────
    private ModelVisual3D? _lightsVisual;
    private Model3D? _sphereModel;
    private ModelVisual3D? _trailVisual;

    // ── Shared frozen mesh for trail dots ───────────────────────────────────
    private static readonly MeshGeometry3D s_trailMesh;

    static LivePage()
    {
        var mb = new MeshBuilder();
        mb.AddSphere(new Point3D(0, 0, 0), 1.0, 6, 6);
        s_trailMesh = mb.ToMesh();
        s_trailMesh.Freeze(); // immutable → GPU-cacheable, safe to share
    }

    /// <summary>Raised when the user presses Stop or the duration expires.</summary>
    public event Action? StopRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // Construction
    // ═══════════════════════════════════════════════════════════════════════

    public LivePage(ConnectionSettings settings, MeasurementSession session)
    {
        _settings = settings;
        _session = session;
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Header labels
        StreamerIpText.Text = _settings.StreamerIp;
        ProcessedPortText.Text = _settings.ProcessedAudioPort.ToString();
        RawPortText.Text = _settings.RawAudioPort.ToString();

        // ── 3D scene (created once, never rebuilt) ──────────────────────
        _sphereModel = BuildSphere();
        _lightsVisual = new ModelVisual3D { Content = BuildLights() };
        _trailVisual = new ModelVisual3D();

        PoincareView.Children.Clear();
        PoincareView.Children.Add(_lightsVisual);
        PoincareView.Children.Add(new ModelVisual3D { Content = _sphereModel });
        PoincareView.Children.Add(_trailVisual);
        PoincareView.ZoomExtents(0);

        // ── ScottPlot dark theme ────────────────────────────────────────
        ConfigurePlot(ProcessedAudioPlot);
        ConfigurePlot(RawAudioPlot);

        // ── UDP listener ────────────────────────────────────────────────
        _listener = new UdpListener(
            _settings.StokesPort, _settings.RawAudioPort, _settings.ProcessedAudioPort);
        _listener.StokesReceived += OnStokesReceived;
        _listener.RawAudioReceived += OnRawAudioReceived;
        _listener.ProcessedAudioReceived += OnProcessedAudioReceived;
        _listener.PacketDropped += (exp, got) =>
            Logger.LogWarning($"Packet loss: expected seq {exp}, got {got}");
        _listener.Start();

        // ── Recorder ────────────────────────────────────────────────────
        _recorder = new DataRecorder(_session);
        _recorder.Start();

        // ── Test data generators ────────────────────────────────────────
        if (_settings.IsTestMode)
        {
            _testGen = new TestDataGenerator(
                _settings.StokesPort, _settings.RawAudioPort, _settings.ProcessedAudioPort);
            _testGen.Start();
        }
        else if (_settings.IsStressTest)
        {
            _stressGen = new StressTestGenerator(
                _settings.StokesPort, _settings.RawAudioPort,
                _settings.StressStokesPerSecond, _settings.StressAudioPerSecond);
            _stressGen.Start();
        }

        // ── Duration timer (fixed-length measurement) ───────────────────
        if (!_settings.IsIndefinite)
        {
            _durationTimer = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(_settings.DurationSeconds) };
            _durationTimer.Tick += (_, _) => { _durationTimer.Stop(); DoStop(); };
            _durationTimer.Start();
        }

        // ── Elapsed time display ────────────────────────────────────────
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _elapsedTimer.Tick += (_, _) =>
        {
            if (_recorder != null)
                ElapsedText.Text = TimeSpan.FromMilliseconds(_recorder.ElapsedMs)
                    .ToString(@"m\:ss");
        };
        _elapsedTimer.Start();

        // ── THE KEY: single render timer drives ALL visual updates ──────
        _renderTimer = new DispatcherTimer
        { Interval = TimeSpan.FromMilliseconds(1000.0 / RENDER_FPS) };
        _renderTimer.Tick += RenderTick;
        _renderTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Cleanup();

    // ═══════════════════════════════════════════════════════════════════════
    // Background-thread callbacks — NO Dispatcher, NO UI, just buffer data
    // ═══════════════════════════════════════════════════════════════════════

    private void OnStokesReceived(StokesPacket packet)
    {
        _recorder?.RecordStokes(packet);

        foreach (var s in packet.Samples)
        {
            // Always update readout — even when S1/S2/S3 are near-zero
            _lastStokes = s;

            double len = Math.Sqrt(s.S1 * s.S1 + s.S2 * s.S2 + s.S3 * s.S3);
            if (len < 0.001) continue; // skip sphere plot for degenerate vector

            var pt = new Point3D(
                s.S1 / len * SPHERE_RADIUS,
                s.S2 / len * SPHERE_RADIUS,
                s.S3 / len * SPHERE_RADIUS);
            _pendingStokes.Enqueue((pt, s));
        }
        _hasNewStokes = true;
    }

    private void OnRawAudioReceived(AudioPacket packet)
    {
        _recorder?.RecordRawAudio(packet);
        _rawAudioRing.Write(packet.Samples);
    }

    private void OnProcessedAudioReceived(AudioPacket packet)
    {
        _recorder?.RecordProcessedAudio(packet);
        _processedAudioRing.Write(packet.Samples);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Render tick — UI thread, ~30 FPS — the ONLY place that touches UI
    // ═══════════════════════════════════════════════════════════════════════

    private void RenderTick(object? sender, EventArgs e)
    {
        UpdateSphere();
        UpdateAudioPlots();
    }

    // ── Sphere + trail ─────────────────────────────────────────────────────

    private void UpdateSphere()
    {
        if (!_hasNewStokes) return;
        _hasNewStokes = false;

        // Drain all pending stokes into the trail queue
        StokeSample latest = _lastStokes;
        while (_pendingStokes.TryDequeue(out var item))
        {
            if (_showTrail)
            {
                _trailQueue.Enqueue(item.pos);
                while (_trailQueue.Count > MAX_TRAIL_POINTS)
                    _trailQueue.Dequeue();
            }
            latest = item.sample;
        }
        _lastStokes = latest;

        if (!_showTrail)
            _trailQueue.Clear();

        // Rebuild trail visual (shared frozen mesh + transform per dot)
        var group = new Model3DGroup();
        int idx = 0;
        int total = _trailQueue.Count;
        foreach (var pos in _trailQueue)
        {
            double t = 1.0 - (double)idx / Math.Max(total, 1); // 1 = oldest, 0 = newest
            byte r = (byte)(255 * (1 - t));
            byte gb = (byte)(100 * t);
            double size = 0.10 * (1 - t * 0.6);

            var model = new GeometryModel3D(s_trailMesh,
                new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(r, gb, gb))));
            var xform = new Transform3DGroup();
            xform.Children.Add(new ScaleTransform3D(size, size, size));
            xform.Children.Add(new TranslateTransform3D(pos.X, pos.Y, pos.Z));
            model.Transform = xform;
            group.Children.Add(model);
            idx++;
        }
        _trailVisual!.Content = group;

        // Update readout labels
        var s = _lastStokes;
        PowerText.Text = $"{s.S0:F2} µW";
        S1Text.Text = $"{s.S1:F4}";
        S2Text.Text = $"{s.S2:F4}";
        S3Text.Text = $"{s.S3:F4}";
        DopText.Text = $"{s.Dop * 100:F1}%";
        PolarizationText.Text = ComputePolarizationLabel(s);
    }

    // ── Audio waveforms ────────────────────────────────────────────────────

    private void UpdateAudioPlots()
    {
        // Processed audio
        var (procData, procTotal) = _processedAudioRing.Snapshot(AUDIO_DISPLAY_SAMPLES);
        if (procTotal != _lastProcTotal && procData.Length > 0)
        {
            _lastProcTotal = procTotal;
            long xStart = procTotal - procData.Length;

            var plt = ProcessedAudioPlot.Plot;
            plt.Clear();
            var sig = plt.Add.Signal(procData);
            sig.Color = ScottPlot.Color.FromHex("#4A90D9");
            sig.Data.XOffset = xStart;

            // Overlay raw audio on processed plot when enabled
            if (_showOverlay)
            {
                var (rawSnap, _) = _rawAudioRing.Snapshot(AUDIO_DISPLAY_SAMPLES);
                if (rawSnap.Length > 0)
                {
                    var overlay = plt.Add.Signal(rawSnap);
                    overlay.Color = ScottPlot.Color.FromHex("#E74C3C");
                    overlay.LineWidth = 1;
                    overlay.Data.XOffset = xStart;
                }
            }

            plt.Title($"Processed  |  {procTotal:N0} samples");
            plt.Axes.SetLimitsX(xStart, procTotal);
            plt.Axes.AutoScaleY();
            ProcessedAudioPlot.Refresh();
        }

        // Raw audio
        var (rawData, rawTotal) = _rawAudioRing.Snapshot(AUDIO_DISPLAY_SAMPLES);
        if (rawTotal != _lastRawTotal && rawData.Length > 0)
        {
            _lastRawTotal = rawTotal;
            long xStart = rawTotal - rawData.Length;

            var plt = RawAudioPlot.Plot;
            plt.Clear();
            var sig = plt.Add.Signal(rawData);
            sig.Data.XOffset = xStart;

            plt.Title($"Raw  |  {rawTotal:N0} samples");
            plt.Axes.SetLimitsX(xStart, rawTotal);
            plt.Axes.AutoScaleY();
            RawAudioPlot.Refresh();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3D scene helpers (called once)
    // ═══════════════════════════════════════════════════════════════════════

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
    // ScottPlot dark theme
    // ═══════════════════════════════════════════════════════════════════════

    private static void ConfigurePlot(ScottPlot.WPF.WpfPlot wpfPlot)
    {
        var plt = wpfPlot.Plot;
        plt.FigureBackground.Color = ScottPlot.Color.FromHex("#28292B");
        plt.DataBackground.Color = ScottPlot.Color.FromHex("#1E1E1E");
        plt.Axes.Bottom.Label.ForeColor = ScottPlot.Color.FromHex("#AAAAAA");
        plt.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#AAAAAA");
        plt.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#888888");
        plt.Axes.Left.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#888888");
        plt.Grid.MajorLineColor = ScottPlot.Color.FromHex("#3A3A3A");
        plt.Axes.Bottom.MajorTickStyle.Color = ScottPlot.Color.FromHex("#555555");
        plt.Axes.Left.MajorTickStyle.Color = ScottPlot.Color.FromHex("#555555");
        plt.XLabel("Sample");
        plt.YLabel("Amplitude");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Polarization label
    // ═══════════════════════════════════════════════════════════════════════

    private static string ComputePolarizationLabel(StokeSample s)
    {
        double dop = Math.Sqrt(s.S1 * s.S1 + s.S2 * s.S2 + s.S3 * s.S3);
        if (dop < 0.01) return "Unpolarized";

        double chi = 0.5 * Math.Asin(Math.Clamp(s.S3 / dop, -1, 1)) * 180.0 / Math.PI;
        double psi = 0.5 * Math.Atan2(s.S2, s.S1) * 180.0 / Math.PI;
        if (psi < 0) psi += 180;

        if (Math.Abs(chi) > 40)
            return s.S3 > 0 ? "Circular (Right)" : "Circular (Left)";
        if (Math.Abs(chi) < 5)
            return $"Linear {psi:F0}°";
        return $"Elliptical {psi:F0}°";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Button handlers
    // ═══════════════════════════════════════════════════════════════════════

    private void StopButton_Click(object sender, RoutedEventArgs e) => DoStop();

    private void NormalizedRaw_Click(object sender, RoutedEventArgs e)
    {
        NormalizedRawButton.Content =
            NormalizedRawButton.Content.ToString() == "Normalized" ? "Raw" : "Normalized";
    }

    private void ResetView_Click(object sender, RoutedEventArgs e) =>
        PoincareView.ZoomExtents();

    private void OverlayReference_Click(object sender, RoutedEventArgs e)
    {
        _showOverlay = !_showOverlay;
        OverlayButton.Content = _showOverlay ? "Hide Reference" : "Overlay Reference";
    }

    private void TrailCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _showTrail = TrailCheckBox.IsChecked == true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Stop & cleanup
    // ═══════════════════════════════════════════════════════════════════════

    private void DoStop()
    {
        _recorder?.Stop();
        Cleanup();
        StopRequested?.Invoke();
    }

    private void Cleanup()
    {
        _renderTimer?.Stop();
        _elapsedTimer?.Stop();
        _durationTimer?.Stop();
        _testGen?.Stop();
        _testGen?.Dispose();
        _stressGen?.Stop();
        _stressGen?.Dispose();
        _listener?.Stop();
        _listener?.Dispose();
    }
}
