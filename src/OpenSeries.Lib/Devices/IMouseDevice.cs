namespace OpenSeries.Devices;

public interface IMouseDevice : ISteelSeriesDevice
{
    MouseSensitivityInfo SensitivityInfo { get; }
    IReadOnlyList<ushort> SupportedPollingRates { get; }
    void SetSensitivity(IReadOnlyList<ushort> dpiPresets);
    void SetPollingRate(ushort pollingRate);
    void SetIllumination(MouseZone zone, RgbColor color);
    void SetSleepTimer(byte minutes);
    BatteryInfo GetBattery();
}

public sealed record MouseSensitivityInfo(ushort Minimum, ushort Maximum, ushort Step, byte MaximumPresetCount);

public sealed record RgbColor(byte Red, byte Green, byte Blue);

public enum MouseZone
{
    Top,
    Middle,
    Bottom
}
