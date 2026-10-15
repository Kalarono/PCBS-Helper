using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PCBSCalculatorHelper
{
    internal sealed class ShopEnhancements : IDisposable
    {
        private static ShopEnhancements instance;
        private readonly ShopSortSettings sortSettings;
        internal readonly ConfigEntry<bool> StockCounters;
        private readonly ShopStockIcons stockIcons = new ShopStockIcons();
        private readonly ManualLogSource log;
        private readonly Dictionary<Shop, SortBinding> shops = new Dictionary<Shop, SortBinding>();
        private readonly Dictionary<ShopItem, ShopCardPresentation> cards = new Dictionary<ShopItem, ShopCardPresentation>();
        private readonly Dictionary<ItemDisplay, InventoryStockPresentation> inventoryCards = new Dictionary<ItemDisplay, InventoryStockPresentation>();
        private Dictionary<string, ShopStockCounts> counts = new Dictionary<string, ShopStockCounts>();
        private static readonly FieldInfo ShopID = AccessTools.Field(typeof(Shop), "m_shopID");
        private static readonly FieldInfo DropdownValue = AccessTools.Field(typeof(Dropdown), "m_Value");
        private bool dirty = true, disposed;
        private float nextRefresh;
        private CareerStatus career;

        internal ShopEnhancements(ConfigFile config, ManualLogSource log)
        {
            sortSettings = new ShopSortSettings(config); this.log = log; instance = this;
            StockCounters = config.Bind("Shop", "ShowStockCounters", true,
                "Show model quantities in shop and inventory cards: green circle = new, yellow diamond = used, red cross = broken. Hover for details.");
            StockCounters.SettingChanged += OnCountersChanged;
        }

        private void OnCountersChanged(object sender, EventArgs args) { dirty = true; nextRefresh = 0; }

        internal void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Shop), "Start"), null,
                new HarmonyMethod(typeof(ShopEnhancements), "AfterShopStarted"), null, null, null);
            harmony.Patch(AccessTools.Method(typeof(ShopItem), "Init", new[] { typeof(ShopEntry) }), null,
                new HarmonyMethod(typeof(ShopEnhancements), "AfterItemChanged"), null, null, null);
            harmony.Patch(AccessTools.Method(typeof(ShopItem), "ChangeVariant"), null,
                new HarmonyMethod(typeof(ShopEnhancements), "AfterItemChanged"), null, null, null);
            harmony.Patch(AccessTools.Method(typeof(ShopItem), "UpdateInventoryCountText"), null,
                new HarmonyMethod(typeof(ShopEnhancements), "AfterNativeCount"), null, null, null);
            harmony.Patch(AccessTools.Method(typeof(ItemDisplay), "Init", new[] { typeof(PartInstance), typeof(Case), typeof(bool), typeof(PeripheralSlot) }), null,
                new HarmonyMethod(typeof(ShopEnhancements), "AfterInventoryItemChanged"), null, null, null);
            CareerStatus.s_onInventoryUpdated += Invalidate;
        }

        private static void AfterShopStarted(Shop __instance)
        {
            if (instance == null || instance.disposed) return;
            try { instance.BindSort(__instance); }
            catch (Exception ex) { instance.log.LogWarning("Shop sorting: " + ex); }
        }

        private void BindSort(Shop shop)
        {
            if (shop == null || shop.m_sortBy == null || shops.ContainsKey(shop)) return;
            string shopID = ShopID.GetValue(shop).ToString();
            ConfigEntry<int> setting = sortSettings.For(shopID);
            Dropdown dropdown = shop.m_sortBy;
            // Unity 2018 has no SetValueWithoutNotify. Restore the caption and
            // value silently so opening the shop keeps its native landing page.
            if (setting.Value >= 0 && setting.Value < dropdown.options.Count)
            { DropdownValue.SetValue(dropdown, setting.Value); dropdown.RefreshShownValue(); }
            UnityAction<int> changed = delegate(int value) { setting.Value = value; };
            dropdown.onValueChanged.AddListener(changed);
            shops.Add(shop, new SortBinding { Dropdown = dropdown, Changed = changed });
        }

        private static void AfterItemChanged(ShopItem __instance)
        {
            if (instance == null || instance.disposed || __instance == null) return;
            try
            {
                ShopCardPresentation ui;
                if (!instance.cards.TryGetValue(__instance, out ui) || ui == null)
                {
                    ui = __instance.gameObject.AddComponent<ShopCardPresentation>();
                    ui.Initialize(__instance, instance.stockIcons);
                    instance.cards[__instance] = ui;
                }
                ui.ResetScroll();
                instance.RefreshCounts();
                instance.Apply(__instance, ui);
            }
            catch (Exception ex) { instance.log.LogWarning("Shop card: " + ex); }
        }

        private static void AfterNativeCount(ShopItem __instance)
        {
            if (instance == null || instance.disposed) return;
            ShopCardPresentation ui;
            if (__instance != null && instance.cards.TryGetValue(__instance, out ui) && ui != null)
                ui.HideNativeCount();
        }

        private static void AfterInventoryItemChanged(ItemDisplay __instance, PartInstance __0)
        {
            if (instance == null || instance.disposed || __instance == null) return;
            try
            {
                InventoryStockPresentation ui;
                PartDesc part = __0 == null || __0.IsProxy() ? null : __0.GetPart();
                bool eligible = part != null && ShopStockCategories.SupportsInventoryCounters(part.m_shopCategory);
                if (!instance.inventoryCards.TryGetValue(__instance, out ui) || ui == null)
                {
                    if (!eligible) return;
                    ui = __instance.gameObject.AddComponent<InventoryStockPresentation>();
                    ui.Initialize(__instance, instance.stockIcons);
                    instance.inventoryCards[__instance] = ui;
                }
                ui.Part = __0;
                instance.RefreshCounts();
                instance.Apply(ui);
            }
            catch (Exception ex) { instance.log.LogWarning("Inventory stock card: " + ex); }
        }

        private void Invalidate() { dirty = true; }
        private void RefreshCounts()
        {
            CareerStatus current = CareerStatus.Get();
            if (career != current) { career = current; dirty = true; }
            if (!dirty) return;
            counts = current == null ? new Dictionary<string, ShopStockCounts>() :
                ShopStockCounts.Build(current.GetInventory(),
                    p => p.IsProxy() ? null : p.GetPartId(), p => p.IsNew(), p => p.IsBroken());
            dirty = false;
        }

        private void Apply(ShopItem item, ShopCardPresentation ui)
        {
            ShopStockCounts stock = new ShopStockCounts();
            PartDesc part = item.Entry == null ? null : item.Entry.m_part;
            if (part != null) counts.TryGetValue(part.m_id, out stock);
            // Filter the product category rather than the current shop page:
            // search results, variants and the detail page follow the same rule.
            bool eligible = part != null && ShopStockCategories.SupportsCounters(part.m_shopCategory);
            ui.ShowStock(stock, HelperLocalization.Russian, StockCounters.Value && eligible);
        }

        private void Apply(InventoryStockPresentation ui)
        {
            PartDesc part = ui.Part == null || ui.Part.IsProxy() ? null : ui.Part.GetPart();
            bool eligible = part != null && ShopStockCategories.SupportsInventoryCounters(part.m_shopCategory);
            ShopStockCounts stock = new ShopStockCounts();
            if (eligible) counts.TryGetValue(part.m_id, out stock);
            ui.Show(stock, HelperLocalization.Russian, StockCounters.Value && eligible);
        }

        internal void Update()
        {
            if (disposed || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            foreach (Shop shop in shops.Keys.Where(s => s == null).ToArray()) shops.Remove(shop);
            foreach (ShopItem item in cards.Keys.Where(i => i == null).ToArray()) cards.Remove(item);
            foreach (ItemDisplay item in inventoryCards.Keys.Where(i => i == null).ToArray()) inventoryCards.Remove(item);
            if (!cards.Keys.Any(i => i.gameObject.activeInHierarchy) &&
                !inventoryCards.Keys.Any(i => i.gameObject.activeInHierarchy)) return;
            try
            {
                // Condition changes do not always raise the normal inventory
                // event; a bounded refresh covers those too.
                dirty = true; RefreshCounts();
                foreach (var pair in cards) if (pair.Key.gameObject.activeInHierarchy) Apply(pair.Key, pair.Value);
                foreach (var pair in inventoryCards) if (pair.Key.gameObject.activeInHierarchy) Apply(pair.Value);
            }
            catch (Exception ex) { log.LogWarning("Shop inventory counts: " + ex); }
        }

        public void Dispose()
        {
            disposed = true;
            StockCounters.SettingChanged -= OnCountersChanged;
            CareerStatus.s_onInventoryUpdated -= Invalidate;
            foreach (SortBinding binding in shops.Values)
                if (binding.Dropdown != null) binding.Dropdown.onValueChanged.RemoveListener(binding.Changed);
            foreach (ShopCardPresentation card in cards.Values) if (card != null) card.Dispose();
            foreach (InventoryStockPresentation card in inventoryCards.Values) if (card != null) card.Dispose();
            stockIcons.Dispose();
            shops.Clear(); cards.Clear(); inventoryCards.Clear();
            if (instance == this) instance = null;
        }

        private sealed class SortBinding { internal Dropdown Dropdown; internal UnityAction<int> Changed; }
    }
}
