# Changelog

All notable changes to DspUniversalDepot are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.3] - 2026-06-12

### Changed
- **Built against REAL BepInEx 5.4.23.5 + HarmonyX + UnityEngine 2021.3 reference assemblies** (downloaded from NuGet)
- The DLL now references real BepInEx.Unity.IL2CPP, HarmonyLib, UnityEngine, BepInEx.Configuration namespaces
- Compiled on Linux (.NET 6.0) — no Windows + DSP install required for the build
- Will load in DSP with BepInEx installed (still requires BepInEx runtime on target)
- No code changes — same behavior as 0.2.2

### Added
- `libs/` folder with 88 reference DLLs from NuGet (BepInEx, Harmony, Unity, CommonAPI, LDBTool, NebulaAPI, DSPModSave, AsmResolver)
- `dotnet/scripts/download-libs.sh` — auto-fetch all reference DLLs from NuGet + GitHub
- Three build modes documented in `DspUniversalDepot.csproj`:
  1. Default: real DLLs from `./libs/` (cross-platform)
  2. DSP_GAME_PATH=...: real DLLs from your DSP install (Windows)
  3. -p:DSP_USE_STUBS=true: stub types (CI / sandbox)

## [0.2.2] - 2026-05-23

### Changed
- **Removed warning threshold** — no more near-full warnings
- **Higher recipe cost** for balance: 30x Titanium + 20x Circuit + 10x Microcrystalline + 2x Particle Broad-band, 10s craft

## [0.2.1] - 2026-05-23

### Added
- 3 belt input lanes (port 0-2) for MK1/MK2/MK3 belts
- 3 belt output lanes (port 3-5) for MK1/MK2/MK3 belts
- 0 ILS remote ports (planet-only building)
- `ConveyorPatcher.IsInputLane` / `IsOutputLane` helper methods
- ILS-style port configuration: planet logistics via belts only

## [0.2.0] - 2026-05-23

### Added
- Real LDB (Local DataBase) integration via Harmony hooks on `VFPreload.InvokeOnLoadWorkEnded`
- Real AssetBundle loader (icon + 3D model with vanilla fallback)
- Real `StorageComponent` patches (GetItemCount / TakeItem / AddItem)
- Configurable custom item ID and recipe ID (for conflict resolution)
- GitHub Actions CI for build verification
- GitHub Actions release workflow (auto-zip + auto-release on tag push)
- Issue templates (bug report, feature request, compatibility)
- Contributing guide
- Build instructions (`docs/BUILDING.md`)
- Unity Editor script for AssetBundle generation (`tools/AssetBundleBuilder.cs`)
- Real 256x256 mod icon (cosmic theme with planet + ring + storage crate)

### Changed
- Improved StorageManager with `Contains()` method for entity checks
- Storage-Overflow eviction respects "newest first" — recent deposits are protected
- Bumped version to 0.2.0 to reflect significant internal restructuring

### Notes
- AssetBundle is optional — mod functions correctly with vanilla visuals
- DSP source not bundled; only API signatures (stubs) are referenced in code
- For final integration, real DSP DLLs from `$DSP_GAME_PATH/DSPGAME_Data/Managed/`
  must be available at build time

## [0.1.0] - 2026-05-23

### Added
- Initial release
- Universal Planetary Depot custom building (item id 100001)
- Configurable item limit per slot (default: 5000)
- Dynamic slot allocation — auto-creates slots for new item types
- Optional overflow deletion (`DeleteOverflow` setting)
- Configurable max slot count (default: 1000)
- Warning threshold for near-full slots
- Nebula Multiplayer compatibility (no network-state changes)
- BepInEx config file at `BepInEx/config/com.boehla.dspuniversaldepot.cfg`
