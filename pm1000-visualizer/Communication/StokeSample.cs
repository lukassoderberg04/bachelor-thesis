namespace pm1000_visualizer.Communication;

/// <summary>
/// One measurement sample from the PM1000 polarimeter.
/// S1–S3 are normalized −1…+1. DOP (degree of polarization) is 0…1.
/// </summary>
public record struct StokeSample(float S0, float S1, float S2, float S3, float Dop);

