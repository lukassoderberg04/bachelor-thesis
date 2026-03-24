import numpy
from typing import Self

"""
    File module contains utility classes and functions
    for stokes vectors.
"""

"""
    A class that can open a result file and retrieve the stokes vectors
    and also the attributes of the recording (from a PM1000 recording).
"""
class StokesVector:
    def __init__(self, s0: int, s1: int, s2: int, s3: int) -> None:
        self._stokes = numpy.array([s0, s1, s2, s3])

    def GetPower(self) -> int:
        """
        Returns the power s0.
        """ 
        return self._stokes[0]
    
    def GetAmplitudeOfCombinedStokes(self) -> float:
        """
        Combines the stokes vectors (not power) into
        one vector and calculates the magnitude.
        """
        stokes = self._stokes[1:]

        return numpy.sqrt(stokes.dot(stokes)) # return sqrt(s1^2 + s2^2 + s3^2).
    
    def GetS1(self) -> int:
        return self._stokes[1]
    
    def GetS2(self) -> int:
        return self._stokes[2]
    
    def GetS3(self) -> int:
        return self._stokes[3]

    def Normalized(self) -> Self:
        """
        Returns a normalized stokes vector `[1, S1/S0, S2/S0, S3/S0]`.
        """
        return type(self)(*(self._stokes / self._stokes[0]))