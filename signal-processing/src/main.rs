mod udp_sender;
use udp_sender::AudioUdpSender;

use std::error::Error;

use ndarray::prelude::*;
use num_complex::{ComplexFloat};
use ruviz::{axes::AxisScale, core::Plot};
use scirs2::{
    linalg::{compat::Norm, svd},
    signal::{
        FilterType, butter,
        filter::{self, lfilter, parallel_filtfilt},
        filtfilt,
        parametric::{SpectrumOptions, detect_spectral_peaks},
    },
};
use scirs2_fft::{fft::complex_magnitude, rfft, rfftfreq};
use scirs2_io::csv::{CsvReaderConfig, read_csv};

fn main() {
    // TODO! Read data from stream

    // TODO! Implement proper error handling once functionality is verified

    let config = CsvReaderConfig {
        comment_char: Some('#'),
        has_header: false,
        skip_rows: 11,
        trim: true,
        ..Default::default()
    };

    let (_, data) = read_csv(
        "./example_data/polarimeter_recording_440Hz_0.txt",
        Some(config),
    )
    .expect("Failed to read CSV file");

    // Timestamps, S0, S1, S2, S3
    let data = data.map(|f| f.parse::<f64>().unwrap());

    // Convert from nanosecond timestamps to second timestamps
    let timestamps = data.slice(s![.., 0]).into_owned() * 1e-9;

    // Extract the stokes parameters S1 through S3 and normalize them by S0
    let s = data.slice(s![.., 2..=4]).into_owned() / data.column(1).insert_axis(Axis(1));

    let dt = timestamps.diff(1, Axis(0));
    let sampling_rate = 1.0 / dt.mean().unwrap();

    // Perform PCA using SVD. This only works for static datasets and will not work for the future stream
    let amplitudes = static_pca(&s.view()).unwrap();

    // Filter frequencies
    let cutoff_freq = 20.0; // Hz
    let nyquist_freq = sampling_rate / 2.0;
    let normalized_cutoff = cutoff_freq / nyquist_freq;

    let (b, a) = butter(4, normalized_cutoff, FilterType::Highpass).unwrap();
    let hp_amplitudes = Array1::from_vec(parallel_filtfilt(&b, &a, &amplitudes.as_slice().unwrap(), None).unwrap());

    let n = hp_amplitudes.len();
    let window = Array1::from_shape_fn(n, |i| {
        0.5 * (1.0 - (2.0 * std::f64::consts::PI * i as f64 / (n as f64 - 1.0)).cos())
    });

    let windowed_signal = &hp_amplitudes * &window;

    let spectrum = rfft(windowed_signal.as_slice().unwrap(), Some(n))
        .unwrap()
        .iter()
        .map(|f| f.abs() / (n as f64 / 2.0) * 2.0)
        .collect::<Array1<f64>>();

    let freqs = Array1::from_vec(rfftfreq(n, 1.0 / sampling_rate).unwrap());

    let peak_options = SpectrumOptions {
        peak_threshold: 2e-5,
        ..Default::default()
    };

    let peaks = detect_spectral_peaks(&spectrum, &freqs, &peak_options)
        .unwrap()
        .into_iter()
        .filter(|peak| peak.prominence >= 2.0e-5);

    let mut fig = Plot::new()
        .line(&freqs.as_slice().unwrap(), &spectrum.as_slice().unwrap())
        .xlabel("Frekvens")
        .ylabel("Magnitud");

    for peak in peaks {
        println!(
            "Peak with frequency {:.1}, magnitude {:.2e} and prominence {:.2e}",
            peak.frequency, peak.power, peak.prominence
        );

        fig = fig.vline(peak.frequency)
    }

    fig.save_with_size("./junk/fft.png", 1280, 960)
        .unwrap();

    

    // Convert the extracted amplitudes (f64) to f32 audio samples.
    let audio_samples: Vec<f32> = hp_amplitudes.iter().map(|&x| x as f32).collect();

    // Send the block to the visualizer on port 5001.
    // In the streaming integration, call send_block() once per processing window
    // instead of once for the whole recording — see Documents/integration-guide.md.
    let mut sender = AudioUdpSender::new("127.0.0.1", sampling_rate as u32)
        .expect("Failed to bind UDP socket");
    sender
        .send_block(&audio_samples)
        .expect("Failed to send audio UDP packet");
}

fn static_pca(s: &ArrayView2<f64>) -> Result<Array1<f64>, Box<dyn Error>> {
    let s_centered = s.to_owned() - s.mean_axis(Axis(0)).unwrap();

    let (_u, _s, vt) = svd(&s_centered.view(), false, None)?;

    let weights_pca = vt.slice(s![0, ..]).to_owned();

    let amplitudes = s_centered.dot(&weights_pca);

    Ok(amplitudes)
}

fn ojas_rule(
    weights: &mut Array1<f64>,
    normalised_stokes_vector: &ArrayView1<f64>,
    learning_rate: f64,
) -> f64 {
    let y = weights.dot(normalised_stokes_vector);

    *weights = &*weights + learning_rate * y * (normalised_stokes_vector - &*weights * y);

    y
}
