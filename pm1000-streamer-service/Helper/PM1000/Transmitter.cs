using FTD3XXWU_NET;

namespace pm1000_streamer_service.PM1000;

/// <summary>
/// The transmitter takes care of sending and recieving packages to the device.
/// </summary>
public class Transmitter
{
    /// <summary>
    /// Tries to send a packet using the FTDI open connection. Returns the packet if no timeout happened.
    /// </summary>
    public Packet? SendPacket(Packet packet)
    {
        var buffer = packet.GetBytes();

        if (!FtdiService.FlushPipe(FtdiService.READ_PIPE)) return null;

        if(!FtdiService.WriteToPipe(FtdiService.SEND_PIPE, buffer)) return null;

        /*
            * Check if data is avaible, else just check until timeout.
            * If avaible, read it and convert it to the correct packet.
            * Return the packet.
        */

        throw new NotImplementedException();
    }
}