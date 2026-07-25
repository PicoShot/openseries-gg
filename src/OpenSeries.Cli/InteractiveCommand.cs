using System.ComponentModel;
using System.Globalization;
using OpenSeries.Devices;
using Spectre.Console;
using Spectre.Console.Cli;
using DeviceFeatures = OpenSeries.Devices.Features;

namespace OpenSeries.Cli;

[Description("Open an interactive device control menu.")]
internal sealed class InteractiveCommand : Command<InteractiveSettings>
{
    private const string SelectAnotherDevice = "Select another device";
    private const string Exit = "Exit";

    protected override int Execute(CommandContext context, InteractiveSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<ISteelSeriesDevice> devices = CliSupport.Discover();
        if (devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No supported SteelSeries device was found.[/]");
            return 1;
        }

        ShowConnectedDevices(devices);
        int failures = 0;
        ISteelSeriesDevice device = SelectDevice(devices);

        while (true)
        {
            AnsiConsole.WriteLine();
            ShowDeviceDetails(device);
            Dictionary<string, Action> operations = BuildOperations(device);
            while (true)
            {
                IEnumerable<string> choices = operations.Keys
                    .Append(devices.Count > 1 ? SelectAnotherDevice : Exit);
                if (devices.Count > 1)
                {
                    choices = choices.Append(Exit);
                }

                string choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("[bold]What would you like to do?[/]")
                    .HighlightStyle(new Style(Color.Aqua))
                    .PageSize(12)
                    .AddChoices(choices));

                if (choice == Exit)
                {
                    return failures == 0 ? 0 : 1;
                }
                if (choice == SelectAnotherDevice)
                {
                    device = SelectDevice(devices);
                    break;
                }

                try
                {
                    AnsiConsole.WriteLine();
                    operations[choice]();
                    if (!choice.StartsWith("Show ", StringComparison.Ordinal))
                    {
                        AnsiConsole.MarkupLine("[green]✓ Operation completed.[/]");
                    }
                }
                catch (Exception exception)
                {
                    failures++;
                    CliSupport.Error(device, exception);
                }
            }
        }
    }

    private static ISteelSeriesDevice SelectDevice(IReadOnlyList<ISteelSeriesDevice> devices) =>
        devices.Count == 1
            ? devices[0]
            : AnsiConsole.Prompt(new SelectionPrompt<ISteelSeriesDevice>()
                .Title("[bold]Select a connected device[/]")
                .HighlightStyle(new Style(Color.Aqua))
                .UseConverter(DeviceLabel)
                .AddChoices(devices));

    private static void ShowConnectedDevices(IReadOnlyList<ISteelSeriesDevice> devices)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Connected SteelSeries devices[/]")
            .AddColumn("Type")
            .AddColumn("Model")
            .AddColumn("Product ID")
            .AddColumn("Available controls");

        foreach (ISteelSeriesDevice device in devices)
        {
            table.AddRow(
                DeviceType(device),
                Markup.Escape(device.Name),
                $"0x{device.ProductId:x4}",
                Markup.Escape(string.Join(", ", FeatureNames(device))));
        }
        AnsiConsole.Write(table);
    }

    private static void ShowDeviceDetails(ISteelSeriesDevice device)
    {
        var details = new Grid().AddColumn().AddColumn();
        details.AddRow("[grey]Type[/]", DeviceType(device));
        details.AddRow("[grey]ID[/]", Markup.Escape(device.Id));
        details.AddRow("[grey]Product ID[/]", $"0x{device.ProductId:x4}");
        AnsiConsole.Write(new Panel(details)
            .Header($"[bold]{Markup.Escape(device.Name)}[/]")
            .Border(BoxBorder.Rounded));

        var features = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Feature")
            .AddColumn("Configuration");
        AddFeatureRows(features, device);
        AnsiConsole.Write(features);
    }

    private static void AddFeatureRows(Table table, ISteelSeriesDevice device)
    {
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.BatteryStatus))
            table.AddRow("Battery", "Read level and charging state");

        if (device is IHeadsetDevice headset)
        {
            if (device.SupportedFeatures.HasFlag(DeviceFeatures.Chatmix))
                table.AddRow("ChatMix", "Read game/chat balance");
            if (device.SupportedFeatures.HasFlag(DeviceFeatures.Sidetone))
                table.AddRow("Sidetone", "Level 0-128");
            if (device.SupportedFeatures.HasFlag(DeviceFeatures.InactiveTime))
                table.AddRow("Inactive time", "0-90 minutes");
            if (device.SupportedFeatures.HasFlag(DeviceFeatures.EqualizerPreset))
                table.AddRow("EQ presets", Markup.Escape(string.Join(", ", headset.EqualizerPresets.Select(preset => preset.Name))));
            if (device.SupportedFeatures.HasFlag(DeviceFeatures.Equalizer))
                table.AddRow("Custom equalizer",
                    $"{headset.EqualizerInfo.BandCount} bands, {headset.EqualizerInfo.Minimum}-{headset.EqualizerInfo.Maximum} dB, step {headset.EqualizerInfo.Step}");
        }

        if (device is IMouseDevice mouse)
        {
            if (device.SupportedFeatures.HasFlag(DeviceFeatures.MouseSensitivity))
                table.AddRow("DPI presets",
                    $"{mouse.SensitivityInfo.Minimum}-{mouse.SensitivityInfo.Maximum}, step {mouse.SensitivityInfo.Step}, up to {mouse.SensitivityInfo.MaximumPresetCount}");
            if (device.SupportedFeatures.HasFlag(DeviceFeatures.PollingRate))
                table.AddRow("Polling rate", $"{string.Join(", ", mouse.SupportedPollingRates)} Hz");
            if (device.SupportedFeatures.HasFlag(DeviceFeatures.Illumination))
                table.AddRow("RGB zones", string.Join(", ", mouse.SupportedIlluminationZones));
            if (device.SupportedFeatures.HasFlag(DeviceFeatures.SleepTimer))
                table.AddRow("Sleep timer", "0-20 minutes");
        }
    }

    private static Dictionary<string, Action> BuildOperations(ISteelSeriesDevice device)
    {
        var operations = new Dictionary<string, Action>();
        if (device is IHeadsetDevice headset)
        {
            AddHeadsetOperations(operations, headset);
        }
        if (device is IMouseDevice mouse)
        {
            AddMouseOperations(operations, mouse);
        }
        return operations;
    }

    private static void AddHeadsetOperations(Dictionary<string, Action> operations, IHeadsetDevice device)
    {
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.BatteryStatus))
            operations["Show battery status"] = () => ShowBattery(device.GetBattery());
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Chatmix))
            operations["Show ChatMix status"] = () =>
            {
                ChatmixInfo value = device.GetChatmix();
                AnsiConsole.MarkupLine(
                    $"[bold]{value.Level}/128[/] · Game {value.GameVolumePercentage}% · Chat {value.ChatVolumePercentage}%");
            };
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Sidetone))
            operations["Set sidetone"] = () =>
                device.SetSidetone((byte)AskInRange("Sidetone level", 0, 128));
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.InactiveTime))
            operations["Set inactive time"] = () =>
                device.SetInactiveTime((ushort)AskInRange("Inactive time in minutes", 0, 90));
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.EqualizerPreset))
            operations["Apply equalizer preset"] = () =>
            {
                EqualizerPreset preset = AnsiConsole.Prompt(new SelectionPrompt<EqualizerPreset>()
                    .Title("Select an equalizer preset")
                    .UseConverter(value => value.Name)
                    .AddChoices(device.EqualizerPresets));
                device.SetEqualizerPreset((byte)device.EqualizerPresets.IndexOf(preset));
            };
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Equalizer))
            operations["Set custom equalizer"] = () =>
            {
                EqualizerInfo info = device.EqualizerInfo;
                string input = AnsiConsole.Ask<string>(
                    $"{info.BandCount} comma-separated bands [grey]({info.Minimum} to {info.Maximum} dB, step {info.Step})[/]:");
                string[] parts = input.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length != info.BandCount ||
                    parts.Any(part => !float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                    throw new ArgumentException($"Exactly {info.BandCount} numeric bands are required.");
                device.SetEqualizer(parts.Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray());
            };
    }

    private static void AddMouseOperations(Dictionary<string, Action> operations, IMouseDevice device)
    {
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.BatteryStatus))
            operations["Show battery status"] = () => ShowBattery(device.GetBattery());
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.MouseSensitivity))
            operations["Set DPI presets"] = () =>
            {
                MouseSensitivityInfo info = device.SensitivityInfo;
                string input = AnsiConsole.Ask<string>(
                    $"Comma-separated DPI presets [grey]({info.Minimum}-{info.Maximum}, step {info.Step}, max {info.MaximumPresetCount})[/]:");
                string[] parts = input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length is < 1 || parts.Length > info.MaximumPresetCount ||
                    parts.Any(part => !ushort.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                    throw new ArgumentException($"Enter between one and {info.MaximumPresetCount} numeric DPI presets.");
                ushort[] presets = parts.Select(part => ushort.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                if (presets.Any(dpi => dpi < info.Minimum || dpi > info.Maximum ||
                    (dpi - info.Minimum) % info.Step != 0))
                    throw new ArgumentOutOfRangeException(nameof(input),
                        $"Each DPI preset must be from {info.Minimum} to {info.Maximum} in steps of {info.Step}.");
                device.SetSensitivity(presets);
            };
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.PollingRate))
            operations["Set polling rate"] = () =>
            {
                ushort rate = AnsiConsole.Prompt(new SelectionPrompt<ushort>()
                    .Title("Select polling rate")
                    .UseConverter(value => $"{value} Hz")
                    .AddChoices(device.SupportedPollingRates));
                device.SetPollingRate(rate);
            };
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Illumination))
            operations["Set RGB zone color"] = () =>
            {
                MouseZone zone = AnsiConsole.Prompt(new SelectionPrompt<MouseZone>()
                    .Title("Select a lighting zone")
                    .UseConverter(value => value.ToString())
                    .AddChoices(device.SupportedIlluminationZones));
                string input = AnsiConsole.Ask<string>("Color [grey](RRGGBB, for example ff8000)[/]:");
                device.SetIllumination(zone, ParseColor(input));
            };
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.SleepTimer))
            operations["Set sleep timer"] = () =>
                device.SetSleepTimer((byte)AskInRange("Sleep timer in minutes", 0, 20));
    }

    private static void ShowBattery(BatteryInfo battery) =>
        AnsiConsole.MarkupLine(CliSupport.BatteryDisplay(
            battery.LevelPercentage, battery.Status.ToString()));

    private static int AskInRange(string label, int minimum, int maximum) =>
        AnsiConsole.Ask<int>($"{label} [grey]({minimum}-{maximum})[/]:").Clamp(minimum, maximum);

    private static RgbColor ParseColor(string value)
    {
        string color = value.Trim().TrimStart('#');
        if (color.Length != 6 ||
            !byte.TryParse(color[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red) ||
            !byte.TryParse(color[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green) ||
            !byte.TryParse(color[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
            throw new ArgumentException("Color must be six hexadecimal digits, for example ff8000.");
        return new RgbColor(red, green, blue);
    }

    private static string DeviceLabel(ISteelSeriesDevice device) =>
        $"{device.Name} · {DeviceType(device)} · 0x{device.ProductId:x4}";

    private static string DeviceType(ISteelSeriesDevice device) => device switch
    {
        IHeadsetDevice => "Headset",
        IMouseDevice => "Mouse",
        _ => "Device"
    };

    private static IEnumerable<string> FeatureNames(ISteelSeriesDevice device)
    {
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.BatteryStatus)) yield return "Battery";
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Chatmix)) yield return "ChatMix";
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Sidetone)) yield return "Sidetone";
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.InactiveTime)) yield return "Inactive time";
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.EqualizerPreset)) yield return "EQ presets";
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Equalizer)) yield return "Equalizer";
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.MouseSensitivity)) yield return "DPI";
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.PollingRate)) yield return "Polling";
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.Illumination)) yield return "RGB";
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.SleepTimer)) yield return "Sleep timer";
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
