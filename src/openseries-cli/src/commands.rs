use crate::interactive;
use clap::{CommandFactory, Parser};
use comfy_table::{Table, presets::ASCII_MARKDOWN};
use openseries::devices::headsets::{
    BatteryInfo, BluetoothCallVolumeMode, ChatmixInfo, EqualizerFilterType, Headset,
    ParametricEqualizerBand,
};
use openseries::devices::mice::{Mouse, MouseZone, RgbColor};
use openseries::devices::{Capabilities, Device, Persistence};
use openseries::discover_devices;
use serde::Serialize;
use std::io::{self, Write};

use crate::settings::*;
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct DeviceJson {
    id: String,
    model: String,
    product_id: String,
    capabilities: Vec<String>,
}
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct BatteryJson {
    id: String,
    model: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    level_percentage: Option<u16>,
    #[serde(skip_serializing_if = "Option::is_none")]
    charging_state: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    error: Option<String>,
}
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ChatMixJson {
    id: String,
    model: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    level: Option<u16>,
    #[serde(skip_serializing_if = "Option::is_none")]
    game_volume_percentage: Option<u16>,
    #[serde(skip_serializing_if = "Option::is_none")]
    chat_volume_percentage: Option<u16>,
    #[serde(skip_serializing_if = "Option::is_none")]
    error: Option<String>,
}
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct StatusJson {
    id: String,
    model: String,
    product_id: String,
    capabilities: Vec<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    battery: Option<BatteryJson>,
    #[serde(skip_serializing_if = "Option::is_none")]
    chat_mix: Option<ChatMixJson>,
    #[serde(skip_serializing_if = "Option::is_none")]
    error: Option<String>,
}

pub(crate) fn run() -> i32 {
    let no_arguments = std::env::args_os().len() == 1;
    let cli = Cli::parse();
    if no_arguments {
        let status = run_status(None, false);
        println!();
        let _ = Cli::command().print_help();
        println!();
        status
    } else if let Some(command) = cli.command {
        dispatch(command)
    } else {
        validation("A command is required.")
    }
}

fn dispatch(command: Command) -> i32 {
    match command {
        Command::Battery(args) => run_all_battery(args.json),
        Command::Status(args) => run_status(args.device.device.as_deref(), args.json),
        Command::Interactive => interactive::run(),
        Command::Devices {
            command: DevicesCommand::List(args),
        } => list(args.json),
        Command::Headset { command } => headset(command),
        Command::Mouse { command } => mouse(command),
    }
}

pub(crate) fn discover(quiet: bool) -> std::result::Result<Vec<Device>, i32> {
    discover_devices().map_err(|error| {
        if !quiet {
            error_line(&error.to_string());
        }
        1
    })
}

fn list(json: bool) -> i32 {
    let Ok(devices) = discover(json) else {
        if json {
            write_json(&Vec::<DeviceJson>::new());
        }
        return 1;
    };
    let rows: Vec<_> = devices.iter().map(device_json).collect();
    if json {
        write_json(&rows);
    } else if rows.is_empty() {
        eprintln!("No supported SteelSeries device was found.");
    } else {
        let mut table = Table::new();
        table
            .load_preset(ASCII_MARKDOWN)
            .set_header(["ID", "Model", "PID", "Capabilities"]);
        for row in &rows {
            table.add_row([
                row.id.clone(),
                row.model.clone(),
                row.product_id.clone(),
                row.capabilities.join(", "),
            ]);
        }
        println!("{table}");
    }
    i32::from(rows.is_empty())
}

fn device_json(device: &Device) -> DeviceJson {
    DeviceJson {
        id: device.id().into(),
        model: device.name().into(),
        product_id: format!("0x{:04x}", device.product_id()),
        capabilities: capabilities(device),
    }
}

fn capabilities(device: &Device) -> Vec<String> {
    let features = device.capabilities();
    let mut values: Vec<String> = Capabilities::ALL
        .iter()
        .filter(|(flag, _)| features.contains(*flag))
        .map(|(_, name)| (*name).into())
        .collect();
    if features.contains(Capabilities::SIDETONE) {
        values.push("SidetoneRange:0-128".into());
    }
    if features.contains(Capabilities::INACTIVE_TIME) {
        values.push("InactiveTimeRange:0-90".into());
    }
    if let Device::Headset(headset) = device {
        if features.contains(Capabilities::EQUALIZER)
            && let Ok(info) = headset.equalizer_info()
        {
            values.push(format!(
                "Equalizer:{} bands,{}-{} dB,step {}",
                info.band_count,
                number(info.minimum),
                number(info.maximum),
                number(info.step)
            ));
        }
        if features.contains(Capabilities::PARAMETRIC_EQUALIZER)
            && let Some(info) = headset.parametric_equalizer_info()
        {
            values.push(format!(
                "ParametricEqualizer:1-{} bands,{}-{} Hz,{}-{} dB",
                info.maximum_band_count,
                info.minimum_frequency,
                info.maximum_frequency,
                number(info.minimum_gain),
                number(info.maximum_gain)
            ));
        }
    }
    if features.contains(Capabilities::MICROPHONE_VOLUME) {
        values.push("MicrophoneVolumeRange:0-128".into());
    }
    if features.contains(Capabilities::MICROPHONE_MUTE_LED_BRIGHTNESS) {
        values.push("MicrophoneMuteLedBrightnessRange:0-3".into());
    }
    if features.contains(Capabilities::VOLUME_LIMITER) {
        values.push("VolumeLimiter:on,off".into());
    }
    if features.contains(Capabilities::BLUETOOTH_WHEN_POWERED_ON) {
        values.push("BluetoothWhenPoweredOn:on,off".into());
    }
    if features.contains(Capabilities::BLUETOOTH_CALL_VOLUME) {
        values.push("BluetoothCallVolume:unchanged,lower,mute-game".into());
    }
    if let Device::Mouse(mouse) = device {
        if let Ok(info) = mouse.sensitivity_info() {
            values.push(format!(
                "SensitivityRange:{}-{},step {},max {} presets",
                info.minimum, info.maximum, info.step, info.maximum_preset_count
            ));
        }
        if let Ok(rates) = mouse.supported_polling_rates() {
            values.push(format!(
                "PollingRates:{} Hz",
                rates
                    .iter()
                    .map(u16::to_string)
                    .collect::<Vec<_>>()
                    .join(",")
            ));
        }
    }
    values
}

fn run_status(selector: Option<&str>, json: bool) -> i32 {
    let Ok(mut devices) = discover(json) else {
        if json {
            write_json(&Vec::<StatusJson>::new());
        }
        return 1;
    };
    if let Some(id) = selector {
        devices.retain(|device| device.id() == id);
        if devices.len() != 1 {
            if json {
                write_json(&Vec::<StatusJson>::new());
            } else {
                error_line(&format!("No unique device has ID {id}."));
            }
            return 1;
        }
    }
    if devices.is_empty() {
        if json {
            write_json(&Vec::<StatusJson>::new());
        } else {
            error_line("No supported SteelSeries device was found.");
        }
        return 1;
    }
    let mut failures = 0;
    let mut statuses = Vec::with_capacity(devices.len());
    for device in &mut devices {
        let id = device.id().to_owned();
        let model = device.name().to_owned();
        let product_id = format!("0x{:04x}", device.product_id());
        let caps = capabilities(device);
        let features = device.capabilities();
        let mut errors = Vec::new();
        let mut battery = None;
        let mut mix = None;
        match device {
            Device::Headset(headset) => {
                if features.contains(Capabilities::BATTERY_STATUS) {
                    match headset.get_battery() {
                        Ok(value) => battery = Some(battery_json(&id, &model, value)),
                        Err(e) => {
                            failures += 1;
                            errors.push(e.to_string());
                            if !json {
                                error_line(&format!("{id}: {e}"));
                            }
                        }
                    }
                }
                if features.contains(Capabilities::CHATMIX) {
                    match headset.get_chatmix() {
                        Ok(value) => {
                            mix = Some(ChatMixJson {
                                id: id.clone(),
                                model: model.clone(),
                                level: Some(value.level),
                                game_volume_percentage: Some(value.game_volume_percentage),
                                chat_volume_percentage: Some(value.chat_volume_percentage),
                                error: None,
                            })
                        }
                        Err(e) => {
                            failures += 1;
                            errors.push(e.to_string());
                            if !json {
                                error_line(&format!("{id}: {e}"));
                            }
                        }
                    }
                }
            }
            Device::Mouse(mouse) if features.contains(Capabilities::BATTERY_STATUS) => {
                match mouse.get_battery() {
                    Ok(value) => battery = Some(battery_json(&id, &model, value)),
                    Err(e) => {
                        failures += 1;
                        errors.push(e.to_string());
                        if !json {
                            error_line(&format!("{id}: {e}"));
                        }
                    }
                }
            }
            _ => {}
        }
        statuses.push(StatusJson {
            id,
            model,
            product_id,
            capabilities: caps,
            battery,
            chat_mix: mix,
            error: (!errors.is_empty()).then(|| errors.join("; ")),
        });
    }
    if json {
        write_json(&statuses);
    } else {
        for status in &statuses {
            println!("Device ID    {}", status.id);
            println!("Model        {}", status.model);
            println!("PID          {}", status.product_id);
            if let Some(battery) = &status.battery {
                println!(
                    "Battery      {}% ({})",
                    battery.level_percentage.unwrap_or_default(),
                    battery.charging_state.as_deref().unwrap_or("Unknown")
                );
            }
            if let Some(mix) = &status.chat_mix {
                println!(
                    "ChatMix      {}/128 (game {}%, chat {}%)",
                    mix.level.unwrap_or_default(),
                    mix.game_volume_percentage.unwrap_or_default(),
                    mix.chat_volume_percentage.unwrap_or_default()
                );
            }
            println!("Capabilities {}", status.capabilities.join(", "));
            println!();
        }
    }
    i32::from(failures != 0)
}

fn run_all_battery(json: bool) -> i32 {
    let Ok(mut devices) = discover(json) else {
        return 1;
    };
    let mut rows = Vec::new();
    let mut failed = false;
    for device in &mut devices {
        if !device.capabilities().contains(Capabilities::BATTERY_STATUS) {
            continue;
        }
        let id = device.id().to_owned();
        let model = device.name().to_owned();
        let result = match device {
            Device::Headset(value) => value.get_battery(),
            Device::Mouse(value) => value.get_battery(),
        };
        match result {
            Ok(value) => rows.push(battery_json(&id, &model, value)),
            Err(error) => {
                failed = true;
                rows.push(BatteryJson {
                    id: id.clone(),
                    model,
                    level_percentage: None,
                    charging_state: None,
                    error: Some(error.to_string()),
                });
                if !json {
                    error_line(&format!("{id}: {error}"));
                }
            }
        }
    }
    if json {
        write_json(&rows);
    } else {
        for row in &rows {
            if let (Some(level), Some(state)) =
                (row.level_percentage, row.charging_state.as_deref())
            {
                println!("{}: {}% ({state}) [{}]", row.id, level, battery_bar(level));
            }
        }
    }
    i32::from(failed || rows.is_empty())
}

fn headset(command: HeadsetCommand) -> i32 {
    match command {
        HeadsetCommand::Battery(args) => headset_read(
            args.device.device.as_deref(),
            args.json,
            Capabilities::BATTERY_STATUS,
            |device| device.get_battery().map(ReadValue::Battery),
        ),
        HeadsetCommand::Chatmix(args) => headset_read(
            args.device.device.as_deref(),
            args.json,
            Capabilities::CHATMIX,
            |device| device.get_chatmix().map(ReadValue::Chatmix),
        ),
        HeadsetCommand::Sidetone { level, device } => {
            if !(0..=128).contains(&level) {
                return validation("Sidetone must be between 0 and 128.");
            }
            headset_set(device.device.as_deref(), Capabilities::SIDETONE, |value| {
                value.set_sidetone(level as u8)
            })
        }
        HeadsetCommand::InactiveTime { minutes, device } => {
            if !(0..=90).contains(&minutes) {
                return validation("Inactive time must be between 0 and 90 minutes.");
            }
            headset_set(
                device.device.as_deref(),
                Capabilities::INACTIVE_TIME,
                |value| value.set_inactive_time(minutes as u16),
            )
        }
        HeadsetCommand::MicrophoneVolume { volume, device } => {
            if !(0..=128).contains(&volume) {
                return validation("Microphone volume must be between 0 and 128.");
            }
            headset_set(
                device.device.as_deref(),
                Capabilities::MICROPHONE_VOLUME,
                |value| value.set_microphone_volume(volume as u8),
            )
        }
        HeadsetCommand::MicrophoneMuteLed { brightness, device } => {
            if !(0..=3).contains(&brightness) {
                return validation("Microphone mute LED brightness must be between 0 and 3.");
            }
            headset_set(
                device.device.as_deref(),
                Capabilities::MICROPHONE_MUTE_LED_BRIGHTNESS,
                |value| value.set_microphone_mute_led_brightness(brightness as u8),
            )
        }
        HeadsetCommand::VolumeLimiter { state, device } => {
            let Some(enabled) = parse_bool(&state) else {
                return validation("State must be on or off.");
            };
            headset_set(
                device.device.as_deref(),
                Capabilities::VOLUME_LIMITER,
                |value| value.set_volume_limiter(enabled),
            )
        }
        HeadsetCommand::Bluetooth { command } => match command {
            BluetoothCommand::PowerOn { state, device } => {
                let Some(enabled) = parse_bool(&state) else {
                    return validation("State must be on or off.");
                };
                headset_set(
                    device.device.as_deref(),
                    Capabilities::BLUETOOTH_WHEN_POWERED_ON,
                    |value| value.set_bluetooth_when_powered_on(enabled),
                )
            }
            BluetoothCommand::CallVolume { mode, device } => {
                let mode = match mode.to_ascii_lowercase().as_str() {
                    "unchanged" => BluetoothCallVolumeMode::Unchanged,
                    "lower" => BluetoothCallVolumeMode::LowerBy12Decibels,
                    "mute-game" => BluetoothCallVolumeMode::MuteGame,
                    _ => return validation("Mode must be unchanged, lower, or mute-game."),
                };
                headset_set(
                    device.device.as_deref(),
                    Capabilities::BLUETOOTH_CALL_VOLUME,
                    |value| value.set_bluetooth_call_volume(mode),
                )
            }
        },
        HeadsetCommand::Equalizer { command } => equalizer(command),
    }
}

enum ReadValue {
    Battery(BatteryInfo),
    Chatmix(ChatmixInfo),
}

fn headset_read(
    selector: Option<&str>,
    json: bool,
    feature: Capabilities,
    mut operation: impl FnMut(&mut Headset) -> openseries::Result<ReadValue>,
) -> i32 {
    let Ok(mut devices) = discover(json) else {
        return 1;
    };
    let indexes = selected(&devices, selector, feature, DeviceCategory::Headset, json);
    let mut failed = indexes.is_empty();
    let mut batteries = Vec::new();
    let mut mixes = Vec::new();
    for index in indexes {
        let id = devices[index].id().to_owned();
        let model = devices[index].name().to_owned();
        let result = devices[index]
            .as_headset_mut()
            .map(&mut operation)
            .ok_or_else(|| {
                openseries::OpenSeriesError::Protocol("internal headset selection mismatch".into())
            })
            .and_then(std::convert::identity);
        match result {
            Ok(ReadValue::Battery(value)) => batteries.push(battery_json(&id, &model, value)),
            Ok(ReadValue::Chatmix(value)) => mixes.push(ChatMixJson {
                id: id.clone(),
                model,
                level: Some(value.level),
                game_volume_percentage: Some(value.game_volume_percentage),
                chat_volume_percentage: Some(value.chat_volume_percentage),
                error: None,
            }),
            Err(error) => {
                failed = true;
                if json {
                    if feature == Capabilities::CHATMIX {
                        mixes.push(ChatMixJson {
                            id,
                            model,
                            level: None,
                            game_volume_percentage: None,
                            chat_volume_percentage: None,
                            error: Some(error.to_string()),
                        });
                    } else {
                        batteries.push(BatteryJson {
                            id,
                            model,
                            level_percentage: None,
                            charging_state: None,
                            error: Some(error.to_string()),
                        });
                    }
                } else {
                    error_line(&format!("{id}: {error}"));
                }
            }
        }
    }
    if json {
        if feature == Capabilities::CHATMIX {
            write_json(&mixes);
        } else {
            write_json(&batteries);
        }
    } else {
        for value in batteries {
            println!(
                "{}: {}% ({})",
                value.id,
                value.level_percentage.unwrap_or_default(),
                value.charging_state.as_deref().unwrap_or("Unknown")
            );
        }
        for value in mixes {
            println!(
                "{}: {}/128 (game {}%, chat {}%)",
                value.id,
                value.level.unwrap_or_default(),
                value.game_volume_percentage.unwrap_or_default(),
                value.chat_volume_percentage.unwrap_or_default()
            );
        }
    }
    i32::from(failed)
}

fn headset_set(
    selector: Option<&str>,
    feature: Capabilities,
    mut operation: impl FnMut(&mut Headset) -> openseries::Result<()>,
) -> i32 {
    let Ok(mut devices) = discover(false) else {
        return 1;
    };
    let indexes = selected(&devices, selector, feature, DeviceCategory::Headset, false);
    if indexes.is_empty() {
        return 1;
    }
    let mut failed = false;
    for index in indexes {
        let id = devices[index].id().to_owned();
        let result = devices[index]
            .as_headset_mut()
            .map(&mut operation)
            .ok_or_else(|| {
                openseries::OpenSeriesError::Protocol("internal headset selection mismatch".into())
            })
            .and_then(std::convert::identity);
        if let Err(error) = result {
            failed = true;
            error_line(&format!("{id}: {error}"));
        } else {
            println!("{id}: Done.");
        }
    }
    i32::from(failed)
}

fn equalizer(command: EqualizerCommand) -> i32 {
    match command {
        EqualizerCommand::Preset { preset, device } => headset_set(
            device.device.as_deref(),
            Capabilities::EQUALIZER_PRESET,
            |headset| {
                let presets = headset.equalizer_presets()?;
                let index = preset.parse::<usize>().ok().or_else(|| {
                    presets
                        .iter()
                        .position(|value| value.name.eq_ignore_ascii_case(&preset))
                });
                let index = index.ok_or_else(|| {
                    openseries::OpenSeriesError::InvalidArgument(
                        "Preset must be a valid index or name.".into(),
                    )
                })?;
                headset.set_equalizer_preset(index)
            },
        ),
        EqualizerCommand::Set { bands, device } => {
            let Some(values) = parse_floats(&bands, 10) else {
                return validation("Equalizer requires exactly 10 comma-separated numeric values.");
            };
            let Ok(values) = <Vec<f32> as TryInto<[f32; 10]>>::try_into(values) else {
                return validation("Equalizer requires exactly 10 comma-separated numeric values.");
            };
            headset_set(
                device.device.as_deref(),
                Capabilities::EQUALIZER,
                |headset| headset.set_equalizer(&values),
            )
        }
        EqualizerCommand::Parametric { bands, device } => {
            let values = match parse_parametric(&bands) {
                Ok(values) => values,
                Err(error) => return validation(&error),
            };
            headset_set(
                device.device.as_deref(),
                Capabilities::PARAMETRIC_EQUALIZER,
                |headset| headset.set_parametric_equalizer(&values),
            )
        }
    }
}

fn mouse(command: MouseCommand) -> i32 {
    match command {
        MouseCommand::Battery(args) => {
            let Ok(mut devices) = discover(args.json) else {
                return 1;
            };
            let indexes = selected(
                &devices,
                args.device.device.as_deref(),
                Capabilities::BATTERY_STATUS,
                DeviceCategory::Mouse,
                args.json,
            );
            let mut rows = Vec::new();
            let mut failed = indexes.is_empty();
            for index in indexes {
                let id = devices[index].id().to_owned();
                let model = devices[index].name().to_owned();
                let result = devices[index]
                    .as_mouse_mut()
                    .ok_or_else(|| {
                        openseries::OpenSeriesError::Protocol(
                            "internal mouse selection mismatch".into(),
                        )
                    })
                    .and_then(Mouse::get_battery);
                match result {
                    Ok(value) => rows.push(battery_json(&id, &model, value)),
                    Err(error) => {
                        failed = true;
                        if args.json {
                            rows.push(BatteryJson {
                                id,
                                model,
                                level_percentage: None,
                                charging_state: None,
                                error: Some(error.to_string()),
                            });
                        } else {
                            error_line(&format!("{id}: {error}"));
                        }
                    }
                }
            }
            if args.json {
                write_json(&rows);
            } else {
                for row in rows {
                    println!(
                        "{}: {}% ({})",
                        row.id,
                        row.level_percentage.unwrap_or_default(),
                        row.charging_state.as_deref().unwrap_or("Unknown")
                    );
                }
            }
            i32::from(failed)
        }
        MouseCommand::Sensitivity {
            dpi_presets,
            device,
        } => {
            let Some(values) = parse_u16s(&dpi_presets, 1, 5) else {
                return validation("Sensitivity requires one to five comma-separated DPI values.");
            };
            mouse_set(
                device.device.as_deref(),
                Capabilities::MOUSE_SENSITIVITY,
                |mouse| mouse.set_sensitivity(&values),
            )
        }
        MouseCommand::PollingRate { hz, device } => {
            if ![125, 250, 500, 1000].contains(&hz) {
                return validation("Polling rate must be 125, 250, 500, or 1000 Hz.");
            }
            mouse_set(
                device.device.as_deref(),
                Capabilities::POLLING_RATE,
                |mouse| mouse.set_polling_rate(hz as u16),
            )
        }
        MouseCommand::Color {
            zone,
            color,
            device,
        } => {
            let Some(zone) = parse_zone(&zone) else {
                return validation("Zone must be top, middle, bottom, logo, or wheel.");
            };
            let Some(color) = parse_color(&color) else {
                return validation("Color must be six hexadecimal digits, for example ff8000.");
            };
            mouse_set(
                device.device.as_deref(),
                Capabilities::ILLUMINATION,
                |mouse| mouse.set_illumination(zone, color, Persistence::Save),
            )
        }
        MouseCommand::SleepTimer { minutes, device } => {
            if !(0..=20).contains(&minutes) {
                return validation("Sleep timer must be between 0 and 20 minutes.");
            }
            mouse_set(
                device.device.as_deref(),
                Capabilities::SLEEP_TIMER,
                |mouse| mouse.set_sleep_timer(minutes as u8),
            )
        }
    }
}

fn mouse_set(
    selector: Option<&str>,
    feature: Capabilities,
    mut operation: impl FnMut(&mut Mouse) -> openseries::Result<()>,
) -> i32 {
    let Ok(mut devices) = discover(false) else {
        return 1;
    };
    let indexes = selected(&devices, selector, feature, DeviceCategory::Mouse, false);
    if indexes.is_empty() {
        return 1;
    }
    let mut failed = false;
    for index in indexes {
        let id = devices[index].id().to_owned();
        let result = devices[index]
            .as_mouse_mut()
            .map(&mut operation)
            .ok_or_else(|| {
                openseries::OpenSeriesError::Protocol("internal mouse selection mismatch".into())
            })
            .and_then(std::convert::identity);
        if let Err(error) = result {
            failed = true;
            error_line(&format!("{id}: {error}"));
        } else {
            println!("{id}: Done.");
        }
    }
    i32::from(failed)
}

#[derive(Clone, Copy)]
enum DeviceCategory {
    Headset,
    Mouse,
}

fn selected(
    devices: &[Device],
    selector: Option<&str>,
    feature: Capabilities,
    category: DeviceCategory,
    quiet: bool,
) -> Vec<usize> {
    let exact: Vec<_> = devices
        .iter()
        .enumerate()
        .filter(|(_, device)| selector.is_none_or(|id| device.id() == id))
        .map(|(index, _)| index)
        .collect();
    if selector.is_some() && exact.len() != 1 {
        if !quiet {
            error_line(if exact.is_empty() {
                "No device has that ID."
            } else {
                "Device ID is ambiguous."
            });
        }
        return Vec::new();
    }
    let matches: Vec<_> = exact
        .into_iter()
        .filter(|index| {
            matches!(
                (&devices[*index], category),
                (Device::Headset(_), DeviceCategory::Headset)
                    | (Device::Mouse(_), DeviceCategory::Mouse)
            ) && devices[*index].capabilities().contains(feature)
        })
        .collect();
    if matches.is_empty() && !quiet {
        error_line(match category {
            DeviceCategory::Headset => "No compatible connected headset was found.",
            DeviceCategory::Mouse => "No compatible connected mouse was found.",
        });
    }
    matches
}

fn battery_json(id: &str, model: &str, value: BatteryInfo) -> BatteryJson {
    BatteryJson {
        id: id.into(),
        model: model.into(),
        level_percentage: Some(value.level_percentage),
        charging_state: Some(value.status.to_string()),
        error: None,
    }
}

fn battery_bar(level: u16) -> String {
    format!(
        "{}{}",
        "=".repeat(usize::from(level.min(100) / 10)),
        " ".repeat(10 - usize::from(level.min(100) / 10))
    )
}

fn write_json<T: Serialize>(value: &T) {
    let stdout = io::stdout();
    let mut out = stdout.lock();
    serde_json::to_writer_pretty(&mut out, value).expect("serialize JSON");
    writeln!(out).expect("write JSON");
}

pub(crate) fn error_line(message: &str) {
    eprintln!("{message}");
}

pub(crate) fn validation(message: &str) -> i32 {
    error_line(message);
    1
}

fn parse_bool(value: &str) -> Option<bool> {
    match value.to_ascii_lowercase().as_str() {
        "on" | "enabled" | "true" | "1" => Some(true),
        "off" | "disabled" | "false" | "0" => Some(false),
        _ => None,
    }
}

pub(crate) fn parse_floats(value: &str, count: usize) -> Option<Vec<f32>> {
    let values: Vec<_> = value
        .split(',')
        .map(str::trim)
        .map(str::parse)
        .collect::<Result<_, _>>()
        .ok()?;
    (values.len() == count).then_some(values)
}

pub(crate) fn parse_u16s(value: &str, min: usize, max: usize) -> Option<Vec<u16>> {
    let values: Vec<_> = value
        .split(',')
        .map(str::trim)
        .filter(|v| !v.is_empty())
        .map(str::parse)
        .collect::<Result<_, _>>()
        .ok()?;
    (min..=max).contains(&values.len()).then_some(values)
}

fn parse_zone(value: &str) -> Option<MouseZone> {
    match value.to_ascii_lowercase().as_str() {
        "top" => Some(MouseZone::Top),
        "middle" => Some(MouseZone::Middle),
        "bottom" => Some(MouseZone::Bottom),
        "logo" => Some(MouseZone::Logo),
        "wheel" => Some(MouseZone::Wheel),
        _ => None,
    }
}

pub(crate) fn parse_color(value: &str) -> Option<RgbColor> {
    let value = value.trim_start_matches('#');
    if value.len() != 6 {
        return None;
    }
    Some(RgbColor {
        red: u8::from_str_radix(&value[0..2], 16).ok()?,
        green: u8::from_str_radix(&value[2..4], 16).ok()?,
        blue: u8::from_str_radix(&value[4..6], 16).ok()?,
    })
}

fn step(value: f32, size: f32) -> bool {
    ((value / size) - (value / size).round()).abs() <= 0.0001
}

fn number(value: f32) -> String {
    if value.fract() == 0.0 {
        format!("{}", value as i32)
    } else {
        format!("{value}")
    }
}

pub(crate) fn parse_parametric(value: &str) -> Result<Vec<ParametricEqualizerBand>, String> {
    let encoded: Vec<_> = value
        .split(',')
        .map(str::trim)
        .filter(|item| !item.is_empty())
        .collect();
    if !(1..=10).contains(&encoded.len()) {
        return Err("Parametric EQ requires between one and ten bands.".into());
    }
    let mut bands = Vec::new();
    for (index, item) in encoded.iter().enumerate() {
        let fields: Vec<_> = item.split(':').map(str::trim).collect();
        if fields.len() != 4 {
            return Err(format!(
                "Band {} must use frequency:gain:q:filter.",
                index + 1
            ));
        }
        let invalid = || format!("Band {} must use frequency:gain:q:filter.", index + 1);
        let frequency = fields[0].parse::<u16>().map_err(|_| invalid())?;
        let gain = fields[1].parse::<f32>().map_err(|_| invalid())?;
        let q_factor = fields[2].parse::<f32>().map_err(|_| invalid())?;
        let filter = match fields[3].to_ascii_lowercase().as_str() {
            "peaking" | "peak" => EqualizerFilterType::Peaking,
            "low-pass" | "lowpass" => EqualizerFilterType::LowPass,
            "high-pass" | "highpass" => EqualizerFilterType::HighPass,
            "low-shelf" | "lowshelf" => EqualizerFilterType::LowShelf,
            "high-shelf" | "highshelf" => EqualizerFilterType::HighShelf,
            _ => return Err(invalid()),
        };
        if !(20..=20_000).contains(&frequency) {
            return Err(format!(
                "Band {} frequency must be between 20 and 20000 Hz.",
                index + 1
            ));
        }
        if !(-10.0..=10.0).contains(&gain) || !step(gain, 0.5) {
            return Err(format!(
                "Band {} gain must be between -10 and +10 dB in 0.5 dB increments.",
                index + 1
            ));
        }
        if !(0.2..=10.0).contains(&q_factor) || !step(q_factor, 0.001) {
            return Err(format!(
                "Band {} Q factor must be between 0.2 and 10.0 in 0.001 increments.",
                index + 1
            ));
        }
        bands.push(ParametricEqualizerBand {
            frequency,
            gain,
            q_factor,
            filter,
        });
    }
    Ok(bands)
}
