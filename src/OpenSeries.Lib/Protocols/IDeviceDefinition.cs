using HidSharp;
using OpenSeries.Devices;

namespace OpenSeries.Protocols;

internal interface IDeviceDefinition
{
    string Slug { get; }
    bool Matches(HidDevice endpoint);
    ISteelSeriesDevice Connect(HidDevice endpoint, DeviceIdentity identity);
}