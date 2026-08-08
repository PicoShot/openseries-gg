use super::*;

pub(crate) static DEFINITION: DeviceDefinition = DeviceDefinition {
    slug: "arctis-nova-7p",
    product_ids: &[0x220a, 0x22a7],
    rule: MatchRule::VendorControl,
    connect: |identity, transport| {
        Device::Headset(Headset::new(Box::new(Nova7P::new(identity, transport))))
    },
};

pub(crate) struct Nova7P {
    base: DeviceContext,
}
impl Nova7P {
    pub(crate) fn new(identity: Identity, transport: HidTransport) -> Self {
        Self {
            base: DeviceContext::new(identity, transport),
        }
    }
}
impl DeviceProtocol for Nova7P {
    fn id(&self) -> &str {
        &self.base.identity.id
    }
    fn name(&self) -> &str {
        "SteelSeries Arctis Nova 7P"
    }
    fn product_id(&self) -> u16 {
        self.base.identity.product_id
    }
    fn supported_features(&self) -> Capabilities {
        Capabilities::BATTERY_STATUS
            | Capabilities::INACTIVE_TIME
            | Capabilities::EQUALIZER
            | Capabilities::EQUALIZER_PRESET
    }
}
impl HeadsetProtocol for Nova7P {
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
        let level = if self.base.identity.product_id == 0x220a {
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
}
