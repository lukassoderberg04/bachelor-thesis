# Signal-Processing → Visualizer Integration Guide

This guide explains how to wire the real-time Stokes stream into the signal-processing
pipeline and send the resulting audio samples to the visualizer.

---

## 1. What the visualizer expects

`pm1000-visualizer` listens on **UDP port 5001** for audio packets.

**Packet format (little-endian):**

| Offset | Type               | Field            | Description                            |
| ------ | ------------------ | ---------------- | -------------------------------------- |
| 0      | u32                | `sequence_nr`    | Monotonically increasing, wraps at 2³² |
| 4      | u32                | `sample_rate_hz` | e.g. `16000`                           |
| 8      | u16                | `block_size`     | Number of f32 samples that follow      |
| 10     | f32 × `block_size` | `amplitude`      | One sample per entry, −1.0 … +1.0      |

---

## 2. What is already done

`src/udp_sender.rs` provides `AudioUdpSender` — a thin wrapper that handles
serialization, sequence numbering and UDP dispatch. You only need to call one
method from your processing loop:

```rust
sender.send_block(&samples)?;   // samples: &[f32]
```

No external crate is required; it uses only `std::net::UdpSocket`.

---

## 3. How to integrate your streaming pipeline

### 3.1 Replace the static CSV read with your incoming stream

The current `main.rs` reads a CSV file once. Replace that section with whatever
source delivers Stokes vectors in real time (e.g. reading UDP packets from
Lukas's streamer on port 5000, or reading from a shared buffer).

Each time you receive a block of `N` Stokes measurements you will have a matrix
`s` of shape `(N, 3)` containing normalized S1, S2, S3.

### 3.2 Replace `static_pca` with `ojas_rule`

`static_pca` uses SVD over the entire recording — that is not possible in real
time. Use the existing `ojas_rule` function to update a weight vector
incrementally for each incoming Stokes vector:

```rust
// Initialise once, outside your loop:
let mut weights = Array1::from_vec(vec![1.0_f64 / 3.0f64.sqrt(); 3]);
let learning_rate = 0.01;

// Inside your processing loop, for each incoming Stokes block:
let mut audio_block: Vec<f32> = Vec::with_capacity(block.len());
for row in s.rows() {
    let amplitude = ojas_rule(&mut weights, &row, learning_rate);
    audio_block.push(amplitude as f32);
}
```

> **Tip:** The learning rate controls how fast the principal component estimate
> adapts. Start with `0.01` and tune if the signal sounds unstable.

### 3.3 Apply the highpass filter per block

The Butterworth coefficients `(b, a)` only need to be computed once.
Use `lfilter` (causal, single-pass) instead of `filtfilt` (requires the whole
signal) so it works on a rolling stream:

```rust
// Compute once:
let cutoff_hz = 20.0;
let nyquist   = sample_rate_hz as f64 / 2.0;
let (b, a)    = butter(4, cutoff_hz / nyquist, FilterType::Highpass).unwrap();

// Per block:
let filtered = lfilter(&b, &a, &audio_block_f64, None).unwrap();
let samples_f32: Vec<f32> = filtered.iter().map(|&x| x as f32).collect();
```

### 3.4 Send the block

```rust
// Initialise once:
let mut sender = AudioUdpSender::new("127.0.0.1", sample_rate_hz)
    .expect("Failed to bind UDP socket");

// Per block (after filtering):
sender.send_block(&samples_f32)?;
```

---

## 4. Full streaming loop sketch

```rust
mod udp_sender;
use udp_sender::AudioUdpSender;

// --- one-time setup ---
let mut weights     = Array1::from_vec(vec![1.0_f64 / 3.0f64.sqrt(); 3]);
let learning_rate   = 0.01_f64;
let sample_rate_hz  = 16_000_u32;
let (b, a)          = butter(4, 20.0 / (sample_rate_hz as f64 / 2.0), FilterType::Highpass).unwrap();
let mut sender      = AudioUdpSender::new("127.0.0.1", sample_rate_hz).unwrap();

// --- per-block (inside your receive loop) ---
loop {
    // 1. Get next Stokes block from the stream.
    //    `s` is an Array2<f64> of shape (block_size, 3): columns are S1/S0, S2/S0, S3/S0.
    let s: Array2<f64> = receive_next_stokes_block()?;

    // 2. Oja's rule → scalar amplitude per sample.
    let mut raw: Vec<f64> = Vec::with_capacity(s.nrows());
    for row in s.rows() {
        raw.push(ojas_rule(&mut weights, &row, learning_rate));
    }

    // 3. Highpass filter.
    let filtered = lfilter(&b, &a, &raw, None)?;

    // 4. Convert f64 → f32 and send.
    let samples: Vec<f32> = filtered.iter().map(|&x| x as f32).collect();
    sender.send_block(&samples)?;
}
```

---

## 5. Running the visualizer alongside

Start the `pm1000-visualizer` on the same machine (or set `"127.0.0.1"` in
`AudioUdpSender::new` to the visualizer machine's IP). The test-data generator
inside the visualizer will keep the Stokes sphere animated; your audio data
replaces the synthetic 440 Hz sine wave on the audio waveform plot as soon as
real packets arrive on port 5001.

---

## 6. Checklist

- [ ] Replace CSV read with live Stokes stream source
- [ ] Switch `static_pca` → `ojas_rule` inside the loop
- [ ] Switch `filtfilt` → `lfilter` for real-time filtering
- [ ] Call `sender.send_block(&samples)` once per processing window
- [ ] Verify packet reception in the visualizer's `StatusText` / log output
