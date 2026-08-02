use clap::{Args, Parser, Subcommand};

#[derive(Parser)]
#[command(
    name = "openseries",
    version,
    disable_version_flag = true,
    disable_help_subcommand = true
)]
pub(crate) struct Cli {
    #[arg(
        short = 'v',
        long = "version",
        action = clap::ArgAction::Version,
        help = "Prints version information"
    )]
    pub(crate) version: (),
    #[command(subcommand)]
    pub(crate) command: Option<Command>,
}

#[derive(Subcommand)]
pub(crate) enum Command {
    /// Read battery status for all supported devices.
    Battery(JsonArgs),
    /// Show device status and capabilities.
    Status(DeviceJsonArgs),
    /// Open an interactive device control menu.
    Interactive,
    Devices {
        #[command(subcommand)]
        command: DevicesCommand,
    },
    Headset {
        #[command(subcommand)]
        command: HeadsetCommand,
    },
    Mouse {
        #[command(subcommand)]
        command: MouseCommand,
    },
}

#[derive(Subcommand)]
pub(crate) enum DevicesCommand {
    /// List connected supported devices.
    List(JsonArgs),
}

#[derive(Subcommand)]
pub(crate) enum HeadsetCommand {
    /// Read headset battery status.
    Battery(DeviceJsonArgs),
    /// Read headset ChatMix status.
    Chatmix(DeviceJsonArgs),
    /// Set headset sidetone (0-128).
    Sidetone {
        level: i32,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Set headset inactivity timeout in minutes (0-90).
    InactiveTime {
        minutes: i32,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Set headset microphone volume (0-128).
    MicrophoneVolume {
        volume: i32,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Set microphone mute LED brightness (0=off, 1=low, 2=medium, 3=high).
    MicrophoneMuteLed {
        brightness: i32,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Enable or disable the headset volume limiter.
    VolumeLimiter {
        state: String,
        #[command(flatten)]
        device: DeviceArg,
    },
    Bluetooth {
        #[command(subcommand)]
        command: BluetoothCommand,
    },
    Equalizer {
        #[command(subcommand)]
        command: EqualizerCommand,
    },
}

#[derive(Subcommand)]
pub(crate) enum BluetoothCommand {
    /// Enable or disable Bluetooth when the headset powers on.
    PowerOn {
        state: String,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Set Bluetooth call behavior (unchanged, lower, or mute-game).
    CallVolume {
        mode: String,
        #[command(flatten)]
        device: DeviceArg,
    },
}

#[derive(Subcommand)]
pub(crate) enum EqualizerCommand {
    /// Apply an equalizer preset by index or name.
    Preset {
        preset: String,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Set ten comma-separated equalizer bands.
    Set {
        bands: String,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Set one to ten parametric EQ bands as frequency:gain:q:filter.
    Parametric {
        bands: String,
        #[command(flatten)]
        device: DeviceArg,
    },
}

#[derive(Subcommand)]
pub(crate) enum MouseCommand {
    /// Read wireless mouse battery status.
    Battery(DeviceJsonArgs),
    /// Set one to five comma-separated mouse DPI presets.
    Sensitivity {
        dpi_presets: String,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Set mouse polling rate (125, 250, 500, or 1000 Hz).
    PollingRate {
        hz: i32,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Set a mouse lighting zone to an RRGGBB color.
    Color {
        zone: String,
        color: String,
        #[command(flatten)]
        device: DeviceArg,
    },
    /// Set wireless mouse sleep timer in minutes (0-20).
    SleepTimer {
        minutes: i32,
        #[command(flatten)]
        device: DeviceArg,
    },
}

#[derive(Args, Default)]
pub(crate) struct DeviceArg {
    #[arg(short = 'd', long = "device", value_name = "ID")]
    pub(crate) device: Option<String>,
}
#[derive(Args, Default)]
pub(crate) struct JsonArgs {
    #[arg(long)]
    pub(crate) json: bool,
}
#[derive(Args, Default)]
pub(crate) struct DeviceJsonArgs {
    #[command(flatten)]
    pub(crate) device: DeviceArg,
    #[arg(long)]
    pub(crate) json: bool,
}
