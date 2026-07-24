using Spectre.Console.Cli;

namespace OpenSeries.Cli;

internal sealed class InteractiveSettings : CommandSettings;

internal class DeviceSettings : CommandSettings
{
    [CommandOption("-d|--device <ID>")]
    public string? Device { get; init; }
}

internal sealed class JsonSettings : CommandSettings
{
    [CommandOption("--json")]
    public bool Json { get; init; }
}

internal sealed class DeviceJsonSettings : DeviceSettings
{
    [CommandOption("--json")]
    public bool Json { get; init; }
}

internal sealed class SidetoneSettings : DeviceSettings
{
    [CommandArgument(0, "<LEVEL>")]
    public int Level { get; init; }
}

internal sealed class InactiveTimeSettings : DeviceSettings
{
    [CommandArgument(0, "<MINUTES>")]
    public int Minutes { get; init; }
}

internal sealed class PresetSettings : DeviceSettings
{
    [CommandArgument(0, "<PRESET>")]
    public string Preset { get; init; } = "";
}

internal sealed class EqualizerSettings : DeviceSettings
{
    [CommandArgument(0, "<BANDS>")]
    public string Bands { get; init; } = "";
}

internal sealed class MouseSensitivitySettings : DeviceSettings
{
    [CommandArgument(0, "<DPI_PRESETS>")]
    public string DpiPresets { get; init; } = "";
}

internal sealed class MousePollingRateSettings : DeviceSettings
{
    [CommandArgument(0, "<HZ>")]
    public int PollingRate { get; init; }
}

internal sealed class MouseColorSettings : DeviceSettings
{
    [CommandArgument(0, "<ZONE>")]
    public string Zone { get; init; } = "";

    [CommandArgument(1, "<RRGGBB>")]
    public string Color { get; init; } = "";
}

internal sealed class MouseSleepTimerSettings : DeviceSettings
{
    [CommandArgument(0, "<MINUTES>")]
    public int Minutes { get; init; }
}
