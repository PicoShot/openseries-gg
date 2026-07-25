using HidSharp;
using OpenSeries.Protocols;

namespace OpenSeries.Devices.Headsets;

internal sealed class ArctisNova7Definition : IDeviceDefinition
{
    private static readonly int[] ProductIds =
    [
        0x2202, // Nova 7, discrete battery
        0x22a1, // Nova 7, percentage battery
        0x227e, // Nova 7 Wireless Gen 2
        0x2206, // Nova 7X, discrete battery
        0x2258, // Nova 7X v2
        0x229e, // Nova 7X v2
        0x22ad, // Nova 7X v2
        0x223a, // Nova 7 Diablo IV, discrete battery
        0x22a9, // Nova 7 Diablo IV, percentage battery
        0x227a, // Nova 7 WoW Edition, discrete battery
        0x22a4, // Nova 7X, discrete battery
        0x22a5  // Nova 7X, percentage battery
    ];

    public string Slug => "arctis-nova-7";

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

    public ISteelSeriesDevice Connect(HidDevice endpoint, DeviceIdentity identity) => new ArctisNova7(endpoint, identity);
}

internal sealed class ArctisNova7(HidDevice endpoint, DeviceIdentity identity) : IHeadsetDevice
{
    private static readonly int[] DiscreteBatteryProductIds = [0x2202, 0x2206, 0x223a, 0x227a, 0x22a4];

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
    public string Name => "SteelSeries Arctis Nova 7";
    public int ProductId => identity.ProductId;

    public Features SupportedFeatures =>
        Features.Sidetone |
        Features.BatteryStatus |
        Features.Chatmix |
        Features.InactiveTime |
        Features.Equalizer |
        Features.EqualizerPreset |
        Features.MicrophoneVolume |
        Features.MicrophoneMuteLedBrightness |
        Features.VolumeLimiter |
        Features.BluetoothWhenPoweredOn |
        Features.BluetoothCallVolume;

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

        int level = DiscreteBatteryProductIds.Contains(ProductId) ? Map(data[2], 0, 4, 0, 100) : data[2];
        level = Math.Clamp(level, 0, 100);

        BatteryStatus status = data[3] is 0x01 or 0x02 ? BatteryStatus.Charging : level == 100 ? BatteryStatus.Charged : BatteryStatus.Discharging;

        return new BatteryInfo((ushort)level, status, data);
    }

    public ChatmixInfo GetChatmix()
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

    public void SetSidetone(byte level)
    {
        if (level > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Sidetone must be between 0 and 128.");
        }

        byte deviceLevel = level switch
        {
            < 32 => 0,
            < 64 => 1,
            < 96 => 2,
            _ => 3
        };
        SendCommand([0x00, 0x39, deviceLevel]);
    }

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

    public void SetMicrophoneVolume(byte volume)
    {
        if (volume > 128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volume), "Microphone volume must be between 0 and 128.");
        }

        // Nova 7 exposes eight microphone levels, 0x00 through 0x07.
        byte deviceLevel = (byte)Math.Min(volume / 16, 7);
        SendCommand([0x00, 0x37, deviceLevel]);
    }

    public void SetMicrophoneMuteLedBrightness(byte brightness)
    {
        if (brightness > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(brightness), "Microphone mute LED brightness must be between 0 and 3.");
        }

        // Unlike Nova 5, Nova 7 uses the logical 0-3 value directly.
        SendCommand([0x00, 0xae, brightness]);
    }

    public void SetVolumeLimiter(bool enabled) =>
        SendCommand([0x00, 0x3a, enabled ? (byte)0x01 : (byte)0x00]);

    public void SetParametricEqualizer(IReadOnlyList<ParametricEqualizerBand> bands) =>
        throw new NotSupportedException($"{Name} does not support a parametric equalizer.");

    public void SetBluetoothWhenPoweredOn(bool enabled)
    {
        SendCommand([0x00, 0xb2, enabled ? (byte)0x01 : (byte)0x00]);
        // Nova 7 persists this setting with a distinct 0x06 report prefix.
        SendCommand([0x06, 0x09]);
    }

    public void SetBluetoothCallVolume(BluetoothCallVolumeMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Unknown Bluetooth call volume mode.");
        }

        SendCommand([0x00, 0xb3, (byte)mode]);
    }

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
