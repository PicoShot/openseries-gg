using HidSharp;
using OpenSeriesGG.Core;

namespace OpenSeriesGG.Devices;

[Flags]
public enum Features
{
    None = 0,
    Sidetone = 1 << 0,
    BatteryStatus = 1 << 1,
    Chatmix = 1 << 2,
    InactiveTime = 1 << 3,
    Equalizer = 1 << 4,
    EqualizerPreset = 1 << 5
}

public enum BatteryStatus
{
    Unknown,
    Disconnected,
    Discharging,
    Charging,
    Charged
}

public interface IHeadsetDevice : ISteelSeriesDevice
{
    Features SupportedFeatures { get; }
    EqualizerInfo EqualizerInfo { get; }
    IReadOnlyList<EqualizerPreset> EqualizerPresets { get; }

    void SetSidetone(byte level); // 0-128
    BatteryInfo GetBattery();
    void SetInactiveTime(ushort minutes); // 0-90 minutes, 0 disables
    ChatmixInfo GetChatmix();
    void SetEqualizer(IReadOnlyList<float> bands);
    void SetEqualizerPreset(byte preset);
}

public sealed record BatteryInfo(
    ushort LevelPercentage,
    BatteryStatus Status,
    IReadOnlyList<byte> RawData);

public sealed record ChatmixInfo(
    ushort Level,
    ushort GameVolumePercentage,
    ushort ChatVolumePercentage);

public sealed record EqualizerInfo(
    int BandCount,
    float Minimum,
    float Maximum,
    float Step);

public sealed record EqualizerPreset(
    string Name,
    IReadOnlyList<float> Bands);
