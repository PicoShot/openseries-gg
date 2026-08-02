use openseries::devices::{Capabilities, Device};
use openseries::discover_devices;
use std::io::{self, Write};
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};
use std::thread;
use std::time::Duration;

const BAR_WIDTH: usize = 31;

fn main() {
    let mut devices = match discover_devices() {
        Ok(devices) => devices,
        Err(error) => {
            eprintln!("{error}");
            std::process::exit(1);
        }
    };
    let Some(index) = devices.iter().position(|device| {
        matches!(device, Device::Headset(_))
            && device.capabilities().contains(Capabilities::CHATMIX)
    }) else {
        eprintln!("No connected ChatMix-capable headset was found.");
        std::process::exit(1);
    };

    let running = Arc::new(AtomicBool::new(true));
    let signal = Arc::clone(&running);
    ctrlc::set_handler(move || signal.store(false, Ordering::Relaxed))
        .expect("install Ctrl+C handler");
    println!(
        "{} · turn the ChatMix dial · Ctrl+C to exit",
        devices[index].name()
    );
    print!("\x1b[?25l");
    let _ = io::stdout().flush();

    while running.load(Ordering::Relaxed) {
        let Some(headset) = devices[index].as_headset_mut() else {
            eprintln!("Selected device changed category.");
            break;
        };
        match headset.get_chatmix() {
            Ok(value) => {
                let marker =
                    (f64::from(value.level) / 128.0 * (BAR_WIDTH - 1) as f64).round() as usize;
                let mut bar = vec!['─'; BAR_WIDTH];
                bar[marker] = '◆';
                print!(
                    "\r\x1b[2K\x1b[1;36mGame\x1b[0m {:>3}%  [\x1b[1m{}\x1b[0m]  {:>3}% \x1b[1;35mChat\x1b[0m",
                    value.game_volume_percentage,
                    bar.into_iter().collect::<String>(),
                    value.chat_volume_percentage
                );
                println!("{}", value.level)
            }
            Err(error) => print!("\r\x1b[2K\x1b[31mWaiting for ChatMix: {error}\x1b[0m"),
        }
        let _ = io::stdout().flush();
        thread::sleep(Duration::from_millis(50));
    }
    println!("\x1b[?25h");
}
