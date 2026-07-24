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

    public string Id => identity.Id;
    public string Name => "SteelSeries Arctis Nova 7";
    public int ProductId => identity.ProductId;
    public string? SerialNumber => identity.SerialNumber;

    public Features SupportedFeatures =>
        Features.Sidetone |
        Features.BatteryStatus |
        Features.Chatmix |
        Features.InactiveTime |
        Features.Equalizer |
        Features.EqualizerPreset;

    public EqualizerInfo EqualizerInfo { get; } =
        new(
            EqualizerBands,
            EqualizerMinimum,
            EqualizerMaximum,
            EqualizerStep);

    public IReadOnlyList<EqualizerPreset> EqualizerPresets { get; } =
    [
        new("Flat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        new("Bass", [3.5f, 5.5f, 4, 1, -1.5f, -1.5f, -1, -1, -1, -1]),
        new("Focus", [-5, -3.5f, -1, -3.5f, -2.5f, 4, 6, -3.5f, 0, 0]),
        new("Smiley", [3, 3.5f, 1.5f, -1.5f, -4, -4, -2.5f, 1.5f, 3, 4])
    ];

    public BatteryInfo GetBattery()
    {
        byte[] data = ReadDeviceStatus();
        if (data[3] == 0x00)
        {
            return new BatteryInfo(0, BatteryStatus.Disconnected, data);
        }

        int level = DiscreteBatteryProductIds.Contains(ProductId)
            ? Map(data[2], 0, 4, 0, 100)
            : data[2];
        level = Math.Clamp(level, 0, 100);

        BatteryStatus status = data[3] is 0x01 or 0x02
            ? BatteryStatus.Charging
            : level == 100
                ? BatteryStatus.Charged
                : BatteryStatus.Discharging;

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
            throw new ArgumentOutOfRangeException(
                nameof(level),
                "Sidetone must be between 0 and 128.");
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
            throw new ArgumentOutOfRangeException(
                nameof(minutes),
                "Inactive time must be between 0 and 90 minutes.");
        }

        SendCommand([0x00, 0xa3, (byte)minutes]);
    }

    public void SetEqualizerPreset(byte preset)
    {
        if (preset >= EqualizerPresets.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preset),
                "Preset index must be between 0 and 3.");
        }

        SetEqualizer(EqualizerPresets[preset].Bands);
    }

    public void SetEqualizer(IReadOnlyList<float> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.Count != EqualizerBands)
        {
            throw new ArgumentException(
                $"Exactly {EqualizerBands} equalizer bands are required.",
                nameof(bands));
        }

        var command = new byte[MessageSize];
        command[1] = 0x33;

        for (int index = 0; index < bands.Count; index++)
        {
            float value = bands[index];
            if (value < EqualizerMinimum || value > EqualizerMaximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bands),
                    $"Band {index + 1} must be between -10 and +10 dB.");
            }

            float steps = value / EqualizerStep;
            if (MathF.Abs(steps - MathF.Round(steps)) > 0.0001f)
            {
                throw new ArgumentException(
                    $"Band {index + 1} must use 0.5 dB increments.",
                    nameof(bands));
            }

            // This follows the Nova protocol's baseline-plus-gain encoding.
            command[index + 2] = checked((byte)(EqualizerBaseline + value));
        }

        command[EqualizerBands + 2] = 0x00;
        SendCommand(command);
    }

    private byte[] ReadDeviceStatus()
    {
        using HidStream stream = OpenStream();
        WriteReport(stream, [0x00, 0xb0]);

        var response = new byte[
            Math.Max(StatusBufferSize, endpoint.GetMaxInputReportLength())];
        int bytesRead = stream.Read(response);
        if (bytesRead < 6)
        {
            throw new InvalidDataException(
                $"Device returned a short status response ({bytesRead} bytes).");
        }

        return response[..bytesRead];
    }

    private void SendCommand(ReadOnlySpan<byte> command)
    {
        using HidStream stream = OpenStream();
        WriteReport(stream, command);
    }

    private HidStream OpenStream()
    {
        HidStream stream = endpoint.Open();
        stream.ReadTimeout = IoTimeoutMilliseconds;
        stream.WriteTimeout = IoTimeoutMilliseconds;
        return stream;
    }

    private void WriteReport(
        HidStream stream,
        ReadOnlySpan<byte> command)
    {
        int reportLength = endpoint.GetMaxOutputReportLength();
        if (reportLength < command.Length || reportLength < MessageSize)
        {
            throw new InvalidDataException(
                $"Output report length {reportLength} cannot carry this command.");
        }

        byte[] report = new byte[reportLength];
        command.CopyTo(report);
        stream.Write(report);
    }

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
