namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Contains all the useful register addresses for the PM1000.
/// </summary>
public static class Register
{
    public const UInt16 Base = 512; // Base address.

    public const UInt16 S0uWU = Base + 10; // Input power in µW, integer part.
    public const UInt16 S0uWL = Base + 11; // Input power in µW, fractional part. Updated at last read of S0uWU.

    public const UInt16 S1uWU = Base + 12; // S1 of Stokes vector normalized to 1 µW, integer part. Offset=2^15.
    public const UInt16 S1uWL = Base + 13; // S1 of Stokes vector normalized to 1 µW, fractional part. Updated at last read of S1uWU.

    public const UInt16 S2uWU = Base + 14; // S2 of Stokes vector normalized to 1 µW, integer part. Offset=2^15.
    public const UInt16 S2uWL = Base + 15; // S2 of Stokes vector normalized to 1 µW, fractional part. Updated at last read of S2uWU.

    public const UInt16 S3uWU = Base + 16; // S3 of Stokes vector normalized to 1 µW, integer part. Offset=2^15.
    public const UInt16 S3uWL = Base + 17; // S3 of Stokes vector normalized to 1 µW, fractional part. Updated at last read of S3uWU.

    public const UInt16 DOPSt = Base + 24; // Degree of polarization (DOP), 16 bit unsigned, 15 fractional bits.
}