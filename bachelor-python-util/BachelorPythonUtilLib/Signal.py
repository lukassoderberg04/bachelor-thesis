import numpy as np

def moving_average_pca(stokes_data: np.ndarray, fs: float, cuttoff_freq=20) -> np.ndarray:
    """
    Converts 3D stokes vector vibrations into a 1D scalar amplitude.

    ## Parameters
    - `stokes_data` NumPy array of shape (N, 3) of [S1, S2, S3]
    - `fs` Sampling frequency in Hz
    - `cutoff_freq` The frequency below which we consider signal to be "drift".
    """

    # Define the window size for the moving average (low pass)
    window_size = int(fs / cuttoff_freq)
    if window_size % 2 == 0: 
        window_size += 1

    # Calculate the moving average
    kernel = np.ones(window_size) / window_size
    s_ref = np.zeros_like(stokes_data)

    for i in range(3):
        s_ref[:, i] = np.convolve(stokes_data[:, i], kernel, mode='same')

    # Extract high frequency deviation
    delta_s = stokes_data - s_ref

    # Center the data (should already be near-zero mean, but doesn't hurt)
    delta_s -= delta_s.mean(axis=0) 

    principal_axis = oja_principal_axis(delta_s)
    scalar_signal = delta_s @ principal_axis

    return scalar_signal

def oja_principal_axis(delta_s: np.ndarray, learning_rate: float = 0.01, n_epochs: int = 1) -> np.ndarray:
    """
    Estimates the principal axis of `delta_s` using Oja's rule.

    ## Parameters

    - `delta_s` Variation from the moving average of the stokes vector.
    - `learning_rate` The learning rate for Oja's rule. Higher means faster fitting, 
    but increased unstability.
    - `n_epochs` Number of iterations to run the algorithm. Higher will help convergence, 
    but will increase time linearly with a slope of `len(delta_s)`.

    ## Returns

    Returns a unit vector with sign fixed by larges absolute component.
    """

    # Initialize from the first SVD estimate on a small warm-up window
    # This gives a better starting point than a random vector
    warmup = min(50, len(delta_s))
    _, _, Vt = np.linalg.svd(delta_s[:warmup], full_matrices=False)
    w = Vt[0].copy()

    for _ in range(n_epochs):
        for x in delta_s:
            y = np.dot(w, x)
            w += learning_rate * y * (x - y * w)

    # Fix sign ambiguity
    w *= np.sign(w[np.argmax(np.abs(w))])

    # Normalize w
    w /= np.linalg.norm(w)

    return w


