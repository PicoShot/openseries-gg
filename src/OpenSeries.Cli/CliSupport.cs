using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSeries.Devices;
using Spectre.Console;
using DeviceFeatures = OpenSeries.Devices.Features;

namespace OpenSeries.Cli;

internal static class CliSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    internal static IReadOnlyList<ISteelSeriesDevice> Discover(bool quiet = false)
    {
        try
        {
            return new DeviceManager().GetConnectedDevices();
        }
        catch (Exception exception)
        {
            if (!quiet) Error(null, exception);
            return [];
        }
    }

    internal static IReadOnlyList<IHeadsetDevice> SelectHeadsets(
        string? id,
        DeviceFeatures required,
        bool quiet,
        out int exitCode)
    {
        IReadOnlyList<ISteelSeriesDevice> all = Discover(quiet);
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
                return [];
            }
            if (matches.Length > 1)
            {
                if (!quiet) AnsiConsole.MarkupLine($"[red]Device ID is ambiguous:[/] {Markup.Escape(id)}.");
                exitCode = 1;
                return [];
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
            return [];
        }
        exitCode = 0;
        return headsets;
    }

    internal static void Json(object value) =>
        Console.Out.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

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

internal sealed record DeviceJson(
    string Id,
    string Model,
    string ProductId,
    string? SerialNumber,
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
    string? SerialNumber,
    string[] Capabilities,
    BatteryJson? Battery,
    ChatMixJson? ChatMix,
    string? Error);
