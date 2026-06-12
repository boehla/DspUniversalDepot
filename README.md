# DspUniversalDepot

A Dyson Sphere Program mod that adds a **Universal Planetary Depot** with:

- **Configurable item limit** per slot (default: 5000)
- **Dynamic slot allocation** — slots are created automatically for each unique item type
- **Overflow deletion** (optional) — keep your conveyor belts running forever
- **Nebula Multiplayer compatibility** — works in multiplayer without breaking sync

![Icon](icon.png)

## Features

| Feature | Default | Configurable |
|---------|---------|--------------|
| Item limit per slot | 5000 | ✓ (1-999999) |
| Dynamic slots | enabled | ✓ |
| Overflow deletion | disabled | ✓ |
| Max slot count | 1000 | ✓ (0 = unlimited) |
| Belt lanes | 3 in / 3 out (MK1/MK2/MK3) | — |
| ILS remote ports | 0 (planet-only) | — |

## Recipe

Universal Depot is **expensive** to balance the unlimited storage:

- 30x Titanium Ingot
- 20x Circuit Board
- 10x Microcrystalline Component
- 2x Particle Broad-band
- Craft time: 10s

## Installation

### For Players (using pre-built release)

1. Install [BepInEx 5.4.x](https://thunderstore.io/c/dyson-sphere-program/p/BepInEx/BepInExPack/) for DSP
2. Download the latest release from [Releases](https://github.com/boehla/DspUniversalDepot/releases)
3. Extract `DspUniversalDepot.dll` to `BepInEx/plugins/`
4. Launch DSP — config is auto-generated at `BepInEx/config/com.boehla.dspuniversaldepot.cfg`

### For Developers (building from source)

See [docs/BUILDING.md](docs/BUILDING.md)

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
```

## How It Works

DSP normally requires each storage type to have predefined slots in its
`ItemProto`. Universal Depot patches the local `StorageComponent` so any
storage entity tagged as ours can dynamically accept and serve any item
type without pre-allocation.

When a belt requests `GetItemCount(itemId)`, the patch consults our
`DepotStorage` which tracks counts per unique item ID in a dictionary.
This works alongside DSP's own slot system — the depot looks like a
"vanilla" storage to the conveyor logic.

## Compatibility

- ✅ DSP 0.10.x
- ✅ BepInEx 5.4.x (IL2CPP)
- ✅ Nebula Multiplayer 0.10.x (no network-state changes)
- ⚠️ Other storage mods — should work, but test together

## Project Structure

```
DspUniversalDepot/
├── manifest.json              Thunderstore package manifest
├── icon.png                   Mod icon (256x256)
├── README.md                  This file
├── CHANGELOG.md               Version history
├── LICENSE                    MIT
├── CONTRIBUTING.md            Dev guide
├── .github/
│   ├── workflows/
│   │   ├── build.yml          CI build verification
│   │   └── release.yml        Auto-release on tag push
│   └── ISSUE_TEMPLATE/        Bug/feature templates
├── docs/
│   └── BUILDING.md            Build instructions
├── tools/
│   └── AssetBundleBuilder.cs  Unity Editor script
├── icons/
│   ├── icon.png               256x256
│   └── icon-128.png           128x128
└── src/
    ├── DspUniversalDepot.csproj
    ├── UniversalDepotPlugin.cs     Main entry + config
    ├── StorageManager.cs           Dynamic slot logic
    ├── AssetBundleManager.cs       Icon/model loader
    ├── LDBPatcher.cs               DSP database hooks
    └── ConveyorPatcher.cs          Belt/storage interface
```

## License

MIT — see [LICENSE](LICENSE)
