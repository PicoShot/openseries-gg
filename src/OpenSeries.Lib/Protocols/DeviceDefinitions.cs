using OpenSeries.Devices.Headsets;

namespace OpenSeries.Protocols;

internal static class DeviceDefinitions
{
    internal static IReadOnlyList<IDeviceDefinition> All { get; } = [new Arctis7PlusDefinition()];
}