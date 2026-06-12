# DspUniversalDepot

A Dyson Sphere Program mod that adds a **Universal Planetary Depot** — a storage
building with a large, configurable slot count.

![Icon](icon.png)

## How it works

The depot is **cloned from the vanilla storage box** at load time using
[LDBTool](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/LDBTool/).
Because it stays a real DSP storage entity, it keeps full compatibility with:

- belts and sorters,
- the normal storage window UI,
- and the **native save format** — contents persist with your save, no extra
  save mod required.

The only thing the mod changes is the slot count: a small Harmony patch on
`FactoryStorage.NewStorageComponent` forces our building's storage to
`SlotCount` slots (default 500) instead of the vanilla 30–60.

> **v0.4.0 was a full rewrite.** Earlier versions (≤ 0.3.0) targeted a BepInEx 6
> / IL2CPP / .NET 6 setup and a custom dictionary-based storage that **never
> loaded into the game** — DSP is a Mono game, and storage is a fixed-grid
> `StorageComponent`. See the changelog for details.

## Recipe

Hand-craftable / assembler recipe (available without research):

- 20× Titanium Ingot
- 10× Circuit Board
- Craft time: 2 s → 1× Universal Planetary Depot

## Installation

### Players (Thunderstore / r2modman / Mod Manager)

1. Install [BepInEx 5.4.x](https://thunderstore.io/c/dyson-sphere-program/p/BepInEx/BepInExPack/) for DSP.
2. Install [LDBTool](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/LDBTool/) (required dependency).
3. Download the latest [release](https://github.com/boehla/DspUniversalDepot/releases) and
   extract `DspUniversalDepot.dll` into `BepInEx/plugins/`.
4. Launch DSP. The depot appears in the storage build category. Config is generated at
   `BepInEx/config/com.boehla.dspuniversaldepot.cfg`.

### Developers (build from source)

See [docs/BUILDING.md](docs/BUILDING.md).

## Configuration

`BepInEx/config/com.boehla.dspuniversaldepot.cfg`:

```ini
[General]
## Number of storage slots in the Universal Depot (vanilla storage has 30-60).
SlotCount = 500

[Advanced]
## Vanilla storage item that is cloned (2101 = Storage MK.I, 2102 = Storage MK.II).
SourceStorageItemId = 2102

## Item / Recipe IDs — change only on conflict with another mod.
DepotItemId = 7777
DepotRecipeId = 7777

## Column (1-12) inside the storage build category where the icon appears.
BuildBarIndex = 12
```

## Compatibility

- ✅ BepInEx 5.4.x (Mono)
- ✅ LDBTool 3.x (required)
- ✅ Native save/load (no DSPModSave needed)
- ⚠️ Other storage / building mods — should work; test together

## License

MIT — see [LICENSE](LICENSE)
