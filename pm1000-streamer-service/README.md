# pm1000-streamer-service

Console service that reads raw data from the PM1000 polarimeter over USB and broadcasts Stokes parameters over UDP to connected visualizers.

---

## Architecture

```
PM1000 device
     │  USB (FTDI FT60x)
     ▼
pm1000-streamer-service   (this project)
     │  UDP :5000  Stokes packets  ──► pm1000-visualizer
     └  HTTP :5002 REST API         ──► pm1000-visualizer
```

---

## Prerequisites

- .NET 8.0 SDK
- Windows
- FTDI FT60x USB driver installed — see [FTDI Drivers Installation Guide](Documents/FTDI_Drivers_Installation_Guide__Windows_10_11.pdf)

### Native DLLs

The project references FTDI's managed wrapper. Both files must be present in the `lib/` folder:

| File | Description |
|---|---|
| `FTD3XXWU.dll` | Native FTDI FT60x driver |
| `FTD3XXWU_NET.dll` | Managed (.NET) wrapper for the above |

> These are copy-to-output, so they will appear next to the built executable automatically.

### NuGet packages

None. The streamer has no NuGet dependencies.

---

## Building & running

```
cd pm1000-streamer-service
dotnet build
dotnet run
```

On startup the service lists detected FTDI devices. Select the PM1000 device to begin streaming.

---

## UDP packet format

All packets share the same 10-byte header, followed by samples:

### Header (10 bytes)

| Bytes | Type | Field |
|---|---|---|
| 0–3 | `uint32` | Sequence number |
| 4–7 | `uint32` | Sample rate (Hz) |
| 8–9 | `uint16` | Block size (number of samples in this packet) |

### Stokes packet — port 5000

Each sample is 20 bytes (5 × `float32`, little-endian):

| Field | Range | Description |
|---|---|---|
| S0 | > 0 | Total optical power (µW) |
| S1 | −1 … +1 | Linear horizontal/vertical |
| S2 | −1 … +1 | Linear ±45° |
| S3 | −1 … +1 | Circular polarization |
| DOP | 0 … 1 | Degree of polarization |

See `Helper/Streaming/PacketSerializer.cs` for the serialization implementation.

---

## REST API — port 5002

| Method | Endpoint | Description |
|---|---|---|
| GET | `/frequency` | Laser light frequency in Hz (`double`) |
| GET | `/samplerate` | Current PM1000 sampling rate in Hz (`uint`) |
| POST | `/samplerate` | Set a new sampling rate |

---

## Project structure

```
pm1000-streamer-service/
├── Helper/
│   ├── ConfigurationBuilder.cs     # Reads appsettings / config
│   ├── DeviceInfoWrapper.cs        # Wraps FTDI device info
│   ├── FtdiService.cs              # Lists & connects to FTDI devices
│   ├── Logger.cs                   # Console logging with timestamps
│   ├── PM1000/
│   │   ├── CRC.cs                  # CRC validation for PM1000 frames
│   │   ├── PM1000Service.cs        # Reads measurement frames from device
│   │   ├── Register.cs             # PM1000 register map constants
│   │   ├── Transmitter.cs          # Low-level USB read/write
│   │   └── Packets/
│   │       ├── Packet.cs           # Base packet type
│   │       ├── PacketType.cs       # Enum of packet types
│   │       ├── ReadPacket.cs       # Read-register packet
│   │       ├── TransferPacket.cs   # Data transfer packet
│   │       └── WritePacket.cs      # Write-register packet
│   └── Streaming/
│       ├── StokeSample.cs          # Data model (S0–S3, DOP)
│       ├── PacketSerializer.cs     # Serializes samples into UDP bytes
│       └── UdpBroadcaster.cs       # Sends packets to registered clients
├── Program.cs                      # Entry point, device selection loop
└── lib/
    ├── FTD3XXWU.dll
    └── FTD3XXWU_NET.dll
```

---

## Integration guide — how to send data to the visualizer

Three files have been added under `Helper/Streaming/`. Here is exactly how to wire them into your existing code.

### 1. Create the broadcaster (once, on startup)

```csharp
using pm1000_streamer_service.Streaming;

var broadcaster = new UdpBroadcaster();

// Add the IP of the machine running the visualizer.
// Use "127.0.0.1" if both programs run on the same PC.
broadcaster.AddTarget("127.0.0.1");
```

### 2. Build a `StokeSample` from your PM1000 measurement

Each time PM1000Service gives you a measurement, wrap it in a `StokeSample`:

```csharp
var sample = new StokeSample(
    s0:  rawMeasurement.S0,   // optical power µW
    s1:  rawMeasurement.S1,   // normalized −1…+1
    s2:  rawMeasurement.S2,
    s3:  rawMeasurement.S3,
    dop: rawMeasurement.DOP   // 0…1
);
```

### 3. Send a block of samples

Collect however many samples you read in one polling cycle into an array, then call `SendStokes`. The broadcaster handles the packet header (sequence number, sample rate, block size) automatically.

```csharp
// Example: one sample per send — or batch many samples together
var block = new StokeSample[] { sample };
broadcaster.SendStokes(block, sampleRateHz: 16000);
```

> `sampleRateHz` should match the actual PM1000 sampling rate you configured. The visualizer uses this to label the display.

### 4. Dispose when done

```csharp
broadcaster.Dispose();
```

Or wrap in a `using` block if the broadcaster's lifetime is scoped.

---

### What you do NOT need to touch

- `PacketSerializer` — called internally by `UdpBroadcaster.SendStokes`, you never call it directly.
- Ports — `UdpBroadcaster` always sends to port **5000**. The visualizer listens on that port. No config needed.
- Audio (port 5001) — Ludwig's responsibility, not part of this service.

---

## PM1000 hardware

The PM1000 is a polarimeter capable of measuring all four Stokes parameters at up to 100 MHz. For full hardware documentation see [PM1000 User Guide](Documents/PM1000_User_Guide.pdf).
