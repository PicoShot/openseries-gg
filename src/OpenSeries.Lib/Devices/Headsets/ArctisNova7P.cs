using HidSharp;
using OpenSeries.Protocols;

namespace OpenSeries.Devices.Headsets;

internal sealed class ArctisNova7PDefinition : IDeviceDefinition
{
    private static readonly int[] ProductIds =
    [
        0x220a, // Nova 7P, discrete battery
        0x22a7  // Nova 7P v2, percentage battery
    ];

    public string Slug => "arctis-nova-7p";

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

    public ISteelSeriesDevice Connect(HidDevice endpoint, DeviceIdentity identity) => new ArctisNova7P(endpoint, identity);
}

internal sealed class ArctisNova7P(HidDevice endpoint, DeviceIdentity identity) : IHeadsetDevice
{
    private const int DiscreteBatteryProductId = 0x220a;
    private const int IoTimeoutMilliseconds = 2_000;
    private const int MessageSize = 64;
    private const int StatusBufferSize = 128;
    private const int EqualizerBands = 10;
    private const float EqualizerMinimum = -10;
    private const float EqualizerMaximum = 10;
    private const float EqualizerStep = 0.5f;
    private const byte EqualizerBaseline = 0x14;
    private readonly HidTransport transport = new(endpoint, IoTimeoutMilliseconds);

    public string Id => identity.Id;
    public string Name => "SteelSeries Arctis Nova 7P";
    public int ProductId => identity.ProductId;

    public Features SupportedFeatures =>
        Features.BatteryStatus |
        Features.InactiveTime |
        Features.Equalizer |
        Features.EqualizerPreset;

    public EqualizerInfo EqualizerInfo { get; } = new
    (
        EqualizerBands,
        EqualizerMinimum,
        EqualizerMaximum,
        EqualizerStep
    );

    public IReadOnlyList<EqualizerPreset> EqualizerPresets { get; } =
    [
        new("Flat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        new("Bass", [3.5f, 5.5f, 4, 1, -1.5f, -1.5f, -1, -1, -1, -1]),
        new("Focus", [-5, -3.5f, -1, -3.5f, -2.5f, 4, 6, -3.5f, 0, 0]),
        new("Smiley", [3, 3.5f, 1.5f, -1.5f, -4, -4, -2.5f, 1.5f, 3, 4])
    ];
    public ParametricEqualizerInfo? ParametricEqualizerInfo => null;

    public BatteryInfo GetBattery()
    {
        byte[] data = ReadDeviceStatus();
        if (data[3] == 0x00)
        {
            return new BatteryInfo(0, BatteryStatus.Disconnected, data);
        }

        int level = ProductId == DiscreteBatteryProductId ? Map(data[2], 0, 4, 0, 100) : data[2];
        level = Math.Clamp(level, 0, 100);
        BatteryStatus status = data[3] is 0x01 or 0x02
            ? BatteryStatus.Charging
            : level == 100 ? BatteryStatus.Charged : BatteryStatus.Discharging;

        return new BatteryInfo((ushort)level, status, data);
    }

    public ChatmixInfo GetChatmix() => throw new NotSupportedException($"{Name} does not support ChatMix.");

    public void SetSidetone(byte level) => throw new NotSupportedException($"{Name} does not support sidetone control.");

    public void SetInactiveTime(ushort minutes)
    {
        if (minutes > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "Inactive time must be between 0 and 90 minutes.");
        }

        SendCommand([0x00, 0xa3, (byte)minutes]);
    }

    public void SetEqualizerPreset(byte preset)
    {
        if (preset >= EqualizerPresets.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(preset), "Preset index must be between 0 and 3.");
        }

        SetEqualizer(EqualizerPresets[preset].Bands);
    }

    public void SetEqualizer(IReadOnlyList<float> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.Count != EqualizerBands)
        {
            throw new ArgumentException($"Exactly {EqualizerBands} equalizer bands are required.", nameof(bands));
        }

        var command = new byte[MessageSize];
        command[1] = 0x33;

        for (int index = 0; index < bands.Count; index++)
        {
            float value = bands[index];
            if (value < EqualizerMinimum || value > EqualizerMaximum)
            {
                throw new ArgumentOutOfRangeException(nameof(bands), $"Band {index + 1} must be between -10 and +10 dB.");
            }

            float steps = value / EqualizerStep;
            if (MathF.Abs(steps - MathF.Round(steps)) > 0.0001f)
            {
                throw new ArgumentException($"Band {index + 1} must use 0.5 dB increments.", nameof(bands));
            }

            command[index + 2] = checked((byte)(EqualizerBaseline + value));
        }

        command[EqualizerBands + 2] = 0x00;
        SendCommand(command);
    }

    public void SetMicrophoneVolume(byte volume) =>
        throw new NotSupportedException($"{Name} does not support microphone volume control.");

    public void SetMicrophoneMuteLedBrightness(byte brightness) =>
        throw new NotSupportedException($"{Name} does not support microphone mute LED brightness control.");

    public void SetVolumeLimiter(bool enabled) =>
        throw new NotSupportedException($"{Name} does not support volume limiter control.");

    public void SetParametricEqualizer(IReadOnlyList<ParametricEqualizerBand> bands) =>
        throw new NotSupportedException($"{Name} does not support a parametric equalizer.");

    private byte[] ReadDeviceStatus()
    {
        using HidStream stream = transport.OpenStream();
        stream.Write([0x00, 0xb0]);

        var response = new byte[Math.Max(StatusBufferSize, endpoint.GetMaxInputReportLength())];
        int bytesRead = stream.Read(response);
        if (bytesRead < 6)
        {
            throw new InvalidDataException($"Device returned a short status response ({bytesRead} bytes).");
        }

        int statusOffset = bytesRead >= 7 && response[0] == 0x00 && response[1] == 0xb0 ? 1 : 0;
        return response[statusOffset..bytesRead];
    }

    private void SendCommand(ReadOnlySpan<byte> command) => transport.WriteOutput(command, minimumReportLength: MessageSize);

    private static int Map(
        int value,
        int sourceMinimum,
        int sourceMaximum,
        int targetMinimum,
        int targetMaximum) =>
        (value - sourceMinimum) *
        (targetMaximum - targetMinimum) /
        (sourceMaximum - sourceMinimum) +
        targetMinimum;
}
