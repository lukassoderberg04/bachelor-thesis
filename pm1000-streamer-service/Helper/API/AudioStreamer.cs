namespace pm1000_streamer_service.API;

/// <summary>
/// Will serve the audio as an amplitude together with time.
/// Port 5001 --> serves audio: 1 * float32 + 1 * UInt32 (AMPLITUDE, TIME) aka 8 bytes.
/// </summary>
public static class AudioStreamer
{
    public static readonly int AUDIO_PORT = 5001;
}