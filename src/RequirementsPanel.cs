using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PCBSCalculatorHelper
{
    internal sealed class RequirementsPanel
    {
        private sealed class Choice
        {
            public string Key;
            public ProgramRequirementsDesc Requirements;
            public int JobId;
            public Job Job;
        }

        private readonly ManualLogSource log;
        private readonly HelperTheme theme = new HelperTheme();
        private readonly PanelPlacement placement;
        private readonly List<Choice> jobChoices = new List<Choice>();
        private readonly List<Choice> catalog = new List<Choice>();
        private readonly List<RequirementPart> parts = new List<RequirementPart>();
        private readonly List<RequirementPart> candidates = new List<RequirementPart>();
        private RequirementClarifications clarifications = new RequirementClarifications();
        private Choice selected;
        private Job currentJob;
        private bool loaded, manual, selecting;
        private bool pinned, suppressed;
        private bool displayBlocked;
        private readonly bool autoShow;
        private string autoContext = "";
        private EmailJobDetails observedEmail;
        private JobStatus observedStatus;
        private static readonly FieldInfo EmailJobField = AccessTools.Field(typeof(EmailJobDetails), "m_job");
        private static readonly FieldInfo StatusFocusField = AccessTools.Field(typeof(AlphaTransition), "m_focus");
        private static readonly FieldInfo CurrentStateField = AccessTools.Field(typeof(GameController), "m_currentState");
        private static readonly FieldInfo GameModeField = AccessTools.Field(typeof(GameController), "m_gameMode");
        private static readonly FieldInfo EsportsRequirementsIdField = AccessTools.Field(typeof(FuturLab.EsportsInstalledAndPlayableObjective), "m_requirementsID");
        private bool restrictByCareerLevel;
        private string query = "", status = "Point at a job PC or open a job email.";
        private Vector2 scroll;
        private Vector2 detailsScroll;
        private float detailsHeight = 530;
        private float nextLoadAttempt;
        private GUIStyle titleStyle, bodyStyle, mutedStyle, labelStyle, buttonStyle;
        private Texture2D background;
        private Rect rect;
        private KeyCode toggleKey;
        private string lastLoggedKey;
        private string detailsLanguage;
        private bool clarificationError;

        public RequirementsPanel(ConfigFile config, ManualLogSource log, KeyCode toggleKey, bool autoShow)
        {
            this.log = log; this.toggleKey = toggleKey; this.autoShow = autoShow;
            placement = new PanelPlacement(config, "Requirements");
            Enabled = true;
        }

        public bool Enabled { get; private set; }
        public bool Visible { get; private set; }
        public Func<Vector2, bool> PointerOccluded;
        public bool CursorEnabled { get; set; }
        public bool WantsCursor { get { return Visible && Application.isFocused &&
            (placement.IsDragging || CursorEnabled); } }
        public void CancelCursorInteraction() { placement.CancelDrag(); }
        public bool PointerOverPanel
        {
            get { return Visible && Cursor.lockState != CursorLockMode.Locked &&
                rect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)) &&
                (PointerOccluded == null || !PointerOccluded(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))); }
        }

        public void SetEnabled(bool value)
        {
            Enabled = value;
            autoContext = ""; suppressed = false; nextLoadAttempt = 0;
            if (!value) { placement.CancelDrag(); Visible = false; pinned = false; }
        }

        public void SetDisplayBlocked(bool value)
        {
            displayBlocked = value;
            // Switching helpers dismisses this panel rather than keeping it pinned behind them.
            if (value) { placement.CancelDrag(); pinned = false; suppressed = true; Visible = false; }
        }

        public void Toggle()
        {
            if (!Enabled) return;
            if (pinned && Visible) { placement.CancelDrag(); pinned = false; suppressed = true; Visible = false; }
            else
            {
                EnsureData();
                if (!Visible) FindVisibleJob();
                pinned = true; suppressed = false; Visible = true;
            }
        }

        public void ShowJob(Job job)
        {
            if (!Enabled || job == null) return;
            placement.CancelDrag();
            currentJob = job; manual = false;
            SelectJob(job);
            pinned = true; suppressed = false; Visible = !displayBlocked;
        }

        public void Update()
        {
            if (!Application.isFocused) placement.CancelDrag();
            if (!Enabled) return;
            if (Visible && loaded && selected != null && detailsLanguage != I2.Loc.LocalizationManager.CurrentLanguageCode)
                RefreshAlternatives();
            if ((Visible || autoShow) && !loaded && Time.unscaledTime >= nextLoadAttempt)
            {
                nextLoadAttempt = Time.unscaledTime + 2;
                if (EnsureData() && currentJob != null && !manual) SelectJob(currentJob);
            }
            Job activeJob;
            string context = FindActiveTask(out activeJob);
            if (!autoShow) { autoContext = context; Visible = pinned && !displayBlocked; return; }
            if (context != autoContext)
            {
                autoContext = context;
                suppressed = displayBlocked;
                if (activeJob != null)
                {
                    currentJob = activeJob;
                    if (!manual && !pinned) SelectJob(activeJob);
                }
            }
            if (activeJob != null && !manual && !pinned && !System.Object.ReferenceEquals(currentJob, activeJob))
                ObserveJob(activeJob);
            bool show = !displayBlocked && (pinned || (!suppressed && context.StartsWith("pc:", StringComparison.Ordinal) &&
                activeJob != null && !manual && selected != null));
            if (!show) placement.CancelDrag();
            Visible = show;
        }

        public void ObserveEmail(Job job, EmailJobDetails details)
        { observedEmail = details; ObserveJob(job); }

        public void ObserveStatus(int jobId, JobStatus status)
        { observedStatus = status; ObserveJobId(jobId); }

        public void RefreshObjectives()
        {
            if (currentJob != null && !manual && !pinned) SelectJob(currentJob);
            else if (!manual) RefreshAlternatives();
            if (observedEmail != null && observedEmail.isActiveAndEnabled) observedEmail.UpdateStatus();
            JobStatus status = observedStatus != null ? observedStatus : WorkshopUI.m_jobStatus;
            if (status != null)
            {
                BenchSlot slot = StatusFocusField.GetValue(status) as BenchSlot;
                if (slot != null) slot.RequestQuickUpdate();
            }
        }

        private string FindActiveTask(out Job job)
        {
            job = null;
            GameController controller = GameController.Get();
            if (controller == null) return "";
            State state = CurrentStateField.GetValue(controller) as State;
            if (state is LookingAtMonitorState && observedEmail != null && observedEmail.isActiveAndEnabled)
            {
                job = EmailJobField.GetValue(observedEmail) as Job;
                if (job != null) return "email:" + job.GetId();
            }
            // Emails provide context for an explicit button/F3 action; only assembly opens automatically.
            if (!(state is WorkingOnComputerStateBase)) return "";
            JobStatus status = observedStatus != null ? observedStatus : WorkshopUI.m_jobStatus;
            if (status == null || !status.isActiveAndEnabled) return "";
            CanvasGroup group = status.GetComponent<CanvasGroup>();
            if (group != null && group.alpha < 0.05f) return "";
            BenchSlot slot = StatusFocusField.GetValue(status) as BenchSlot;
            if (slot == null || slot.m_status == null || slot.m_status.jobID <= 0) return "";
            CareerStatus career = CareerStatus.Get();
            if (career != null) job = career.GetJob(slot.m_status.jobID);
            return job == null ? "" : "pc:" + job.GetId();
        }

        public void ObserveJob(Job job)
        {
            if (job == null || System.Object.ReferenceEquals(currentJob, job)) return;
            currentJob = job;
            if (Enabled && !manual && !pinned) SelectJob(job);
        }

        public void ObserveJobId(int jobId)
        {
            if (jobId <= 0) return;
            CareerStatus career = CareerStatus.Get();
            if (career != null) ObserveJob(career.GetJob(jobId));
        }

        private void FindVisibleJob()
        {
            // Also handles opening the HUD after a context update was already emitted.
            // Explicit observations have priority over other PCs' still-running email windows.
            if (currentJob != null)
            {
                if (!manual) SelectJob(currentJob);
                else RefreshAlternatives();
                return;
            }
            Job visibleEmailJob = null;
            foreach (EmailJobDetails details in UnityEngine.Object.FindObjectsOfType<EmailJobDetails>())
                if (details.isActiveAndEnabled)
                {
                    Job job = EmailJobField.GetValue(details) as Job;
                    if (job == null) continue;
                    if (visibleEmailJob != null && !System.Object.ReferenceEquals(visibleEmailJob, job))
                    { visibleEmailJob = null; break; }
                    visibleEmailJob = job;
                }
            if (visibleEmailJob != null) { ObserveJob(visibleEmailJob); return; }
            if (WorkshopUI.m_jobStatus != null)
            {
                BenchSlot slot = StatusFocusField.GetValue(WorkshopUI.m_jobStatus) as BenchSlot;
                if (slot != null && slot.m_status != null) { ObserveJobId(slot.m_status.jobID); return; }
            }
            if (currentJob != null && !manual) SelectJob(currentJob);
        }

        private void SelectJob(Job job)
        {
            if (!EnsureData()) return;
            jobChoices.Clear();
            foreach (Objective objective in job.GetObjectives())
            {
                string key;
                ProgramRequirementsDesc desc;
                if (TryGetRequirements(objective, out key, out desc))
                    jobChoices.Add(new Choice { Key = key, Requirements = desc, JobId = job.GetId(), Job = job });
            }
            selected = jobChoices.Count > 0 ? jobChoices[0] : null;
            status = "This job has no visible Will it Run? requirements.";
            selecting = false;
            RefreshAlternatives();
        }

        public static bool HasJobRequirements(Job job)
        {
            if (job == null) return false;
            foreach (Objective objective in job.GetObjectives())
            {
                string key;
                ProgramRequirementsDesc desc;
                if (TryGetRequirements(objective, out key, out desc)) return true;
            }
            return false;
        }

        private static bool TryGetRequirements(Objective objective, out string key, out ProgramRequirementsDesc desc)
        {
            key = null; desc = null;
            if (objective == null || objective.IsInvalid()) return false;
            if (objective.IsHidden() && !DebugVars.s_showHiddenObjectives &&
                !(objective.IsHiddenByDiagnose() && DebugVars.s_showDiagnoseObjectives)) return false;
            CanPlayObjective play = objective as CanPlayObjective;
            if (play != null) key = play.GetSoftware();
            else
            {
                FuturLab.EsportsInstalledAndPlayableObjective esports = objective as FuturLab.EsportsInstalledAndPlayableObjective;
                if (esports != null) key = EsportsRequirementsIdField.GetValue(esports) as string;
            }
            if (!String.IsNullOrEmpty(key)) desc = CareerDesc.GetProgramRequirements(key);
            return desc != null;
        }

        private bool EnsureData()
        {
            if (loaded) return true;
            if (CareerDesc.Get() == null) { status = "Load the workshop to read game requirements."; return false; }
            try
            {
                Dictionary<string, ProgramRequirementsDesc> data = CareerDesc.GetProgramRequirements();
                if (data == null || data.Count == 0) return false;
                catalog.Clear();
                foreach (KeyValuePair<string, ProgramRequirementsDesc> entry in data)
                    if (entry.Value.m_isGame) catalog.Add(new Choice { Key = entry.Key, Requirements = entry.Value });
                catalog.Sort(delegate(Choice a, Choice b)
                {
                    int byName = String.Compare(a.Requirements.m_uiName, b.Requirements.m_uiName, StringComparison.OrdinalIgnoreCase);
                    return byName != 0 ? byName : a.Requirements.m_type.CompareTo(b.Requirements.m_type);
                });
                parts.Clear(); candidates.Clear();
                foreach (PartDesc part in PartsDatabase.GetAllParts())
                {
                    if (part.m_isPlatformLocked || (part.m_isDLC && !part.m_isDLCInstalled)) continue;
                    RequirementPart item = JobRequirementReader.FromPart(part);
                    parts.Add(item);
                    if (part.m_inShop && (part is PartDescCPU || part is PartDescGPU)) candidates.Add(item);
                }
                loaded = true;
                log.LogInfo("Requirements database ready: " + catalog.Count + " game profiles, " + candidates.Count + " CPU/GPU candidates.");
                return true;
            }
            catch (Exception ex) { log.LogWarning("Requirements data not ready yet: " + ex.Message); return false; }
        }

        private void RefreshAlternatives()
        {
            clarifications = new RequirementClarifications();
            clarificationError = false;
            detailsLanguage = I2.Loc.LocalizationManager.CurrentLanguageCode;
            detailsScroll = Vector2.zero;
            if (selected == null) return;
            ProgramRequirementsDesc r = selected.Requirements;
            CareerStatus career = CareerStatus.Get();
            GameController controller = GameController.Get();
            restrictByCareerLevel = controller != null && (GameMode)GameModeField.GetValue(controller) == GameMode.CAREER;
            foreach (RequirementPart part in candidates) part.Unlocked = !restrictByCareerLevel || (career != null && career.IsPartUnlocked(part.Id));
            if (!manual && selected.Job != null)
            {
                try
                {
                    RequirementProfile profile = new RequirementProfile { Cpu = JobRequirementReader.FromPart(r.m_cpu),
                        Gpu = JobRequirementReader.FromPart(r.m_gpu), Ram = r.m_ram, Storage = r.m_storage };
                    clarifications = RequirementClarifications.Build(profile, JobRequirementReader.Read(selected.Job, parts), candidates, restrictByCareerLevel);
                }
                catch (Exception ex) { clarificationError = true; log.LogWarning("Could not read job clarifications: " + ex.Message); }
            }
            string logKey = selected.JobId + ":" + selected.Key + ":" + manual;
            if (lastLoggedKey != logKey)
            {
                lastLoggedKey = logKey;
                log.LogInfo("Requirements selected: " + r.m_uiName + " [" + r.m_type + "] RAM=" + r.m_ram +
                    " VRAM=" + (r.m_gpu == null ? 0 : r.m_gpu.m_vramGb) + " Storage=" + r.m_storage +
                    " CPU=" + (r.m_cpu == null ? "none" : r.m_cpu.m_uiName) +
                    " GPU=" + (r.m_gpu == null ? "none" : r.m_gpu.m_uiName) + " job=" + selected.JobId);
                foreach (JobRefinement rule in clarifications.Notes) log.LogInfo("Job clarification: " + Clean(rule.Description));
                foreach (RequirementPart part in new RequirementPart[] { clarifications.Cpu, clarifications.Gpu })
                    if (part != null) log.LogInfo(part.Type + " replacement: " + part.Name + " score=" + part.Score.ToString("F2", CultureInfo.InvariantCulture) + " unlocked=" + part.Unlocked);
                foreach (BrandReplacementOptions options in clarifications.BrandOptions)
                    foreach (RequirementPart part in new RequirementPart[] { options.Cpu, options.Gpu })
                        if (part != null) log.LogInfo(options.Rule.Brand + " " + part.Type + " brand option: " + part.Name +
                            " score=" + part.Score.ToString("F2", CultureInfo.InvariantCulture) + " unlocked=" + part.Unlocked);
            }
        }

        public void Draw()
        {
            if (!Visible) return;
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.2f);
            float width = Mathf.Min(424 * scale, Screen.width * 0.46f);
            // Keep the virtual email's title bar and close button accessible.
            float top = (autoContext.StartsWith("email:", StringComparison.Ordinal) ? 218 : 118) * scale;
            float desiredHeight = selected != null && !selecting && clarifications.Notes.Count == 0 ? 480 : 650;
            float height = Mathf.Min(desiredHeight * scale, Screen.height - top - 24 * scale);
            rect = placement.Resolve(new Rect(Screen.width - width - 20 * scale, top, width, height));
            bool occluded = PointerOccluded != null && PointerOccluded(Event.current.mousePosition);
            placement.HandleDrag(ref rect, 68 * scale, Application.isFocused && !occluded &&
                (Cursor.lockState != CursorLockMode.Locked || WantsCursor));
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            int oldDepth = GUI.depth;
            GUI.depth = -9000;
            GUI.DrawTexture(rect, background);
            GUI.matrix = Matrix4x4.TRS(new Vector3(rect.x, rect.y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            float logicalWidth = width / scale;
            GUI.Label(new Rect(18, 14, logicalWidth - 36, 28), HelperLocalization.Text("Will it Run? Helper"), titleStyle);
            GUI.Label(new Rect(18, 44, logicalWidth - 36, 24), toggleKey + HelperLocalization.Text(pinned ? " — hide" : " — pin"), mutedStyle);
            GUI.enabled = !occluded && (WantsCursor || PointerOverPanel);
            if (GUI.Button(new Rect(18, 77, 185, 30), HelperLocalization.Text("Choose game"), buttonStyle)) { selecting = !selecting; query = ""; scroll = Vector2.zero; }
            if (GUI.Button(new Rect(212, 77, logicalWidth - 230, 30), HelperLocalization.Text("From job"), buttonStyle))
            {
                manual = false; selecting = false; pinned = false; suppressed = false;
                if (currentJob != null) SelectJob(currentJob);
                else { selected = null; status = "Point at a job PC or open a job email."; }
                if (autoContext == "") pinned = true;
            }
            float footerY = height / scale - 42;
            if (selecting) DrawSelector(logicalWidth, footerY);
            else DrawRequirements(logicalWidth, footerY);
            GUI.enabled = oldEnabled;
            GUI.Label(new Rect(18, footerY + 8, logicalWidth - 36, 24), HelperLocalization.Text(CursorEnabled ? "Alt — hide cursor" : "Alt — show cursor"), mutedStyle);
            GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth;
        }

        private void DrawSelector(float width, float footerY)
        {
            GUI.Label(new Rect(18, 120, width - 36, 22), HelperLocalization.Text("Search games"), labelStyle);
            query = GUI.TextField(new Rect(18, 146, width - 36, 30), query);
            List<Choice> filtered = catalog.FindAll(delegate(Choice c)
                { return c.Requirements.m_uiName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0; });
            Rect viewport = new Rect(18, 190, width - 36, Mathf.Max(60, footerY - 190));
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, viewport.width - 20, filtered.Count * 64));
            for (int i = 0; i < filtered.Count; i++)
            {
                Choice c = filtered[i];
                if (GUI.Button(new Rect(0, i * 64, viewport.width - 22, 58), Clean(c.Requirements.m_uiName) + "\n" + TypeLabel(c.Requirements.m_type), buttonStyle))
                { selected = c; manual = true; pinned = true; selecting = false; RefreshAlternatives(); }
            }
            GUI.EndScrollView();
        }

        private void DrawRequirements(float width, float footerY)
        {
            if (!loaded || selected == null)
            { GUI.Label(new Rect(18, 124, width - 36, 110), HelperLocalization.Text(status) + HelperLocalization.Text("\n\nYou can select a game manually with Choose game."), bodyStyle); return; }
            ProgramRequirementsDesc r = selected.Requirements;
            float viewportHeight = Mathf.Max(80, footerY - 124);
            detailsScroll = GUI.BeginScrollView(new Rect(0, 122, width, viewportHeight), detailsScroll,
                new Rect(0, 0, width - 20, detailsHeight));
            width -= 20;
            float y = 0;
            y = Text(Clean(r.m_uiName), y, width, titleStyle);
            y = Text(TypeLabel(r.m_type) + (manual ? HelperLocalization.Text(" • selected game") : HelperLocalization.Text(" • job #") + selected.JobId), y + 3, width, mutedStyle);
            if (jobChoices.Count > 1 && !manual)
            {
                if (GUI.Button(new Rect(18, y + 4, width - 36, 28), HelperLocalization.Text("Next job objective"), buttonStyle))
                { selected = jobChoices[(jobChoices.IndexOf(selected) + 1) % jobChoices.Count]; RefreshAlternatives(); }
                y += 36;
            }
            y += 12;
            y = Row(HelperLocalization.Text("RAM"), r.m_ram + HelperLocalization.Text(" GB"), y, width);
            y = Row(HelperLocalization.Text("VRAM"), (r.m_gpu == null ? "—" : r.m_gpu.m_vramGb + HelperLocalization.Text(" GB")), y, width);
            y = Row(HelperLocalization.Text("GPU"), r.m_gpu == null ? "—" : Clean(r.m_gpu.m_uiName), y, width);
            y = Row(HelperLocalization.Text("CPU"), r.m_cpu == null ? "—" : Clean(r.m_cpu.m_uiName) + "\n" + SocketLabel(r.m_cpu.m_socket), y, width);
            y = Row(HelperLocalization.Text("Storage"), r.m_storage + HelperLocalization.Text(" GB"), y, width);
            if (clarificationError)
                y = Text(HelperLocalization.Text("Could not read the job clarifications. Check the order email."), y + 16, width, bodyStyle);
            if (clarifications.Notes.Count > 0)
            {
                y = Text(HelperLocalization.Text("Job clarifications"), y + 16, width, labelStyle);
                foreach (JobRefinement rule in clarifications.Notes)
                {
                    string prefix = rule.Kind == JobRefinementKind.Brand && (rule.Type == "CPU" || rule.Type == "GPU")
                        ? HelperLocalization.Text(rule.Type) + ": " : "";
                    y = Text("• " + prefix + Clean(rule.Description), y + 6, width, bodyStyle);
                }
                if (clarifications.NeedsCpu) y = DrawReplacement("CPU", clarifications.Cpu, clarifications.ManualCpu, y + 12, width);
                if (clarifications.NeedsGpu) y = DrawReplacement("GPU", clarifications.Gpu, clarifications.ManualGpu, y + 12, width);
                bool hasBrandReplacement = false;
                foreach (BrandReplacementOptions options in clarifications.BrandOptions)
                {
                    hasBrandReplacement |= options.Cpu != null || options.Gpu != null;
                    if (options.Rule.Count > 1)
                    {
                        y = Text(HelperLocalization.Text("Brand alternatives • ") + options.Rule.Brand, y + 14, width, labelStyle);
                        y = Text(HelperLocalization.Text("Required new parts: ") + options.Rule.Count + ". " +
                            HelperLocalization.Text("Select the combination manually to meet the part count."), y + 6, width, mutedStyle);
                        continue;
                    }
                    if (options.HasCpu && options.HasGpu)
                    {
                        y = Text(HelperLocalization.Text("Brand alternatives • ") + options.Rule.Brand, y + 14, width, labelStyle);
                        y = Text(HelperLocalization.Text("One matching new part is enough; choose one of the alternatives below."), y + 6, width, mutedStyle);
                    }
                    if (options.HasCpu) y = DrawReplacement("CPU", options.Cpu, clarifications.ManualCpu, y + 10, width);
                    if (options.HasGpu) y = DrawReplacement("GPU", options.Gpu, clarifications.ManualGpu, y + 10, width);
                }
                if (hasBrandReplacement)
                    y = Text(HelperLocalization.Text("Alternatives meet Will it Run? for their category. Check socket, case and power compatibility."), y + 10, width, mutedStyle);
                else if (clarifications.Cpu != null || clarifications.Gpu != null)
                    y = Text(HelperLocalization.Text("Meets Will it Run? and the listed part constraints. Check socket, case and power compatibility."), y + 10, width, mutedStyle);
            }
            if (Event.current.type == EventType.Repaint) detailsHeight = y + 12;
            GUI.EndScrollView();
        }

        private float DrawReplacement(string type, RequirementPart part, bool needsManual, float y, float width)
        {
            y = Text(HelperLocalization.Text("Suitable replacement • ") + HelperLocalization.Text(type), y, width, labelStyle);
            if (needsManual) return Text(HelperLocalization.Text("Select this part manually to meet all additional job conditions."), y + 6, width, mutedStyle);
            if (part == null) return Text(HelperLocalization.Text(restrictByCareerLevel ? "No suitable alternatives at the current level." : "No suitable parts meet all these requirements."), y + 6, width, mutedStyle);
            y = Text(Clean(part.Name), y + 6, width, bodyStyle);
            string details = type == "CPU" ? SocketLabelText(part.Socket) : part.Vram + HelperLocalization.Text(" GB") + HelperLocalization.Text(" VRAM");
            return Text(details + " • $" + part.Price, y, width, mutedStyle);
        }

        private float Row(string label, string value, float y, float width)
        {
            bool enabled = GUI.enabled; GUI.enabled = true;
            float h = Mathf.Max(26, bodyStyle.CalcHeight(new GUIContent(value), width - 156));
            GUI.Label(new Rect(18, y, 118, h), label, mutedStyle);
            GUI.Label(new Rect(140, y, width - 158, h), value, bodyStyle);
            GUI.enabled = enabled;
            return y + h + 4;
        }

        private float Text(string text, float y, float width, GUIStyle style)
        {
            bool enabled = GUI.enabled; GUI.enabled = true;
            float h = style.CalcHeight(new GUIContent(text), width - 36);
            GUI.Label(new Rect(18, y, width - 36, h), text, style);
            GUI.enabled = enabled;
            return y + h;
        }

        private void EnsureStyles()
        {
            if (bodyStyle != null) return;
            theme.Ensure();
            bodyStyle = theme.Body;
            mutedStyle = theme.Muted;
            labelStyle = new GUIStyle(bodyStyle); labelStyle.fontStyle = FontStyle.Bold;
            titleStyle = theme.Title;
            buttonStyle = theme.Button;
            background = theme.Background;
        }

        internal static string TypeLabel(ProgramRequirementsDesc.Type type)
        { return HelperLocalization.Text(type == ProgramRequirementsDesc.Type.rec ? "Recommended" : type == ProgramRequirementsDesc.Type.min ? "Minimum" : "4K"); }
        private static string Clean(string value) { return Regex.Replace(value ?? "", "<[^>]+>", ""); }
        private static string SocketLabel(CpuSocket socket)
        { return SocketLabelText(socket.ToString()); }
        private static string SocketLabelText(string socket)
        {
            return (socket ?? "").Replace("LGA_", "LGA ").Replace("_COFFEE", " (Coffee Lake)")
                .Replace("_KABY", " (Kaby Lake)").Replace("_SKY", " (Skylake)").Replace("_PLUS", "+").Replace("_V3", "-v3");
        }
        public void Dispose() { placement.CancelDrag(); theme.Dispose(); }
    }
}
