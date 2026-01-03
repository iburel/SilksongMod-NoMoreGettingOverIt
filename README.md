# NoMoreGettingOverIt

A Hollow Knight: Silksong mod that lets you rewind time. Never lose progress again!

## Features

- Automatically saves game state every 10 seconds
- Press **F2** to rewind to the previous savestate
- Keeps up to 12 savestates in memory (2 minutes of gameplay)
- Press F2 multiple times to go further back in time
- Works across scene transitions

## What gets saved

- Position and velocity
- Health, max health, geo, silk
- Facing direction
- Current scene

## Installation

1. Install [BepInEx 5.x](https://github.com/BepInEx/BepInEx) for Hollow Knight: Silksong
2. Download `NoMoreGettingOverIt.dll` from [Releases](https://github.com/iburel/SilksongMod-NoMoreGettingOverIt/releases)
3. Place the DLL in `BepInEx/plugins/`
4. Launch the game

## Configuration

After first launch, a config file is created at `BepInEx/config/com.iwan.nomoregettingoverit.cfg`:

| Setting | Default | Description |
|---------|---------|-------------|
| RewindKey | F2 | Key to rewind to previous savestate |
| SaveInterval | 10 | Seconds between automatic savestates |
| MaxSavestates | 12 | Maximum savestates kept in memory |

## Building from source

```bash
dotnet build -c Release
```

The DLL will be in `bin/Release/netstandard2.1/`.

## License

MIT
