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
    private const ushort ParametricFrequencyMinimum = 20;
    private const ushort ParametricFrequencyMaximum = 20_000;
    private const ushort DisabledParametricFrequency = 20_001;
    private const float ParametricQMinimum = 0.2f;
    private const float ParametricQMaximum = 10;
    private readonly HidTransport transport = new(endpoint, IoTimeoutMilliseconds);

    public string Id => identity.Id;
    public string Name => "SteelSeries Arctis Nova 5/5X";
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
        Features.ParametricEqualizer;

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

    public ParametricEqualizerInfo ParametricEqualizerInfo { get; } = new(
        EqualizerBands,
        ParametricFrequencyMinimum,
        ParametricFrequencyMaximum,
        EqualizerMinimum,
        EqualizerMaximum,
        EqualizerStep,
        ParametricQMinimum,
        ParametricQMaximum,
        Enum.GetValues<EqualizerFilterType>());

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
                ? (byte)0x01 : index == 0 ? (byte)0x04
                : index == EqualizerBands - 1 ? (byte)0x05 : (byte)0x01;
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

    public void SetMicrophoneVolume(byte volume)
    {
        if (volume > 128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volume), "Microphone volume must be between 0 and 128.");
        }

        // Nova 5 exposes sixteen microphone levels, 0x00 through 0x0f.
        byte deviceLevel = (byte)Math.Min(volume / 8, 15);
        SendCommand([0x00, 0x37, deviceLevel]);
        SaveState();
    }

    public void SetMicrophoneMuteLedBrightness(byte brightness)
    {
        byte deviceBrightness = brightness switch
        {
            0 => 0x00,
            1 => 0x01,
            2 => 0x04,
            3 => 0x0a,
            _ => throw new ArgumentOutOfRangeException(
                nameof(brightness), "Microphone mute LED brightness must be between 0 and 3.")
        };

        SendCommand([0x00, 0xae, deviceBrightness]);
        SaveState();
    }

    public void SetVolumeLimiter(bool enabled)
    {
        SendCommand([0x00, 0x27, enabled ? (byte)0x01 : (byte)0x00]);
        SaveState();
    }

    public void SetParametricEqualizer(IReadOnlyList<ParametricEqualizerBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.Count is < 1 or > EqualizerBands)
        {
            throw new ArgumentException(
                $"Between one and {EqualizerBands} parametric equalizer bands are required.",
                nameof(bands));
        }

        var command = new byte[MessageSize];
        command[1] = 0x33;
        for (int index = 0; index < bands.Count; index++)
        {
            ParametricEqualizerBand band = bands[index];
            if (band.Frequency is < ParametricFrequencyMinimum or > ParametricFrequencyMaximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bands),
                    $"Band {index + 1} frequency must be between 20 and 20000 Hz.");
            }
            if (band.Gain is < EqualizerMinimum or > EqualizerMaximum ||
                !UsesStep(band.Gain, EqualizerStep))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bands),
                    $"Band {index + 1} gain must be between -10 and +10 dB in 0.5 dB increments.");
            }
            if (band.QFactor is < ParametricQMinimum or > ParametricQMaximum ||
                !UsesStep(band.QFactor, 0.001f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bands),
                    $"Band {index + 1} Q factor must be between 0.2 and 10.0 in 0.001 increments.");
            }

            WriteParametricBand(command, index, band);
        }

        // The device expects all ten slots; unused slots use frequency 20001.
        for (int index = bands.Count; index < EqualizerBands; index++)
        {
            WriteParametricBand(
                command,
                index,
                new(DisabledParametricFrequency, 0, 1.414f, EqualizerFilterType.Peaking));
        }

        SendCommand(command);
        SaveState();
    }

    private byte[] ReadDeviceStatus(int minimumLength)
    {
        using HidStream stream = transport.OpenStream();
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

    private static void WriteParametricBand(
        Span<byte> command,
        int index,
        ParametricEqualizerBand band)
    {
        int offset = 2 + 6 * index;
        command[offset] = (byte)band.Frequency;
        command[offset + 1] = (byte)(band.Frequency >> 8);
        command[offset + 2] = band.Filter switch
        {
            EqualizerFilterType.Peaking => 0x01,
            EqualizerFilterType.LowPass => 0x02,
            EqualizerFilterType.HighPass => 0x03,
            EqualizerFilterType.LowShelf => 0x04,
            EqualizerFilterType.HighShelf => 0x05,
            _ => throw new ArgumentOutOfRangeException(nameof(band), "Unknown equalizer filter type.")
        };
        command[offset + 3] = checked((byte)(EqualizerBaseline + MathF.Round(band.Gain * 2)));
        ushort encodedQ = checked((ushort)MathF.Round(band.QFactor * 1_000));
        command[offset + 4] = (byte)encodedQ;
        command[offset + 5] = (byte)(encodedQ >> 8);
    }

    private static bool UsesStep(float value, float step)
    {
        float steps = value / step;
        return MathF.Abs(steps - MathF.Round(steps)) <= 0.0001f;
    }
}
