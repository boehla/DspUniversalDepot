using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DspUniversalDepot
{
    /// <summary>
    /// Patches DSP's conveyor/storage interface so belts see our dynamic
    /// slot system. Three belt lanes in (MK1/MK2/MK3), three out, zero
    /// ILS remote ports.
    ///
    /// All patches are gated by IsUniversalDepot() which checks
    /// EntityData.protoId — this is set when DSP loads the building
    /// (LDB.items[CustomItemId] → registered as our depot). This means:
    ///   • vanilla StorageComponent behavior is preserved for ALL other
    ///     buildings (miners, labs, vessels, ILS, …)
    ///   • no need to manually register depots — protoId lookup is O(1)
    /// </summary>
    public static class PatchStorageQueries
    {
        // 3 input lanes (MK1/MK2/MK3) + 3 output lanes, no ILS ports
        public const int INPUT_LANE_START = 0;
        public const int INPUT_LANE_END = 2;
        public const int OUTPUT_LANE_START = 3;
        public const int OUTPUT_LANE_END = 5;
        public const int BELT_LANE_COUNT = 3;

        /// <summary>
        /// Returns true if the given entity is one of our Universal Depots.
        /// Uses EntityData.protoId lookup via reflection.
        /// </summary>
        public static bool IsUniversalDepot(StorageComponent storage)
        {
            if (storage == null) return false;
            try
            {
                int entityId = storage.entityId;
                if (entityId < 0) return false;

                // Resolve the planet factory + entity data.
                // We walk the GameMain.gameData → galaxy → star → planet → factory.
                var gameMain = GameMain.gameData;
                if (gameMain == null) return false;

                // Try to get the current planet's factory.
                // gameMain.galaxy.PlanetByLoadedIndex() or localPlanet
                PlanetFactory factory = GetLocalFactory();
                if (factory == null) return false;

                // entityId is local to the planet; factory.entityPool[entityId] gives data
                if (entityId >= factory.entityPool.Length) return false;
                var entityData = factory.entityPool[entityId];
                if (entityData.id == 0) return false;

                int customId = UniversalDepotPlugin.CustomItemId.Value;
                return entityData.protoId == customId;
            }
            catch
            {
                return false;
            }
        }

        private static PlanetFactory GetLocalFactory()
        {
            try
            {
                var gameMain = GameMain.gameData;
                if (gameMain == null) return null;
                var galaxy = gameMain.galaxy;
                if (galaxy == null) return null;

                // LocalPlanet for the player
                if (GameMain.localPlanet != null)
                    return GameMain.localPlanet.factory;

                return null;
            }
            catch
            {
                return null;
            }
        }

        // ── Harmony patches ──────────────────────────────────────

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StorageComponent), "GetItemCount",
            new Type[] { typeof(int) })]
        public static bool GetItemCount_Prefix(
            StorageComponent __instance,
            int itemId,
            ref int __result)
        {
            if (!IsUniversalDepot(__instance)) return true;

            var storage = UniversalDepotPlugin.Storage.GetOrCreate(__instance.entityId);
            __result = storage.GetCount(itemId);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StorageComponent), "TakeItem",
            new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) })]
        public static bool TakeItem_Prefix(
            StorageComponent __instance,
            int filterFrom,
            int filterTo,
            int desiredItemId,
            int desiredCount,
            ref int __result)
        {
            if (!IsUniversalDepot(__instance)) return true;

            var storage = UniversalDepotPlugin.Storage.GetOrCreate(__instance.entityId);
            __result = storage.TakeItems(desiredItemId, desiredCount);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StorageComponent), "AddItem",
            new Type[] { typeof(int), typeof(int) })]
        public static bool AddItem_Prefix(
            StorageComponent __instance,
            int itemId,
            int count,
            ref int __result)
        {
            if (!IsUniversalDepot(__instance)) return true;

            var storage = UniversalDepotPlugin.Storage.GetOrCreate(__instance.entityId);
            int rejected = storage.AddItems(itemId, count);
            __result = count - rejected;
            return false;
        }

        // ── Input/output lane helpers (used by belt connection code) ──

        public static bool IsInputLane(int port) =>
            port >= INPUT_LANE_START && port <= INPUT_LANE_END;

        public static bool IsOutputLane(int port) =>
            port >= OUTPUT_LANE_START && port <= OUTPUT_LANE_END;
    }
}
