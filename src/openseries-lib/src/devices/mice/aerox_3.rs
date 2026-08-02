use super::*;

pub(crate) static DEFINITION: DeviceDefinition = DeviceDefinition {
    slug: "aerox-3",
    product_ids: &[0x1836, 0x1838, 0x183a, 0x1878, 0x187a],
    rule: MatchRule::VendorControl,
    connect: |identity, transport| {
        Device::Mouse(Mouse::new(Box::new(Aerox::new(
            identity,
            transport,
            AeroxFamily::Three,
        ))))
    },
};
