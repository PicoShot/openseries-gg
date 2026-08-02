use crate::devices::Device;
use crate::protocol::definitions::DEFINITIONS;
use crate::protocol::{Endpoint, HidTransport, Identity};
use crate::{Result, protocol::map_hid_error};
use hidapi::HidApi;
use std::sync::Arc;

const STEELSERIES_VENDOR_ID: u16 = 0x1038;

pub fn discover_devices() -> Result<Vec<Device>> {
    let api = Arc::new(HidApi::new().map_err(map_hid_error)?);
    let mut devices = Vec::new();
    for info in api
        .device_list()
        .filter(|info| info.vendor_id() == STEELSERIES_VENDOR_ID)
    {
        let endpoint = Endpoint::inspect(&api, info);
        if let Some(definition) = DEFINITIONS
            .iter()
            .find(|definition| definition.matches(&endpoint))
        {
            let identity = Identity::new(definition.slug, info);
            let transport = HidTransport::new(
                Arc::clone(&api),
                info.path().to_owned(),
                2_000,
                endpoint.report_sizes(),
            );
            devices.push((definition.connect)(identity, transport));
        }
    }
    devices.sort_unstable_by(|left, right| left.id().cmp(right.id()));
    Ok(devices)
}
