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
        var sendBuffer = packet.GetBytes();

        if (!FtdiService.FlushPipe(FtdiService.READ_PIPE)) return null;

        if (!FtdiService.WriteToPipe(FtdiService.SEND_PIPE, sendBuffer)) return null;

        var readBuffer = new byte[8];

        if (!FtdiService.ReadFromPipe(FtdiService.READ_PIPE, readBuffer, (UInt32)readBuffer.Length)) return null;

        Packet readPacket;

        switch (packet.GetPacketType())
        {
            case PacketType.Read:
                readPacket = new ReadResponsePacket(readBuffer);
                break;

            case PacketType.Write:
                readPacket = new WriteResponsePacket(readBuffer);
                break;

            default:
                readPacket = new ArbitraryPacket(readBuffer);
                break;
        }

        return readPacket;
    }
}