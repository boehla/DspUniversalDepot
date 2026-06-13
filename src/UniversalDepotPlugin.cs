using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
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
        public const string VERSION = "0.7.0";

        // Nebula's API plugin GUID — kept as a literal so the no-Nebula path never touches a Nebula type.
        public const string NEBULA_API_GUID = "dsp.nebula-multiplayer-api";

        public static ManualLogSource Log;

        public static ConfigEntry<int> SlotCount;
        public static ConfigEntry<int> SupplyMaxPerSlot;
        public static ConfigEntry<int> SourceItemId;
        public static ConfigEntry<int> DepotItemId;
        public static ConfigEntry<int> DepotRecipeId;
        public static ConfigEntry<int> BuildBarIndex;
        public static ConfigEntry<int> GridColumns;
        public static ConfigEntry<int> GridVisibleRows;

        // --- custom design (v0.7.0) ---
        public static ConfigEntry<bool> CustomIconEnabled;
        public static ConfigEntry<bool> CustomModel;
        public static ConfigEntry<int> DepotModelId;
        public static ConfigEntry<string> TintColor;

        // Resolved at registration time. _depotModelId is 0 until a custom model is
        // successfully queued; the source PLS model id is kept for the safe fallback.
        private static int _depotModelId;
        private static int _plsModelId;
        private static Sprite _depotIconSprite;

        private static readonly FieldInfo _itemIconField =
            typeof(ItemProto).GetField("_iconSprite", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _recipeIconField =
            typeof(RecipeProto).GetField("_iconSprite", BindingFlags.Instance | BindingFlags.NonPublic);

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
            BuildBarIndex = Config.Bind("Advanced", "BuildBarIndex", 12,
                "Column (1-12) inside the build category where the depot icon appears.");
            GridColumns = Config.Bind("UI", "GridColumns", 10,
                "Number of item tiles per row in the depot's compact storage grid.");
            GridVisibleRows = Config.Bind("UI", "GridVisibleRows", 4,
                "Number of tile rows visible at once before the grid scrolls.");

            CustomIconEnabled = Config.Bind("Design", "CustomIcon", true,
                "Use the mod's own build-menu/inventory icon instead of the cloned\n" +
                "logistics-station icon.");
            CustomModel = Config.Bind("Design", "CustomModel", true,
                "Give the placed depot its own tinted model so it is visually distinct from a\n" +
                "normal Planetary Logistics Station. Reuses the PLS mesh/prefab (same ports,\n" +
                "drones, collisions) — only the colour changes. Turn OFF for a plain PLS clone.");
            DepotModelId = Config.Bind("Design", "DepotModelId", 0,
                "Model proto ID for the custom depot model. 0 = auto-assign the next free ID.\n" +
                "Change only if it conflicts with another mod's custom model.");
            TintColor = Config.Bind("Design", "TintColor", "#33D6B0",
                "Tint applied to the depot model (hex #RRGGBB or #RRGGBBAA). It multiplies the\n" +
                "base albedo, so the metal/panel shading is preserved. Default is a teal-green.");

            // LDBTool fires PreAddDataAction once the vanilla protos are loaded
            // but before it merges custom protos — the right moment to clone.
            LDBTool.PreAddDataAction += registerDepot;
            // PostAddDataAction runs after our protos are merged into LDB but before
            // LDBTool rebuilds the icon atlas — the right moment to inject the custom
            // icon and to build + tint the custom model.
            LDBTool.PostAddDataAction += applyDepotAssets;

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
                item.preTech = null;
                item.IsEntity = true;
                item.CanBuild = true;

                // Remember the PLS model for the fallback path; the copied item still
                // points at it via ModelIndex / the copied (shared) prefabDesc.
                _plsModelId = src.ModelIndex;

                // Queue a private, tinted clone of the PLS model so the depot looks distinct.
                // On failure the item keeps the PLS ModelIndex (plain clone) — never fatal.
                if(CustomModel.Value) {
                    ModelProto depotModel = buildDepotModel(src);
                    if(depotModel != null) {
                        LDBTool.PreAddProto(depotModel);
                        _depotModelId = depotModel.ID;   // Bind() may rewrite ID; read it back
                        item.ModelIndex = depotModel.ID;
                    }
                }

                // Clear the inherited PLS icon path so ItemProto/RecipeProto.Preload won't
                // reload it over our injected sprite (applyDepotAssets sets _iconSprite).
                if(CustomIconEnabled.Value) item.IconPath = "";

                RecipeProto recipe = buildRecipe(src, item);
                item.maincraft = recipe;
                item.handcraft = recipe;

                LDBTool.PreAddProto(item);
                LDBTool.PreAddProto(recipe);
                LDBTool.SetBuildBar(category, index, item.ID);

                Log.LogInfo($"[Depot] Registered item {item.ID} cloned from {src.ID} \"{src.Name}\" " +
                    $"(model={item.ModelIndex}, srcModel={src.ModelIndex}) at build bar " +
                    $"{category},{index} with {SlotCount.Value} supply slots");
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
            // Empty when the custom icon is active so Preload won't load the PLS sprite
            // over the one applyDepotAssets injects; otherwise inherit the PLS recipe icon.
            recipe.IconPath = CustomIconEnabled.Value ? "" : src.IconPath;
            return recipe;
        }

        /// <summary>
        /// Build a private <see cref="ModelProto"/> for the depot that reuses the PLS
        /// prefab path (identical mesh, colliders, belt/drone ports) but lives under its own
        /// model index. <see cref="applyDepotAssets"/> later preloads it and tints its own
        /// material copies, so only depot instances are recoloured — real PLS is untouched.
        /// Ruin/wreckage spawning is disabled (RuinId = 0) to avoid loading per-id ruin
        /// assets that do not exist for our model.
        /// </summary>
        private ModelProto buildDepotModel(ItemProto src) {
            ModelProto pls = LDB.models.Select(src.ModelIndex);
            if(pls == null || string.IsNullOrEmpty(pls.PrefabPath)) {
                Log.LogWarning($"[Depot] PLS model {src.ModelIndex} has no prefab path; " +
                    "custom model skipped (falling back to plain PLS clone).");
                return null;
            }

            ModelProto m = new ModelProto();
            m.Name = "UniversalDepotModel";
            m.SID = "";
            m.ID = DepotModelId.Value > 0 ? DepotModelId.Value : nextFreeModelId();
            m.OverrideName = "Universal Planetary Depot";
            m.Order = pls.Order;
            m.ObjectType = pls.ObjectType;
            m.RendererType = pls.RendererType;
            m.RotSymmetry = pls.RotSymmetry;
            m.HpMax = pls.HpMax;
            m.HpUpgrade = pls.HpUpgrade;
            m.HpRecover = pls.HpRecover;
            m.PrefabPath = pls.PrefabPath;
            m.RuinType = pls.RuinType;
            m.RuinId = 0;
            m.RuinCount = 0;
            m.RuinLifeTime = pls.RuinLifeTime;
            return m;
        }

        /// <summary>Largest existing model ID + 1, kept within the modelArray sizing (count + 64).</summary>
        private static int nextFreeModelId() {
            int maxId = 0;
            foreach(ModelProto mp in LDB.models.dataArray) {
                if(mp != null && mp.ID > maxId) maxId = mp.ID;
            }
            return maxId + 1;
        }

        /// <summary>
        /// Runs after LDBTool merged our protos but before it rebuilds the icon atlas.
        /// (1) Rebuilds the model tables so our new ModelProto is reachable, preloads its
        /// prefab and tints its private material copies, then repoints the item's prefabDesc.
        /// (2) Injects the custom icon sprite onto the item + recipe. Every step is guarded;
        /// any failure leaves the depot as a working plain PLS clone.
        /// </summary>
        private void applyDepotAssets() {
            if(CustomModel.Value && _depotModelId > 0) {
                try { applyCustomModel(); }
                catch(Exception ex) {
                    Log.LogError($"[Depot] custom model failed, reverting to PLS clone: {ex}");
                    revertToPlsModel();
                }
            }

            if(CustomIconEnabled.Value) {
                try { applyCustomIcon(); }
                catch(Exception ex) { Log.LogWarning($"[Depot] custom icon failed: {ex.Message}"); }
            }
        }

        private void applyCustomModel() {
            // LDBTool's AddProtosToSet does not call ModelProtoSet.OnAfterDeserialize, so the
            // ID-indexed modelArray + static index tables still miss our new model. Rebuild them.
            LDB.models.OnAfterDeserialize();
            ModelProto.InitMaxModelIndex();
            ModelProto.InitModelIndices();
            ModelProto.InitModelOrders();

            ModelProto depotModel = LDB.models.Select(_depotModelId);
            if(depotModel == null) throw new Exception($"depot model {_depotModelId} not in LDB after rebuild");

            depotModel.Preload();   // Resources.Load the PLS prefab → builds this model's own prefabDesc
            PrefabDesc pd = depotModel.prefabDesc;
            if(pd == null || pd.lodMaterials == null || pd.lodMaterials.Length == 0)
                throw new Exception("depot prefabDesc/lodMaterials empty after Preload");

            Color tint = parseColor(TintColor.Value);
            int tinted = tintPrefab(pd, tint);

            // Point the item at the tinted prefabDesc so the build ghost (prefabDesc.modelIndex)
            // and gameplay flags match the placed entity, which renders via item.ModelIndex.
            ItemProto item = LDB.items.Select(DepotItemId.Value);
            if(item != null) {
                item.prefabDesc = pd;
                item.ModelIndex = _depotModelId;
            }
            Log.LogInfo($"[Depot] custom model {_depotModelId} ready (prefab='{depotModel.PrefabPath}', " +
                $"tinted {tinted} materials, tint={TintColor.Value})");
        }

        private void revertToPlsModel() {
            ItemProto item = LDB.items.Select(DepotItemId.Value);
            if(item != null && _plsModelId > 0) {
                ModelProto pls = LDB.models.Select(_plsModelId);
                item.ModelIndex = _plsModelId;
                if(pls != null) item.prefabDesc = pls.prefabDesc;
            }
        }

        /// <summary>
        /// Clone every render material in the prefab's <c>materials</c> and <c>lodMaterials</c>
        /// arrays and multiply its albedo by <paramref name="tint"/>. Cloning is essential:
        /// Resources.Load caches one prefab per path, so PLS and depot share the same Material
        /// instances — mutating them in place would tint real stations too. Blueprint/ghost
        /// materials are left alone so build previews keep their normal look. Returns the count.
        /// </summary>
        private static int tintPrefab(PrefabDesc pd, Color tint) {
            int n = tintMaterials(pd.materials, tint);
            if(pd.lodMaterials != null) {
                foreach(Material[] lod in pd.lodMaterials) n += tintMaterials(lod, tint);
            }
            return n;
        }

        private static int tintMaterials(Material[] mats, Color tint) {
            if(mats == null) return 0;
            int n = 0;
            for(int i = 0; i < mats.Length; i++) {
                if(mats[i] == null) continue;
                Material m = new Material(mats[i]);   // private copy — never touch the shared one
                if(m.HasProperty("_Color")) m.SetColor("_Color", m.GetColor("_Color") * tint);
                if(m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);
                mats[i] = m;
                n++;
            }
            return n;
        }

        private void applyCustomIcon() {
            Sprite sp = loadDepotIcon();
            if(sp == null) return;
            ItemProto item = LDB.items.Select(DepotItemId.Value);
            RecipeProto recipe = LDB.recipes.Select(DepotRecipeId.Value);
            if(item != null) _itemIconField?.SetValue(item, sp);
            if(recipe != null) _recipeIconField?.SetValue(recipe, sp);
            Log.LogInfo("[Depot] custom icon applied to item + recipe");
        }

        /// <summary>
        /// Decode the embedded 80×80 PNG into a Sprite (cached). 80px matches the game's icon
        /// atlas tile (IconSet.Create blits a fixed 80×80 region), so it shows crisply both in
        /// the atlas (inventory/replicator) and as the directly-rendered build-menu/tooltip sprite.
        /// </summary>
        private static Sprite loadDepotIcon() {
            if(_depotIconSprite != null) return _depotIconSprite;
            try {
                Assembly asm = Assembly.GetExecutingAssembly();
                byte[] data = null;
                using(System.IO.Stream s = asm.GetManifestResourceStream("DspUniversalDepot.depot-icon.png")) {
                    if(s != null) {
                        data = new byte[s.Length];
                        int off = 0, read;
                        while(off < data.Length && (read = s.Read(data, off, data.Length - off)) > 0) off += read;
                    }
                }
                if(data == null) { Log.LogWarning("[Depot] embedded icon resource not found"); return null; }

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if(!tex.LoadImage(data)) { Log.LogWarning("[Depot] icon decode failed"); return null; }
                tex.name = "depot-icon";
                _depotIconSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
                _depotIconSprite.name = "depot-icon";
            } catch(Exception ex) {
                Log.LogWarning($"[Depot] icon load failed: {ex.Message}");
            }
            return _depotIconSprite;
        }

        /// <summary>Parse "#RRGGBB" / "#RRGGBBAA" (alpha defaults to opaque). Falls back to white.</summary>
        private static Color parseColor(string hex) {
            if(!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.Trim(), out Color c)) return c;
            Log.LogWarning($"[Depot] could not parse TintColor '{hex}', using white (no tint)");
            return Color.white;
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
}
