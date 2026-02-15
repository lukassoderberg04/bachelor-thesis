namespace pm1000_streamer_service.PM1000;

/// <summary>
/// The transmitter takes care of sending packages to the device.
/// </summary>
public class Transmitter
{
    /// <summary>
    /// Tries to send a packet using the FTDI open connection. Returns true if successful.
    /// </summary>
    public bool SendPacket(Packet packet)
    {
        var buffer = packet.GetBytes();

        var success = FtdiService.WriteToPipe(FtdiService.SEND_PIPE, buffer);

        return success;
    }
}