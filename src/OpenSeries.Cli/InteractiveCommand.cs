using System.ComponentModel;
using OpenSeries.Devices;
using Spectre.Console;
using Spectre.Console.Cli;
using DeviceFeatures = OpenSeries.Devices.Features;

namespace OpenSeries.Cli;

[Description("Open an interactive device control menu.")]
internal sealed class InteractiveCommand : Command<InteractiveSettings>
{
    protected override int Execute(CommandContext context, InteractiveSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<IHeadsetDevice> devices = CliSupport.Discover().OfType<IHeadsetDevice>().ToArray();
        if (devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No compatible connected headset was found.[/]");
            return 1;
        }

        IHeadsetDevice device = devices.Count == 1
            ? devices[0]
            : AnsiConsole.Prompt(new SelectionPrompt<IHeadsetDevice>()
                .Title("Select a device")
                .UseConverter(value => $"{value.Name} ({value.Id})")
                .AddChoices(devices));

        int failures = 0;
        while (true)
        {
            var operations = BuildOperations(device);
            string choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold]{Markup.Escape(device.Name)}[/]")
                .AddChoices(operations.Keys.Append("Exit")));
            if (choice == "Exit")
                return failures == 0 ? 0 : 1;
            try
            {
                operations[choice]();
                AnsiConsole.MarkupLine("[green]Operation completed.[/]");
            }
            catch (Exception exception)
            {
                failures++;
                CliSupport.Error(device, exception);
            }
        }
    }

    private static Dictionary<string, Action> BuildOperations(IHeadsetDevice device)
    {
        var operations = new Dictionary<string, Action>();
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.BatteryStatus))
            operations["Battery status"] = () =>
            {
                BatteryInfo battery = device.GetBattery();
                AnsiConsole.MarkupLine($"[bold]{battery.LevelPercentage}%[/] ({battery.Status})");
            };
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Chatmix))
            operations["ChatMix status"] = () =>
            {
                ChatmixInfo value = device.GetChatmix();
                AnsiConsole.MarkupLine($"[bold]{value.Level}/128[/] (game {value.GameVolumePercentage}%, chat {value.ChatVolumePercentage}%)");
            };
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Sidetone))
            operations["Set sidetone"] = () =>
                device.SetSidetone((byte)AnsiConsole.Ask<int>("Level [grey](0-128)[/]:").Clamp(0, 128));
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.InactiveTime))
            operations["Set inactive time"] = () =>
                device.SetInactiveTime((ushort)AnsiConsole.Ask<int>("Minutes [grey](0-90)[/]:").Clamp(0, 90));
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.EqualizerPreset))
            operations["Apply equalizer preset"] = () =>
            {
                EqualizerPreset preset = AnsiConsole.Prompt(new SelectionPrompt<EqualizerPreset>()
                    .Title("Select a preset")
                    .UseConverter(value => value.Name)
                    .AddChoices(device.EqualizerPresets));
                device.SetEqualizerPreset((byte)device.EqualizerPresets.IndexOf(preset));
            };
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Equalizer))
            operations["Set custom equalizer"] = () =>
            {
                string input = AnsiConsole.Ask<string>("10 comma-separated bands:");
                string[] parts = input.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length != device.EqualizerInfo.BandCount ||
                    parts.Any(part => !float.TryParse(part, System.Globalization.CultureInfo.InvariantCulture, out _)))
                    throw new ArgumentException($"Exactly {device.EqualizerInfo.BandCount} numeric bands are required.");
                device.SetEqualizer(parts.Select(part =>
                    float.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray());
            };
        return operations;
    }
}

internal static class NumberExtensions
{
    internal static int Clamp(this int value, int minimum, int maximum)
    {
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value must be between {minimum} and {maximum}.");
        return value;
    }

    internal static int IndexOf<T>(this IReadOnlyList<T> values, T value)
    {
        for (int index = 0; index < values.Count; index++)
            if (EqualityComparer<T>.Default.Equals(values[index], value))
                return index;
        return -1;
    }
}
