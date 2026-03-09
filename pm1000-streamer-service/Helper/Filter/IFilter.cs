using pm1000_streamer_service.PM1000;

namespace pm1000_streamer_service.Filter;

/// <summary>
/// A filter takes in a list of stokes vector and outputs an audio sample.
/// </summary>
public interface IFilter
{
    /// <summary>
    /// Process incoming stokes packet and output an audio magnitude.
    /// </summary>
    float ProcessStokesPacket(StokesSnapshotPacket stokesPacket);
}