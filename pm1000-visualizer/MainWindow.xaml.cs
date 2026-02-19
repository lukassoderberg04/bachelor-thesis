using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using pm1000_visualizer.Communication;

namespace pm1000_visualizer;

public partial class MainWindow : Window
{
    private UdpListener _udpListener = null!;
    private StreamerApiClient _apiClient = null!;
    private DispatcherTimer? _testTimer;
    private uint _testSequence = 0;
    private double _testAngle = 0;
    private double _testAudioPhase = 0;

    // Poincaré sphere trajectory trail
    private List<(Point3D position, int age)> _trail = new();
    private const int TRAIL_LENGTH = 5;      // How many updates to keep (5 × 100ms = 0.5 seconds)
    private const double SPHERE_RADIUS = 5.0;

    // Cached 3D scene objects (built once, reused every frame)
    private ModelVisual3D? _lightsVisual;
    private Model3D? _sphereModel;

    // Audio waveform history
    private readonly Queue<float> _audioHistory = new();
    private const int AUDIO_HISTORY_SAMPLES = 32000;  // ~2 seconds at 16 kHz

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _apiClient = new StreamerApiClient("127.0.0.1");
        _udpListener = new UdpListener();
        _udpListener.StokesReceived += OnStokesReceived;
        _udpListener.AudioReceived += OnAudioReceived;
        _udpListener.PacketDropped += (expected, got) =>
            Dispatcher.BeginInvoke(() => Logger.LogWarning($"Packet loss: expected {expected}, got {got}"));
        _udpListener.Start();

        // Build sphere and lights once — they never change
        _sphereModel = BuildSphere();
        _lightsVisual = new ModelVisual3D { Content = BuildLights() };

        // TODO: Remove when Lukas streamer is ready
        StartTestDataGenerator();
    }

    // ── Test data generator ────────────────────────────────────────────────────

    private void StartTestDataGenerator()
    {
        _testTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _testTimer.Tick += (_, _) => SendTestPacket();
        _testTimer.Start();
        Logger.LogInfo("Test mode active.");
    }

    private void SendTestPacket()
    {
        // Advance the angle to create smooth movement around the sphere each tick
        _testAngle += 0.05;

        var samples = new StokeSample[64];
        for (int i = 0; i < 64; i++)
        {
            // Spread 64 samples evenly around the current angle (one full orbit per packet)
            double angle = _testAngle + (i / 64.0) * Math.PI * 2;

            // Two slow secondary rotations to create an interesting 3D path
            double s1 = Math.Sin(angle) * Math.Cos(_testAngle * 0.3);
            double s2 = Math.Cos(angle) * Math.Sin(_testAngle * 0.3);
            double s3 = Math.Sin(angle * 0.5) * Math.Cos(_testAngle * 0.7);

            // Normalize so the point lands on the unit sphere
            double len = Math.Sqrt(s1 * s1 + s2 * s2 + s3 * s3);
            samples[i] = new StokeSample(1.0f, (float)(s1 / len), (float)(s2 / len), (float)(s3 / len), 0.95f);
        }

        OnStokesReceived(new StokesPacket(_testSequence++, 16000, samples));

        // Generate a sine wave audio packet at 440 Hz (A4)
        const int audioBlockSize = 256;
        const double sampleRate = 16000;
        const double frequency = 440;
        var audio = new float[audioBlockSize];
        for (int i = 0; i < audioBlockSize; i++)
        {
            audio[i] = (float)Math.Sin(_testAudioPhase);
            _testAudioPhase += 2 * Math.PI * frequency / sampleRate;
            if (_testAudioPhase > 2 * Math.PI) _testAudioPhase -= 2 * Math.PI;  // Prevent unbounded growth
        }
        OnAudioReceived(new AudioPacket(_testSequence, 16000, audio));
    }

    // ── Stokes visualization ───────────────────────────────────────────────────

    private void OnStokesReceived(StokesPacket packet)
    {
        // Prepare trail data on calling thread before touching UI
        for (int i = 0; i < _trail.Count; i++)
            _trail[i] = (position: _trail[i].position, age: _trail[i].age + 1);
        _trail.RemoveAll(p => p.age >= TRAIL_LENGTH);

        foreach (var s in packet.Samples)
        {
            double len = Math.Sqrt(s.S1 * s.S1 + s.S2 * s.S2 + s.S3 * s.S3);
            if (len < 0.01) continue;
            _trail.Add((new Point3D(s.S1 / len * SPHERE_RADIUS, s.S2 / len * SPHERE_RADIUS, s.S3 / len * SPHERE_RADIUS), 0));
        }

        var trailSnapshot = _trail.ToList();
        uint seq = packet.SequenceNr;

        Dispatcher.BeginInvoke(() =>
        {
            // Rebuild only the trail points; sphere and lights are reused from cache
            var scene = new Model3DGroup();
            scene.Children.Add(_sphereModel!);
            foreach (var (pos, age) in trailSnapshot)
                scene.Children.Add(BuildTrailPoint(pos, age));

            StokesPlot3D.Children.Clear();
            StokesPlot3D.Children.Add(_lightsVisual!);
            StokesPlot3D.Children.Add(new ModelVisual3D { Content = scene });

            StatusText.Text = $"Stokes 3D  |  seq={seq}  |  trail={trailSnapshot.Count} pts";
        });
    }

    private Model3D BuildSphere()
    {
        var mesh = new MeshBuilder();
        mesh.AddSphere(new Point3D(0, 0, 0), SPHERE_RADIUS, 32, 32);
        var model = new GeometryModel3D(mesh.ToMesh(), new DiffuseMaterial(System.Windows.Media.Brushes.Gray));
        model.BackMaterial = model.Material;
        return model;
    }

    private static Model3D BuildTrailPoint(Point3D position, int age)
    {
        double t = (double)age / TRAIL_LENGTH;  // 0 = newest, 1 = oldest

        // Colour: bright red → dark gray as point ages
        var color = System.Windows.Media.Color.FromRgb(
            (byte)(255 * (1 - t)),
            (byte)(100 * t),
            (byte)(100 * t));

        double size = 0.08 * (1 - t * 0.5);  // Shrink slightly as point ages

        var mesh = new MeshBuilder();
        mesh.AddSphere(position, size, 12, 12);
        return new GeometryModel3D(mesh.ToMesh(),
            new DiffuseMaterial(new System.Windows.Media.SolidColorBrush(color)));
    }

    private static Model3DGroup BuildLights()
    {
        var lights = new Model3DGroup();
        lights.Children.Add(new AmbientLight(System.Windows.Media.Colors.White));
        lights.Children.Add(new DirectionalLight(System.Windows.Media.Colors.White, new Vector3D(1, 1, 1)));
        return lights;
    }

    // ── Audio (placeholder until Ludwig is ready) ──────────────────────────────

    private void OnAudioReceived(AudioPacket packet)
    {
        // Append new samples, drop oldest to stay within history limit
        foreach (var s in packet.Samples)
            _audioHistory.Enqueue(s);
        while (_audioHistory.Count > AUDIO_HISTORY_SAMPLES)
            _audioHistory.Dequeue();

        var snapshot = _audioHistory.Select(s => (double)s).ToArray();

        Dispatcher.BeginInvoke(() =>
        {
            var plot = AudioPlot.Plot;
            plot.Clear();
            plot.Add.Signal(snapshot);
            plot.Title($"Audio  |  seq={packet.SequenceNr}  |  {packet.SampleRateHz} Hz");
            plot.XLabel("Sample");
            plot.YLabel("Amplitude");
            plot.Axes.SetLimitsY(-1.2, 1.2);
            plot.Axes.AutoScaleX();
            AudioPlot.Refresh();
        });
    }

    // ── Cleanup ────────────────────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _testTimer?.Stop();
        _udpListener?.Stop();
        _udpListener?.Dispose();
    }
}
