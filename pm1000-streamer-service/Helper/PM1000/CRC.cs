namespace pm1000_streamer_service.PM1000;

/// <summary>
/// Utility class, specifically for the PM1000 device, that is used for
/// calculating the CRC - Cycle Redundancy Check.
/// </summary>
public static class CRC
{
    public static readonly UInt16 CRC_OK = 52428;

    /// <summary>
    /// Calculates the redundancy check by going from the left to right in the packet array.
    /// The length just determins how many UInt16 values the CRC will cover.
    /// </summary>
    public static UInt16 CalculateRedundancyCheck(UInt16[] packet, int length)
    {
        UInt16 crc = 0xFFFF; // Cyclic redundency check!

        for (int i = 0; i < length; i++)
        {
            crc = (UInt16)((crc << 1) | (crc >> 15));

            crc ^= packet[i];
        }

        return crc;
    }
}
