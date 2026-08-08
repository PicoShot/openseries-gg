use crate::devices::Device;
use crate::protocol::definitions::DEFINITIONS;
use crate::protocol::{Endpoint, HidTransport, Identity};
use crate::{Result, protocol::map_hid_error};
use hidapi::HidApi;
use std::sync::Arc;
use std::time::Duration;

const STEELSERIES_VENDOR_ID: u16 = 0x1038;

/// Configuration used while discovering and communicating with devices.
#[derive(Clone, Copy, Debug)]
#[non_exhaustive]
pub struct DiscoveryOptions {
    timeout: Duration,
}

impl Default for DiscoveryOptions {
    fn default() -> Self {
        Self {
            timeout: Duration::from_secs(2),
        }
    }
}

impl DiscoveryOptions {
    /// Sets the maximum wait for a response to a device command.
    pub fn with_timeout(mut self, timeout: Duration) -> Self {
        self.timeout = timeout;
        self
    }

    /// Returns the configured device-response timeout.
    pub fn timeout(self) -> Duration {
        self.timeout
    }
}

/// Discovers supported devices using the default options.
pub fn discover_devices() -> Result<Vec<Device>> {
    discover_devices_with_options(DiscoveryOptions::default())
}

/// Discovers supported devices using the supplied communication options.
pub fn discover_devices_with_options(options: DiscoveryOptions) -> Result<Vec<Device>> {
    let timeout_ms = i32::try_from(options.timeout.as_millis()).map_err(|_| {
        crate::OpenSeriesError::InvalidArgument(
            "Device timeout must not exceed 2147483647 milliseconds.".into(),
        )
    })?;
    let api = Arc::new(HidApi::new().map_err(map_hid_error)?);
    let mut devices = Vec::new();
    for info in api
        .device_list()
        .filter(|info| info.vendor_id() == STEELSERIES_VENDOR_ID)
    {
        if !DEFINITIONS
            .iter()
            .any(|definition| definition.product_ids.contains(&info.product_id()))
        {
            continue;
        }
        let mut endpoint = Endpoint::inspect(&api, info);
        if let Some(definition) = DEFINITIONS
            .iter()
            .copied()
            .filter(|definition| definition.product_ids.contains(&info.product_id()))
            .find(|definition| definition.matches(&endpoint))
        {
            let identity = Identity::new(definition.slug, info);
            let report_sizes = endpoint.report_sizes();
            let device = endpoint.take_device();
            let transport = HidTransport::new(
                Arc::clone(&api),
                info.path().to_owned(),
                device,
                timeout_ms,
                report_sizes,
            );
            devices.push((definition.connect)(identity, transport));
        }
    }
    devices.sort_unstable_by(|left, right| left.id().cmp(right.id()));
    Ok(devices)
}
