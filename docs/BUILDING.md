# Building DspUniversalDepot

This guide explains how to build the mod from source.

## Prerequisites

| Tool | Version | Why |
|------|---------|-----|
| .NET SDK | 6.0+ | Compiles the C# plugin DLL |
| Unity Editor | 2022.3.x | Builds the AssetBundle (icon + 3D model) |
| Dyson Sphere Program | 0.10.x | Provides UnityEngine + Assembly-CSharp references |

## Step 1: Build the plugin DLL

Set the `DSP_GAME_PATH` environment variable to your DSP install directory:

```powershell
# Windows PowerShell
$env:DSP_GAME_PATH = "C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program"
cd src
dotnet build -c Release
```

Output: `src/bin/Release/net6.0/DspUniversalDepot.dll`

## Step 2: Build the AssetBundle (optional but recommended)

If you have Unity 2022.3.x installed:

1. Open Unity Hub → New Project → 3D URP Template
2. Copy the contents of `tools/AssetBundleBuilder.cs` to `Assets/Editor/`
3. Copy `icons/icon.png` to `Assets/source/icon.png`
4. (Optional) Add a 3D model to `Assets/source/UniversalDepot.fbx`
5. Run: `Unity.exe -batchmode -quit -projectPath . -executeMethod AssetBundleBuilder.Build`
6. Output: `Assets/StreamingAssets/universaldepot.assets`

## Step 3: Install

Copy these files to your DSP `BepInEx/plugins/` folder:

```
BepInEx/plugins/DspUniversalDepot/
  ├── DspUniversalDepot.dll       (from step 1)
  └── universaldepot.assets       (from step 2, optional)
```

## Step 4: Configure

The config file is auto-generated on first launch at:
`BepInEx/config/com.boehla.dspuniversaldepot.cfg`

## Troubleshooting

### "DSP_GAME_PATH not set"
Set the env var before running `dotnet build`. The build script references
`$DSP_GAME_PATH/DSPGAME_Data/Managed/*.dll` for Unity/Assembly-CSharp.

### "Missing reference to Assembly-CSharp"
The DSP install path is wrong. Verify `$env:DSP_GAME_PATH` points to the
folder containing `DSPGAME.exe` and `DSPGAME_Data/`.

### "AssetBundle not found at runtime"
This is normal if you skipped step 2. The mod falls back to DSP's
vanilla storage tank visual and still works functionally.
