# PM1000 Visualizer
This program aims to visualize the stokes vector from the PM1000 device using a computer (running the application) plugged into the device (PM1000) using USB.

## PM1000
The PM1000 is a polarimeter device used in measuring all 4 stokes parameters at a sampling rate of 100 MHz. For further reading, check the [PM 1000 User Guide](Documents/PM1000_User_Guide.pdf).

## Prerequisites
This WPF application targets .NET 8.0 and the Windows 7 API. The project is generated using Visual Studio, hence highly recommended using that IDE for building and continuing developing this application.

### NuGet packages
Furthermore, there are some NuGet packages that needs to be installed in order for this project to build:

* **ScottPlot for WPF** - Takes care of plotting graphs etc. You can read more about it [here](https://scottplot.net/).

*Note:* All of these packages are exposed by relying on the nuget.config file in the project.

### Install drivers
While the NuGet packages are a great start to be able to build the project, you'll still need to install FTDI drivers. You can find the [FTDI drivers installation guide](Documents/FTDI_Drivers_Installation_Guide__Windows_10_11.pdf) inside the documents folder.

*Note:* The type of driver needed needs to be checked... download both 2DXX (comes together with VPC) and 3DXX just in case.
