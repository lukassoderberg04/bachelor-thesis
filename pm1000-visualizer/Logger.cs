namespace pm1000_visualizer;

/// <summary>
/// Simple logger for console output with timestamps and color coding.
/// </summary>
public static class Logger
{
    public static void LogError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"{GetTimestamp()} ERROR: {msg}");
        Console.ResetColor();
    }

    public static void LogWarning(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{GetTimestamp()} WARNING: {msg}");
        Console.ResetColor();
    }

    public static void LogInfo(string msg)
    {
        Console.WriteLine($"{GetTimestamp()} INFO: {msg}");
    }

    private static string GetTimestamp() => $"[{DateTime.Now:HH:mm:ss}]";
}
