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
        private readonly ManualLogSource log;
        private readonly HelperTheme theme = new HelperTheme();
        private readonly Action objectivesChanged;
        private readonly KeyCode key, calculatorKey, requirementsKey;
        private readonly PanelPlacement placement;
        private readonly ConfigEntry<bool> calculatorEnabled, requirementsEnabled;
        private readonly Action<bool> calculatorChanged, requirementsChanged;
        public bool Visible { get; private set; }

        public TweakerPanel(ConfigFile config, ManualLogSource log, KeyCode key, KeyCode calculatorKey, KeyCode requirementsKey, Action objectivesChanged,
            ConfigEntry<bool> calculatorEnabled, ConfigEntry<bool> requirementsEnabled,
            Action<bool> calculatorChanged, Action<bool> requirementsChanged)
        {
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
                Mathf.Min(424 * scale, Screen.width * 0.46f), 450 * scale));
        }

        public bool Contains(Vector2 point) { return Visible && GetRect().Contains(point); }

        public void Draw()
        {
            if (!Visible) return;
            theme.Ensure();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.2f);
            Rect rect = GetRect();
            placement.HandleDrag(ref rect, 68 * scale, Application.isFocused);
            float width = rect.width;
            Matrix4x4 oldMatrix = GUI.matrix;
            int oldDepth = GUI.depth;
            GUI.depth = -10000;
            GUI.DrawTexture(rect, theme.Background);
            GUI.matrix = Matrix4x4.TRS(new Vector3(rect.x, rect.y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            float w = width / scale;
            GUI.Label(new Rect(18, 14, w - 36, 28), "PCBS Helper", theme.Title);
            GUI.Label(new Rect(18, 44, w - 36, 24), key + HelperLocalization.Text("/Esc — close window"), theme.Muted);
            bool changed = Row(allApps, "Install All Apps", 80, w);
            changed |= Row(instant3DMark, "Instant 3DMark", 130, w);
            changed |= Row(diagnose, "Show Diagnose Objectives", 180, w);
            changed |= Row(hidden, "Show Hidden Objectives", 230, w);
            if (changed)
            {
                Apply();
                if (objectivesChanged != null) objectivesChanged();
            }
            if (Row(calculatorEnabled, "Build Helper", 290, w, calculatorKey)) calculatorChanged(calculatorEnabled.Value);
            if (Row(requirementsEnabled, "Will it Run? Helper", 340, w, requirementsKey)) requirementsChanged(requirementsEnabled.Value);
            if (GUI.Button(new Rect(18, 402, w - 36, 30), HelperLocalization.Text("Close"), theme.Button)) Hide();
            GUI.matrix = oldMatrix; GUI.depth = oldDepth;
        }

        private bool Row(ConfigEntry<bool> setting, string label, float y, float width, KeyCode shortcut = KeyCode.None)
        {
            bool value = GUI.Toggle(new Rect(18, y, width - 36, 42), setting.Value,
                HelperLocalization.Text(setting.Value ? "[ON]   " : "[OFF]  ") + HelperLocalization.Text(label) +
                (shortcut == KeyCode.None ? "" : " (" + shortcut + ")"), theme.Toggle);
            if (value == setting.Value) return false;
            setting.Value = value;
            return true;
        }

        public void Dispose() { placement.CancelDrag(); theme.Dispose(); }
    }
}
