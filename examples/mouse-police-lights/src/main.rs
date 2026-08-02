use openseries::devices::mice::RgbColor;
use openseries::devices::{Capabilities, Device, Persistence};
use openseries::discover_devices;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};
use std::thread;
use std::time::Duration;

fn main() {
    let mut devices = match discover_devices() {
        Ok(devices) => devices,
        Err(error) => {
            eprintln!("{error}");
            std::process::exit(1);
        }
    };
    let Some(index) = devices.iter().position(|device| {
        matches!(device, Device::Mouse(_))
            && device.capabilities().contains(Capabilities::ILLUMINATION)
    }) else {
        eprintln!("No connected mouse with controllable illumination was found.");
        std::process::exit(1);
    };
    let name = devices[index].name().to_owned();
    let Some(mouse) = devices[index].as_mouse_mut() else {
        eprintln!("Selected device changed category.");
        std::process::exit(1);
    };
    let zones = mouse
        .supported_illumination_zones()
        .expect("illumination metadata");
    println!(
        "{name} · police lights across {} · Ctrl+C to exit",
        zones
            .iter()
            .map(ToString::to_string)
            .collect::<Vec<_>>()
            .join(", ")
    );

    let running = Arc::new(AtomicBool::new(true));
    let signal = Arc::clone(&running);
    ctrlc::set_handler(move || signal.store(false, Ordering::Relaxed))
        .expect("install Ctrl+C handler");
    let red = RgbColor {
        red: 255,
        green: 0,
        blue: 0,
    };
    let blue = RgbColor {
        red: 0,
        green: 60,
        blue: 255,
    };
    let mut phase = 0;
    while running.load(Ordering::Relaxed) {
        for (zone_index, zone) in zones.iter().copied().enumerate() {
            let color = if (zone_index + phase) % 2 == 0 {
                red
            } else {
                blue
            };
            if let Err(error) = mouse.set_illumination(zone, color, Persistence::Temporary) {
                eprintln!("Lighting effect stopped: {error}");
                std::process::exit(1);
            }
        }
        phase += 1;
        thread::sleep(Duration::from_millis(400));
    }
}
