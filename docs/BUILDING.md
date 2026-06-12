# Building DspUniversalDepot

## Prerequisites

| Tool | Version | Why |
|------|---------|-----|
| .NET SDK | 6.0+ | Compiles the C# plugin (project targets `net472`) |
| Dyson Sphere Program | installed | Provides `Assembly-CSharp.dll` + `UnityEngine*.dll` |

DSP runs on **Mono**, so the plugin targets `net472` and is a BepInEx 5
`BaseUnityPlugin`. There is no IL2CPP / .NET 6 runtime involved.

## Step 1: Fetch reference DLLs

```bash
bash dotnet/scripts/download-libs.sh
```

This populates `../libs/` (gitignored) with:

- `Assembly-CSharp.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`
  — copied from your local DSP install,
- `BepInEx.dll`, `0Harmony.dll` — BepInEx 5.4.21,
- `LDBTool.dll` — from NuGet (`DysonSphereProgram.Modding.LDBTool`).

If DSP is not at the default Steam path, set `DSP_GAME_PATH` first:

```bash
export DSP_GAME_PATH="/c/Program Files (x86)/Steam/steamapps/common/Dyson Sphere Program"
bash dotnet/scripts/download-libs.sh
```

## Step 2: Build

```bash
cd src
dotnet build -c Release
```

Output: `src/bin/Release/DspUniversalDepot.dll`

## Step 3: Install for testing

```
BepInEx/plugins/DspUniversalDepot/DspUniversalDepot.dll
BepInEx/plugins/LDBTool/LDBTool.dll        (required dependency)
```

Launch DSP. The config is generated at
`BepInEx/config/com.boehla.dspuniversaldepot.cfg`, and the depot appears in the
storage build category.

## Troubleshooting

### "Missing reference to Assembly-CSharp"
`../libs/` is empty or DSP wasn't found. Re-run `download-libs.sh` (optionally
with `DSP_GAME_PATH` set to the folder containing `DSPGAME.exe`).

### The depot doesn't appear in the build menu
LDBTool must be installed in `BepInEx/plugins/`. Check `BepInEx/LogOutput.log`
for the `[Depot] Registered item ...` line.
