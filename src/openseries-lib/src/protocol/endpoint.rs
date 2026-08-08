use super::identity::normalized_path;
use super::transport::{ReportSizes, report_sizes};
use hidapi::{DeviceInfo, HidApi, HidDevice};

pub(crate) struct Endpoint {
    product_id: u16,
    direct_usage: bool,
    descriptor_usage: bool,
    windows_interface_three: bool,
    report_sizes: Option<ReportSizes>,
    device: Option<HidDevice>,
}

impl Endpoint {
    pub(crate) fn inspect(api: &HidApi, info: &DeviceInfo) -> Self {
        let device = info.open_device(api).ok();
        let descriptor = device.as_ref().and_then(endpoint_descriptor);
        Self {
            product_id: info.product_id(),
            direct_usage: info.usage_page() == 0xffc0 && info.usage() == 1,
            descriptor_usage: descriptor
                .as_deref()
                .is_some_and(|value| descriptor_contains_usage(value, 0xffc0, 1)),
            windows_interface_three: normalized_path(info.path()).contains("&mi_03"),
            report_sizes: descriptor.as_deref().map(report_sizes),
            device,
        }
    }

    pub(crate) fn product_id(&self) -> u16 {
        self.product_id
    }

    pub(crate) fn is_vendor_control(&self) -> bool {
        self.direct_usage || self.descriptor_usage || self.windows_interface_three
    }

    pub(crate) fn has_report_sizes(&self, output: usize, feature: usize) -> bool {
        self.report_sizes
            .as_ref()
            .is_some_and(|sizes| sizes.output >= output && sizes.feature >= feature)
    }

    pub(crate) fn report_sizes(&self) -> ReportSizes {
        self.report_sizes.unwrap_or(ReportSizes {
            input: 64,
            output: 64,
            feature: 64,
        })
    }

    pub(crate) fn take_device(&mut self) -> Option<HidDevice> {
        self.device.take()
    }
}

fn endpoint_descriptor(device: &HidDevice) -> Option<Vec<u8>> {
    let mut descriptor = vec![0_u8; hidapi::MAX_REPORT_DESCRIPTOR_SIZE];
    let length = device.get_report_descriptor(&mut descriptor).ok()?;
    descriptor.truncate(length);
    Some(descriptor)
}

pub(crate) fn descriptor_contains_usage(
    descriptor: &[u8],
    expected_page: u16,
    expected_usage: u16,
) -> bool {
    let mut usage_page = 0_u32;
    let mut index = 0;
    while index < descriptor.len() {
        let prefix = descriptor[index];
        index += 1;
        if prefix == 0xfe {
            if index + 2 > descriptor.len() {
                return false;
            }
            index = index.saturating_add(2 + usize::from(descriptor[index]));
            continue;
        }
        let size = match prefix & 0x03 {
            0 => 0,
            1 => 1,
            2 => 2,
            _ => 4,
        };
        if index + size > descriptor.len() {
            return false;
        }
        let value = descriptor[index..index + size]
            .iter()
            .enumerate()
            .fold(0_u32, |value, (shift, byte)| {
                value | (u32::from(*byte) << (shift * 8))
            });
        index += size;
        match ((prefix >> 2) & 0x03, prefix >> 4) {
            (1, 0) => usage_page = value,
            (2, 0) => {
                let combined = if size == 4 {
                    value
                } else {
                    usage_page << 16 | value
                };
                if combined == (u32::from(expected_page) << 16 | u32::from(expected_usage)) {
                    return true;
                }
            }
            _ => {}
        }
    }
    false
}
