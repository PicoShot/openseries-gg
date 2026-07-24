using HidSharp;
using OpenSeries.Devices;
using OpenSeries.Protocols;

namespace OpenSeries;

public sealed class DeviceManager
{
    public IReadOnlyList<ISteelSeriesDevice> GetConnectedDevices()
    {
        var devices = new List<ISteelSeriesDevice>();
        foreach (HidDevice endpoint in DeviceList.Local.GetHidDevices(0x1038))
        {
            foreach (IDeviceDefinition definition in DeviceDefinitions.All)
            {
                if (!definition.Matches(endpoint))
                {
                    continue;
                }

                devices.Add(definition.Connect(endpoint, DeviceIdentity.Create(definition.Slug, endpoint)));
            }
        }

        return devices.OrderBy(device => device.Id, StringComparer.Ordinal).ToArray();
    }
}
