mod signal_processing;
mod streaming;

use std::{
    net::Ipv4Addr, process::exit, sync::{
        Arc,
        atomic::{AtomicBool, Ordering},
        mpsc::{self, Receiver, Sender},
    }, thread::{self, JoinHandle}
};

use ndarray::prelude::*;

use crate::{
    signal_processing::{highpass, pca},
    streaming::{AudioUdpSender, StokesUdpListener},
};

// Constants
const FILTER_ORDER: usize = 4;
const TIME_UNITS: f64 = 1e-6; // Time is passed as microseconds

// TODO: Possibly make this more dynamic so that the program works even if that port is taken.
const STOKES_PORT: u16 = 5000;
const AUDIO_PORT: u16 = 5001;

// TODO: The following constants should probably be adjustable using arguments/config.
const OJA_LEARNING_RATE: f64 = 0.01;
const CUTOFF_FREQ: f64 = 20.0;

fn main() {
    // TODO: Read command line argument to allow for configuration

    // Set up a flag to signify when to halt operations
    let should_stop = Arc::new(AtomicBool::new(false));

    let should_stop_listener = Arc::clone(&should_stop);
    let should_stop_pca = Arc::clone(&should_stop);
    let should_stop_filter = Arc::clone(&should_stop);
    let should_stop_sender = Arc::clone(&should_stop);
    let should_stop_controller = Arc::clone(&should_stop);
    
    // Set handler for CTRL+C
    ctrlc::set_handler(move || {
        println!("Shutdown signal recieved. Exiting.");

        should_stop_controller.store(true, Ordering::SeqCst);
    }).expect("Error setting Ctrl-C handler");

    // Initiate channels for cross thread communication
    let (stokes_sender, stokes_reciever) = mpsc::channel();
    let (pca_sender, pca_reciever) = mpsc::channel();
    let (filter_sender, filter_reciever) = mpsc::channel();
    // let (spectrogram_sender, spectrogram_reciever) = mpsc::channel();

    // Initiate threads
    let listener_handle = thread::spawn(move || fetch_data(stokes_sender, &should_stop_listener));
    let pca_handle = thread::spawn(move || pca(&stokes_reciever, pca_sender, &should_stop_pca));
    let filter_handle = thread::spawn(move || {
        highpass::<{ FILTER_ORDER + 1 }>(
            &pca_reciever,
            filter_sender,
            &should_stop_filter,
            CUTOFF_FREQ,
            TIME_UNITS,
        )
    });
    let audio_sender_handle =
        thread::spawn(move || send_audio(&filter_reciever, &should_stop_sender));

    // Collect all thread handles into a vector for monitoring.
    let mut handles = Vec::new();

    handles.push(listener_handle);
    handles.push(pca_handle);
    handles.push(filter_handle);
    handles.push(audio_sender_handle);

    let mut exit_flag = 0;

    let mut handles: Vec<Option<JoinHandle<Result<(), String>>>> = handles
        .into_iter()
        .map(Some)
        .collect();

    // Monitor threads to make sure none finish early
    while !should_stop.load(Ordering::Relaxed) {
        for slot in &mut handles {
            // Take the handle out of the Option, leaving None in its place.
            // If already None (already joined), skip.
            let Some(handle) = slot else { continue };

            if !handle.is_finished() {
                continue;
            }

            // Move the handle out of the slot so we can call join()
            let result = slot.take().unwrap().join().expect("Failed to join thread.");

            if let Err(message) = result {
                println!("An error has occured: {}", message);
                exit_flag = 1;
            }

            should_stop.store(true, Ordering::SeqCst);
        }

        std::thread::sleep(std::time::Duration::from_secs(1));
    }

    exit(exit_flag)
}

/// Fetches data from the upstream UDP sender.
///
/// ## Parameters
///
/// - `tx` Sender for the output channel.
/// - `should_stop` Flag to signify when to halt operations.
///
/// ## Returns
///
/// This function does not return unless an error occurs or `should_stop` is set to `true`.
/// Data is instead sent through the `tx` channel.
fn fetch_data(tx: Sender<(u32, Array1<f64>)>, should_stop: &AtomicBool) -> Result<(), String> {
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
        tx.send((t, s))
            .map_err(|err| format!("Sender error: {}", err))?;
    }

    Ok(())
}

fn send_audio(rx: &Receiver<(u32, f64)>, should_stop: &AtomicBool) -> Result<(), String> {
    let sender = AudioUdpSender::bind((Ipv4Addr::LOCALHOST, 0), (Ipv4Addr::LOCALHOST, AUDIO_PORT))?;

    while !should_stop.load(Ordering::Relaxed) {
        let (timestamp, amplitude) = rx.recv().map_err(|f| f.to_string())?;

        sender.send(amplitude, timestamp)?
    }

    Ok(())
}
