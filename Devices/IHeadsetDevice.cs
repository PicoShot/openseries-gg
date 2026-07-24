using HidSharp;
using OpenSeriesGG.Core;

namespace OpenSeriesGG.Devices;

[Flags]
public enum Features 
{
    Sidetone,
    BatteryStatus,
    Chatmix,
    InactiveTime,
    Equalizer
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
    void SetSideTone(sbyte sideTone); // 0-128
    BatteryStatus GetBatteryStatus();
    ushort GetBatteryLevel(); 
    void SetInactiveTime(ushort inactiveTime); // (0-90 minutes, 0 disables)
    ushort GetChatmix(); // Get chat-mix-dial level (0-128, <64 for game, >64 for chat)

    void SetEqualizerBand(); // TODO: Implement equalizer params
    void SetEqualizerPreset(); // TODO: Implement equalizer preset params
}
