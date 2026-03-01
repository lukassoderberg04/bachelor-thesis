mod signal_processing;
mod streaming;

use std::{
    net::Ipv4Addr,
    sync::{
        Arc,
        atomic::{AtomicBool, Ordering},
        mpsc::{self, Sender},
    },
    thread::{self},
};

use ndarray::prelude::*;

use crate::{
    signal_processing::{highpass, pca},
    streaming::StokesUdpListener,
};

// Constants
const FILTER_ORDER: usize = 4;

// TODO: Possibly make this more dynamic so that the program works even if that port is taken.
const STOKES_PORT: u16 = 5000;

// TODO: The following constants should probably be adjustable using arguments/config.
const OJA_LEARNING_RATE: f64 = 0.01;
const CUTOFF_FREQ: f64 = 20.0;
const SAMPLING_FREQ: f64 = 1525.88;

fn main() {
    // TODO: Read data from stream

    // TODO: Implement proper error handling once functionality is verified

    // TODO: Replace Box<dyn Error> with concrete types in error handling

    let mut controllers = Vec::new();

    let should_stop_listener = Arc::new(AtomicBool::new(false));
    controllers.push(Arc::clone(&should_stop_listener));

    let should_stop_pca = Arc::new(AtomicBool::new(false));
    controllers.push(Arc::clone(&should_stop_pca));

    let should_stop_filter = Arc::new(AtomicBool::new(false));
    controllers.push(Arc::clone(&should_stop_filter));

    let (stokes_sender, stokes_reciever) = mpsc::channel();
    let (timestamp_sender, timestamp_reciever) = mpsc::channel();
    let (pca_sender, pca_reciever) = mpsc::channel();
    let (filter_sender, filter_reciever) = mpsc::channel();

    let mut handles = Vec::new();

    let listener_handle =
        thread::spawn(move || fetch_data(stokes_sender, timestamp_sender, &should_stop_listener));
    handles.push(listener_handle);

    let pca_handle = thread::spawn(move || pca(&stokes_reciever, pca_sender, &should_stop_pca));
    handles.push(pca_handle);

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
    handles.push(filter_handle);

    let stft_handle = thread::spawn(move || todo!());
    let sender_handle = thread::spawn(move || todo!());

    // TODO: Stream data onward

    // // Convert the extracted amplitudes (f64) to f32 audio samples.
    // let audio_samples: Vec<f32> = hp_amplitudes.iter().map(|&x| x as f32).collect();

    // // Send the block to the visualizer on port 5001.
    // // In the streaming integration, call send_block() once per processing window
    // // instead of once for the whole recording — see Documents/integration-guide.md.
    // let mut sender =
    //     AudioUdpSender::new("127.0.0.1", sampling_rate as u32).expect("Failed to bind UDP socket");
    // sender
    //     .send_block(&audio_samples)
    //     .expect("Failed to send audio UDP packet");
}

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

        ts.send(s).map_err(|err| format!("Sender error: {}", err))?;
        tt.send(t).map_err(|err| format!("Sender error: {}", err))?;
    }

    Ok(())
}
