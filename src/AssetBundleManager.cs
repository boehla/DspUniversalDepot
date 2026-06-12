using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public bool HasCustomAssets => Bundle != null && Icon != null && ModelPrefab != null;

        public void Load()
        {
            try
            {
                string pluginPath = Path.GetDirectoryName(typeof(UniversalDepotPlugin).Assembly.Location);
                string bundlePath = Path.Combine(pluginPath, "universaldepot.assets");

                if (!File.Exists(bundlePath))
                {
                    UniversalDepotPlugin.Log.LogWarning(
                        $"[Assets] AssetBundle not found at {bundlePath}, using vanilla fallback");
                    return;
                }

                Bundle = AssetBundle.LoadFromFile(bundlePath);
                if (Bundle == null)
                {
                    UniversalDepotPlugin.Log.LogError("[Assets] Failed to load AssetBundle");
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

                UniversalDepotPlugin.Log.LogInfo(
                    $"[Assets] Loaded: icon={(Icon != null ? "yes" : "no")}, " +
                    $"model={(ModelPrefab != null ? "yes" : "no")}");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[Assets] Load failed: {e}");
            }
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
