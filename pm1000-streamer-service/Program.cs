using pm1000_streamer_service;
using pm1000_streamer_service.API;
using pm1000_streamer_service.PM1000;

Logger.WriteDashedLine();
Logger.WriteText("PM 1000 streamer service");
Logger.WriteText("To stop the device, please press CTRL + C!");
Logger.WriteDashedLine();

Logger.WriteEmptyLine();

bool hasSelectedDevice  = false;
int selectedDeviceIndex = 0;

List<DeviceInfoWrapper> devices = new();

while (!hasSelectedDevice)
{
    devices = FtdiService.GetConnectedDevicesInfo();

    Logger.WriteText("First 9 connected devices printed below:");

    for (int i = 0; i < devices.Count && i < 9; i++)
    {
        var device = devices[i];

        Logger.WriteText($"[{i}] {device.Description} | {device.SerialNumber}");
    }

    Logger.WriteText("Select one of the devices by writing a number, or empty to refresh!");

    var read = (char)Console.Read();

    if (char.IsDigit(read))
    {
        int index = read - '0';

        selectedDeviceIndex = index;
        hasSelectedDevice   = true;
    }

    Logger.WriteEmptyLine();
}

PM1000Service.InitializeCommunication(devices[selectedDeviceIndex]);

CancellationTokenSource tokenSrc = new();

// Set the CTRL + C handler to cancel all other processes.
Console.CancelKeyPress += (s, e) => 
{
    tokenSrc.Cancel();
};

Retriever.Start(tokenSrc.Token);

API.Start(tokenSrc.Token, enableRest: false);