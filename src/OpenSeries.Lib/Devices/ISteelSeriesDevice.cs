namespace OpenSeries.Devices;

public interface ISteelSeriesDevice
{
    string Id { get; }
    string Name { get; }
    int ProductId { get; }
    Features SupportedFeatures { get; }
}

[Flags]
public enum Features
{
    None = 0,
    Sidetone = 1 << 0,
    BatteryStatus = 1 << 1,
    Chatmix = 1 << 2,
    InactiveTime = 1 << 3,
    Equalizer = 1 << 4,
    EqualizerPreset = 1 << 5,
    MouseSensitivity = 1 << 6,
    PollingRate = 1 << 7,
    Illumination = 1 << 8,
    SleepTimer = 1 << 9
}
