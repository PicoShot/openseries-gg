use super::*;

pub(crate) static DEFINITION: DeviceDefinition = DeviceDefinition {
    slug: "arctis-7-plus",
    product_ids: &[0x220e, 0x2212, 0x2216, 0x2236],
    rule: MatchRule::VendorControl,
    connect: |identity, transport| {
        Device::Headset(Headset::new(Box::new(Arctis7Plus::new(
            identity, transport,
        ))))
    },
};

pub(crate) struct Arctis7Plus {
    base: DeviceContext,
}
impl Arctis7Plus {
    pub(crate) fn new(identity: Identity, transport: HidTransport) -> Self {
        Self {
            base: DeviceContext::new(identity, transport),
        }
    }
}
impl DeviceProtocol for Arctis7Plus {
    fn id(&self) -> &str {
        &self.base.identity.id
    }
    fn name(&self) -> &str {
        "SteelSeries Arctis 7+"
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
    }
}
impl HeadsetProtocol for Arctis7Plus {
    fn equalizer_info(&self) -> Result<EqualizerInfo> {
        Ok(EqualizerInfo {
            band_count: 10,
            minimum: -12.0,
            maximum: 12.0,
            step: 0.5,
        })
    }
    fn equalizer_presets(&self) -> Result<&'static [EqualizerPreset]> {
        Ok(&PRESETS_7)
    }
    fn get_battery(&mut self) -> Result<BatteryInfo> {
        let data = status(&mut self.base.transport, 6)?;
        if data[1] == 1 {
            return Ok(BatteryInfo {
                level_percentage: 0,
                status: BatteryStatus::Disconnected,
            });
        }
        let level = u16::from(data[2]).saturating_mul(25).min(100);
        let state = if data[3] == 1 {
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
        let encoded = if level < 26 {
            0
        } else if level < 51 {
            1
        } else if level < 76 {
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
        let bands = PRESETS_7
            .get(preset)
            .ok_or_else(|| invalid("Preset index is out of range."))?
            .bands;
        self.set_equalizer(bands)
    }
    fn set_equalizer(&mut self, bands: &[f32]) -> Result<()> {
        validate_eq(bands, -12.0, 12.0)?;
        let mut command = [0_u8; 13];
        command[1] = 0x33;
        for (index, value) in bands.iter().enumerate() {
            command[index + 2] = (24.0 + value * 2.0).round() as u8;
        }
        self.base.transport.write_output(&command, 0, 64)
    }
}
