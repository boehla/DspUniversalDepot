# Releasing DspUniversalDepot

This is the exact, repeatable process for cutting a new release and producing the
Thunderstore / r2modman zip. Follow it top to bottom.

## TL;DR

```bash
# 1. bump the version in all three files (see step 1)
# 2. add a CHANGELOG entry (see step 2)
# 3. build + package:
bash dotnet/scripts/package-r2modman.sh
# → writes releases/DspUniversalDepot-<version>-r2modman.zip
# 4. (optional) test locally, commit + tag, upload to Thunderstore
```

## Versioning

[Semantic Versioning](https://semver.org): `MAJOR.MINOR.PATCH`.

- **PATCH** (0.7.3 → 0.7.4) — bug fixes only, no new behaviour.
- **MINOR** (0.7.3 → 0.8.0) — a new feature, backwards compatible.
- **MAJOR** (0.x → 1.0) — breaking change (rare for a mod; e.g. dropping save compat).

## Step 1 — bump the version (THREE places, must match)

The version lives in three files and they must all agree:

| File | Field |
| --- | --- |
| `manifest.json` | `"version_number": "X.Y.Z"` |
| `src/DspUniversalDepot.csproj` | `<Version>X.Y.Z</Version>` |
| `src/UniversalDepotPlugin.cs` | `public const string VERSION = "X.Y.Z";` |

`manifest.json` is the source of truth the packaging script reads for the zip filename.

Quick check that all three agree:

```bash
grep -h "X.Y.Z" manifest.json src/DspUniversalDepot.csproj src/UniversalDepotPlugin.cs
```

## Step 2 — update the changelog

Add a new section at the **top** of the changelog list in `CHANGELOG.md` (above the previous
version), dated today, following the existing Keep-a-Changelog style:

```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added / Fixed / Changed: short headline

A paragraph on what changed and why, then bullets for the detail.
```

Keep the README's `[Design]` / config block in sync if the release adds or renames a config option.

## Step 3 — build and package

```bash
bash dotnet/scripts/package-r2modman.sh
```

What the script does:

1. Reads the version from `manifest.json`.
2. Builds `src/DspUniversalDepot.csproj` in **Release**. The post-build auto-deploy to the
   r2modman profile may print copy errors **if the game is currently running** — that is
   expected and harmless; the script ignores it and verifies the DLL exists instead.
3. Stages exactly five files and zips them **flat** (no top-level folder, which Thunderstore
   requires) into `releases/DspUniversalDepot-<version>-r2modman.zip`:
   - `DspUniversalDepot.dll` (from `src/bin/Release/`)
   - `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`
4. Prints the archive's contents so you can eyeball the file list and the DLL size.

The script needs no `zip` binary — it uses PowerShell's `Compress-Archive`.

## Step 4 — verify locally (recommended)

The post-build step auto-deploys the DLL into the r2modman profile
(`%AppData%\r2modmanPlus-local\DysonSphereProgram\profiles\main\BepInEx\plugins\…`), **but only
when DSP is closed** (a running game holds the DLL locked).

1. Close DSP.
2. Re-run the build (or the package script) so the fresh DLL deploys.
3. Launch DSP, load a save, place a depot, and confirm it builds, renders and behaves.

For the rendering side specifically, `MeshDebugBox = true` in the config draws a plain box —
a fast sanity check that the custom-mesh pipeline still works after geometry changes.

## Step 5 — commit, tag, publish

```bash
git add -A
git commit -m "release: vX.Y.Z <headline>"
git tag vX.Y.Z
git push && git push --tags
```

Then upload `releases/DspUniversalDepot-<version>-r2modman.zip` to Thunderstore
(https://thunderstore.io) — Upload → select the zip → submit. Thunderstore validates that
`manifest.json`, `icon.png` (256×256) and `README.md` sit at the archive root, which the
packaging script guarantees.

## Package layout reference

A valid r2modman/Thunderstore zip is flat:

```
DspUniversalDepot-X.Y.Z-r2modman.zip
├── DspUniversalDepot.dll
├── manifest.json
├── icon.png
├── README.md
└── CHANGELOG.md
```

No folders, no `BepInEx/` prefix — r2modman places the DLL for you.
