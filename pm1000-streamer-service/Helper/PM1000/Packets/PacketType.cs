namespace pm1000_streamer_service.PM1000;

public enum PacketType
{
    Read,
    ReadResponse,
    Write,
    WriteResponse,
    Transfer,
    TransferResponse,
    StokesSnapshot
}
