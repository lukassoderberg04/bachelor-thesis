use ndarray::prelude::*;
use scirs2::linalg as linalg;

fn main() {
    // TODO! Read data from stream

    // TODO! Convert the stokes vector into an audio sample
    
    //  TODO! Open output stream and send data
}

fn ojas_rule(weights: &mut Array1<f64>, normalised_stokes_vector: &Array1<f64>, learning_rate: f64) -> f64 {
    let y = weights.dot(normalised_stokes_vector);

    *weights = &*weights + learning_rate * y * (normalised_stokes_vector - &*weights * y);

    y
}
