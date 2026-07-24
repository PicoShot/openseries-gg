namespace OpenSeries.Devices;

public interface IHeadsetDevice : ISteelSeriesDevice
{
    EqualizerInfo EqualizerInfo { get; }
    IReadOnlyList<EqualizerPreset> EqualizerPresets { get; }

    BatteryInfo GetBattery();
    ChatmixInfo GetChatmix();
    void SetSidetone(byte level);
    void SetInactiveTime(ushort minutes);
    void SetEqualizer(IReadOnlyList<float> bands);
    void SetEqualizerPreset(byte preset);
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
