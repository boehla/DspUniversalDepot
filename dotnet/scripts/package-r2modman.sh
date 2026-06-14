#!/usr/bin/env bash
# Build a Thunderstore / r2modman package for DspUniversalDepot.
#
# Produces releases/DspUniversalDepot-<version>-r2modman.zip — a FLAT zip (no top-level
# folder) containing exactly the files r2modman/Thunderstore expect at the archive root:
#   DspUniversalDepot.dll  manifest.json  icon.png  README.md  CHANGELOG.md
#
# The version is read from manifest.json (the single source of truth for the package version),
# so bump that (plus the csproj <Version> and the plugin VERSION const) before running.
#
# Usage:  bash dotnet/scripts/package-r2modman.sh
set -euo pipefail

# Repo root = two levels up from this script (dotnet/scripts/ -> repo).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO"

# 1) Version from manifest.json.
VERSION="$(grep -oE '"version_number"[[:space:]]*:[[:space:]]*"[^"]+"' manifest.json | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')"
if [ -z "$VERSION" ]; then echo "ERROR: could not read version_number from manifest.json" >&2; exit 1; fi
echo "Packaging DspUniversalDepot v$VERSION"

# 2) Build Release (does NOT depend on the auto-deploy; that target may fail if the game is
#    running, but the DLL is still produced under src/bin/Release).
dotnet build src/DspUniversalDepot.csproj -c Release -nologo --verbosity quiet || true
DLL="src/bin/Release/DspUniversalDepot.dll"
if [ ! -f "$DLL" ]; then echo "ERROR: $DLL not found — build failed" >&2; exit 1; fi

# 3) Stage exactly the five package files, flat.
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
cp "$DLL" "$STAGE/DspUniversalDepot.dll"
cp manifest.json icon.png README.md CHANGELOG.md "$STAGE/"

# 4) Zip the staged files at the archive root (PowerShell, since `zip` isn't on this box).
mkdir -p releases
OUT="releases/DspUniversalDepot-$VERSION-r2modman.zip"
rm -f "$OUT"
OUT_WIN="$(cd releases && pwd -W)/DspUniversalDepot-$VERSION-r2modman.zip"
STAGE_WIN="$(cd "$STAGE" && pwd -W)"
powershell.exe -NoProfile -Command \
  "Compress-Archive -Path '$STAGE_WIN\\*' -DestinationPath '$OUT_WIN' -Force"

echo "Wrote $OUT"
powershell.exe -NoProfile -Command \
  "Add-Type -A System.IO.Compression.FileSystem; \
   [IO.Compression.ZipFile]::OpenRead('$OUT_WIN').Entries | ForEach-Object { '{0,9}  {1}' -f \$_.Length, \$_.FullName }"
