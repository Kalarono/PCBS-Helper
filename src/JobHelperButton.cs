using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PCBSCalculatorHelper
{
    // Native email buttons: cloning the visual style does not retain the Accept action.
    internal sealed class JobHelperButton : ICanvasElement
    {
        private static readonly FieldInfo Positive = AccessTools.Field(typeof(EmailPane), "m_positiveButton");
        private static readonly FieldInfo Negative = AccessTools.Field(typeof(EmailPane), "m_negativeButton");
        private static readonly FieldInfo Alternate = AccessTools.Field(typeof(EmailPane), "m_dlc2AlternateButton");
        private static readonly FieldInfo Email = AccessTools.Field(typeof(EmailPane), "m_email");
        private sealed class EmailHelpers
        {
            public Button Calculator, Requirements;
            public List<Button> VisibleButtons()
            {
                List<Button> result = new List<Button>();
                if (Calculator != null && Calculator.gameObject.activeSelf) result.Add(Calculator);
                if (Requirements != null && Requirements.gameObject.activeSelf) result.Add(Requirements);
                return result;
            }
        }
        private readonly Dictionary<EmailPane, EmailHelpers> buttons = new Dictionary<EmailPane, EmailHelpers>();
        private readonly Dictionary<EmailPane, List<ButtonLayout>> compactActions = new Dictionary<EmailPane, List<ButtonLayout>>();
        private readonly Func<bool> calculatorEnabled, requirementsEnabled;
        private readonly Action<Job> openCalculator, openRequirements;
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
            foreach (KeyValuePair<EmailPane, EmailHelpers> pair in buttons)
            {
                if (pair.Key == null || !pair.Key.isActiveAndEnabled) continue;
                try
                {
                    RestoreAction(pair.Key);
                    List<Button> shown = pair.Value.VisibleButtons();
                    if (shown.Count > 0) Position(pair.Key, shown);
                }
                catch (Exception ex) { log.LogWarning("Could not position job helper button: " + ex.Message); }
            }
        }
        public JobHelperButton(Func<bool> calculatorEnabled, Action<Job> openCalculator,
            Func<bool> requirementsEnabled, Action<Job> openRequirements, ManualLogSource log)
        {
            this.calculatorEnabled = calculatorEnabled; this.openCalculator = openCalculator;
            this.requirementsEnabled = requirementsEnabled; this.openRequirements = openRequirements; this.log = log;
        }
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
                bool showCalculator = calculatorEnabled() && job != null && JobCalculatorImport.Target(job) > 0;
                bool showRequirements = requirementsEnabled() && RequirementsPanel.HasJobRequirements(job);
                EmailHelpers helpers;
                if (!buttons.TryGetValue(pane, out helpers))
                {
                    if (!showCalculator && !showRequirements) return;
                    helpers = new EmailHelpers(); buttons[pane] = helpers;
                }
                helpers.Calculator = RefreshButton(pane, helpers.Calculator, showCalculator, "Helper",
                    calculatorEnabled, delegate(Job current) { return JobCalculatorImport.Target(current) > 0; }, openCalculator);
                helpers.Requirements = RefreshButton(pane, helpers.Requirements, showRequirements, "Will it Run?",
                    requirementsEnabled, RequirementsPanel.HasJobRequirements, openRequirements);
                QueueLayout();
            }
            catch (Exception ex) { log.LogWarning("Could not update job helper button: " + ex.Message); }
        }
        private static Button RefreshButton(EmailPane pane, Button helper, bool show, string label,
            Func<bool> enabled, Func<Job, bool> matches, Action<Job> open)
        {
            if (helper == null && show)
            {
                Button source = Positive.GetValue(pane) as Button;
                if (source == null) return null;
                helper = UnityEngine.Object.Instantiate(source, source.transform.parent, false);
                helper.gameObject.name = "PCBS Helper Job Button " + label;
                helper.onClick = new Button.ButtonClickedEvent();
                // Localize components would otherwise overwrite our label with "Accept".
                foreach (I2.Loc.Localize localize in helper.GetComponentsInChildren<I2.Loc.Localize>(true)) localize.enabled = false;
                LayoutElement layout = helper.GetComponent<LayoutElement>();
                if (layout == null) layout = helper.gameObject.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                helper.onClick.AddListener(delegate {
                    Job current = pane == null ? null : Email.GetValue(pane) as Job;
                    if (enabled() && current != null && matches(current)) open(current);
                });
            }
            if (helper == null) return null;
            helper.gameObject.SetActive(show);
            if (show)
            {
                helper.interactable = true;
                Text text = helper.GetComponentInChildren<Text>(true);
                if (text != null) text.text = HelperLocalization.Text(label);
            }
            return helper;
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
            List<ButtonLayout> layouts;
            if (compactActions.TryGetValue(pane, out layouts))
            { foreach (ButtonLayout layout in layouts) layout.Restore(); compactActions.Remove(pane); }
        }
        private static void SetRect(RectTransform target, RectTransform parent, float x, float y, float width, float height)
        {
            target.anchorMin = target.anchorMax = new Vector2(0, 0); target.pivot = new Vector2(0, 0);
            target.anchoredPosition = new Vector2(x - parent.rect.xMin, y - parent.rect.yMin);
            target.sizeDelta = new Vector2(width, height);
        }
        private static void FitLabel(Button button)
        {
            Text text = button.GetComponentInChildren<Text>(true);
            if (text == null) return;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(12, text.fontSize); text.resizeTextMaxSize = text.fontSize;
        }
        private void Position(EmailPane pane, List<Button> helpers)
        {
            Button positive = Positive.GetValue(pane) as Button;
            if (positive == null) return;
            RectTransform source = positive.transform as RectTransform;
            RectTransform parentRect = source.parent as RectTransform;
            if (parentRect == null) return;
            Rect p = Bounds(source, source.parent);
            float rowLeft = p.xMin, minimumLeft = parentRect.rect.xMin + 8;
            List<Button> actions = new List<Button>();
            foreach (Button action in new Button[] { Negative.GetValue(pane) as Button, Alternate.GetValue(pane) as Button })
                if (action != null && action.gameObject.activeSelf)
                {
                    Rect bounds = Bounds(action.transform as RectTransform, source.parent);
                    rowLeft = Mathf.Min(rowLeft, bounds.xMin);
                    minimumLeft = Mathf.Max(minimumLeft, bounds.xMax + 8);
                    actions.Add(action);
                }
            actions.Sort(delegate(Button a, Button b) {
                return Bounds(a.transform as RectTransform, source.parent).xMin.CompareTo(
                    Bounds(b.transform as RectTransform, source.parent).xMin);
            });
            int nativeCount = actions.Count + (positive.gameObject.activeSelf ? 1 : 0);
            EmailButtonLayout plan = EmailButtonLayout.Calculate(rowLeft, p.xMax, p.xMin, p.width,
                minimumLeft, nativeCount, helpers.Count);
            float x = plan.Left;
            if (plan.CompactNative)
            {
                List<ButtonLayout> saved = new List<ButtonLayout>();
                foreach (Button action in actions)
                {
                    saved.Add(new ButtonLayout(action));
                    SetRect(action.transform as RectTransform, parentRect, x, p.yMin, plan.Width, p.height);
                    FitLabel(action); x += plan.Width + EmailButtonLayout.Gap;
                }
                if (positive.gameObject.activeSelf) saved.Add(new ButtonLayout(positive));
                compactActions[pane] = saved;
            }
            foreach (Button helper in helpers)
            {
                RectTransform target = helper.transform as RectTransform;
                SetRect(target, parentRect, x, p.yMin, plan.Width, p.height);
                FitLabel(helper); target.SetAsLastSibling();
                x += plan.Width + EmailButtonLayout.Gap;
            }
            if (plan.CompactNative && positive.gameObject.activeSelf)
            { SetRect(source, parentRect, x, p.yMin, plan.Width, p.height); FitLabel(positive); }
        }
        public void Dispose()
        {
            disposed = true;
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
            foreach (List<ButtonLayout> layouts in compactActions.Values)
                foreach (ButtonLayout layout in layouts) layout.Restore();
            compactActions.Clear();
            foreach (EmailHelpers helpers in buttons.Values)
            {
                if (helpers.Calculator != null) UnityEngine.Object.Destroy(helpers.Calculator.gameObject);
                if (helpers.Requirements != null) UnityEngine.Object.Destroy(helpers.Requirements.gameObject);
            }
            buttons.Clear();
        }
    }
}
