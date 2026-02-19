namespace pm1000_streamer_service.API;

/// <summary>
/// Will serve the stokes parameters and the DOP.
/// Port 5000 --> serves stokes vectors: 5 * float32 + 1 UInt32 (S0, S1, S2, S3, DOP, TIME) aka 24 bytes.
/// </summary>
public static class StokesStreamer
{
    public static readonly int STOKES_PORT = 5000;
}
