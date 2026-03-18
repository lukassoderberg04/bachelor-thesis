# Simple FFT Filter

Minimal system identification using two WAV files:

- PM1000 / measured (x): x(t).wav
- Original (y): y(t).wav

Formula:

- X(f) = FFT{x(t)}
- Y(f) = FFT{y(t)}
- H(f) = Y(f) / X(f)
- Y_hat(f) = X(f) \* H(f)

Run:

python convolution-analysis/simple_fft_filter.py

Outputs (in convolution-analysis/output):

- reconstructed_y_t_from_x_t.wav
- x_t_pm1000.png
- y_t_original.png
- x_t_and_y_t.png
- y_t_vs_reconstructed_y_t.png
