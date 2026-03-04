using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using pm1000_visualizer;

namespace pm1000_visualizer.Communication;

/// <summary>
/// Listens for incoming UDP data on three ports and fires typed events.
///
///   Port 5000 → Stokes data        (from streamer-service)
///   Port 5001 → Raw audio          (from streamer-service microphone)
///   Port 5002 → Processed audio    (from signal-processing)
///
/// All ports are configurable via the constructor.
/// </summary>
public class UdpListener : IDisposable
{
    public int StokesPort { get; }
    public int RawAudioPort { get; }
    public int ProcessedAudioPort { get; }

    private UdpClient? _stokesClient;
    private UdpClient? _rawAudioClient;
    private UdpClient? _processedAudioClient;
    private bool _running;

    public event Action<StokesPacket>? StokesReceived;
    public event Action<AudioPacket>? RawAudioReceived;
    public event Action<AudioPacket>? ProcessedAudioReceived;
    public event Action<uint, uint>? PacketDropped; // (expected, got)

    private uint _lastStokesSeq = uint.MaxValue;
    private uint _lastRawAudioSeq = uint.MaxValue;
    private uint _lastProcessedAudioSeq = uint.MaxValue;

    // Synthetic sequence counters for the streamer-service raw format (no header).
    private uint _streamerStokesSeq;
    private uint _streamerRawAudioSeq;

    // ── Streamer-service deduplication / downsampling ───────────────────
    // The streamer sends the same sample in a tight spin-loop, producing
    // thousands of duplicate packets per unique value.  For audio we
    // downsample to exactly 8000 Hz using wall-clock timing; for stokes
    // we deduplicate by the packet timestamp.
    private uint _lastStreamerStokesTime = uint.MaxValue;
    private readonly Stopwatch _rawAudioClock = new();
    private long _rawAudioEmitted;
    private float _latestRawNormalized;

    public UdpListener(int stokesPort = 5000, int rawAudioPort = 5001, int processedAudioPort = 5002)
    {
        StokesPort = stokesPort;
        RawAudioPort = rawAudioPort;
        ProcessedAudioPort = processedAudioPort;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;

        _stokesClient = new UdpClient(new IPEndPoint(IPAddress.Any, StokesPort));
        _rawAudioClient = new UdpClient(new IPEndPoint(IPAddress.Any, RawAudioPort));
        _processedAudioClient = new UdpClient(new IPEndPoint(IPAddress.Any, ProcessedAudioPort));

        Task.Run(() => ReceiveLoop(_stokesClient, Channel.Stokes));
        Task.Run(() => ReceiveLoop(_rawAudioClient, Channel.RawAudio));
        Task.Run(() => ReceiveLoop(_processedAudioClient, Channel.ProcessedAudio));

        Logger.LogInfo($"UDP listener started (Stokes:{StokesPort}, RawAudio:{RawAudioPort}, ProcessedAudio:{ProcessedAudioPort}).");
    }

    public void Stop()
    {
        _running = false;
        _stokesClient?.Close();
        _rawAudioClient?.Close();
        _processedAudioClient?.Close();
        Logger.LogInfo("UDP listener stopped.");
    }

    private enum Channel { Stokes, RawAudio, ProcessedAudio }

    private async Task ReceiveLoop(UdpClient client, Channel channel)
    {
        while (_running)
        {
            try
            {
                var data = (await client.ReceiveAsync()).Buffer;

                switch (channel)
                {
                    case Channel.Stokes:
                        {
                            if (data.Length == PacketDeserializer.STREAMER_STOKES_SIZE)
                            {
                                // Streamer sends the same stokes in a tight loop.
                                // Deduplicate by timestamp (bytes 20-23).
                                uint ts = BitConverter.ToUInt32(data, 20);
                                if (ts == _lastStreamerStokesTime) continue;
                                _lastStreamerStokesTime = ts;

                                var sample = PacketDeserializer.TryDeserializeStreamerStokes(data);
                                if (sample == null)
                                {
                                    Logger.LogError($"[Stokes] Failed to deserialize streamer raw format packet ({data.Length} bytes)");
                                    continue;
                                }
                                var pkt = new StokesPacket(_streamerStokesSeq++, 0, new[] { sample.Value });
                                StokesReceived?.Invoke(pkt);
                            }
                            else
                            {
                                // Standard header+payload format.
                                var pkt = PacketDeserializer.TryDeserializeStokes(data);
                                if (pkt == null)
                                {
                                    Logger.LogError($"[Stokes] Failed to deserialize standard format packet ({data.Length} bytes, expected >= {PacketDeserializer.HEADER_SIZE})");
                                    continue;
                                }
                                CheckSequence(pkt.SequenceNr, ref _lastStokesSeq);
                                StokesReceived?.Invoke(pkt);
                            }
                            break;
                        }
                    case Channel.RawAudio:
                        {
                            if (data.Length == PacketDeserializer.STREAMER_AUDIO_SIZE)
                            {
                                // Streamer sends the same audio sample in a tight loop.
                                // Down-sample to exactly 8000 Hz using wall-clock timing
                                // so we store only ~80 000 samples per 10 s instead of ~575 000+.
                                var amplitude = PacketDeserializer.TryDeserializeStreamerAudio(data);
                                if (amplitude == null) continue;
                                _latestRawNormalized = amplitude.Value / 32768f;

                                if (!_rawAudioClock.IsRunning) _rawAudioClock.Start();
                                long target = (long)(_rawAudioClock.Elapsed.TotalSeconds * 8000);
                                if (_rawAudioEmitted >= target) continue; // not time yet

                                var pkt = new AudioPacket(_streamerRawAudioSeq++, 8000,
                                                          new[] { _latestRawNormalized });
                                RawAudioReceived?.Invoke(pkt);
                                _rawAudioEmitted++;
                            }
                            else
                            {
                                // Standard header+payload format.
                                var pkt = PacketDeserializer.TryDeserializeAudio(data);
                                if (pkt == null) continue;
                                CheckSequence(pkt.SequenceNr, ref _lastRawAudioSeq);
                                RawAudioReceived?.Invoke(pkt);
                            }
                            break;
                        }
                    case Channel.ProcessedAudio:
                        {
                            var pkt = PacketDeserializer.TryDeserializeAudio(data);
                            if (pkt == null) continue;
                            CheckSequence(pkt.SequenceNr, ref _lastProcessedAudioSeq);
                            ProcessedAudioReceived?.Invoke(pkt);
                            break;
                        }
                }

            }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { Logger.LogError($"UDP receive error ({channel}): {ex.Message}"); }
        }
    }

    private void CheckSequence(uint received, ref uint last)
    {
        if (last != uint.MaxValue && received != last + 1)
            PacketDropped?.Invoke(last + 1, received);
        last = received;
    }

    public void Dispose() => Stop();
}
