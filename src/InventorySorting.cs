using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PCBSCalculatorHelper
{
    internal sealed class InventorySorting : IDisposable
    {
        private static InventorySorting instance;
        internal readonly ConfigEntry<bool> Enabled;
        private readonly ConfigEntry<InventorySortMode> mode;
        private readonly ManualLogSource log;
        private readonly Dictionary<Inventory, Controls> controls = new Dictionary<Inventory, Controls>();
        private static readonly FieldInfo Pending = AccessTools.Field(typeof(Inventory), "m_itemsPendingCreation");
        private float nextRefresh;

        internal InventorySorting(ConfigFile config, ManualLogSource log)
        {
            this.log = log; instance = this;
            Enabled = config.Bind("Inventory", "SortingEnabled", true, "Show price and arrival-order sort controls in the inventory.");
            mode = config.Bind("Inventory", "SortMode", InventorySortMode.NewestFirst,
                "Inventory display order. Arrival order uses the game's saved append-only inventory order, not real-world timestamps.");
        }
        internal void Install(Harmony harmony)
        {
            // Sort before CoCreateItems consumes the pending list, after every native filter.
            harmony.Patch(AccessTools.Method(typeof(Inventory), "UpdateInventory"),
                new HarmonyMethod(typeof(InventorySorting), "BeforeUpdateInventory"), null, null, null, null);
            harmony.Patch(AccessTools.Method(typeof(Inventory), "ShowInventory"), null,
                new HarmonyMethod(typeof(InventorySorting), "OnInventoryShown"), null, null, null);
        }
        private static void BeforeUpdateInventory(Inventory __instance)
        {
            if (instance == null || !instance.Enabled.Value) return;
            try
            {
                CareerStatus career = CareerStatus.Get();
                List<PartInstance> shown = Pending.GetValue(__instance) as List<PartInstance>;
                if (career == null || shown == null) return;
                InventoryOrder.Sort(shown, career.GetInventory().Concat(career.GetPeripheralInventory()),
                    instance.mode.Value, delegate(PartInstance part) { return part.GetResaleValue(); });
            }
            catch (Exception ex) { instance.log.LogWarning("Could not sort the inventory: " + ex); }
        }
        private static void OnInventoryShown(Inventory __instance)
        { if (instance != null) instance.Refresh(__instance); }
        internal void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            foreach (Inventory inventory in controls.Keys.ToArray())
            {
                if (inventory == null) { controls.Remove(inventory); continue; }
                Refresh(inventory);
            }
        }
        private void Refresh(Inventory inventory)
        {
            try
            {
                Controls ui;
                if (!controls.TryGetValue(inventory, out ui))
                {
                    if (!Enabled.Value || inventory.inventory == null) return;
                    ui = new Controls();
                    ui.Price = CreateButton(inventory, "Price", -184, delegate {
                        mode.Value = mode.Value == InventorySortMode.PriceLowFirst ? InventorySortMode.PriceHighFirst : InventorySortMode.PriceLowFirst;
                        inventory.ConstructInventory(false); Refresh(inventory);
                    });
                    ui.Arrival = CreateButton(inventory, "Arrival", -10, delegate {
                        mode.Value = mode.Value == InventorySortMode.NewestFirst ? InventorySortMode.OldestFirst : InventorySortMode.NewestFirst;
                        inventory.ConstructInventory(false); Refresh(inventory);
                    });
                    controls.Add(inventory, ui);
                    ui.WasEnabled = Enabled.Value;
                }
                bool show = Enabled.Value && inventory.inventory.activeInHierarchy &&
                    inventory.m_itemList != null && inventory.m_itemList.gameObject.activeInHierarchy;
                ui.Price.gameObject.SetActive(show); ui.Arrival.gameObject.SetActive(show);
                Label(ui.Price, mode.Value == InventorySortMode.PriceHighFirst ? "Price: high first" : "Price: low first",
                    mode.Value == InventorySortMode.PriceLowFirst || mode.Value == InventorySortMode.PriceHighFirst);
                Label(ui.Arrival, mode.Value == InventorySortMode.OldestFirst ? "Arrival: oldest first" : "Arrival: newest first",
                    mode.Value == InventorySortMode.OldestFirst || mode.Value == InventorySortMode.NewestFirst);
                if (ui.WasEnabled != Enabled.Value)
                {
                    ui.WasEnabled = Enabled.Value;
                    if (inventory.inventory.activeInHierarchy) inventory.ConstructInventory(false);
                }
            }
            catch (Exception ex) { log.LogWarning("Could not create inventory sorting controls: " + ex); }
        }
        private static Button CreateButton(Inventory inventory, string name, float x, UnityEngine.Events.UnityAction click)
        {
            // The inventory root has a zero-size RectTransform. Its scroll view's
            // parent is the actual 830.5 x 595.1 game panel, including the title bar.
            Button button = UnityEngine.Object.Instantiate(inventory.categoryButtonPrefab, inventory.m_itemList.transform.parent, false);
            button.gameObject.name = "PCBS Helper Sort " + name;
            foreach (I2.Loc.Localize localize in button.GetComponentsInChildren<I2.Loc.Localize>(true)) localize.enabled = false;
            // Remove native category decoration while retaining the game's button and font.
            foreach (Image icon in button.GetComponentsInChildren<Image>(true))
                if (icon.gameObject != button.gameObject) icon.gameObject.SetActive(false);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = new Vector2(x, -28); rect.sizeDelta = new Vector2(166, 30);
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.gameObject.SetActive(true); text.alignment = TextAnchor.MiddleCenter;
                text.resizeTextForBestFit = true; text.resizeTextMinSize = 10; text.resizeTextMaxSize = 14;
                RectTransform label = text.rectTransform;
                label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one;
                label.offsetMin = new Vector2(8, 4); label.offsetMax = new Vector2(-8, -4);
            }
            button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(click);
            button.interactable = true;
            return button;
        }
        private static void Label(Button button, string english, bool active)
        {
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null) { text.text = HelperLocalization.Text(english); text.color = active ? Color.white : new Color(0.06f, 0.09f, 0.14f); }
            Image background = button.targetGraphic as Image;
            if (background != null) background.color = active ? new Color(0.12f, 0.42f, 0.65f) : new Color(0.78f, 0.81f, 0.85f);
        }
        public void Dispose()
        {
            foreach (Controls ui in controls.Values)
            {
                if (ui.Price != null) UnityEngine.Object.Destroy(ui.Price.gameObject);
                if (ui.Arrival != null) UnityEngine.Object.Destroy(ui.Arrival.gameObject);
            }
            controls.Clear(); if (instance == this) instance = null;
        }
        private sealed class Controls { internal Button Price, Arrival; internal bool WasEnabled; }
    }
}
