using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System;
using System.Reflection;

namespace DspUniversalDepot
{
    /// <summary>
    /// Main entry point. BepInEx auto-instantiates this on plugin load.
    /// Inherits from BasePlugin (IL2CPP version) because DSP runs IL2CPP.
    /// </summary>
    [BepInPluginAttribute(GUID, NAME, VERSION)]
    [BepInProcessAttribute("DSPGAME.exe")]
    public class UniversalDepotPlugin : IL2CPPBasePlugin
    {
        public const string GUID = "com.boehla.dspuniversaldepot";
        public const string NAME = "DspUniversalDepot";
        public const string VERSION = "0.3.0";

        public static UniversalDepotPlugin Instance;
        public static BepInEx.Logging.ManualLogSource Log;

        // ── Config ───────────────────────────────────────────────
        public static ConfigEntry<int> ItemLimit;
        public static ConfigEntry<bool> DynamicSlots;
        public static ConfigEntry<int> MaxSlotCount;
        public static ConfigEntry<bool> DeleteOverflow;
        public static ConfigEntry<bool> EnableDebugLogs;
        public static ConfigEntry<int> CustomItemId;
        public static ConfigEntry<int> CustomRecipeId;
        public static ConfigEntry<bool> EnableSaveLoad;

        // ── Subsystems ───────────────────────────────────────────
        public static StorageManager Storage;
        public static AssetBundleManager Assets;
        public static RecipePatcher Recipe;
        public static Harmony HarmonyInstance;

        public override void Load()
        {
            Instance = this;
            Log = base.Log;

            // ── Config bindings ──────────────────────────────────
            ItemLimit = Config.Bind(
                "General",
                "ItemLimit",
                50000,
                "Maximum stack size per item slot in the Universal Depot.\n" +
                "Range: 1-999999. Default 50000 (raised from 5000 in v0.3.0).");

            DynamicSlots = Config.Bind(
                "General",
                "DynamicSlots",
                true,
                "Automatically create a new slot for each unique item type.");

            MaxSlotCount = Config.Bind(
                "General",
                "MaxSlotCount",
                1000,
                "Maximum unique item types the depot can hold.\n" +
                "0 = unlimited (may impact performance with many mods).");

            DeleteOverflow = Config.Bind(
                "General",
                "DeleteOverflow",
                false,
                "If true: when a slot is full, oldest items are DELETED to make\n" +
                "room. Conveyors keep running but you lose items.");

            EnableDebugLogs = Config.Bind(
                "Debug",
                "EnableDebugLogs",
                false,
                "Verbose logging for development. Disable in production.");

            CustomItemId = Config.Bind(
                "Advanced",
                "CustomItemId",
                100001,
                "Item ID for the Universal Depot building. Change if it conflicts\n" +
                "with another mod. Must be > 1000 and unique.");

            CustomRecipeId = Config.Bind(
                "Advanced",
                "CustomRecipeId",
                100002,
                "Recipe ID for the Universal Depot. Change if it conflicts.");

            EnableSaveLoad = Config.Bind(
                "General",
                "EnableSaveLoad",
                true,
                "Persist Universal Depot contents across save/load.\n" +
                "Requires DSPModSave (https://github.com/soarqin/DSP_Mods/tree/master/DSPModSave).");

            // ── Initialize subsystems ────────────────────────────
            try
            {
                // 1. Load AssetBundle (icon + 3D model, optional)
                Assets = new AssetBundleManager();
                Assets.Load();

                // 2. Register building + recipe via LDBTool
                Recipe = new RecipePatcher();
                Recipe.Register();

                // 3. Initialize storage
                Storage = new StorageManager();

                // 4. Apply all Harmony patches (one instance, one PatchAll)
                HarmonyInstance = new Harmony(GUID);
                HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

                Log.LogInfo($"{NAME} v{VERSION} loaded");
                Log.LogInfo($"  ItemLimit      = {ItemLimit.Value}");
                Log.LogInfo($"  DynamicSlots   = {DynamicSlots.Value}");
                Log.LogInfo($"  MaxSlotCount   = {MaxSlotCount.Value}");
                Log.LogInfo($"  DeleteOverflow = {DeleteOverflow.Value}");
                Log.LogInfo($"  ItemId         = {CustomItemId.Value}");
                Log.LogInfo($"  RecipeId       = {CustomRecipeId.Value}");
                Log.LogInfo($"  SaveLoad       = {EnableSaveLoad.Value}");

                if (!UniversalDepotPlugin.EnableSaveLoad.Value)
                {
                    Log.LogWarning(
                        "[Init] Save/Load disabled via config — depot contents will be lost on save/reload.");
                }
            }
            catch (Exception e)
            {
                Log.LogError($"[Init] Failed: {e}");
            }
        }

        public override bool Unload()
        {
            try
            {
                if (HarmonyInstance != null)
                {
                    HarmonyInstance.UnpatchSelf();
                    HarmonyInstance = null;
                }
                if (Storage != null)
                {
                    Storage.Clear();
                    Storage = null;
                }
                if (Assets != null)
                {
                    Assets.Unload();
                    Assets = null;
                }
                Log?.LogInfo($"{NAME} unloaded");
            }
            catch (Exception e)
            {
                Log?.LogError($"[Unload] Failed: {e}");
            }
            return base.Unload();
        }
    }
}
