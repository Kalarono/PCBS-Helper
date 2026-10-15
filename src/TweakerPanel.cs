using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace PCBSCalculatorHelper
{
    internal sealed class TweakerPanel
    {
        private readonly ConfigEntry<bool> allApps, instant3DMark, diagnose, hidden;
        private readonly ConfigEntry<bool> clickPurchase;
        private readonly ManualLogSource log;
        private readonly TweakerTheme theme = new TweakerTheme();
        private readonly Action objectivesChanged;
        private readonly KeyCode key, calculatorKey, requirementsKey;
        private readonly PanelPlacement placement;
        private readonly ConfigEntry<bool> calculatorEnabled, requirementsEnabled;
        private readonly Action<bool> calculatorChanged, requirementsChanged;
        private readonly WorkbenchAutomation workbench;
        private readonly InventorySorting inventorySorting;
        private readonly ConfigEntry<bool> shopStockCounters;
        private float logicalHeight = 692;
        private readonly float[] rowHeights = new float[12];
        private static readonly string[] Labels = {
            "Install All Apps", "Instant 3DMark", "Click Purchase", "Show Diagnose Objectives", "Show Hidden Objectives",
            "Shop inventory counters", "Automatic thermal paste", "Remove dependencies while holding LMB",
            "Replace all customizable cables", "Inventory sorting", "Build Helper", "Will it Run? Helper"
        };
        public bool Visible { get; private set; }
        public bool ClickPurchaseEnabled { get { return clickPurchase.Value; } }

        public TweakerPanel(ConfigFile config, ManualLogSource log, KeyCode key, KeyCode calculatorKey, KeyCode requirementsKey, Action objectivesChanged,
            ConfigEntry<bool> calculatorEnabled, ConfigEntry<bool> requirementsEnabled,
            Action<bool> calculatorChanged, Action<bool> requirementsChanged,
            WorkbenchAutomation workbench, InventorySorting inventorySorting, ConfigEntry<bool> shopStockCounters)
        {
            this.workbench = workbench; this.inventorySorting = inventorySorting;
            this.shopStockCounters = shopStockCounters;
            this.log = log; this.key = key; this.objectivesChanged = objectivesChanged;
            this.calculatorKey = calculatorKey; this.requirementsKey = requirementsKey;
            placement = new PanelPlacement(config, "Tweaker");
            this.calculatorEnabled = calculatorEnabled; this.requirementsEnabled = requirementsEnabled;
            this.calculatorChanged = calculatorChanged; this.requirementsChanged = requirementsChanged;
            // Import only the four retained settings; never save changes to the old mod's file.
            string legacyPath = Path.Combine(Paths.ConfigPath, "castix.pcbs.cheatmod.cfg");
            ConfigFile legacy = null;
            if (File.Exists(legacyPath))
            {
                try { legacy = new ConfigFile(legacyPath, false); legacy.SaveOnConfigSet = false; }
                catch (Exception ex) { log.LogWarning("Could not import old Cheat Menu settings: " + ex.Message); }
            }
            allApps = config.Bind("Tweaker", "InstallAllApps", Legacy(legacy, "InstallAllApps"), "Install All Apps.");
            instant3DMark = config.Bind("Tweaker", "Instant3DMark", Legacy(legacy, "Instant3DMark"), "Instant 3DMark.");
            clickPurchase = config.Bind("Tweaker", "ClickPurchase", false,
                "Left-click a part in PC stats opened from an email to add one new part to its shop cart.");
            diagnose = config.Bind("Tweaker", "ShowDiagnoseObjectives", Legacy(legacy, "ShowDiagnose"), "Show Diagnose Objectives.");
            hidden = config.Bind("Tweaker", "ShowHiddenObjectives", Legacy(legacy, "ShowHidden"), "Show Hidden Objectives.");
        }

        private static bool Legacy(ConfigFile legacy, string name)
        { return legacy != null && legacy.Bind("General", name, false, "").Value; }

        public void Apply()
        {
            DebugVars.s_installAllApps = allApps.Value;
            DebugVars.s_instant3DMark = instant3DMark.Value;
            DebugVars.s_showDiagnoseObjectives = diagnose.Value;
            DebugVars.s_showHiddenObjectives = hidden.Value;
            log.LogInfo("Tweaker flags: InstallAllApps=" + DebugVars.s_installAllApps +
                " Instant3DMark=" + DebugVars.s_instant3DMark + " Diagnose=" + DebugVars.s_showDiagnoseObjectives +
                " Hidden=" + DebugVars.s_showHiddenObjectives);
        }

        public void Toggle() { if (Visible) Hide(); else Visible = true; }
        public void Hide() { placement.CancelDrag(); Visible = false; }

        private Rect GetRect()
        {
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.2f);
            return placement.Resolve(new Rect(20 * scale, 118 * scale,
                Mathf.Min(480 * scale, Screen.width * 0.65f), logicalHeight * scale));
        }

        public bool Contains(Vector2 point) { return Visible && GetRect().Contains(point); }

        public void Draw()
        {
            if (!Visible) return;
            theme.Ensure();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.2f);
            Rect rect = GetRect();
            float w = rect.width / scale;
            float footerTop = 86;
            for (int i = 0; i < Labels.Length; i++)
            {
                rowHeights[i] = Mathf.Max(40, Mathf.Ceil(theme.Row.CalcHeight(new GUIContent(HelperLocalization.Text(Labels[i])), w - 32)));
                footerTop += rowHeights[i] + (i + 1 < Labels.Length ? 4 : 0);
            }
            footerTop += 16; logicalHeight = footerTop + 66;
            rect = GetRect();
            Rect closeHit = new Rect(rect.x + rect.width - 52 * scale, rect.y + 20 * scale, 30 * scale, 30 * scale);
            placement.HandleDrag(ref rect, 74 * scale, Application.isFocused && !closeHit.Contains(Event.current.mousePosition));
            Matrix4x4 oldMatrix = GUI.matrix;
            int oldDepth = GUI.depth;
            Color oldColor = GUI.color;
            GUI.depth = -10000;
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.TRS(new Vector3(rect.x, rect.y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                GUI.Box(new Rect(0, 0, w, logicalHeight), GUIContent.none, theme.Window);
                GUI.Box(new Rect(22, 22, 36, 36), GUIContent.none, theme.Logo);
                DrawLogo();
                GUI.Label(new Rect(72, 20, w - 132, 25), "PCBS Helper", theme.Title);
                GUI.Label(new Rect(72, 48, w - 132, 18), HelperLocalization.Text("Convenience settings"), theme.Muted);
                if (GUI.Button(new Rect(w - 52, 20, 30, 30), "×", theme.Close)) Hide();
                float y = 86;
                bool changed = Row(allApps, 0, ref y, w);
                changed |= Row(instant3DMark, 1, ref y, w);
                Row(clickPurchase, 2, ref y, w);
                changed |= Row(diagnose, 3, ref y, w);
                changed |= Row(hidden, 4, ref y, w);
                if (changed)
                {
                    Apply();
                    if (objectivesChanged != null) objectivesChanged();
                }
                Row(shopStockCounters, 5, ref y, w);
                Row(workbench.ThermalPaste, 6, ref y, w);
                Row(workbench.RemoveDependencies, 7, ref y, w);
                Row(workbench.CableColour, 8, ref y, w);
                Row(inventorySorting.Enabled, 9, ref y, w);
                if (Row(calculatorEnabled, 10, ref y, w, calculatorKey)) calculatorChanged(calculatorEnabled.Value);
                if (Row(requirementsEnabled, 11, ref y, w, requirementsKey)) requirementsChanged(requirementsEnabled.Value);
                GUI.Box(new Rect(1, footerTop, w - 2, 65), GUIContent.none, theme.Footer);
                GUI.Label(new Rect(22, footerTop + 23, w - 150, 20), key + " / " + HelperLocalization.Text("Esc — close"), theme.Muted);
                if (GUI.Button(new Rect(w - 118, footerTop + 16, 96, 34), HelperLocalization.Text("Close"), theme.Button)) Hide();
            }
            finally { GUI.matrix = oldMatrix; GUI.depth = oldDepth; GUI.color = oldColor; }
        }

        private bool Row(ConfigEntry<bool> setting, int index, ref float y, float width, KeyCode shortcut = KeyCode.None)
        {
            float height = rowHeights[index];
            bool value = GUI.Toggle(new Rect(16, y, width - 32, height), setting.Value, HelperLocalization.Text(Labels[index]), theme.Row);
            Rect checkbox = new Rect(28, y + (height - 17) / 2, 17, 17);
            GUI.Box(checkbox, GUIContent.none, value ? theme.CheckOn : theme.CheckOff);
            if (value)
            {
                Color ink = new Color(0.09f, 0.11f, 0.16f);
                Line(new Vector2(checkbox.x + 4, checkbox.y + 8), new Vector2(checkbox.x + 7, checkbox.y + 11), ink, 2);
                Line(new Vector2(checkbox.x + 7, checkbox.y + 11), new Vector2(checkbox.x + 13, checkbox.y + 5), ink, 2);
            }
            GUI.Label(new Rect(width - 74, y, 42, height), HelperLocalization.Text(value ? "ON" : "OFF"), value ? theme.StatusOn : theme.StatusOff);
            if (shortcut != KeyCode.None)
                GUI.Box(new Rect(width - 112, y + (height - 20) / 2, 26, 20), shortcut.ToString(), theme.Keycap);
            y += height + 4;
            if (value == setting.Value) return false;
            setting.Value = value;
            return true;
        }

        private void DrawLogo()
        {
            for (int i = 0; i < 3; i++)
            {
                float y = 32 + i * 7, x = i == 1 ? 44 : 36;
                Line(new Vector2(32, y), new Vector2(48, y), theme.Accent, 1);
                Line(new Vector2(x, y - 2), new Vector2(x, y + 2), theme.Accent, 2);
            }
        }

        private static void Line(Vector2 from, Vector2 to, Color color, float thickness)
        {
            Matrix4x4 matrix = GUI.matrix; Color previous = GUI.color;
            Vector2 delta = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.color = color;
            GUI.DrawTexture(new Rect(from.x, from.y - thickness / 2, delta.magnitude, thickness), Texture2D.whiteTexture);
            GUI.matrix = matrix; GUI.color = previous;
        }

        public void Dispose() { placement.CancelDrag(); theme.Dispose(); }
    }
}
