using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ZenFulcrum.EmbeddedBrowser;

namespace PCBSCalculatorHelper
{
    [BepInPlugin("local.pcbs.calculator", "PCBS Helper", "1.2.1")]
    [DefaultExecutionOrder(-32000)]
    public sealed class CalculatorPlugin : BaseUnityPlugin
    {
        internal static CalculatorPlugin Instance;
        private ConfigEntry<KeyCode> hotkey;
        private ConfigEntry<float> zoom;
        private Harmony harmony;
        private Browser browser;
        private GameObject browserObject;
        private OverlayInput input;
        private bool visible;
        private bool pageLoaded;
        private string error;
        private string htmlPath;
        private int polledFrame = -1;
        private int blockUntilFrame = -1;
        private bool waitForMouseRelease;
        private Rect windowRect;
        private Rect pageRect;
        private readonly HelperTheme calculatorTheme = new HelperTheme();
        private bool inputGuardsAttempted;
        private RequirementsPanel requirements;
        private ConfigEntry<KeyCode> requirementsKey;
        private ConfigEntry<KeyCode> tweakerKey;
        private ConfigEntry<bool> expandedSetting;
        private TweakerPanel tweaker;
        private bool expanded;
        private PanelPlacement calculatorPlacement;
        private ConfigEntry<bool> calculatorEnabled, requirementsEnabled;
        private CalculatorSession calculatorSession;
        private float nextCareerSync;
        private string lastCareerJson;
        private bool clearSupported = true;
        private JobHelperButton jobHelper;
        private JSONNode pendingJobImport;
        private bool panelOwnsCursor;
        private CursorLockMode panelPreviousLock;
        private bool panelPreviousVisible;

        internal static bool BlockGameInput
        {
            get
            {
                return Instance != null && (Instance.ModalVisible || Instance.panelOwnsCursor || Instance.CalculatorPointerOverPanel ||
                    (Instance.requirements != null && Instance.requirements.PointerOverPanel) ||
                    Time.frameCount <= Instance.blockUntilFrame || Instance.waitForMouseRelease);
            }
        }

        private void Awake()
        {
            Instance = this;
            hotkey = Config.Bind("Controls", "ToggleKey", KeyCode.F2,
                "Open/close the embedded calculator. Restart the game after editing.");
            zoom = Config.Bind("Display", "Zoom", 1.0f, "Browser zoom: 0.6 to 1.8. Also adjustable in the toolbar.");
            requirementsKey = Config.Bind("Controls", "RequirementsKey", KeyCode.F3,
                "Toggle the game requirements side panel. Hold Alt to interact with it.");
            bool autoRequirements = Config.Bind("Display", "AutoShowRequirements", true,
                "Show game requirements automatically beside the visible email/PC job. F3 pins the panel.").Value;
            calculatorEnabled = Config.Bind("Helpers", "CalculatorEnabled", true,
                "Enable the calculator and its hotkey. Also controlled in Tweaker.");
            requirementsEnabled = Config.Bind("Helpers", "WillItRunEnabled", true,
                "Enable requirements, automatic display and F3. Also controlled in Tweaker.");
            calculatorPlacement = new PanelPlacement(Config, "Calculator");
            requirements = new RequirementsPanel(Config, Logger, requirementsKey.Value, autoRequirements);
            requirements.SetEnabled(requirementsEnabled.Value);
            requirements.PointerOccluded = delegate(Vector2 point)
            { return (visible && windowRect.Contains(point)) || (tweaker != null && tweaker.Contains(point)); };
            tweakerKey = Config.Bind("Controls", "TweakerKey", KeyCode.F1, "Open/close Tweaker.");
            expandedSetting = Config.Bind("Display", "CalculatorExpanded", false,
                "Open the calculator as a full window. Switch sizes with Expand/Compact in the toolbar.");
            expanded = expandedSetting.Value;
            tweaker = new TweakerPanel(Config, Logger, tweakerKey.Value, requirements.RefreshObjectives,
                calculatorEnabled, requirementsEnabled,
                delegate(bool value) { if (!value) SetVisible(false); UpdatePanelCursor(); },
                delegate(bool value) { requirements.SetEnabled(value); UpdatePanelCursor(); });
            htmlPath = Path.Combine(Path.GetDirectoryName(Info.Location), "calculator.html");
            calculatorSession = new CalculatorSession(Paths.ConfigPath, Logger);
            jobHelper = new JobHelperButton(delegate { return calculatorEnabled.Value; }, OpenJobCalculator, Logger);
            Logger.LogInfo("Calculator ready; waiting for the game's input system before installing guards.");
        }

        private void InstallInputGuards()
        {
            inputGuardsAttempted = true;
            try
            {
                harmony = new Harmony("local.pcbs.calculator");
                Patch(typeof(InputAxis), "Get", "BlockAxis");
                Patch(typeof(InputButton), "Get", "BlockAxis");
                foreach (string method in new string[] { "IsHeld", "IsDown", "IsDownOnJoystick", "IsUp", "ConsumeDown" })
                    Patch(typeof(InputButton), method, "BlockButton");
                Patch(typeof(InputModule), "Process", "BlockProcess");
                Patch(typeof(InputModule), "UpdateModule", "BlockProcess");
                Patch(typeof(PCBSInput), "Update", "PollBeforeGameInput");
                Patch(typeof(GameController), "CursorOverUI", "BlockWorldPointer", Type.EmptyTypes);
                MethodInfo jobUpdate = AccessTools.Method(typeof(JobStatus), "UpdateJob", new Type[] { typeof(int), typeof(ComputerSave) });
                harmony.Patch(jobUpdate, null, new HarmonyMethod(typeof(CalculatorPlugin), "OnJobStatusUpdated"), null, null, null);
                MethodInfo emailSet = AccessTools.Method(typeof(EmailJobDetails), "Set", new Type[] { typeof(Job), typeof(bool) });
                harmony.Patch(emailSet, null, new HarmonyMethod(typeof(CalculatorPlugin), "OnEmailJobShown"), null, null, null);
                MethodInfo emailPane = AccessTools.Method(typeof(EmailPane), "UpdateJobInfo", Type.EmptyTypes);
                harmony.Patch(emailPane, null, new HarmonyMethod(typeof(CalculatorPlugin), "OnEmailPaneUpdated"), null, null, null);
                tweaker.Apply();
                Logger.LogInfo("Installed input guards. Press " + hotkey.Value + " to open the offline calculator.");
            }
            catch (Exception ex)
            {
                error = "Input protection could not be initialized. See BepInEx/LogOutput.log.";
                Logger.LogError(ex);
                if (harmony != null) harmony.UnpatchSelf();
            }
        }

        private void Patch(Type type, string method, string prefix)
        {
            Patch(type, method, prefix, Type.EmptyTypes);
        }

        private void Patch(Type type, string method, string prefix, Type[] arguments)
        {
            MethodInfo target = AccessTools.Method(type, method, arguments);
            if (target == null) throw new MissingMethodException(type.FullName, method);
            harmony.Patch(target, new HarmonyMethod(typeof(CalculatorPlugin), prefix), null, null, null, null);
        }

        private static bool BlockAxis(ref float __result)
        {
            if (!BlockGameInput) return true;
            __result = 0;
            return false;
        }

        private static bool BlockButton(ref bool __result)
        {
            if (!BlockGameInput) return true;
            __result = false;
            return false;
        }

        private static bool BlockProcess() { return !BlockGameInput; }

        private static bool BlockWorldPointer(ref bool __result)
        {
            if (!BlockGameInput) return true;
            __result = true;
            return false;
        }

        private static void PollBeforeGameInput()
        {
            if (Instance != null) Instance.PollHotkey();
        }

        private static void OnJobStatusUpdated(int __0, JobStatus __instance)
        {
            if (Instance != null && Instance.requirements != null)
            {
                try { Instance.requirements.ObserveStatus(__0, __instance); }
                catch (Exception ex) { Instance.Logger.LogWarning("Could not read PC job: " + ex.Message); }
            }
        }

        private static void OnEmailJobShown(Job __0, EmailJobDetails __instance)
        {
            if (Instance != null && Instance.requirements != null)
            {
                try { Instance.requirements.ObserveEmail(__0, __instance); }
                catch (Exception ex) { Instance.Logger.LogWarning("Could not read email job: " + ex.Message); }
            }
        }

        private static void OnEmailPaneUpdated(EmailPane __instance)
        { if (Instance != null && Instance.jobHelper != null) Instance.jobHelper.Refresh(__instance); }

        private void OpenJobCalculator(Job job)
        {
            if (!calculatorEnabled.Value) return;
            try
            {
                pendingJobImport = JobCalculatorImport.Read(job);
                SetVisible(true);
                if (browser != null && pageLoaded) ApplyPendingJob();
                Logger.LogInfo("Calculator request from job #" + job.GetId() + ": " + job.GetJobType());
            }
            catch (Exception ex) { Logger.LogError("Could not import job into calculator: " + ex); }
        }

        private void ApplyPendingJob()
        {
            if (pendingJobImport == null) return;
            browser.CallFunction("pcbsImportJob", new JSONNode[] { pendingJobImport });
            pendingJobImport = null;
        }

        private void PollHotkey()
        {
            if (polledFrame == Time.frameCount) return;
            polledFrame = Time.frameCount;
            if (waitForMouseRelease && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
                waitForMouseRelease = false;
            if (!Application.isFocused) { calculatorPlacement.CancelDrag(); UpdatePanelCursor(); return; }
            if (Input.GetKeyDown(tweakerKey.Value))
            {
                if (!tweaker.Visible) SetVisible(false);
                tweaker.Toggle();
                SyncPanelVisibility();
                ReleaseInputGrace();
            }
            else if (calculatorEnabled.Value && Input.GetKeyDown(hotkey.Value)) SetVisible(!visible);
            else if (requirementsEnabled.Value && Input.GetKeyDown(requirementsKey.Value))
            {
                SetVisible(false); tweaker.Hide(); requirements.SetDisplayBlocked(false);
                requirements.Toggle(); ReleaseInputGrace();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (tweaker.Visible) { tweaker.Hide(); ReleaseInputGrace(); }
                else if (visible) SetVisible(false);
            }
            SyncPanelVisibility(); UpdatePanelCursor();
        }

        private void SyncPanelVisibility()
        { requirements.SetDisplayBlocked(visible || (tweaker != null && tweaker.Visible)); }

        private bool ModalVisible { get { return (visible && expanded) || (tweaker != null && tweaker.Visible); } }

        private bool CalculatorPointerOverPanel
        {
            get { return visible && Cursor.lockState != CursorLockMode.Locked &&
                windowRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)); }
        }

        private void ReleaseInputGrace()
        { blockUntilFrame = Time.frameCount + 2; waitForMouseRelease = true; }

        private void UpdatePanelCursor()
        {
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool takeCursor = Application.isFocused && (ModalVisible || (visible && (alt || calculatorPlacement.IsDragging)) || (requirements != null && requirements.WantsCursor));
            if (takeCursor && !panelOwnsCursor)
            {
                panelPreviousLock = Cursor.lockState;
                panelPreviousVisible = Cursor.visible;
                panelOwnsCursor = true;
            }
            else if (!takeCursor && panelOwnsCursor)
            {
                panelOwnsCursor = false;
                Cursor.lockState = panelPreviousLock;
                Cursor.visible = panelPreviousVisible;
                ReleaseInputGrace();
            }
            if (panelOwnsCursor) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        private void Update()
        {
            // Patching PCBSInput in Awake forces its static constructor before Rewired exists.
            if (!inputGuardsAttempted && Rewired.ReInput.isReady) InstallInputGuards();
            if (!inputGuardsAttempted) return;
            SyncPanelVisibility(); requirements.Update();
            jobHelper.Update();
            PollHotkey();
            if (browser != null && pageLoaded && Time.unscaledTime >= nextCareerSync)
            {
                nextCareerSync = Time.unscaledTime + 1;
                string career = CalculatorSession.CareerJson();
                if (career != lastCareerJson)
                {
                    browser.CallFunction("pcbsUpdateCareer", new JSONNode[] { JSONNode.Parse(career) });
                    lastCareerJson = career;
                }
            }
            if (visible)
            {
                UpdateLayout();
            }
        }

        private void LateUpdate()
        {
            if (panelOwnsCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void SetVisible(bool show)
        {
            if (show && !calculatorEnabled.Value) return;
            if (visible == show) return;
            if (show)
            {
                if (tweaker != null) tweaker.Hide();
                visible = true;
                UpdateLayout();
                if (error == null && browser == null) CreateBrowser();
                if (browser != null) browser.EnableRendering = true;
                Logger.LogInfo("Calculator opened.");
            }
            else
            {
                visible = false;
                calculatorPlacement.CancelDrag();
                // Keep the page alive, but let the browser process focus loss and key releases.
                if (input != null) input.Clear();
                if (browser != null) browser.EnableRendering = false;
                if (browser != null && pageLoaded) browser.CallFunction("pcbsFlush", new JSONNode[0]);
                ReleaseInputGrace();
                Logger.LogInfo("Calculator hidden; page state preserved.");
            }
            SyncPanelVisibility(); UpdatePanelCursor();
        }

        private void CreateBrowser()
        {
            try
            {
                string assetDir = Path.GetDirectoryName(htmlPath);
                Assembly assembly = typeof(CalculatorPlugin).Assembly;
                string html = EmbeddedAssets.Read("calculator.html", assembly, assetDir);
                html = html.Replace("</body>", "<script>" + EmbeddedAssets.Read("embedded-ui.js", assembly, assetDir) + "</script></body>");
                html = html.Replace("</head>", "<style>" + EmbeddedAssets.Read("calculator-theme.css", assembly, assetDir) + "</style></head>");
                string bootstrap = "window.pcbsInitialState = JSON.parse(" + CalculatorSession.ScriptString(calculatorSession.Load()) +
                    "); window.pcbsInitialCareer = " + CalculatorSession.CareerJson() + ";";
                html = html.Replace("</body>", "<script>" + bootstrap + "</script><script>" +
                    EmbeddedAssets.Read("calculator-state-codec.js", assembly, assetDir) + "</script><script>" +
                    EmbeddedAssets.Read("calculator-job-rules.js", assembly, assetDir) + "</script><script>" +
                    EmbeddedAssets.Read("calculator-job-import.js", assembly, assetDir) + "</script><script>" +
                    EmbeddedAssets.Read("calculator-state.js", assembly, assetDir) + "</script></body>");
                browserObject = new GameObject("PCBS Calculator Browser");
                DontDestroyOnLoad(browserObject);
                input = new OverlayInput(this);
                browser = browserObject.AddComponent<Browser>();
                browser.UIHandler = input;
                browser.EnableInput = true;
                browser.Resize((int)pageRect.width, (int)pageRect.height);
                browser.Zoom = Mathf.Clamp(zoom.Value, 0.6f, 1.8f);
                browser.SetNewWindowHandler(Browser.NewWindowAction.Ignore, null);
                browser.onLoad += delegate(JSONNode info)
                {
                    pageLoaded = true;
                    browser.RegisterFunction("pcbsSaveCalculator", delegate(JSONNode args) {
                        if (args.Count == 1 && args[0].Value is string) calculatorSession.Save((string)args[0]);
                    });
                    browser.RegisterFunction("pcbsCalculatorTab", delegate(JSONNode args) {
                        if (args.Count == 1 && args[0].Value is bool) clearSupported = (bool)args[0];
                    });
                    nextCareerSync = 0; lastCareerJson = null;
                    ApplyPendingJob();
                    Logger.LogInfo("Offline calculator page loaded.");
                };
                browser.onConsoleMessage += delegate(string message, string source)
                {
                    Logger.LogInfo("Calculator JS: " + message + " (" + source + ")");
                };
                browser.onSadTab += delegate { error = "The calculator browser stopped. Restart the game to retry."; };
                // The game's localGame origin supports JS storage without an HTTP server.
                browser.LoadHTML(html, Browser.LocalUrlPrefix + "pcbs-calculator/index.html");
            }
            catch (Exception ex)
            {
                error = "Could not open the calculator. See BepInEx/LogOutput.log.";
                Logger.LogError(ex);
                if (browserObject != null) Destroy(browserObject);
                browser = null;
                browserObject = null;
            }
        }

        private void UpdateLayout()
        {
            float oldPageWidth = pageRect.width, oldPageHeight = pageRect.height;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.2f);
            float margin = 20 * scale;
            if (expanded) windowRect = new Rect(margin, margin, Screen.width - margin * 2, Screen.height - margin * 2);
            else windowRect = new Rect(margin, 118 * scale, Mathf.Min(560 * scale, Screen.width * 0.46f),
                Mathf.Min(860 * scale, Screen.height - 142 * scale));
            if (!expanded) windowRect = calculatorPlacement.Resolve(windowRect);
            UpdatePageRect();
            if (browser != null && (oldPageWidth != pageRect.width || oldPageHeight != pageRect.height))
                browser.Resize(Mathf.Max(100, (int)pageRect.width), Mathf.Max(100, (int)pageRect.height));
        }

        private void UpdatePageRect()
        {
            pageRect = new Rect(windowRect.x + 8, windowRect.y + 108, windowRect.width - 16, windowRect.height - 144);
        }

        private void ChangeZoom(float delta)
        {
            zoom.Value = Mathf.Clamp(zoom.Value + delta, 0.6f, 1.8f);
            if (browser != null) browser.Zoom = zoom.Value;
        }

        private void SetExpanded(bool value)
        {
            calculatorPlacement.CancelDrag();
            expanded = value; expandedSetting.Value = value;
            UpdateLayout();
            if (input != null) input.Clear();
            UpdatePanelCursor();
            Logger.LogInfo("Calculator size: " + (expanded ? "full" : "compact") + "; page state preserved.");
        }

        private void OnGUI()
        {
            SyncPanelVisibility();
            if (requirements != null) requirements.Draw();
            if (tweaker != null) tweaker.Draw();
            SyncPanelVisibility();
            UpdatePanelCursor();
            if (!visible) return;
            GUI.depth = -10000;
            calculatorTheme.Ensure();
            Event ev = Event.current;
            if (!expanded && calculatorPlacement.HandleDrag(ref windowRect, 68, Application.isFocused &&
                (Cursor.lockState != CursorLockMode.Locked || panelOwnsCursor))) UpdatePageRect();
            UpdatePanelCursor();
            if (input != null && input.KeyboardHasFocus &&
                (ev.type == EventType.KeyDown || ev.type == EventType.KeyUp) &&
                ev.keyCode != hotkey.Value && ev.keyCode != tweakerKey.Value && ev.keyCode != requirementsKey.Value && ev.keyCode != KeyCode.Escape)
                input.Enqueue(ev);

            Color oldColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(windowRect, calculatorTheme.Background);
            GUI.Label(new Rect(windowRect.x + 18, windowRect.y + 14, windowRect.width - 36, 28), "Calculator", calculatorTheme.Title);
            GUI.Label(new Rect(windowRect.x + 18, windowRect.y + 44, windowRect.width - 36, 24), hotkey.Value + " / Esc — hide • Offline", calculatorTheme.Muted);
            float x = windowRect.x + 18, y = windowRect.y + 74;
            bool narrow = windowRect.width - 36 < 430;
            float expandW = narrow ? 78 : 104, stepW = narrow ? 24 : 34, numberW = narrow ? 44 : 50;
            float smallW = narrow ? 54 : 70, gap = narrow ? 6 : 10;
            if (GUI.Button(new Rect(x, y, expandW, 28), expanded ? "Compact" : "Expand", calculatorTheme.Button)) SetExpanded(!expanded);
            x += expandW + gap;
            if (GUI.Button(new Rect(x, y, stepW, 28), "−", calculatorTheme.Button)) ChangeZoom(-0.1f);
            x += stepW + gap;
            GUI.Label(new Rect(x, y + 4, numberW, 24), Mathf.RoundToInt(zoom.Value * 100) + "%", calculatorTheme.Muted);
            x += numberW + gap;
            if (GUI.Button(new Rect(x, y, stepW, 28), "+", calculatorTheme.Button)) ChangeZoom(0.1f);
            x += stepW + gap;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && pageLoaded && clearSupported;
            if (GUI.Button(new Rect(x, y, smallW, 28), "Clear", calculatorTheme.Button))
                browser.CallFunction("pcbsClearCurrentTab", new JSONNode[0]);
            GUI.enabled = oldEnabled;
            if (GUI.Button(new Rect(windowRect.xMax - smallW - 18, y, smallW, 28), "Close", calculatorTheme.Button))
                SetVisible(false);
            if (browser != null && browser.Texture != null && pageLoaded && error == null)
                GUI.DrawTexture(pageRect, browser.Texture, ScaleMode.StretchToFill, false);
            else
                GUI.Label(new Rect(pageRect.x + 20, pageRect.y + 20, pageRect.width - 40, 80),
                    error ?? "Loading the offline calculator...");
            GUI.color = oldColor;
            GUI.Label(new Rect(windowRect.x + 18, windowRect.yMax - 28, windowRect.width - 36, 24),
                expanded ? "Compact returns to the saved side-panel position" : "Alt — cursor • Drag header to move", calculatorTheme.Muted);
            if (ev.type != EventType.Repaint && ev.type != EventType.Layout && ev.type != EventType.Used &&
                (ModalVisible || windowRect.Contains(ev.mousePosition) || (input != null && input.KeyboardHasFocus)) &&
                (ev.isKey || ev.isMouse || ev.type == EventType.ScrollWheel)) ev.Use();
        }

        private void OnDisable()
        {
            if (visible) SetVisible(false);
            if (tweaker != null) tweaker.Hide();
            if (panelOwnsCursor) { panelOwnsCursor = false; Cursor.lockState = panelPreviousLock; Cursor.visible = panelPreviousVisible; }
        }

        private void OnDestroy()
        {
            if (visible) SetVisible(false);
            if (browserObject != null) Destroy(browserObject);
            if (requirements != null) requirements.Dispose();
            if (tweaker != null) tweaker.Dispose();
            if (jobHelper != null) jobHelper.Dispose();
            calculatorTheme.Dispose();
            calculatorPlacement.CancelDrag();
            if (harmony != null) harmony.UnpatchSelf();
            if (Instance == this) Instance = null;
        }

        private sealed class OverlayInput : IBrowserUI
        {
            private readonly CalculatorPlugin owner;
            private List<Event> pending = new List<Event>();
            private List<Event> current = new List<Event>();
            public OverlayInput(CalculatorPlugin owner)
            {
                this.owner = owner;
                BrowserCursor = new BrowserCursor();
                InputSettings = new BrowserInputSettings();
            }
            public void Enqueue(Event ev) { pending.Add(new Event(ev)); }
            public void Clear() { pending.Clear(); current.Clear(); }
            public void InputUpdate()
            {
                List<Event> swap = current;
                current = pending;
                pending = swap;
                pending.Clear();
            }
            private Vector2 Pointer
            {
                get { return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y); }
            }
            public bool MouseHasFocus { get { return KeyboardHasFocus && owner.pageRect.Contains(Pointer); } }
            public bool KeyboardHasFocus { get { return owner.visible && Application.isFocused &&
                (owner.expanded || owner.CalculatorPointerOverPanel); } }
            public Vector2 MousePosition
            {
                get
                {
                    Rect r = owner.pageRect;
                    Vector2 p = Pointer;
                    return new Vector2((p.x - r.x) / r.width, 1 - (p.y - r.y) / r.height);
                }
            }
            public MouseButton MouseButtons
            {
                get
                {
                    MouseButton buttons = 0;
                    if (!MouseHasFocus) return buttons;
                    if (Input.GetMouseButton(0)) buttons |= MouseButton.Left;
                    if (Input.GetMouseButton(1)) buttons |= MouseButton.Right;
                    if (Input.GetMouseButton(2)) buttons |= MouseButton.Middle;
                    return buttons;
                }
            }
            public Vector2 MouseScroll { get { return MouseHasFocus ? Input.mouseScrollDelta : Vector2.zero; } }
            public List<Event> KeyEvents { get { return current; } }
            public BrowserCursor BrowserCursor { get; private set; }
            public BrowserInputSettings InputSettings { get; private set; }
        }
    }
}
