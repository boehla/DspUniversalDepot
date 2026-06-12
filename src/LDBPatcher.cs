using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;

namespace DspUniversalDepot
{
    /// <summary>
    /// Cleanup patch: when a building is removed from a planet, also
    /// remove its Universal Depot storage from StorageManager. Prevents
    /// memory leak when depots are destroyed.
    /// </summary>
    public static class PlanetFactory_RemoveEntityData_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetFactory), "RemoveEntityData",
            new Type[] { typeof(int) })]
        public static void Postfix(PlanetFactory __instance, int id)
        {
            try
            {
                if (UniversalDepotPlugin.Storage == null) return;
                if (UniversalDepotPlugin.Storage.Contains(id))
                {
                    UniversalDepotPlugin.Storage.Remove(id);
                    if (UniversalDepotPlugin.EnableDebugLogs.Value)
                        UniversalDepotPlugin.Log.LogMessage(
                            $"[Storage] Cleanup: removed depot entity={id}");
                }
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError(
                    $"[PlanetFactory] RemoveEntityData postfix failed: {e}");
            }
        }
    }

    /// <summary>
    /// Save/Load support via DSPModSave. Hooks into the save/load cycle
    /// so Universal Depot contents survive a save → quit → reload.
    ///
    /// Saved data layout (simple JSON):
    ///   {
    ///     "depots": [
    ///       { "entityId": 123, "slots": { "1101": 50, "1102": 30 } },
    ///       ...
    ///     ]
    ///   }
    /// </summary>
    public static class SaveLoad_Patch
    {
        public const string SaveKey = "UniversalDepot.Storage";

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameSave), "SaveCurrentGame")]
        public static void SavePostfix(GameSave __instance)
        {
            try
            {
                if (UniversalDepotPlugin.Storage == null) return;

                // Serialize: build a dictionary of {entityId → {itemId → count}}
                var allData = new System.Collections.Generic.Dictionary<int,
                    System.Collections.Generic.Dictionary<int, int>>();

                foreach (var kvp in UniversalDepotPlugin.Storage.EnumerateAllDepots())
                {
                    var slots = new System.Collections.Generic.Dictionary<int, int>();
                    foreach (var slot in kvp.Value.AllSlots)
                    {
                        if (slot.Value > 0)
                            slots[slot.Key] = slot.Value;
                    }
                    if (slots.Count > 0)
                        allData[kvp.Key] = slots;
                }

                if (allData.Count == 0)
                {
                    // No depots — clear any prior saved data
                    DSPModSave.SaveDataManager.SetSaveData(SaveKey, "");
                    return;
                }

                string json = MiniJSON.Serialize(allData);
                DSPModSave.SaveDataManager.SetSaveData(SaveKey, json);

                if (UniversalDepotPlugin.EnableDebugLogs.Value)
                    UniversalDepotPlugin.Log.LogMessage(
                        $"[Save] Saved {allData.Count} depots ({json.Length} bytes)");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[Save] Failed: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameSave), "LoadCurrentGame")]
        public static void LoadPostfix(GameSave __instance)
        {
            try
            {
                if (UniversalDepotPlugin.Storage == null) return;
                UniversalDepotPlugin.Storage.Clear();

                string json = DSPModSave.SaveDataManager.GetSaveData(SaveKey);
                if (string.IsNullOrEmpty(json)) return;

                var allData = MiniJSON.Deserialize(json) as System.Collections.Generic.Dictionary<string, object>;
                if (allData == null) return;

                int depotCount = 0, slotCount = 0;
                foreach (var depotKv in allData)
                {
                    if (!int.TryParse(depotKv.Key, out int entityId)) continue;
                    var storage = UniversalDepotPlugin.Storage.GetOrCreate(entityId);
                    var slotsData = depotKv.Value as System.Collections.Generic.Dictionary<string, object>;
                    if (slotsData == null) continue;
                    foreach (var slotKv in slotsData)
                    {
                        if (!int.TryParse(slotKv.Key, out int itemId)) continue;
                        if (!int.TryParse(slotKv.Value?.ToString() ?? "0", out int count)) continue;
                        if (count > 0)
                        {
                            storage.AddItems(itemId, count);
                            slotCount++;
                        }
                    }
                    depotCount++;
                }

                UniversalDepotPlugin.Log.LogInfo(
                    $"[Load] Restored {depotCount} depots, {slotCount} slots");
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[Load] Failed: {e}");
            }
        }
    }

    /// <summary>
    /// Debug command: /depot-stats [in chat] — prints depot counts.
    /// Activated by the EnableDebugLogs config option.
    /// </summary>
    public static class DebugChatCommand_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIChatWindow), "OnSubmitText")]
        public static void OnChatPostfix(UIChatWindow __instance, ref string text)
        {
            try
            {
                if (text == null) return;
                string trimmed = text.Trim().ToLower();
                if (trimmed == "/depot-stats" || trimmed == "/depot_list")
                {
                    int total = 0;
                    int itemTotal = 0;
                    int depotsWithItems = 0;
                    if (UniversalDepotPlugin.Storage != null)
                    {
                        foreach (var kvp in UniversalDepotPlugin.Storage.EnumerateAllDepots())
                        {
                            total++;
                            int items = kvp.Value.TotalItems;
                            if (items > 0)
                            {
                                itemTotal += items;
                                depotsWithItems++;
                            }
                        }
                    }
                    string msg =
                        $"[Universal Depot] {total} depots placed, " +
                        $"{depotsWithItems} contain items, " +
                        $"{itemTotal} items total";
                    UniversalDepotPlugin.Log.LogInfo(msg);
                    // Echo to chat so user can see it
                    if (UIRoot.instance != null && UIRoot.instance.uiGame != null)
                    {
                        UIRoot.instance.uiGame.PlayAudioClip(13);
                    }
                }
            }
            catch (Exception e)
            {
                UniversalDepotPlugin.Log.LogError($"[DebugChat] Failed: {e}");
            }
        }
    }
}
