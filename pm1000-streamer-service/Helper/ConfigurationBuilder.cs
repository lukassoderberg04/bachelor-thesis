using FTD3XXWU_NET;

namespace pm1000_streamer_service;

/// <summary>
/// Builder class for building a chip configuration for a FTDI device.
/// </summary>
public class ConfigurationBuilder
{
    private FTDI.FT_60XCONFIGURATION config;

    public ConfigurationBuilder(FTDI.FT_60XCONFIGURATION config)
    {
        this.config = config;
    }

    public ConfigurationBuilder()
    {
        FTDI.FT_60XCONFIGURATION cfg = new();

        FtdiService.GetConfiguration(cfg);

        this.config = cfg;
    }

    /// <summary>
    /// Sets notification to either on (true) or off (false) on the read pipe.
    /// </summary>
    public ConfigurationBuilder SetNotification(bool readPipeOn)
    {
        if (readPipeOn)
        {
            config.OptionalFeatureSupport |= (1 << 2);

            return this;
        }

        // Same as ~(1 << 2)!
        config.OptionalFeatureSupport &= 0xFFFB;

        return this;
    }

    /// <summary>
    /// Gets the config object!
    /// </summary>
    public FTDI.FT_60XCONFIGURATION GetConfiguration()
    {
        return config;
    }
}
