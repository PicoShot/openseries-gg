use crate::commands::{
    discover, error_line, parse_color, parse_floats, parse_parametric, parse_u16s, validation,
};
use openseries::DiscoveryOptions;
use openseries::devices::headsets::{BluetoothCallVolumeMode, Headset};
use openseries::devices::mice::Mouse;
use openseries::devices::{Capabilities, Device, Persistence};

pub(crate) fn run(discovery: DiscoveryOptions) -> i32 {
    let Ok(mut devices) = discover(false, discovery) else {
        return 1;
    };
    if devices.is_empty() {
        return validation("No supported SteelSeries device was found.");
    }
    let labels: Vec<_> = devices
        .iter()
        .map(|d| format!("{} · {} · 0x{:04x}", d.id(), d.name(), d.product_id()))
        .collect();
    let Ok(label) = inquire::Select::new("Select a device", labels.clone()).prompt() else {
        return 1;
    };
    let Some(index) = labels.iter().position(|item| item == &label) else {
        return validation("The selected device is no longer available.");
    };
    let features = devices[index].capabilities();
    println!("{}\n", devices[index].name());
    let mut actions = Vec::new();
    if features.contains(Capabilities::BATTERY_STATUS) {
        actions.push("Show battery status");
    }
    if features.contains(Capabilities::CHATMIX) {
        actions.push("Show ChatMix status");
    }
    if features.contains(Capabilities::SIDETONE) {
        actions.push("Set sidetone");
    }
    if features.contains(Capabilities::INACTIVE_TIME) {
        actions.push("Set inactive time");
    }
    if features.contains(Capabilities::MICROPHONE_VOLUME) {
        actions.push("Set microphone volume");
    }
    if features.contains(Capabilities::MICROPHONE_MUTE_LED_BRIGHTNESS) {
        actions.push("Set microphone mute LED brightness");
    }
    if features.contains(Capabilities::VOLUME_LIMITER) {
        actions.push("Set volume limiter");
    }
    if features.contains(Capabilities::BLUETOOTH_WHEN_POWERED_ON) {
        actions.push("Set Bluetooth at power-on");
    }
    if features.contains(Capabilities::BLUETOOTH_CALL_VOLUME) {
        actions.push("Set Bluetooth call-volume mode");
    }
    if features.contains(Capabilities::EQUALIZER_PRESET) {
        actions.push("Apply equalizer preset");
    }
    if features.contains(Capabilities::EQUALIZER) {
        actions.push("Set graphic equalizer");
    }
    if features.contains(Capabilities::PARAMETRIC_EQUALIZER) {
        actions.push("Set parametric equalizer");
    }
    if features.contains(Capabilities::MOUSE_SENSITIVITY) {
        actions.push("Set mouse sensitivity");
    }
    if features.contains(Capabilities::POLLING_RATE) {
        actions.push("Set polling rate");
    }
    if features.contains(Capabilities::ILLUMINATION) {
        actions.push("Set illumination");
    }
    if features.contains(Capabilities::SLEEP_TIMER) {
        actions.push("Set sleep timer");
    }
    actions.push("Exit");

    loop {
        let Ok(action) = inquire::Select::new("Choose an operation", actions.clone()).prompt()
        else {
            return 1;
        };
        if action == "Exit" {
            break;
        }
        let result = interactive_action(&mut devices[index], action);
        if let Err(error) = result {
            error_line(&error);
        }
    }
    0
}

fn interactive_action(device: &mut Device, action: &str) -> Result<(), String> {
    match action {
        "Show battery status" => {
            let value = match device {
                Device::Headset(value) => value.get_battery(),
                Device::Mouse(value) => value.get_battery(),
            }
            .map_err(|error| error.to_string())?;
            println!("{}% ({})", value.level_percentage, value.status);
        }
        "Show ChatMix status" => {
            let value = headset(device)?
                .get_chatmix()
                .map_err(|error| error.to_string())?;
            println!(
                "{}/128 (game {}%, chat {}%)",
                value.level, value.game_volume_percentage, value.chat_volume_percentage
            );
        }
        "Set sidetone" => {
            let value = prompt_number::<u8>("Sidetone level (0-128)")?;
            headset(device)?
                .set_sidetone(value)
                .map_err(|error| error.to_string())?;
        }
        "Set inactive time" => {
            let value = prompt_number::<u16>("Inactive time in minutes (0-90)")?;
            headset(device)?
                .set_inactive_time(value)
                .map_err(|error| error.to_string())?;
        }
        "Set microphone volume" => {
            let value = prompt_number::<u8>("Microphone volume (0-128)")?;
            headset(device)?
                .set_microphone_volume(value)
                .map_err(|error| error.to_string())?;
        }
        "Set microphone mute LED brightness" => {
            let value = prompt_number::<u8>("Brightness (0-3)")?;
            headset(device)?
                .set_microphone_mute_led_brightness(value)
                .map_err(|error| error.to_string())?;
        }
        "Set volume limiter" => {
            let enabled = inquire::Confirm::new("Enable volume limiter?")
                .prompt()
                .map_err(|error| error.to_string())?;
            headset(device)?
                .set_volume_limiter(enabled)
                .map_err(|error| error.to_string())?;
        }
        "Set Bluetooth at power-on" => {
            let enabled = inquire::Confirm::new("Enable Bluetooth at power-on?")
                .prompt()
                .map_err(|error| error.to_string())?;
            headset(device)?
                .set_bluetooth_when_powered_on(enabled)
                .map_err(|error| error.to_string())?;
        }
        "Set Bluetooth call-volume mode" => {
            let labels = ["unchanged", "lower", "mute-game"];
            let value = inquire::Select::new("Call-volume mode", labels.to_vec())
                .prompt()
                .map_err(|error| error.to_string())?;
            let mode = match value {
                "lower" => BluetoothCallVolumeMode::LowerBy12Decibels,
                "mute-game" => BluetoothCallVolumeMode::MuteGame,
                _ => BluetoothCallVolumeMode::Unchanged,
            };
            headset(device)?
                .set_bluetooth_call_volume(mode)
                .map_err(|error| error.to_string())?;
        }
        "Apply equalizer preset" => {
            let headset = headset(device)?;
            let presets = headset
                .equalizer_presets()
                .map_err(|error| error.to_string())?;
            let labels: Vec<_> = presets.iter().map(|preset| preset.name).collect();
            let selected = inquire::Select::new("Equalizer preset", labels)
                .prompt()
                .map_err(|error| error.to_string())?;
            let index = presets
                .iter()
                .position(|preset| preset.name == selected)
                .ok_or_else(|| "The selected preset is no longer available.".to_owned())?;
            headset
                .set_equalizer_preset(index)
                .map_err(|error| error.to_string())?;
        }
        "Set graphic equalizer" => {
            let encoded = prompt_text("Ten comma-separated EQ bands")?;
            let values = parse_floats(&encoded, 10)
                .ok_or_else(|| "Equalizer requires exactly 10 numeric values.".to_owned())?;
            let values: [f32; 10] = values
                .try_into()
                .map_err(|_| "Equalizer requires exactly 10 numeric values.".to_owned())?;
            headset(device)?
                .set_equalizer(&values)
                .map_err(|error| error.to_string())?;
        }
        "Set parametric equalizer" => {
            let encoded = prompt_text("Bands as frequency:gain:q:filter")?;
            let values = parse_parametric(&encoded)?;
            headset(device)?
                .set_parametric_equalizer(&values)
                .map_err(|error| error.to_string())?;
        }
        "Set mouse sensitivity" => {
            let encoded = prompt_text("One to five comma-separated DPI presets")?;
            let values = parse_u16s(&encoded, 1, 5)
                .ok_or_else(|| "Sensitivity requires one to five DPI values.".to_owned())?;
            mouse(device)?
                .set_sensitivity(&values)
                .map_err(|error| error.to_string())?;
        }
        "Set polling rate" => {
            let selected = inquire::Select::new("Polling rate", vec![125_u16, 250, 500, 1000])
                .prompt()
                .map_err(|error| error.to_string())?;
            mouse(device)?
                .set_polling_rate(selected)
                .map_err(|error| error.to_string())?;
        }
        "Set illumination" => {
            let mouse = mouse(device)?;
            let zones = mouse
                .supported_illumination_zones()
                .map_err(|error| error.to_string())?;
            let zone = inquire::Select::new("Lighting zone", zones.to_vec())
                .prompt()
                .map_err(|error| error.to_string())?;
            let encoded = prompt_text("RRGGBB color")?;
            let color = parse_color(&encoded)
                .ok_or_else(|| "Color must be six hexadecimal digits.".to_owned())?;
            mouse
                .set_illumination(zone, color, Persistence::Save)
                .map_err(|error| error.to_string())?;
        }
        "Set sleep timer" => {
            let minutes = prompt_number::<u8>("Sleep timer in minutes (0-20)")?;
            mouse(device)?
                .set_sleep_timer(minutes)
                .map_err(|error| error.to_string())?;
        }
        _ => return Err("Unknown interactive operation.".into()),
    }
    println!("Done.");
    Ok(())
}

fn headset(device: &mut Device) -> Result<&mut Headset, String> {
    device
        .as_headset_mut()
        .ok_or_else(|| "Selected device is not a headset.".to_owned())
}

fn mouse(device: &mut Device) -> Result<&mut Mouse, String> {
    device
        .as_mouse_mut()
        .ok_or_else(|| "Selected device is not a mouse.".to_owned())
}

fn prompt_text(message: &str) -> Result<String, String> {
    inquire::Text::new(message)
        .prompt()
        .map_err(|error| error.to_string())
}

fn prompt_number<T>(message: &str) -> Result<T, String>
where
    T: std::str::FromStr,
{
    prompt_text(message)?
        .parse()
        .map_err(|_| "Enter a valid number.".to_owned())
}
