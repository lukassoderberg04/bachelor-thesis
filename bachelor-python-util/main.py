"""
    The main script that utilizes the BachelorPythonUtilLib for
    generating and displaying figures for the bachelor thesis
    project.
"""

from pathlib import Path
import numpy as np
import matplotlib.pyplot as plt
from BachelorPythonUtilLib.File import WaveFileReader

"""
==================================================================================
"""

activePlot = "frequency_from_people_talking"

"""
==================================================================================
"""

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
    "frequency_from_people_talking": lambda: plotFrequencyFromPeopleTalking()
}

# If the plot exist in the plots... run the lambda function for that specific plot.
if activePlot in plots:
    plots[activePlot]()

"""
==================================================================================
"""