using OpenSeries.Protocols;

namespace OpenSeries.Devices.Headsets;

internal abstract class HeadsetDeviceBase(HidSharp.HidDevice endpoint, DeviceIdentity identity) : IHeadsetDevice
{
    protected HidTransport Transport { get; } = new(endpoint, 2_000);
    public string Id => identity.Id;
    public abstract string Name { get; }
    public int ProductId => identity.ProductId;
    public abstract Features SupportedFeatures { get; }

    public virtual EqualizerInfo EqualizerInfo => throw Unsupported("equalizer");

    public virtual IReadOnlyList<EqualizerPreset> EqualizerPresets => throw Unsupported("equalizer presets");

    public virtual ParametricEqualizerInfo? ParametricEqualizerInfo => null;

    public virtual BatteryInfo GetBattery() => throw Unsupported("battery status");

    public virtual ChatmixInfo GetChatmix() => throw Unsupported("ChatMix");

    public virtual void SetSidetone(byte level) => throw Unsupported("sidetone control");

    public virtual void SetInactiveTime(ushort minutes) => throw Unsupported("inactive time control");

    public virtual void SetEqualizer(IReadOnlyList<float> bands) => throw Unsupported("equalizer");

    public virtual void SetEqualizerPreset(byte preset) => throw Unsupported("equalizer presets");

    public virtual void SetMicrophoneVolume(byte volume) => throw Unsupported("microphone volume control");

    public virtual void SetMicrophoneMuteLedBrightness(byte brightness) => throw Unsupported("microphone mute LED brightness control");

    public virtual void SetVolumeLimiter(bool enabled) => throw Unsupported("volume limiter control");

    public virtual void SetParametricEqualizer(IReadOnlyList<ParametricEqualizerBand> bands) => throw Unsupported("parametric equalizer");

    public virtual void SetBluetoothWhenPoweredOn(bool enabled) => throw Unsupported("Bluetooth power-on control");

    public virtual void SetBluetoothCallVolume(BluetoothCallVolumeMode mode) => throw Unsupported("Bluetooth call volume control");

    public void Dispose() => Transport.Dispose();

    private NotSupportedException Unsupported(string feature) => new($"{Name} does not support {feature}.");
}
