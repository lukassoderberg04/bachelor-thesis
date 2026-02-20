using pm1000_streamer_service;
using pm1000_streamer_service.PM1000;

Logger.WriteDashedLine();
Logger.WriteText("PM 1000 streamer service");
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

foreach (var pipeInfo in FtdiService.GetAllPipesInformation())
{
    Logger.WriteText($"[0x{pipeInfo.PipeId:X2}] Type: {pipeInfo.PipeType.ToString()} | Max packet size: {pipeInfo.MaximumPacketSize}");
}