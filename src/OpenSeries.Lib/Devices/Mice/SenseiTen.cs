using HidSharp;
using OpenSeries.Protocols;

namespace OpenSeries.Devices.Mice;

internal sealed class SenseiTenDefinition : IDeviceDefinition
{
    private static readonly int[] ProductIds =
    [
        0x1832, // Sensei Ten
        0x1834  // Sensei Ten CS:GO Neon Rider Edition
    ];

    public string Slug => "sensei-ten";

    public bool Matches(HidDevice endpoint)
    {
        if (!ProductIds.Contains(endpoint.ProductID))
        {
            return false;
        }

        try
        {
            return endpoint.GetMaxOutputReportLength() >= 15 &&
                   endpoint.GetMaxFeatureReportLength() >= 36;
        }
        catch
        {
            return endpoint.DevicePath.Contains("&mi_00", StringComparison.OrdinalIgnoreCase);
        }
    }

    public ISteelSeriesDevice Connect(HidDevice endpoint, DeviceIdentity identity) => new SenseiTen(endpoint, identity);
}

internal sealed class SenseiTen(HidDevice endpoint, DeviceIdentity identity) : IMouseDevice
{
    private const int IoTimeoutMilliseconds = 2_000;
    private const int CommandDelayMilliseconds = 50;
    private readonly HidTransport transport = new(endpoint, IoTimeoutMilliseconds);

    public string Id => identity.Id;
    public string Name => ProductId == 0x1834
        ? "SteelSeries Sensei Ten CS:GO Neon Rider Edition"
        : "SteelSeries Sensei Ten";
    public int ProductId => identity.ProductId;
    public Features SupportedFeatures =>
        Features.MouseSensitivity |
        Features.PollingRate |
        Features.Illumination;

    public MouseSensitivityInfo SensitivityInfo { get; } = new(50, 18_000, 50, 5);
    public IReadOnlyList<ushort> SupportedPollingRates { get; } = [125, 250, 500, 1000];
    public IReadOnlyList<MouseZone> SupportedIlluminationZones { get; } =
        [MouseZone.Logo, MouseZone.Wheel];

    public void SetSensitivity(IReadOnlyList<ushort> dpiPresets)
    {
        ArgumentNullException.ThrowIfNull(dpiPresets);
        if (dpiPresets.Count is < 1 or > 5)
        {
            throw new ArgumentException("Between one and five DPI presets are required.", nameof(dpiPresets));
        }

        var command = new byte[4 + dpiPresets.Count * 2];
        command[0] = 0x55;
        command[1] = 0x00;
        command[2] = (byte)((1 << dpiPresets.Count) - 1);
        command[3] = 0x01;
        for (int index = 0; index < dpiPresets.Count; index++)
        {
            ushort dpi = dpiPresets[index];
            if (dpi is < 50 or > 18_000 || dpi % 50 != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dpiPresets), $"DPI preset {index + 1} must be from 50 to 18000 in steps of 50.");
            }

            ushort encoded = (ushort)(dpi / 50);
            command[4 + index * 2] = (byte)encoded;
            command[5 + index * 2] = (byte)(encoded >> 8);
        }

        SendAndSave(command);
    }

    public void SetPollingRate(ushort pollingRate)
    {
        byte encoded = pollingRate switch
        {
            125 => 0x04,
            250 => 0x03,
            500 => 0x02,
            1000 => 0x01,
            _ => throw new ArgumentOutOfRangeException(
                nameof(pollingRate), "Polling rate must be 125, 250, 500, or 1000 Hz.")
        };

        SendAndSave([0x54, 0x00, encoded]);
    }

    public void SetIllumination(MouseZone zone, RgbColor color)
    {
        ArgumentNullException.ThrowIfNull(color);
        byte ledId = zone switch
        {
            MouseZone.Logo => 0x00,
            MouseZone.Wheel => 0x01,
            _ => throw new ArgumentOutOfRangeException(
                nameof(zone), "Sensei Ten lighting zone must be logo or wheel.")
        };

        var command = new byte[35];
        command[0] = 0x5b;
        command[1] = 0x00;
        command[2] = ledId;
        command[3] = 0xe8;
        command[4] = 0x03;
        command[19] = 0x01;
        command[27] = 0x01;
        command[28] = color.Red;
        command[29] = color.Green;
        command[30] = color.Blue;
        command[31] = color.Red;
        command[32] = color.Green;
        command[33] = color.Blue;
        command[34] = 0x00;

        SendFeatureAndSave(command);
    }

    public void SetSleepTimer(byte minutes) =>
        throw new NotSupportedException("Sensei Ten does not provide a sleep timer.");

    public BatteryInfo GetBattery() =>
        throw new NotSupportedException("Sensei Ten is a wired mouse and has no battery.");

    private void SendAndSave(ReadOnlySpan<byte> command)
    {
        SendOutput(command);
        Thread.Sleep(CommandDelayMilliseconds);
        SendOutput([0x59, 0x00]);
    }

    private void SendFeatureAndSave(ReadOnlySpan<byte> command)
    {
        transport.WriteFeature(command, commandOffset: 1);

        Thread.Sleep(CommandDelayMilliseconds);
        SendOutput([0x59, 0x00]);
    }

    private void SendOutput(ReadOnlySpan<byte> command) => transport.WriteOutput(command, commandOffset: 1);
}
