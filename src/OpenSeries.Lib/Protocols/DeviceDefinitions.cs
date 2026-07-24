using OpenSeries.Devices.Headsets;
using OpenSeries.Devices.Mice;

namespace OpenSeries.Protocols;

internal static class DeviceDefinitions
{
    internal static IReadOnlyList<IDeviceDefinition> All { get; } =
    [
        new Arctis7PlusDefinition(),
        new ArctisNova5Definition(),
        new ArctisNova7Definition(),
        new ArctisNova7PDefinition(),
        new Aerox5Definition()
    ];
}
