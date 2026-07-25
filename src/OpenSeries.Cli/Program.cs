using OpenSeries.Cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(configuration =>
{
    configuration.SetApplicationName("openseries");
    configuration.SetApplicationVersion(typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0");
    configuration.AddCommand<StatusCommand>("status");
    configuration.AddCommand<InteractiveCommand>("interactive");
    configuration.AddBranch("devices", devices => devices.AddCommand<DevicesListCommand>("list"));
    configuration.AddBranch("headset", headset =>
    {
        headset.AddCommand<BatteryCommand>("battery");
        headset.AddCommand<ChatMixCommand>("chatmix");
        headset.AddCommand<SidetoneCommand>("sidetone");
        headset.AddCommand<InactiveTimeCommand>("inactive-time");
        headset.AddCommand<MicrophoneVolumeCommand>("microphone-volume");
        headset.AddCommand<MicrophoneMuteLedCommand>("microphone-mute-led");
        headset.AddCommand<VolumeLimiterCommand>("volume-limiter");
        headset.AddBranch("equalizer", equalizer =>
        {
            equalizer.AddCommand<EqualizerPresetCommand>("preset");
            equalizer.AddCommand<EqualizerSetCommand>("set");
            equalizer.AddCommand<ParametricEqualizerCommand>("parametric");
        });
    });
    configuration.AddBranch("mouse", mouse =>
    {
        mouse.AddCommand<MouseBatteryCommand>("battery");
        mouse.AddCommand<MouseSensitivityCommand>("sensitivity");
        mouse.AddCommand<MousePollingRateCommand>("polling-rate");
        mouse.AddCommand<MouseColorCommand>("color");
        mouse.AddCommand<MouseSleepTimerCommand>("sleep-timer");
    });
});

if (args.Length == 0)
{
    int status = Reporters.Status(null, false);
    Console.WriteLine();
    app.Run(["--help"]);
    return status;
}

try
{
    return app.Run(args);
}
catch (Exception exception)
{
    CliSupport.Error(null, exception);
    return 1;
}
