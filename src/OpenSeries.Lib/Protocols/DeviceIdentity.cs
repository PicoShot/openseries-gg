using System.Security.Cryptography;
using System.Text;
using HidSharp;

namespace OpenSeries.Protocols;

internal sealed record DeviceIdentity(string Id, int ProductId)
{
    internal static DeviceIdentity Create(string slug, HidDevice endpoint)
    {

        string suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.DevicePath)))[..12].ToLowerInvariant();

        return new DeviceIdentity($"{slug}-{endpoint.ProductID:x4}-{suffix}", endpoint.ProductID);
    }
}
