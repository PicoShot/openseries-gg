using HidSharp;
using OpenSeries.Protocols;

namespace OpenSeries.Devices.Mice;

internal sealed class Aerox3Definition : IDeviceDefinition
{
    private static readonly int[] ProductIds =
    [
        0x1836, // Aerox 3
        0x1838, // Aerox 3 Wireless, 2.4 GHz
        0x183a, // Aerox 3 Wireless, wired
        0x1878, // Aerox 3 Wireless CS2 Dragon Lore Edition, 2.4 GHz
        0x187a  // Aerox 3 Wireless CS2 Dragon Lore Edition, wired
    ];

    public string Slug => "aerox-3";

    public bool Matches(HidDevice endpoint)
    {
        if (!ProductIds.Contains(endpoint.ProductID))
        {
            return false;
        }

        try
        {
            return endpoint.GetReportDescriptor().DeviceItems.Any(
                item => item.Usages.ContainsValue(0xffc00001));
        }
        catch
        {
            return endpoint.DevicePath.Contains("&mi_03", StringComparison.OrdinalIgnoreCase);
        }
    }

    public ISteelSeriesDevice Connect(HidDevice endpoint, DeviceIdentity identity) =>
        new Aerox3(endpoint, identity);
}

internal sealed class Aerox3(HidDevice endpoint, DeviceIdentity identity) : IMouseDevice
{
    private static readonly int[] ReceiverProductIds = [0x1838, 0x1878];
    private static readonly int[] WirelessProductIds = [0x1838, 0x183a, 0x1878, 0x187a];

    private static readonly byte[] CoreSensitivityValues =
    [
        0x04, 0x06, 0x08, 0x0b, 0x0d, 0x0f, 0x12, 0x14, 0x16, 0x19,
        0x1b, 0x1d, 0x20, 0x22, 0x24, 0x27, 0x29, 0x2b, 0x2e, 0x30,
        0x32, 0x34, 0x37, 0x39, 0x3b, 0x3e, 0x40, 0x42, 0x45, 0x47,
        0x49, 0x4c, 0x4e, 0x50, 0x53, 0x55, 0x57, 0x5a, 0x5c, 0x5e,
        0x61, 0x63, 0x65, 0x68, 0x6a, 0x6c, 0x6f, 0x71, 0x73, 0x76,
        0x78, 0x7a, 0x7d, 0x7f, 0x81, 0x84, 0x86, 0x88, 0x8b, 0x8d,
        0x8f, 0x92, 0x94, 0x96, 0x99, 0x9b, 0x9d, 0xa0, 0xa2, 0xa4,
        0xa7, 0xa9, 0xab, 0xad, 0xb0, 0xb2, 0xb4, 0xb7, 0xb9, 0xbc,
        0xbe, 0xc0, 0xc3, 0xc5
    ];

    private static readonly byte[] SkippedAirSensitivityValues =
    [
        0x08, 0x0f, 0x15, 0x1c, 0x22, 0x24, 0x2b, 0x31, 0x37, 0x3d, 0x43,
        0x49, 0x4f, 0x55, 0x5b, 0x61, 0x67, 0x6d, 0x73, 0x79, 0x7f, 0x85,
        0x8b, 0x91, 0x97, 0x9d, 0xa3, 0xa9, 0xaf, 0xbe, 0xc1, 0xc8, 0xce, 0xd4
    ];

    private const int IoTimeoutMilliseconds = 2_000;
    private const int ReceiverResponseLength = 64;
    private const int CommandDelayMilliseconds = 50;

    private bool IsReceiver => ReceiverProductIds.Contains(ProductId);
    private bool IsWirelessModel => WirelessProductIds.Contains(ProductId);

    public string Id => identity.Id;
    public string Name => ProductId switch
    {
        0x1836 => "SteelSeries Aerox 3",
        0x1878 or 0x187a => "SteelSeries Aerox 3 Wireless CS2 Dragon Lore Edition",
        _ => "SteelSeries Aerox 3 Wireless"
    };
    public int ProductId => identity.ProductId;
    public string? SerialNumber => identity.SerialNumber;

    public Features SupportedFeatures =>
        Features.MouseSensitivity |
        Features.PollingRate |
        Features.Illumination |
        (IsWirelessModel ? Features.BatteryStatus | Features.SleepTimer : Features.None);

    public MouseSensitivityInfo SensitivityInfo => IsWirelessModel
        ? new(100, 18_000, 100, 5)
        : new(200, 8_500, 100, 5);
    public IReadOnlyList<ushort> SupportedPollingRates { get; } = [125, 250, 500, 1000];

    public void SetSensitivity(IReadOnlyList<ushort> dpiPresets)
    {
        ArgumentNullException.ThrowIfNull(dpiPresets);
        if (dpiPresets.Count is < 1 or > 5)
        {
            throw new ArgumentException("Between one and five DPI presets are required.", nameof(dpiPresets));
        }

        var command = new byte[3 + dpiPresets.Count];
        command[0] = 0x2d;
        command[1] = (byte)dpiPresets.Count;
        command[2] = IsWirelessModel ? (byte)0x00 : (byte)0x01;
        for (int index = 0; index < dpiPresets.Count; index++)
        {
            ushort dpi = dpiPresets[index];
            command[index + 3] = IsWirelessModel
                ? EncodeAirSensitivity(dpi, index)
                : EncodeCoreSensitivity(dpi, index);
        }

        SendAndSave(command);
    }

    public void SetPollingRate(ushort pollingRate)
    {
        byte encoded = pollingRate switch
        {
            125 => IsWirelessModel ? (byte)0x03 : (byte)0x04,
            250 => IsWirelessModel ? (byte)0x02 : (byte)0x03,
            500 => IsWirelessModel ? (byte)0x01 : (byte)0x02,
            1000 => IsWirelessModel ? (byte)0x00 : (byte)0x01,
            _ => throw new ArgumentOutOfRangeException(
                nameof(pollingRate), "Polling rate must be 125, 250, 500, or 1000 Hz.")
        };

        SendAndSave([0x2b, encoded]);
    }

    public void SetIllumination(MouseZone zone, RgbColor color)
    {
        ArgumentNullException.ThrowIfNull(color);
        if (zone is not (MouseZone.Top or MouseZone.Middle or MouseZone.Bottom))
        {
            throw new ArgumentOutOfRangeException(
                nameof(zone), "Aerox 3 lighting zone must be top, middle, or bottom.");
        }

        byte[] prefix = IsWirelessModel
            ? [0x21, 0x01, (byte)zone]
            : zone switch
            {
                MouseZone.Top => [0x21, 0x01],
                MouseZone.Middle => [0x21, 0x02, 0x00, 0x00, 0x00],
                MouseZone.Bottom => [0x21, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
                _ => throw new ArgumentOutOfRangeException(nameof(zone))
            };

        SendAndSave([.. prefix, color.Red, color.Green, color.Blue]);
    }

    public void SetSleepTimer(byte minutes)
    {
        if (!IsWirelessModel)
        {
            throw new NotSupportedException("Sleep timer is only available on Aerox 3 Wireless models.");
        }
        if (minutes > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "Sleep timer must be between 0 and 20 minutes.");
        }

        int milliseconds = minutes * 60_000;
        SendAndSave(
        [
            0x29,
            (byte)milliseconds,
            (byte)(milliseconds >> 8),
            (byte)(milliseconds >> 16)
        ]);
    }

    public BatteryInfo GetBattery()
    {
        if (!IsWirelessModel)
        {
            throw new NotSupportedException("Battery status is only available on Aerox 3 Wireless models.");
        }

        byte[] response = SendCommand([0x92], true);
        byte responseCommand = IsReceiver ? (byte)0xd2 : (byte)0x92;
        int offset = response.Length >= 3 && response[0] == 0x00 && response[1] == responseCommand ? 1 : 0;
        if (response.Length - offset < 2)
        {
            throw new InvalidDataException($"Device returned a short battery response ({response.Length} bytes).");
        }

        byte raw = response[offset + 1];
        int batteryStep = raw & 0x7f;
        if (batteryStep is < 1 or > 21)
        {
            throw new InvalidOperationException("mouse is disconnected or returned an invalid battery level");
        }

        ushort level = (ushort)((batteryStep - 1) * 5);
        BatteryStatus status = (raw & 0x80) != 0
            ? BatteryStatus.Charging
            : level == 100 ? BatteryStatus.Charged : BatteryStatus.Discharging;
        return new BatteryInfo(level, status, response);
    }

    private void SendAndSave(ReadOnlySpan<byte> command)
    {
        SendCommand(command, IsReceiver);
        Thread.Sleep(CommandDelayMilliseconds);
        SendCommand([0x11, 0x00], IsReceiver);
    }

    private byte[] SendCommand(ReadOnlySpan<byte> command, bool readResponse)
    {
        using HidStream stream = endpoint.Open();
        stream.ReadTimeout = IoTimeoutMilliseconds;
        stream.WriteTimeout = IoTimeoutMilliseconds;

        int reportLength = endpoint.GetMaxOutputReportLength();
        if (reportLength < command.Length + 1)
        {
            throw new InvalidDataException($"Output report length {reportLength} cannot carry this command.");
        }

        byte[] report = new byte[reportLength];
        command.CopyTo(report.AsSpan(1));
        if (IsReceiver)
        {
            report[1] |= 0x40;
        }
        stream.Write(report);

        if (!readResponse)
        {
            return [];
        }

        var response = new byte[Math.Max(ReceiverResponseLength, endpoint.GetMaxInputReportLength())];
        int bytesRead = stream.Read(response);
        return response[..bytesRead];
    }

    private static byte EncodeAirSensitivity(ushort dpi, int index)
    {
        if (dpi is < 100 or > 18_000 || dpi % 100 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dpi), $"DPI preset {index + 1} must be from 100 to 18000 in steps of 100.");
        }
        if (dpi == 100)
        {
            return 0;
        }

        int encoded = dpi / 100;
        foreach (byte skipped in SkippedAirSensitivityValues)
        {
            if (encoded < skipped)
            {
                break;
            }
            encoded++;
        }
        return checked((byte)encoded);
    }

    private static byte EncodeCoreSensitivity(ushort dpi, int index)
    {
        if (dpi is < 200 or > 8_500 || dpi % 100 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dpi), $"DPI preset {index + 1} must be from 200 to 8500 in steps of 100.");
        }

        return CoreSensitivityValues[(dpi - 200) / 100];
    }
}
