using OpenSeriesGG.Devices;

namespace OpenSeriesGG.Core;

public sealed class DeviceApplication(
    IReadOnlyList<ISteelSeriesDevice> definitions,
    HidDeviceProvider deviceProvider)
{
    public int Run()
    {
        bool foundDevice = false;

        foreach (var definition in definitions)
        {
            foreach (var device in deviceProvider.GetDevices(definition))
            {
                foundDevice = true;

                try
                {
                    definition.Connect(device);

                    Console.WriteLine($"Device: {definition.Name}");
                    if (definition is IHeadsetDevice headset &&
                        headset.SupportedFeatures.HasFlag(Features.BatteryStatus))
                    {
                        BatteryInfo battery = headset.GetBattery();
                        Console.WriteLine($"Battery: {battery.LevelPercentage}%");
                        Console.WriteLine($"Status: {battery.Status}");
                    }
                }
                catch (TimeoutException)
                {
                    Console.Error.WriteLine(
                        $"Timed out waiting for a response from {definition.Name}.");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.Error.WriteLine(
                        $"Permission denied opening {device.GetFileSystemName()}. " +
                        "Install the included udev rule, then reconnect the device.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"Could not query {definition.Name}: {ex.Message}");
                }
            }
        }

        if (foundDevice)
        {
            return 0;
        }

        Console.Error.WriteLine(
            definitions.Count == 0
                ? "No device implementations were registered."
                : "No supported SteelSeries device was found.");
        return 1;
    }
}
