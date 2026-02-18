# pm1000-visualizer

WPF application that visualizes real-time Stokes parameter data from the PM1000 polarimeter.  
It receives data over UDP from `pm1000-streamer-service` and displays:

- **Poincaré sphere** — 3D visualization of the polarization state with a fading trajectory trail
- **Audio waveform** — extracted audio signal sent by Ludwig's signal processing module

---

## Architecture

```
PM1000 device
     │  USB
     ▼
pm1000-streamer-service   (Lukas)
     │  UDP :5000  Stokes packets
     │  UDP :5001  Audio packets   ◄── Ludwig signal processor sends here
     │  HTTP :5002 REST API
     ▼
pm1000-visualizer         (this project)
```

---

## Prerequisites

- .NET 8.0 SDK
- Windows (WPF)
- Visual Studio 2022 recommended

### NuGet packages

| Package                   | Purpose                     |
| ------------------------- | --------------------------- |
| `ScottPlot.WPF 5.1.57`    | Audio waveform plot         |
| `HelixToolkit.Wpf 2.23.0` | 3D Poincaré sphere viewport |

Packages are restored automatically by NuGet on first build. The `nuget.config` in the project root points to the correct feed.

---

## Building & running

```
cd pm1000-visualizer
dotnet build
dotnet run
```

Or open `pm1000-visualizer.sln` in Visual Studio and press **F5**.

---

## UDP packet format

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
| Audio       | 5001 | 1 × `float32` → amplitude (4 bytes)            |

All values little-endian. See `Communication/PacketDeserializer.cs` for the implementation.

---

## REST API (streamer)

The visualizer can query the streamer on port 5002 via `Communication/StreamerApiClient.cs`:

| Method | Endpoint      | Description                 |
| ------ | ------------- | --------------------------- |
| GET    | `/frequency`  | Laser light frequency in Hz |
| GET    | `/samplerate` | Current sampling rate in Hz |
| POST   | `/samplerate` | Set a new sampling rate     |

---

## Project structure

```
pm1000-visualizer/
├── Communication/
│   ├── StokeSample.cs          # Data model (S0–S3, DOP)
│   ├── PacketDeserializer.cs   # Parses raw UDP bytes into typed packets
│   ├── UdpListener.cs          # Background UDP receiver, fires events
│   └── StreamerApiClient.cs    # REST client for streamer metadata
├── MainWindow.xaml             # UI layout (3D viewport + audio plot)
├── MainWindow.xaml.cs          # Main logic, visualization, test data generator
├── Logger.cs                   # Console logging with timestamps and colors
└── App.xaml / App.xaml.cs      # WPF application entry point
```

---

## Test mode

When `pm1000-streamer-service` is not running, the app generates synthetic data automatically:

- **Stokes**: smooth orbital path around the Poincaré sphere
- **Audio**: 440 Hz sine wave (A4)

To disable test mode when integrating with Lukas's streamer, remove the `StartTestDataGenerator()` call in `Window_Loaded()`.
