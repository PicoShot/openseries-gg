using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using OpenSeries.Devices;
using Spectre.Console;
using DeviceFeatures = OpenSeries.Devices.Features;

namespace OpenSeries.Cli;

internal static class CliSupport
{
    internal static DeviceSelection<ISteelSeriesDevice> Discover(bool quiet = false)
    {
        try
        {
            IReadOnlyList<ISteelSeriesDevice> devices = new DeviceManager().GetConnectedDevices();
            return new DeviceSelection<ISteelSeriesDevice>(devices, new DeviceDisposer(devices));
        }
        catch (Exception exception)
        {
            if (!quiet) Error(null, exception);
            return DeviceSelection<ISteelSeriesDevice>.Empty;
        }
    }

    internal static DeviceSelection<IHeadsetDevice> SelectHeadsets(
        string? id,
        DeviceFeatures required,
        bool quiet,
        out int exitCode)
    {
        DeviceSelection<ISteelSeriesDevice> all = Discover(quiet);
        IEnumerable<ISteelSeriesDevice> selected = all;
        if (!string.IsNullOrWhiteSpace(id))
        {
            ISteelSeriesDevice[] matches = all
                .Where(device => string.Equals(device.Id, id, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                if (!quiet) AnsiConsole.MarkupLine($"[red]No device has ID[/] {Markup.Escape(id)}.");
                exitCode = 1;
                all.Dispose();
                return DeviceSelection<IHeadsetDevice>.Empty;
            }
            if (matches.Length > 1)
            {
                if (!quiet) AnsiConsole.MarkupLine($"[red]Device ID is ambiguous:[/] {Markup.Escape(id)}.");
                exitCode = 1;
                all.Dispose();
                return DeviceSelection<IHeadsetDevice>.Empty;
            }
            selected = matches;
        }

        IHeadsetDevice[] headsets = selected.OfType<IHeadsetDevice>()
            .Where(headset => headset.SupportedFeatures.HasFlag(required))
            .ToArray();
        if (headsets.Length == 0)
        {
            if (!quiet) AnsiConsole.MarkupLine("[red]No compatible connected headset was found.[/]");
            exitCode = 1;
            all.Dispose();
            return DeviceSelection<IHeadsetDevice>.Empty;
        }
        exitCode = 0;
        return new DeviceSelection<IHeadsetDevice>(headsets, all);
    }

    internal static DeviceSelection<IMouseDevice> SelectMice(
        string? id,
        DeviceFeatures required,
        bool quiet,
        out int exitCode)
    {
        DeviceSelection<ISteelSeriesDevice> all = Discover(quiet);
        IEnumerable<ISteelSeriesDevice> selected = all;
        if (!string.IsNullOrWhiteSpace(id))
        {
            ISteelSeriesDevice[] matches = all
                .Where(device => string.Equals(device.Id, id, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                if (!quiet)
                    AnsiConsole.MarkupLine(matches.Length == 0
                        ? $"[red]No device has ID[/] {Markup.Escape(id)}."
                        : $"[red]Device ID is ambiguous:[/] {Markup.Escape(id)}.");
                exitCode = 1;
                all.Dispose();
                return DeviceSelection<IMouseDevice>.Empty;
            }
            selected = matches;
        }

        IMouseDevice[] mice = selected.OfType<IMouseDevice>()
            .Where(mouse => mouse.SupportedFeatures.HasFlag(required))
            .ToArray();
        if (mice.Length == 0)
        {
            if (!quiet) AnsiConsole.MarkupLine("[red]No compatible connected mouse was found.[/]");
            exitCode = 1;
            all.Dispose();
            return DeviceSelection<IMouseDevice>.Empty;
        }
        exitCode = 0;
        return new DeviceSelection<IMouseDevice>(mice, all);
    }

    internal static void Json<T>(T value, JsonTypeInfo<T> typeInfo) =>
        Console.Out.WriteLine(JsonSerializer.Serialize(value, typeInfo));

    internal static void Error(ISteelSeriesDevice? device, Exception exception)
    {
        string prefix = device is null ? "" : $"{Markup.Escape(device.Id)}: ";
        string message = exception switch
        {
            UnauthorizedAccessException => "permission denied; install the appropriate udev rule and reconnect the device",
            TimeoutException => "timed out waiting for the device",
            IOException => $"device protocol error: {exception.Message}",
            InvalidOperationException => exception.Message,
            NotSupportedException => $"unsupported feature: {exception.Message}",
            _ => exception.Message
        };
        AnsiConsole.MarkupLine($"[red]{prefix}{Markup.Escape(message)}[/]");
    }

    internal static string Features(DeviceFeatures features) =>
        string.Join(", ", Enum.GetValues<DeviceFeatures>()
            .Where(feature => feature != DeviceFeatures.None && features.HasFlag(feature)));

    internal static string BatteryDisplay(ushort levelPercentage, string chargingState)
    {
        int level = Math.Clamp((int)levelPercentage, 0, 100);
        int filledCells = level / 10;
        string bar = new string('=', filledCells) + new string(' ', 10 - filledCells);
        return $"{Markup.Escape(chargingState)} [[{bar}]] {level} %";
    }
}

internal sealed class DeviceSelection<T>(
    IReadOnlyList<T> devices,
    IDisposable? owner) : IReadOnlyList<T>, IDisposable
{
    internal static DeviceSelection<T> Empty { get; } = new([], null);

    public int Count => devices.Count;
    public T this[int index] => devices[index];
    public IEnumerator<T> GetEnumerator() => devices.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    public void Dispose() => owner?.Dispose();
}

internal sealed class DeviceDisposer(IEnumerable<ISteelSeriesDevice> devices) : IDisposable
{
    public void Dispose()
    {
        foreach (ISteelSeriesDevice device in devices)
            device.Dispose();
    }
}

internal sealed record DeviceJson(
    string Id,
    string Model,
    string ProductId,
    string[] Capabilities);

internal sealed record BatteryJson(
    string Id,
    string Model,
    ushort? LevelPercentage,
    string? ChargingState,
    string? Error = null);

internal sealed record ChatMixJson(
    string Id,
    string Model,
    ushort? Level,
    ushort? GameVolumePercentage,
    ushort? ChatVolumePercentage,
    string? Error = null);

internal sealed record StatusJson(
    string Id,
    string Model,
    string ProductId,
    string[] Capabilities,
    BatteryJson? Battery,
    ChatMixJson? ChatMix,
    string? Error);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(DeviceJson[]))]
[JsonSerializable(typeof(BatteryJson[]))]
[JsonSerializable(typeof(ChatMixJson[]))]
[JsonSerializable(typeof(StatusJson[]))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
