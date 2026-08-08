use super::*;

const AEROX_3_ZONES: [MouseZone; 3] = [MouseZone::Top, MouseZone::Middle, MouseZone::Bottom];
const SKIPPED_SENSITIVITY: [u8; 35] = [
    0x08, 0x0f, 0x15, 0x1c, 0x22, 0x24, 0x2b, 0x31, 0x37, 0x3d, 0x43, 0x49, 0x4f, 0x55, 0x5b, 0x61,
    0x67, 0x6d, 0x73, 0x79, 0x7f, 0x85, 0x8b, 0x91, 0x97, 0x9d, 0xa3, 0xa9, 0xaf, 0xbe, 0xc1, 0xc8,
    0xce, 0xd4, 0xff,
];
const CORE_SENSITIVITY: [u8; 84] = [
    0x04, 0x06, 0x08, 0x0b, 0x0d, 0x0f, 0x12, 0x14, 0x16, 0x19, 0x1b, 0x1d, 0x20, 0x22, 0x24, 0x27,
    0x29, 0x2b, 0x2e, 0x30, 0x32, 0x34, 0x37, 0x39, 0x3b, 0x3e, 0x40, 0x42, 0x45, 0x47, 0x49, 0x4c,
    0x4e, 0x50, 0x53, 0x55, 0x57, 0x5a, 0x5c, 0x5e, 0x61, 0x63, 0x65, 0x68, 0x6a, 0x6c, 0x6f, 0x71,
    0x73, 0x76, 0x78, 0x7a, 0x7d, 0x7f, 0x81, 0x84, 0x86, 0x88, 0x8b, 0x8d, 0x8f, 0x92, 0x94, 0x96,
    0x99, 0x9b, 0x9d, 0xa0, 0xa2, 0xa4, 0xa7, 0xa9, 0xab, 0xad, 0xb0, 0xb2, 0xb4, 0xb7, 0xb9, 0xbc,
    0xbe, 0xc0, 0xc3, 0xc5,
];

pub(crate) static DEFINITION: DeviceDefinition = DeviceDefinition {
    slug: "aerox-3",
    product_ids: &[0x1836, 0x1838, 0x183a, 0x1878, 0x187a],
    rule: MatchRule::VendorControl,
    connect: |identity, transport| {
        Device::Mouse(Mouse::new(Box::new(Aerox3::new(identity, transport))))
    },
};

struct Aerox3 {
    base: DeviceContext,
}

impl Aerox3 {
    fn new(identity: Identity, transport: HidTransport) -> Self {
        Self {
            base: DeviceContext::new(identity, transport),
        }
    }

    fn receiver(&self) -> bool {
        [0x1838, 0x1878].contains(&self.base.identity.product_id)
    }

    fn wireless(&self) -> bool {
        [0x1838, 0x183a, 0x1878, 0x187a].contains(&self.base.identity.product_id)
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
            return Err(unsupported(
                "Battery status is only available on Aerox 3 Wireless models.",
            ));
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

impl DeviceProtocol for Aerox3 {
    fn id(&self) -> &str {
        &self.base.identity.id
    }

    fn name(&self) -> &str {
        match self.base.identity.product_id {
            0x1836 => "SteelSeries Aerox 3",
            0x1878 | 0x187a => "SteelSeries Aerox 3 Wireless CS2 Dragon Lore Edition",
            _ => "SteelSeries Aerox 3 Wireless",
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

impl MouseProtocol for Aerox3 {
    fn sensitivity_info(&self) -> Result<MouseSensitivityInfo> {
        Ok(if self.wireless() {
            MouseSensitivityInfo {
                minimum: 100,
                maximum: 18_000,
                step: 100,
                maximum_preset_count: 5,
            }
        } else {
            MouseSensitivityInfo {
                minimum: 200,
                maximum: 8_500,
                step: 100,
                maximum_preset_count: 5,
            }
        })
    }

    fn supported_polling_rates(&self) -> Result<&'static [u16]> {
        Ok(&POLLING_RATES)
    }

    fn supported_illumination_zones(&self) -> Result<&'static [MouseZone]> {
        Ok(&AEROX_3_ZONES)
    }

    fn set_sensitivity(&mut self, values: &[u16]) -> Result<()> {
        if !(1..=5).contains(&values.len()) {
            return Err(invalid("Between one and five DPI presets are required."));
        }
        let wireless = self.wireless();
        let mut command = vec![0x2d, values.len() as u8, if wireless { 0 } else { 1 }];
        for (index, dpi) in values.iter().copied().enumerate() {
            command.push(if wireless {
                Self::encode_air(dpi, index)?
            } else {
                if !(200..=8_500).contains(&dpi) || !dpi.is_multiple_of(100) {
                    return Err(invalid(format!(
                        "DPI preset {} must be from 200 to 8500 in steps of 100.",
                        index + 1
                    )));
                }
                CORE_SENSITIVITY[usize::from((dpi - 200) / 100)]
            });
        }
        self.send_and_save(&command)
    }

    fn set_polling_rate(&mut self, rate: u16) -> Result<()> {
        let encoded = match (rate, self.wireless()) {
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
        if !AEROX_3_ZONES.contains(&zone) {
            return Err(invalid(
                "Aerox 3 lighting zone must be top, middle, or bottom.",
            ));
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
            return Err(unsupported(
                "Sleep timer is only available on Aerox 3 Wireless models.",
            ));
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
