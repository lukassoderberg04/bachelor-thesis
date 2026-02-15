namespace pm1000_streamer_service.PM1000;

/// <summary>
/// The contract that all packets need to fullfill.
/// </summary>
public interface IPacket
{
    /// <summary>
    /// Should return the packets size in bytes.
    /// </summary>
    int GetPacketSizeInBytes();

    /// <summary>
    /// Should return the bytes that is the packet.
    /// </summary>
    byte[] GetBytes();
}