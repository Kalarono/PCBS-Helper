using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PCBSCalculatorHelper
{
    // A native email button: cloning the visual style does not retain the Accept action.
    internal sealed class JobHelperButton : ICanvasElement
    {
        private static readonly FieldInfo Positive = AccessTools.Field(typeof(EmailPane), "m_positiveButton");
        private static readonly FieldInfo Negative = AccessTools.Field(typeof(EmailPane), "m_negativeButton");
        private static readonly FieldInfo Alternate = AccessTools.Field(typeof(EmailPane), "m_dlc2AlternateButton");
        private static readonly FieldInfo Email = AccessTools.Field(typeof(EmailPane), "m_email");
        private readonly Dictionary<EmailPane, Button> buttons = new Dictionary<EmailPane, Button>();
        private readonly Dictionary<EmailPane, ButtonLayout> compactActions = new Dictionary<EmailPane, ButtonLayout>();
        private readonly Func<bool> enabled;
        private readonly Action<Job> open;
        private readonly ManualLogSource log;
        private float nextUpdate;
        private bool disposed;
        // This managed layout element spans the cached email panes; Unity permits
        // a null transform and all native Layout phases finish before PostLayout.
        public Transform transform { get { return null; } }
        public bool IsDestroyed() { return disposed; }
        public void LayoutComplete() { }
        public void GraphicUpdateComplete() { }
        private void QueueLayout()
        { if (!disposed) CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this); }
        public void Rebuild(CanvasUpdate phase)
        {
            if (disposed || phase != CanvasUpdate.PostLayout) return;
            foreach (KeyValuePair<EmailPane, Button> pair in buttons)
            {
                if (pair.Key == null || pair.Value == null || !pair.Key.isActiveAndEnabled || !pair.Value.gameObject.activeSelf) continue;
                try
                {
                    RestoreAction(pair.Key);
                    Position(pair.Key, pair.Value);
                }
                catch (Exception ex) { log.LogWarning("Could not position job helper button: " + ex.Message); }
            }
        }
        public JobHelperButton(Func<bool> enabled, Action<Job> open, ManualLogSource log)
        { this.enabled = enabled; this.open = open; this.log = log; }
        public void Update()
        {
            if (disposed) return;
            // Geometry updates every frame after native layout, while discovering
            // email panes and refreshing labels keeps the existing polling interval.
            if (buttons.Count > 0) QueueLayout();
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.25f;
            foreach (EmailPane pane in UnityEngine.Object.FindObjectsOfType<EmailPane>()) Refresh(pane);
        }
        public void Refresh(EmailPane pane)
        {
            if (pane == null) return;
            try
            {
                RestoreAction(pane);
                Job job = Email.GetValue(pane) as Job;
                Button helper;
                bool show = enabled() && job != null && JobCalculatorImport.Target(job) > 0;
                if (!buttons.TryGetValue(pane, out helper))
                {
                    if (!show) return;
                    Button source = Positive.GetValue(pane) as Button;
                    if (source == null) return;
                    helper = UnityEngine.Object.Instantiate(source, source.transform.parent, false);
                    helper.gameObject.name = "PCBS Helper Job Button";
                    helper.onClick = new Button.ButtonClickedEvent();
                    // Localize components would otherwise overwrite our label with "Accept".
                    foreach (I2.Loc.Localize localize in helper.GetComponentsInChildren<I2.Loc.Localize>(true)) localize.enabled = false;
                    LayoutElement layout = helper.GetComponent<LayoutElement>();
                    if (layout == null) layout = helper.gameObject.AddComponent<LayoutElement>();
                    layout.ignoreLayout = true;
                    helper.onClick.AddListener(delegate {
                        Job current = pane == null ? null : Email.GetValue(pane) as Job;
                        if (enabled() && current != null && JobCalculatorImport.Target(current) > 0) open(current);
                    });
                    buttons[pane] = helper;
                }
                if (helper == null) { buttons.Remove(pane); return; }
                helper.gameObject.SetActive(show);
                if (!show) return;
                helper.interactable = true;
                Text text = helper.GetComponentInChildren<Text>(true);
                text.text = HelperLocalization.Text("Helper");
                QueueLayout();
            }
            catch (Exception ex) { log.LogWarning("Could not update job helper button: " + ex.Message); }
        }
        private static Rect Bounds(RectTransform rect, Transform parent)
        {
            Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector3 a = parent.InverseTransformPoint(corners[0]), b = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }
        private sealed class ButtonLayout
        {
            public RectTransform Rect;
            public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
            public Text Text;
            public bool BestFit;
            public int MinSize, MaxSize;
            public ButtonLayout(Button button)
            {
                Rect = button.transform as RectTransform;
                AnchorMin = Rect.anchorMin; AnchorMax = Rect.anchorMax; Pivot = Rect.pivot;
                Position = Rect.anchoredPosition; Size = Rect.sizeDelta;
                Text = button.GetComponentInChildren<Text>(true);
                if (Text != null) { BestFit = Text.resizeTextForBestFit; MinSize = Text.resizeTextMinSize; MaxSize = Text.resizeTextMaxSize; }
            }
            public void Restore()
            {
                if (Rect == null) return;
                Rect.anchorMin = AnchorMin; Rect.anchorMax = AnchorMax; Rect.pivot = Pivot;
                Rect.anchoredPosition = Position; Rect.sizeDelta = Size;
                if (Text != null) { Text.resizeTextForBestFit = BestFit; Text.resizeTextMinSize = MinSize; Text.resizeTextMaxSize = MaxSize; }
            }
        }
        private void RestoreAction(EmailPane pane)
        {
            ButtonLayout layout;
            if (compactActions.TryGetValue(pane, out layout)) { layout.Restore(); compactActions.Remove(pane); }
        }
        private static void SetRect(RectTransform target, RectTransform parent, float x, float y, float width, float height)
        {
            target.anchorMin = target.anchorMax = new Vector2(0, 0); target.pivot = new Vector2(0, 0);
            target.anchoredPosition = new Vector2(x - parent.rect.xMin, y - parent.rect.yMin);
            target.sizeDelta = new Vector2(width, height);
        }
        private void Position(EmailPane pane, Button button)
        {
            Button positive = Positive.GetValue(pane) as Button, negative = Negative.GetValue(pane) as Button;
            RectTransform source = positive.transform as RectTransform, target = button.transform as RectTransform;
            Rect p = Bounds(source, source.parent);
            float left = p.xMin - p.width - 8;
            if (negative != null && negative.gameObject.activeSelf)
                left = Mathf.Max(left, Bounds(negative.transform as RectTransform, source.parent).xMax + 8);
            Button alternate = Alternate.GetValue(pane) as Button;
            if (alternate != null && alternate.gameObject.activeSelf)
                left = Mathf.Max(left, Bounds(alternate.transform as RectTransform, source.parent).xMax + 8);
            float width = Mathf.Min(p.width, p.xMin - 8 - left);
            RectTransform parentRect = source.parent as RectTransform;
            // DLC can have a third native action. Share the positive action's slot in
            // that case instead of covering the PC Stats/status row above the footer.
            if (width < 90)
            {
                compactActions[pane] = new ButtonLayout(positive);
                width = (p.width - 8) / 2; left = p.xMin;
                SetRect(source, parentRect, p.xMin + width + 8, p.yMin, width, p.height);
                Text nativeText = positive.GetComponentInChildren<Text>(true);
                nativeText.resizeTextForBestFit = true; nativeText.resizeTextMinSize = 12; nativeText.resizeTextMaxSize = nativeText.fontSize;
            }
            SetRect(target, parentRect, left, p.yMin, width, p.height);
            Text helperText = button.GetComponentInChildren<Text>(true);
            helperText.resizeTextForBestFit = true; helperText.resizeTextMinSize = 12; helperText.resizeTextMaxSize = helperText.fontSize;
            target.SetAsLastSibling();
        }
        public void Dispose()
        {
            disposed = true;
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
            foreach (ButtonLayout layout in compactActions.Values) layout.Restore(); compactActions.Clear();
            foreach (Button button in buttons.Values) if (button != null) UnityEngine.Object.Destroy(button.gameObject); buttons.Clear();
        }
    }
}
