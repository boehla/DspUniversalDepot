#!/bin/bash
# download-libs.sh — fetch all reference DLLs from NuGet + GitHub
# Run this once after cloning the repo, or when upgrading dependency versions.
# Requires: curl, python3 (with zipfile module)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LIBS_DIR="$REPO_ROOT/libs"
NUGET_CACHE="$(mktemp -d)"

echo "==> Downloading reference DLLs to $LIBS_DIR"
mkdir -p "$LIBS_DIR"
cd "$NUGET_CACHE"

# Helper: download latest version of a NuGet package
fetch_nuget() {
  local pkg="$1"
  local id_lower="${pkg,,}"
  local url="https://api.nuget.org/v3-flatcontainer/${id_lower}/index.json"
  local ver
  ver=$(curl -sL --max-time 10 "$url" | python3 -c "import json,sys; print(json.load(sys.stdin)['versions'][-1])")
  if [ -z "$ver" ]; then
    echo "  ✗ $pkg not found on NuGet"
    return 1
  fi
  echo "  ↓ $pkg v$ver"
  curl -sL --max-time 60 "https://api.nuget.org/v3-flatcontainer/${id_lower}/${ver}/${id_lower}.${ver}.nupkg" \
    -o "${id_lower}.${ver}.nupkg"
}

# DSP modding packages
echo "==> DSP modding packages (NuGet)"
for pkg in \
  "DysonSphereProgram.Modding.CommonAPI" \
  "DysonSphereProgram.Modding.NebulaMultiplayerModApi" \
  "DysonSphereProgram.Modding.LDBTool" \
  "DysonSphereProgram.Modding.DSPModSave" \
  "UnityEngine.Modules" \
  "BepInEx.AssemblyPublicizer.MSBuild"
do
  fetch_nuget "$pkg"
done

# BepInEx pack (from GitHub releases)
echo "==> BepInEx 5.4.23.5 (GitHub releases)"
curl -sL --max-time 60 \
  "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x86_5.4.23.5.zip" \
  -o "bepinex.zip"

# Extract DLLs from all nupkg + zip
echo "==> Extracting DLLs"
NUGET_CACHE="$NUGET_CACHE" LIBS_DIR="$LIBS_DIR" python3 << 'PYEOF'
import zipfile
import os
import shutil

cache = os.environ['NUGET_CACHE']
out = os.environ['LIBS_DIR']
os.makedirs(out, exist_ok=True)

for f in os.listdir(cache):
    if not f.endswith(('.nupkg', '.zip')):
        continue
    path = os.path.join(cache, f)
    with zipfile.ZipFile(path) as z:
        for info in z.infolist():
            # DSP packages: prefer lib/net472/
            if 'lib/net472/' in info.filename and info.filename.endswith('.dll'):
                target = info.filename.replace('lib/net472/', '')
                # Skip AssemblyPublicizer (build-time tool, not runtime)
                if 'AssemblyPublicizer' in target:
                    continue
                target_path = os.path.join(out, target)
                with z.open(info) as src, open(target_path, 'wb') as dst:
                    shutil.copyfileobj(src, dst)
                print(f'  ✓ {target}')
            # UnityEngine: use lib/netstandard2.0/
            elif 'lib/netstandard2.0/' in info.filename and info.filename.endswith('.dll'):
                target = info.filename.replace('lib/netstandard2.0/', '')
                target_path = os.path.join(out, target)
                with z.open(info) as src, open(target_path, 'wb') as dst:
                    shutil.copyfileobj(src, dst)
                print(f'  ✓ {target}')
            # BepInEx pack: any *.dll in BepInEx/core/
            elif 'BepInEx/core/' in info.filename and info.filename.endswith('.dll'):
                target = info.filename.replace('BepInEx/core/', '')
                target_path = os.path.join(out, target)
                with z.open(info) as src, open(target_path, 'wb') as dst:
                    shutil.copyfileobj(src, dst)
                print(f'  ✓ {target}')
PYEOF

# Cleanup
rm -rf "$NUGET_CACHE"

echo "==> Done. $(ls "$LIBS_DIR" | wc -l) DLLs in $LIBS_DIR"
ls -la "$LIBS_DIR" | head -10
echo "..."
echo ""
echo "Build the project with: dotnet build -c Release"
echo "(See DspUniversalDepot.csproj comments for build modes.)"
