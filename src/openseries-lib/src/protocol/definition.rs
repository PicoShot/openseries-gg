use super::{Endpoint, HidTransport, Identity};
use crate::devices::Device;

#[derive(Clone, Copy)]
pub(crate) enum MatchRule {
    VendorControl,
    ReportSizes { output: usize, feature: usize },
}

pub(crate) struct DeviceDefinition {
    pub(crate) slug: &'static str,
    pub(crate) product_ids: &'static [u16],
    pub(crate) rule: MatchRule,
    pub(crate) connect: fn(Identity, HidTransport) -> Device,
}

impl DeviceDefinition {
    pub(crate) fn matches(&self, endpoint: &Endpoint) -> bool {
        self.product_ids.contains(&endpoint.product_id())
            && match self.rule {
                MatchRule::VendorControl => endpoint.is_vendor_control(),
                MatchRule::ReportSizes { output, feature } => {
                    endpoint.has_report_sizes(output, feature)
                }
            }
    }
}
