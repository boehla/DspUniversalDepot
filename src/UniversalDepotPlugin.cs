using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using xiaoye97;

namespace DspUniversalDepot {
    /// <summary>
    /// Universal Planetary Depot — a storage building cloned from the vanilla
    /// storage box, but with a large, configurable slot count.
    ///
    /// Because it IS a real DSP storage entity it keeps full compatibility with
    /// belts, sorters, the storage window UI and the native save format
    /// (FactoryStorage.Export/Import) — no custom serialization needed.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("DSPGAME.exe")]
    [BepInDependency("me.xiaoye97.plugin.Dyson.LDBTool")]
    public class UniversalDepotPlugin : BaseUnityPlugin {
        public const string GUID = "com.boehla.dspuniversaldepot";
        public const string NAME = "DspUniversalDepot";
        public const string VERSION = "0.4.0";

        public static ManualLogSource Log;

        public static ConfigEntry<int> SlotCount;
        public static ConfigEntry<int> SourceItemId;
        public static ConfigEntry<int> DepotItemId;
        public static ConfigEntry<int> DepotRecipeId;
        public static ConfigEntry<int> BuildBarIndex;

        private void Awake() {
            Log = Logger;

            SlotCount = Config.Bind("General", "SlotCount", 500,
                "Number of storage slots in the Universal Depot. Vanilla storage has 30-60.\n" +
                "Higher values give more capacity; very large values make the storage window tall.");
            SourceItemId = Config.Bind("Advanced", "SourceStorageItemId", 2102,
                "Vanilla storage item that is cloned (model, icon, collider).\n" +
                "2101 = Storage MK.I, 2102 = Storage MK.II.");
            DepotItemId = Config.Bind("Advanced", "DepotItemId", 7777,
                "Item ID for the Universal Depot. Change only if it conflicts with another mod.");
            DepotRecipeId = Config.Bind("Advanced", "DepotRecipeId", 7777,
                "Recipe ID for the Universal Depot. Change only if it conflicts with another mod.");
            BuildBarIndex = Config.Bind("Advanced", "BuildBarIndex", 12,
                "Column (1-12) inside the storage build category where the depot icon appears.");

            // LDBTool fires PreAddDataAction once the vanilla protos are loaded
            // but before it merges custom protos — the right moment to clone.
            LDBTool.PreAddDataAction += registerDepot;

            Harmony harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(UniversalDepotPlugin).Assembly);

            Log.LogInfo($"{NAME} v{VERSION} initialised (slots={SlotCount.Value}, source={SourceItemId.Value})");
        }

        /// <summary>
        /// Clone the vanilla storage ItemProto, give it a new identity + recipe,
        /// and queue it with LDBTool. Runs inside LDBTool.PreAddDataAction.
        /// </summary>
        private void registerDepot() {
            try {
                ItemProto src = LDB.items.Select(SourceItemId.Value);
                if(src == null) src = LDB.items.Select(2102);
                if(src == null) src = LDB.items.Select(2101);
                if(src == null) {
                    Log.LogError("[Depot] No vanilla storage proto found to clone (tried " +
                        $"{SourceItemId.Value}, 2102, 2101). Depot NOT registered.");
                    return;
                }

                int category = src.BuildIndex / 100;
                int index = BuildBarIndex.Value;

                // MethedEx.Copy is marked obsolete in favour of CommonAPI, but it
                // is stable and lets us avoid an extra hard dependency.
#pragma warning disable CS0618
                ItemProto item = src.Copy();
#pragma warning restore CS0618
                item.ID = DepotItemId.Value;
                item.SID = "";
                item.Name = "Universal Planetary Depot";
                item.Description = "A planetary depot cloned from the storage box, " +
                    "with a greatly increased slot count. Works with belts, sorters and " +
                    "saves natively with your game.";
                item.Type = EItemType.Logistics;
                item.BuildIndex = category * 100 + index;
                item.preTech = null;
                item.IsEntity = true;
                item.CanBuild = true;

                RecipeProto recipe = buildRecipe(src, item);
                item.maincraft = recipe;
                item.handcraft = recipe;

                LDBTool.PreAddProto(item);
                LDBTool.PreAddProto(recipe);
                LDBTool.SetBuildBar(category, index, item.ID);

                Log.LogInfo($"[Depot] Registered item {item.ID} cloned from {src.ID} \"{src.Name}\" " +
                    $"(model={src.ModelIndex}) at build bar {category},{index} with {SlotCount.Value} slots");
            } catch(Exception ex) {
                Log.LogError($"[Depot] registerDepot failed: {ex}");
            }
        }

        /// <summary>
        /// A simple, always-available assemble/handcraft recipe producing the depot.
        /// Ingredients use vanilla items (Titanium Ingot + Circuit Board) so it is
        /// craftable mid-game without depending on tech unlocks.
        /// </summary>
        private RecipeProto buildRecipe(ItemProto src, ItemProto item) {
            RecipeProto recipe = new RecipeProto();
            recipe.ID = DepotRecipeId.Value;
            recipe.SID = "";
            recipe.Name = "Universal Planetary Depot";
            recipe.Description = "";
            recipe.Type = ERecipeType.Assemble;
            recipe.Handcraft = true;
            recipe.Explicit = true;
            recipe.TimeSpend = 120; // 2 seconds at 60 ticks/s
            recipe.GridIndex = src.GridIndex;
            recipe.Items = new int[] { 1106, 1303 }; // Titanium Ingot, Circuit Board
            recipe.ItemCounts = new int[] { 20, 10 };
            recipe.Results = new int[] { item.ID };
            recipe.ResultCounts = new int[] { 1 };
            recipe.preTech = null;
            recipe.IconPath = src.IconPath;
            return recipe;
        }
    }

    /// <summary>
    /// Forces the depot's storage to the configured slot count. When the factory
    /// creates the storage component for a freshly placed building it normally
    /// uses prefabDesc.storageCol * storageRow; we override that for our protoId.
    /// On save load the size comes from the save file, so this only affects newly
    /// placed depots — exactly what we want.
    /// </summary>
    [HarmonyPatch]
    public static class StorageSizePatch {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FactoryStorage), nameof(FactoryStorage.NewStorageComponent))]
        public static void NewStorageComponentPrefix(FactoryStorage __instance, int entityId, ref int size) {
            PlanetFactory factory = __instance.factory;
            if(factory == null) return;
            EntityData[] pool = factory.entityPool;
            if(entityId <= 0 || pool == null || entityId >= pool.Length) return;
            if(pool[entityId].protoId == UniversalDepotPlugin.DepotItemId.Value) {
                size = UniversalDepotPlugin.SlotCount.Value;
            }
        }
    }
}
