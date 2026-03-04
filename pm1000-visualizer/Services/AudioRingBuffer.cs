namespace pm1000_visualizer.Services;

/// <summary>
/// Fixed-capacity ring buffer for streaming audio samples.
/// Thread-safe: background UDP thread writes, UI timer reads snapshots.
/// </summary>
public sealed class AudioRingBuffer
{
    private readonly float[] _buffer;
    private int _writePos;
    private int _count;
    private long _totalWritten;
    private readonly object _lock = new();

    public AudioRingBuffer(int capacity)
    {
        _buffer = new float[capacity];
    }

    public long TotalWritten
    {
        get { lock (_lock) return _totalWritten; }
    }

    /// <summary>Write a batch of samples into the ring buffer.</summary>
    public void Write(float[] samples)
    {
        lock (_lock)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                _buffer[_writePos] = samples[i];
                _writePos = (_writePos + 1) % _buffer.Length;
            }
            _count = Math.Min(_count + samples.Length, _buffer.Length);
            _totalWritten += samples.Length;
        }
    }

    /// <summary>
    /// Copies the most recent <paramref name="maxSamples"/> into a new double array.
    /// Returns the snapshot and the total number of samples written since creation.
    /// </summary>
    public (double[] samples, long total) Snapshot(int maxSamples)
    {
        lock (_lock)
        {
            int n = Math.Min(maxSamples, _count);
            var result = new double[n];
            int readStart = (_writePos - n + _buffer.Length) % _buffer.Length;
            for (int i = 0; i < n; i++)
                result[i] = _buffer[(readStart + i) % _buffer.Length];
            return (result, _totalWritten);
        }
    }
}
