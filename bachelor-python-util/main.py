"""
    The main script that utilizes the BachelorPythonUtilLib for
    generating and displaying figures for the bachelor thesis
    project.
"""

from pathlib import Path
import numpy as np
import matplotlib.pyplot as plt
from BachelorPythonUtilLib.File import WaveFileReader, WaveFileWriter

"""
==================================================================================
"""

activePlot = "filter_and_save_speech"

"""
==================================================================================
"""

def filterAndSaveSpeech():
    file_path = Path(__file__).parent / "files" / "group_talking.wav"
    with WaveFileReader(file_path) as reader:
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
    "filter_and_save_speech": lambda: filterAndSaveSpeech()
}

# If the plot exist in the plots... run the lambda function for that specific plot.
if activePlot in plots:
    plots[activePlot]()

"""
==================================================================================
"""