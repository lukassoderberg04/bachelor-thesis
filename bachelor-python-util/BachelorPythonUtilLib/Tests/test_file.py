from pathlib import Path
import File
import unittest

"""
    Contains tests for the PM1000 module.
"""

"""
    Tests for parsing of PM1000 result file.
"""
class TestPM1000FileParsing(unittest.TestCase):
    """
        Tests parsing the header from a example file
        and makes sure the attributes match up.
    """

    def setUp(self):
        self._fileToParse = Path(__file__).parent / "Data" / "test_data.txt"

    def test_parsing_header_from_file(self):
        with (File.PM1000ResultFileReader(self._fileToParse) as reader):
            attributes = reader.GetAttributes()

            self.assertEqual(attributes.get("ATE"), "16", f"Couldn't read ATE attribute from data file. Read {attributes.get("ATE")}.")
            self.assertEqual(attributes.get("SamplePeriod_ns"), "655360", f"Couldn't read SamplePeriod_ns attribute from data file. Read {attributes.get("SamplePeriod_ns")}.")
            self.assertEqual(attributes.get("ME"), "11", f"Couldn't read ME attribute from data file. Read {attributes.get("ME")}.")
            self.assertEqual(attributes.get("TriggerGatingReg"), "32777", f"Couldn't read TriggerGatingReg attribute from data file. Read {attributes.get("TriggerGatingReg")}.")
            self.assertEqual(attributes.get("CyclicRecording"), "0", f"Couldn't read CyclicRecording attribute from data file. Read {attributes.get("CyclicRecording")}.")

    def test_parsing_data_from_file(self):        
        with (File.PM1000ResultFileReader(self._fileToParse) as reader):
            data0 = reader.GetNextSample()
            data1 = reader.GetNextSample()

            self.assertEqual(data0.GetTimestamp(), 0, f"Data 0 Timestamp doesn't align, read {data0.GetTimestamp()}")
            self.assertEqual(data0.GetS0(), 57327, f"Data 0 S0 doesn't align, read {data0.GetS0()}")
            self.assertEqual(data0.GetS1(), 5446, f"Data 0 S1 doesn't align, read {data0.GetS1()}")
            self.assertEqual(data0.GetS2(), 22700, f"Data 0 S2 doesn't align, read {data0.GetS2()}")
            self.assertEqual(data0.GetS3(), 17731, f"Data 0 S3 doesn't align, read {data0.GetS3()}")

            self.assertEqual(data1.GetTimestamp(), 655360, f"Data 1 Timestamp doesn't align, read {data1.GetTimestamp()}")
            self.assertEqual(data1.GetS0(), 57288, f"Data 1 S0 doesn't align, read {data1.GetS0()}")
            self.assertEqual(data1.GetS1(), 5453, f"Data 1 S1 doesn't align, read {data1.GetS1()}")
            self.assertEqual(data1.GetS2(), 22693, f"Data 1 S2 doesn't align, read {data1.GetS2()}")
            self.assertEqual(data1.GetS3(), 17725, f"Data 1 S3 doesn't align, read {data1.GetS3()}")

    def test_parse_file_until_end(self):
        row = 0
        
        with (File.PM1000ResultFileReader(self._fileToParse) as reader):
            while reader.GetNextSample():
                continue
            
            row = reader.GetNextSampleIndex()
            
        self.assertEqual(row, 2048, f"The last row of the file doesn't match the end of the file. Read {row}.")

    def test_parse_all_samples(self):
        samples: list[File.PM1000Measurment] = []

        with (File.PM1000ResultFileReader(self._fileToParse) as reader):
            samples = reader.GetAllSamples()

        self.assertEqual(len(samples), 2048, f"The amount of samples retrieved after parsing all samples didn't match with the expected. Got {len(samples)}.")