# PM1000 Streamer Service
A service that bridges the gap between the PM1000 and other services that's dependent on PM1000 data. It's main objective is to retrieve data from the PM1000 using a USB connection and stream it to a specified port for anyone on the system to listen to.

## PM1000
The PM1000 is a polarimeter device used in measuring all 4 stokes parameters at a sampling rate of 100 MHz. For further reading, check the [PM 1000 User Guide](Documents/PM1000_User_Guide.pdf).

## Prerequisites
This streaming service is a console application targeting .NET 8.0 on Windows 10 using the x64 architecture. Since this project is generated and built using the Visual Studio build tools, it is highly recommended to use Visual Studio IDE for ease of development.

### Dotnet build tools
This project used Visual Studio's bundled build tools when installing the .NET framework. To install the dotnet standalone build toolchain for .NET 8, visit this [page](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

### Native DLLs
The project references FTDI's managed wrapper. Both files must be present in the `lib/` folder:

| File               | Description |
|--------------------|--------------------|
| `FTD3XXWU.dll`     | Native FTDI FT60x driver. |
| `FTD3XXWU_NET.dll` | Managed (.NET) wrapper for the above. |

*Note:* These are copied to the root of the build folder using a post-build script.

### Installing drivers
Since the project communicates with an FTDI device via USB 3.0 it's required to install the dedicated drivers to make sure the used .NET dll works as intended. To find the drivers, please visit this [website](https://ftdichip.com/drivers/) and install D3XX drivers for Windows x64. If you need help, please resolve to the [drivers installation guide](Documents/FTDI_Drivers_Installation_Guide__Windows_10_11.pdf).

## Building and running
Make sure you have the following prerequisites before trying to build and run the application.

```bash
cd pm1000-streamer-service

dotnet build

dotnet run
```

## Architecture

## Using the service