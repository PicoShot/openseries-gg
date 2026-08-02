use super::*;

pub(crate) static DEFINITION: DeviceDefinition = DeviceDefinition {
    slug: "aerox-5",
    product_ids: &[0x1850, 0x1852, 0x1854, 0x185c, 0x185e, 0x1860, 0x1862],
    rule: MatchRule::VendorControl,
    connect: |identity, transport| {
        Device::Mouse(Mouse::new(Box::new(Aerox::new(
            identity,
            transport,
            AeroxFamily::Five,
        ))))
    },
};
