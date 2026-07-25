using OpenSeries;
using OpenSeries.Devices;

const int BarWidth = 31;
IReadOnlyList<ISteelSeriesDevice> devices = new DeviceManager().GetConnectedDevices();
IHeadsetDevice? headset = devices
    .OfType<IHeadsetDevice>()
    .FirstOrDefault(device => device.SupportedFeatures.HasFlag(Features.Chatmix));

if (headset is null)
{
    foreach (ISteelSeriesDevice device in devices)
        device.Dispose();

    Console.Error.WriteLine("No connected ChatMix-capable headset was found.");
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

Console.WriteLine($"{headset.Name} · turn the ChatMix dial · Ctrl+C to exit");
SetCursorVisible(false);

try
{
    while (!cancellation.IsCancellationRequested)
    {
        try
        {
            ChatmixInfo chatmix = headset.GetChatmix();
            int marker = (int)Math.Round(chatmix.Level / 128d * (BarWidth - 1));
            string bar = string.Create(BarWidth, marker, static (characters, position) =>
            {
                characters.Fill('─');
                characters[position] = '◆';
            });

            WriteCurrentLine(
                $"\e[1;36mGame\e[0m {chatmix.GameVolumePercentage,3}%  " +
                $"[\e[1m{bar}\e[0m]  " +
                $"{chatmix.ChatVolumePercentage,3}% \e[1;35mChat\e[0m");
        }
        catch (Exception exception)
        {
            WriteCurrentLine($"\e[31mWaiting for ChatMix: {exception.Message}\e[0m");
        }

        if (cancellation.Token.WaitHandle.WaitOne(50))
            break;
    }
}
finally
{
    SetCursorVisible(true);
    Console.WriteLine();
    foreach (ISteelSeriesDevice device in devices)
        device.Dispose();
}

return 0;

static void WriteCurrentLine(string value)
{
    Console.Write("\r\e[2K");
    Console.Write(value);
}

static void SetCursorVisible(bool visible)
{
    if (!Console.IsOutputRedirected)
        Console.Write(visible ? "\e[?25h" : "\e[?25l");
}
