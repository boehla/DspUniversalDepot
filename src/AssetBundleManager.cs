using BepInEx.Logging;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DspUniversalDepot
{
    /// <summary>
    /// Loads the AssetBundle (icon, 3D model) embedded as a BepInEx resource.
    /// Falls back to DSP's vanilla storage tank if AssetBundle is missing —
    /// the mod still works, just with a default visual.
    /// </summary>
    public class AssetBundleManager
    {
        public AssetBundle Bundle;
        public Sprite Icon;
        public GameObject ModelPrefab;
        public Texture2D IconTexture;

        public bool HasCustomAssets =>
            Bundle != null && Icon != null && ModelPrefab != null;

        public void Load()
        {
            try
            {
                string bundlePath = ResolveAssetBundlePath();
                if (string.IsNullOrEmpty(bundlePath) || !File.Exists(bundlePath))
                {
                    UniversalDepotPlugin.Log?.LogWarning(
                        $"[Assets] AssetBundle not found (searched plugin dirs), " +
                        "using vanilla fallback.");
                    return;
                }

                Bundle = AssetBundle.LoadFromFile(bundlePath);
                if (Bundle == null)
                {
                    UniversalDepotPlugin.Log?.LogError(
                        "[Assets] Failed to load AssetBundle from " + bundlePath);
                    return;
                }

                // Load icon (256x256 PNG, named "icon")
                IconTexture = Bundle.LoadAsset<Texture2D>("icon");
                if (IconTexture != null)
                {
                    Icon = Sprite.Create(
                        IconTexture,
                        new Rect(0, 0, IconTexture.width, IconTexture.height),
                        new Vector2(0.5f, 0.5f));
                }

                // Load 3D model prefab (named "prefab")
                ModelPrefab = Bundle.LoadAsset<GameObject>("prefab");

                UniversalDepotPlugin.Log?.LogInfo(
                    $"[Assets] Loaded: icon={(Icon != null ? "yes" : "no")}, " +
                    $"model={(ModelPrefab != null ? "yes" : "no")} from {bundlePath}");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log?.LogError($"[Assets] Load failed: {e}");
            }
        }

        /// <summary>
        /// Find universaldepot.assets in any of the candidate paths:
        ///   1. Same directory as the DLL (standard r2modman install)
        ///   2. BepInEx/plugins/ (dev install)
        ///   3. BepInEx/plugins/DspUniversalDepot/ (subfolder)
        ///   4. Game root (portable install)
        /// </summary>
        private static string ResolveAssetBundlePath()
        {
            string dllDir = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);

            string[] candidates =
            {
                Path.Combine(dllDir ?? "", "universaldepot.assets"),
                Path.Combine(dllDir ?? "", "..", "universaldepot.assets"),
                Path.Combine(dllDir ?? "", "..", "DspUniversalDepot", "universaldepot.assets"),
                Path.Combine(dllDir ?? "", "..", "..", "..", "universaldepot.assets"),
            };

            foreach (string path in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(path);
                    if (File.Exists(full)) return full;
                }
                catch
                {
                    // Skip invalid paths
                }
            }
            return null;
        }

        public void Unload()
        {
            if (Bundle != null)
            {
                Bundle.Unload(true);
                Bundle = null;
            }
            Icon = null;
            ModelPrefab = null;
            IconTexture = null;
        }
    }
}
