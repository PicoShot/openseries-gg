using System.Reflection;

namespace OpenSeriesGG.Core;

public static class DeviceRegistry
{
    public static IReadOnlyList<ISteelSeriesDevice> Discover()
    {
        return [.. Assembly.GetExecutingAssembly().GetTypes()
            .Where(type =>
                !type.IsAbstract && !type.IsInterface &&
                typeof(ISteelSeriesDevice).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (ISteelSeriesDevice)Activator.CreateInstance(type)!)
            .OrderBy(device => device.Name)];
    }
}
