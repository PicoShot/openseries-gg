namespace OpenSeries.Devices;

public interface ISteelSeriesDevice : IDisposable
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
    SleepTimer = 1 << 9,
    MicrophoneVolume = 1 << 10,
    MicrophoneMuteLedBrightness = 1 << 11,
    VolumeLimiter = 1 << 12,
    ParametricEqualizer = 1 << 13,
    BluetoothWhenPoweredOn = 1 << 14,
    BluetoothCallVolume = 1 << 15
}
