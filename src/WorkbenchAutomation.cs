using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PCBSCalculatorHelper
{
    internal sealed class WorkbenchAutomation : IDisposable
    {
        private static WorkbenchAutomation instance;
        internal readonly ConfigEntry<bool> ThermalPaste, RemoveDependencies, CableColour;
        private readonly ManualLogSource log;
        private readonly DependencyRemoval removal;
        private readonly CableColourReplacement cables;
        private readonly HelperTheme theme = new HelperTheme();
        private string notice;
        private float noticeUntil;
        internal static readonly FieldInfo CurrentState = AccessTools.Field(typeof(GameController), "m_currentState");
        internal static readonly FieldInfo WorkCase = AccessTools.Field(typeof(WorkingOnComputerStateBase), "m_case");
        internal static readonly FieldInfo WorkCamera = AccessTools.Field(typeof(WorkingOnComputerStateBase), "m_camera");
        internal static readonly FieldInfo WorkMode = AccessTools.Field(typeof(WorkingOnComputerState), "m_mode");
        internal static readonly FieldInfo Selected = AccessTools.Field(typeof(WorkingOnComputerStateBase), "m_selected");
        private static readonly FieldInfo InstallDirection = AccessTools.Field(typeof(InstallingPartState), "m_dir");
        private static readonly FieldInfo InstallPart = AccessTools.Field(typeof(InstallingPartState), "m_part");
        private static readonly FieldInfo InstallSlot = AccessTools.Field(typeof(InstallingPartState), "m_slot");
        private static readonly FieldInfo AutoBuild = AccessTools.Field(typeof(InstallingPartState), "m_auto");
        private static readonly FieldInfo AutoStandOff = AccessTools.Field(typeof(InstallingPartState), "m_autoStandoff");
        private static readonly FieldInfo OneClickScrew = AccessTools.Field(typeof(InstallingPartState), "m_oneClickScrew");

        internal WorkbenchAutomation(ConfigFile config, ManualLogSource log)
        {
            this.log = log; instance = this;
            ThermalPaste = config.Bind("Workbench", "AutoThermalPaste", false,
                "Apply thermal paste when installing a CPU; reapply when installing a CPU air cooler, AIO or water block.");
            RemoveDependencies = config.Bind("Workbench", "RemoveDependencies", false,
                "Hold left mouse on a blocked part to remove its dependencies with normal animations. Release to pause; Esc cancels.");
            CableColour = config.Bind("Workbench", "ReplaceCableColour", false,
                "Selecting a cable in a PC's inventory replaces connected player-customizable cables with that style.");
            removal = new DependencyRemoval(this);
            cables = new CableColourReplacement(this);
        }

        internal static State StateNow
        { get { GameController ctrl = GameController.Get(); return ctrl == null ? null : CurrentState.GetValue(ctrl) as State; } }
        internal static bool Held
        { get { return Application.isFocused && !CalculatorPlugin.BlockGameInput && Input.GetMouseButton(0) &&
            !Input.GetKey(KeyCode.Escape) && PCBSInput.m_action.IsHeld() &&
            !PCBSInput.m_cameraRotate.IsHeld() && !PCBSInput.m_cameraPan.IsHeld(); } }

        internal void Install(Harmony harmony)
        {
            Hook(harmony, typeof(InstallingPartState), "OnStateEnter", null, "OnInstallEntered");
            Hook(harmony, typeof(InstallingPartState), "Tick", "BeforeInstallTick", null);
            Hook(harmony, typeof(InstallingPartState), "DoScrewsStep", "BeforeScrews", null);
            Hook(harmony, typeof(InstallingPartState), "HandleExit", "BeforeInstallExit", null);
            Hook(harmony, typeof(WorkingOnComputerState), "DisassemblyTick", "BeforeDisassembly", null);
            Hook(harmony, typeof(WorkingOnComputerState), "HandleInput", "BeforeWorkInput", null);
            Hook(harmony, typeof(WorkingOnComputerState), "HandleExit", "BeforeWorkExit", null);
            Hook(harmony, typeof(CableInstance), "Update", "BeforeCableAnimation", null);
            Hook(harmony, typeof(Pin), "PinAnimation", null, "AfterPinAnimation");
            Hook(harmony, typeof(AM3Slot), "OpenSocket", null, "AfterPinAnimation");
            Hook(harmony, typeof(AM3Slot), "CloseSocket", null, "AfterPinAnimation");
            Hook(harmony, typeof(LGASlot), "Update", "BeforeSocketAnimation", null);
            Hook(harmony, typeof(GameController), "SetCableType", null, "OnCableSelected");
        }

        private static void Hook(Harmony harmony, Type type, string method, string prefix, string postfix)
        {
            MethodInfo original = AccessTools.Method(type, method);
            if (original == null) throw new MissingMethodException(type.Name, method);
            harmony.Patch(original, prefix == null ? null : new HarmonyMethod(typeof(WorkbenchAutomation), prefix),
                postfix == null ? null : new HarmonyMethod(typeof(WorkbenchAutomation), postfix), null, null, null);
        }

        private static void OnInstallEntered(InstallingPartState __instance)
        {
            if (instance == null || !instance.ThermalPaste.Value || (float)InstallDirection.GetValue(__instance) <= 0) return;
            try
            {
                PartInstance part = InstallPart.GetValue(__instance) as PartInstance;
                Slot slot = InstallSlot.GetValue(__instance) as Slot;
                Case pc = WorkCase.GetValue(__instance) as Case;
                if (part == null || slot == null || pc == null) return;
                PartDesc.Type type = part.GetPart().m_type;
                if (!IsThermalPastePart(type)) return;
                Motherboard board = pc.GetMotherboard();
                // AIOs are installed in a case/radiator slot and only proxy the
                // CPU cooler slot. Use the part type, not the installation slot.
                ComponentPC component = board == null || board.cpuSlot == null ? null : board.cpuSlot.GetComponent();
                CPU cpu = component == null || component.m_isPlaceholder ? null : component.GetComponent<CPU>();
                if (cpu == null || cpu.IsThermalPasteApplied()) return;
                cpu.SetThermalPasteState(true);
                pc.UpdateGameStatus();
                instance.log.LogInfo("Auto thermal paste applied: " + component.GetPartInstance().GetPartId() +
                    " while installing " + part.GetPartId() + " in " + slot.m_slotType + ".");
            }
            catch (Exception ex) { instance.Warn("Auto thermal paste", ex); }
        }

        internal static bool IsThermalPastePart(PartDesc.Type type)
        { return type == PartDesc.Type.CPU || type == PartDesc.Type.AIR_COOLER ||
            type == PartDesc.Type.LIQUID_COOLER || type == PartDesc.Type.CPU_BLOCK; }

        private static bool BeforeDisassembly(WorkingOnComputerState __instance)
        {
            if (instance == null || !instance.RemoveDependencies.Value || !Held || !PCBSInput.m_action.IsDown() ||
                (WorkingOnComputerState.Mode)WorkMode.GetValue(__instance) != WorkingOnComputerState.Mode.DISASSEMBLY) return true;
            try { return !instance.removal.Begin(__instance, Selected.GetValue(__instance) as ComponentPC); }
            catch (Exception ex) { instance.removal.Cancel(); instance.Warn("Dependency removal", ex); return true; }
        }
        private static bool BeforeWorkInput(WorkingOnComputerState __instance)
        {
            if (instance == null) return true;
            try { return instance.removal.WorkTick(__instance); }
            catch (Exception ex) { instance.removal.Cancel(); instance.Warn("Dependency removal", ex); return true; }
        }
        private static bool BeforeInstallTick(InstallingPartState __instance)
        { return instance == null || !instance.removal.Owns(__instance) || Held; }
        private static bool BeforeScrews(InstallingPartState __instance, List<Screw> __0, List<Screw> __1)
        {
            if (instance == null || !instance.removal.Owns(__instance)) return true;
            if (!Held) return false;
            // Match the native upgrades instead of slowing one-click/automatic screws.
            if (__0.Count == 0) return true;
            Screw screw = __0[0];
            float speed = WorkbenchRules.ScrewSpeed(screw.m_isStandOff, (bool)AutoBuild.GetValue(__instance),
                (bool)AutoStandOff.GetValue(__instance), (bool)OneClickScrew.GetValue(__instance));
            screw.SetOutline(cakeslice.CustomOutline.REMOVABLE);
            if (screw.Tighten(-Time.deltaTime * speed))
            { __0.RemoveAt(0); __1.Add(screw); screw.SetOutline(cakeslice.CustomOutline.OFF); }
            return __0.Count == 0; // Native code performs the transition to the next animation.
        }
        private static void BeforeInstallExit(InstallingPartState __instance)
        { if (instance != null && instance.removal.Owns(__instance)) instance.removal.Cancel(); }
        private static bool BeforeWorkExit(WorkingOnComputerState __instance, ref bool __result)
        {
            if (instance == null || !instance.removal.Owns(__instance)) return true;
            instance.removal.Cancel(); __result = true; return false;
        }
        private static bool BeforeCableAnimation(CableInstance __instance)
        { return instance == null || !instance.removal.Owns(__instance) || Held; }
        private static bool BeforeSocketAnimation(LGASlot __instance)
        { return instance == null || !instance.removal.Owns(__instance) || Held; }
        private static void AfterPinAnimation(CPin __instance, ref IEnumerator __result)
        {
            if (instance != null && instance.removal.Owns(__instance))
                __result = HoldPinAnimation(__instance, __result);
        }
        private static IEnumerator HoldPinAnimation(CPin pin, IEnumerator animation)
        {
            while (true)
            {
                if (instance != null && instance.removal.Owns(pin) && !Held) { yield return null; continue; }
                if (!animation.MoveNext()) yield break;
                yield return animation.Current;
            }
        }
        private static void OnCableSelected(PartDesc __0)
        {
            if (instance == null || !instance.CableColour.Value || __0 == null || __0.m_type != PartDesc.Type.CABLE) return;
            try
            {
                WorkingOnComputerState state = StateNow as WorkingOnComputerState;
                if (state != null) instance.cables.Apply(state, __0);
            }
            catch (Exception ex) { instance.Warn("Cable replacement", ex); }
        }

        internal void Update()
        {
            try { removal.CheckContext(); }
            catch (Exception ex) { removal.Cancel(); Warn("Workbench helper", ex); }
        }
        internal void Notify(string english) { notice = english; noticeUntil = Time.unscaledTime + 4; }
        internal void LogRemoval(string message) { log.LogInfo("Dependency removal: " + message); }
        internal void LogRemovalBlocked(string target, List<string> trace)
        { log.LogWarning("Dependency removal stopped for " + target + "\n  " + String.Join("\n  ", trace.ToArray())); }
        internal void Warn(string action, Exception ex) { log.LogWarning(action + ": " + ex); Notify("Workbench action stopped. See the helper log."); }
        internal void DrawNotice()
        {
            string text = removal.Active ? "Hold LMB to remove dependencies • release to pause • Esc to cancel" :
                Time.unscaledTime < noticeUntil ? notice : null;
            if (text == null) return;
            theme.Ensure();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.2f);
            Rect rect = new Rect((Screen.width - 650 * scale) / 2, Screen.height - 100 * scale, 650 * scale, 62 * scale);
            GUI.DrawTexture(rect, theme.Background);
            GUI.Label(new Rect(rect.x + 12, rect.y + 9, rect.width - 24, rect.height - 18), HelperLocalization.Text(text), theme.Body);
        }
        public void Dispose() { removal.Cancel(); theme.Dispose(); if (instance == this) instance = null; }
    }
}
