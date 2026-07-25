namespace OpenSeries.Devices;

public interface IHeadsetDevice : ISteelSeriesDevice
{
    EqualizerInfo EqualizerInfo { get; }
    IReadOnlyList<EqualizerPreset> EqualizerPresets { get; }
    ParametricEqualizerInfo? ParametricEqualizerInfo { get; }

    BatteryInfo GetBattery();
    ChatmixInfo GetChatmix();
    void SetSidetone(byte level);
    void SetInactiveTime(ushort minutes);
    void SetEqualizer(IReadOnlyList<float> bands);
    void SetEqualizerPreset(byte preset);
    void SetMicrophoneVolume(byte volume);
    void SetMicrophoneMuteLedBrightness(byte brightness);
    void SetVolumeLimiter(bool enabled);
    void SetParametricEqualizer(IReadOnlyList<ParametricEqualizerBand> bands);
    void SetBluetoothWhenPoweredOn(bool enabled);
    void SetBluetoothCallVolume(BluetoothCallVolumeMode mode);
}

public enum BatteryStatus
{
    Unknown,
    Disconnected,
    Discharging,
    Charging,
    Charged
}

public sealed record BatteryInfo(ushort LevelPercentage, BatteryStatus Status, IReadOnlyList<byte> RawData);

public sealed record ChatmixInfo(ushort Level, ushort GameVolumePercentage, ushort ChatVolumePercentage);

public sealed record EqualizerInfo(int BandCount, float Minimum, float Maximum, float Step);

public sealed record EqualizerPreset(string Name, IReadOnlyList<float> Bands);

public sealed record ParametricEqualizerBand(
    ushort Frequency,
    float Gain,
    float QFactor,
    EqualizerFilterType Filter);

public sealed record ParametricEqualizerInfo(
    byte MaximumBandCount,
    ushort MinimumFrequency,
    ushort MaximumFrequency,
    float MinimumGain,
    float MaximumGain,
    float GainStep,
    float MinimumQFactor,
    float MaximumQFactor,
    IReadOnlyList<EqualizerFilterType> SupportedFilters);

public enum EqualizerFilterType
{
    Peaking,
    LowPass,
    HighPass,
    LowShelf,
    HighShelf
}

public enum BluetoothCallVolumeMode
{
    Unchanged,
    LowerBy12Decibels,
    MuteGame
}
