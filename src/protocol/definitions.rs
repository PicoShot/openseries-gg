use super::definition::DeviceDefinition;
use crate::devices::{headset_models, mouse_models};

pub(crate) static DEFINITIONS: [&DeviceDefinition; 7] = [
    &headset_models::ARCTIS_7_PLUS_DEFINITION,
    &headset_models::NOVA_5_DEFINITION,
    &headset_models::NOVA_7_DEFINITION,
    &headset_models::NOVA_7P_DEFINITION,
    &mouse_models::AEROX_3_DEFINITION,
    &mouse_models::AEROX_5_DEFINITION,
    &mouse_models::SENSEI_TEN_DEFINITION,
];
