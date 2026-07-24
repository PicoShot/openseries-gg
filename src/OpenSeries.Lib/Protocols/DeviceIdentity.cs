using System.Security.Cryptography;
using System.Text;
using HidSharp;

namespace OpenSeries.Protocols;

internal sealed record DeviceIdentity(string Id, int ProductId, string? SerialNumber)
{
    internal static DeviceIdentity Create(string slug, HidDevice endpoint)
    {
        string? serial = null;
        try
        {
            serial = endpoint.GetSerialNumber();
        }
        catch
        {
            // Some platforms do not expose a serial without opening the endpoint.
        }

        string suffix = string.IsNullOrWhiteSpace(serial)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.DevicePath)))[..12].ToLowerInvariant()
            : Slugify(serial);

        return new DeviceIdentity(
            $"{slug}-{endpoint.ProductID:x4}-{suffix}",
            endpoint.ProductID,
            string.IsNullOrWhiteSpace(serial) ? null : serial);
    }

    private static string Slugify(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (char character in value.Trim().ToLowerInvariant())
        {
            result.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        return result.ToString().Trim('-');
    }
}
