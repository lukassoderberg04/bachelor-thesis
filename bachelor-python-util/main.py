"""
    The main script that utilizes the BachelorPythonUtilLib for
    generating and displaying figures for the bachelor thesis
    project.
"""

from pathlib import Path
import numpy as np
import matplotlib.pyplot as plt
from BachelorPythonUtilLib.File import WaveFileReader, WaveFileWriter, PM1000ResultFileReader, PM1000Measurment

"""
==================================================================================
"""

activePlot = "read_stokes_file_and_plot_FFT"

"""
==================================================================================
"""

def readStokesFileAndPlotFFT():
    filePath = Path(__file__).parent / "files" / "measurments" / "spool-long-speaker-on-side-air-200-to-2200-2026-03-23.txt"
    polarimeterSampleRate = 48800

    samples: list[PM1000Measurment] = []

    with PM1000ResultFileReader(filePath) as reader:
        samples = reader.GetAllSamples()

    s1Samples: list[int] = []

    for sample in samples:
        s1Samples.append(sample.GetS1())

    # Use fourier transform to get all samples.
    fftValues = np.fft.fft(s1Samples)
    frequencies = np.fft.fftfreq(len(s1Samples), 1 / polarimeterSampleRate)

    magnitudes = np.abs(fftValues)

    plt.title("Frekvensspektrum från en grupp som pratar")
    plt.xlabel("Frekvens (Hz)")
    plt.ylabel("Amplitud")
    
    plt.plot(frequencies, magnitudes)
    plt.show()

def filterAndSaveSpeech():
    filePath = Path(__file__).parent / "files" / "group_talking.wav"
    with WaveFileReader(filePath) as reader:
        samples = reader.ReadAllSamplesFromFirstChannel()
        sampleRate = reader.GetSamplingFrequency()

    # Use fourier transform to get all samples.
    fftValues = np.fft.fft(samples)
    frequencies = np.fft.fftfreq(len(samples), 1 / sampleRate)

    # Create a mask function that is 1 when 0 <= frequency <= 3000, else 0. 
    lowCut = 200
    highCut = 5000
    mask = (np.abs(frequencies) >= lowCut) & (np.abs(frequencies) <= highCut)
    
    # Apply the mask for each value.
    filteredFft = fftValues * mask

    # Do the inverse fourier transform to get back the audio sample.
    filteredSamples = np.fft.ifft(filteredFft).real
    
    # Make sure that, if there's a DC constant current that amps the audio, make sure
    # we center at the top of that DC so that it doesn't bother us.
    filteredSamples = filteredSamples - np.mean(filteredSamples)

    # Save the new wave file.
    outputPath = Path(__file__).parent / "files" / "filtered_talking.wav"
    with WaveFileWriter(outputPath, sampleRate, channels=1, sampleWidth=2) as writer:
        writer.WriteSamples(filteredSamples)

def plotFrequencyFromPeopleTalking():
    samples: np.array = []
    frequency         = 1000

    with (WaveFileReader(Path(__file__).parent / "files" / "group_talking.wav") as reader):
        samples   = reader.ReadAllSamplesFromFirstChannel()
        frequency = reader.GetSamplingFrequency()

    timeline = np.linspace(0, 1, frequency, False)

    fftValues   = np.fft.fft(samples)
    frequencies = np.fft.fftfreq(len(samples), 1 / frequency)

    magnitudes = np.abs(fftValues)

    plt.title("Frekvensspektrum från en grupp som pratar")
    plt.xlabel("Frekvens (Hz)")
    plt.ylabel("Amplitud")
    
    plt.plot(frequencies, magnitudes)
    plt.show()

"""
==================================================================================
"""

"""
    Contains all the different plots that can be generated in this file.
"""
plots = {
    "frequency_from_people_talking": lambda: plotFrequencyFromPeopleTalking(),
    "filter_and_save_speech": lambda: filterAndSaveSpeech(),
    "read_stokes_file_and_plot_FFT": lambda: readStokesFileAndPlotFFT()
}

# If the plot exist in the plots... run the lambda function for that specific plot.
if activePlot in plots:
    plots[activePlot]()

"""
==================================================================================
"""