using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCBSCalculatorHelper
{
    internal sealed class PurchaseByClick : IDisposable
    {
        private static PurchaseByClick instance;
        private static readonly FieldInfo TrolleyChanged = AccessTools.Field(typeof(Shop), "s_updateTrolley");
        private readonly Func<bool> enabled;
        private readonly ManualLogSource log;
        private readonly HelperTheme theme = new HelperTheme();
        private int emailStatsDepth;
        private bool disposed;
        private string noticeKey, noticePart;
        private float noticeUntil;

        internal PurchaseByClick(Func<bool> enabled, ManualLogSource log)
        { this.enabled = enabled; this.log = log; instance = this; }
        internal bool Enabled { get { return !disposed && enabled(); } }

        internal void Install(Harmony harmony)
        {
            MethodInfo emailStats = AccessTools.Method(typeof(EmailJobDetails), "OnPCStats", Type.EmptyTypes);
            MethodInfo rowInit = AccessTools.Method(typeof(PCStatEntry), "Init",
                new Type[] { typeof(PartInstance), typeof(bool), typeof(bool) });
            if (emailStats == null || rowInit == null || TrolleyChanged == null)
                throw new MissingMethodException("Click purchase game hooks are unavailable.");
            harmony.Patch(emailStats, new HarmonyMethod(typeof(PurchaseByClick), "BeginEmailStats"), null, null,
                new HarmonyMethod(typeof(PurchaseByClick), "EndEmailStats"), null);
            harmony.Patch(rowInit, null, new HarmonyMethod(typeof(PurchaseByClick), "OnRowInitialized"), null, null, null);
        }

        private static void BeginEmailStats(out bool __state)
        {
            __state = instance != null;
            if (__state) instance.emailStatsDepth++;
        }
        private static Exception EndEmailStats(bool __state, Exception __exception)
        {
            if (__state && instance != null) instance.emailStatsDepth = Math.Max(0, instance.emailStatsDepth - 1);
            return __exception;
        }
        private static void OnRowInitialized(PCStatEntry __instance, PartInstance __0)
        {
            if (instance == null || __instance == null) return;
            try
            {
                ClickPurchaseRow row = __instance.GetComponent<ClickPurchaseRow>();
                bool fromEmail = instance.emailStatsDepth > 0;
                if (row == null && !fromEmail) return;
                if (row == null) row = __instance.gameObject.AddComponent<ClickPurchaseRow>();
                row.Bind(instance, fromEmail && __0 != null && !__0.IsProxy() ? __0.GetPart() : null);
            }
            catch (Exception ex) { instance.log.LogWarning("Could not enable click purchase for a PC stats row: " + ex.Message); }
        }

        internal void Add(PartDesc part)
        {
            if (!Enabled || !Application.isFocused || CalculatorPlugin.BlockGameInput) return;
            try
            {
                ClickPurchaseResult result = ClickPurchaseCart.Add(CareerStatus.Get(), part);
                if (result == ClickPurchaseResult.Added)
                {
                    ShowNotice("Added to cart: ", part.m_uiName);
                    log.LogInfo("Click purchase added one new part to cart: " + part.m_id);
                    // Notify existing shop windows; unopened shops read this cart when created.
                    RefreshCarts();
                }
                else if (result == ClickPurchaseResult.AlreadyOwned) ShowNotice("This item is already owned or in the cart.", null);
                else if (result == ClickPurchaseResult.NoCareer) ShowNotice("Load the workshop to add parts to the cart.", null);
                else ShowNotice("This part is not available in the shop.", null);
            }
            catch (Exception ex)
            {
                log.LogWarning("Could not add a PC stats part to cart: " + ex.Message);
                ShowNotice("Could not add the part to the cart.", null);
            }
        }
        internal void RefreshCarts()
        {
            Action changed = TrolleyChanged.GetValue(null) as Action;
            if (changed == null) return;
            foreach (Action listener in changed.GetInvocationList())
                try { listener(); }
                catch (Exception ex) { log.LogWarning("Could not refresh a shop cart: " + ex.Message); }
        }
        private void ShowNotice(string key, string part)
        { noticeKey = key; noticePart = part; noticeUntil = Time.unscaledTime + 3; }
        internal void DrawNotice()
        {
            if (disposed || noticeKey == null || Time.unscaledTime >= noticeUntil) return;
            theme.Ensure();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.2f);
            Rect rect = new Rect(20 * scale, Screen.height - 100 * scale,
                Mathf.Min(620 * scale, Screen.width - 40 * scale), 76 * scale);
            int oldDepth = GUI.depth; Color oldColor = GUI.color;
            GUI.depth = -11000; GUI.color = Color.white;
            GUI.DrawTexture(rect, theme.Background);
            GUI.Label(new Rect(rect.x + 14 * scale, rect.y + 12 * scale, rect.width - 28 * scale, rect.height - 24 * scale),
                HelperLocalization.Text(noticeKey) + (noticePart ?? ""), theme.Body);
            GUI.color = oldColor; GUI.depth = oldDepth;
        }
        public void Dispose()
        {
            disposed = true; emailStatsDepth = 0; theme.Dispose();
            if (instance == this) instance = null;
        }
    }

    internal sealed class ClickPurchaseRow : MonoBehaviour, IPointerClickHandler
    {
        private PurchaseByClick owner;
        private PartDesc part;
        private Image hitArea;
        internal void Bind(PurchaseByClick owner, PartDesc part)
        {
            this.owner = owner; this.part = part;
            // The transparent root makes the entire row clickable, including whitespace.
            // Child graphics still bubble their pointer clicks to this handler.
            if (hitArea == null && GetComponent<Graphic>() == null)
            {
                hitArea = gameObject.AddComponent<Image>();
                hitArea.color = Color.clear;
            }
            Update();
        }
        private void Update()
        { if (hitArea != null) hitArea.raycastTarget = owner != null && owner.Enabled && part != null; }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || eventData.dragging ||
                owner == null || part == null) return;
            owner.Add(part);
        }
    }
}
