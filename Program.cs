using OpenSeriesGG.Core;

var application = new DeviceApplication(
    DeviceRegistry.Discover(),
    new HidDeviceProvider());

return application.Run();
