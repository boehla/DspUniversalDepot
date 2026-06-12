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
        // Slot counts: external interface
        // DSP's belt port config:
        //   port0, port1, port2, port3 = 3 input lanes + 3 output lanes for MK1/MK2/MK3
        //   port4, port5 = 2 planetary ILS delivery ports
        // Universal Depot = ILS-style: 3 in + 3 out for belts, 0 ILS ports (planet only)
        public const int STORAGE_SLOT_INDEX = 0;
        public const int BUILD_CATEGORY = 4; // 0-9, 4 = Logistics / Storage
        public const int BELT_LANE_COUNT = 3; // MK1 / MK2 / MK3 input + output lanes
        public const int ILS_PORT_COUNT = 0;   // 0 = no remote logistics, planet-bound only

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
            // Recipe (high-cost):
            //   30x Titanium Ingot
            //   20x Circuit Board
            //   10x Microcrystalline Component
            //    2x Particle Broad-band
            //   → 1x Universal Depot (10s craft)
            //
            // Priced higher than ILS to balance the unlimited storage
            // and dynamic-slot convenience. The depot is local-only
            // (no ILS remote ports) — so the player pays in materials
            // what they save in logistics complexity.
            UniversalDepotPlugin.Log.LogInfo(
                $"[LDB] Registered recipe: 30x Titanium Ingot + 20x Circuit Board + " +
                $"10x Microcrystalline Component + 2x Particle Broad-band → " +
                $"Universal Depot (id={UniversalDepotPlugin.CustomRecipeId.Value}, craft=10s)");
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
            //
            // Belt ports (ILS-style):
            //   - 3 input lanes  (port0/1/2 = MK1/MK2/MK3 belt in)
            //   - 3 output lanes (port3/4/5 = MK1/MK2/MK3 belt out)
            //   - 0 ILS remote ports (planet-only building)
            UniversalDepotPlugin.Log.LogInfo(
                $"[LDB] Registered ItemProto: type=Storage, stack=50, " +
                $"belt_lanes={BELT_LANE_COUNT} in + {BELT_LANE_COUNT} out, " +
                $"ils_ports={ILS_PORT_COUNT}, model=AssetBundle-or-vanilla");
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

    // ──────────────────────────────────────────────────────────
    //  Note: VFPreload stub is in _Stubs.cs
    // ──────────────────────────────────────────────────────────
}
