using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DspUniversalDepot {
    /// <summary>
    /// Replaces the vanilla station window's six fixed storage rows with a compact,
    /// scrollable grid for our N-slot depot: one square tile per <i>occupied</i> slot
    /// showing the item icon, a fill bar and a fill percentage. Read-only — the depot is
    /// auto-managed — so hovering a tile only shows the native item tooltip; nothing here
    /// mutates station state, which also means no Nebula sync is required.
    ///
    /// Why this is needed: the vanilla <c>UIStationWindow</c> hard-codes
    /// <c>storageUIs[6]</c> and, in <c>OnStationIdChange</c>, sizes the window to
    /// <c>280 + 76 * storage.Length + 36</c> px tall. For a 60-slot depot that is a
    /// ~4900px window that still only renders the first 6 slots. We hide those rows for
    /// depots, build the grid once per window (the window object is reused across every
    /// station the player opens) and refresh it each frame from <c>station.storage</c>.
    ///
    /// This also absorbs the old <c>StationOverflowUIPatch</c>: both want a postfix on
    /// <c>OnStationIdChange</c> and both rewrite the window height, so they must be a single
    /// pass to avoid fighting over the layout.
    /// </summary>
    internal class DepotCell {
        public GameObject go;
        public UIButton button;
        public Image icon;
        public Image bar;
        public Text percent;
        public int lastItemId = -1;
    }

    internal class DepotGridView {
        public GameObject root;
        public DepotCell[] cells;
    }

    public static class DepotStationUI {
        // The window is reused for every station the player inspects, so the built grid is
        // cached per window and just shown/hidden as depots vs. normal stations are opened.
        private static readonly ConditionalWeakTable<UIStationWindow, DepotGridView> views =
            new ConditionalWeakTable<UIStationWindow, DepotGridView>();

        // storageUIs is private on UIStationWindow; we only ever toggle its rows' visibility.
        private static readonly AccessTools.FieldRef<UIStationWindow, UIStationStorage[]> storageUIsRef =
            AccessTools.FieldRefAccess<UIStationWindow, UIStationStorage[]>("storageUIs");

        private static Sprite whiteSprite;

        // Layout constants. Tiles are square; column count / visible rows come from config,
        // the actual cell size is derived from the window width at build time.
        private const float gridTop = -90f;     // grid top edge — where the vanilla rows began
        private const float gridLeft = 24f;
        private const float gridSpacing = 4f;
        private const float windowChrome = 330f; // header + bottom config panel kept around the grid

        private static Sprite White() {
            if(whiteSprite == null) {
                whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            }
            return whiteSprite;
        }

        /// <summary>A depot is our enlarged, planetary, non-collector supply station.</summary>
        private static bool IsDepot(StationComponent sc) {
            return sc != null && sc.storage != null && sc.storage.Length > 6 && !sc.isCollector && !sc.isStellar;
        }

        private static StationComponent resolveStation(UIStationWindow w) {
            int stationId = w.stationId;
            PlanetTransport transport = w.transport;
            if(stationId == 0 || transport == null) return null;
            StationComponent sc = transport.stationPool[stationId];
            if(sc == null || sc.id != stationId) return null;
            return sc;
        }

        // ---------------------------------------------------------------- layout / visibility

        /// <summary>
        /// Postfix on the per-station-type layout pass. Runs on every station switch, so it
        /// toggles both ways: builds/shows the grid + overflow toggle for depots and overrides
        /// the broken window height; restores the vanilla rows for everything else.
        /// </summary>
        [HarmonyPatch(typeof(UIStationWindow), "OnStationIdChange")]
        public static class LayoutPatch {
            [HarmonyPostfix]
            public static void Postfix(UIStationWindow __instance) {
                try {
                    StationComponent sc = resolveStation(__instance);
                    bool depot = IsDepot(sc);

                    UIStationStorage[] rows = storageUIsRef(__instance);
                    if(rows != null) {
                        foreach(UIStationStorage row in rows) {
                            if(row != null) row.gameObject.SetActive(!depot);
                        }
                    }

                    if(!depot) {
                        if(views.TryGetValue(__instance, out DepotGridView existing) && existing.root != null) {
                            existing.root.SetActive(false);
                        }
                        return;
                    }

                    DepotGridView view = ensureGrid(__instance);
                    view.root.SetActive(true);
                    applyDepotLayout(__instance, view);
                    refresh(sc, view);
                } catch(Exception ex) {
                    UniversalDepotPlugin.Log.LogWarning($"[Depot] grid layout patch failed: {ex}");
                }
            }
        }

        /// <summary>Per-frame value refresh for the visible depot grid.</summary>
        [HarmonyPatch(typeof(UIStationWindow), "_OnUpdate")]
        public static class UpdatePatch {
            [HarmonyPostfix]
            public static void Postfix(UIStationWindow __instance) {
                if(!views.TryGetValue(__instance, out DepotGridView view) || view.root == null || !view.root.activeSelf) return;
                try {
                    StationComponent sc = resolveStation(__instance);
                    if(IsDepot(sc)) refresh(sc, view);
                } catch(Exception ex) {
                    UniversalDepotPlugin.Log.LogWarning($"[Depot] grid refresh failed: {ex.Message}");
                }
            }
        }

        private static void applyDepotLayout(UIStationWindow w, DepotGridView view) {
            RectTransform root = view.root.GetComponent<RectTransform>();
            float gridHeight = root.sizeDelta.y;
            w.windowTrans.sizeDelta = new Vector2(w.windowTrans.sizeDelta.x, windowChrome + gridHeight);
            showOverflowToggle(w);
        }

        /// <summary>
        /// Reveal + relabel the vanilla orbital-collector checkbox as our "Discard overflow"
        /// toggle. Its button already flips <c>StationComponent.includeOrbitCollector</c> (the
        /// field the belt patch repurposes as the overflow flag) and its check image is
        /// re-synced every frame by the window's own <c>_OnUpdate</c>, so we only un-hide the
        /// group (hidden for planetary stations) and relabel it. Idempotent across repeated
        /// <c>OnStationIdChange</c> calls.
        /// </summary>
        private static void showOverflowToggle(UIStationWindow w) {
            RectTransform group = w.includeOrbitCollectorGroup;
            if(group == null) return;
            group.gameObject.SetActive(true);
            group.anchoredPosition = new Vector2(group.anchoredPosition.x, -116f);
            foreach(Localizer loc in group.GetComponentsInChildren<Localizer>(true)) {
                loc.stringKey = "";
                loc.enabled = false;
            }
            foreach(Text t in group.GetComponentsInChildren<Text>(true)) {
                t.text = "Discard overflow";
            }
        }

        // ---------------------------------------------------------------- value refresh

        private static void refresh(StationComponent sc, DepotGridView view) {
            StationStore[] storage = sc.storage;
            DepotCell[] cells = view.cells;
            for(int i = 0; i < cells.Length; i++) {
                DepotCell cell = cells[i];
                bool occupied = i < storage.Length && storage[i].itemId > 0;
                if(!occupied) {
                    // Inactive children are skipped by the GridLayoutGroup, so occupied tiles
                    // stay packed together in slot order — exactly the compact view we want.
                    if(cell.go.activeSelf) cell.go.SetActive(false);
                    continue;
                }
                if(!cell.go.activeSelf) cell.go.SetActive(true);

                StationStore s = storage[i];
                if(cell.lastItemId != s.itemId) {
                    ItemProto proto = LDB.items.Select(s.itemId);
                    cell.icon.sprite = proto != null ? proto.iconSprite : null;
                    cell.icon.enabled = proto != null;
                    cell.button.tips.itemId = s.itemId;
                    cell.button.tips.type = UIButton.ItemTipType.Item;
                    cell.lastItemId = s.itemId;
                }
                cell.button.tips.itemCount = s.count;
                cell.button.tips.itemInc = s.inc;

                float frac = s.max > 0 ? Mathf.Clamp01((float)s.count / s.max) : 0f;
                cell.percent.text = (int)(frac * 100f + 0.5f) + "%";
                cell.bar.fillAmount = frac;
                cell.bar.color = fillColor(frac);
            }
        }

        // Red (empty) → yellow (half) → green (full); kept translucent so the icon stays readable.
        private static Color fillColor(float frac) {
            Color low = new Color(0.85f, 0.25f, 0.20f, 0.40f);
            Color mid = new Color(0.85f, 0.70f, 0.20f, 0.40f);
            Color high = new Color(0.20f, 0.75f, 0.30f, 0.45f);
            return frac < 0.5f ? Color.Lerp(low, mid, frac * 2f) : Color.Lerp(mid, high, (frac - 0.5f) * 2f);
        }

        // ---------------------------------------------------------------- grid construction

        private static DepotGridView ensureGrid(UIStationWindow w) {
            if(views.TryGetValue(w, out DepotGridView v)) {
                if(v.root != null) return v;
                views.Remove(w);
            }
            v = buildGrid(w);
            views.Add(w, v);
            return v;
        }

        private static DepotGridView buildGrid(UIStationWindow w) {
            RectTransform parent = w.windowTrans;
            int columns = Mathf.Max(1, UniversalDepotPlugin.GridColumns.Value);
            int visibleRows = Mathf.Max(1, UniversalDepotPlugin.GridVisibleRows.Value);
            int slots = Mathf.Max(1, UniversalDepotPlugin.SlotCount.Value);

            float width = parent.rect.width > 0f ? parent.rect.width : 360f;
            float gridWidth = width - gridLeft * 2f;
            float cell = (gridWidth - gridSpacing * (columns - 1)) / columns;
            float viewportHeight = visibleRows * cell + (visibleRows - 1) * gridSpacing;

            GameObject root = newRect("DepotGrid", parent);
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(gridLeft, gridTop);
            rootRt.sizeDelta = new Vector2(gridWidth, viewportHeight);

            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = cell * 0.5f;

            GameObject viewport = newRect("Viewport", rootRt);
            RectTransform vpRt = viewport.GetComponent<RectTransform>();
            stretch(vpRt);
            Image vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.30f);
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = vpRt;

            GameObject content = newRect("Content", vpRt);
            RectTransform cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0f, 1f);
            cRt.anchoredPosition = Vector2.zero;
            GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(gridSpacing, gridSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperLeft;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = cRt;

            // Reuse the window's title font and clone a vanilla item button's tip settings
            // (corner / offset / delay / level) so the tooltip matches the rest of the game.
            Font font = w.titleText != null ? w.titleText.font : null;
            UIButton tipTemplate = null;
            UIStationStorage[] rows = storageUIsRef(w);
            if(rows != null && rows.Length > 0 && rows[0] != null) tipTemplate = rows[0].itemButton;

            DepotCell[] cells = new DepotCell[slots];
            for(int i = 0; i < slots; i++) {
                cells[i] = buildCell(cRt, cell, font, tipTemplate);
            }

            UniversalDepotPlugin.Log.LogInfo($"[Depot] built grid UI: {slots} cells, {columns} cols, {visibleRows} visible rows, cell={cell:0}px");
            return new DepotGridView { root = root, cells = cells };
        }

        private static DepotCell buildCell(RectTransform parent, float size, Font font, UIButton tipTemplate) {
            GameObject go = newRect("Cell", parent);

            Image bg = go.AddComponent<Image>();
            bg.sprite = White();
            bg.type = Image.Type.Simple;
            bg.color = new Color(0.12f, 0.13f, 0.16f, 0.85f);

            GameObject barGo = newRect("Bar", go.transform);
            RectTransform barRt = barGo.GetComponent<RectTransform>();
            stretch(barRt);
            Image bar = barGo.AddComponent<Image>();
            bar.sprite = White();
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Vertical;
            bar.fillOrigin = (int)Image.OriginVertical.Bottom;
            bar.raycastTarget = false;
            bar.color = fillColor(0f);

            GameObject iconGo = newRect("Icon", go.transform);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            float pad = size * 0.12f;
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(pad, pad);
            iconRt.offsetMax = new Vector2(-pad, -pad);
            Image icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            GameObject txtGo = newRect("Percent", go.transform);
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0f, 0f);
            txtRt.anchorMax = new Vector2(1f, 0f);
            txtRt.pivot = new Vector2(0.5f, 0f);
            txtRt.sizeDelta = new Vector2(0f, size * 0.34f);
            txtRt.anchoredPosition = Vector2.zero;
            Text percent = txtGo.AddComponent<Text>();
            percent.font = font;
            percent.fontSize = Mathf.Max(10, (int)(size * 0.26f));
            percent.alignment = TextAnchor.LowerCenter;
            percent.color = Color.white;
            percent.raycastTarget = false;
            percent.horizontalOverflow = HorizontalWrapMode.Overflow;
            percent.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = txtGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            // The UIButton drives the native item tooltip on hover (UIItemTip.Create needs no
            // prefab). It self-inits in Start(); an empty transitions array avoids NREs in its
            // own LateUpdate, which indexes transitions.Length unconditionally.
            UIButton button = go.AddComponent<UIButton>();
            button.transitions = new UIButton.Transition[0];
            if(tipTemplate != null) button.tips = tipTemplate.tips;
            button.tips.type = UIButton.ItemTipType.Item;
            if(button.tips.corner == 0) button.tips.corner = 9;

            return new DepotCell { go = go, button = button, icon = icon, bar = bar, percent = percent };
        }

        private static GameObject newRect(string name, Transform parent) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void stretch(RectTransform rt) {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
