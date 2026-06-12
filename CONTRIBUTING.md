# Contributing

Thanks for your interest in DspUniversalDepot!

## Development Setup

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Set `DSP_GAME_PATH` env var to your DSP install
4. Make your changes in `src/`
5. Build and test locally (you need DSP for integration tests)
6. Commit with conventional commits: `feat:`, `fix:`, `docs:`, `chore:`
7. Push and open a Pull Request

## Code Style

- C# 10 / .NET 6.0
- 4-space indentation
- `PascalCase` for types, methods, properties
- `camelCase` for local vars and params
- `SCREAMING_SNAKE_CASE` for constants
- XML doc comments for public API

## Testing

- Add tests for new features (we use xUnit, suite in `tests/`)
- Verify your changes don't break Nebula Multiplayer compat
- Test with various item types (regular, recipes, warpers)

## Release Process

1. Update `CHANGELOG.md` with your changes
2. Bump version in `src/UniversalDepotPlugin.cs` (VERSION constant)
3. Bump version in `manifest.json`
4. Commit: `git commit -am "chore: release v0.x.0"`
5. Tag: `git tag v0.x.0`
6. Push tag: `git push origin v0.x.0`
7. GitHub Actions will create a release

## Nebula Compatibility Rules

- ❌ Don't patch anything in `DSP.Network` namespace
- ❌ Don't modify synced GameObject state
- ✅ Only patch local `StorageComponent` and `VFPreload` callbacks
- ✅ Only store data in our own dictionaries, never in DSP's persistent storage
