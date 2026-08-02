use super::*;

pub(crate) struct Aerox {
    base: DeviceContext,
    family: AeroxFamily,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(crate) enum AeroxFamily {
    Three,
    Five,
}

impl AeroxFamily {
    fn number(self) -> u8 {
        match self {
            Self::Three => 3,
            Self::Five => 5,
        }
    }
}

impl Aerox {
    pub(crate) fn new(identity: Identity, transport: HidTransport, family: AeroxFamily) -> Self {
        Self {
            base: DeviceContext::new(identity, transport),
            family,
        }
    }
    fn receiver(&self) -> bool {
        if self.family == AeroxFamily::Three {
            [0x1838, 0x1878].contains(&self.base.identity.product_id)
        } else {
            [0x1852, 0x185c, 0x1860].contains(&self.base.identity.product_id)
        }
    }
    fn wireless(&self) -> bool {
        if self.family == AeroxFamily::Three {
            [0x1838, 0x183a, 0x1878, 0x187a].contains(&self.base.identity.product_id)
        } else {
            [0x1852, 0x1854, 0x185c, 0x185e, 0x1860, 0x1862]
                .contains(&self.base.identity.product_id)
        }
    }
    fn send(&mut self, command: &[u8], read: bool) -> Result<Vec<u8>> {
        let mut framed = command.to_vec();
        if self.receiver() {
            framed[0] |= 0x40;
        }
        if read {
            self.base
                .transport
                .write_output_and_read(&framed, 64, 1, 0, None)
        } else {
            self.base.transport.write_output(&framed, 1, 0)?;
            Ok(Vec::new())
        }
    }
    fn send_and_save(&mut self, command: &[u8]) -> Result<()> {
        let receiver = self.receiver();
        self.send(command, receiver)?;
        thread::sleep(Duration::from_millis(50));
        self.send(&[0x11, 0], receiver)?;
        Ok(())
    }
    fn encode_air(dpi: u16, index: usize) -> Result<u8> {
        if !(100..=18_000).contains(&dpi) || !dpi.is_multiple_of(100) {
            return Err(invalid(format!(
                "DPI preset {} must be from 100 to 18000 in steps of 100.",
                index + 1
            )));
        }
        if dpi == 100 {
            return Ok(0);
        }
        let mut encoded = usize::from(dpi / 100);
        for skipped in SKIPPED_SENSITIVITY {
            if encoded < usize::from(skipped) {
                break;
            }
            encoded += 1;
        }
        u8::try_from(encoded).map_err(|_| invalid("DPI encoding overflow."))
    }
    fn battery(&mut self) -> Result<BatteryInfo> {
        if !self.wireless() {
            return Err(unsupported(format!(
                "Battery status is only available on Aerox {} Wireless models.",
                self.family.number()
            )));
        }
        let receiver = self.receiver();
        let response = self.send(&[0x92], true)?;
        let command = if receiver { 0xd2 } else { 0x92 };
        let offset = usize::from(response.len() >= 3 && response[0] == 0 && response[1] == command);
        if response.len().saturating_sub(offset) < 2 {
            return Err(OpenSeriesError::Protocol(format!(
                "Device returned a short battery response ({} bytes).",
                response.len()
            )));
        }
        let raw = response[offset + 1];
        let step = raw & 0x7f;
        if !(1..=21).contains(&step) {
            return Ok(BatteryInfo {
                level_percentage: 0,
                status: BatteryStatus::Disconnected,
            });
        }
        let level = u16::from(step - 1) * 5;
        let status = if raw & 0x80 != 0 {
            BatteryStatus::Charging
        } else if level == 100 {
            BatteryStatus::Charged
        } else {
            BatteryStatus::Discharging
        };
        Ok(BatteryInfo {
            level_percentage: level,
            status,
        })
    }
}

impl DeviceProtocol for Aerox {
    fn id(&self) -> &str {
        &self.base.identity.id
    }
    fn name(&self) -> &str {
        match (self.family, self.base.identity.product_id) {
            (AeroxFamily::Three, 0x1836) => "SteelSeries Aerox 3",
            (AeroxFamily::Three, 0x1878 | 0x187a) => {
                "SteelSeries Aerox 3 Wireless CS2 Dragon Lore Edition"
            }
            (AeroxFamily::Three, _) => "SteelSeries Aerox 3 Wireless",
            (AeroxFamily::Five, 0x1850) => "SteelSeries Aerox 5",
            (AeroxFamily::Five, 0x185c | 0x185e) => {
                "SteelSeries Aerox 5 Wireless Destiny 2 Edition"
            }
            (AeroxFamily::Five, 0x1860 | 0x1862) => {
                "SteelSeries Aerox 5 Wireless Diablo IV Edition"
            }
            _ => "SteelSeries Aerox 5 Wireless",
        }
    }
    fn product_id(&self) -> u16 {
        self.base.identity.product_id
    }
    fn supported_features(&self) -> Capabilities {
        Capabilities::MOUSE_SENSITIVITY
            | Capabilities::POLLING_RATE
            | Capabilities::ILLUMINATION
            | if self.wireless() {
                Capabilities::BATTERY_STATUS | Capabilities::SLEEP_TIMER
            } else {
                Capabilities::empty()
            }
    }
}

impl MouseProtocol for Aerox {
    fn sensitivity_info(&self) -> Result<MouseSensitivityInfo> {
        Ok(if self.family == AeroxFamily::Three && !self.wireless() {
            MouseSensitivityInfo {
                minimum: 200,
                maximum: 8_500,
                step: 100,
                maximum_preset_count: 5,
            }
        } else {
            MouseSensitivityInfo {
                minimum: 100,
                maximum: 18_000,
                step: 100,
                maximum_preset_count: 5,
            }
        })
    }
    fn supported_polling_rates(&self) -> Result<&'static [u16]> {
        Ok(&POLLING_RATES)
    }
    fn supported_illumination_zones(&self) -> Result<&'static [MouseZone]> {
        Ok(&AEROX_ZONES)
    }
    fn set_sensitivity(&mut self, values: &[u16]) -> Result<()> {
        if !(1..=5).contains(&values.len()) {
            return Err(invalid("Between one and five DPI presets are required."));
        }
        let wireless = self.wireless();
        let mut command = vec![0x2d, values.len() as u8, if wireless { 0 } else { 1 }];
        for (index, dpi) in values.iter().copied().enumerate() {
            command.push(if self.family == AeroxFamily::Three && !wireless {
                if !(200..=8_500).contains(&dpi) || !dpi.is_multiple_of(100) {
                    return Err(invalid(format!(
                        "DPI preset {} must be from 200 to 8500 in steps of 100.",
                        index + 1
                    )));
                }
                CORE_SENSITIVITY[usize::from((dpi - 200) / 100)]
            } else {
                Self::encode_air(dpi, index)?
            });
        }
        self.send_and_save(&command)
    }
    fn set_polling_rate(&mut self, rate: u16) -> Result<()> {
        let wireless = self.wireless();
        let encoded = match (rate, wireless) {
            (125, true) => 3,
            (250, true) => 2,
            (500, true) => 1,
            (1000, true) => 0,
            (125, false) => 4,
            (250, false) => 3,
            (500, false) => 2,
            (1000, false) => 1,
            _ => return Err(invalid("Polling rate must be 125, 250, 500, or 1000 Hz.")),
        };
        self.send_and_save(&[0x2b, encoded])
    }
    fn set_illumination(
        &mut self,
        zone: MouseZone,
        color: RgbColor,
        persistence: Persistence,
    ) -> Result<()> {
        if !AEROX_ZONES.contains(&zone) {
            return Err(invalid(format!(
                "Aerox {} lighting zone must be top, middle, or bottom.",
                self.family.number()
            )));
        }
        let mut command = if self.wireless() {
            vec![0x21, 1, zone as u8]
        } else {
            match zone {
                MouseZone::Top => vec![0x21, 1],
                MouseZone::Middle => vec![0x21, 2, 0, 0, 0],
                MouseZone::Bottom => vec![0x21, 4, 0, 0, 0, 0, 0, 0],
                _ => unreachable!(),
            }
        };
        command.extend([color.red, color.green, color.blue]);
        if persistence == Persistence::Save {
            self.send_and_save(&command)
        } else {
            self.send(&command, self.receiver()).map(drop)
        }
    }
    fn set_sleep_timer(&mut self, minutes: u8) -> Result<()> {
        if !self.wireless() {
            return Err(unsupported(format!(
                "Sleep timer is only available on Aerox {} Wireless models.",
                self.family.number()
            )));
        }
        if minutes > 20 {
            return Err(invalid("Sleep timer must be between 0 and 20 minutes."));
        }
        let ms = u32::from(minutes) * 60_000;
        let bytes = ms.to_le_bytes();
        self.send_and_save(&[0x29, bytes[0], bytes[1], bytes[2]])
    }
    fn get_battery(&mut self) -> Result<BatteryInfo> {
        self.battery()
    }
}
