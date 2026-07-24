using HidSharp;
using OpenSeriesGG.Core;

namespace OpenSeriesGG.Devices.Headsets;

public sealed class Arctis7Plus : IHeadsetDevice
{
    private const int IoTimeoutMilliseconds = 2_000;
    private const int MessageSize = 64;
    private const int StatusBufferSize = 128;
    private const int EqualizerBands = 10;
    private const float EqualizerMinimum = -12.0f;
    private const float EqualizerMaximum = 12.0f;
    private const float EqualizerStep = 0.5f;
    private const byte EqualizerBaseline = 0x18;

    private HidDevice? _device;

    public string Name => "SteelSeries Arctis 7+";

    public IReadOnlyCollection<int> ProductIds { get; } =
    [
        0x220e, // Arctis 7+
        0x2212, // Arctis 7+ PS5
        0x2216, // Arctis 7+ Xbox
        0x2236  // Arctis 7+ Destiny
    ];

    public uint Usage => 0xffc00001;
    public int WindowsInterfaceNumber => 3;

    public Features SupportedFeatures =>
        Features.Sidetone |
        Features.BatteryStatus |
        Features.Chatmix |
        Features.InactiveTime |
        Features.Equalizer |
        Features.EqualizerPreset;

    public EqualizerInfo EqualizerInfo { get; } =
        new(EqualizerBands, EqualizerMinimum, EqualizerMaximum, EqualizerStep);

    public IReadOnlyList<EqualizerPreset> EqualizerPresets { get; } =
    [
        new("Flat", [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        new("Bass Boost", [3.5f, 4.0f, 1.0f, -1.5f, -1.5f, -1.0f, -1.0f, -1.0f, -1.0f, 5.5f]),
        new("Smiley", [3.0f, 1.5f, -1.5f, -4.0f, -4.0f, -2.5f, 1.5f, 3.0f, 4.0f, 3.5f]),
        new("Focus", [-5.0f, -1.0f, -3.5f, -2.5f, 4.0f, 6.0f, 3.5f, -3.5f, 0.0f, -3.5f])
    ];

    public void Connect(HidDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (device.VendorID != ISteelSeriesDevice.VendorId ||
            !ProductIds.Contains(device.ProductID))
        {
            throw new ArgumentException(
                "The HID endpoint is not a supported Arctis 7+ device.",
                nameof(device));
        }

        _device = device;
    }

    public BatteryInfo GetBattery()
    {
        byte[] data = ReadDeviceStatus();
        if (data[1] == 0x01)
        {
            return new BatteryInfo(0, BatteryStatus.Disconnected, data);
        }

        ushort level = (ushort)Math.Clamp(data[2] * 25, 0, 100);
        BatteryStatus status = data[3] == 0x01
            ? BatteryStatus.Charging
            : level == 100
                ? BatteryStatus.Charged
                : BatteryStatus.Discharging;

        return new BatteryInfo(level, status, data);
    }

    public void SetSidetone(byte level)
    {
        if (level > 128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level), level, "Sidetone must be between 0 and 128.");
        }

        byte deviceLevel = level switch
        {
            < 26 => 0x00,
            < 51 => 0x01,
            < 76 => 0x02,
            _ => 0x03
        };

        SendCommand([0x00, 0x39, deviceLevel]);
    }

    public void SetInactiveTime(ushort minutes)
    {
        if (minutes > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutes), minutes, "Inactive time must be between 0 and 90 minutes.");
        }

        SendCommand([0x00, 0xa3, (byte)minutes]);
    }

    public ChatmixInfo GetChatmix()
    {
        byte[] data = ReadDeviceStatus();
        int gameRaw = data[4];
        int chatRaw = data[5];

        int game = Map(gameRaw, 0, 100, 0, 64);
        int chat = Map(chatRaw, 0, 100, 0, -64);
        int level = Math.Clamp(64 - (chat + game), 0, 128);

        return new ChatmixInfo(
            (ushort)level,
            (ushort)Math.Clamp(Map(gameRaw, 0, 100, 0, 100), 0, 100),
            (ushort)Math.Clamp(Map(chatRaw, 0, 100, 0, 100), 0, 100));
    }

    public void SetEqualizerPreset(byte preset)
    {
        if (preset >= EqualizerPresets.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preset), preset, "The Arctis 7+ supports presets 0 through 3.");
        }

        SetEqualizer(EqualizerPresets[preset].Bands);
    }

    public void SetEqualizer(IReadOnlyList<float> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.Count != EqualizerBands)
        {
            throw new ArgumentException(
                $"The Arctis 7+ requires exactly {EqualizerBands} equalizer bands.",
                nameof(bands));
        }

        byte[] command = new byte[EqualizerBands + 3];
        command[0] = 0x00;
        command[1] = 0x33;

        for (int index = 0; index < bands.Count; index++)
        {
            float value = bands[index];
            if (value < EqualizerMinimum || value > EqualizerMaximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bands), value, "Band values must be between -12 and +12 dB.");
            }

            float steps = value / EqualizerStep;
            if (MathF.Abs(steps - MathF.Round(steps)) > 0.0001f)
            {
                throw new ArgumentException(
                    $"Band {index} must use {EqualizerStep:0.0} dB increments.",
                    nameof(bands));
            }

            command[index + 2] =
                checked((byte)(EqualizerBaseline + MathF.Round(2 * value)));
        }

        command[^1] = 0x00;
        SendCommand(command);
    }

    private byte[] ReadDeviceStatus()
    {
        HidDevice device = GetConnectedDevice();
        using HidStream stream = OpenStream(device);

        WriteReport(stream, device, [0x00, 0xb0]);

        byte[] response = new byte[Math.Max(StatusBufferSize, device.GetMaxInputReportLength())];
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
        HidDevice device = GetConnectedDevice();
        using HidStream stream = OpenStream(device);
        WriteReport(stream, device, command);
    }

    private static HidStream OpenStream(HidDevice device)
    {
        HidStream stream = device.Open();
        stream.ReadTimeout = IoTimeoutMilliseconds;
        stream.WriteTimeout = IoTimeoutMilliseconds;
        return stream;
    }

    private static void WriteReport(
        HidStream stream,
        HidDevice device,
        ReadOnlySpan<byte> command)
    {
        int reportLength = device.GetMaxOutputReportLength();
        if (reportLength < command.Length || reportLength < MessageSize)
        {
            throw new InvalidDataException(
                $"Output report length {reportLength} cannot carry the {command.Length}-byte command.");
        }

        byte[] report = new byte[reportLength];
        command.CopyTo(report);
        stream.Write(report);
    }

    private HidDevice GetConnectedDevice() =>
        _device ?? throw new InvalidOperationException(
            $"{Name} is not connected. Call Connect before using headset features.");

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
