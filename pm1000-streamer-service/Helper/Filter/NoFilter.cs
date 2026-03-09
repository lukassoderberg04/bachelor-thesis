using pm1000_streamer_service.PM1000;

namespace pm1000_streamer_service.Filter;

/// <summary>
/// A blank filter for occupying an empty filter.
/// </summary>
public class NoFilter : IFilter
{
    public static NoFilter Instance { get; } = new NoFilter();

    /// <summary>
    /// Returns 0.0f since this filter doesn't do anything.
    /// </summary>
    public AudioSnapshotPacket ProcessStokesPacket(StokesSnapshotPacket stokesPacket)
    {
        return AudioSnapshotPacket.Default();
    }
}