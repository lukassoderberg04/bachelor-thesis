from pathlib import Path
import wave

import matplotlib.pyplot as plt
import numpy as np

root = Path(__file__).resolve().parent.parent


"""
    Reads a mono channel from a wave file and normalizes it.
"""
def load_wav(path: Path) -> tuple[np.ndarray, int]:
    with wave.open(str(path), "rb") as wf:
        channels = wf.getnchannels()
        fs = wf.getframerate()
        sample_width = wf.getsampwidth()
        raw_bytes = wf.readframes(wf.getnframes())

    if sample_width == 1:
        raw = np.frombuffer(raw_bytes, dtype=np.uint8).astype(np.float64)
        raw = raw - 128.0
    elif sample_width == 2:
        raw = np.frombuffer(raw_bytes, dtype=np.int16).astype(np.float64)
    elif sample_width == 4:
        raw = np.frombuffer(raw_bytes, dtype=np.int32).astype(np.float64)
    else:
        raise RuntimeError(f"Unsupported sample width: {sample_width}")

    x = raw.reshape(-1, channels)[:, 0]

    x = x - np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-12)
    return x, fs


"""
    Saves a normalized mono signal as int16 wave.
"""
def save_wav(path: Path, x: np.ndarray, fs: int) -> None:
    x = x / (np.max(np.abs(x)) + 1e-12)
    x_i16 = np.int16(np.clip(x, -1.0, 1.0) * 32767)
    with wave.open(str(path), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(fs)
        wf.writeframes(x_i16.tobytes())


def nmse(a: np.ndarray, b: np.ndarray) -> float:
    return float(np.mean((a - b) ** 2) / (np.mean(a**2) + 1e-12))


"""
    Plots two signals and marks overlap in green.
"""
def save_overlap_plot(path: Path, a: np.ndarray, b: np.ndarray, fs: int, label_a: str, label_b: str) -> None:
    n_show = min(len(a), fs * 3)
    t = np.arange(n_show) / fs
    a_plot = a[:n_show]
    b_plot = b[:n_show]
    tol = 0.03
    overlap_mask = np.abs(a_plot - b_plot) <= tol
    overlap_line = np.where(overlap_mask, 0.5 * (a_plot + b_plot), np.nan)

    plt.figure(figsize=(12, 4))
    plt.plot(t, a_plot, label=label_a, linewidth=1.0, color="blue")
    plt.plot(t, b_plot, label=label_b, linewidth=1.0, color="orange")
    plt.plot(t, overlap_line, linewidth=2.0, color="green", label="Overlap")
    plt.xlabel("Time (s)")
    plt.ylabel("Amplitude")
    plt.title(f"Overlap in time domain: {label_a} vs {label_b}")
    plt.legend()
    plt.tight_layout()
    plt.savefig(path, dpi=150)
    plt.close()


"""
    Saves a single time-domain signal plot.
"""
def save_single_time_plot(path: Path, x: np.ndarray, fs: int, title: str) -> None:
    n_show = min(len(x), fs * 3)
    t = np.arange(n_show) / fs
    x_plot = x[:n_show]

    plt.figure(figsize=(12, 3))
    plt.plot(t, x_plot, linewidth=1.0)
    plt.xlabel("Time (s)")
    plt.ylabel("Amplitude")
    plt.title(title)
    plt.tight_layout()
    plt.savefig(path, dpi=150)
    plt.close()


def save_combined_time_plot(path: Path, x_t: np.ndarray, y_t: np.ndarray, fs: int) -> None:
    n_show = min(len(x_t), len(y_t), fs * 3)
    t = np.arange(n_show) / fs

    plt.figure(figsize=(12, 4))
    plt.plot(t, x_t[:n_show], linewidth=1.0, label="PM1000 x(t)")
    plt.plot(t, y_t[:n_show], linewidth=1.0, label="Original y(t)")
    plt.xlabel("Time (s)")
    plt.ylabel("Amplitude")
    plt.title("x(t) and y(t) in time domain")
    plt.legend()
    plt.tight_layout()
    plt.savefig(path, dpi=150)
    plt.close()


"""
    Aligns one signal to a reference using cross-correlation lag.
"""
def align_to_reference(reference: np.ndarray, signal: np.ndarray) -> np.ndarray:
    corr = np.correlate(signal, reference, mode="full")
    lag = int(np.argmax(corr) - (len(reference) - 1))
    if lag > 0:
        signal = signal[lag:]
        signal = np.pad(signal, (0, lag))
    elif lag < 0:
        signal = np.pad(signal, (-lag, 0))[: len(signal)]
    return signal


"""
    Main pipeline: estimate H(f), reconstruct y(t), and save outputs.
"""
def main() -> None:
    # Root-level files: PM1000 signal is x(t), original signal is y(t).
    x_path = root / "x(t).wav"
    y_path = root / "y(t).wav"

    if not x_path.exists() or not y_path.exists():
        raise FileNotFoundError(f"Missing expected files: {x_path} and/or {y_path}")

    out = Path(__file__).resolve().parent / "output"
    out.mkdir(parents=True, exist_ok=True)

    x_t, fs_x = load_wav(x_path)
    y_t, fs_y = load_wav(y_path)
    if fs_x != fs_y:
        raise RuntimeError(f"Sampling mismatch: {fs_x} vs {fs_y}")

    n = min(len(x_t), len(y_t))
    x_t = x_t[:n]
    y_t = y_t[:n]

    # Frequency-domain notation: X(f)=FFT{x(t)}, Y(f)=FFT{y(t)}.
    X_f = np.fft.rfft(x_t)
    Y_f = np.fft.rfft(y_t)
    a = 1e-4
    H_f = (np.conj(X_f) * Y_f) / (np.abs(X_f) ** 2 + a)

    # Forward reconstruction of original from PM1000: Y_reconstructed(f) = X(f)H(f)
    y_reconstructed_t = np.fft.irfft(X_f * H_f, n=n)
    y_reconstructed_t = align_to_reference(y_t, y_reconstructed_t)

    save_wav(out / "reconstructed_y_t_from_x_t.wav", y_reconstructed_t, fs_x)
    save_single_time_plot(out / "x_t_pm1000.png", x_t, fs_x, "PM1000 signal x(t)")
    save_single_time_plot(out / "y_t_original.png", y_t, fs_x, "Original signal y(t)")
    save_combined_time_plot(out / "x_t_and_y_t.png", x_t, y_t, fs_x)
    save_overlap_plot(
        out / "y_t_vs_reconstructed_y_t.png",
        y_t,
        y_reconstructed_t,
        fs_x,
        "Original y(t)",
        "Reconstructed y(t)",
    )

    print("Done")
    print(f"Input PM1000 x(t): {x_path}")
    print(f"Input original y(t): {y_path}")
    print(f"NMSE(y(t), reconstructed y(t)) = {nmse(y_t, y_reconstructed_t):.6f}")
    print(f"Saved: {out / 'reconstructed_y_t_from_x_t.wav'}")
    print(f"Saved: {out / 'x_t_pm1000.png'}")
    print(f"Saved: {out / 'y_t_original.png'}")
    print(f"Saved: {out / 'x_t_and_y_t.png'}")
    print(f"Saved: {out / 'y_t_vs_reconstructed_y_t.png'}")


if __name__ == "__main__":
    main()
