using HidSharp;
using OpenSeries.Protocols;

namespace OpenSeries.Devices.Headsets;

internal sealed class ArctisNova5Definition : IDeviceDefinition
{
    private static readonly int[] ProductIds =
    [
        0x2232, // Nova 5
        0x2253  // Nova 5X
    ];

    public string Slug => "arctis-nova-5";

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

    public ISteelSeriesDevice Connect(HidDevice endpoint, DeviceIdentity identity) => new ArctisNova5(endpoint, identity);
}

internal sealed class ArctisNova5(HidDevice endpoint, DeviceIdentity identity) : IHeadsetDevice
{
    private static readonly ushort[] EqualizerFrequencies = [32, 64, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    private const int IoTimeoutMilliseconds = 2_000;
    private const int MessageSize = 64;
    private const int StatusBufferSize = 128;
    private const int MinimumStatusLength = 16;
    private const int EqualizerBands = 10;
    private const float EqualizerMinimum = -10;
    private const float EqualizerMaximum = 10;
    private const float EqualizerStep = 0.5f;
    private const byte EqualizerBaseline = 20;

    public string Id => identity.Id;
    public string Name => "SteelSeries Arctis Nova 5/5X";
    public int ProductId => identity.ProductId;

    public Features SupportedFeatures =>
        Features.Sidetone |
        Features.BatteryStatus |
        Features.Chatmix |
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

    public BatteryInfo GetBattery()
    {
        byte[] data = ReadDeviceStatus(MinimumStatusLength);
        if (data[1] == 0x02)
        {
            return new BatteryInfo(0, BatteryStatus.Disconnected, data);
        }

        int level = Math.Clamp((int)data[3], 0, 100);
        BatteryStatus status = data[4] == 0x01
            ? BatteryStatus.Charging
            : level == 100
                ? BatteryStatus.Charged
                : BatteryStatus.Discharging;

        return new BatteryInfo((ushort)level, status, data);
    }

    public ChatmixInfo GetChatmix()
    {
        byte[] data = ReadDeviceStatus(7);
        int gameRaw = data[5];
        int chatRaw = data[6];
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

        // Nova 5 exposes eleven hardware levels, 0x00 through 0x0a.
        byte deviceLevel = MapSidetone(level);
        SendCommand([0x00, 0x39, deviceLevel]);
        SaveState();
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
        }

        var command = new byte[MessageSize];
        command[1] = 0x33;
        for (int index = 0; index < bands.Count; index++)
        {
            ushort frequency = EqualizerFrequencies[index];
            byte rawGain = checked((byte)(EqualizerBaseline + MathF.Round(bands[index] * 2)));
            byte gainFlag = rawGain == EqualizerBaseline
                ? (byte)0x01
                : index == 0
                    ? (byte)0x04
                    : index == EqualizerBands - 1
                        ? (byte)0x05
                        : (byte)0x01;
            int offset = 2 + 6 * index;

            command[offset] = (byte)frequency;
            command[offset + 1] = (byte)(frequency >> 8);
            command[offset + 2] = gainFlag;
            command[offset + 3] = rawGain;
            command[offset + 4] = 0x86; // Q factor 1.414, encoded as 1414 LE.
            command[offset + 5] = 0x05;
        }

        SendCommand(command);
        SaveState();
    }

    private byte[] ReadDeviceStatus(int minimumLength)
    {
        using HidStream stream = OpenStream();
        stream.Write([0x00, 0xb0]);

        var response = new byte[Math.Max(StatusBufferSize, endpoint.GetMaxInputReportLength())];
        int bytesRead = stream.Read(response);
        int statusOffset = bytesRead >= 2 && response[0] == 0x00 && response[1] == 0xb0 ? 1 : 0;
        int statusLength = bytesRead - statusOffset;
        if (statusLength < minimumLength)
        {
            throw new InvalidDataException($"Device returned a short status response ({bytesRead} bytes).");
        }

        return response[statusOffset..bytesRead];
    }

    private void SaveState()
    {
        SendCommand([0x00, 0x09]);
        SendCommand([0x00, 0x35, 0x01]);
    }

    private void SendCommand(ReadOnlySpan<byte> command)
    {
        using HidStream stream = OpenStream();
        int reportLength = endpoint.GetMaxOutputReportLength();
        if (reportLength < command.Length || reportLength < MessageSize)
        {
            throw new InvalidDataException($"Output report length {reportLength} cannot carry this command.");
        }

        byte[] report = new byte[reportLength];
        command.CopyTo(report);
        stream.Write(report);
    }

    private HidStream OpenStream()
    {
        HidStream stream = endpoint.Open();
        stream.ReadTimeout = IoTimeoutMilliseconds;
        stream.WriteTimeout = IoTimeoutMilliseconds;
        return stream;
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

    private static byte MapSidetone(byte level)
    {
        const int levels = 11;
        const int step = 128 / levels;
        for (byte index = 1; index < levels; index++)
        {
            if (level < step * index)
            {
                return (byte)(index - 1);
            }
        }

        return levels - 1;
    }
}
