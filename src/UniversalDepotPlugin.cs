using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using xiaoye97;

namespace DspUniversalDepot {
    /// <summary>
    /// Universal Planetary Depot — a planetary <b>supply</b> station cloned from the
    /// vanilla Planetary Logistics Station (PLS), but with a large, configurable
    /// number of item slots.
    ///
    /// Behaviour: items fed into the building by belt are <b>auto-registered</b> into a
    /// free slot as <see cref="ELogisticStorage.Supply"/>. From there the planet's
    /// logistics drones deliver them to any station that demands that item. The depot
    /// only ever <i>provides</i> (Supply) — it never demands.
    ///
    /// Why a station and not a storage box: only <c>StationComponent</c> participates in
    /// the drone logistics network. The vanilla station logic is hard-wired to ~5 item
    /// kinds in a handful of unrolled helpers (<c>HasLocalSupply</c>, <c>AddItem</c>, …);
    /// the heavy tick/save paths already loop over <c>storage.Length</c>. We lift the cap
    /// by (a) reallocating the slot array to N in an <c>Init</c> postfix and (b) replacing
    /// those unrolled helpers with behaviour-identical loop versions. Native save/load
    /// already handles N slots, so contents persist without any custom serialization.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("DSPGAME.exe")]
    [BepInDependency("me.xiaoye97.plugin.Dyson.LDBTool")]
    [BepInDependency(NEBULA_API_GUID, BepInDependency.DependencyFlags.SoftDependency)]
    public class UniversalDepotPlugin : BaseUnityPlugin {
        public const string GUID = "com.boehla.dspuniversaldepot";
        public const string NAME = "DspUniversalDepot";
        public const string VERSION = "0.6.2";

        // Nebula's API plugin GUID — kept as a literal so the no-Nebula path never touches a Nebula type.
        public const string NEBULA_API_GUID = "dsp.nebula-multiplayer-api";

        public static ManualLogSource Log;

        public static ConfigEntry<int> SlotCount;
        public static ConfigEntry<int> SupplyMaxPerSlot;
        public static ConfigEntry<int> SourceItemId;
        public static ConfigEntry<int> DepotItemId;
        public static ConfigEntry<int> DepotRecipeId;
        public static ConfigEntry<int> BuildBarIndex;
        public static ConfigEntry<int> DepotGridIndex;
        public static ConfigEntry<int> GridColumns;
        public static ConfigEntry<int> GridVisibleRows;

        private void Awake() {
            Log = Logger;

            SlotCount = Config.Bind("General", "SlotCount", 60,
                "Number of distinct item slots (kinds) the depot can hold and supply.\n" +
                "Each slot is auto-assigned when an item first arrives by belt. The vanilla\n" +
                "station window only shows the first 6 slots; the rest are managed automatically.");
            SupplyMaxPerSlot = Config.Bind("General", "SupplyMaxPerSlot", 10000,
                "Per-slot capacity (max stored count of one item kind). Belt input stops once a\n" +
                "slot is full, so the belt backs up — that is the intended back-pressure.");
            SourceItemId = Config.Bind("Advanced", "SourceStationItemId", 2103,
                "Vanilla station item that is cloned (model, prefab, drones, belt ports).\n" +
                "2103 = Planetary Logistics Station (planetary, drones only).");
            DepotItemId = Config.Bind("Advanced", "DepotItemId", 7777,
                "Item ID for the Universal Depot. Change only if it conflicts with another mod.");
            DepotRecipeId = Config.Bind("Advanced", "DepotRecipeId", 7777,
                "Recipe ID for the Universal Depot. Change only if it conflicts with another mod.");
            BuildBarIndex = Config.Bind("Advanced", "BuildBarIndex", 7,
                "Slot (F-key position) inside the build category where the depot appears.\n" +
                "The Transportation category fills slots F2-F6 in vanilla, so 7 (F7) appends\n" +
                "the depot right after them. Avoid leaving a gap (e.g. 12): DSP only renders a\n" +
                "contiguous run of slots, so a gapped slot stays invisible.");
            DepotGridIndex = Config.Bind("Advanced", "DepotGridIndex", 0,
                "Replicator grid cell for the depot (format page*1000 + row*100 + col, e.g. 2501).\n" +
                "0 = auto: keep the cloned station's tab/page but move it to an empty row so it\n" +
                "no longer hides behind the Planetary Logistics Station's cell. Set explicitly\n" +
                "to relocate it within the replicator.");
            GridColumns = Config.Bind("UI", "GridColumns", 8,
                "Number of item tiles per row in the depot's compact storage grid.");
            GridVisibleRows = Config.Bind("UI", "GridVisibleRows", 2,
                "Number of tile rows visible at once before the grid scrolls. Lower = shorter window.");

            // LDBTool fires PreAddDataAction once the vanilla protos are loaded
            // but before it merges custom protos — the right moment to clone.
            LDBTool.PreAddDataAction += registerDepot;
            // …and PostAddDataAction once those custom protos are merged into LDB — the right
            // moment to Preload them. LDBTool never calls Preload() on the protos it injects, and
            // the game's own Preload pass already ran (it precedes InvokeOnLoadWorkEnded, where
            // LDBTool adds protos). Without this the depot ItemProto keeps recipes/makes/maincraft
            // == null, and the replicator NREs the instant the depot recipe is clicked.
            LDBTool.PostAddDataAction += finalizeDepotProtos;

            Harmony harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(UniversalDepotPlugin).Assembly);

            // Wire up Nebula multiplayer sync only when its API is actually loaded. The ContainsKey check
            // uses a string literal, so no Nebula type is referenced on the no-Nebula path.
            if(Chainloader.PluginInfos.ContainsKey(NEBULA_API_GUID)) {
                NebulaCompat.TryInit(Assembly.GetExecutingAssembly());
            }

            Log.LogInfo($"{NAME} v{VERSION} initialised (slots={SlotCount.Value}, source={SourceItemId.Value})");
        }

        /// <summary>
        /// Clone the vanilla PLS ItemProto, give it a new identity + recipe, and queue it
        /// with LDBTool. Keeping the source ModelIndex means our building reuses the PLS
        /// prefab — so it is a real station with drones and belt ports out of the box.
        /// </summary>
        private void registerDepot() {
            try {
                ItemProto src = LDB.items.Select(SourceItemId.Value);
                if(src == null) src = LDB.items.Select(2103);
                if(src == null) {
                    Log.LogError("[Depot] No vanilla logistics-station proto found to clone (tried " +
                        $"{SourceItemId.Value}, 2103). Depot NOT registered.");
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
                item.Description = "A planetary supply depot cloned from the logistics station. " +
                    "Items fed in by belt are automatically offered to the planet's logistics " +
                    "network and delivered to demanding stations by drones. Holds many item " +
                    "kinds and saves natively with your game.";
                item.Type = EItemType.Logistics;
                item.BuildIndex = category * 100 + index;
                item.GridIndex = depotGridIndex(src);
                item.preTech = null;
                item.IsEntity = true;
                item.CanBuild = true;
                // -1 = "always unlocked". The depot recipe hangs off no tech (preTech=null), so it
                // never enters GameHistoryData.recipeUnlocked on its own — and UIBuildMenu hides any
                // item whose ItemUnlocked() is false. UnlockKey=-1 makes ItemUnlocked short-circuit
                // to true (independent of whether FindRecipes manages to link maincraft), so the
                // building shows in the build menu from the start. The replicator separately needs
                // the recipe in recipeUnlocked — see DepotRecipeUnlockPatch.
                item.UnlockKey = -1;

                // src.Copy() copies the PLS's public recipe fields, and ItemProto.FindRecipes()
                // early-returns when `recipes != null` — so without this, DSP keeps the PLS recipe
                // link and the depot's tooltip shows the PLS recipe. Null them out so FindRecipes
                // re-derives the link from our recipe (Results=[depot]) during proto preload.
                item.recipes = null;
                item.handcrafts = null;
                item.makes = null;
                item.maincraft = null;
                item.handcraft = null;

                RecipeProto recipe = buildRecipe(src, item);

                LDBTool.PreAddProto(item);
                LDBTool.PreAddProto(recipe);
                LDBTool.SetBuildBar(category, index, item.ID);

                Log.LogInfo($"[Depot] Registered item {item.ID} cloned from {src.ID} \"{src.Name}\" " +
                    $"(model={src.ModelIndex}, grid={item.GridIndex}, srcGrid={src.GridIndex}) at build bar " +
                    $"{category},{index} with {SlotCount.Value} supply slots");
            } catch(Exception ex) {
                Log.LogError($"[Depot] registerDepot failed: {ex}");
            }
        }

        /// <summary>
        /// Preloads the depot item + recipe after LDBTool has merged them into LDB. The game runs
        /// its per-proto <c>Preload()</c> pass (which calls <c>ItemProto.FindRecipes()</c>) before
        /// the <c>InvokeOnLoadWorkEnded</c> hook where LDBTool injects custom protos, and LDBTool
        /// itself never preloads them — so our protos would otherwise stay half-initialised. In
        /// particular <c>ItemProto.recipes/makes/maincraft</c> remain null (we deliberately nulled
        /// them in <see cref="registerDepot"/> so FindRecipes re-derives the link to our recipe),
        /// and <c>UIReplicatorWindow.OnSelectedRecipeChange</c> dereferences <c>makes</c> directly,
        /// crashing the moment the depot recipe is selected. Calling Preload here runs FindRecipes
        /// (now that our recipe is in LDB.recipes) and loads the recipe's icon.
        /// </summary>
        private void finalizeDepotProtos() {
            try {
                RecipeProto recipe = LDB.recipes.Select(DepotRecipeId.Value);
                if(recipe != null) {
                    recipe.Preload(Array.IndexOf(LDB.recipes.dataArray, recipe));
                }
                ItemProto item = LDB.items.Select(DepotItemId.Value);
                if(item != null) {
                    // Preload → FindRecipes links our recipe (Results=[depot]) into recipes/makes/
                    // maincraft. recipes is null (from registerDepot) so FindRecipes won't early-return.
                    item.Preload(Array.IndexOf(LDB.items.dataArray, item));
                    Log.LogInfo($"[Depot] Preloaded depot proto (recipes={item.recipes?.Count ?? -1}, " +
                        $"makes={item.makes?.Count ?? -1}, maincraft={(item.maincraft != null ? item.maincraft.ID : 0)})");
                }
            } catch(Exception ex) {
                Log.LogError($"[Depot] finalizeDepotProtos failed: {ex}");
            }
        }

        /// <summary>
        /// Replicator grid cell for the depot. DSP decodes GridIndex as page*1000 + row*100 + col.
        /// Cloning kept the PLS's GridIndex, so the depot sat on the exact same replicator cell as
        /// the station and was invisible. Auto mode keeps the station's page (Buildings tab) but
        /// drops it into row 5 col 1 — empty in vanilla — so it shows as its own icon.
        /// </summary>
        private int depotGridIndex(ItemProto src) {
            if(DepotGridIndex.Value > 0) return DepotGridIndex.Value;
            int page = src.GridIndex / 1000;
            if(page < 1) page = 2; // buildings page fallback
            return page * 1000 + 5 * 100 + 1;
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
            recipe.GridIndex = item.GridIndex; // own cell, not the PLS's (else it hides in the replicator)
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
    /// Grows a freshly built depot's slot array to <see cref="UniversalDepotPlugin.SlotCount"/>.
    /// The PLS prefab is shared with real stations, so we cannot change its
    /// <c>stationMaxItemKinds</c>; instead we reallocate <c>storage</c>/<c>priorityLocks</c>
    /// after Init, only for entities whose protoId is our depot. Loaded depots get their
    /// slot count straight from the save (Import allocates from the stored length), so this
    /// postfix only matters for newly placed buildings.
    /// </summary>
    [HarmonyPatch(typeof(StationComponent), nameof(StationComponent.Init))]
    public static class StationInitPatch {
        [HarmonyPostfix]
        public static void Postfix(StationComponent __instance, int _entityId, EntityData[] _entityPool) {
            if(_entityPool == null || _entityId <= 0 || _entityId >= _entityPool.Length) return;
            if(_entityPool[_entityId].protoId != UniversalDepotPlugin.DepotItemId.Value) return;

            int target = UniversalDepotPlugin.SlotCount.Value;
            StationStore[] storage = __instance.storage;
            if(storage == null || target <= storage.Length) return;

            StationStore[] grown = new StationStore[target];
            Array.Copy(storage, grown, storage.Length);
            __instance.storage = grown;
            __instance.priorityLocks = new StationPriorityLock[target];
        }
    }

    /// <summary>
    /// Replaces the six logistics helper methods that the base game hard-codes to the
    /// first ~6 slots (unrolled <c>storage[0]</c>…<c>storage[5]</c>) with loop versions
    /// that scan all <c>storage.Length</c> slots. The loop result is identical to the
    /// vanilla unrolled code for stations with ≤6 slots, so applying these globally is
    /// safe; it only changes behaviour for our larger depots — which is exactly what makes
    /// drones see supply/demand beyond slot 5.
    /// </summary>
    [HarmonyPatch]
    public static class StationCapacityPatches {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StationComponent), nameof(StationComponent.HasLocalSupply))]
        public static bool HasLocalSupply(StationComponent __instance, int itemId, int countAtLeast, ref int __result) {
            StationStore[] s = __instance.storage;
            for(int i = 0; i < s.Length; i++) {
                if(s[i].itemId == itemId && s[i].localLogic == ELogisticStorage.Supply && s[i].count >= countAtLeast) {
                    __result = i; return false;
                }
            }
            __result = -1; return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StationComponent), nameof(StationComponent.HasLocalDemand))]
        public static bool HasLocalDemand(StationComponent __instance, int itemId, int countAtLeast, ref int __result) {
            StationStore[] s = __instance.storage;
            for(int i = 0; i < s.Length; i++) {
                if(s[i].itemId == itemId && s[i].localLogic == ELogisticStorage.Demand && s[i].max - s[i].count >= countAtLeast) {
                    __result = i; return false;
                }
            }
            __result = -1; return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StationComponent), nameof(StationComponent.HasRemoteSupply))]
        public static bool HasRemoteSupply(StationComponent __instance, int itemId, int countAtLeast, ref int __result) {
            StationStore[] s = __instance.storage;
            for(int i = 0; i < s.Length; i++) {
                if(s[i].itemId == itemId && s[i].remoteLogic == ELogisticStorage.Supply && s[i].count >= countAtLeast) {
                    __result = i; return false;
                }
            }
            __result = -1; return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StationComponent), nameof(StationComponent.HasRemoteDemand))]
        public static bool HasRemoteDemand(StationComponent __instance, int itemId, int countAtLeast, ref int __result) {
            StationStore[] s = __instance.storage;
            for(int i = 0; i < s.Length; i++) {
                if(s[i].itemId == itemId && s[i].remoteLogic == ELogisticStorage.Demand && s[i].max - s[i].count >= countAtLeast) {
                    __result = i; return false;
                }
            }
            __result = -1; return false;
        }

        /// <summary>Drone delivery into the station: add to the matching slot, scanning all slots.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StationComponent), nameof(StationComponent.AddItem))]
        public static bool AddItem(StationComponent __instance, int itemId, int count, int inc, ref int __result) {
            if(itemId <= 0) { __result = 0; return false; }
            StationStore[] s = __instance.storage;
            lock(s) {
                for(int i = 0; i < s.Length; i++) {
                    if(s[i].itemId == itemId) {
                        s[i].count += count;
                        s[i].inc += inc;
                        __result = count; return false;
                    }
                }
            }
            __result = 0; return false;
        }
    }

    /// <summary>
    /// Belt auto-register: for our depot stations, replaces the vanilla belt-input handler.
    /// Vanilla only accepts items already listed in the 6-entry <c>needs</c> array (filled
    /// from slots 0-4), so it cannot register arbitrary items into slots beyond the 5th.
    /// This version reads each input belt directly: the item at the belt's rear is routed
    /// to its existing Supply slot, or to a freshly claimed empty slot (set to Supply),
    /// bypassing the <c>needs</c> cap entirely. Non-depot stations fall through to vanilla.
    /// </summary>
    [HarmonyPatch(typeof(StationComponent), nameof(StationComponent.UpdateInputSlots))]
    public static class StationBeltInputPatch {
        [ThreadStatic] private static int[] _tmpNeeds;

        [HarmonyPrefix]
        public static bool Prefix(StationComponent __instance, CargoTraffic traffic, SignData[] signPool, bool active) {
            StationStore[] storage = __instance.storage;
            // Discriminator: only our enlarged, planetary, non-collector depots.
            if(storage == null || storage.Length <= 6) return true;
            if(__instance.isCollector || __instance.isVeinCollector || __instance.isStellar) return true;
            // In multiplayer the host is authoritative for the factory tick and Nebula syncs station
            // storage down; a client must not also consume belt items / assign slots or it would diverge.
            // (The Enabled gate keeps this Nebula-free in singleplayer / without Nebula.)
            if(NebulaCompat.Enabled && NebulaCompat.IsClientInMultiplayer) return true;

            int max = UniversalDepotPlugin.SupplyMaxPerSlot.Value;
            int[] needs = _tmpNeeds ?? (_tmpNeeds = new int[6]);
            BeltComponent[] beltPool = traffic.beltPool;
            SlotData[] slots = __instance.slots;

            lock(storage) {
                for(int i = 0; i < slots.Length; i++) {
                    if(slots[i].dir != IODir.Input) {
                        if(slots[i].dir != IODir.Output) { slots[i].beltId = 0; slots[i].counter = 0; }
                        continue;
                    }
                    if(slots[i].counter > 0) { slots[i].counter--; continue; }
                    if(slots[i].beltId == 0) continue;

                    CargoPath path = traffic.GetCargoPath(beltPool[slots[i].beltId].segPathId);
                    if(path == null) continue;
                    int itemId = path.GetItemIdAtRear();
                    if(itemId <= 0) continue;

                    int slotIdx = findOrClaimSupplySlot(storage, itemId);
                    bool canStore = slotIdx >= 0 && storage[slotIdx].count < max;
                    if(!canStore) {
                        // Cannot store: either this kind is full, or all slots are taken.
                        // Overflow OFF → leave the item on the belt (back-pressure).
                        // Overflow ON  → pick it off the belt and discard it (destroyed).
                        // The per-station overflow flag is parked in `includeOrbitCollector`,
                        // an unused-yet-natively-saved bool for a planetary non-collector station.
                        if(!__instance.includeOrbitCollector) continue;
                        needs[0] = itemId; needs[1] = needs[2] = needs[3] = needs[4] = needs[5] = 0;
                        path.TryPickItemAtRear(needs, out int _, out byte _, out byte _);
                        slots[i].counter = 1;
                        continue;
                    }

                    needs[0] = itemId; needs[1] = needs[2] = needs[3] = needs[4] = needs[5] = 0;
                    int picked = path.TryPickItemAtRear(needs, out int needIdx, out byte stack, out byte inc);
                    if(needIdx != 0 || picked <= 0) continue;

                    storage[slotIdx].itemId = itemId;
                    storage[slotIdx].localLogic = ELogisticStorage.Supply;
                    storage[slotIdx].remoteLogic = ELogisticStorage.None;      // planetary only, never interstellar
                    storage[slotIdx].max = max;
                    storage[slotIdx].count += stack;
                    storage[slotIdx].inc += inc;
                    slots[i].storageIdx = slotIdx + 1;
                    slots[i].counter = 1;

                    if(active) {
                        int entityId = beltPool[slots[i].beltId].entityId;
                        signPool[entityId].iconType = 1u;
                        signPool[entityId].iconId0 = (uint)itemId;
                    }
                }
            }
            return false;
        }

        /// <summary>Existing Supply slot for the item, else the first empty slot, else -1.</summary>
        private static int findOrClaimSupplySlot(StationStore[] storage, int itemId) {
            int firstEmpty = -1;
            for(int i = 0; i < storage.Length; i++) {
                if(storage[i].itemId == itemId) return i;
                if(firstEmpty < 0 && storage[i].itemId <= 0) firstEmpty = i;
            }
            return firstEmpty;
        }
    }

    // The "Discard overflow" checkbox is now revealed/relabelled by DepotStationUI's
    // OnStationIdChange postfix (src/DepotStationUI.cs), merged with the grid layout so the
    // two passes no longer fight over the window height.

    /// <summary>
    /// Keeps the depot recipe in <c>GameHistoryData.recipeUnlocked</c>. The recipe hangs off no
    /// tech (preTech=null) and is not in <c>freeMode.recipes</c>, so the game never unlocks it:
    /// <c>SetForNewGame</c> only adds the free-mode recipes and <c>Import</c> only restores what a
    /// save already had. Without this the recipe is absent from the unlock set, so the replicator
    /// (filters by <c>RecipeUnlocked</c>) and the build menu (filters by <c>ItemUnlocked</c>, which
    /// reads the same set) both hide the depot. We re-add it after a new game starts and after any
    /// save loads, so it is always available — including in saves created before this fix.
    /// </summary>
    [HarmonyPatch(typeof(GameHistoryData))]
    public static class DepotRecipeUnlockPatch {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(GameHistoryData.SetForNewGame))]
        public static void AfterNewGame(GameHistoryData __instance) => unlock(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(nameof(GameHistoryData.Import), new[] { typeof(BinaryReader), typeof(bool) })]
        public static void AfterImport(GameHistoryData __instance) => unlock(__instance);

        private static void unlock(GameHistoryData history) {
            if(history?.recipeUnlocked == null) return;
            history.recipeUnlocked.Add(UniversalDepotPlugin.DepotRecipeId.Value);
        }
    }

    /// <summary>
    /// Enlarges the entity brief-info popup's icon pool so it can render our many-slot depot.
    /// <c>UIEntityBriefInfo._OnUpdate</c> lays out one icon per station storage slot with an
    /// <b>unbounded</b> loop — <c>for (i = 0; i &lt; num12; i++) icons[i].position = …</c>, where
    /// <c>num12</c> is the slot count rounded up to the column count. Vanilla stations never exceed
    /// the prefab's small <c>icons</c> array, so the missing bound is harmless there; our depot has
    /// <see cref="UniversalDepotPlugin.SlotCount"/> (60) slots, so <c>icons[i]</c> runs off the end
    /// and throws IndexOutOfRangeException every frame the depot is on screen. Growing the pool once
    /// on creation — cloning the prefab's <c>icons[0]</c> exactly as the game's own _OnCreate does —
    /// keeps every index in range. (+16 covers the column rounding plus the equipment/drone icons.)
    /// </summary>
    [HarmonyPatch(typeof(UIEntityBriefInfo), "_OnCreate")]
    public static class BriefInfoIconPoolPatch {
        [HarmonyPostfix]
        public static void Postfix(UIEntityBriefInfo __instance) {
            try {
                UIIconCountInc[] icons = __instance.icons;
                if(icons == null || icons.Length == 0 || icons[0] == null) return;
                int needed = UniversalDepotPlugin.SlotCount.Value + 16;
                if(icons.Length >= needed) return;

                UIIconCountInc proto = icons[0];
                UIIconCountInc[] grown = new UIIconCountInc[needed];
                Array.Copy(icons, grown, icons.Length);
                for(int i = icons.Length; i < needed; i++) {
                    UIIconCountInc clone = UnityEngine.Object.Instantiate(proto, proto.transform.parent);
                    clone.SetTransformIdentity();
                    clone.visible = false;
                    grown[i] = clone;
                }
                __instance.icons = grown;
                UniversalDepotPlugin.Log.LogInfo($"[Depot] Brief-info icon pool grown {icons.Length} -> {needed} " +
                    $"to fit {UniversalDepotPlugin.SlotCount.Value}-slot depots");
            } catch(Exception ex) {
                UniversalDepotPlugin.Log.LogWarning($"[Depot] brief-info icon pool patch failed: {ex.Message}");
            }
        }
    }
}
