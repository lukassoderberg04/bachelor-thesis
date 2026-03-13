from pathlib import Path
from io import TextIOWrapper
from typing import Optional
import re as regex

"""
    PM1000 module contains classes and functions that are important
    for retrieving and communicating with the PM1000 polarimeter.
"""

"""
    A class that can open a result file and retrieve the stokes vectors
    and also the attributes of the recording (from a PM1000 recording).
"""
class PM1000ResultFileReader:
    def __init__(self, filePath: Path, chunkSize: int) -> "PM1000ResultFileReader":
        self._filePath                            = filePath
        self._chunkSize                           = chunkSize
        self._fileHandle: Optional[TextIOWrapper] = None
        self._attributes: dict[str, str]          = {}

        if not self._filePath.exists():
            raise FileNotFoundError(f"The file in question wasn't found: {self._filePath}")
        
    """
        Parses the headers and fills in the attribute list.
    """
    def _parseHeader(self):
        """
            ^     - Start of line.
            \s*   - Match whitespaces (\s = whitespace, * = 0 or more).
            (\w+) - Create a group, which is defined by parenthesis, which contains words (\w = letter).
            =     - Just matches the equals token.
            ['"]? - The square bracket means match one of these tokens in it. The question marks just states optional.
            (.*?) - Match ANY token, like a joker. The question marks just states that it should stop when seeing the next pattern in the list.
            ;     - Match this token.
            $     - Marks end of line.
        """
        headerPattern: regex.Pattern = regex.compile(r"^ \s* (\w+) \s* = \s* ['\"]? (.*?) ['\"]? \s* ; \s* $")
        
        while True:
            lastPos = self._fileHandle.tell()
            line    = self._fileHandle.readline()

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

        self._parseHeader()

    """
        Gets the next list of samples from the file.
        Returns None if there's no more samples to read.
    """
    def GetNextSample(self) -> Optional[list[float]]:
        if not self._fileHandle:
            raise RuntimeError("File must be open before reading!")
        
        line = self._fileHandle.readline()
        if not line or not line.strip():
            self.Close()
            return None
        
        values = [int(x.strip()) for x in line.split(',')]

        if not len(values) == 5:
            raise RuntimeError(f"File was missing a value when reading line at line: {self._fileHandle.tell()}")

        return PM1000Measurment(values[0], values[1], values[2], values[3], values[4])
    
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