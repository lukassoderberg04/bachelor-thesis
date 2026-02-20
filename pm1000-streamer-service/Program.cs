using NAudio.Wave;
using pm1000_streamer_service.API;
using pm1000_streamer_service.PM1000;
using System;
using System.Linq;

var waveIn = new WaveInEvent
{
    DeviceNumber = 0, 
    WaveFormat = new WaveFormat(44100, 16, 1), // 44.1kHz, 16-bit, mono
    BufferMilliseconds = 10 //hur länge den samlar data innan den itererar igenom och skickar
};

waveIn.DataAvailable += WaveIn_DataAvailable;
waveIn.StartRecording();

Console.WriteLine("Recording... Press ENTER to stop.");
Console.ReadLine();

waveIn.StopRecording();
waveIn.Dispose();

void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
{
    short[] samples = new short[e.Buffer.Length / 2];
    Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.Buffer.Length);

    float topValue = samples.Max() / 32768f;
    int barLength = (int)(topValue * 50);
    string bar = new string('#', barLength);
    Console.CursorLeft = 0;
    Console.Write($"[{bar.PadRight(50, '-')}] {topValue:0.000}");


    for (int i = 0; i < samples.Length; i += 1)
    {
        float fraction = samples[i] / 32768f;
        DataProvider.AudioPacket = new AudioSnapshotPacket(fraction, (uint)DateTime.Now.Ticks);

    }
}