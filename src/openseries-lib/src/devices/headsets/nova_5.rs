use super::*;

pub(crate) static DEFINITION: DeviceDefinition = DeviceDefinition {
    slug: "arctis-nova-5",
    product_ids: &[0x2232, 0x2253],
    rule: MatchRule::VendorControl,
    connect: |identity, transport| {
        Device::Headset(Headset::new(Box::new(Nova5::new(identity, transport))))
    },
};

pub(crate) struct Nova5 {
    base: DeviceContext,
}
impl Nova5 {
    pub(crate) fn new(identity: Identity, transport: HidTransport) -> Self {
        Self {
            base: DeviceContext::new(identity, transport),
        }
    }
}
impl DeviceProtocol for Nova5 {
    fn id(&self) -> &str {
        &self.base.identity.id
    }
    fn name(&self) -> &str {
        "SteelSeries Arctis Nova 5/5X"
    }
    fn product_id(&self) -> u16 {
        self.base.identity.product_id
    }
    fn supported_features(&self) -> Capabilities {
        Capabilities::SIDETONE
            | Capabilities::BATTERY_STATUS
            | Capabilities::CHATMIX
            | Capabilities::INACTIVE_TIME
            | Capabilities::EQUALIZER
            | Capabilities::EQUALIZER_PRESET
            | Capabilities::MICROPHONE_VOLUME
            | Capabilities::MICROPHONE_MUTE_LED_BRIGHTNESS
            | Capabilities::VOLUME_LIMITER
            | Capabilities::PARAMETRIC_EQUALIZER
    }
}
impl Nova5 {
    fn save(&mut self) -> Result<()> {
        self.base.transport.write_output(&[0, 0x09], 0, 64)?;
        self.base.transport.write_output(&[0, 0x35, 1], 0, 64)
    }
    fn write_parametric(
        command: &mut [u8; 64],
        index: usize,
        band: ParametricEqualizerBand,
    ) -> Result<()> {
        let offset = 2 + 6 * index;
        command[offset..offset + 2].copy_from_slice(&band.frequency.to_le_bytes());
        command[offset + 2] = match band.filter {
            EqualizerFilterType::Peaking => 1,
            EqualizerFilterType::LowPass => 2,
            EqualizerFilterType::HighPass => 3,
            EqualizerFilterType::LowShelf => 4,
            EqualizerFilterType::HighShelf => 5,
        };
        command[offset + 3] = (20.0 + band.gain * 2.0).round() as u8;
        let q = (band.q_factor * 1000.0).round() as u16;
        command[offset + 4..offset + 6].copy_from_slice(&q.to_le_bytes());
        Ok(())
    }
}
impl HeadsetProtocol for Nova5 {
    fn equalizer_info(&self) -> Result<EqualizerInfo> {
        Ok(EqualizerInfo {
            band_count: 10,
            minimum: -10.0,
            maximum: 10.0,
            step: 0.5,
        })
    }
    fn equalizer_presets(&self) -> Result<&'static [EqualizerPreset]> {
        Ok(&PRESETS_NOVA)
    }
    fn parametric_equalizer_info(&self) -> Option<ParametricEqualizerInfo> {
        Some(ParametricEqualizerInfo {
            maximum_band_count: 10,
            minimum_frequency: 20,
            maximum_frequency: 20_000,
            minimum_gain: -10.0,
            maximum_gain: 10.0,
            gain_step: 0.5,
            minimum_q_factor: 0.2,
            maximum_q_factor: 10.0,
            supported_filters: &FILTERS,
        })
    }
    fn get_battery(&mut self) -> Result<BatteryInfo> {
        let data = status(&mut self.base.transport, 16)?;
        if data[1] == 2 {
            return Ok(BatteryInfo {
                level_percentage: 0,
                status: BatteryStatus::Disconnected,
            });
        }
        let level = u16::from(data[3]).min(100);
        let state = if data[4] == 1 {
            BatteryStatus::Charging
        } else if level == 100 {
            BatteryStatus::Charged
        } else {
            BatteryStatus::Discharging
        };
        Ok(BatteryInfo {
            level_percentage: level,
            status: state,
        })
    }
    fn get_chatmix(&mut self) -> Result<ChatmixInfo> {
        let data = status(&mut self.base.transport, 7)?;
        Ok(chatmix(&data, 5, 6))
    }
    fn set_sidetone(&mut self, level: u8) -> Result<()> {
        if level > 128 {
            return Err(invalid("Sidetone must be between 0 and 128."));
        }
        let step = 128 / 11;
        let encoded = (1_u8..11)
            .find(|index| level < step * index)
            .map_or(10, |index| index - 1);
        self.base
            .transport
            .write_output(&[0, 0x39, encoded], 0, 64)?;
        self.save()
    }
    fn set_inactive_time(&mut self, minutes: u16) -> Result<()> {
        if minutes > 90 {
            return Err(invalid("Inactive time must be between 0 and 90 minutes."));
        }
        self.base
            .transport
            .write_output(&[0, 0xa3, minutes as u8], 0, 64)
    }
    fn set_equalizer_preset(&mut self, preset: usize) -> Result<()> {
        let bands = PRESETS_NOVA
            .get(preset)
            .ok_or_else(|| invalid("Preset index must be between 0 and 3."))?
            .bands;
        self.set_equalizer(bands)
    }
    fn set_equalizer(&mut self, bands: &[f32]) -> Result<()> {
        const FREQUENCIES: [u16; 10] = [32, 64, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
        validate_eq(bands, -10.0, 10.0)?;
        let mut command = [0_u8; 64];
        command[1] = 0x33;
        for (index, value) in bands.iter().copied().enumerate() {
            let offset = 2 + 6 * index;
            let raw = (20.0 + value * 2.0).round() as u8;
            command[offset..offset + 2].copy_from_slice(&FREQUENCIES[index].to_le_bytes());
            command[offset + 2] = if raw == 20 {
                1
            } else if index == 0 {
                4
            } else if index == 9 {
                5
            } else {
                1
            };
            command[offset + 3] = raw;
            command[offset + 4] = 0x86;
            command[offset + 5] = 0x05;
        }
        self.base.transport.write_output(&command, 0, 64)?;
        self.save()
    }
    fn set_microphone_volume(&mut self, volume: u8) -> Result<()> {
        if volume > 128 {
            return Err(invalid("Microphone volume must be between 0 and 128."));
        }
        self.base
            .transport
            .write_output(&[0, 0x37, (volume / 8).min(15)], 0, 64)?;
        self.save()
    }
    fn set_microphone_mute_led_brightness(&mut self, brightness: u8) -> Result<()> {
        let encoded = match brightness {
            0 => 0,
            1 => 1,
            2 => 4,
            3 => 10,
            _ => {
                return Err(invalid(
                    "Microphone mute LED brightness must be between 0 and 3.",
                ));
            }
        };
        self.base
            .transport
            .write_output(&[0, 0xae, encoded], 0, 64)?;
        self.save()
    }
    fn set_volume_limiter(&mut self, enabled: bool) -> Result<()> {
        self.base
            .transport
            .write_output(&[0, 0x27, u8::from(enabled)], 0, 64)?;
        self.save()
    }
    fn set_parametric_equalizer(&mut self, bands: &[ParametricEqualizerBand]) -> Result<()> {
        if !(1..=10).contains(&bands.len()) {
            return Err(invalid(
                "Between one and 10 parametric equalizer bands are required.",
            ));
        }
        let mut command = [0_u8; 64];
        command[1] = 0x33;
        for (index, band) in bands.iter().copied().enumerate() {
            if !(20..=20_000).contains(&band.frequency) {
                return Err(invalid(format!(
                    "Band {} frequency must be between 20 and 20000 Hz.",
                    index + 1
                )));
            }
            if !(-10.0..=10.0).contains(&band.gain) || !uses_step(band.gain, 0.5) {
                return Err(invalid(format!(
                    "Band {} gain must be between -10 and +10 dB in 0.5 dB increments.",
                    index + 1
                )));
            }
            if !(0.2..=10.0).contains(&band.q_factor) || !uses_step(band.q_factor, 0.001) {
                return Err(invalid(format!(
                    "Band {} Q factor must be between 0.2 and 10.0 in 0.001 increments.",
                    index + 1
                )));
            }
            Self::write_parametric(&mut command, index, band)?;
        }
        for index in bands.len()..10 {
            Self::write_parametric(
                &mut command,
                index,
                ParametricEqualizerBand {
                    frequency: 20_001,
                    gain: 0.0,
                    q_factor: 1.414,
                    filter: EqualizerFilterType::Peaking,
                },
            )?;
        }
        self.base.transport.write_output(&command, 0, 64)?;
        self.save()
    }
}