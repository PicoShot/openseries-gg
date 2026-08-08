# OpenSeries

OpenSeries is a Rust command-line application and reusable library for
discovering and controlling supported SteelSeries HID devices without
SteelSeries GG.

The project began as a Linux-friendly way to check device status and change
settings, and is designed to work across Linux, Windows, and macOS. Human output
is readable in a terminal, while stable JSON output supports scripts and other
integrations.

> [!IMPORTANT]
> OpenSeries is an independent project and is not affiliated with or endorsed
> by SteelSeries.

## Showcase

![OpenSeries terminal showcase](assets/showcase.gif)

## Supported devices

### Headsets

| Device                      | Battery | ChatMix | Sidetone | Inactive time | Equalizer | EQ presets | Advanced controls |
| --------------------------- | ------- | :-----: | :------: | :-----------: | :-------: | :--------: | :---------------: |
| Arctis 7+ variants          | Yes     |   Yes   |   Yes    |      Yes      |    Yes    |    Yes     |        N/A        |
| Arctis Nova 5 / 5X          | Yes     |   Yes   |   Yes    |      Yes      |    Yes    |    Yes     |        Yes        |
| Arctis Nova 7 / 7X variants | Yes     |   Yes   |   Yes    |      Yes      |    Yes    |    Yes     |        Yes        |
| Arctis Nova 7P / 7P v2      | Yes     |   No    |    No    |      Yes      |    Yes    |    Yes     |        N/A        |

### Mice

| Device                    | Battery | DPI presets | Polling rate | RGB zones | Sleep timer |
| ------------------------- | :-----: | :---------: | :----------: | :-------: | :---------: |
| Aerox 3 Wired             |   N/A   |     Yes     |     Yes      |    Yes    |     N/A     |
| Aerox 3 Wireless variants |   Yes   |     Yes     |     Yes      |    Yes    |     Yes     |
| Aerox 5 Wired             |   N/A   |     Yes     |     Yes      |    Yes    |     N/A     |
| Aerox 5 Wireless variants |   Yes   |     Yes     |     Yes      |    Yes    |     Yes     |
| Sensei Ten variants       |   N/A   |     Yes     |     Yes      |    Yes    |     N/A     |

Contributions for additional devices are welcome. Protocol behavior must be
verified against the exact model rather than inferred from a related device.

## Requirements

- The Rust toolchain pinned by `rust-toolchain.toml`
- A supported SteelSeries device
- Permission to access its HID control interface
- On Linux, the development files for `libudev`

## Build

```bash
git clone https://github.com/PicoShot/openseries-gg.git
cd openseries-gg
cargo build --workspace --release --locked
```

The executable is written to `target/release/openseries`.

Run the verification suite:

```bash
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --locked -- -D warnings
```

## Usage

```bash
# use --json for json output
openseries devices list
openseries status
openseries battery
# Override the default 2000 ms device-response timeout
openseries status --timeout-ms 500

# Headsets
openseries headset battery
openseries headset chatmix
openseries headset sidetone 50
openseries headset inactive-time 5
openseries headset equalizer preset 1
openseries headset equalizer set "0,0,0,0,0,0,0,0,0,0"
openseries headset microphone-volume 96
openseries headset microphone-mute-led 2
openseries headset volume-limiter on
openseries headset equalizer parametric "32:3.5:1.414:low-shelf,1000:0:1.0:peaking"
openseries headset bluetooth power-on on
openseries headset bluetooth call-volume lower

# Mice
openseries mouse battery
openseries mouse sensitivity 400,800,1600
openseries mouse polling-rate 1000
openseries mouse color top ff8000
openseries mouse sleep-timer 5

# Interactive controls
openseries interactive
```

Without a device selector, compatible commands operate on every connected
device. Use `--device <id>` to target one device.

## Library usage

The `openseries` library crate contains discovery and protocol logic without
CLI or terminal dependencies:

```rust
use openseries::devices::{Capabilities, Device};
use openseries::discover_devices;

fn main() -> openseries::Result<()> {
    for mut device in discover_devices()? {
        println!("{}: {}", device.name(), device.id());

        if device.capabilities().contains(Capabilities::BATTERY_STATUS) {
            let battery = match &mut device {
                Device::Headset(headset) => headset.get_battery()?,
                Device::Mouse(mouse) => mouse.get_battery()?,
            };
            println!("{}% ({})", battery.level_percentage, battery.status);
        }
    }

    Ok(())
}
```

See [examples](examples/) for complete
programs built on the library.

`discover_devices` keeps each successfully opened HID connection alive for the
lifetime of its returned `Device`. Use `discover_devices_with_options` and
`DiscoveryOptions::with_timeout` when an application needs a response timeout
other than the two-second default.
