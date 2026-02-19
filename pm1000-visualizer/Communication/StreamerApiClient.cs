using System.Net.Http;
using System.Net.Http.Json;

namespace pm1000_visualizer.Communication;

/// <summary>
/// REST client for talking to Lukas's pm1000-streamer-service API.
///
/// Base URL: http://{streamerHost}:5002
///
/// Available endpoints:
///   GET  /frequency   → returns { "hz": 193414000000.0 }  (laser light frequency)
///   GET  /samplerate  → returns { "hz": 16000 }
///   POST /samplerate  → body: { "hz": 16000 }  (set sampling rate from GUI)
/// </summary>
public class StreamerApiClient
{
    public const int API_PORT = 5002;

    private readonly HttpClient _http;

    public StreamerApiClient(string streamerHost = "127.0.0.1")
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri($"http://{streamerHost}:{API_PORT}"),
            Timeout = TimeSpan.FromSeconds(3)
        };
    }

    /// <summary>
    /// Gets the laser light frequency in Hz from the streamer service.
    /// Returns null if the request fails.
    /// </summary>
    public async Task<double?> GetFrequencyAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<FrequencyResponse>("/frequency");
            return response?.Hz;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get frequency: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the current sampling rate in Hz from the streamer service.
    /// Returns null if the request fails.
    /// </summary>
    public async Task<uint?> GetSampleRateAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<SampleRateResponse>("/samplerate");
            return response?.Hz;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get sample rate: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sends a new sampling rate to the streamer service.
    /// Returns true if accepted.
    /// </summary>
    public async Task<bool> SetSampleRateAsync(uint hz)
    {
        try
        {
            var result = await _http.PostAsJsonAsync("/samplerate", new SampleRateResponse(hz));
            return result.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to set sample rate: {ex.Message}");
            return false;
        }
    }

    private record FrequencyResponse(double Hz);
    private record SampleRateResponse(uint Hz);
}
