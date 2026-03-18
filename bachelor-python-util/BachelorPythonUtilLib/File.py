from pathlib import Path
from io import TextIOWrapper
from typing import Optional
import re as regex
import wave
import numpy

"""
    File module contains classes and functions that are important
    for retrieving and communicating with different file formats
    that are used by the bachelor thesis project.
"""

"""
    A class that can open a result file and retrieve the stokes vectors
    and also the attributes of the recording (from a PM1000 recording).
"""
class PM1000ResultFileReader:
    def __init__(self, filePath: Path, chunkSize: int = 1) -> "PM1000ResultFileReader":
        self._filePath                            = filePath
        self._chunkSize                           = chunkSize
        self._fileHandle: Optional[TextIOWrapper] = None
        self._attributes: dict[str, str]          = {}
        self._nextSampleIndex                     = 0

        if not self._filePath.exists():
            raise FileNotFoundError(f"The file in question wasn't found: {self._filePath}")
        
    """
        Returns the attributes after opening the file.
    """
    def GetAttributes(self) -> dict[str, str]:
        return self._attributes
    
    """
        Returns the index of the next sample index.
    """
    def GetNextSampleIndex(self) -> int:
        return self._nextSampleIndex

    """
        Parses the headers and fills in the attribute list.
    """
    def _parseHeader(self):
        r"""
            ^     - Start of line.
            \s*   - Match whitespaces (\s = whitespace, * = 0 or more).
            (\w+) - Create a group, which is defined by parenthesis, which contains words (\w = letter).
            =     - Just matches the equals token.
            ['"]? - The square bracket means match one of these tokens in it. The question marks just states optional.
            (.*?) - Match ANY token, like a joker. The question marks just states that it should stop when seeing the next pattern in the list.
            ;     - Match this token.
            $     - Marks end of line.
        """
        headerPattern: regex.Pattern = regex.compile(r"^#\s*(\w+)\s*=\s*['\"]?(.*?)['\"]?\s*;\s*$")

        # Read the first line and just throw it away.
        _ = self._fileHandle.readline()
        
        while True:
            lastPos = self._fileHandle.tell()
            line    = self._fileHandle.readline()

            if not line:
                break

            match   = headerPattern.match(line)

            if match: # If a match was found:
                key   = match.group(1)
                value = match.group(2)

                self._attributes[key] = value

            else: # If no match was found... data begins:
                self._fileHandle.seek(lastPos)
                break
    
    """
        Opens the designated file and parses the headers.
        Now ready to read stokes parameter chunk by chunk.
    """
    def Open(self):
        # Open a read handle to the file.
        self._fileHandle = self._filePath.open("r", encoding="utf-8")
        self._nextSampleIndex = 0

        self._parseHeader()

    """
        Gets the next list of samples from the file.
        Returns None if there's no more samples to read.
    """
    def GetNextSample(self) -> Optional["PM1000Measurment"]:
        if not self._fileHandle:
            raise RuntimeError("File must be open before reading!")
        
        line = self._fileHandle.readline()
        if not line or not line.strip():
            self.Close()
            return None
        
        values = [int(x.strip()) for x in line.split(',')]

        if not len(values) == 5:
            raise RuntimeError(f"File was missing a value when reading line at line: {self._fileHandle.tell()}")
        
        self._nextSampleIndex += 1

        return PM1000Measurment(values[0], values[1], values[2], values[3], values[4])
    
    """
        Returns all samples from a file as a list.
    """
    def GetAllSamples(self) -> list["PM1000Measurment"]:
        if not self._fileHandle:
            raise RuntimeError("File must be open before reading!")
        
        samples: list[PM1000Measurment] = []

        while True:
            sample = self.GetNextSample()

            if sample:
                samples.append(sample)
            else:
                return samples
    
    """
        Closes the handle to the file.
    """
    def Close(self):
        if self._fileHandle:
            self._fileHandle.close()
            self._fileHandle = None
    
    """
        If using 'with' on the class for reading,
        makes sure that on enter it opens the file handle.
    """
    def __enter__(self): 
        self.Open()
        return self
    
    """
        If using 'with' on the class for reading,
        makes sure that on leave it closes the file handle.
    """
    def __exit__(self, *args): 
        self.Close()

"""
    A class modeling the measurment retrieved from a file.
"""
class PM1000Measurment:
    def __init__(self, timestamp: int, s0: int, s1: int, s2: int, s3: int):
        self._timestamp = timestamp
        self._s0        = s0
        self._s1        = s1
        self._s2        = s2
        self._s3        = s3

    def GetTimestamp(self) -> int:
        return self._timestamp
    
    def GetS0(self) -> int:
        return self._s0
    
    def GetS1(self) -> int:
        return self._s1

    def GetS2(self) -> int:
        return self._s2

    def GetS3(self) -> int:
        return self._s3
    
"""
    The wave file reader reads the audio data from
    the files.
"""
class WaveFileReader:
    def __init__(self, filePath: Path):
        self._filePath = str(filePath)
        self._waveHandle: Optional[wave.Wave_read] = None

    """
        Opens the wave file to read from it.
    """
    def Open(self):
        self._waveHandle = wave.open(self._filePath, 'rb')

    """
        Closes the wave file.
    """
    def Close(self):
        self._waveHandle.close()

    """
        Reads all values from only the first channel and returns
        the array of values.
    """
    def ReadAllSamplesFromFirstChannel(self) -> numpy.array:
        if not self._waveHandle:
            raise RuntimeError("File must be open before reading!")

        channels = self._waveHandle.getnchannels()
        sampleWidth = self._waveHandle.getsampwidth()

        if sampleWidth == 1:
            dtype = numpy.uint8
        elif sampleWidth == 2:
            dtype = numpy.int16
        elif sampleWidth == 4:
            dtype = numpy.int32
        else:
            raise RuntimeError(f"The wave file reader doesn't support {sampleWidth * 8} bit files!")
        
        rawData = self._waveHandle.readframes(self._waveHandle.getnframes()) # A long array of bytes.
        audioTable = numpy.frombuffer(rawData, dtype=dtype).reshape(-1, channels) # A table where each column specifies a channel (list of samples).

        # Return all rows (:), but just get the column 0. Hence, return only channel 0.
        return audioTable[:, 0]
    
    def GetSamplingFrequency(self) -> int:
        if not self._waveHandle:
            raise RuntimeError("File must be open before reading!")
        
        return self._waveHandle.getframerate()

    """
        If using 'with' on the class for reading,
        makes sure that on enter it opens the file handle.
    """
    def __enter__(self): 
        self.Open()
        return self
    
    """
        If using 'with' on the class for reading,
        makes sure that on leave it closes the file handle.
    """
    def __exit__(self, *args): 
        self.Close()

"""
    Class for creating and writing to wave files.
"""
class WaveFileWriter:
    def __init__(self, filePath: Path, samplingFrequency: int, channels: int = 1, sampleWidth: int = 2):
        self._filePath    = str(filePath)
        self._frequency   = samplingFrequency
        self._channels    = channels    # How many columns of audio data to write.
        self._sampleWidth = sampleWidth # Ex 2 means 2 * 8 bits, hence 16 bits.
        self._waveHandle: Optional[wave.Wave_write] = None

    """
        Opens the file and sets all the differents attributes of
        the file to that specified in the constructor.
    """
    def Open(self):
        self._waveHandle = wave.open(self._filePath, 'wb')
        self._waveHandle.setnchannels(self._channels)
        self._waveHandle.setsampwidth(self._sampleWidth)
        self._waveHandle.setframerate(self._frequency)

    """
        Writes the samples to the file. Samples has to floats.
    """
    def WriteSamples(self, samples: numpy.ndarray[float]):
        if not self._waveHandle:
            raise RuntimeError("File must be open before writing!")
        
        # Make sure that the values are normalized between -1 and 1.
        maxVal = numpy.max(numpy.abs(samples))
        if maxVal > 0:
            samples = samples / maxVal

        if self._sampleWidth == 1:
            # 8-bit is unsigned (0 till 255) using the WAV-format, where silence = 128.
            outData = ((samples + 1.0) * 127.5).astype(numpy.uint8)
            
        elif self._sampleWidth == 2:
            # 16-bit signed (-32768 till 32767).
            outData = (samples * 32767).astype(numpy.int16)
            
        elif self._sampleWidth == 4:
            # 32-bit signed (-2147483648 till 2147483647).
            outData = (samples * 2147483647).astype(numpy.int32)
            
        else:
            raise RuntimeError(f"Support for {self._sampleWidth * 8} bit is not implemented.")
        
        self._waveHandle.writeframes(outData.tobytes())

    """
        Closes the file handle.
    """
    def Close(self):
        if self._waveHandle:
            self._waveHandle.close()

    """
        If using 'with' on the class for reading,
        makes sure that on enter it opens the file handle.
    """
    def __enter__(self): 
        self.Open()
        return self
    
    """
        If using 'with' on the class for reading,
        makes sure that on leave it closes the file handle.
    """
    def __exit__(self, *args): 
        self.Close()