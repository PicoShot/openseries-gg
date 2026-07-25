using OpenSeries.Devices;
using Spectre.Console;
using DeviceFeatures = OpenSeries.Devices.Features;

namespace OpenSeries.Cli;

internal static class Reporters
{
    internal static int List(bool json)
    {
        using DeviceSelection<ISteelSeriesDevice> devices = CliSupport.Discover(json);
        var rows = devices.Select(ToDeviceJson).ToArray();
        if (json)
        {
            CliSupport.Json(rows, CliJsonContext.Default.DeviceJsonArray);
        }
        else if (rows.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No supported SteelSeries device was found.[/]");
        }
        else
        {
            var table = new Table().AddColumns("ID", "Model", "PID", "Capabilities");
            foreach (DeviceJson device in rows)
                table.AddRow(
                    Markup.Escape(device.Id), Markup.Escape(device.Model), device.ProductId,
                    Markup.Escape(string.Join(", ", device.Capabilities)));
            AnsiConsole.Write(table);
        }
        return devices.Count == 0 ? 1 : 0;
    }

    internal static int Status(string? selector, bool json)
    {
        using DeviceSelection<ISteelSeriesDevice> discovered = CliSupport.Discover(json);
        IReadOnlyList<ISteelSeriesDevice> devices = discovered;
        if (!string.IsNullOrWhiteSpace(selector))
        {
            devices = devices.Where(device => device.Id == selector).ToArray();
            if (devices.Count != 1)
            {
                if (json) CliSupport.Json(Array.Empty<StatusJson>(), CliJsonContext.Default.StatusJsonArray);
                else AnsiConsole.MarkupLine($"[red]No unique device has ID[/] {Markup.Escape(selector)}.");
                return 1;
            }
        }
        if (devices.Count == 0)
        {
            if (json) CliSupport.Json(Array.Empty<StatusJson>(), CliJsonContext.Default.StatusJsonArray);
            else AnsiConsole.MarkupLine("[red]No supported SteelSeries device was found.[/]");
            return 1;
        }

        int failures = 0;
        var statuses = new List<StatusJson>();
        foreach (ISteelSeriesDevice device in devices)
        {
            BatteryJson? battery = null;
            ChatMixJson? chatMix = null;
            var errors = new List<string>();
            if (device is IHeadsetDevice headset)
            {
                if (device.SupportedFeatures.HasFlag(DeviceFeatures.BatteryStatus))
                {
                    try
                    {
                        BatteryInfo value = headset.GetBattery();
                        battery = new(device.Id, device.Name, value.LevelPercentage, value.Status.ToString());
                    }
                    catch (Exception exception) { errors.Add(exception.Message); failures++; if (!json) CliSupport.Error(device, exception); }
                }
                if (device.SupportedFeatures.HasFlag(DeviceFeatures.Chatmix))
                {
                    try
                    {
                        ChatmixInfo value = headset.GetChatmix();
                        chatMix = new(device.Id, device.Name, value.Level, value.GameVolumePercentage, value.ChatVolumePercentage);
                    }
                    catch (Exception exception) { errors.Add(exception.Message); failures++; if (!json) CliSupport.Error(device, exception); }
                }
            }
            else if (device is IMouseDevice mouse &&
                     device.SupportedFeatures.HasFlag(DeviceFeatures.BatteryStatus))
            {
                try
                {
                    BatteryInfo value = mouse.GetBattery();
                    battery = new(device.Id, device.Name, value.LevelPercentage, value.Status.ToString());
                }
                catch (Exception exception) { errors.Add(exception.Message); failures++; if (!json) CliSupport.Error(device, exception); }
            }
            statuses.Add(new(
                device.Id, device.Name, $"0x{device.ProductId:x4}",
                CapabilityArray(device), battery, chatMix, errors.Count == 0 ? null : string.Join("; ", errors)));
        }

        if (json)
        {
            CliSupport.Json(statuses.ToArray(), CliJsonContext.Default.StatusJsonArray);
        }
        else
        {
            for (int index = 0; index < statuses.Count; index++)
            {
                StatusJson status = statuses[index];
                var grid = new Grid().AddColumn().AddColumn();
                grid.AddRow("[bold]Device ID[/]", Markup.Escape(status.Id));
                grid.AddRow("[bold]Model[/]", Markup.Escape(status.Model));
                grid.AddRow("[bold]PID[/]", status.ProductId);
                grid.AddRow("[bold]Battery[/]", status.Battery is null
                    ? "Capability unavailable"
                    : CliSupport.BatteryDisplay(
                        status.Battery.LevelPercentage!.Value,
                        status.Battery.ChargingState!));
                grid.AddRow("[bold]ChatMix[/]", status.ChatMix is null ? "Capability unavailable" :
                    $"{status.ChatMix.Level}/128 (game {status.ChatMix.GameVolumePercentage}%, chat {status.ChatMix.ChatVolumePercentage}%)");
                grid.AddRow("[bold]Capabilities[/]", Markup.Escape(string.Join(", ", status.Capabilities)));
                if (index < statuses.Count - 1)
                    grid.AddEmptyRow();
                AnsiConsole.Write(grid);
            }
        }
        return failures == 0 ? 0 : 1;
    }

    internal static DeviceJson ToDeviceJson(ISteelSeriesDevice device) =>
        new(device.Id, device.Name, $"0x{device.ProductId:x4}", CapabilityArray(device));

    private static string[] CapabilityArray(ISteelSeriesDevice device)
    {
        var values = Enum.GetValues<DeviceFeatures>()
            .Where(value => value != DeviceFeatures.None && device.SupportedFeatures.HasFlag(value))
            .Select(value => value == DeviceFeatures.Chatmix ? "ChatMix" : value.ToString())
            .ToList();
        if (device is IHeadsetDevice headset && device.SupportedFeatures.HasFlag(DeviceFeatures.Sidetone))
            values.Add("SidetoneRange:0-128");
        if (device is IHeadsetDevice headset2 && device.SupportedFeatures.HasFlag(DeviceFeatures.InactiveTime))
            values.Add("InactiveTimeRange:0-90");
        if (device is IHeadsetDevice headset3 && device.SupportedFeatures.HasFlag(DeviceFeatures.Equalizer))
            values.Add($"Equalizer:{headset3.EqualizerInfo.BandCount} bands,{headset3.EqualizerInfo.Minimum}-{headset3.EqualizerInfo.Maximum} dB,step {headset3.EqualizerInfo.Step}");
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.MicrophoneVolume))
            values.Add("MicrophoneVolumeRange:0-128");
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.MicrophoneMuteLedBrightness))
            values.Add("MicrophoneMuteLedBrightnessRange:0-3");
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.VolumeLimiter))
            values.Add("VolumeLimiter:on,off");
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.BluetoothWhenPoweredOn))
            values.Add("BluetoothWhenPoweredOn:on,off");
        if (device.SupportedFeatures.HasFlag(DeviceFeatures.BluetoothCallVolume))
            values.Add("BluetoothCallVolume:unchanged,lower,mute-game");
        if (device is IHeadsetDevice headset4 &&
            device.SupportedFeatures.HasFlag(DeviceFeatures.ParametricEqualizer) &&
            headset4.ParametricEqualizerInfo is { } parametric)
        {
            values.Add(
                $"ParametricEqualizer:1-{parametric.MaximumBandCount} bands," +
                $"{parametric.MinimumFrequency}-{parametric.MaximumFrequency} Hz," +
                $"{parametric.MinimumGain}-{parametric.MaximumGain} dB");
        }
        if (device is IMouseDevice mouse && device.SupportedFeatures.HasFlag(DeviceFeatures.MouseSensitivity))
            values.Add($"SensitivityRange:{mouse.SensitivityInfo.Minimum}-{mouse.SensitivityInfo.Maximum},step {mouse.SensitivityInfo.Step},max {mouse.SensitivityInfo.MaximumPresetCount} presets");
        if (device is IMouseDevice mouse2 && device.SupportedFeatures.HasFlag(DeviceFeatures.PollingRate))
            values.Add($"PollingRates:{string.Join(",", mouse2.SupportedPollingRates)} Hz");
        return values.ToArray();
    }
}
