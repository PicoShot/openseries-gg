use crate::protocol::{HidTransport, Identity};
use crate::{OpenSeriesError, Result};
use bitflags::bitflags;

#[path = "headsets/mod.rs"]
pub(crate) mod headset_models;
#[path = "mice/mod.rs"]
pub(crate) mod mouse_models;

pub mod headsets {
    pub use super::{
        BatteryInfo, BatteryStatus, BluetoothCallVolumeMode, ChatmixInfo, EqualizerFilterType,
        EqualizerInfo, EqualizerPreset, Headset, HeadsetStatus, ParametricEqualizerBand,
        ParametricEqualizerInfo,
    };
}

pub mod mice {
    pub use super::{
        BatteryInfo, BatteryStatus, Mouse, MouseSensitivityInfo, MouseZone, Persistence, RgbColor,
    };
}

bitflags! {
    #[derive(Clone, Copy, Debug, Eq, PartialEq)]
    pub struct Capabilities: u32 {
        const SIDETONE = 1 << 0;
        const BATTERY_STATUS = 1 << 1;
        const CHATMIX = 1 << 2;
        const INACTIVE_TIME = 1 << 3;
        const EQUALIZER = 1 << 4;
        const EQUALIZER_PRESET = 1 << 5;
        const MOUSE_SENSITIVITY = 1 << 6;
        const POLLING_RATE = 1 << 7;
        const ILLUMINATION = 1 << 8;
        const SLEEP_TIMER = 1 << 9;
        const MICROPHONE_VOLUME = 1 << 10;
        const MICROPHONE_MUTE_LED_BRIGHTNESS = 1 << 11;
        const VOLUME_LIMITER = 1 << 12;
        const PARAMETRIC_EQUALIZER = 1 << 13;
        const BLUETOOTH_WHEN_POWERED_ON = 1 << 14;
        const BLUETOOTH_CALL_VOLUME = 1 << 15;
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct DeviceMetadata<'a> {
    pub id: &'a str,
    pub name: &'a str,
    pub product_id: u16,
    pub capabilities: Capabilities,
}

impl Capabilities {
    pub const ALL: [(Capabilities, &'static str); 16] = [
        (Self::SIDETONE, "Sidetone"),
        (Self::BATTERY_STATUS, "BatteryStatus"),
        (Self::CHATMIX, "ChatMix"),
        (Self::INACTIVE_TIME, "InactiveTime"),
        (Self::EQUALIZER, "Equalizer"),
        (Self::EQUALIZER_PRESET, "EqualizerPreset"),
        (Self::MOUSE_SENSITIVITY, "MouseSensitivity"),
        (Self::POLLING_RATE, "PollingRate"),
        (Self::ILLUMINATION, "Illumination"),
        (Self::SLEEP_TIMER, "SleepTimer"),
        (Self::MICROPHONE_VOLUME, "MicrophoneVolume"),
        (
            Self::MICROPHONE_MUTE_LED_BRIGHTNESS,
            "MicrophoneMuteLedBrightness",
        ),
        (Self::VOLUME_LIMITER, "VolumeLimiter"),
        (Self::PARAMETRIC_EQUALIZER, "ParametricEqualizer"),
        (Self::BLUETOOTH_WHEN_POWERED_ON, "BluetoothWhenPoweredOn"),
        (Self::BLUETOOTH_CALL_VOLUME, "BluetoothCallVolume"),
    ];
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum BatteryStatus {
    Disconnected,
    Discharging,
    Charging,
    Charged,
}

impl std::fmt::Display for BatteryStatus {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{self:?}")
    }
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct BatteryInfo {
    pub level_percentage: u16,
    pub status: BatteryStatus,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct ChatmixInfo {
    pub level: u16,
    pub game_volume_percentage: u16,
    pub chat_volume_percentage: u16,
}

/// A single headset status response decoded into its supported values.
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct HeadsetStatus {
    pub battery: Option<BatteryInfo>,
    pub chatmix: Option<ChatmixInfo>,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct EqualizerInfo {
    pub band_count: usize,
    pub minimum: f32,
    pub maximum: f32,
    pub step: f32,
}

#[derive(Clone, Debug, PartialEq)]
pub struct EqualizerPreset {
    pub name: &'static str,
    pub bands: &'static [f32; 10],
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum EqualizerFilterType {
    Peaking,
    LowPass,
    HighPass,
    LowShelf,
    HighShelf,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ParametricEqualizerBand {
    pub frequency: u16,
    pub gain: f32,
    pub q_factor: f32,
    pub filter: EqualizerFilterType,
}

#[derive(Clone, Debug, PartialEq)]
pub struct ParametricEqualizerInfo {
    pub maximum_band_count: u8,
    pub minimum_frequency: u16,
    pub maximum_frequency: u16,
    pub minimum_gain: f32,
    pub maximum_gain: f32,
    pub gain_step: f32,
    pub minimum_q_factor: f32,
    pub maximum_q_factor: f32,
    pub supported_filters: &'static [EqualizerFilterType],
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum BluetoothCallVolumeMode {
    Unchanged,
    LowerBy12Decibels,
    MuteGame,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct MouseSensitivityInfo {
    pub minimum: u16,
    pub maximum: u16,
    pub step: u16,
    pub maximum_preset_count: u8,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct RgbColor {
    pub red: u8,
    pub green: u8,
    pub blue: u8,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
#[repr(u8)]
pub enum MouseZone {
    Top,
    Middle,
    Bottom,
    Logo,
    Wheel,
}

impl std::fmt::Display for MouseZone {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{self:?}")
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum Persistence {
    Temporary,
    Save,
}

pub(crate) struct DeviceContext {
    pub(crate) identity: Identity,
    pub(crate) transport: HidTransport,
}

impl DeviceContext {
    pub(crate) fn new(identity: Identity, transport: HidTransport) -> Self {
        Self {
            identity,
            transport,
        }
    }
}

pub(crate) trait DeviceProtocol: Send {
    fn id(&self) -> &str;
    fn name(&self) -> &str;
    fn product_id(&self) -> u16;
    fn supported_features(&self) -> Capabilities;
}

pub(crate) trait HeadsetProtocol: DeviceProtocol {
    fn equalizer_info(&self) -> Result<EqualizerInfo> {
        Err(unsupported(self, "equalizer"))
    }
    fn equalizer_presets(&self) -> Result<&'static [EqualizerPreset]> {
        Err(unsupported(self, "equalizer presets"))
    }
    fn parametric_equalizer_info(&self) -> Option<ParametricEqualizerInfo> {
        None
    }
    fn get_battery(&mut self) -> Result<BatteryInfo> {
        Err(unsupported(self, "battery status"))
    }
    fn get_chatmix(&mut self) -> Result<ChatmixInfo> {
        Err(unsupported(self, "ChatMix"))
    }
    fn get_status(&mut self) -> Result<HeadsetStatus> {
        let features = self.supported_features();
        Ok(HeadsetStatus {
            battery: if features.contains(Capabilities::BATTERY_STATUS) {
                Some(self.get_battery()?)
            } else {
                None
            },
            chatmix: if features.contains(Capabilities::CHATMIX) {
                Some(self.get_chatmix()?)
            } else {
                None
            },
        })
    }
    fn set_sidetone(&mut self, _level: u8) -> Result<()> {
        Err(unsupported(self, "sidetone control"))
    }
    fn set_inactive_time(&mut self, _minutes: u16) -> Result<()> {
        Err(unsupported(self, "inactive time control"))
    }
    fn set_equalizer(&mut self, _bands: &[f32]) -> Result<()> {
        Err(unsupported(self, "equalizer"))
    }
    fn set_equalizer_preset(&mut self, _preset: usize) -> Result<()> {
        Err(unsupported(self, "equalizer presets"))
    }
    fn set_microphone_volume(&mut self, _volume: u8) -> Result<()> {
        Err(unsupported(self, "microphone volume control"))
    }
    fn set_microphone_mute_led_brightness(&mut self, _brightness: u8) -> Result<()> {
        Err(unsupported(self, "microphone mute LED brightness control"))
    }
    fn set_volume_limiter(&mut self, _enabled: bool) -> Result<()> {
        Err(unsupported(self, "volume limiter control"))
    }
    fn set_parametric_equalizer(&mut self, _bands: &[ParametricEqualizerBand]) -> Result<()> {
        Err(unsupported(self, "parametric equalizer"))
    }
    fn set_bluetooth_when_powered_on(&mut self, _enabled: bool) -> Result<()> {
        Err(unsupported(self, "Bluetooth power-on control"))
    }
    fn set_bluetooth_call_volume(&mut self, _mode: BluetoothCallVolumeMode) -> Result<()> {
        Err(unsupported(self, "Bluetooth call volume control"))
    }
}

pub(crate) trait MouseProtocol: DeviceProtocol {
    fn sensitivity_info(&self) -> Result<MouseSensitivityInfo> {
        Err(unsupported(self, "sensitivity control"))
    }
    fn supported_polling_rates(&self) -> Result<&'static [u16]> {
        Err(unsupported(self, "polling rate control"))
    }
    fn supported_illumination_zones(&self) -> Result<&'static [MouseZone]> {
        Err(unsupported(self, "illumination control"))
    }
    fn set_sensitivity(&mut self, _dpi_presets: &[u16]) -> Result<()> {
        Err(unsupported(self, "sensitivity control"))
    }
    fn set_polling_rate(&mut self, _polling_rate: u16) -> Result<()> {
        Err(unsupported(self, "polling rate control"))
    }
    fn set_illumination(
        &mut self,
        _zone: MouseZone,
        _color: RgbColor,
        _persistence: Persistence,
    ) -> Result<()> {
        Err(unsupported(self, "illumination control"))
    }
    fn set_sleep_timer(&mut self, _minutes: u8) -> Result<()> {
        Err(unsupported(self, "sleep timer"))
    }
    fn get_battery(&mut self) -> Result<BatteryInfo> {
        Err(unsupported(self, "battery status"))
    }
}

fn unsupported<T: DeviceProtocol + ?Sized>(device: &T, feature: &str) -> OpenSeriesError {
    OpenSeriesError::Unsupported(format!("{} does not support {feature}.", device.name()))
}

pub struct Headset {
    inner: Box<dyn HeadsetProtocol>,
}

impl Headset {
    pub(crate) fn new(inner: Box<dyn HeadsetProtocol>) -> Self {
        Self { inner }
    }

    pub fn id(&self) -> &str {
        self.inner.id()
    }
    pub fn name(&self) -> &str {
        self.inner.name()
    }
    pub fn product_id(&self) -> u16 {
        self.inner.product_id()
    }
    pub fn capabilities(&self) -> Capabilities {
        self.inner.supported_features()
    }
    pub fn metadata(&self) -> DeviceMetadata<'_> {
        DeviceMetadata {
            id: self.id(),
            name: self.name(),
            product_id: self.product_id(),
            capabilities: self.capabilities(),
        }
    }
    pub fn equalizer_info(&self) -> Result<EqualizerInfo> {
        self.inner.equalizer_info()
    }
    pub fn equalizer_presets(&self) -> Result<&'static [EqualizerPreset]> {
        self.inner.equalizer_presets()
    }
    pub fn parametric_equalizer_info(&self) -> Option<ParametricEqualizerInfo> {
        self.inner.parametric_equalizer_info()
    }
    pub fn get_battery(&mut self) -> Result<BatteryInfo> {
        self.inner.get_battery()
    }
    pub fn get_chatmix(&mut self) -> Result<ChatmixInfo> {
        self.inner.get_chatmix()
    }
    pub fn get_status(&mut self) -> Result<HeadsetStatus> {
        self.inner.get_status()
    }
    pub fn set_sidetone(&mut self, level: u8) -> Result<()> {
        self.inner.set_sidetone(level)
    }
    pub fn set_inactive_time(&mut self, minutes: u16) -> Result<()> {
        self.inner.set_inactive_time(minutes)
    }
    pub fn set_equalizer(&mut self, bands: &[f32; 10]) -> Result<()> {
        self.inner.set_equalizer(bands)
    }
    pub fn set_equalizer_preset(&mut self, preset: usize) -> Result<()> {
        self.inner.set_equalizer_preset(preset)
    }
    pub fn set_microphone_volume(&mut self, volume: u8) -> Result<()> {
        self.inner.set_microphone_volume(volume)
    }
    pub fn set_microphone_mute_led_brightness(&mut self, brightness: u8) -> Result<()> {
        self.inner.set_microphone_mute_led_brightness(brightness)
    }
    pub fn set_volume_limiter(&mut self, enabled: bool) -> Result<()> {
        self.inner.set_volume_limiter(enabled)
    }
    pub fn set_parametric_equalizer(&mut self, bands: &[ParametricEqualizerBand]) -> Result<()> {
        self.inner.set_parametric_equalizer(bands)
    }
    pub fn set_bluetooth_when_powered_on(&mut self, enabled: bool) -> Result<()> {
        self.inner.set_bluetooth_when_powered_on(enabled)
    }
    pub fn set_bluetooth_call_volume(&mut self, mode: BluetoothCallVolumeMode) -> Result<()> {
        self.inner.set_bluetooth_call_volume(mode)
    }
}

pub struct Mouse {
    inner: Box<dyn MouseProtocol>,
}

impl Mouse {
    pub(crate) fn new(inner: Box<dyn MouseProtocol>) -> Self {
        Self { inner }
    }
    pub fn id(&self) -> &str {
        self.inner.id()
    }
    pub fn name(&self) -> &str {
        self.inner.name()
    }
    pub fn product_id(&self) -> u16 {
        self.inner.product_id()
    }
    pub fn capabilities(&self) -> Capabilities {
        self.inner.supported_features()
    }
    pub fn metadata(&self) -> DeviceMetadata<'_> {
        DeviceMetadata {
            id: self.id(),
            name: self.name(),
            product_id: self.product_id(),
            capabilities: self.capabilities(),
        }
    }
    pub fn sensitivity_info(&self) -> Result<MouseSensitivityInfo> {
        self.inner.sensitivity_info()
    }
    pub fn supported_polling_rates(&self) -> Result<&'static [u16]> {
        self.inner.supported_polling_rates()
    }
    pub fn supported_illumination_zones(&self) -> Result<&'static [MouseZone]> {
        self.inner.supported_illumination_zones()
    }
    pub fn set_sensitivity(&mut self, values: &[u16]) -> Result<()> {
        self.inner.set_sensitivity(values)
    }
    pub fn set_polling_rate(&mut self, rate: u16) -> Result<()> {
        self.inner.set_polling_rate(rate)
    }
    pub fn set_illumination(
        &mut self,
        zone: MouseZone,
        color: RgbColor,
        persistence: Persistence,
    ) -> Result<()> {
        self.inner.set_illumination(zone, color, persistence)
    }
    pub fn set_sleep_timer(&mut self, minutes: u8) -> Result<()> {
        self.inner.set_sleep_timer(minutes)
    }
    pub fn get_battery(&mut self) -> Result<BatteryInfo> {
        self.inner.get_battery()
    }
}

pub enum Device {
    Headset(Headset),
    Mouse(Mouse),
}

impl Device {
    pub fn id(&self) -> &str {
        match self {
            Self::Headset(d) => d.id(),
            Self::Mouse(d) => d.id(),
        }
    }
    pub fn name(&self) -> &str {
        match self {
            Self::Headset(d) => d.name(),
            Self::Mouse(d) => d.name(),
        }
    }
    pub fn product_id(&self) -> u16 {
        match self {
            Self::Headset(d) => d.product_id(),
            Self::Mouse(d) => d.product_id(),
        }
    }
    pub fn capabilities(&self) -> Capabilities {
        match self {
            Self::Headset(d) => d.capabilities(),
            Self::Mouse(d) => d.capabilities(),
        }
    }
    pub fn metadata(&self) -> DeviceMetadata<'_> {
        DeviceMetadata {
            id: self.id(),
            name: self.name(),
            product_id: self.product_id(),
            capabilities: self.capabilities(),
        }
    }

    pub fn as_headset_mut(&mut self) -> Option<&mut Headset> {
        match self {
            Self::Headset(device) => Some(device),
            Self::Mouse(_) => None,
        }
    }
    pub fn as_mouse_mut(&mut self) -> Option<&mut Mouse> {
        match self {
            Self::Mouse(device) => Some(device),
            Self::Headset(_) => None,
        }
    }
}
