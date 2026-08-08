mod commands;
mod interactive;
mod settings;

fn main() {
    std::process::exit(commands::run());
}
