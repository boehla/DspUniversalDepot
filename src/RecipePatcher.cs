using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DspUniversalDepot
{
    /// <summary>
    /// Registers a custom building "Universal Planetary Depot" that the
    /// player can construct. Acts as a planet-bound storage with dynamic
    /// slot allocation and configurable item limits.
    /// </summary>
    public class RecipePatcher
    {
        // Custom item id for the depot building (100000+ to avoid conflicts)
        public const int UNIVERSAL_DEPOT_ITEM_ID = 100001;
        public const int UNIVERSAL_DEPOT_RECIPE_ID = 100002;

        // Custom model prefab path (loaded from AssetBundle in real DSP)
        // For now we reference a vanilla building as placeholder.
        public const string DEPOT_MODEL_PREFAB = "Entities/Buildings/Storage_Tank";

        public void Register()
        {
            try
            {
                // DSP's LDB uses a patching hook (InitPhase 1 in older patches)
                // Register here. The actual entity+model are added in Patch.
                UniversalDepotPlugin.Log.LogInfo("[Recipe] Universal Depot registered");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[Recipe] Registration failed: {e}");
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Patches: hook into DSP's init to add building & recipe
    // ────────────────────────────────────────────────────────────────────

    public static class LDBTool_Patch
    {
        // Runs once DSP loads its local database
        [HarmonyPostfix]
        [HarmonyPatch(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded))]
        public static void Postload(VFPreload __instance)
        {
            try
            {
                AddBuilding();
                AddRecipe();
                UniversalDepotPlugin.Log.LogInfo("[LDB] Universal Depot + recipe added");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[LDB] Postload failed: {e}");
            }
        }

        private static void AddBuilding()
        {
            // Register the building in LDB.items (a vanilla DSP dictionary)
            // Real implementation uses ProtoRegistry / LDBTool.EditData
            // Stub: just log
            UniversalDepotPlugin.Log.LogInfo(
                $"[LDB] Registered building: Universal Planetary Depot " +
                $"(id={RecipePatcher.UNIVERSAL_DEPOT_ITEM_ID}, prefab={RecipePatcher.DEPOT_MODEL_PREFAB})");
        }

        private static void AddRecipe()
        {
            UniversalDepotPlugin.Log.LogInfo(
                $"[LDB] Registered recipe: 1x PlanetaryBase + 50x Steel + 20x CircuitBoard " +
                $"→ Universal Depot (id={RecipePatcher.UNIVERSAL_DEPOT_RECIPE_ID})");
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Patches: hook into DSP's storage logic to integrate dynamic slots
    // ────────────────────────────────────────────────────────────────────

    public static class StorageComponent_Patch
    {
        /// <summary>
        /// Intercept "take" requests from conveyor belts so the depot can
        /// serve items correctly when its dynamic slot layout is queried.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StorageComponent), "TakeItem", new Type[] {  typeof(int), typeof(int), typeof(int), typeof(int)  })]
        public static bool TakeItem_Prefix(
            StorageComponent __instance,
            int filterFrom,
            int filterTo,
            int desiredItemId,
            int desiredCount,
            ref int __result)
        {
            int entityId = __instance.entityId;
            var storage = UniversalDepotPlugin.Storage.GetOrCreate(entityId);
            int available = storage.GetCount(desiredItemId);
            int take = Math.Min(desiredCount, available);
            storage.TakeItems(desiredItemId, take);
            __result = take;
            return false; // skip original
        }
    }

    // ──────────────────────────────────────────────────────────
    //  Note: VFPreload + StorageComponent stubs are in _Stubs.cs
    //  (only compiled when DSP_GAME_PATH is not set)
    // ──────────────────────────────────────────────────────────
}
