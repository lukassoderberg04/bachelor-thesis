# PM1000 Visualizer

A real-time WPF application that receives, displays, and records polarimetric Stokes data and audio from the PM1000 optical polarimeter system. It is one of three components in the measurement pipeline:

```
PM1000 hardware
      | USB/FTDI
      v
pm1000-streamer-service          (C#)
      |
      +-- UDP :5000  Stokes snapshot  (one sample per datagram, raw 24-byte format)
      +-- UDP :5001  Raw audio        (one amplitude per datagram, raw 8-byte format)

signal-processing                (Rust)
      +-- UDP :5002  Processed audio  (block packets with 10-byte header)

                         v
              pm1000-visualizer   (this project)
```

The visualizer is a standalone receiver. It opens UDP sockets, waits for packets, displays them in real time, and records everything for post-measurement review and export. There is no REST API or any other back-channel between the components.

---

## Requirements

- Windows 10 or later
- **.NET 8.0 SDK** -- note this is the **SDK**, not just the runtime. The runtime alone (which Windows may install separately) is not sufficient for building. Install from https://aka.ms/dotnet/8.0/dotnet-sdk-win-x64.exe or via `winget install Microsoft.DotNet.SDK.8`.
- Visual Studio 2022 (optional -- CLI works equally well once the SDK is installed)

### NuGet dependencies (auto-restored on first build)

| Package | Version | Purpose |
|---|---|---|
| `ScottPlot.WPF` | 5.1.57 | 2D audio waveform charts |
| `HelixToolkit.Wpf` | 2.23.0 | 3D Poincare sphere viewport |

The NuGet restore step warns that `HelixToolkit.Wpf` and `SkiaSharp.Views.WPF` target .NET Framework rather than `net8.0-windows`. These are expected warnings -- both packages work correctly at runtime.

---

## Building and running

```bash
cd pm1000-visualizer
dotnet build pm1000-visualizer.csproj
```

To run directly from the output:

```bash
.\bin\Debug\net8.0-windows\pm1000-visualizer.exe
```

Or open `pm1000-visualizer.sln` in Visual Studio 2022 and press **F5**.

Output binary: `bin/Debug/net8.0-windows/pm1000-visualizer.exe`

---

## Application flow

The application drives the user through a fixed three-page lifecycle.

### 1. Pre-Connection page

The first thing shown on launch. The user fills in connection parameters before any networking starts.

**Fields:**

| Field | Default | Description |
|---|---|---|
| Streamer IP | `127.0.0.1` | IP address of the machine running `pm1000-streamer-service` |
| Stokes Port | `5000` | UDP port for incoming Stokes snapshots |
| Raw Audio Port | `5001` | UDP port for raw microphone audio from the streamer |
| Processed Audio Port | `5002` | UDP port for processed audio from the Rust signal-processing pipeline |
| Duration | Indefinite / fixed seconds | If fixed (e.g. 30 s), packets stop being accepted after the time expires and the app transitions to the Post page |
| Test Mode | off | Use synthetic data instead of real hardware |

Pressing **Connect** validates all inputs and navigates to the Live page. The settings are captured into a `ConnectionSettings` object that is passed to every subsequent component.

---

### 2. Live page

This is the main real-time monitoring screen. The following happens in order when the page loads:

#### 2a. UDP listener starts

`UdpListener` opens three separate UDP sockets and starts a background `Task` receive loop on each:

| Socket | Port (configurable) | Source |
|---|---|---|
| Stokes | 5000 | `pm1000-streamer-service` |
| Raw audio | 5001 | `pm1000-streamer-service` or `signal-processing` |
| Processed audio | 5002 | `signal-processing` |

Each loop calls `client.ReceiveAsync()` in a tight `while (_running)` loop. Received bytes are dispatched to the appropriate handler described below. Exceptions that are not `ObjectDisposedException` are logged as errors; `ObjectDisposedException` triggers a clean exit from the loop when the listener is stopped.

#### 2b. DataRecorder starts

`DataRecorder` starts a `Stopwatch` and records `session.StartTime = DateTime.Now`. Every packet received from this point is timestamped relative to this stopwatch. The recorder is thread-safe (uses `lock`) because all three UDP loops run on background threads.

#### 2c. Stokes data path

Each datagram received on the Stokes port goes through the following chain:

```
UDP datagram (bytes)
      |
      v
UdpListener -- detect wire format by packet size
      |
      +- 24 bytes  ->  PacketDeserializer.TryDeserializeStreamerStokes()
      |                 Reads: float32 S0, S1, S2, S3, DOP + uint32 Time (discarded)
      |                 Returns a StokeSample.
      |                 Wrapped into StokesPacket(synthetic_seq++, sampleRate=0, samples=[1])
      |
      +- other size  ->  PacketDeserializer.TryDeserializeStokes()
                         Reads: 10-byte header (uint32 seq, uint32 rate, uint16 blockSize)
                         Then blockSize x 20 bytes (5 x float32 per sample: S0, S1, S2, S3, DOP)
                         Returns a StokesPacket.
                         Sequence gaps are detected and logged as dropped packets.
      |
      v
StokesReceived event fires on background thread
      |
      +---> DataRecorder.RecordStokes(packet)
      |        - records session.SampleRateHz = packet.SampleRateHz
      |        - foreach sample: appends TimestampedStokes(stopwatch_ms, sample) to session
      |
      +---> LivePage.OnStokesReceived(packet)
               - ages all existing trail points by +1
               - removes trail points older than TRAIL_LENGTH (10)
               - iterates packet.Samples:
                   computes len = sqrt(S1^2 + S2^2 + S3^2)
                   if len < 0.001: skip (degenerate vector)
                   projects onto sphere surface: pos = (S1/len, S2/len, S3/len) x SPHERE_RADIUS (5.0)
                   adds (pos, age=0) to trail list (if trail checkbox is on)
                   keeps latest valid sample as _lastStokes
               - dispatches to UI thread via Dispatcher.BeginInvoke():
                   rebuilds Model3DGroup: sphere + one small sphere per trail point
                   trail points coloured red to dark-grey by age
                   updates readout labels: Power, S1, S2, S3, DOP%, Polarization
```

**Polarization classification** (`ComputePolarizationLabel`):

- DOP < 0.01: "Unpolarized"
- |chi| > 40 degrees: "Circular (Right)" or "Circular (Left)" where chi = 0.5 * arcsin(S3/DOP)
- |chi| < 5 degrees: "Linear psi degrees" where psi = 0.5 * atan2(S2, S1), mapped to [0, 180]
- otherwise: "Elliptical psi degrees"

#### 2d. Raw audio data path

Each datagram on the raw audio port:

```
UDP datagram (bytes)
      |
      v
UdpListener -- detect wire format by packet size
      |
      +- 8 bytes  ->  PacketDeserializer.TryDeserializeStreamerAudio()
      |                 Reads: float32 Amplitude + uint32 Time (discarded)
      |                 Returns float? amplitude.
      |                 Wrapped into AudioPacket(synthetic_seq++, sampleRate=0, samples=[1])
      |
      +- other size  ->  PacketDeserializer.TryDeserializeAudio()
                         Reads: 10-byte header (uint32 seq, uint32 rate, uint16 blockSize)
                         Then blockSize x 4 bytes (one float32 per sample)
                         Returns an AudioPacket.
      |
      v
RawAudioReceived event fires on background thread
      |
      +---> DataRecorder.RecordRawAudio(packet)
      |        - records session.RawAudioSampleRate = packet.SampleRateHz
      |        - appends all samples to session.RawAudioSamples
      |
      +---> LivePage.OnRawAudioReceived(packet)
               - enqueues all samples into _rawAudioHistory (Queue<float>, max 32000)
               - takes only the most recent 2000 samples for display
               - dispatches to UI thread via Dispatcher.BeginInvoke():
                   clears RawAudioPlot, adds a Signal, sets Y-axis to [-1.2, 1.2]
                   X-axis fixed to [0, 2000] so the waveform stays readable
                   calls RawAudioPlot.Refresh()
```

**Why 2000 samples for display?** At 32000 samples the chart becomes an unreadable solid block because hundreds of samples map to a single pixel. Showing only the latest 2000 samples (~125 ms at 16 kHz) keeps individual waveform cycles clearly visible. All 32000 samples remain in the queue for recording purposes.

#### 2e. Processed audio data path

Identical pipeline to raw audio above, but using `_processedAudioHistory`, `ProcessedAudioPlot`, and `DataRecorder.RecordProcessedAudio`. Additionally, when the overlay is enabled (`_showOverlay = true`), the most recent 2000 raw audio samples are drawn as a red (`#E74C3C`) signal on top of the blue (`#4A90D9`) processed signal.

#### 2f. Duration timer (fixed-length measurement only)

If the user chose a fixed duration (e.g. 30 seconds), a `DispatcherTimer` fires once after `DurationSeconds`. When it fires, `DoStop()` is called exactly as if the user had pressed the STOP button. After this point no more packets are processed.

#### 2g. Elapsed time display

A separate `DispatcherTimer` fires every 500 ms and updates the `ElapsedText` label with `DataRecorder.ElapsedMs` formatted as `m:ss`.

#### 2h. Test mode

If Test Mode is enabled, `TestDataGenerator` is started at `Loaded` alongside the real `UdpListener`. It uses a `System.Threading.Timer` that fires every 50 ms (20 packets/sec) and sends synthetic UDP datagrams to `localhost` on all three configured ports. The `UdpListener` receives them normally -- the code path is identical to real hardware.

Synthetic data:
- **Stokes** -- 16 samples per packet, smooth orbit on Poincare sphere, S0 ~ 13-17 uW, DOP ~ 97%. Uses the block format (10-byte header + 16 x 20 bytes).
- **Raw audio** -- 800 samples per packet, 440 Hz sine wave (A4). Block format.
- **Processed audio** -- 800 samples per packet, 880 Hz sine wave (A5). Block format.

#### 2i. Stopping

Pressing **STOP** (or expiry of the duration timer) calls `DoStop()`:

1. `DataRecorder.Stop()` -- stops the stopwatch, records `session.EndTime`
2. `_elapsedTimer.Stop()`, `_durationTimer?.Stop()`
3. `TestDataGenerator?.Stop()` and `?.Dispose()`
4. `UdpListener.Stop()` -- sets `_running = false`, closes all three `UdpClient`s, which causes `ReceiveAsync` to throw `ObjectDisposedException` and exit each receive loop cleanly
5. `StopRequested` event fires -> `MainWindow` navigates to the Post page

---

### 3. Post page

Entered after measurement ends. The `MeasurementSession` is passed in with all recorded data.

#### 3a. Page load

On `Loaded`:

1. Duration, Stokes sample count, raw audio sample count, processed audio sample count are shown in a summary bar.
2. Two temporary WAV files are written to `%TEMP%\PM1000\<guid>\`: `raw.wav` and `processed.wav` (via `FileSaver`).
3. The two `MediaElement` controls are pointed at those temp files so playback is immediately available.
4. The Poincare playback is initialized: all `session.StokesData` is loaded; the slider max is set to `StokesData.Count - 1`.

#### 3b. Poincare playback

A `DispatcherTimer` steps through `session.StokesData` one sample at a time:

- Maintains a 40-point trail (same sphere rendering logic as the Live page)
- Play/Pause -- start/stop the timer
- Stop -- reset index to 0, redraw at first sample
- Slider -- scrubbing moves the index directly; the sphere redraws at the new position

#### 3c. Saving data

`FileSaver` creates a timestamped folder and writes three files:

**`stokes.csv`** -- downsampled to one row per 100 ms:

```
timestamp_ms,S0_uW,S1,S2,S3,DOP
0,15.20,0.0450,-0.1230,0.0670,0.972
100,15.31,0.0410,-0.1250,0.0700,0.970
```

**`audio_raw.wav`** and **`audio_processed.wav`** -- PCM 16-bit signed mono WAV:
- Sample rate: `session.RawAudioSampleRate` / `session.ProcessedAudioSampleRate`
- Amplitude `float` samples are multiplied by 32767 and clamped to [-32768, 32767] before casting to `short`

Save buttons:
- **Save All to Folder...** -- folder picker, then writes all three files into a timestamped subfolder `YYYY-MM-DD_HH-mm-ss/`
- **Save Stokes CSV** / **Save Raw Audio** / **Save Processed Audio** -- individual file-save dialogs

#### 3d. New Measurement

Pressing **New Measurement** fires `NewMeasurementRequested` -> `MainWindow` creates a fresh `MeasurementSession` and navigates back to the Pre-Connection page.

---

## UDP wire formats

### Streamer-service raw format (no header)

The `pm1000-streamer-service` fires one UDP datagram per measurement cycle, with no framing header.

**Stokes -- port 5000 -- 24 bytes, all little-endian:**

| Offset | Size | Type | Field |
|---|---|---|---|
| 0 | 4 | `float32` | S0 (optical power, uW) |
| 4 | 4 | `float32` | S1 (normalized Stokes parameter) |
| 8 | 4 | `float32` | S2 |
| 12 | 4 | `float32` | S3 |
| 16 | 4 | `float32` | DOP (degree of polarization, 0-1) |
| 20 | 4 | `uint32` | Timestamp (us since app start, wraps ~71 min) |

**Audio -- port 5001 -- 8 bytes, all little-endian:**

| Offset | Size | Type | Field |
|---|---|---|---|
| 0 | 4 | `float32` | Amplitude (normalized, 0-1) |
| 4 | 4 | `uint32` | Timestamp (us since app start) |

> **Note:** The streamer-service `AudioSnapshotPacket` constructor currently does not write data into its payload buffer -- the 8 bytes sent are always zero. This is a known stub in the streamer codebase.

### Block format with header (signal-processing, TestDataGenerator)

Used by the Rust `signal-processing` pipeline and the internal `TestDataGenerator`. A single datagram may contain multiple samples.

**Header -- 10 bytes, little-endian:**

| Offset | Size | Type | Field |
|---|---|---|---|
| 0 | 4 | `uint32` | Sequence number (wraps at 2^32) |
| 4 | 4 | `uint32` | Sample rate (Hz) |
| 8 | 2 | `uint16` | Block size (number of samples that follow) |

**Stokes samples -- immediately after header, `blockSize x 20` bytes:**

| Offset within sample | Size | Type | Field |
|---|---|---|---|
| 0 | 4 | `float32` | S0 |
| 4 | 4 | `float32` | S1 |
| 8 | 4 | `float32` | S2 |
| 12 | 4 | `float32` | S3 |
| 16 | 4 | `float32` | DOP |

**Audio samples -- immediately after header, `blockSize x 4` bytes:**

| Offset within sample | Size | Type | Field |
|---|---|---|---|
| 0 | 4 | `float32` | Amplitude |

### Format auto-detection

`UdpListener` dispatches incoming bytes to `PacketDeserializer` based on packet size:

| Port | Packet size | Format selected |
|---|---|---|
| 5000 (Stokes) | exactly 24 bytes | streamer-service raw |
| 5000 (Stokes) | any other size | block format with header |
| 5001 (Raw audio) | exactly 8 bytes | streamer-service raw |
| 5001 (Raw audio) | any other size | block format with header |
| 5002 (Processed audio) | any size | always block format with header |

---

## Project structure

```
pm1000-visualizer/
+-- App.xaml / App.xaml.cs          Application entry point, global dark-theme styles
+-- MainWindow.xaml / .cs           Shell window; swaps content between pages,
|                                   passes ConnectionSettings and MeasurementSession
|                                   between pages, handles page navigation events
+-- Logger.cs                       Console logging (Info / Warning / Error + timestamp)
|
+-- Models/
|   +-- ConnectionSettings.cs       Snapshot of the Pre-Connection form:
|   |                               IP, three UDP ports, duration settings, IsTestMode
|   +-- MeasurementSession.cs       All data accumulated during a measurement:
|                                   List<TimestampedStokes>, List<float> raw/processed audio,
|                                   start/end time, sample rates
|
+-- Communication/
|   +-- StokeSample.cs              record struct StokeSample(S0, S1, S2, S3, Dop)
|   +-- PacketDeserializer.cs       Static parser. Four methods:
|   |                                 TryDeserializeStokes()        - block format
|   |                                 TryDeserializeAudio()         - block format
|   |                                 TryDeserializeStreamerStokes() - raw 24-byte format
|   |                                 TryDeserializeStreamerAudio()  - raw 8-byte format
|   |                               Also defines StokesPacket and AudioPacket records.
|   +-- UdpListener.cs              Opens three UdpClient sockets, one Task per socket.
|   |                               Auto-detects wire format from datagram size.
|   |                               Fires: StokesReceived, RawAudioReceived,
|   |                               ProcessedAudioReceived, PacketDropped events.
|   +-- StreamerApiClient.cs        (Unused, kept for reference. REST API was removed.)
|
+-- Services/
|   +-- DataRecorder.cs             Thread-safe (lock). Started by LivePage.
|   |                               RecordStokes() timestamps each sample with Stopwatch ms.
|   |                               RecordRawAudio() / RecordProcessedAudio() accumulate
|   |                               flat float lists. Exposes ElapsedMs.
|   +-- FileSaver.cs                SaveAll(), SaveStokes(), SaveRawAudio(), SaveProcessedAudio().
|   |                               Stokes CSV downsampled to 100 ms buckets.
|   |                               Audio exported as PCM 16-bit mono WAV (no external library).
|   +-- TestDataGenerator.cs        System.Threading.Timer at 50 ms (20 Hz).
|                                   Sends header+payload packets to localhost.
|                                   Stokes: 16 samples/pkt, sphere orbit.
|                                   Audio: 800 samples/pkt (50 ms x 16 kHz), sine waves.
|
+-- Pages/
    +-- PreConnectionPage.xaml/.cs  Form -> validates -> fires ConnectRequested(ConnectionSettings)
    +-- LivePage.xaml/.cs           UDP listener, 3D sphere, dual audio plots, elapsed timer.
    |                               Fires StopRequested when done.
    +-- PostPage.xaml/.cs           Reads MeasurementSession. Poincare playback (DispatcherTimer),
                                    MediaElement audio playback via temp WAV files.
                                    Fires NewMeasurementRequested to loop.
```

---

## Troubleshooting

**No Stokes data appears on the sphere**

- Confirm `pm1000-streamer-service` is running and sending to port 5000.
- Check Windows Firewall -- UDP inbound must be allowed on ports 5000-5002.
- Enable Test Mode to verify the UI pipeline works end-to-end without hardware.

**Audio chart shows flat line**

- The streamer-service `AudioSnapshotPacket` is currently a stub and sends 8 zero bytes. Raw audio from the streamer will show zeroes until that is fixed in the streamer codebase. Processed audio from the Rust pipeline works normally.

**App freezes or becomes unresponsive**

- Check console output (visible when running with `dotnet run`). UDP thread exceptions are logged.

**Cannot save files**

- The target folder must be writable. Temp WAV files are written to `%TEMP%\PM1000\<guid>` on Post page load; if that fails it will be logged.

---

## Known limitations and stretch goals

| Item | Status |
|---|---|
| Raw (non-normalized) Stokes display | Stretch goal -- button exists but does nothing |
| Spectrogram view for audio | Stretch goal -- buttons show placeholder message box |
| Synchronized Poincare + audio playback | Not implemented -- playback controls are independent |
| Processed audio from signal-processing | Works when Rust binary sends to port 5002 |
| Raw audio from streamer-service | Received correctly; currently all-zero due to stub in streamer |
| Remote measurement (streamer on different machine) | Supported -- just change the IP on Pre-Connection page |
