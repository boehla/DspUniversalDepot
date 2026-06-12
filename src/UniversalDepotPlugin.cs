using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;

namespace DspUniversalDepot
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class UniversalDepotPlugin : BasePlugin
    {
        public const string GUID = "com.boehla.dspuniversaldepot";
        public const string NAME = "DspUniversalDepot";
        public const string VERSION = "0.1.0";

        public static UniversalDepotPlugin Instance;
        public static ManualLogSource Log;

        // Config
        public static ConfigEntry<int> ItemLimit;
        public static ConfigEntry<bool> DynamicSlots;
        public static ConfigEntry<int> MaxSlotCount;
        public static ConfigEntry<bool> DeleteOverflow;
        public static ConfigEntry<int> WarningThreshold;
        public static ConfigEntry<bool> EnableDebugLogs;

        public static StorageManager Storage;
        public static RecipePatcher Recipes;

        public override void Load()
        {
            Instance = this;
            Log = Log;

            // ── Config bindings ──────────────────────────────────────
            ItemLimit = Config.Bind(
                "General",
                "ItemLimit",
                5000,
                "Maximum stack size per item slot in the Universal Depot.\n" +
                "Higher = more items stored per slot. Range: 1-99999.");

            DynamicSlots = Config.Bind(
                "General",
                "DynamicSlots",
                true,
                "If true, the depot automatically creates a new slot whenever a new\n" +
                "item type arrives. Disable to use a fixed slot count.");

            MaxSlotCount = Config.Bind(
                "General",
                "MaxSlotCount",
                1000,
                "Maximum number of unique item types the depot can store.\n" +
                "0 = unlimited (not recommended for very large mod lists).");

            DeleteOverflow = Config.Bind(
                "General",
                "DeleteOverflow",
                false,
                "If true, when a slot is full, oldest items are deleted to make room\n" +
                "for incoming items. Conveyor belts keep running, but you LOSE items.");

            WarningThreshold = Config.Bind(
                "General",
                "WarningThreshold",
                90,
                "When a slot reaches this % of ItemLimit, log a warning.\n" +
                "Set to 0 to disable. Range: 0-100.");

            EnableDebugLogs = Config.Bind(
                "Debug",
                "EnableDebugLogs",
                false,
                "Verbose logging for development. Disable in production.");

            // ── Initialize subsystems ───────────────────────────────
            Storage = new StorageManager();
            Recipes = new RecipePatcher();

            // ── Apply Harmony patches ──────────────────────────────
            var harmony = new Harmony(GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // ── Register custom building & recipe ─────────────────
            Recipes.Register();

            Log.LogInfo($"{NAME} v{VERSION} loaded");
            Log.LogInfo($"  ItemLimit     = {ItemLimit.Value}");
            Log.LogInfo($"  DynamicSlots  = {DynamicSlots.Value}");
            Log.LogInfo($"  MaxSlotCount  = {MaxSlotCount.Value}");
            Log.LogInfo($"  DeleteOverflow= {DeleteOverflow.Value}");
        }
    }
}
