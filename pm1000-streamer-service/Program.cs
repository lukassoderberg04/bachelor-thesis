using pm1000_streamer_service;

Logger.WriteDashedLine();
Logger.WriteText("PM 1000 streamer service");
Logger.WriteDashedLine();

Logger.WriteEmptyLine();

var devices = FtdiService.GetConnectedDevicesInfo();

/*
    * Retrieve devices.
    * Make the user either refresh the list or select one device.
    * Initialize the connection.
*/