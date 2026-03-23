mod signal_processing;
mod streaming;

use std::{
    net::Ipv4Addr,
    sync::{
        Arc,
        atomic::{AtomicBool, Ordering},
        mpsc::{self, Receiver, Sender},
    },
    thread::{self},
};

use ndarray::prelude::*;

use crate::{
    signal_processing::{highpass, pca, stft},
    streaming::{AudioUdpSender, StokesUdpListener},
};

// Constants
const FILTER_ORDER: usize = 4;

// TODO: Possibly make this more dynamic so that the program works even if that port is taken.
const STOKES_PORT: u16 = 5000;
const AUDIO_PORT: u16 = 5001;

// TODO: The following constants should probably be adjustable using arguments/config.
const OJA_LEARNING_RATE: f64 = 0.01;
const CUTOFF_FREQ: f64 = 20.0;
const SAMPLING_FREQ: f64 = 1525.88;
const SPECTROGRAM_WINDOW_SIZE: usize = 500; // How many samples to include in one iteration of the spectrogram. 
const SPECTROGRAM_RESOLUTION: usize = 250; // How many samples between each iteration of the spectrogram.

fn main() {
    // TODO: Read command line argument to allow for configuration

    // Set up flags to signify when to halt operations
    let should_stop_listener = Arc::new(AtomicBool::new(false));
    let should_stop_pca = Arc::new(AtomicBool::new(false));
    let should_stop_filter = Arc::new(AtomicBool::new(false));
    let should_stop_stft = Arc::new(AtomicBool::new(false));
    let should_stop_sender = Arc::new(AtomicBool::new(false));
    
    // Create a list of all flags
    let mut controllers = Vec::new();
    controllers.push(Arc::clone(&should_stop_listener));
    controllers.push(Arc::clone(&should_stop_pca));
    controllers.push(Arc::clone(&should_stop_filter));
    controllers.push(Arc::clone(&should_stop_stft));
    controllers.push(Arc::clone(&should_stop_sender));

    // Initiate channels for cross thread communication
    let (stokes_sender, stokes_reciever) = mpsc::channel();
    let (timestamp_sender, timestamp_reciever) = mpsc::channel();
    let (pca_sender, pca_reciever) = mpsc::channel();
    let (filter_sender, filter_reciever) = mpsc::channel();
    // let (spectrogram_sender, spectrogram_reciever) = mpsc::channel();

    // Initiate threads
    let listener_handle = thread::spawn(move || 
        fetch_data(stokes_sender, timestamp_sender, &should_stop_listener)
    );

    let pca_handle = thread::spawn(move || 
        pca(&stokes_reciever, pca_sender, &should_stop_pca)
    );

    let filter_handle = thread::spawn(move || {
        highpass(
            &pca_reciever,
            filter_sender,
            &should_stop_filter,
            CUTOFF_FREQ,
            SAMPLING_FREQ,
            FILTER_ORDER,
        )
    });

    // TODO: Split streams such that both the spectrogram feature and the audio sender can consume the same values
    // let stft_handle = thread::spawn(move || {
    //     stft(
    //         &filter_reciever,
    //         &timestamp_reciever,
    //         spectrogram_sender,
    //         &should_stop_stft,
    //         SPECTROGRAM_WINDOW_SIZE,
    //         SPECTROGRAM_RESOLUTION,
    //     )
    // });

    let audio_sender_handle = thread::spawn(move || 
        send_audio(
            &filter_reciever, 
            &timestamp_reciever, 
            &should_stop_sender,
        )
    );

    // Collect all thread handles into a vector for monitoring.
    let mut handles = Vec::new();
    
    handles.push(listener_handle);
    handles.push(pca_handle);
    handles.push(filter_handle);
    handles.push(audio_sender_handle);
    
}

/// Fetches data from the upstream UDP sender. 
/// 
/// ## Parameters
/// 
/// - `ts` Sender for the output channel for stokes vectors.
/// - `tt` Sender for the output channel for timestamps.
/// - `should_stop` Flag to signify when to halt operations.
/// 
/// ## Returns
/// 
/// This function does not return unless an error occurs or `should_stop` is set to `true`.
/// Data is instead sent through `ts` and `tt` channels.
fn fetch_data(
    ts: Sender<Array1<f64>>,
    tt: Sender<u32>,
    should_stop: &AtomicBool,
) -> Result<(), String> {
    let listener = StokesUdpListener::bind((Ipv4Addr::LOCALHOST, STOKES_PORT))
        .expect("Unable to bind to endpoint.");

    while !should_stop.load(Ordering::Relaxed) {
        // Read values from the stokes UDP stream.
        let result = listener.recv();

        let (t, s0, s1, s2, s3) = match result {
            Err(err) if &err[..] == "Didn't recieve data." => continue,
            Err(err) if err.starts_with("Incorrect byte amount.") => panic!("{}", err),
            Err(_) => unreachable!(),
            Ok(data) => data,
        };

        // Create a normalized stokes array
        let s = array![s1, s2, s3] / s0;

        // Send values downstream
        ts.send(s).map_err(|err| format!("Sender error: {}", err))?;
        tt.send(t).map_err(|err| format!("Sender error: {}", err))?;
    }

    Ok(())
}

fn send_audio(rx: &Receiver<f64>, rt: &Receiver<u32>, should_stop: &AtomicBool) -> Result<(), String> {
    todo!();

    Ok(())
}