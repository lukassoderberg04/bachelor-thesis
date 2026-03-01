use std::{collections::VecDeque, error::Error, sync::{atomic::{AtomicBool, Ordering}, mpsc::{Receiver, Sender}}};

use ndarray::{Array1, ArrayView1, array};
use scirs2::{linalg::compat::Norm, signal::{FilterType, butter}};
use scirs2_fft::rfft;

use crate::OJA_LEARNING_RATE;

pub fn pca(rx: &Receiver<Array1<f64>>, ty: Sender<f64>, should_stop: &AtomicBool) -> Result<(), String> {
    let mut weights = Array1::from_vec(vec![1f64/f64::sqrt(3f64); 3]);

    while !should_stop.load(Ordering::Relaxed) {
        let s = rx.recv().map_err(|err| format!("{}", err))?;

        let y = ojas_rule(&mut weights, &s.view(), OJA_LEARNING_RATE);

        ty.send(y).map_err(|err| format!("{}", err))?;
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
/// ## Rerurns
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
/// - `sampling_freq` The sampling frequency.
/// - `filter_order` The order of the filter applied.
///
/// ## Returns
///
/// The function never returns unless an error occurs or `should_stop` is set to `true`.
/// The values are instead returned through the output channel.
pub fn highpass(
    rx: &Receiver<f64>,
    ty: Sender<f64>,
    should_stop: &AtomicBool,
    cutoff_freq: f64,
    sampling_freq: f64,
    filter_order: usize,
) -> Result<(), String> {
    // TODO: Might break if samples are missed. Investigate further.

    // Normalize the cutoff frequency by the Nyquist frequency
    let nyquist_freq = sampling_freq / 2f64;
    let cutoff = cutoff_freq / nyquist_freq;

    let (b, a) = butter(filter_order, cutoff, FilterType::Highpass).map_err(|err| format!("{}", err))?;

    // A and B will be length N + 1, where N is the filter order.
    // Therefore we need to store N y values from history
    // and to make life easier we push the current value of x into
    // history for our calculations.
    let mut y_prev: VecDeque<f64> = VecDeque::with_capacity(filter_order);
    let mut x_prev: VecDeque<f64> = VecDeque::with_capacity(filter_order + 1);

    while !should_stop.load(Ordering::Relaxed) {
        let x = rx.recv().map_err(|err| format!("{}", err))?;

        // Remove old values before pushing new to avoid reallocation
        if x_prev.len() >= filter_order + 1 {
            x_prev.pop_back().unwrap();
        }
        x_prev.push_front(x);

        let y = b
            .iter()
            .zip(x_prev.iter())
            .fold(0f64, |acc, (b, x)| acc + *b * *x)
            + a.iter()
                .skip(1) // Skip the coefficient for y
                .zip(y_prev.iter())
                .fold(0f64, |acc, (a, y)| acc + *a + *y);

        // Remove old values before pushing new to avoid reallocation
        if y_prev.len() >= filter_order {
            y_prev.pop_back().unwrap();
        }
        y_prev.push_front(y);

        ty.send(y).map_err(|err| format!("{}", err))?
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
    ty: Sender<Vec<f64>>,
    should_stop: &AtomicBool,
    window_size: usize,
    hop_size: usize,
) -> Result<(), Box<dyn Error>> {
    // TODO: Make sure ty will be properly hung up if an error occurs.

    let mut x = VecDeque::new();
    let mut iter = 0usize..;

    // Window size being 0 will cause logical errors
    if window_size == 0 {
        return Err("Window size can not be 0.".into());
    }

    while !should_stop.load(Ordering::Relaxed) {
        let i = iter.next().unwrap(); // Will not return None

        // Recieve a value from the channel and insert it into the deque.
        x.push_back(rx.recv()?);

        // Make sure enough samples have arrived and that the window is big enough.
        if x.len() < window_size + 1 {
            continue;
        }

        // Remove now irrelevant values. The previous guard clause asserts that the deque is not empty.
        x.pop_front().unwrap();

        // Only do the FFT when the hop size has been reached.
        if i % hop_size != 0 {
            continue;
        }

        // Rearrange the contents of the deque into a single slice and pass it to the fourier iteration.
        let intensity_spectrum = stft_iteration(x.make_contiguous())?;

        ty.send(intensity_spectrum)?;
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
pub fn stft_iteration(x: &[f64]) -> Result<Vec<f64>, Box<dyn Error>> {
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
            .ok_or("Could not create a slice from `x_weigthed`.")?,
        Some(n),
    )?;

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
