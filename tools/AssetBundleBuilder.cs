// Unity Editor script — run via:
//   Unity.exe -batchmode -quit -projectPath . -executeMethod AssetBundleBuilder.Build
//
// This script bundles:
//   - assets/icon.png (256x256)         → AssetBundle "icon"
//   - assets/prefab/UniversalDepot.fbx  → AssetBundle "prefab"
//
// Output: Assets/StreamingAssets/universaldepot.assets
// Copy to: BepInEx/plugins/ next to the DLL.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class AssetBundleBuilder
{
    private const string SOURCE_ICON = "Assets/source/icon.png";
    private const string SOURCE_PREFAB = "Assets/source/UniversalDepot.fbx";
    private const string OUTPUT_DIR = "Assets/StreamingAssets";
    private const string BUNDLE_NAME = "universaldepot.assets";

    [MenuItem("Build/Build Universal Depot AssetBundle")]
    public static void Build()
    {
        Debug.Log("[AssetBundleBuilder] Starting...");

        // Verify source files exist
        if (!File.Exists(SOURCE_ICON))
        {
            Debug.LogError($"[AssetBundleBuilder] Missing source icon: {SOURCE_ICON}");
            EditorApplication.Exit(1);
            return;
        }
        if (!File.Exists(SOURCE_PREFAB))
        {
            Debug.LogWarning(
                $"[AssetBundleBuilder] Missing source prefab: {SOURCE_PREFAB}. " +
                "Bundle will have no 3D model. The mod will use DSP's " +
                "vanilla storage tank as fallback.");
        }

        // Create output directory
        if (!Directory.Exists(OUTPUT_DIR))
        {
            Directory.CreateDirectory(OUTPUT_DIR);
        }

        // Configure build
        var build = new BuildAssetBundleOptions
        {
            BundleType = BuildAssetBundleOptions.ChunkBasedCompression,
            TargetPlatform = BuildTarget.StandaloneWindows64,
        };

        // Collect assets to bundle
        var assetsToBundle = new System.Collections.Generic.List<string>();
        assetsToBundle.Add(SOURCE_ICON);
        if (File.Exists(SOURCE_PREFAB))
        {
            assetsToBundle.Add(SOURCE_PREFAB);
        }

        // Build
        string outputPath = Path.Combine(OUTPUT_DIR, BUNDLE_NAME);
        var manifest = BuildPipeline.BuildAssetBundles(
            OUTPUT_DIR,
            new[] { new AssetBundleBuild
            {
                assetBundleName = BUNDLE_NAME,
                assetNames = assetsToBundle.ToArray()
            }},
            build,
            BuildTarget.StandaloneWindows64
        );

        if (manifest == null)
        {
            Debug.LogError("[AssetBundleBuilder] Build failed!");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[AssetBundleBuilder] OK → {outputPath}");
        Debug.Log($"[AssetBundleBuilder] Size: {new FileInfo(outputPath).Length} bytes");
        EditorApplication.Exit(0);
    }
}
#endif
