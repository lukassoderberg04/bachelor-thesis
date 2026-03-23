use std::sync::{
    atomic::AtomicBool,
    Arc
};

fn main() {
    let should_stop = Arc::new(AtomicBool::new(false));

    let should_stop_stft = Arc::clone(&should_stop);

    
}

fn fetch_audio() {

}