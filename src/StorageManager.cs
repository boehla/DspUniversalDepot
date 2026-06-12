using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DspUniversalDepot
{
    /// <summary>
    /// Manages the storage state for all Universal Depot instances.
    /// Each item is stored in its own slot. Slots are created dynamically
    /// when new item types arrive. Optional overflow deletion evicts old
    /// items so conveyors never block.
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
                    UniversalDepotPlugin.Log.LogMessage($"[Storage] New depot entity={entityId}");
            }
            return s;
        }

        public void Remove(int entityId)
        {
            if (_depots.Remove(entityId))
            {
                if (UniversalDepotPlugin.EnableDebugLogs.Value)
                    UniversalDepotPlugin.Log.LogMessage($"[Storage] Removed depot entity={entityId}");
            }
        }

        public bool Contains(int entityId) => _depots.ContainsKey(entityId);

        public void Clear()
        {
            _depots.Clear();
            UniversalDepotPlugin.Log.LogInfo("[Storage] All depots cleared");
        }
    }

    /// <summary>
    /// Per-depot storage state. Each slot holds one item type with a count.
    /// Slots are created on first arrival of a new item.
    /// </summary>
    public class DepotStorage
    {
        public int EntityId { get; }
        public int SlotCount => _slots.Count;
        public int TotalItems => _slots.Values.Sum();

        // itemId → count (slot implicitly = unique itemId)
        private readonly Dictionary<int, int> _slots = new();

        // Track oldest itemId per slot for overflow eviction
        private readonly Dictionary<int, DateTime> _slotTimestamps = new();

        public DepotStorage(int entityId)
        {
            EntityId = entityId;
        }

        /// <summary>
        /// Try to add `count` items of `itemId`. Returns amount that did NOT fit
        /// (0 = all added successfully). In overflow mode, evicts old items to
        /// accept incoming.
        /// </summary>
        public int AddItems(int itemId, int count)
        {
            if (count <= 0) return 0;
            int limit = UniversalDepotPlugin.ItemLimit.Value;
            int maxSlots = UniversalDepotPlugin.MaxSlotCount.Value;

            // New item type? need a slot
            if (!_slots.ContainsKey(itemId))
            {
                if (UniversalDepotPlugin.DynamicSlots.Value)
                {
                    if (maxSlots > 0 && _slots.Count >= maxSlots)
                    {
                        // No more slots allowed → block (or evict oldest slot)
                        if (UniversalDepotPlugin.DeleteOverflow.Value)
                        {
                            EvictOldestSlot();
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
                    // Dynamic disabled, no slot for this item → reject
                    return count;
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
                // Try to make room by evicting the OLDEST slot's items
                int freed = EvictOldestItems(itemId, rejected);
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
        /// Remove `count` items of `itemId`. Returns amount removed (clamped to
        /// current count).
        /// </summary>
        public int TakeItems(int itemId, int count)
        {
            if (count <= 0) return 0;
            if (!_slots.TryGetValue(itemId, out var current))
                return 0;
            int take = Math.Min(count, current);
            _slots[itemId] = current - take;
            return take;
        }

        public int GetCount(int itemId)
        {
            return _slots.TryGetValue(itemId, out var c) ? c : 0;
        }

        public IEnumerable<KeyValuePair<int, int>> AllSlots => _slots;

        // ── Overflow helpers ────────────────────────────────────────

        /// <summary>Evict the slot whose items were placed longest ago.</summary>
        private void EvictOldestSlot()
        {
            if (_slotTimestamps.Count == 0) return;
            int oldest = _slotTimestamps
                .OrderBy(kv => kv.Value)
                .First().Key;
            _slots.Remove(oldest);
            _slotTimestamps.Remove(oldest);
            if (UniversalDepotPlugin.EnableDebugLogs.Value)
                UniversalDepotPlugin.Log.LogWarning(
                    $"[Depot #{EntityId}] Overflow: evicted slot item={oldest}");
        }

        /// <summary>Evict items from the oldest slot to free up `count` room.</summary>
        private int EvictOldestItems(int excludeItem, int count)
        {
            if (_slotTimestamps.Count <= 1) return 0; // can't evict our own
            int oldest = _slotTimestamps
                .Where(kv => kv.Key != excludeItem)
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .FirstOrDefault();
            if (oldest == 0) return 0;
            int freed = _slots[oldest];
            _slots.Remove(oldest);
            _slotTimestamps.Remove(oldest);
            if (UniversalDepotPlugin.EnableDebugLogs.Value)
                UniversalDepotPlugin.Log.LogWarning(
                    $"[Depot #{EntityId}] Overflow: deleted {freed}x item={oldest}");
            return freed;
        }
    }

    // Lightweight OrderBy for older C# (Unity 2022 = C# 9 compatible)
    internal static class LinqShim
    {
        public static IOrderedEnumerable<T> OrderBy<T, TKey>(this IEnumerable<T> src, Func<T, TKey> key)
            => System.Linq.Enumerable.OrderBy(src, key);
        public static IOrderedEnumerable<T> OrderByDescending<T, TKey>(this IEnumerable<T> src, Func<T, TKey> key)
            => System.Linq.Enumerable.OrderByDescending(src, key);
        public static IEnumerable<T> Where<T>(this IEnumerable<T> src, Func<T, bool> pred)
            => System.Linq.Enumerable.Where(src, pred);
        public static T FirstOrDefault<T>(this IEnumerable<T> src)
            => System.Linq.Enumerable.FirstOrDefault(src);
        public static T First<T>(this IEnumerable<T> src)
            => System.Linq.Enumerable.First(src);
        public static int Sum<T>(this IEnumerable<T> src, Func<T, int> sel)
            => System.Linq.Enumerable.Sum(src, sel);
    }
}
