mod device_manager;
pub mod devices;
mod protocol;

pub use device_manager::{DiscoveryOptions, discover_devices, discover_devices_with_options};

use thiserror::Error;

pub type Result<T> = std::result::Result<T, OpenSeriesError>;

#[derive(Debug, Error)]
pub enum OpenSeriesError {
    #[error("permission denied; install the appropriate udev rule and reconnect the device")]
    PermissionDenied,
    #[error("timed out waiting for the device")]
    Timeout,
    #[error("device is disconnected")]
    Disconnected,
    #[error("unsupported feature: {0}")]
    Unsupported(String),
    #[error("invalid argument: {0}")]
    InvalidArgument(String),
    #[error("device protocol error: {0}")]
    Protocol(String),
    #[error("HID error: {0}")]
    Hid(#[from] hidapi::HidError),
}
