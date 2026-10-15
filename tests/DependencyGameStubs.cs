using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// Deliberately small doubles for the planner's native API, not a Unity runtime.
#pragma warning disable 0649
namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public string name;
        private bool Missing { get { return Destroyed || (this is MonoBehaviour && ((MonoBehaviour)this).gameObject.Destroyed); } }
        public static bool operator ==(Object a, Object b)
        { return ReferenceEquals(a, b) || ((ReferenceEquals(a, null) || a.Missing) && (ReferenceEquals(b, null) || b.Missing)); }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return base.GetHashCode(); }
        public int GetInstanceID() { return GetHashCode(); }
        public static void DestroyImmediate(Object obj) { if (!ReferenceEquals(obj, null)) obj.Destroyed = true; }
    }
    public class GameObject : Object
    {
        internal MonoBehaviour Owner;
        internal Transform Transform;
        public T GetComponent<T>() where T : class { return Owner as T; }
    }
    public class Transform : Object
    {
        public Transform parent;
        internal GameObject Object;
        public int childCount;
        public T GetComponent<T>() where T : class { return Object.GetComponent<T>(); }
        public bool IsChildOf(Transform other) { return this == other || (parent != null && parent.IsChildOf(other)); }
    }
    public class MonoBehaviour : Object
    {
        public readonly GameObject gameObject;
        public Transform transform { get { return gameObject.Transform; } }
        public MonoBehaviour()
        { gameObject = new GameObject { Owner = this }; gameObject.Transform = new Transform { Object = gameObject }; }
        public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
        public T GetComponentInParent<T>() where T : class
        {
            for (Transform t = transform; t != null; t = t.parent)
            { T found = t.GetComponent<T>(); if (found != null) return found; }
            return null;
        }
        public T[] GetComponentsInChildren<T>() where T : class { T found = GetComponent<T>(); return found == null ? new T[0] : new[] { found }; }
    }
    public class Camera : MonoBehaviour { }
    public static class Time { public static float deltaTime; }
}
namespace HarmonyLib
{
    internal static class AccessTools
    {
        internal static FieldInfo Field(Type type, string name)
        { return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
        internal static MethodInfo Method(Type type, string name)
        { return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
    }
}
public interface IBlocker { bool IsBlocking(Slot slot); }
public class CBlocker : UnityEngine.MonoBehaviour, IBlocker
{ public virtual bool IsBlocking(Slot slot) { return false; } }
public class Slot : CBlocker
{
    public enum SlotType { PSU, RAM, CPU, PCI, STORAGE, MOTHERBOARD, CASE_PART_OPTIONAL, PCI_COVER, CASE_COOLER, RADIATOR }
    public SlotType m_slotType;
    public ComponentPC Component;
    public bool Fits = true, NativeBlocked;
    public List<CBlocker> blockedBy = new List<CBlocker>();
    public List<ComponentPC> m_dynamicBlockedBy = new List<ComponentPC>(), m_dynamicComponentCollidesWith = new List<ComponentPC>();
    public CPin[] closePins = new CPin[0];
    public bool blocked { get { return NativeBlocked || CurrentBlockers(false).Any() || m_dynamicBlockedBy.Any(x => x != null) || m_dynamicComponentCollidesWith.Any(x => x != null); } }
    public ComponentPC GetComponent() { return Component == null ? null : Component; }
    public bool CanFillWithCurrentItem() { return Fits && GameController.Get().selectedItem != null && (GetComponent() == null || Component.m_isPlaceholder); }
    public bool IsUninstallingPathBlocked() { return m_dynamicBlockedBy.Any(x => x != null); }
    public override bool IsBlocking(Slot slot) { return GetComponent() != null && !Component.m_isPlaceholder; }
    public IEnumerable<IBlocker> CurrentBlockers(bool excludeRadiatorBlockers, bool recurseProxies = true)
    {
        foreach (CBlocker blocker in blockedBy) if (blocker.IsBlocking(this)) yield return blocker;
        foreach (CPin pin in closePins) if (pin.IsOpen()) yield return pin;
    }
}
public class ComponentPC : UnityEngine.MonoBehaviour
{
    public bool m_isPlaceholder, m_isProxy, m_includesPipes;
    public ComponentPC m_proxy;
    public Slot[] subslots = new Slot[0];
    public Slot Slot;
    public PartInstance Part;
    public WaterLoop m_loop;
    public ComponentPC PreviousComponentInCombo, NextComponentInCombo;
    public string NextPartIdInCombo;
    public PowerConnector[] powerConnectors = new PowerConnector[0];
    public WaterConnector[] waterConnectors = new WaterConnector[0];
    public ComponentPC FirstComponentInCombo { get { return PreviousComponentInCombo == null ? this : PreviousComponentInCombo.FirstComponentInCombo; } }
    public ComponentPC LastComponentInCombo { get { return NextComponentInCombo == null ? this : NextComponentInCombo.LastComponentInCombo; } }
    public bool CanBeUninstalled() { return !m_isPlaceholder && !Slot.blocked && powerConnectors.All(x => !x.IsConnected()) && (m_loop == null || !m_loop.IsFull()); }
    public PartDesc GetPartDesc() { return new PartDesc { m_id = Part.Id, m_uiName = Part.Id }; }
    public PartInstance GetPartInstance() { return Part; }
    public ComponentPC GetDecoration() { return null; }
}
public class PartInstance
{
    public readonly string Id;
    public PartInstance(string id) { Id = id; }
    public string GetPartId() { return Id; }
}
public class PartDesc { public string m_id, m_uiName; }
public class CPin : CBlocker
{
    public bool Open, Animating;
    public bool IsOpen() { return Open; }
    public bool IsAnimating() { return Animating; }
    public void TogglePin() { if (!IsBlocked()) Open = !Open; }
    public virtual bool IsBlocked() { return false; }
    public override bool IsBlocking(Slot slot) { return !Open; }
}
public class Pin : CPin
{
    public CBlocker[] blockedBy = new CBlocker[0], m_BlockedByClosedPins = new CBlocker[0], m_BlockedByOpenPins = new CBlocker[0], m_ClosedBlockedByOpenPins = new CBlocker[0];
    public override bool IsBlocked() { return blockedBy.Any(x => x.IsBlocking(null)); }
}
public class AM3Slot : CPin { public CBlocker[] m_blockedBy; public override bool IsBlocked() { return false; } }
public class LGASlot : CPin { public CBlocker[] m_blockedBy; public override bool IsBlocked() { return false; } }
public class RAMSlot : CBlocker
{
    public UnityEngine.GameObject pin1, pin2;
    public override bool IsBlocking(Slot slot) { return !pin1.GetComponent<CPin>().IsOpen() || !pin2.GetComponent<CPin>().IsOpen(); }
}
public class WaterLoop { public bool Full; public bool IsFull() { return Full; } }
public class PowerConnector : CBlocker
{ public string id; public bool Connected; public bool IsConnected() { return Connected; } }
public class WaterConnector : UnityEngine.MonoBehaviour
{
    public bool IsConnected() { return false; }
    public bool AreConnectedComponentsInSameCombo() { return false; }
    public WaterPipe GetPipe() { return null; }
}
public class WaterPipe : CBlocker { public WaterConnector m_from, m_to; }
public class CableInstance : UnityEngine.MonoBehaviour
{
    public bool Uninstalling;
    public PowerConnector m_start, m_end;
    public void Uninstall() { UnityEngine.Object.DestroyImmediate(gameObject); }
}
public class Case : UnityEngine.MonoBehaviour
{
    public void OnChangePart(UnityEngine.GameObject obj) { }
    public void UpdateGameStatus() { }
    public CableInstance GetCableFromStart(PowerConnector connector) { return null; }
}
public class State { }
public class WorkingOnComputerStateBase : State
{
    public Case m_case;
    public UnityEngine.Camera m_camera = new UnityEngine.Camera();
}
public class WorkingOnComputerState : WorkingOnComputerStateBase
{
    public enum Mode { ASSEMBLY, COMBO_ASSEMBLY, DISASSEMBLY }
    public Mode m_mode = Mode.ASSEMBLY;
    public readonly List<Slot> Slots = new List<Slot>();
    private readonly List<Slot> m_pendingSlotsForPlaceholders = new List<Slot>();
    public int PendingCount { get { return m_pendingSlotsForPlaceholders.Count; } }
    public WorkingOnComputerState(Case pc) { m_case = pc; }
    public void SetDisassemblyMode() { GameController.Get().selectedItem = null; m_mode = Mode.DISASSEMBLY; }
    public void SetAssemblyMode() { m_mode = Mode.ASSEMBLY; }
    public void ClearPlaceholders()
    {
        m_pendingSlotsForPlaceholders.Clear();
        foreach (Slot slot in Slots) if (slot.GetComponent() != null && slot.Component.m_isPlaceholder)
            UnityEngine.Object.DestroyImmediate(slot.Component.gameObject);
    }
    public void PopulatePlaceholders(Slot[] slots) { m_pendingSlotsForPlaceholders.AddRange(slots.Where(x => x != null)); }
    public void TickPopulatePlaceholders()
    {
        Slot slot = m_pendingSlotsForPlaceholders[0]; m_pendingSlotsForPlaceholders.RemoveAt(0); Preview(slot);
    }
    private void Preview(Slot slot)
    {
        if (slot.CanFillWithCurrentItem())
        {
            ComponentPC preview = new ComponentPC { Slot = slot, Part = GameController.Get().selectedItem, m_isPlaceholder = true };
            preview.transform.parent = slot.transform; slot.Component = preview;
        }
    }
    public void Regenerate() { foreach (Slot slot in Slots) Preview(slot); }
}
public class InstallingPartState : WorkingOnComputerStateBase
{
    public Slot Slot;
    public PartInstance Part;
    public ComponentPC Component;
    public float Direction;
    public InstallingPartState(Case pc, UnityEngine.Camera camera, PartInstance part, Slot slot)
    {
        m_case = pc; m_camera = camera; Slot = slot; Part = part; Direction = 1;
        GameController.Get().Inventory.Remove(part); GameController.Get().selectedItem = null;
        Component = new ComponentPC { Part = part, Slot = slot }; Component.transform.parent = slot.transform; slot.Component = Component;
    }
    private InstallingPartState(Case pc, UnityEngine.Camera camera, ComponentPC component)
    { m_case = pc; m_camera = camera; Component = component; Slot = component.Slot; Part = component.Part; Direction = -1; }
    public static InstallingPartState UninstallState(Case pc, UnityEngine.Camera camera, ComponentPC component)
    { return new InstallingPartState(pc, camera, component); }
}
public class GameController
{
    public static GameController Instance;
    public static GameController Get() { return Instance; }
    public State Current;
    public WorkingOnComputerState Work;
    internal PCBSCalculatorHelper.DependencyRemoval Queue;
    public PartInstance selectedItem;
    public ComponentPC LastComponentInstalled, NextComponentToUninstall;
    public Stack<Slot> UninstalledSlots = new Stack<Slot>();
    public List<PartInstance> Inventory = new List<PartInstance>();
    public void SetCurrentState(State state)
    {
        Work.ClearPlaceholders(); Current = state;
        InstallingPartState install = state as InstallingPartState;
        if (install != null) Queue.OnInstallEntered(install);
    }
    public void Finish()
    {
        InstallingPartState state = (InstallingPartState)Current;
        if (state.Direction < 0)
        {
            Inventory.Add(state.Part); UnityEngine.Object.DestroyImmediate(state.Component.gameObject);
            LastComponentInstalled = null; NextComponentToUninstall = state.Component.PreviousComponentInCombo;
        }
        else
        {
            LastComponentInstalled = state.Component; NextComponentToUninstall = null;
            if (!String.IsNullOrEmpty(state.Component.NextPartIdInCombo)) selectedItem = new PartInstance(state.Component.NextPartIdInCombo);
        }
        Current = Work; Queue.BeforeWorkEntered(Work);
        Work.m_mode = selectedItem == null ? WorkingOnComputerState.Mode.DISASSEMBLY :
            LastComponentInstalled != null && !String.IsNullOrEmpty(LastComponentInstalled.NextPartIdInCombo) ?
                WorkingOnComputerState.Mode.COMBO_ASSEMBLY : WorkingOnComputerState.Mode.ASSEMBLY;
        if (selectedItem != null) Work.PopulatePlaceholders(Work.Slots.ToArray());
    }
}
public class FakeInput { public bool Down; public bool IsDown() { return Down; } }
public static class PCBSInput
{
    public static readonly FakeInput m_assemblyMode = new FakeInput(), m_disassemblyMode = new FakeInput(), m_cablingMode = new FakeInput(), m_pipingMode = new FakeInput(), m_inventory = new FakeInput();
    public static void Reset() { m_assemblyMode.Down = m_disassemblyMode.Down = m_cablingMode.Down = m_pipingMode.Down = m_inventory.Down = false; }
}
namespace PCBSCalculatorHelper
{
    internal sealed class FakeOption { internal bool Value = true; }
    internal sealed class WorkbenchAutomation
    {
        internal readonly FakeOption RemoveDependencies = new FakeOption();
        internal readonly List<string> Notices = new List<string>(), Log = new List<string>();
        internal static bool Held;
        internal static State StateNow { get { return GameController.Get().Current; } }
        internal static readonly FieldInfo WorkCase = HarmonyLib.AccessTools.Field(typeof(WorkingOnComputerStateBase), "m_case"),
            WorkCamera = HarmonyLib.AccessTools.Field(typeof(WorkingOnComputerStateBase), "m_camera"),
            WorkMode = HarmonyLib.AccessTools.Field(typeof(WorkingOnComputerState), "m_mode");
        internal void Notify(string value) { Notices.Add(value); }
        internal void LogRemoval(string value) { Log.Add(value); }
        internal void LogRemovalBlocked(string target, List<string> trace) { Log.Add(target); }
    }
}
