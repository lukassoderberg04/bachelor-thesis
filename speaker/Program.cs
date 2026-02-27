using System;
using System.Threading;

class FrequencySweep
{
    static void Main(string[] args)
    {
        // === USER SETTINGS ===
        double startFrequency = 440;     // Hz
        double endFrequency = 4400;      // Hz
        int steps = 7;                 // Number of frequencies
        int toneDurationMs = 500;       // Tone duration (milliseconds)
        int silenceDurationMs = 10;    // Silence duration (milliseconds)
        bool useLogarithmicSpacing = false; // true = log, false = linear
        // =====================

        if (steps < 2)
        {
            Console.WriteLine("Steps must be at least 2.");
            return;
        }

        for (int i = 0; i < steps; i++)
        {
            double frequency;

            if (useLogarithmicSpacing)
            {
                double logStart = Math.Log10(startFrequency);
                double logEnd = Math.Log10(endFrequency);
                double logStep = (logEnd - logStart) / (steps - 1);

                double currentLog = logStart + i * logStep;
                frequency = Math.Pow(10, currentLog);
            }
            else
            {
                double linearStep = (endFrequency - startFrequency) / (steps - 1);
                frequency = startFrequency + i * linearStep;
            }

            // Clamp to valid Console.Beep range
            int beepFrequency = (int)Math.Clamp(frequency, 37, 32767);

            Console.WriteLine($"Playing: {beepFrequency} Hz");

            Console.Beep(beepFrequency, toneDurationMs);
            Thread.Sleep(silenceDurationMs);
        }

        Console.WriteLine("Done.");
    }
}