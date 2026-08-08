use super::*;
use crate::protocol::definition::{DeviceDefinition, MatchRule};
use crate::protocol::{HidTransport, Identity};
use std::thread;
use std::time::Duration;

const POLLING_RATES: [u16; 4] = [125, 250, 500, 1000];
const SENSEI_ZONES: [MouseZone; 2] = [MouseZone::Logo, MouseZone::Wheel];

fn invalid(message: impl Into<String>) -> OpenSeriesError {
    OpenSeriesError::InvalidArgument(message.into())
}
fn unsupported(message: impl Into<String>) -> OpenSeriesError {
    OpenSeriesError::Unsupported(message.into())
}

mod aerox_3;
mod aerox_5;
mod sensei_ten;

pub(crate) use aerox_3::DEFINITION as AEROX_3_DEFINITION;
pub(crate) use aerox_5::DEFINITION as AEROX_5_DEFINITION;
pub(crate) use sensei_ten::DEFINITION as SENSEI_TEN_DEFINITION;
