use super::*;

pub(crate) static DEFINITION: DeviceDefinition = DeviceDefinition {
    slug: "sensei-ten",
    product_ids: &[0x1832, 0x1834],
    rule: MatchRule::ReportSizes {
        output: 15,
        feature: 36,
    },
    connect: |identity, transport| {
        Device::Mouse(Mouse::new(Box::new(SenseiTen::new(identity, transport))))
    },
};

pub(crate) struct SenseiTen {
    base: DeviceContext,
}
impl SenseiTen {
    pub(crate) fn new(identity: Identity, transport: HidTransport) -> Self {
        Self {
            base: DeviceContext::new(identity, transport),
        }
    }
}
impl DeviceProtocol for SenseiTen {
    fn id(&self) -> &str {
        &self.base.identity.id
    }
    fn name(&self) -> &str {
        if self.base.identity.product_id == 0x1834 {
            "SteelSeries Sensei Ten CS:GO Neon Rider Edition"
        } else {
            "SteelSeries Sensei Ten"
        }
    }
    fn product_id(&self) -> u16 {
        self.base.identity.product_id
    }
    fn supported_features(&self) -> Capabilities {
        Capabilities::MOUSE_SENSITIVITY | Capabilities::POLLING_RATE | Capabilities::ILLUMINATION
    }
}
impl SenseiTen {
    fn output(&mut self, command: &[u8]) -> Result<()> {
        self.base.transport.write_output(command, 1, 0)
    }
    fn output_and_save(&mut self, command: &[u8]) -> Result<()> {
        self.output(command)?;
        thread::sleep(Duration::from_millis(50));
        self.output(&[0x59, 0])
    }
}
impl MouseProtocol for SenseiTen {
    fn sensitivity_info(&self) -> Result<MouseSensitivityInfo> {
        Ok(MouseSensitivityInfo {
            minimum: 50,
            maximum: 18_000,
            step: 50,
            maximum_preset_count: 5,
        })
    }
    fn supported_polling_rates(&self) -> Result<&'static [u16]> {
        Ok(&POLLING_RATES)
    }
    fn supported_illumination_zones(&self) -> Result<&'static [MouseZone]> {
        Ok(&SENSEI_ZONES)
    }
    fn set_sensitivity(&mut self, values: &[u16]) -> Result<()> {
        if !(1..=5).contains(&values.len()) {
            return Err(invalid("Between one and five DPI presets are required."));
        }
        let mut command = vec![0x55, 0, (1_u8 << values.len()) - 1, 1];
        for (index, dpi) in values.iter().copied().enumerate() {
            if !(50..=18_000).contains(&dpi) || !dpi.is_multiple_of(50) {
                return Err(invalid(format!(
                    "DPI preset {} must be from 50 to 18000 in steps of 50.",
                    index + 1
                )));
            }
            command.extend((dpi / 50).to_le_bytes());
        }
        self.output_and_save(&command)
    }
    fn set_polling_rate(&mut self, rate: u16) -> Result<()> {
        let encoded = match rate {
            125 => 4,
            250 => 3,
            500 => 2,
            1000 => 1,
            _ => return Err(invalid("Polling rate must be 125, 250, 500, or 1000 Hz.")),
        };
        self.output_and_save(&[0x54, 0, encoded])
    }
    fn set_illumination(
        &mut self,
        zone: MouseZone,
        color: RgbColor,
        persistence: Persistence,
    ) -> Result<()> {
        let led = match zone {
            MouseZone::Logo => 0,
            MouseZone::Wheel => 1,
            _ => return Err(invalid("Sensei Ten lighting zone must be logo or wheel.")),
        };
        let mut command = [0_u8; 35];
        command[0] = 0x5b;
        command[2] = led;
        command[3] = 0xe8;
        command[4] = 3;
        command[19] = 1;
        command[27] = 1;
        command[28..31].copy_from_slice(&[color.red, color.green, color.blue]);
        command[31..34].copy_from_slice(&[color.red, color.green, color.blue]);
        self.base.transport.write_feature(&command, 1, 36)?;
        if persistence == Persistence::Save {
            thread::sleep(Duration::from_millis(50));
            self.output(&[0x59, 0])
        } else {
            Ok(())
        }
    }
}
