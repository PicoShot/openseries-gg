using HidSharp;

namespace OpenSeriesGG.Core;

public interface ISteelSeriesDevice
{
    const int VendorId = 0x1038;

    string Name { get; }
    IReadOnlyCollection<int> ProductIds { get; }
    uint Usage { get; }
    int WindowsInterfaceNumber { get; }
}
