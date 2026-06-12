using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DspUniversalDepot
{
    /// <summary>
    /// Patches DSP's conveyor/transport logic to interact with our
    /// dynamic-slot Universal Depot.
    ///
    /// Key idea: when a belt pulls from the depot, the game's
    /// StorageComponent.GetItemCount(itemId) returns 0 unless the slot
    /// is statically registered. We patch this to consult our
    /// DepotStorage which tracks counts per itemId.
    ///
    /// Nebula compatibility: we only patch the LOCAL simulation;
    /// we never modify anything that gets sent over the network.
    /// </summary>
    public class ConveyorPatcher
    {
        // 3 input lanes (MK1/MK2/MK3 belts) + 3 output lanes
        public const int INPUT_LANE_START = 0;  // port 0
        public const int INPUT_LANE_END = 2;    // port 2
        public const int OUTPUT_LANE_START = 3; // port 3
        public const int OUTPUT_LANE_END = 5;   // port 5

        public ConveyorPatcher()
        {
            var harmony = new Harmony(UniversalDepotPlugin.GUID);
            // PatchStorageQueries applies Harmony hooks to local reads
            // (GetItemCount / TakeItem / etc.) so belts see dynamic slots.
        }

        /// <summary>
        /// Returns true if the given port index is a belt input lane.
        /// </summary>
        public static bool IsInputLane(int port) => port >= INPUT_LANE_START && port <= INPUT_LANE_END;

        /// <summary>
        /// Returns true if the given port index is a belt output lane.
        /// </summary>
        public static bool IsOutputLane(int port) => port >= OUTPUT_LANE_START && port <= OUTPUT_LANE_END;
    }

    // ─────────────────────────────────────────────────────────────
    //  Harmony patches for StorageComponent (conveyor interface)
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(StorageComponent))]
    public static class PatchStorageQueries
    {
        /// <summary>
        /// Returns the count of an item stored in this entity.
        /// We override to consult our dynamic slot system.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(StorageComponent.GetItemCount), new[] { typeof(int) })]
        public static bool GetItemCount_Prefix(
            StorageComponent __instance,
            int itemId,
            ref int __result)
        {
            int entityId = __instance.entityId;
            if (!IsUniversalDepot(entityId)) return true; // vanilla path

            var storage = UniversalDepotPlugin.Storage.GetOrCreate(entityId);
            __result = storage.GetCount(itemId);
            return false; // skip original
        }

        /// <summary>
        /// Take up to `count` items of `itemId`. We serve from our storage.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(StorageComponent.TakeItem))]
        public static bool TakeItem_Prefix(
            StorageComponent __instance,
            int filterFrom,
            int filterTo,
            int desiredItemId,
            int desiredCount,
            ref int __result)
        {
            int entityId = __instance.entityId;
            if (!IsUniversalDepot(entityId)) return true;

            var storage = UniversalDepotPlugin.Storage.GetOrCreate(entityId);
            __result = storage.TakeItems(desiredItemId, desiredCount);
            return false;
        }

        /// <summary>
        /// Insert items into the depot. Honors dynamic-slot + overflow rules.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(StorageComponent.AddItem))]
        public static bool AddItem_Prefix(
            StorageComponent __instance,
            int itemId,
            int count,
            ref int __result)
        {
            int entityId = __instance.entityId;
            if (!IsUniversalDepot(entityId)) return true;

            var storage = UniversalDepotPlugin.Storage.GetOrCreate(entityId);
            int rejected = storage.AddItems(itemId, count);
            __result = count - rejected;
            return false;
        }

        // ── Helpers ──────────────────────────────────────────────

        /// <summary>
        /// Check if entityId is one of our Universal Depots. We tag them
        /// by their customItemId so we can identify them at runtime.
        /// </summary>
        private static bool IsUniversalDepot(int entityId)
        {
            // In real DSP: entityId → EntityData → itemId
            // We just look up in our registry or check if a storage exists
            // For now: any entity with active storage we created counts.
            return UniversalDepotPlugin.Storage != null &&
                   UniversalDepotPlugin.Storage.Contains(entityId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Stub StorageComponent for compile
    // ─────────────────────────────────────────────────────────────

    internal class StorageComponent
    {
        public int entityId;
        public int GetItemCount(int itemId) => 0;
        public int TakeItem(int filterFrom, int filterTo, int desiredItemId, int desiredCount) => 0;
        public int AddItem(int itemId, int count) => 0;
    }
}
