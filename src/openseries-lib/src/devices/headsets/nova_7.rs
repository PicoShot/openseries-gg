use super::*;

pub(crate) static DEFINITION: DeviceDefinition = DeviceDefinition {
    slug: "arctis-nova-7",
    product_ids: &[
        0x2202, 0x22a1, 0x227e, 0x2206, 0x2258, 0x229e, 0x22ad, 0x223a, 0x22a9, 0x227a, 0x22a4,
        0x22a5,
    ],
    rule: MatchRule::VendorControl,
    connect: |identity, transport| {
        Device::Headset(Headset::new(Box::new(Nova7::new(identity, transport))))
    },
};

pub(crate) struct Nova7 {
    base: DeviceContext,
}
impl Nova7 {
    pub(crate) fn new(identity: Identity, transport: HidTransport) -> Self {
        Self {
            base: DeviceContext::new(identity, transport),
        }
    }
}
impl DeviceProtocol for Nova7 {
    fn id(&self) -> &str {
        &self.base.identity.id
    }
    fn name(&self) -> &str {
        "SteelSeries Arctis Nova 7"
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
            | Capabilities::BLUETOOTH_WHEN_POWERED_ON
            | Capabilities::BLUETOOTH_CALL_VOLUME
    }
}
impl HeadsetProtocol for Nova7 {
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
    fn get_battery(&mut self) -> Result<BatteryInfo> {
        let data = status(&mut self.base.transport, 6)?;
        if data[3] == 0 {
            return Ok(BatteryInfo {
                level_percentage: 0,
                status: BatteryStatus::Disconnected,
            });
        }
        let discrete =
            [0x2202, 0x2206, 0x223a, 0x227a, 0x22a4].contains(&self.base.identity.product_id);
        let level = if discrete {
            map(i32::from(data[2]), 0, 4, 0, 100)
        } else {
            i32::from(data[2])
        }
        .clamp(0, 100) as u16;
        let state = if matches!(data[3], 1 | 2) {
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
        let data = status(&mut self.base.transport, 6)?;
        Ok(chatmix(&data, 4, 5))
    }
    fn set_sidetone(&mut self, level: u8) -> Result<()> {
        if level > 128 {
            return Err(invalid("Sidetone must be between 0 and 128."));
        }
        let encoded = if level < 32 {
            0
        } else if level < 64 {
            1
        } else if level < 96 {
            2
        } else {
            3
        };
        self.base.transport.write_output(&[0, 0x39, encoded], 0, 64)
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
        validate_eq(bands, -10.0, 10.0)?;
        let mut command = [0_u8; 64];
        command[1] = 0x33;
        for (index, value) in bands.iter().enumerate() {
            command[index + 2] = (20.0 + value) as u8;
        }
        self.base.transport.write_output(&command, 0, 64)
    }
    fn set_microphone_volume(&mut self, volume: u8) -> Result<()> {
        if volume > 128 {
            return Err(invalid("Microphone volume must be between 0 and 128."));
        }
        self.base
            .transport
            .write_output(&[0, 0x37, (volume / 16).min(7)], 0, 64)
    }
    fn set_microphone_mute_led_brightness(&mut self, brightness: u8) -> Result<()> {
        if brightness > 3 {
            return Err(invalid(
                "Microphone mute LED brightness must be between 0 and 3.",
            ));
        }
        self.base
            .transport
            .write_output(&[0, 0xae, brightness], 0, 64)
    }
    fn set_volume_limiter(&mut self, enabled: bool) -> Result<()> {
        self.base
            .transport
            .write_output(&[0, 0x3a, u8::from(enabled)], 0, 64)
    }
    fn set_bluetooth_when_powered_on(&mut self, enabled: bool) -> Result<()> {
        self.base
            .transport
            .write_output(&[0, 0xb2, u8::from(enabled)], 0, 64)?;
        self.base.transport.write_output(&[0x06, 0x09], 0, 64)
    }
    fn set_bluetooth_call_volume(&mut self, mode: BluetoothCallVolumeMode) -> Result<()> {
        self.base
            .transport
            .write_output(&[0, 0xb3, mode as u8], 0, 64)
    }
}
