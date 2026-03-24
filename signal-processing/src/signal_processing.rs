use std::{
    collections::VecDeque,
    sync::{
        atomic::{AtomicBool, Ordering},
        mpsc::{Receiver, Sender},
    },
};

use ndarray::{Array1, ArrayView1};
use scirs2::{
    linalg::compat::Norm,
    signal::{FilterType, butter},
};
use scirs2_fft::rfft;

use crate::OJA_LEARNING_RATE;

/// Performs adaptive PCA on the stokes vectors using Oja's rule.
/// 
/// ## Parameters
/// 
/// - `rx` Reciever for the input data in the form of `(timestamp, [s1, s2, s3])`, 
/// where s_i is the i-th stokes parameter.
/// - `ty` Sender for the output data. Data  vill be sent as `(timestamp, amplitude)`
/// - `should_stop` Atomic bool flag for broadcasting when to halt operations.
/// 
/// ## Returns
/// 
/// This function only returns in the case of an error or if `should_stop` is set to `true`.
/// Output data is instead sent through `ty`.
pub fn pca(
    rx: &Receiver<(u32, Array1<f64>)>,
    ty: Sender<(u32, f64)>,
    should_stop: &AtomicBool,
) -> Result<(), String> {
    let mut weights = Array1::from_vec(vec![1f64 / f64::sqrt(3f64); 3]);

    while !should_stop.load(Ordering::Relaxed) {
        let (t, s) = rx.recv().map_err(|err| format!("{}", err))?;

        let y = ojas_rule(&mut weights, &s.view(), OJA_LEARNING_RATE);

        ty.send((t, y)).map_err(|err| format!("{}", err))?;
    }

    Ok(())
}

/// Uses Oja's rule to perform PCA of data where the future of the dataset is unknown.
///
/// ## Parameters
///
/// - `weights` An `&mut Array1` with weights for each input component.
/// The array can have any (normalized) weights when the function is initially called,
/// but should be reused and untouched during the run.
///
/// - `x` An `&ArrayView1` with the input data.
///
/// - `learning_rate` The rate of how fast the weights should be adjusted.
///
/// ## Returns
///
/// The magnitude along the principal component axis.
fn ojas_rule(weights: &mut Array1<f64>, x: &ArrayView1<f64>, learning_rate: f64) -> f64 {
    let y = weights.dot(x);

    *weights = &*weights + learning_rate * y * (x - &*weights * y);
    *weights /= weights.norm(); // Normalize 

    y
}

/// Reads data from a channel, applies a highpass butterworth filter and returns the filtered data through a second channel.
///
/// ## Parameters
///
/// - `rx` A reciever for the input channel.
/// - `ty` A sender for the output channel.
/// - `should_stop` An `AtomicBool` signifying weather the function should cease operations.
/// - `cutoff_freq` The cuttoff frequency for the filter.
/// - `time_units` Units of the timestamps (eg `10e-3` for milliseconds)
/// - `BUFFER_SIZE` The size of buffers used to store values. Equal to `FILTER_ORDER + 1`.
///
/// ## Returns
///
/// The function never returns unless an error occurs or `should_stop` is set to `true`.
/// The values are instead returned through the output channel.
pub fn highpass<const BUFFER_SIZE: usize>(
    rx: &Receiver<(u32, f64)>,
    ty: Sender<(u32, f64)>,
    should_stop: &AtomicBool,
    cutoff_freq: f64,
    time_units: f64,
) -> Result<(), String> {
    let mut b = [0.0; BUFFER_SIZE];
    let mut a = [0.0; BUFFER_SIZE];

    // A and B will be length N + 1, where N is the filter order.
    // Therefore we need to store N y values from history
    // and to make life easier we push the current value of x into
    // history for our calculations.
    let mut y_prev = [0.0; BUFFER_SIZE];
    let mut x_prev = [0.0; BUFFER_SIZE];

    let mut current_sampling_freq: Option<f64> = None;
    let mut t_prev = None;

    while !should_stop.load(Ordering::Relaxed) {
        let (t, x) = rx.recv().map_err(|err| format!("{}", err))?;

        let dt = match t_prev {
            None => {
                t_prev = Some(t);
                continue;
            }
            Some(prev) if t <= prev => {
                t_prev = Some(t);
                continue;
            }
            Some(prev) => (t - prev) as f64,
        };
        t_prev = Some(t);

        // Correct for units for t
        let instant_freq = 1.0 / (dt as f64 * time_units);

        // Initialize sampling frequency and filter coefficients if it has not been done yet
        // or reinitialize if sampling frequency has drifted by more than 2%
        if current_sampling_freq.map_or(true, |f| (instant_freq - f).abs() / f > 0.02) {
            current_sampling_freq = Some(instant_freq);

            let nyquist_freq = current_sampling_freq.unwrap() / 2f64;
            let cutoff = cutoff_freq / nyquist_freq;

            let (new_b, new_a) = butter(BUFFER_SIZE - 1, cutoff, FilterType::Highpass)
                .map_err(|err| format!("{}", err))?;
            b.copy_from_slice(&new_b[..]);
            a.copy_from_slice(&new_a[..]);
        }

        // Shift X values into history
        for i in (1..BUFFER_SIZE).rev() {
            x_prev[i] = x_prev[i - 1];
        }
        x_prev[0] = x;

        // Calculate y
        let b_part = b
            .iter()
            .zip(x_prev.iter())
            .fold(0f64, |acc, (b, x)| acc + b * x);
        let a_part = a
            .iter()
            .skip(1)
            .zip(y_prev.iter())
            .fold(0f64, |acc, (a, y)| acc + a * y);

        let y = b_part - a_part;

        // Shift Y values into history
        for i in (1..BUFFER_SIZE).rev() {
            // Changed from BUFFER_SIZE - 1
            y_prev[i] = y_prev[i - 1];
        }
        y_prev[0] = y;

        ty.send((t, y)).map_err(|err| format!("{}", err))?
    }

    Ok(())
}

/// Reads values from a channel, does a STFT on them and sends the resulting vectos through an output channel.
///
/// ## Parameters
///
/// - `rx` A reciever for the input channel.
/// - `ty` A sender for the output channel.
/// - `should_stop` An `AtomicBool` signifying weather the function should cease operations.
/// - `window_size` The number of elements on which to perform each iteration of the STFT.
/// - `hop_size` The number of elements between each iteration of the STFT.
///
/// ## Returns
///
/// The function never returns unless an error occurs or `should_stop` is set to `true`.
/// The values are instead returned through the output channel in the form of `Vec<f64>`
/// corresponding to the intensity at each frequency bin for one iteration.
pub fn stft(
    rx: &Receiver<f64>,
    rt: &Receiver<u32>,
    ty: Sender<(u32, Vec<f64>)>,
    should_stop: &AtomicBool,
    window_size: usize,
    hop_size: usize,
) -> Result<(), String> {
    let mut x = VecDeque::new();
    let mut t = VecDeque::new();
    let mut iter = 0usize..;

    // Window size being 0 will cause logical errors
    if window_size == 0 {
        return Err("Window size can not be 0.".into());
    }

    while !should_stop.load(Ordering::Relaxed) {
        let i = iter.next().unwrap(); // Will not return None

        // Recieve values from the channels and insert them into the deque.
        x.push_back(rx.recv().map_err(|err| format!("{}", err))?);
        t.push_back(rt.recv().map_err(|err| format!("{}", err))?);

        // Make sure enough samples have arrived and that the window is big enough.
        if x.len() < window_size + 1 {
            continue;
        }

        // Remove now irrelevant values. The previous guard clause asserts that the deque is not empty.
        x.pop_front().unwrap();
        t.pop_front().unwrap();

        // Only do the FFT when the hop size has been reached.
        if i % hop_size != 0 {
            continue;
        }
        // Reset iterator to avoid reaching integer limit
        iter = 0usize..;

        // Rearrange the contents of the deque into a single slice and pass it to the fourier iteration.
        let intensity_spectrum = stft_iteration(x.make_contiguous())?;

        let t_mean = (t[0] + t.iter().last().unwrap()) / 2;

        ty.send((t_mean, intensity_spectrum))
            .map_err(|err| format!("{}", err))?;
    }

    Ok(())
}

/// Performs an iteration of the STFT with a few adjustments.
///
/// ## Parameters
///
/// - `x` A slice containing the values on which to perform the operation.
///
/// ## Returns
///
/// A vector of intensities represented as the magnitudes squared of the frequency spectrum.
pub fn stft_iteration(x: &[f64]) -> Result<Vec<f64>, String> {
    let n = x.len();

    // Convert to Array1 to help with calculations
    let x = Array1::from_iter(x.into_iter());

    // Generate window coefficients
    let window = hann(n);

    let x_weighted = window * x;

    // Perform the FFT on the weighted function
    let spectrum = rfft(
        &x_weighted
            .as_slice()
            .ok_or("Could not create a slice from `x_weighted`.")?,
        Some(n),
    )
    .map_err(|err| format!("{}", err))?;

    // Return intensity as magnitude squared
    Ok(spectrum
        .iter()
        .map(|a| a.norm().powi(2))
        .collect::<Vec<f64>>())
}

/// Creates a hann window of a given length.
///
/// ## Parameters
///
/// - `n` Number of samples.
///
/// ## Returns
///
/// An `Array1<f64>` of samples of the hann window.
pub fn hann(n: usize) -> Array1<f64> {
    Array1::from_shape_fn(n, |i| {
        0.5 * (1.0 - (2.0 * std::f64::consts::PI * i as f64 / (n as f64 - 1.0)).cos())
    })
}
