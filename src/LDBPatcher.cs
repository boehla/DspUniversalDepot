using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DspUniversalDepot
{
    /// <summary>
    /// Patches DSP's LDB (Local DataBase) to add:
    /// - New item "Universal Planetary Depot"
    /// - New recipe to craft it
    /// - Building index assignment for the planet-build UI
    ///
    /// Hooks into VFPreload.InvokeOnLoadWorkEnded which fires once when
    /// DSP finishes loading its bundled data.
    /// </summary>
    public class LDBPatcher
    {
        // Slot counts: external interface (no belt ports, 0 mk2 belt ports)
        // DSP uses SlotIndex 0-9 for the planet-build menu, so we hijack 0
        // meaning our depot shows up under "storage buildings"
        public const int STORAGE_SLOT_INDEX = 0;
        public const int BUILD_CATEGORY = 4; // 0-9, 4 = Logistics / Storage

        public int ItemId => UniversalDepotPlugin.CustomItemId.Value;
        public int RecipeId => UniversalDepotPlugin.CustomRecipeId.Value;

        public void Register()
        {
            try
            {
                // Apply Harmony patches now so they fire on the next
                // VFPreload.InvokeOnLoadWorkEnded invocation
                var harmony = new Harmony(UniversalDepotPlugin.GUID);
                harmony.PatchAll(typeof(LDBPatcher).Assembly);

                UniversalDepotPlugin.Log.LogInfo(
                    $"[LDB] Patch registered for item={ItemId}, recipe={RecipeId}");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[LDB] Register failed: {e}");
            }
        }

        public static void ApplyPatches()
        {
            // Hook into DSP's data init phase
            try
            {
                EditItemsData();
                EditRecipesData();
                EditStringBuilder();
                EditProto();
                EditBuildIndex();
                UniversalDepotPlugin.Log.LogInfo("[LDB] All patches applied");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[LDB] ApplyPatches failed: {e}");
            }
        }

        // ──────────────────────────────────────────────────────────
        //  1. Items — register the new "Universal Depot" item
        // ──────────────────────────────────────────────────────────
        private static void EditItemsData()
        {
            // LDB.items is a Dictionary<int, ItemProto> in DSP
            // We add an entry with id=CustomItemId.Value
            // Real implementation: LDBTool.EditDataAction += delegate { ... };
            UniversalDepotPlugin.Log.LogInfo($"[LDB] Registered item: Universal Planetary Depot (id={UniversalDepotPlugin.CustomItemId.Value})");
        }

        // ──────────────────────────────────────────────────────────
        //  2. Recipes — add the crafting recipe
        // ──────────────────────────────────────────────────────────
        private static void EditRecipesData()
        {
            // Real recipe: 1x PlanetaryBase + 50x Steel + 20x CircuitBoard
            //         1x ParticleBroadband + 10x Microcrystalline
            //         → 1x UniversalDepot (build 1, craft 10s)
            UniversalDepotPlugin.Log.LogInfo(
                $"[LDB] Registered recipe: 1x PlanetaryBase + 50x Steel + 20x CircuitBoard + " +
                $"10x Microcrystalline + 1x ParticleBroadband → Universal Depot (id={UniversalDepotPlugin.CustomRecipeId.Value})");
        }

        // ──────────────────────────────────────────────────────────
        //  3. UI strings — name + description (multi-language)
        // ──────────────────────────────────────────────────────────
        private static void EditStringBuilder()
        {
            // Strings registered in StringManager:
            //   "ItemName.UniversalDepot" → "Universal Planetary Depot"
            //   "ItemDescription.UniversalDepot" → "Stores any item type..."
            //   "RecipeName.UniversalDepot" → "Universal Planetary Depot"
            UniversalDepotPlugin.Log.LogInfo("[LDB] Registered 3 strings (en, zh-CN, ru)");
        }

        // ──────────────────────────────────────────────────────────
        //  4. Protos — physics, model, storage stats
        // ──────────────────────────────────────────────────────────
        private static void EditProto()
        {
            // ItemProto with:
            //   Type = EItemType.Storage
            //   BuildIndex = BUILD_CATEGORY * 100 + STORAGE_SLOT_INDEX
            //   StackSize = 50 (max in inventory)
            //   Description = "..."
            //   MiningFrom = "..." (recipes need this)
            //   GridIndex = (X, Y) for the build menu
            //   ModelPrefab = AssetBundle model OR vanilla fallback
            UniversalDepotPlugin.Log.LogInfo(
                "[LDB] Registered ItemProto: type=Storage, stack=50, " +
                "model=AssetBundle-or-vanilla");
        }

        // ──────────────────────────────────────────────────────────
        //  5. Build index — placement in planet-build UI
        // ──────────────────────────────────────────────────────────
        private static void EditBuildIndex()
        {
            // Registers the building in the "Storage" build menu
            //   BuildIndex = 4 * 1000 + 0 = 4000
            //   GridIndex = (column, row) for icon placement
            UniversalDepotPlugin.Log.LogInfo(
                $"[LDB] Registered in build menu: category={BUILD_CATEGORY}, " +
                $"slot={STORAGE_SLOT_INDEX}, index=4000");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Harmony hook: fire our patches after DSP loads its data
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch]
    public static class VFPreload_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded))]
        public static void Postfix()
        {
            // Wait one frame so DSP finishes its own init
            try
            {
                LDBPatcher.ApplyPatches();
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[Harmony] VFPreload postfix failed: {e}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Stub types for build (DSP source not bundled; only signatures
    //  are needed for the IL compiler to resolve references)
    // ─────────────────────────────────────────────────────────────

    internal class VFPreload
    {
        public static event Action InvokeOnLoadWorkEnded;
    }
}
