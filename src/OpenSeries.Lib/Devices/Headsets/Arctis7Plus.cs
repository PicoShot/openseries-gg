using HidSharp;
using OpenSeries.Protocols;

namespace OpenSeries.Devices.Headsets;

internal sealed class Arctis7PlusDefinition : IDeviceDefinition
{
    private static readonly int[] ProductIds = [0x220e, 0x2212, 0x2216, 0x2236];
    public string Slug => "arctis-7-plus";

    public bool Matches(HidDevice endpoint)
    {
        if (!ProductIds.Contains(endpoint.ProductID))
        {
            return false;
        }

        try
        {
            return endpoint.GetReportDescriptor().DeviceItems.Any(item => item.Usages.ContainsValue(0xffc00001));
        }
        catch
        {
            return endpoint.DevicePath.Contains("&mi_03", StringComparison.OrdinalIgnoreCase);
        }
    }

    public ISteelSeriesDevice Connect(HidDevice endpoint, DeviceIdentity identity) => new Arctis7Plus(endpoint, identity);
}

internal sealed class Arctis7Plus(HidDevice endpoint, DeviceIdentity identity) : HeadsetDeviceBase(endpoint, identity)
{
    private const int MessageSize = 64;
    private const int StatusBufferSize = 128;
    private const int EqualizerBands = 10;
    private const float EqualizerMinimum = -12;
    private const float EqualizerMaximum = 12;
    private const float EqualizerStep = 0.5f;
    private const byte EqualizerBaseline = 0x18;

    public override string Name => "SteelSeries Arctis 7+";
    public override Features SupportedFeatures =>
        Features.Sidetone |
        Features.BatteryStatus |
        Features.Chatmix |
        Features.InactiveTime |
        Features.Equalizer |
        Features.EqualizerPreset;

    public override EqualizerInfo EqualizerInfo { get; } = new
    (
        EqualizerBands,
        EqualizerMinimum,
        EqualizerMaximum,
        EqualizerStep
    );

    public override IReadOnlyList<EqualizerPreset> EqualizerPresets { get; } =
    [
        new("Flat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        new("Bass Boost", [3.5f, 4, 1, -1.5f, -1.5f, -1, -1, -1, -1, 5.5f]),
        new("Smiley", [3, 1.5f, -1.5f, -4, -4, -2.5f, 1.5f, 3, 4, 3.5f]),
        new("Focus", [-5, -1, -3.5f, -2.5f, 4, 6, 3.5f, -3.5f, 0, -3.5f])
    ];
    public override BatteryInfo GetBattery()
    {
        byte[] data = ReadDeviceStatus();
        if (data[1] == 0x01)
        {
            return new BatteryInfo(0, BatteryStatus.Disconnected, data);
        }

        ushort level = (ushort)Math.Clamp(data[2] * 25, 0, 100);
        BatteryStatus status = data[3] == 0x01 ? BatteryStatus.Charging : level == 100 ? BatteryStatus.Charged : BatteryStatus.Discharging;
        return new BatteryInfo(level, status, data);
    }

    public override ChatmixInfo GetChatmix()
    {
        byte[] data = ReadDeviceStatus();
        int gameRaw = data[4];
        int chatRaw = data[5];
        int game = Map(gameRaw, 0, 100, 0, 64);
        int chat = Map(chatRaw, 0, 100, 0, -64);
        return new ChatmixInfo(
            (ushort)Math.Clamp(64 - (chat + game), 0, 128),
            (ushort)Math.Clamp(gameRaw, 0, 100),
            (ushort)Math.Clamp(chatRaw, 0, 100));
    }

    public override void SetSidetone(byte level)
    {
        if (level > 128)
            throw new ArgumentOutOfRangeException(nameof(level), "Sidetone must be between 0 and 128.");
        byte deviceLevel = level switch { < 26 => 0, < 51 => 1, < 76 => 2, _ => 3 };
        SendCommand([0, 0x39, deviceLevel]);
    }

    public override void SetInactiveTime(ushort minutes)
    {
        if (minutes > 90)
            throw new ArgumentOutOfRangeException(nameof(minutes), "Inactive time must be between 0 and 90 minutes.");
        SendCommand([0, 0xa3, (byte)minutes]);
    }

    public override void SetEqualizerPreset(byte preset)
    {
        if (preset >= EqualizerPresets.Count)
            throw new ArgumentOutOfRangeException(nameof(preset), "Preset index is out of range.");
        SetEqualizer(EqualizerPresets[preset].Bands);
    }

    public override void SetEqualizer(IReadOnlyList<float> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.Count != EqualizerBands)
            throw new ArgumentException($"Exactly {EqualizerBands} equalizer bands are required.", nameof(bands));

        var command = new byte[EqualizerBands + 3];
        command[1] = 0x33;
        for (int index = 0; index < bands.Count; index++)
        {
            float value = bands[index];
            if (value < EqualizerMinimum || value > EqualizerMaximum)
                throw new ArgumentOutOfRangeException(nameof(bands), $"Band {index + 1} must be between -12 and +12 dB.");
            float steps = value / EqualizerStep;
            if (MathF.Abs(steps - MathF.Round(steps)) > 0.0001f)
                throw new ArgumentException($"Band {index + 1} must use 0.5 dB increments.", nameof(bands));
            command[index + 2] = checked((byte)(EqualizerBaseline + MathF.Round(2 * value)));
        }
        SendCommand(command);
    }

    private byte[] ReadDeviceStatus()
    {
        byte[] response = Transport.WriteOutputAndRead([0x00, 0xb0], StatusBufferSize);
        if (response.Length < 6)
            throw new InvalidDataException($"Device returned a short status response ({response.Length} bytes).");

        int statusOffset = response.Length >= 7 && response[0] == 0x00 && response[1] == 0xb0 ? 1 : 0;
        return response[statusOffset..];
    }

    private void SendCommand(ReadOnlySpan<byte> command) => Transport.WriteOutput(command, minimumReportLength: MessageSize);

    private static int Map(
        int value,
        int sourceMin,
        int sourceMax,
        int targetMin,
        int targetMax) =>
        (value - sourceMin) *
        (targetMax - targetMin) /
        (sourceMax - sourceMin) + targetMin;
}
