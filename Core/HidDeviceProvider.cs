using HidSharp;

namespace OpenSeriesGG.Core;

public sealed class HidDeviceProvider
{
    public IEnumerable<HidDevice> GetDevices(ISteelSeriesDevice definition)
    {
        return DeviceList.Local.GetHidDevices().Where(device =>
            device.VendorID == ISteelSeriesDevice.VendorId &&
            definition.ProductIds.Contains(device.ProductID) &&
            IsRequiredInterface(device, definition));
    }

    private static bool IsRequiredInterface(
        HidDevice device,
        ISteelSeriesDevice definition)
    {
        try
        {
            return device.GetReportDescriptor().DeviceItems
                .Any(item => item.Usages.ContainsValue(definition.Usage));
        }
        catch
        {
            string interfaceMarker = $"&mi_{definition.WindowsInterfaceNumber:x2}";
            return device.DevicePath.Contains(
                interfaceMarker,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
