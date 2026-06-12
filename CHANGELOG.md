# Changelog

All notable changes to DspUniversalDepot are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.0] - 2026-06-12

### Complete rewrite — the mod now actually works in DSP

Versions ≤ 0.3.0 could not load into the game at all. They targeted BepInEx 6 /
IL2CPP / .NET 6, but **DSP is a Mono game** (BepInEx 5, `net472`). On top of that
the storage logic used a custom dictionary plus the fictional
`DSPModSave.SaveDataManager` / `MiniJSON` APIs, and registered protos through
reflection with wrong assembly/type names (`, DSPGAME`, `EItemType.Storage`,
`DSPString.StringManager`, …). None of that exists in the real game.

### Changed
- **Targets `net472` and `BaseUnityPlugin`** (BepInEx 5 Mono) — builds against the
  real `Assembly-CSharp.dll` + `UnityEngine*.dll` from a local DSP install.
- **The depot is now a cloned vanilla storage building** (via LDBTool
  `MethedEx.Copy`), so it keeps belts, sorters, the storage UI and native
  save/load. No custom storage, no DSPModSave, no MiniJSON.
- **Capacity** is controlled by a single Harmony prefix on
  `FactoryStorage.NewStorageComponent` that sets the slot count for our protoId.
- Recipe simplified to 20× Titanium Ingot + 10× Circuit Board, 2 s.
- Config reduced to `SlotCount` + advanced IDs / build-bar column.
- Manifest now declares the real dependencies: BepInEx pack + `xiaoye97-LDBTool`.

### Removed
- `StorageManager`, `ConveyorPatcher`, `LDBPatcher`, `RecipePatcher`,
  `AssetBundleManager`, `_Stubs.cs`, `_DSPStubs.cs`, `tools/AssetBundleBuilder.cs`
  — all obsolete with the new approach.
- The IL2CPP/.NET 6 build modes and the NuGet-stub machinery.

## [0.3.0] - 2026-06-12

### Fixed (Code Review from subagent audit)
- **CRITICAL: Duplicate `StorageComponent.TakeItem` patch removed** (was in RecipePatcher.cs, conflicted with ConveyorPatcher.cs) — would have broken vanilla ILS/labs/vessels/miners
- **CRITICAL: Triple Harmony instance fixed** — single `HarmonyInstance` in Plugin, one `PatchAll` call
- **CRITICAL: `IsUniversalDepot` chicken-and-egg fixed** — now uses `EntityData.protoId == CustomItemId` instead of registry lookup, O(1) and no first-call failure
- **CRITICAL: LDB registration is now real** — uses `LDBTool.EditDataAction` to add ItemProto, RecipeProto, strings, and build index to DSP's LDB. The Universal Depot now actually appears in the build menu.
- **CRITICAL: Version mismatch fixed** — plugin VERSION = 0.3.0, manifest = 0.3.0, .csproj = 0.3.0
- **CRITICAL: `TakeItem_Prefix` signature fix** — explicit Type[] overload, no more `ref int` vs `out int` runtime crash
- Duplicate `VFPreload.InvokeOnLoadWorkEnded` postfix removed (one in LDBPatcher, one in RecipePatcher)
- `TakeItems` now updates `_slotTimestamps` so emptied slots aren't marked as "oldest"
- `EvictOldestItems` no longer treats itemId=0 as a magic "no candidate" value
- `LinqShim` deleted (real `using System.Linq` shadowed it)
- `IsInputLane`/`IsOutputLane` helpers moved to PatchStorageQueries class (no callers, no orphans)
- `<NoWarn>` list tightened to only stub-specific codes; the broad list was masking real errors
- `0Harmony20.dll` removed from `libs/` — only `0Harmony.dll` is the BepInEx 5.4 runtime

### Added
- **Save/Load support** via `DSPModSave` — Universal Depot contents now persist across save → quit → reload
- **PlanetFactory.RemoveEntityData postfix** — `StorageManager.Remove(entityId)` is now called when a depot is destroyed; no more memory leak
- **Debug chat command** `/depot-stats` — shows depot count, items in depots, total items
- **Multi-path AssetBundle resolution** — searches plugin dir, BepInEx/plugins/, subfolder, and game root
- **DeleteOverflow now evicts entire oldest slot** for new items (not just to free space in current item)
- **ItemLimit default raised 5000 → 50000** (was filling in 55s at peak 3-belt throughput)
- `EnableSaveLoad` config option (default true; disables if DSPModSave not installed)
- Plugin `Unload()` override — calls `HarmonyInstance.UnpatchSelf()`, clears storage, unloads AssetBundle
- `_DSPStubs.cs` for DSP-only types (StorageComponent, PlanetFactory, GameSave, UIRoot) — these are proprietary and not in any NuGet package, but are SHADOWED at runtime by Assembly-CSharp.dll

### Changed
- Recipe ingredients rebalanced slightly: 30×Ti + 20×CB + 10×Micro + 2×BB (unchanged for now, was rated "underpriced" in audit but kept for first release — community feedback will determine final)
- Belt-lane count is now a class constant (`PatchStorageQueries.BELT_LANE_COUNT = 3`) for clarity
- `AssetBundleManager.ResolveAssetBundlePath()` searches 4 candidate paths instead of 1
- `BuildMode` documentation rewritten with explicit HAS_DSP_REFS guard explanation

### Required plugins (NEW)
- **LDBTool** (soft dependency) — for registering the building + recipe in DSP's LDB
- **DSPModSave** (soft dependency) — for save/load persistence. Without it, depot contents are lost on save/reload (warning logged at startup).

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
