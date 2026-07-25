using OpenSeries;
using OpenSeries.Devices;

var red = new RgbColor(255, 0, 0);
var blue = new RgbColor(0, 60, 255);
IReadOnlyList<ISteelSeriesDevice> devices = new DeviceManager().GetConnectedDevices();
IMouseDevice? mouse = devices
    .OfType<IMouseDevice>()
    .FirstOrDefault(device => device.SupportedFeatures.HasFlag(Features.Illumination));

if (mouse is null)
{
    DisposeDevices(devices);
    Console.Error.WriteLine("No connected mouse with controllable illumination was found.");
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

Console.WriteLine(
    $"{mouse.Name} · police lights across " +
    $"{string.Join(", ", mouse.SupportedIlluminationZones)} · Ctrl+C to exit");

int phase = 0;
try
{
    while (!cancellation.IsCancellationRequested)
    {
        for (int index = 0; index < mouse.SupportedIlluminationZones.Count; index++)
        {
            MouseZone zone = mouse.SupportedIlluminationZones[index];
            RgbColor color = (index + phase) % 2 == 0 ? red : blue;
            mouse.SetIllumination(zone, color, save: false);
        }

        phase++;
        if (cancellation.Token.WaitHandle.WaitOne(400))
            break;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Lighting effect stopped: {exception.Message}");
    return 1;
}
finally
{
    DisposeDevices(devices);
}

return 0;

static void DisposeDevices(IEnumerable<ISteelSeriesDevice> devices)
{
    foreach (ISteelSeriesDevice device in devices)
        device.Dispose();
}
