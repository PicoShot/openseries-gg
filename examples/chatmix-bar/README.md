# ChatMix Bar

A small terminal application that turns the headset ChatMix dial into a live
game/chat balance bar:

```text
Game 100%  [───────────────◆───────────────]  100% Chat
```


## Run

From the repository root:

```bash
dotnet run --project examples/chatmix-bar
```

Turn the ChatMix dial to move the marker. Press Ctrl+C to exit.

## Requirements

- .NET 10 SDK
- A supported SteelSeries headset with ChatMix
- Permission to access its HID endpoints


The example selects the first connected headset advertising the ChatMix
capability. It displays an error and exits when no compatible headset is found.
