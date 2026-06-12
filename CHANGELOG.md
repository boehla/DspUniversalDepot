# Changelog

All notable changes to DspUniversalDepot are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

### Notes
- The plugin provides the storage + logic; full 3D-model integration with
  the planet-building UI requires a custom AssetBundle (TODO for v0.2.0).
- Currently the depot uses a vanilla building as placeholder visual.
