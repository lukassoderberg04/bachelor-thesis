namespace pm1000_streamer_service;

/// <summary>
/// Logger used for logging different part of the application.
/// </summary>
public static class Logger
{
    public static void LogError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;

        Console.WriteLine($"{getTimestamp()} ERROR: {msg}");

        Console.ResetColor();
    }

    public static void LogWarning(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.WriteLine($"{getTimestamp()} WARNING: {msg}");

        Console.ResetColor();
    }

    public static void LogInfo(string msg)
    {
        Console.WriteLine($"{getTimestamp()} INFO: {msg}");
    }

    private static string getTimestamp()
    {
        return $"[{DateTime.Now.ToString("HH:mm:ss")}]";
    }
}