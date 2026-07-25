using System.ComponentModel;
using System.Globalization;
using OpenSeries.Devices;
using Spectre.Console;
using Spectre.Console.Cli;
using DeviceFeatures = OpenSeries.Devices.Features;

namespace OpenSeries.Cli;

[Description("List connected supported devices.")]
internal sealed class DevicesListCommand : Command<JsonSettings>
{
    protected override int Execute(CommandContext context, JsonSettings settings, CancellationToken cancellationToken) => Reporters.List(settings.Json);
}

[Description("Show device status and capabilities.")]
internal sealed class StatusCommand : Command<DeviceJsonSettings>
{
    protected override int Execute(CommandContext context, DeviceJsonSettings settings, CancellationToken cancellationToken) =>
        Reporters.Status(settings.Device, settings.Json);
}

[Description("Read headset battery status.")]
internal sealed class BatteryCommand : Command<DeviceJsonSettings>
{
    protected override int Execute(CommandContext context, DeviceJsonSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<IHeadsetDevice> devices = CliSupport.SelectHeadsets(
            settings.Device, DeviceFeatures.BatteryStatus, settings.Json, out int exitCode);
        var results = new List<BatteryJson>();
        foreach (IHeadsetDevice device in devices)
        {
            try
            {
                BatteryInfo battery = device.GetBattery();
                results.Add(new(device.Id, device.Name, battery.LevelPercentage, battery.Status.ToString()));
                if (!settings.Json)
                    AnsiConsole.MarkupLine(
                        $"{Markup.Escape(device.Id)}: {CliSupport.BatteryDisplay(battery.LevelPercentage, battery.Status.ToString())}");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                results.Add(new(device.Id, device.Name, null, null, exception.Message));
                if (!settings.Json) CliSupport.Error(device, exception);
            }
        }
        if (settings.Json)
            CliSupport.Json(results.ToArray(), CliJsonContext.Default.BatteryJsonArray);
        return exitCode;
    }
}

[Description("Read headset ChatMix status.")]
internal sealed class ChatMixCommand : Command<DeviceJsonSettings>
{
    protected override int Execute(CommandContext context, DeviceJsonSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<IHeadsetDevice> devices = CliSupport.SelectHeadsets(
            settings.Device, DeviceFeatures.Chatmix, settings.Json, out int exitCode);
        var results = new List<ChatMixJson>();
        foreach (IHeadsetDevice device in devices)
        {
            try
            {
                ChatmixInfo value = device.GetChatmix();
                results.Add(new(device.Id, device.Name, value.Level, value.GameVolumePercentage, value.ChatVolumePercentage));
                if (!settings.Json)
                    AnsiConsole.MarkupLine($"{Markup.Escape(device.Id)}: [bold]{value.Level}/128[/] (game {value.GameVolumePercentage}%, chat {value.ChatVolumePercentage}%)");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                results.Add(new(device.Id, device.Name, null, null, null, exception.Message));
                if (!settings.Json) CliSupport.Error(device, exception);
            }
        }
        if (settings.Json)
            CliSupport.Json(results.ToArray(), CliJsonContext.Default.ChatMixJsonArray);
        return exitCode;
    }
}

[Description("Read wireless mouse battery status.")]
internal sealed class MouseBatteryCommand : Command<DeviceJsonSettings>
{
    protected override int Execute(CommandContext context, DeviceJsonSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<IMouseDevice> devices = CliSupport.SelectMice(
            settings.Device, DeviceFeatures.BatteryStatus, settings.Json, out int exitCode);
        var results = new List<BatteryJson>();
        foreach (IMouseDevice device in devices)
        {
            try
            {
                BatteryInfo battery = device.GetBattery();
                results.Add(new(device.Id, device.Name, battery.LevelPercentage, battery.Status.ToString()));
                if (!settings.Json)
                    AnsiConsole.MarkupLine(
                        $"{Markup.Escape(device.Id)}: {CliSupport.BatteryDisplay(battery.LevelPercentage, battery.Status.ToString())}");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                results.Add(new(device.Id, device.Name, null, null, exception.Message));
                if (!settings.Json) CliSupport.Error(device, exception);
            }
        }
        if (settings.Json)
            CliSupport.Json(results.ToArray(), CliJsonContext.Default.BatteryJsonArray);
        return exitCode;
    }
}

[Description("Set one to five comma-separated mouse DPI presets.")]
internal sealed class MouseSensitivityCommand : Command<MouseSensitivitySettings>
{
    protected override int Execute(CommandContext context, MouseSensitivitySettings settings, CancellationToken cancellationToken)
    {
        string[] parts = settings.DpiPresets.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 5 ||
            parts.Any(part => !ushort.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            AnsiConsole.MarkupLine("[red]Sensitivity requires one to five comma-separated DPI values.[/]");
            return 1;
        }

        ushort[] presets = parts.Select(part => ushort.Parse(part, CultureInfo.InvariantCulture)).ToArray();
        if (presets.Any(dpi => dpi is < 50 or > 18_000 || dpi % 50 != 0))
        {
            AnsiConsole.MarkupLine("[red]Each DPI preset must be from 50 to 18000 in steps of 50.[/]");
            return 1;
        }

        return MouseSetters.Apply(settings.Device, DeviceFeatures.MouseSensitivity,
            mouse => mouse.SetSensitivity(presets), $"DPI presets set to {string.Join(", ", presets)}.");
    }
}

[Description("Set mouse polling rate (125, 250, 500, or 1000 Hz).")]
internal sealed class MousePollingRateCommand : Command<MousePollingRateSettings>
{
    protected override int Execute(CommandContext context, MousePollingRateSettings settings, CancellationToken cancellationToken)
    {
        if (settings.PollingRate is not (125 or 250 or 500 or 1000))
        {
            AnsiConsole.MarkupLine("[red]Polling rate must be 125, 250, 500, or 1000 Hz.[/]");
            return 1;
        }

        return MouseSetters.Apply(settings.Device, DeviceFeatures.PollingRate,
            mouse => mouse.SetPollingRate((ushort)settings.PollingRate),
            $"Polling rate set to {settings.PollingRate} Hz.");
    }
}

[Description("Set a mouse lighting zone to an RRGGBB color.")]
internal sealed class MouseColorCommand : Command<MouseColorSettings>
{
    protected override int Execute(CommandContext context, MouseColorSettings settings, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(settings.Zone, true, out MouseZone zone))
        {
            AnsiConsole.MarkupLine("[red]Zone must be top, middle, bottom, logo, or wheel.[/]");
            return 1;
        }

        string color = settings.Color.TrimStart('#');
        if (color.Length != 6 ||
            !byte.TryParse(color[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red) ||
            !byte.TryParse(color[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green) ||
            !byte.TryParse(color[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
        {
            AnsiConsole.MarkupLine("[red]Color must be six hexadecimal digits, for example ff8000.[/]");
            return 1;
        }

        return MouseSetters.Apply(settings.Device, DeviceFeatures.Illumination,
            mouse => mouse.SetIllumination(zone, new RgbColor(red, green, blue)),
            $"{zone} lighting set to #{color.ToLowerInvariant()}.");
    }
}

[Description("Set wireless mouse sleep timer in minutes (0-20).")]
internal sealed class MouseSleepTimerCommand : Command<MouseSleepTimerSettings>
{
    protected override int Execute(CommandContext context, MouseSleepTimerSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Minutes is < 0 or > 20)
        {
            AnsiConsole.MarkupLine("[red]Sleep timer must be between 0 and 20 minutes.[/]");
            return 1;
        }

        return MouseSetters.Apply(settings.Device, DeviceFeatures.SleepTimer,
            mouse => mouse.SetSleepTimer((byte)settings.Minutes),
            $"Sleep timer set to {settings.Minutes} minute(s).");
    }
}

[Description("Set headset sidetone (0-128).")]
internal sealed class SidetoneCommand : Command<SidetoneSettings>
{
    protected override int Execute(CommandContext context, SidetoneSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Level is < 0 or > 128)
        {
            AnsiConsole.MarkupLine("[red]Sidetone must be between 0 and 128.[/]");
            return 1;
        }
        return Setters.Apply(settings.Device, DeviceFeatures.Sidetone,
            headset => headset.SetSidetone((byte)settings.Level), $"Sidetone set to {settings.Level}.");
    }
}

[Description("Set headset inactivity timeout in minutes (0-90).")]
internal sealed class InactiveTimeCommand : Command<InactiveTimeSettings>
{
    protected override int Execute(CommandContext context, InactiveTimeSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Minutes is < 0 or > 90)
        {
            AnsiConsole.MarkupLine("[red]Inactive time must be between 0 and 90 minutes.[/]");
            return 1;
        }
        return Setters.Apply(settings.Device, DeviceFeatures.InactiveTime,
            headset => headset.SetInactiveTime((ushort)settings.Minutes),
            $"Inactive time set to {settings.Minutes} minute(s).");
    }
}

[Description("Apply an equalizer preset by index or name.")]
internal sealed class EqualizerPresetCommand : Command<PresetSettings>
{
    protected override int Execute(CommandContext context, PresetSettings settings, CancellationToken cancellationToken)
    {
        return Setters.Apply(settings.Device, DeviceFeatures.EqualizerPreset, headset =>
        {
            int index;
            if (!int.TryParse(settings.Preset, NumberStyles.None, CultureInfo.InvariantCulture, out index))
            {
                index = headset.EqualizerPresets
                    .Select((preset, presetIndex) => (preset, presetIndex))
                    .Where(item => item.preset.Name.Equals(settings.Preset, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.presetIndex)
                    .DefaultIfEmpty(-1)
                    .Single();
            }
            if (index < 0 || index >= headset.EqualizerPresets.Count)
                throw new ArgumentException($"Unknown preset '{settings.Preset}'. Available presets: " +
                    string.Join(", ", headset.EqualizerPresets.Select((preset, i) => $"{i}={preset.Name}")));
            headset.SetEqualizerPreset((byte)index);
        }, $"Equalizer preset '{settings.Preset}' applied.");
    }
}

[Description("Set ten comma-separated equalizer bands.")]
internal sealed class EqualizerSetCommand : Command<EqualizerSettings>
{
    protected override int Execute(CommandContext context, EqualizerSettings settings, CancellationToken cancellationToken)
    {
        string[] parts = settings.Bands.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 10 ||
            parts.Any(part => !float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
        {
            AnsiConsole.MarkupLine("[red]Equalizer requires exactly 10 comma-separated numeric values.[/]");
            return 1;
        }
        float[] bands = parts.Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
        if (bands.Any(value => value is < -12 or > 12 || MathF.Abs(value * 2 - MathF.Round(value * 2)) > 0.0001f))
        {
            AnsiConsole.MarkupLine("[red]Each band must be from -12 to +12 dB in 0.5 dB increments.[/]");
            return 1;
        }
        return Setters.Apply(settings.Device, DeviceFeatures.Equalizer,
            headset => headset.SetEqualizer(bands), "Custom equalizer applied.");
    }
}

internal static class Setters
{
    internal static int Apply(
        string? selector,
        DeviceFeatures feature,
        Action<IHeadsetDevice> operation,
        string success)
    {
        IReadOnlyList<IHeadsetDevice> devices = CliSupport.SelectHeadsets(selector, feature, false, out int exitCode);
        foreach (IHeadsetDevice device in devices)
        {
            try
            {
                operation(device);
                AnsiConsole.MarkupLine($"{Markup.Escape(device.Id)}: [green]{Markup.Escape(success)}[/]");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                CliSupport.Error(device, exception);
            }
        }
        return exitCode;
    }
}

internal static class MouseSetters
{
    internal static int Apply(
        string? selector,
        DeviceFeatures feature,
        Action<IMouseDevice> operation,
        string success)
    {
        IReadOnlyList<IMouseDevice> devices = CliSupport.SelectMice(selector, feature, false, out int exitCode);
        foreach (IMouseDevice device in devices)
        {
            try
            {
                operation(device);
                AnsiConsole.MarkupLine($"{Markup.Escape(device.Id)}: [green]{Markup.Escape(success)}[/]");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                CliSupport.Error(device, exception);
            }
        }
        return exitCode;
    }
}
