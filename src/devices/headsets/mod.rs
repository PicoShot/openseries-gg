use super::*;
use crate::protocol::definition::{DeviceDefinition, MatchRule};
use crate::protocol::{HidTransport, Identity};

const FLAT: [f32; 10] = [0.0; 10];
const BASS_7: [f32; 10] = [3.5, 4.0, 1.0, -1.5, -1.5, -1.0, -1.0, -1.0, -1.0, 5.5];
const SMILEY_7: [f32; 10] = [3.0, 1.5, -1.5, -4.0, -4.0, -2.5, 1.5, 3.0, 4.0, 3.5];
const FOCUS_7: [f32; 10] = [-5.0, -1.0, -3.5, -2.5, 4.0, 6.0, 3.5, -3.5, 0.0, -3.5];
const BASS_NOVA: [f32; 10] = [3.5, 5.5, 4.0, 1.0, -1.5, -1.5, -1.0, -1.0, -1.0, -1.0];
const FOCUS_NOVA: [f32; 10] = [-5.0, -3.5, -1.0, -3.5, -2.5, 4.0, 6.0, -3.5, 0.0, 0.0];
const SMILEY_NOVA: [f32; 10] = [3.0, 3.5, 1.5, -1.5, -4.0, -4.0, -2.5, 1.5, 3.0, 4.0];
const PRESETS_7: [EqualizerPreset; 4] = [
    EqualizerPreset {
        name: "Flat",
        bands: &FLAT,
    },
    EqualizerPreset {
        name: "Bass Boost",
        bands: &BASS_7,
    },
    EqualizerPreset {
        name: "Smiley",
        bands: &SMILEY_7,
    },
    EqualizerPreset {
        name: "Focus",
        bands: &FOCUS_7,
    },
];
const PRESETS_NOVA: [EqualizerPreset; 4] = [
    EqualizerPreset {
        name: "Flat",
        bands: &FLAT,
    },
    EqualizerPreset {
        name: "Bass",
        bands: &BASS_NOVA,
    },
    EqualizerPreset {
        name: "Focus",
        bands: &FOCUS_NOVA,
    },
    EqualizerPreset {
        name: "Smiley",
        bands: &SMILEY_NOVA,
    },
];
const FILTERS: [EqualizerFilterType; 5] = [
    EqualizerFilterType::Peaking,
    EqualizerFilterType::LowPass,
    EqualizerFilterType::HighPass,
    EqualizerFilterType::LowShelf,
    EqualizerFilterType::HighShelf,
];

fn invalid(message: impl Into<String>) -> OpenSeriesError {
    OpenSeriesError::InvalidArgument(message.into())
}
fn protocol(message: impl Into<String>) -> OpenSeriesError {
    OpenSeriesError::Protocol(message.into())
}
fn uses_step(value: f32, step: f32) -> bool {
    ((value / step) - (value / step).round()).abs() <= 0.0001
}
fn map(value: i32, source_min: i32, source_max: i32, target_min: i32, target_max: i32) -> i32 {
    (value - source_min) * (target_max - target_min) / (source_max - source_min) + target_min
}
fn chatmix(data: &[u8], game_index: usize, chat_index: usize) -> ChatmixInfo {
    let game_raw = i32::from(data[game_index]);
    let chat_raw = i32::from(data[chat_index]);
    let game = map(game_raw, 0, 100, 0, 64);
    let chat = map(chat_raw, 0, 100, 0, -64);
    ChatmixInfo {
        level: (64 - (chat + game)).clamp(0, 128) as u16,
        game_volume_percentage: game_raw.clamp(0, 100) as u16,
        chat_volume_percentage: chat_raw.clamp(0, 100) as u16,
    }
}
fn validate_eq(bands: &[f32], min: f32, max: f32) -> Result<()> {
    if bands.len() != 10 {
        return Err(invalid("Exactly 10 equalizer bands are required."));
    }
    for (index, value) in bands.iter().copied().enumerate() {
        if !(min..=max).contains(&value) {
            return Err(invalid(format!(
                "Band {} must be between {min} and {max} dB.",
                index + 1
            )));
        }
        if !uses_step(value, 0.5) {
            return Err(invalid(format!(
                "Band {} must use 0.5 dB increments.",
                index + 1
            )));
        }
    }
    Ok(())
}
fn status(transport: &mut HidTransport, minimum: usize) -> Result<Vec<u8>> {
    let response = transport.write_output_and_read(&[0, 0xb0], 128, 0, 0, Some(0xb0))?;
    if response.len() < minimum {
        return Err(protocol(format!(
            "Device returned a short status response ({} bytes).",
            response.len()
        )));
    }
    Ok(response)
}

mod arctis_7_plus;
mod nova_5;
mod nova_7;
mod nova_7p;

pub(crate) use arctis_7_plus::DEFINITION as ARCTIS_7_PLUS_DEFINITION;
pub(crate) use nova_5::DEFINITION as NOVA_5_DEFINITION;
pub(crate) use nova_7::DEFINITION as NOVA_7_DEFINITION;
pub(crate) use nova_7p::DEFINITION as NOVA_7P_DEFINITION;
