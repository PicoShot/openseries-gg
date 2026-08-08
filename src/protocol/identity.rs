use hidapi::DeviceInfo;
use sha2::{Digest, Sha256};
use std::ffi::CStr;
use std::fmt::Write as _;

#[derive(Clone, Debug)]
pub(crate) struct Identity {
    pub id: String,
    pub product_id: u16,
}

impl Identity {
    pub(crate) fn new(slug: &str, info: &DeviceInfo) -> Self {
        Self::from_path(slug, info.product_id(), &info.path().to_string_lossy())
    }

    fn from_path(slug: &str, product_id: u16, path: &str) -> Self {
        let mut hasher = Sha256::new();
        hasher.update(path.as_bytes());
        let digest = hasher.finalize();
        let mut suffix = String::with_capacity(12);
        for byte in &digest[..6] {
            write!(suffix, "{byte:02x}").expect("writing to a String cannot fail");
        }
        Self {
            id: format!("{slug}-{product_id:04x}-{suffix}"),
            product_id,
        }
    }
}

pub(crate) fn normalized_path(path: &CStr) -> String {
    path.to_string_lossy().replace('\\', "/").to_lowercase()
}
