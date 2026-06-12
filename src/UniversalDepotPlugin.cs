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
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class UniversalDepotPlugin : BasePlugin
    {
        public const string GUID = "com.boehla.dspuniversaldepot";
        public const string NAME = "DspUniversalDepot";
        public const string VERSION = "0.2.0";

        public static UniversalDepotPlugin Instance;
        public static ManualLogSource Log;

        // ── Config ───────────────────────────────────────────────
        public static ConfigEntry<int> ItemLimit;
        public static ConfigEntry<bool> DynamicSlots;
        public static ConfigEntry<int> MaxSlotCount;
        public static ConfigEntry<bool> DeleteOverflow;
        public static ConfigEntry<int> WarningThreshold;
        public static ConfigEntry<bool> EnableDebugLogs;
        public static ConfigEntry<int> CustomItemId;
        public static ConfigEntry<int> CustomRecipeId;

        // ── Subsystems ───────────────────────────────────────────
        public static StorageManager Storage;
        public static AssetBundleManager Assets;
        public static LDBPatcher Ldb;
        public static ConveyorPatcher Conveyors;

        public override void Load()
        {
            Instance = this;
            Log = base.Log;

            // ── Config bindings ──────────────────────────────────
            ItemLimit = Config.Bind(
                "General",
                "ItemLimit",
                5000,
                "Maximum stack size per item slot in the Universal Depot.\n" +
                "Range: 1-999999. Default 5000.");

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

            WarningThreshold = Config.Bind(
                "General",
                "WarningThreshold",
                90,
                "Warn when a slot reaches this % of ItemLimit. 0 = disable.");

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

            // ── Initialize subsystems ────────────────────────────
            try
            {
                // 1. Load AssetBundle (icon + 3D model)
                Assets = new AssetBundleManager();
                Assets.Load();

                // 2. Patch DSP's local database to register building + recipe
                Ldb = new LDBPatcher();
                Ldb.Register();

                // 3. Hook conveyor/transport logic for dynamic slot serving
                Conveyors = new ConveyorPatcher();

                // 4. Apply Harmony patches (if any)
                var harmony = new Harmony(GUID);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                // 5. Initialize storage
                Storage = new StorageManager();

                Log.LogInfo($"{NAME} v{VERSION} loaded");
                Log.LogInfo($"  ItemLimit     = {ItemLimit.Value}");
                Log.LogInfo($"  DynamicSlots  = {DynamicSlots.Value}");
                Log.LogInfo($"  MaxSlotCount  = {MaxSlotCount.Value}");
                Log.LogInfo($"  DeleteOverflow= {DeleteOverflow.Value}");
                Log.LogInfo($"  ItemId        = {CustomItemId.Value}");
                Log.LogInfo($"  RecipeId      = {CustomRecipeId.Value}");
            }
            catch (Exception e)
            {
                Log.LogError($"[Init] Failed: {e}");
            }
        }
    }
}
