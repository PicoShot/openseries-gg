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
