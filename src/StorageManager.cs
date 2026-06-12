using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DspUniversalDepot
{
    /// <summary>
    /// Manages storage state for all Universal Depot instances.
    /// Each unique item type gets its own slot, created on first arrival.
    /// Optional overflow eviction deletes oldest items so belts never block.
    /// </summary>
    public class StorageManager
    {
        // entityId → depot storage
        private readonly Dictionary<int, DepotStorage> _depots = new();

        public DepotStorage GetOrCreate(int entityId)
        {
            if (!_depots.TryGetValue(entityId, out var s))
            {
                s = new DepotStorage(entityId);
                _depots[entityId] = s;
                if (UniversalDepotPlugin.EnableDebugLogs.Value)
                    UniversalDepotPlugin.Log.LogMessage(
                        $"[Storage] New depot entity={entityId}");
            }
            return s;
        }

        public void Remove(int entityId)
        {
            if (_depots.Remove(entityId))
            {
                if (UniversalDepotPlugin.EnableDebugLogs.Value)
                    UniversalDepotPlugin.Log.LogMessage(
                        $"[Storage] Removed depot entity={entityId}");
            }
        }

        public bool Contains(int entityId) => _depots.ContainsKey(entityId);

        public IEnumerable<KeyValuePair<int, DepotStorage>> EnumerateAllDepots()
            => _depots;

        public void Clear()
        {
            int n = _depots.Count;
            _depots.Clear();
            UniversalDepotPlugin.Log.LogInfo(
                $"[Storage] All depots cleared (was {n})");
        }
    }

    /// <summary>
    /// Per-depot storage. itemId → count, plus a per-slot timestamp used
    /// for overflow-eviction ordering.
    /// </summary>
    public class DepotStorage
    {
        public int EntityId { get; }
        public int SlotCount => _slots.Count;
        public int TotalItems => _slots.Values.Sum();

        // itemId → count
        private readonly Dictionary<int, int> _slots = new();

        // itemId → UTC timestamp of last access (touch on add, keep on take)
        // Touch on take — emptied slots are NOT eviction candidates.
        private readonly Dictionary<int, DateTime> _slotTimestamps = new();

        public DepotStorage(int entityId)
        {
            EntityId = entityId;
        }

        /// <summary>
        /// Try to add `count` items of `itemId`. Returns amount that did NOT fit
        /// (0 = all added successfully). In overflow mode, evicts oldest items
        /// to accept incoming.
        /// </summary>
        public int AddItems(int itemId, int count)
        {
            if (count <= 0) return 0;
            int limit = UniversalDepotPlugin.ItemLimit.Value;
            int maxSlots = UniversalDepotPlugin.MaxSlotCount.Value;

            // New item type → need a slot
            if (!_slots.ContainsKey(itemId))
            {
                if (UniversalDepotPlugin.DynamicSlots.Value)
                {
                    if (maxSlots > 0 && _slots.Count >= maxSlots)
                    {
                        // No more slots allowed
                        if (UniversalDepotPlugin.DeleteOverflow.Value)
                        {
                            // Evict the oldest slot to make room
                            EvictOldestSlot(excludeItem: -1);
                            // If still full (e.g. maxSlots=0 edge case), reject
                            if (_slots.Count >= maxSlots)
                                return count;
                        }
                        else
                        {
                            return count; // reject
                        }
                    }
                    _slots[itemId] = 0;
                    _slotTimestamps[itemId] = DateTime.UtcNow;
                }
                else
                {
                    return count; // dynamic disabled, no slot for this item
                }
            }

            int current = _slots[itemId];
            int space = limit - current;
            int toAdd = Math.Min(count, space);
            _slots[itemId] = current + toAdd;
            _slotTimestamps[itemId] = DateTime.UtcNow; // touch = "newest"

            int rejected = count - toAdd;
            if (rejected > 0 && UniversalDepotPlugin.DeleteOverflow.Value)
            {
                // Evict the OLDEST slot's items to free up room
                int freed = EvictOldestItems(excludeItem: itemId, count: rejected);
                if (freed > 0)
                {
                    int second = Math.Min(freed, rejected);
                    _slots[itemId] += second;
                    rejected -= second;
                }
            }

            return rejected;
        }

        /// <summary>
        /// Remove `count` items of `itemId`. Returns amount removed
        /// (clamped to current count). Empty slots are NOT removed
        /// (keeps timestamp info for future inserts) but are
        /// de-prioritized by EvictOldestItems via a check on count.
        /// </summary>
        public int TakeItems(int itemId, int count)
        {
            if (count <= 0) return 0;
            if (!_slots.TryGetValue(itemId, out var current))
                return 0;
            int take = Math.Min(count, current);
            int newCount = current - take;
            _slots[itemId] = newCount;
            // Touch timestamp so this slot doesn't get evicted as "oldest"
            // just because it was drained by belts.
            if (newCount > 0)
                _slotTimestamps[itemId] = DateTime.UtcNow;
            return take;
        }

        public int GetCount(int itemId)
        {
            return _slots.TryGetValue(itemId, out var c) ? c : 0;
        }

        public IEnumerable<KeyValuePair<int, int>> AllSlots => _slots;

        // ── Overflow helpers ─────────────────────────────────────

        /// <summary>
        /// Evict the slot whose items were placed longest ago.
        /// Optionally exclude a specific itemId (e.g. when adding to it).
        /// </summary>
        private void EvictOldestSlot(int excludeItem = -1)
        {
            int oldest = FindOldestSlot(excludeItem);
            if (oldest < 0) return;
            int freed = _slots[oldest];
            _slots.Remove(oldest);
            _slotTimestamps.Remove(oldest);
            if (UniversalDepotPlugin.EnableDebugLogs.Value)
                UniversalDepotPlugin.Log.LogWarning(
                    $"[Depot #{EntityId}] Overflow: evicted slot item={oldest}, " +
                    $"freed={freed} items");
        }

        /// <summary>
        /// Evict items from the oldest slot to free up `count` room.
        /// Returns how many items were actually freed (the full slot
        /// count, since eviction removes the entire slot).
        /// </summary>
        private int EvictOldestItems(int excludeItem, int count)
        {
            int oldest = FindOldestSlot(excludeItem);
            if (oldest < 0) return 0;
            int freed = _slots[oldest];
            _slots.Remove(oldest);
            _slotTimestamps.Remove(oldest);
            if (UniversalDepotPlugin.EnableDebugLogs.Value)
                UniversalDepotPlugin.Log.LogWarning(
                    $"[Depot #{EntityId}] Overflow: deleted {freed}x item={oldest} " +
                    $"(needed {count} room)");
            return freed;
        }

        /// <summary>
        /// Find the itemId with the oldest timestamp. Returns -1 if no
        /// candidate is found. Skips items with zero count.
        /// </summary>
        private int FindOldestSlot(int excludeItem)
        {
            int oldestId = -1;
            DateTime oldestTime = DateTime.MaxValue;
            foreach (var kv in _slotTimestamps)
            {
                if (kv.Key == excludeItem) continue;
                // Skip empty slots — they're not useful eviction candidates
                if (!_slots.TryGetValue(kv.Key, out int c) || c <= 0) continue;
                if (kv.Value < oldestTime)
                {
                    oldestTime = kv.Value;
                    oldestId = kv.Key;
                }
            }
            return oldestId;
        }
    }
}
