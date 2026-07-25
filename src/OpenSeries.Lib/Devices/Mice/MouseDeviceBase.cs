using OpenSeries.Protocols;

namespace OpenSeries.Devices.Mice;

internal abstract class MouseDeviceBase(HidSharp.HidDevice endpoint, DeviceIdentity identity) : IMouseDevice
{
    protected HidTransport Transport { get; } = new(endpoint, 2_000);
    public string Id => identity.Id;
    public abstract string Name { get; }
    public int ProductId => identity.ProductId;
    public abstract Features SupportedFeatures { get; }

    public virtual MouseSensitivityInfo SensitivityInfo => throw Unsupported("sensitivity control");

    public virtual IReadOnlyList<ushort> SupportedPollingRates => throw Unsupported("polling rate control");

    public virtual IReadOnlyList<MouseZone> SupportedIlluminationZones => throw Unsupported("illumination control");

    public virtual void SetSensitivity(IReadOnlyList<ushort> dpiPresets) => throw Unsupported("sensitivity control");

    public virtual void SetPollingRate(ushort pollingRate) => throw Unsupported("polling rate control");

    public virtual void SetIllumination(MouseZone zone, RgbColor color) => throw Unsupported("illumination control");

    public virtual void SetSleepTimer(byte minutes) => throw Unsupported("sleep timer");

    public virtual BatteryInfo GetBattery() => throw Unsupported("battery status");

    public void Dispose() => Transport.Dispose();

    private NotSupportedException Unsupported(string feature) => new($"{Name} does not support {feature}.");
}
