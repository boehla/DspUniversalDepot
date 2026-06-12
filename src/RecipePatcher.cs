using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace DspUniversalDepot
{
    /// <summary>
    /// Registers the "Universal Planetary Depot" building via LDBTool.
    /// Once DSP finishes loading its bundled data (VFPreload.InvokeOnLoadWorkEnded),
    /// we use LDBTool.EditDataAction to add:
    ///   1. A new ItemProto to LDB.items
    ///   2. A new RecipeProto to LDB.recipes
    ///   3. UI strings (name + description)
    ///   4. A buildIndex entry in the "Logistics" category
    /// </summary>
    public class RecipePatcher
    {
        // Custom IDs (configurable via BepInEx)
        public static int UniversalDepotItemId =>
            UniversalDepotPlugin.CustomItemId.Value;
        public static int UniversalDepotRecipeId =>
            UniversalDepotPlugin.CustomRecipeId.Value;

        public const string ItemKey = "UniversalDepot";
        public const string RecipeKey = "UniversalDepotRecipe";

        // DSP item IDs for recipe ingredients (vanilla)
        public const int TitaniumIngot = 1106;
        public const int CircuitBoard = 1303;
        public const int Microcrystalline = 1109;
        public const int ParticleBroadBand = 1404;
        public const int FrameMaterial = 1124;  // optional 2nd tier
        public const int UniverseMatrix = 1301; // optional endgame

        public const int DefaultBuildCategory = 4;   // Logistics
        public const int DefaultBuildIndex = 4000;   // first slot in Logistics
        public const int DefaultGridIndex = 0;
        public const int MaxStackSize = 50;          // per conveyor

        /// <summary>
        /// Register the building via LDBTool. Call from plugin Load() after
        /// the LDBTool plugin has loaded. LDBTool.EditDataAction is a delegate
        /// queue that fires when the LDB is ready.
        /// </summary>
        public void Register()
        {
            try
            {
                // LDBTool may not be present — log and continue.
                var ldbToolType = System.Type.GetType(
                    "LDBTool.LDBTool, LDBTool", throwOnError: false);
                if (ldbToolType == null)
                {
                    UniversalDepotPlugin.Log.LogError(
                        "[Recipe] LDBTool not found! Install LDBTool (https://github.com/hetima/DSP_LDBTool) " +
                        "and add it to BepInEx/plugins. Universal Depot will NOT appear in the build menu.");
                    return;
                }

                // LDBTool.EditDataAction is a static Action<Action> — we pass a delegate
                // that runs once the LDB is ready for editing.
                var editDataActionField = ldbToolType.GetField("EditDataAction",
                    BindingFlags.Public | BindingFlags.Static);
                var editDataAction = editDataActionField?.GetValue(null)
                    as Action<Action>;

                if (editDataAction == null)
                {
                    UniversalDepotPlugin.Log.LogError(
                        "[Recipe] LDBTool.EditDataAction field not found. LDBTool API may have changed.");
                    return;
                }

                editDataAction.Invoke(ApplyLDBChanges);
                UniversalDepotPlugin.Log.LogInfo(
                    $"[Recipe] LDB changes queued: item={UniversalDepotItemId}, " +
                    $"recipe={UniversalDepotRecipeId}");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[Recipe] Register failed: {e}");
            }
        }

        /// <summary>
        /// Mutate DSP's LDB once it's ready. Uses reflection because the
        /// LDB proto types are in a different assembly and not always
        /// referenced directly.
        /// </summary>
        private void ApplyLDBChanges()
        {
            try
            {
                var ldbType = System.Type.GetType("LDB.LDB, DSPGAME");
                if (ldbType == null)
                {
                    UniversalDepotPlugin.Log.LogError("[LDB] LDB.LDB type not found");
                    return;
                }

                var ldbInstance = ldbType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (ldbInstance == null)
                {
                    UniversalDepotPlugin.Log.LogError("[LDB] LDB.Instance is null");
                    return;
                }

                // Register item proto (Step 1 + 2)
                RegisterItemProto(ldbInstance);
                RegisterRecipeProto(ldbInstance);

                // Register UI strings (Step 3)
                RegisterStrings();

                // Register in build menu (Step 4)
                RegisterBuildIndex(ldbInstance);

                UniversalDepotPlugin.Log.LogInfo(
                    "[LDB] Universal Depot + recipe + strings + buildindex registered");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[LDB] ApplyLDBChanges failed: {e}");
            }
        }

        private void RegisterItemProto(object ldb)
        {
            // LDB.items is a Dictionary<int, ItemProto>
            var itemsField = ldb.GetType().GetField("items",
                BindingFlags.Public | BindingFlags.Instance);
            if (itemsField == null) return;

            var items = itemsField.GetValue(ldb) as System.Collections.IDictionary;
            if (items == null) return;

            // Build a minimal ItemProto via reflection
            var itemProtoType = System.Type.GetType("LDB.ItemProto, DSPGAME");
            if (itemProtoType == null) return;

            object itemProto = Activator.CreateInstance(itemProtoType);
            itemProtoType.GetField("ID")?.SetValue(itemProto, UniversalDepotItemId);
            itemProtoType.GetField("Name")?.SetValue(itemProto, ItemKey);
            itemProtoType.GetField("Description")?.SetValue(itemProto,
                "ItemDesc.UniversalDepot");
            itemProtoType.GetField("StackSize")?.SetValue(itemProto, MaxStackSize);
            itemProtoType.GetField("BuildIndex")?.SetValue(itemProto, DefaultBuildIndex);
            // EItemType.Storage = 4 (Logistics category)
            var typeField = itemProtoType.GetField("Type");
            if (typeField != null)
            {
                var eItemType = System.Type.GetType("EItemType, DSPGAME");
                if (eItemType != null)
                    typeField.SetValue(itemProto, System.Enum.Parse(eItemType, "Storage"));
            }

            items[UniversalDepotItemId] = itemProto;
            UniversalDepotPlugin.Log.LogInfo(
                $"[LDB] ItemProto registered: id={UniversalDepotItemId}, " +
                $"stack={MaxStackSize}, type=Storage");
        }

        private void RegisterRecipeProto(object ldb)
        {
            var recipesField = ldb.GetType().GetField("recipes",
                BindingFlags.Public | BindingFlags.Instance);
            if (recipesField == null) return;

            var recipes = recipesField.GetValue(ldb) as System.Collections.IDictionary;
            if (recipes == null) return;

            var recipeProtoType = System.Type.GetType("LDB.RecipeProto, DSPGAME");
            if (recipeProtoType == null) return;

            object recipe = Activator.CreateInstance(recipeProtoType);
            recipeProtoType.GetField("ID")?.SetValue(recipe, UniversalDepotRecipeId);
            recipeProtoType.GetField("Name")?.SetValue(recipe, RecipeKey);
            recipeProtoType.GetField("Type")?.SetValue(recipe, 0); // 0 = ERecipeType.Assemble
            recipeProtoType.GetField("TimeSpend")?.SetValue(recipe, 10f); // 10s
            recipeProtoType.GetField("Explicit")?.SetValue(recipe, true);
            recipeProtoType.GetField("ItemCounts")?.SetValue(recipe,
                new[] { 30, 20, 10, 2 });
            recipeProtoType.GetField("Items")?.SetValue(recipe,
                new[] { TitaniumIngot, CircuitBoard, Microcrystalline, ParticleBroadBand });
            recipeProtoType.GetField("Results")?.SetValue(recipe,
                new[] { UniversalDepotItemId });
            recipeProtoType.GetField("ResultCounts")?.SetValue(recipe, new[] { 1 });

            recipes[UniversalDepotRecipeId] = recipe;
            UniversalDepotPlugin.Log.LogInfo(
                $"[LDB] RecipeProto registered: id={UniversalDepotRecipeId}, " +
                $"30×Ti + 20×CB + 10×Micro + 2×BB → 1× UniversalDepot, 10s");
        }

        private void RegisterStrings()
        {
            // StringManager is a static class in DSP. We use reflection
            // to register localized strings. The StringManager.Translate
            // mechanism reads from internal dictionaries keyed by language.
            var stringManagerType = System.Type.GetType("DSPString.StringManager, DSPGAME");
            if (stringManagerType == null) return;

            var currentStrings = stringManagerType.GetField("currentStrings",
                BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                as System.Collections.IDictionary;
            if (currentStrings == null) return;

            currentStrings["ItemName.UniversalDepot"] = "Universal Planetary Depot";
            currentStrings["ItemDesc.UniversalDepot"] =
                "Planet-bound storage that accepts any item type with configurable " +
                "stack size and overflow handling. No remote logistics ports.";
            currentStrings["RecipeName.UniversalDepotRecipe"] = "Universal Planetary Depot";

            UniversalDepotPlugin.Log.LogInfo("[LDB] 3 strings registered (en)");
        }

        private void RegisterBuildIndex(object ldb)
        {
            // BuildIndex tells the planet-build UI where to show the icon.
            // LDB.buildIndex is a Dictionary<int, BuildProto>
            var buildIndexField = ldb.GetType().GetField("buildIndex",
                BindingFlags.Public | BindingFlags.Instance);
            if (buildIndexField == null) return;

            var buildIndex = buildIndexField.GetValue(ldb) as System.Collections.IDictionary;
            if (buildIndex == null) return;

            var buildProtoType = System.Type.GetType("LDB.BuildProto, DSPGAME");
            if (buildProtoType == null) return;

            object build = Activator.CreateInstance(buildProtoType);
            buildProtoType.GetField("ID")?.SetValue(build, DefaultBuildIndex);
            buildProtoType.GetField("Name")?.SetValue(build, ItemKey);
            buildProtoType.GetField("BuildCategory")?.SetValue(build, DefaultBuildCategory);
            buildProtoType.GetField("GridIndex")?.SetValue(build, DefaultGridIndex);
            buildProtoType.GetField("IsBuild")?.SetValue(build, true);
            buildProtoType.GetField("ItemId")?.SetValue(build, UniversalDepotItemId);

            buildIndex[DefaultBuildIndex] = build;
            UniversalDepotPlugin.Log.LogInfo(
                $"[LDB] BuildIndex registered: category={DefaultBuildCategory}, " +
                $"index={DefaultBuildIndex}");
        }
    }
}
