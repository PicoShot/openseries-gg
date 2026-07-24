using HidSharp;

namespace OpenSeriesGG.Devices.Headsets;

/**
 * SteelSeries Arctis 7+ Gaming Headset
 *
 * Features:
 * - Sidetone (4 levels)
 * - Battery status (0-4 range)
 * - Chatmix
 * - Inactive time
 * - Equalizer (10 bands, -12 to +12 range)
 */

public sealed class Arctis7Plus : IHeadsetDevice
{
    public string Name => "SteelSeries Arctis 7+";

    public IReadOnlyCollection<int> ProductIds { get; } =
    [
    0x220e, // Arctis 7+
    0x2212, // Arctis 7+ PS5
    0x2216, // Arctis 7+ Xbox
    0x2236  // Arctis 7+ Destiny
    ];

    public uint Usage => 0xffc00001;
    public int WindowsInterfaceNumber => 3;

    const int EQUALIZER_BANDS = 10;
    const float EQUALIZER_BAND_MIN = -12.0f;
    const float EQUALIZER_BAND_MAX = 12.0f;
    const float EQUALIZER_BAND_STEP = 0.5f;
    const sbyte EQUALIZER_BASELINE = 0x18;
    const int EQUALIZER_PRESETS_COUNT = 4;
}
