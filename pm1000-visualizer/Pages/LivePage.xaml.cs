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

public partial class LivePage : UserControl
{
    private readonly ConnectionSettings _settings;
    private readonly MeasurementSession _session;
    private UdpListener? _listener;
    private DataRecorder? _recorder;
    private TestDataGenerator? _testGenerator;
    private StressTestGenerator? _stressGenerator;
    private DispatcherTimer? _durationTimer;
    private DispatcherTimer? _elapsedTimer;

    // Poincaré sphere
    private List<(Point3D position, int age)> _trail = new();
    private const int TRAIL_LENGTH = 10;
    private const double SPHERE_RADIUS = 5.0;
    private ModelVisual3D? _lightsVisual;
    private Model3D? _sphereModel;
    private ModelVisual3D? _trailVisual;

    // Thread-safe buffer for incoming Stokes positions (written by UDP thread, drained by UI)
    private readonly ConcurrentQueue<Point3D> _pendingPoints = new();

    // Audio display history (rolling window)
    private readonly Queue<float> _rawAudioHistory = new();
    private readonly Queue<float> _processedAudioHistory = new();
    private const int AUDIO_HISTORY_SAMPLES = 32000; // full recording buffer
    private const int AUDIO_DISPLAY_SAMPLES = 2000;  // visible window (~125 ms at 16 kHz)
    private long _rawAudioTotalSamples;
    private long _processedAudioTotalSamples;
    private readonly object _rawAudioLock = new();
    private readonly object _processedAudioLock = new();

    // Overlay state
    private bool _showOverlay;
    private volatile bool _showTrail = true;

    // Last Stokes values for display
    private StokeSample _lastStokes;

    // Prevents BeginInvoke queue buildup — only one UI update in flight at a time
    private int _uiUpdatePending;
    private int _rawAudioUpdatePending;
    private int _processedAudioUpdatePending;

    // Shared frozen mesh for trail points — avoids rebuilding geometry every frame
    private static readonly MeshGeometry3D _sharedTrailMesh;

    static LivePage()
    {
        var mb = new MeshBuilder();
        mb.AddSphere(new Point3D(0, 0, 0), 1.0, 4, 4);
        _sharedTrailMesh = mb.ToMesh();
        _sharedTrailMesh.Freeze();  // immutable → GPU-cacheable
    }

    /// <summary>Raised when measurement should end (user pressed Stop or duration expired).</summary>
    public event Action? StopRequested;

    // ── Construction ───────────────────────────────────────────────────────────

    public LivePage(ConnectionSettings settings, MeasurementSession session)
    {
        _settings = settings;
        _session = session;
        InitializeComponent();

        Loaded += LivePage_Loaded;
        Unloaded += LivePage_Unloaded;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void LivePage_Loaded(object sender, RoutedEventArgs e)
    {
        // UI labels
        StreamerIpText.Text = _settings.StreamerIp;
        ProcessedPortText.Text = _settings.ProcessedAudioPort.ToString();
        RawPortText.Text = _settings.RawAudioPort.ToString();

        // Build 3D scene (once)
        _sphereModel = BuildSphere();
        _lightsVisual = new ModelVisual3D { Content = BuildLights() };

        // Configure ScottPlot dark theme
        ConfigurePlot(ProcessedAudioPlot);
        ConfigurePlot(RawAudioPlot);

        // Start UDP listener
        _listener = new UdpListener(_settings.StokesPort, _settings.RawAudioPort, _settings.ProcessedAudioPort);
        _listener.StokesReceived += OnStokesReceived;
        _listener.RawAudioReceived += OnRawAudioReceived;
        _listener.ProcessedAudioReceived += OnProcessedAudioReceived;
        _listener.PacketDropped += (exp, got) =>
            Dispatcher.BeginInvoke(() => Logger.LogWarning($"Packet loss: expected {exp}, got {got}"));
        _listener.Start();

        // Start recorder
        _recorder = new DataRecorder(_session);
        _recorder.Start();

        // Test data generator
        if (_settings.IsTestMode)
        {
            _testGenerator = new TestDataGenerator(
                _settings.StokesPort, _settings.RawAudioPort, _settings.ProcessedAudioPort);
            _testGenerator.Start();
        }
        else if (_settings.IsStressTest)
        {
            _stressGenerator = new StressTestGenerator(
                _settings.StokesPort, _settings.RawAudioPort,
                _settings.StressStokesPerSecond, _settings.StressAudioPerSecond);
            _stressGenerator.Start();
        }

        // Duration timer (fixed-length measurement)
        if (!_settings.IsIndefinite)
        {
            _durationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.DurationSeconds) };
            _durationTimer.Tick += (_, _) => { _durationTimer.Stop(); DoStop(); };
            _durationTimer.Start();
        }

        // Elapsed time display
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _elapsedTimer.Tick += (_, _) =>
        {
            if (_recorder != null)
                ElapsedText.Text = TimeSpan.FromMilliseconds(_recorder.ElapsedMs).ToString(@"m\:ss");
        };
        _elapsedTimer.Start();

        // Initialize 3D scene — sphere and lights are added once and never removed.
        _trailVisual = new ModelVisual3D();
        PoincareView.Children.Clear();
        PoincareView.Children.Add(_lightsVisual!);
        PoincareView.Children.Add(new ModelVisual3D { Content = _sphereModel });
        PoincareView.Children.Add(_trailVisual);
        PoincareView.ZoomExtents(0);  // 0 ms animation = instant
    }

    private void LivePage_Unloaded(object sender, RoutedEventArgs e)
    {
        Cleanup();
    }

    // ── Stokes handling ────────────────────────────────────────────────────────

    private void OnStokesReceived(StokesPacket packet)
    {
        _recorder?.RecordStokes(packet);

        // Buffer incoming points — trail management is deferred to the UI thread
        StokeSample latest = default;
        bool hasData = false;

        foreach (var s in packet.Samples)
        {
            double len = Math.Sqrt(s.S1 * s.S1 + s.S2 * s.S2 + s.S3 * s.S3);
            if (len < 0.001) continue;
            latest = s;
            hasData = true;

            if (_showTrail)
            {
                _pendingPoints.Enqueue(new Point3D(
                    s.S1 / len * SPHERE_RADIUS,
                    s.S2 / len * SPHERE_RADIUS,
                    s.S3 / len * SPHERE_RADIUS));
            }
        }

        if (hasData)
            _lastStokes = latest;

        // Skip if a UI update is already queued — prevents backlog buildup
        if (System.Threading.Interlocked.CompareExchange(ref _uiUpdatePending, 1, 0) == 0)
        {
            Dispatcher.BeginInvoke(() =>
            {
                // Drain pending points into trail on UI thread (thread-safe)
                bool showTrail = _showTrail;
                int newCount = 0;

                if (showTrail)
                {
                    while (_pendingPoints.TryDequeue(out var pt))
                    {
                        _trail.Add((pt, 0));
                        newCount++;
                    }
                    // Age only the pre-existing points
                    int existingCount = _trail.Count - newCount;
                    for (int i = 0; i < existingCount; i++)
                        _trail[i] = (_trail[i].position, _trail[i].age + newCount);
                    _trail.RemoveAll(p => p.age >= TRAIL_LENGTH);
                }
                else
                {
                    _trail.Clear();
                    while (_pendingPoints.TryDequeue(out _)) { }
                }

                // Rebuild trail visual using shared frozen mesh
                var trailGroup = new Model3DGroup();
                foreach (var (pos, age) in _trail)
                    trailGroup.Children.Add(BuildTrailPoint(pos, age));
                _trailVisual!.Content = trailGroup;

                // Update readout
                UpdateStokesDisplay(_lastStokes);

                System.Threading.Interlocked.Exchange(ref _uiUpdatePending, 0);
            });
        }
    }

    private void UpdateStokesDisplay(StokeSample s)
    {
        PowerText.Text = $"{s.S0:F2} µW";
        S1Text.Text = $"{s.S1:F4}";
        S2Text.Text = $"{s.S2:F4}";
        S3Text.Text = $"{s.S3:F4}";
        DopText.Text = $"{s.Dop * 100:F1}%";
        PolarizationText.Text = ComputePolarizationLabel(s);
    }

    /// <summary>
    /// Determines a human-readable polarization label from the Stokes parameters.
    /// </summary>
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

    // ── Audio handling ─────────────────────────────────────────────────────────

    private void OnRawAudioReceived(AudioPacket packet)
    {
        _recorder?.RecordRawAudio(packet);

        lock (_rawAudioLock)
        {
            foreach (var s in packet.Samples)
                _rawAudioHistory.Enqueue(s);
            _rawAudioTotalSamples += packet.Samples.Length;
            while (_rawAudioHistory.Count > AUDIO_HISTORY_SAMPLES)
                _rawAudioHistory.Dequeue();
        }

        // Skip if a UI update is already queued
        if (System.Threading.Interlocked.CompareExchange(ref _rawAudioUpdatePending, 1, 0) == 0)
        {
            Dispatcher.BeginInvoke(() =>
            {
                double[] snapshot;
                long xEnd, xStart;
                lock (_rawAudioLock)
                {
                    snapshot = _rawAudioHistory
                        .Skip(Math.Max(0, _rawAudioHistory.Count - AUDIO_DISPLAY_SAMPLES))
                        .Select(s => (double)s).ToArray();
                    xEnd = _rawAudioTotalSamples;
                    xStart = xEnd - snapshot.Length;
                }

                var plt = RawAudioPlot.Plot;
                plt.Clear();
                var sig = plt.Add.Signal(snapshot);
                sig.Data.XOffset = xStart;
                plt.Title($"Raw  |  {xEnd:N0} samples received");
                plt.Axes.SetLimitsX(xStart, xEnd);
                plt.Axes.AutoScaleY();
                RawAudioPlot.Refresh();

                System.Threading.Interlocked.Exchange(ref _rawAudioUpdatePending, 0);
            });
        }
    }

    private void OnProcessedAudioReceived(AudioPacket packet)
    {
        _recorder?.RecordProcessedAudio(packet);

        lock (_processedAudioLock)
        {
            foreach (var s in packet.Samples)
                _processedAudioHistory.Enqueue(s);
            _processedAudioTotalSamples += packet.Samples.Length;
            while (_processedAudioHistory.Count > AUDIO_HISTORY_SAMPLES)
                _processedAudioHistory.Dequeue();
        }

        // Skip if a UI update is already queued
        if (System.Threading.Interlocked.CompareExchange(ref _processedAudioUpdatePending, 1, 0) == 0)
        {
            Dispatcher.BeginInvoke(() =>
            {
                double[] snapshot;
                long xEnd, xStart;
                lock (_processedAudioLock)
                {
                    snapshot = _processedAudioHistory
                        .Skip(Math.Max(0, _processedAudioHistory.Count - AUDIO_DISPLAY_SAMPLES))
                        .Select(s => (double)s).ToArray();
                    xEnd = _processedAudioTotalSamples;
                    xStart = xEnd - snapshot.Length;
                }

                var plt = ProcessedAudioPlot.Plot;
                plt.Clear();
                var sig = plt.Add.Signal(snapshot);
                sig.Color = ScottPlot.Color.FromHex("#4A90D9");
                sig.Data.XOffset = xStart;

                // Overlay raw audio on processed plot if enabled
                if (_showOverlay)
                {
                    double[] rawSnap;
                    lock (_rawAudioLock)
                    {
                        if (_rawAudioHistory.Count > 0)
                        {
                            rawSnap = _rawAudioHistory
                                .Skip(Math.Max(0, _rawAudioHistory.Count - AUDIO_DISPLAY_SAMPLES))
                                .Select(s => (double)s).ToArray();
                        }
                        else rawSnap = Array.Empty<double>();
                    }
                    if (rawSnap.Length > 0)
                    {
                        var overlay = plt.Add.Signal(rawSnap);
                        overlay.Color = ScottPlot.Color.FromHex("#E74C3C");
                        overlay.LineWidth = 1;
                        overlay.Data.XOffset = xStart;
                    }
                }

                plt.Title($"Processed  |  {xEnd:N0} samples received");
                plt.Axes.SetLimitsX(xStart, xEnd);
                plt.Axes.AutoScaleY();
                ProcessedAudioPlot.Refresh();

                System.Threading.Interlocked.Exchange(ref _processedAudioUpdatePending, 0);
            });
        }
    }

    // ── 3D scene helpers ───────────────────────────────────────────────────────

    private static Model3D BuildSphere()
    {
        var mesh = new MeshBuilder();
        mesh.AddSphere(new Point3D(0, 0, 0), SPHERE_RADIUS, 32, 32);
        var material = new DiffuseMaterial(new SolidColorBrush(
            Color.FromArgb(90, 160, 160, 160)));   // semi-transparent
        var model = new GeometryModel3D(mesh.ToMesh(), material);
        model.BackMaterial = material;
        return model;
    }

    private static Model3D BuildTrailPoint(Point3D pos, int age)
    {
        double t = (double)age / TRAIL_LENGTH;
        var color = Color.FromRgb(
            (byte)(255 * (1 - t)),
            (byte)(100 * t),
            (byte)(100 * t));
        double size = 0.08 * (1 - t * 0.5);

        // Reuse shared frozen mesh + transform instead of rebuilding geometry every frame
        var model = new GeometryModel3D(_sharedTrailMesh,
            new DiffuseMaterial(new SolidColorBrush(color)));
        var transforms = new Transform3DGroup();
        transforms.Children.Add(new ScaleTransform3D(size, size, size));
        transforms.Children.Add(new TranslateTransform3D(pos.X, pos.Y, pos.Z));
        model.Transform = transforms;
        return model;
    }

    private static Model3DGroup BuildLights()
    {
        var g = new Model3DGroup();
        g.Children.Add(new AmbientLight(Colors.White));
        g.Children.Add(new DirectionalLight(Colors.White, new Vector3D(1, 1, 1)));
        return g;
    }

    // ── ScottPlot dark theme ───────────────────────────────────────────────────

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

    // ── UI event handlers ──────────────────────────────────────────────────────

    private void StopButton_Click(object sender, RoutedEventArgs e) => DoStop();

    private void NormalizedRaw_Click(object sender, RoutedEventArgs e)
    {
        // Toggle label — raw data is a stretch goal
        if (NormalizedRawButton.Content.ToString() == "Normalized")
            NormalizedRawButton.Content = "Raw";
        else
            NormalizedRawButton.Content = "Normalized";
    }

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        PoincareView.ZoomExtents();
    }

    private void OverlayReference_Click(object sender, RoutedEventArgs e)
    {
        _showOverlay = !_showOverlay;
        OverlayButton.Content = _showOverlay ? "Hide Reference" : "Overlay Reference";
    }

    private void TrailCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _showTrail = TrailCheckBox.IsChecked == true;
    }

    private void ProcSpec_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Spectrogram view is a stretch goal — coming soon.",
            "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RawSpec_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Spectrogram view is a stretch goal — coming soon.",
            "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Stop & cleanup ─────────────────────────────────────────────────────────

    private void DoStop()
    {
        _recorder?.Stop();
        Cleanup();
        StopRequested?.Invoke();
    }

    private void Cleanup()
    {
        _elapsedTimer?.Stop();
        _durationTimer?.Stop();
        _testGenerator?.Stop();
        _testGenerator?.Dispose();
        _stressGenerator?.Stop();
        _stressGenerator?.Dispose();
        _listener?.Stop();
        _listener?.Dispose();
    }
}
