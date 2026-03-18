using pm1000_streamer_service.PM1000;

namespace pm1000_streamer_service.Filter;

/// <summary>
/// Uses the derivatives of the stokes vector to capture the sound.
/// </summary>
public class DerivativeFilter : IFilter
{
    public static DerivativeFilter Instance { get; } = new DerivativeFilter();

    private StokesSnapshotPacket lastPacket;

    public DerivativeFilter()
    {
        lastPacket = StokesSnapshotPacket.Default();
    }

    /// <summary>
    /// Calculates the derivatives for each and every stokes vector and
    /// combines the derivatives to get a "total" amount of change.
    /// </summary>
    public AudioSnapshotPacket ProcessStokesPacket(StokesSnapshotPacket stokesPacket)
    {
        var s1  = stokesPacket.S1;
        var s2  = stokesPacket.S2;
        var s3  = stokesPacket.S3;
        var now = stokesPacket.Time;

        var s1Old = lastPacket.S1;
        var s2Old = lastPacket.S2;
        var s3Old = lastPacket.S3;
        var then  = lastPacket.Time;

        float deltaTime = now - then;

        if (deltaTime <= 0) return AudioSnapshotPacket.Default();

        float s1Derivative = (s1 - s1Old) / deltaTime;
        float s2Derivative = (s2 - s2Old) / deltaTime;
        float s3Derivative = (s3 - s3Old) / deltaTime;

        float magnitude = MathF.Sqrt(s1Derivative * s1Derivative + s2Derivative * s2Derivative + s3Derivative * s3Derivative);

        return new AudioSnapshotPacket(magnitude);
    }
}
