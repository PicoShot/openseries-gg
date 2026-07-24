# Why OpenSeries

OpenSeries started when I switched from Windows to Linux. Most of my setup is
made by SteelSeries including my mouse and headset but SteelSeries GG isn't
available on Linux. I still wanted a simple way to check my devices and change
their settings, so I decided to build one.

The goal is to create a friendly, open-source, cross-platform alternative for
controlling SteelSeries devices without needing SteelSeries GG. The project
currently focuses on wireless Arctis headsets, with readable terminal output
for people and stable JSON output for scripts.

> OpenSeries is an independent project and is not affiliated with or endorsed
> by SteelSeries.

## Showcase

![OpenSeries terminal showcase](assets/showcase.gif)

## Supported devices

### Headsets:

| Device                      | Battery | ChatMix | Sidetone | Inactive time | Equalizer | EQ presets |
| --------------------------- | ------- | :-----: | :------: | :-----------: | :-------: | :--------: |
| Arctis 7+ variants          | Yes     |   Yes   |   Yes    |      Yes      |    Yes    |    Yes     |
| Arctis Nova 5 / 5X          | Yes     |   Yes   |   Yes    |      Yes      |    Yes    |    Yes     |
| Arctis Nova 7 / 7X variants | Yes     |   Yes   |   Yes    |      Yes      |    Yes    |    Yes     |
| Arctis Nova 7P / 7P v2      | Yes     |   No    |    No    |      Yes      |    Yes    |    Yes     |
---
### Mouse:
- TODO
--- 
I don't have access to all SteelSeries devices to reverse engineer them. If your device isn't supported yet, contributions are very welcome. Feel free to open a PR.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build
- A supported SteelSeries device
- Permission to access the device's HID interface

## Build

Clone the repository and build the complete solution:

```bash
git clone https://github.com/PicoShot/openseries-gg.git
cd openseries-gg
dotnet restore OpenSeries.sln
dotnet build OpenSeries.sln -c Release
```

Run the compiled application:

```bash
openseries --help
```

You can also run it directly through the .NET SDK:

```bash
dotnet run --project src/OpenSeries.Cli -- devices list
```

## Library usage

`OpenSeries.Lib` can be referenced independently of the CLI.

```csharp
using OpenSeries;
using OpenSeries.Devices;

var manager = new DeviceManager();

foreach (ISteelSeriesDevice device in manager.GetConnectedDevices())
{
    Console.WriteLine($"{device.Name}: {device.Id}");

    if (device is IHeadsetDevice headset &&
        device.SupportedFeatures.HasFlag(Features.BatteryStatus))
    {
        BatteryInfo battery = headset.GetBattery();
        Console.WriteLine($"{battery.LevelPercentage}% ({battery.Status})");
    }
}
```
