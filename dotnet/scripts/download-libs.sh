#!/bin/bash
# download-libs.sh — populate ../libs/ with the reference DLLs needed to build.
# Run once after cloning, or when upgrading dependency versions.
# Requires: curl, python3, unzip.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LIBS_DIR="$REPO_ROOT/libs"
TMP="$(mktemp -d)"
mkdir -p "$LIBS_DIR"

# --- 1. Game DLLs (Mono / Assembly-CSharp) from the local DSP install ---------
DSP_GAME_PATH="${DSP_GAME_PATH:-/c/Program Files (x86)/Steam/steamapps/common/Dyson Sphere Program}"
MANAGED="$DSP_GAME_PATH/DSPGAME_Data/Managed"
if [ ! -d "$MANAGED" ]; then
  echo "✗ DSP Managed folder not found at: $MANAGED"
  echo "  Set DSP_GAME_PATH to the folder containing DSPGAME.exe and re-run."
  exit 1
fi
echo "==> Copying game DLLs from $MANAGED"
for dll in Assembly-CSharp.dll UnityEngine.dll UnityEngine.CoreModule.dll UnityEngine.UI.dll UnityEngine.TextRenderingModule.dll UnityEngine.ImageConversionModule.dll; do
  cp "$MANAGED/$dll" "$LIBS_DIR/"
  echo "  ✓ $dll"
done

# --- 2. BepInEx 5.4.21 (Mono x64) --------------------------------------------
echo "==> BepInEx 5.4.21 (GitHub)"
curl -sL --max-time 120 \
  "https://github.com/BepInEx/BepInEx/releases/download/v5.4.21/BepInEx_x64_5.4.21.0.zip" \
  -o "$TMP/bepinex.zip"
unzip -oq "$TMP/bepinex.zip" -d "$TMP/bep"
cp "$TMP/bep/BepInEx/core/BepInEx.dll" "$TMP/bep/BepInEx/core/0Harmony.dll" "$LIBS_DIR/"
echo "  ✓ BepInEx.dll, 0Harmony.dll"

# --- 3. LDBTool (NuGet) -------------------------------------------------------
echo "==> LDBTool (NuGet)"
PKG="dysonsphereprogram.modding.ldbtool"
VER="$(curl -sL --max-time 15 "https://api.nuget.org/v3-flatcontainer/${PKG}/index.json" \
  | python3 -c "import json,sys; print(json.load(sys.stdin)['versions'][-1])")"
curl -sL --max-time 60 \
  "https://api.nuget.org/v3-flatcontainer/${PKG}/${VER}/${PKG}.${VER}.nupkg" -o "$TMP/ldbtool.nupkg"
unzip -oq "$TMP/ldbtool.nupkg" -d "$TMP/ldbtool"
cp "$TMP/ldbtool/lib/net472/LDBTool.dll" "$LIBS_DIR/"
echo "  ✓ LDBTool.dll v$VER"

# --- 4. NebulaAPI (Thunderstore, optional soft dependency) -------------------
echo "==> NebulaAPI (Thunderstore)"
NEB_URL="$(curl -sL --max-time 15 "https://thunderstore.io/api/experimental/package/nebula/NebulaMultiplayerModApi/" \
  | python3 -c "import json,sys; print(json.load(sys.stdin)['latest']['download_url'])" 2>/dev/null)"
if [ -n "$NEB_URL" ] && curl -sL --max-time 60 "$NEB_URL" -o "$TMP/nebapi.zip"; then
  unzip -oq "$TMP/nebapi.zip" -d "$TMP/nebapi"
  NEB_DLL="$(find "$TMP/nebapi" -iname 'NebulaAPI.dll' | head -1)"
  [ -n "$NEB_DLL" ] && cp "$NEB_DLL" "$LIBS_DIR/" && echo "  ✓ NebulaAPI.dll"
else
  echo "  ! NebulaAPI.dll not fetched (multiplayer build needs it). Copy it from a Nebula install."
fi

rm -rf "$TMP"
echo "==> Done. $(ls "$LIBS_DIR" | wc -l) DLLs in $LIBS_DIR"
echo "Build with: cd src && dotnet build -c Release"
