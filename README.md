# DspUniversalDepot

A Dyson Sphere Program mod that adds a **Universal Planetary Depot** — a
planetary **supply station** that feeds the planet's drone-logistics network and
can hold many item kinds at once.

![Icon](icon.png)

## How it works

The depot is **cloned from the vanilla Planetary Logistics Station** (item 2103)
at load time using
[LDBTool](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/LDBTool/), so
it is a real station with drones and belt ports.

- **Feed it by belt** → each incoming item auto-registers into a free slot as
  **Supply**. No per-slot configuration needed.
- **Planetary drones** then deliver those items to any station on the planet that
  demands them. The depot only ever *provides* — it never demands.
- **Discard-overflow toggle** (per building, in the station window): when on, items
  fed in while the depot is full are discarded instead of backing up the belt.
  Off by default.
- **Native save format** — contents persist with your save, no extra save mod.

To get past the engine's hard-coded ~5-item-kind limit, the mod:

1. grows the depot's slot array to `SlotCount` after the station initialises
   (`StationComponent.Init` postfix), and
2. replaces the six vanilla logistics helpers that are unrolled to slots 0–5
   (`HasLocalSupply`, `AddItem`, …) with loop versions, and
3. reads input belts directly so any item registers into a Supply slot, bypassing
   the 6-entry `needs` cap.

> **Note:** the vanilla station window only shows the **first 6 slots** (drones,
> energy and those 6 are editable as usual); slots 7+ are filled automatically by
> belt input. A scrollable N-slot UI is planned.

> **v0.5.0** reworked the depot from a storage box into a supply station — a
> storage box can never participate in drone logistics. **v0.4.0** was the Mono
> rewrite that first made the mod load at all. See the changelog for details.

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
## Number of distinct item kinds the depot can hold and supply.
SlotCount = 60
## Per-kind capacity (belt input stops when a slot is full → belt backs up).
SupplyMaxPerSlot = 10000

[Advanced]
## Vanilla station item that is cloned (2103 = Planetary Logistics Station).
SourceStationItemId = 2103

## Item / Recipe IDs — change only on conflict with another mod.
DepotItemId = 7777
DepotRecipeId = 7777

## Column (1-12) inside the build category where the icon appears.
BuildBarIndex = 12

[Design]
## Use the mod's own build-menu/inventory icon.
CustomIcon = true
## Give the placed depot its own distinct look (vs. a plain logistics-station clone).
CustomModel = true
## Render a fully custom procedural 3D mesh (platform + silo + crates). Requires CustomModel.
## Off = keep the station mesh, just tinted.
CustomMesh = true
## Tint applied to the depot model (hex #RRGGBB or #RRGGBBAA), multiplies the base albedo.
TintColor = #33D6B0
## Debug aid: render the custom model as a single plain box.
MeshDebugBox = false
```

The placed depot renders its own procedurally generated model (a stacked silo with corner
crates and an antenna) while keeping the logistics station's ports, drones and footprint, so it
behaves identically to the building it is cloned from — only the look changes.

## Compatibility

- ✅ BepInEx 5.4.x (Mono)
- ✅ LDBTool 3.x (required)
- ✅ Native save/load (no DSPModSave needed)
- ✅ **Nebula multiplayer** — soft dependency. When Nebula is present the mod registers a
  version check (`IMultiplayerMod`) and syncs the overflow toggle; the host stays
  authoritative for belt input and Nebula syncs depot contents to clients. **Install the
  same mod version on the host/server *and* every client.** (Verify in your session — see
  the changelog's testing note.)
- ⚠️ Other logistics-station mods — both patch `StationComponent`; test together

## License

MIT — see [LICENSE](LICENSE)
