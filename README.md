# DspUniversalDepot

A Dyson Sphere Program mod that adds a **Universal Planetary Depot** with:

- **Configurable item limit** (default: 5000)
- **Dynamic slot allocation** — slots are created automatically for each unique item type
- **Overflow deletion** (optional) — keep your conveyor belts running forever
- **Nebula Multiplayer compatibility**

## Features

| Feature | Default | Configurable |
|---------|---------|--------------|
| Item limit per slot | 5000 | ✓ |
| Dynamic slots | enabled | ✓ |
| Overflow deletion | disabled | ✓ |
| Slot count cap | 1000 | ✓ |
| Overflow threshold warning | 90% | ✓ |

## Installation

1. Install [BepInEx 5.4.x](https://thunderstore.io/c/dyson-sphere-program/p/BepInEx/BepInExPack/) for DSP
2. Download the latest release from the Releases page
3. Extract `DspUniversalDepot.dll` into `BepInEx/plugins/`
4. Launch DSP — config file is generated at `BepInEx/config/com.boehla.dspuniversaldepot.cfg`

## Configuration

Edit `BepInEx/config/com.boehla.dspuniversaldepot.cfg`:

```ini
[General]
## Maximum stack size per item slot
ItemLimit = 5000

## Automatically create new slots for unseen items
DynamicSlots = true

## Maximum number of unique item types the depot can store (0 = unlimited)
MaxSlotCount = 1000

## Delete oldest items when full (conveyor keeps running)
DeleteOverflow = false

## Warning threshold (% of ItemLimit before notification)
WarningThreshold = 90
```

## Compatibility

- ✅ DSP 0.10.x
- ✅ BepInEx 5.4.x
- ✅ Nebula Multiplayer 0.10.x (no network-state changes)

## Building

Requires .NET 6.0 SDK and DSP game references (see `src/DspUniversalDepot.csproj`).

```bash
cd src
dotnet build -c Release
```

Output: `src/bin/Release/net6.0/DspUniversalDepot.dll`

## License

MIT — see LICENSE
