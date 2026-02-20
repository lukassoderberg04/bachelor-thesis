# PM1000 Visualizer — Three-Page Measurement Suite

A real-time WPF application for visualizing polarimetric Stokes data and synchronized audio from the PM1000 optical device.

---

## Overview

The visualizer implements a **three-page workflow**:

1. **Pre-Connection** — Configure streamer IP, ports, and measurement duration
2. **Live** — Watch the Poincaré sphere and audio waveforms in real time
3. **Post** — Playback the Poincaré trajectory, save data to disk

---

## Architecture

```
PM1000 device
     │ USB
     ▼
pm1000-streamer-service   (Lukas)
     │ UDP :5000  Stokes packets
     │ UDP :5001  Raw audio (microphone)
     │ HTTP :5003 REST API
     │
     └─────────────────────┐
                           │
signal-processing         │
(Rust, Ludwig)            │
     │ UDP :5002           │
     │ Processed audio     │
     │                     │
     ▼                     ▼
pm1000-visualizer (this project)
```

---

## Three Pages

### Page 1: Pre-Connection

Configure before measurement:
- **Streamer IP** (default `127.0.0.1`)
- **API Port** (default `5003`) — REST endpoint for device info
- **Stokes Port** (default `5000`) — UDP for Stokes parameters (S0, S1, S2, S3, DOP)
- **Raw Audio Port** (default `5001`) — UDP for microphone audio from streamer
- **Processed Audio Port** (default `5002`) — UDP for signal-processed audio (from Ludwig's Rust pipeline)
- **Duration** — Indefinite or fixed (e.g., 30 seconds)
- **Test Mode** — Generate synthetic UDP packets (for offline testing)

Click **Connect** to proceed to the Live page.

### Page 2: Live Measurement

Real-time monitoring:

**Left panel: Poincaré Sphere**
- 3D visualization of normalized Stokes vectors (S1, S2, S3) on unit sphere
- Red-to-gray fading trail showing recent measurement history
- **Trail ✓** checkbox to toggle trail visibility
- **Reset View** button to recenter camera

**Right panel: Stokes Readout** (updated every packet)
- **Power** — S0 in µW (microwatts)
- **S1, S2, S3** — Normalized Stokes parameters (−1 to +1)
- **DOP** — Degree of polarization as percentage
- **Polarization** — Human-readable label (Linear, Circular, Elliptical, Unpolarized)

**Center: Controls**
- **Normalized/Raw** — Toggle between normalized and raw Stokes (Raw is a stretch goal; currently always normalized)
- **Trail ✓** — Show/hide point trail on sphere
- **❌ STOP** — End measurement and go to Post page

**Bottom two panels: Audio Waveforms**
- **Processed Audio** (port 5002)
  - Graph/Spec buttons (Spec is placeholder)
  - **Overlay Reference** — Toggle raw audio overlay (red) on processed plot (blue)
- **Raw Reference Audio** (port 5001)
  - For comparison with processed signal

All audio displays are rolling 2-second windows at the packet sample rate.

**Duration timer** (if fixed):
- Auto-transitions to Post page when time expires
- Otherwise user controls via **STOP** button

### Page 3: Post-Measurement

Playback and export:

**Save buttons** (top)
- **Save All to Folder…** — Creates timestamped subfolder with all data
- **Save Stokes CSV**, **Save Raw Audio**, **Save Processed Audio** — Individual file saves

**Folder structure for "Save All":**
```
Measurements/2026-02-20_14-35-22/
├── stokes.csv              (100 ms intervals: timestamp_ms, S0_µW, S1, S2, S3, DOP)
├── audio_raw.wav           (PCM 16-bit mono)
└── audio_processed.wav     (PCM 16-bit mono)
```

**Poincaré Playback**
- Visualizes the recorded trajectory with 40-point fading trail
- **▶ Play / ⏸ Pause** — Playback controls
- **⏹ Stop** — Reset to start
- Slider — Scrub to any point in the measurement
- Time display: `0:00 / 5:23` format

**Audio playback** (independent)
- **Processed Audio** player — controls and current status
- **Raw Reference Audio** player — side-by-side for comparison
- MediaElement (WPF built-in) for WAV playback

**New Measurement** button → returns to Pre-Connection page

---

## Prerequisites

- .NET 8.0 SDK
- Windows 10+ (WPF)
- Visual Studio 2022 recommended (optional; CLI works fine)

### NuGet packages

| Package                   | Purpose                     |
| ------------------------- | --------------------------- |
| `ScottPlot.WPF 5.1.57`    | Audio waveform plots        |
| `HelixToolkit.Wpf 2.23.0` | 3D Poincaré sphere viewport |

Packages are restored automatically by NuGet on first build.

---

## Building & Running

```bash
cd pm1000-visualizer
dotnet build
dotnet run --project pm1000-visualizer.csproj
```

Or open `pm1000-visualizer.sln` in Visual Studio and press **F5**.

Output: `bin/Debug/net8.0-windows/pm1000-visualizer.exe`

---

## Test Mode

Enable **Test Mode** on the Pre-Connection page. The app will:
1. Ignore UDP listeners — use internal `TestDataGenerator` instead
2. Send synthetic packets to `localhost` on all 3 ports
3. Generate:
   - **Stokes**: smooth orbit on the Poincaré sphere (S0 ≈ 13–17 µW, DOP ≈ 97%)
   - **Raw audio**: 440 Hz sine wave (A4 note)
   - **Processed audio**: 880 Hz sine wave (A5 note) — visually distinct
4. All data is recorded and can be saved / played back normally

**Use case**: Offline testing, UI testing, and demo without real hardware.

---

## Data Formats

### Stokes CSV (100 ms downsampled)
```
timestamp_ms,S0_uW,S1,S2,S3,DOP
0,15.2,0.045,-0.123,0.067,0.972
100,15.3,0.041,-0.125,0.070,0.970
200,15.1,0.048,-0.121,0.065,0.975
```

### Audio WAV
- Format: PCM 16-bit signed, mono
- Sample rate: configured from streamer (typically 16 kHz)
- Amplitude range: −1.0 to +1.0 (clipped to [−32768, +32767] on save)

---

## UDP Packet Format

Both Stokes and audio packets share the same 10-byte header:

| Bytes | Type     | Field                          |
| ----- | -------- | ------------------------------ |
| 0–3   | `uint32` | Sequence number                |
| 4–7   | `uint32` | Sample rate (Hz)               |
| 8–9   | `uint16` | Block size (number of samples) |

Followed by samples:

| Packet type | Port | Per-sample layout                              |
| ----------- | ---- | ---------------------------------------------- |
| Stokes      | 5000 | 5 × `float32` → S0, S1, S2, S3, DOP (20 bytes) |
| Raw Audio   | 5001 | 1 × `float32` → amplitude (4 bytes)            |
| Proc Audio  | 5002 | 1 × `float32` → amplitude (4 bytes)            |

All values little-endian. See `Communication/PacketDeserializer.cs` for the implementation.

---

## REST API (Streamer)

The visualizer can query the streamer on the configured API port via `Communication/StreamerApiClient.cs`:

| Method | Endpoint      | Description                 |
| ------ | ------------- | --------------------------- |
| GET    | `/frequency`  | Laser light frequency in Hz |
| GET    | `/samplerate` | Current sampling rate in Hz |
| POST   | `/samplerate` | Set a new sampling rate     |

(Not required for test mode; UI shows "N/A" if unavailable.)

---

## Project Structure

```
pm1000-visualizer/
├── Communication/
│   ├── StokeSample.cs          # Data model (S0–S3, DOP)
│   ├── PacketDeserializer.cs   # Parses raw UDP bytes into typed packets
│   ├── UdpListener.cs          # Background UDP receivers, fires typed events
│   └── StreamerApiClient.cs    # REST client for streamer metadata
├── Models/
│   ├── ConnectionSettings.cs   # User input from Pre-Connection page
│   └── MeasurementSession.cs   # Holds all recorded data
├── Services/
│   ├── DataRecorder.cs         # Thread-safe packet recorder with timestamps
│   ├── FileSaver.cs            # Exports CSV + WAV files
│   └── TestDataGenerator.cs    # Synthetic UDP packet generator
├── Pages/
│   ├── PreConnectionPage.xaml  # Configuration input UI
│   ├── LivePage.xaml           # Real-time measurement monitor
│   └── PostPage.xaml           # Playback & export tools
├── MainWindow.xaml             # Page container shell
├── MainWindow.xaml.cs          # Navigation logic
├── App.xaml                    # Dark-theme styles
└── Logger.cs                   # Console logging with timestamps
```

---

## Key Features

✅ **Three-page workflow** for intuitive measurement lifecycle  
✅ **Real-time 3D Poincaré sphere** with fading trail (HelixToolkit)  
✅ **Dual audio waveforms** (processed + raw reference) with overlay  
✅ **Post-measurement playback** — scrubby timeline, independent audio players  
✅ **Data export** — timestamped CSV + PCM WAV files  
✅ **Test mode** — offline testing with synthetic data  
✅ **Thread-safe recording** — packets received on background threads  
✅ **Dark UI theme** — optimized for lab environment  
✅ **Configurable ports** — IP + 4 ports (1 REST + 3 UDP) user-selectable  

### Stretch goals (not yet implemented)
- Raw (non-normalized) Stokes display
- Spectrogram view for audio
- Synchronized Poincaré ↔ audio playback

---

## Troubleshooting

### App freezes
Check the Console output (attached to `dotnet run`). UDP listener errors will be logged as warnings.

### No packets received
- Verify streamer is running on correct IP/port
- Check firewall allows UDP inbound on ports 5000–5002
- Enable **Test Mode** to verify app logic works

### Cannot save files
Check Windows write permissions on the target folder. Temp files are in `%TEMP%\PM1000\<guid>`.

---

## Future Work

1. **Sync audio ↔ Poincaré playback** (currently independent)
2. **Raw Stokes mode** (stretch goal, not in current spec)
3. **Spectrogram views** for frequency analysis
4. **Calibration mode** for the measurement device
5. **Export to HDF5** or other analysis formats
6. **Remote measurement** (streamer on different machine)

---

## License

© 2026 [Your Institution]. See bundled FTDI driver documentation for third-party requirements.
└── App.xaml / App.xaml.cs      # WPF application entry point
```

---

## Test mode

When `pm1000-streamer-service` is not running, the app generates synthetic data automatically:

- **Stokes**: smooth orbital path around the Poincaré sphere
- **Audio**: 440 Hz sine wave (A4)

To disable test mode when integrating with Lukas's streamer, remove the `StartTestDataGenerator()` call in `Window_Loaded()`.
