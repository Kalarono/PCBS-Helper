using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PCBSCalculatorHelper
{
    internal sealed class DependencyRemoval
    {
        private readonly WorkbenchAutomation owner;
        private WorkingOnComputerState work;
        private InstallingPartState installing;
        private ComponentPC target;
        private Case pc;
        private CableInstance cable;
        private CPin pin;
        private bool fullLoopBlocks;
        private readonly List<string> planTrace = new List<string>();
        private readonly RemovalHold hold = new RemovalHold();
        private static readonly FieldInfo Proxy = AccessTools.Field(typeof(ComponentPC), "m_proxy");
        private static readonly FieldInfo Subslots = AccessTools.Field(typeof(ComponentPC), "subslots");
        private static readonly FieldInfo DynamicBlockers = AccessTools.Field(typeof(Slot), "m_dynamicBlockedBy");
        private static readonly MethodInfo PinBlocked = AccessTools.Method(typeof(Pin), "IsBlocked");
        private static readonly MethodInfo AmdSocketBlocked = AccessTools.Method(typeof(AM3Slot), "IsBlocked");
        private static readonly MethodInfo IntelSocketBlocked = AccessTools.Method(typeof(LGASlot), "IsBlocked");
        private static readonly FieldInfo PinsBlockingClosed = AccessTools.Field(typeof(Pin), "m_BlockedByClosedPins");
        private static readonly MethodInfo DisassemblyMode = AccessTools.Method(typeof(WorkingOnComputerState), "SetDisassemblyMode");

        internal DependencyRemoval(WorkbenchAutomation owner) { this.owner = owner; }
        internal bool Active { get { return work != null; } }
        internal bool Owns(WorkingOnComputerState state) { return work == state; }
        internal bool Owns(InstallingPartState state) { return installing == state; }
        internal bool Owns(CableInstance item) { return cable != null && cable == item; }
        internal bool Owns(CPin item) { return pin != null && pin == item; }

        internal bool Begin(WorkingOnComputerState state, ComponentPC component)
        {
            component = Normalize(component);
            if (component == null || component.m_isPlaceholder || component.CanBeUninstalled()) return false;
            Cancel(); work = state; target = component;
            pc = WorkbenchAutomation.WorkCase.GetValue(state) as Case;
            if (pc != null) owner.LogRemoval("Started: " + Describe(target));
            return pc != null;
        }

        internal void CheckContext()
        {
            if (!Active) return;
            State current = WorkbenchAutomation.StateNow;
            if (pc == null || (current != work && current != installing)) Cancel();
            else if (current == work) installing = null;
        }

        internal bool WorkTick(WorkingOnComputerState state)
        {
            if (!Owns(state)) return true;
            if (PCBSInput.m_assemblyMode.IsDown() || PCBSInput.m_cablingMode.IsDown() || PCBSInput.m_pipingMode.IsDown() ||
                PCBSInput.m_inventory.IsDown()) { Cancel(); return true; }
            if (!owner.RemoveDependencies.Value) { Cancel(); return true; }
            if (target == null || target.m_isPlaceholder) { Cancel(); owner.Notify("Dependency removal complete."); return false; }
            if (!hold.Advance(WorkbenchAutomation.Held, Time.deltaTime)) return false;
            if (cable != null) return false;
            if (pin != null && pin.IsAnimating()) return false;
            if (pin != null) pin = null;
            fullLoopBlocks = false;
            planTrace.Clear();
            Node next = DependencyOrder.Next(new Node(target), Ready, Blockers, new NodeComparer());
            if (next == null)
            {
                Stop(); return false;
            }
            owner.LogRemoval("Next: " + Describe(next.Item) + (next.Item is CPin ? (next.Open ? " [open]" : " [close]") : ""));
            if (next.Item is ComponentPC)
            {
                ComponentPC component = (ComponentPC)next.Item;
                // The native check is authoritative, even after planning the step.
                if (!component.CanBeUninstalled()) return false;
                component = component.LastComponentInCombo;
                pc.OnChangePart(component.gameObject);
                installing = InstallingPartState.UninstallState(pc,
                    (Camera)WorkbenchAutomation.WorkCamera.GetValue(state), component);
                GameController.Get().SetCurrentState(installing);
            }
            else if (next.Item is CableInstance)
            {
                cable = (CableInstance)next.Item;
                // OnChangePart dereferences Plug.m_cable. Old plug references
                // must not prevent removal of a motherboard dependency.
                pc.OnChangePart(cable.gameObject);
                cable.Uninstall();
                // Wait for destruction, not just SetConnected(false), before the next action.
            }
            else if (next.Item is CPin)
            {
                pin = (CPin)next.Item;
                pin.TogglePin();
                if (!pin.IsAnimating() && pin.IsOpen() != next.Open)
                { Trace("Native TogglePin refused: " + Describe(pin)); Stop(); }
            }
            else if (next.Item is WaterPipe)
            {
                UnityEngine.Object.DestroyImmediate(((WaterPipe)next.Item).gameObject);
                pc.UpdateGameStatus();
            }
            return false;
        }

        private bool Ready(Node node)
        {
            ComponentPC component = node.Item as ComponentPC;
            if (component != null)
            {
                if (component.m_loop != null && component.m_loop.IsFull()) fullLoopBlocks = true;
                bool ready = !component.m_isPlaceholder && component.CanBeUninstalled();
                Trace(Describe(component) + " removable=" + ready + " fullLoop=" + (component.m_loop != null && component.m_loop.IsFull()));
                return ready;
            }
            CableInstance wire = node.Item as CableInstance;
            if (wire != null) return !wire.Uninstalling && wire.m_start != null && wire.m_end != null;
            CPin latch = node.Item as CPin;
            if (latch != null)
            {
                bool blocked = IsPinBlocked(latch);
                Trace(Describe(latch) + " open=" + latch.IsOpen() + " wantOpen=" + node.Open +
                    " animating=" + latch.IsAnimating() + " blocked=" + blocked);
                return !latch.IsAnimating() && latch.IsOpen() != node.Open && !blocked;
            }
            WaterPipe pipe = node.Item as WaterPipe;
            if (pipe == null) return false;
            bool full = FullLoop(pipe.m_from) || FullLoop(pipe.m_to);
            if (full) fullLoopBlocks = true;
            return !full;
        }

        private IEnumerable<Node> Blockers(Node node)
        {
            // RAMSlot is a compound CBlocker, not a Slot or a CPin.
            // Its two clips must both open before the RAM can be removed.
            RAMSlot ram = node.Item as RAMSlot;
            if (ram != null)
            {
                CPin first = ram.pin1 == null ? null : ram.pin1.GetComponent<CPin>();
                CPin second = ram.pin2 == null ? null : ram.pin2.GetComponent<CPin>();
                if (first != null && !first.IsOpen()) yield return new Node(first, true);
                if (second != null && !second.IsOpen()) yield return new Node(second, true);
                yield break;
            }
            ComponentPC component = node.Item as ComponentPC;
            if (component != null)
            {
                for (ComponentPC member = component.FirstComponentInCombo; member != null; member = member.NextComponentInCombo)
                {
                    if (member.Slot != null)
                    {
                        foreach (IBlocker blocker in member.Slot.CurrentBlockers(false))
                        {
                            Node dependency = FromBlocker(blocker, member.Slot);
                            if (dependency != null && dependency.Item != component) yield return dependency;
                        }
                        member.Slot.IsUninstallingPathBlocked();
                        List<ComponentPC> dynamic = DynamicBlockers.GetValue(member.Slot) as List<ComponentPC>;
                        if (dynamic != null) foreach (ComponentPC other in dynamic)
                        {
                            ComponentPC normalized = Normalize(other);
                            Trace("Path blocker for " + Describe(member.Slot) + ": " + Describe(normalized));
                            if (normalized != null && normalized != component) yield return new Node(normalized);
                        }
                    }
                    Slot[] children = Subslots.GetValue(member) as Slot[];
                    if (children != null) foreach (Slot slot in children)
                    {
                        ComponentPC child = Normalize(slot.GetComponent());
                        if (slot.transform.childCount > 0) Trace("Filled child slot " + Describe(slot) + ": " + Describe(child));
                        if (child != null && !child.m_isPlaceholder && child != component) yield return new Node(child);
                    }
                    foreach (PowerConnector connector in member.powerConnectors)
                    {
                        if (!connector.IsConnected()) continue;
                        CableInstance wire = pc.GetCableFromStart(connector);
                        Trace("Connected " + Describe(connector) + ": " + Describe(wire));
                        if (wire != null) yield return new Node(wire);
                    }
                    if (!member.m_includesPipes) foreach (WaterConnector connector in member.waterConnectors)
                    {
                        if (connector.IsConnected() && !connector.AreConnectedComponentsInSameCombo()) yield return new Node(connector.GetPipe());
                    }
                    ComponentPC decoration = Normalize(member.GetDecoration());
                    if (decoration != null && decoration != component) yield return new Node(decoration);
                }
            }
            CPin latch = node.Item as CPin;
            if (latch == null) yield break;
            // CPU socket mechanisms are CPins too, but have their own native
            // IsBlocked checks and cooler blockers. Do not attempt them early.
            CBlocker[] direct = PinBlockers(latch);
            if (direct != null) foreach (CBlocker blocker in direct)
            {
                if (blocker == null || !blocker.IsBlocking(null)) continue;
                Node dependency = FromBlocker(blocker, null);
                if (dependency != null) yield return dependency;
            }
            Pin native = latch as Pin;
            if (native == null) yield break;
            CBlocker[] closed = PinsBlockingClosed.GetValue(native) as CBlocker[];
            if (!latch.IsOpen() && closed != null) foreach (CBlocker blocker in closed)
                foreach (Pin other in blocker.GetComponentsInChildren<Pin>())
                    if (!other.IsOpen()) yield return new Node(other, true);
            CBlocker[] open = latch.IsOpen() ? native.m_BlockedByOpenPins : native.m_ClosedBlockedByOpenPins;
            if (open != null) foreach (CBlocker blocker in open)
                foreach (Pin other in blocker.GetComponentsInChildren<Pin>())
                    if (other.IsOpen()) yield return new Node(other, false);
        }

        private Node FromBlocker(IBlocker blocker, Slot slot)
        {
            if (blocker == null) return null;
            Trace("Blocker for " + Describe(slot) + ": " + Describe(blocker as UnityEngine.Object));
            RAMSlot ram = blocker as RAMSlot;
            if (ram != null) return new Node(ram);
            Slot otherSlot = blocker as Slot;
            if (otherSlot != null)
            {
                ComponentPC other = Normalize(otherSlot.GetComponent());
                return other == null || other.m_isPlaceholder ? null : new Node(other);
            }
            CPin latch = blocker as CPin;
            if (latch != null)
            {
                bool open = slot == null || Array.IndexOf(slot.closePins, latch) < 0;
                return new Node(latch, open);
            }
            PowerConnector connector = blocker as PowerConnector;
            if (connector != null) return new Node(pc.GetCableFromStart(connector));
            WaterPipe pipe = blocker as WaterPipe;
            if (pipe == null) Trace("Unhandled blocker: " + blocker.GetType().FullName + " " + Describe(blocker as UnityEngine.Object));
            return pipe == null ? null : new Node(pipe);
        }

        private static bool IsPinBlocked(CPin latch)
        {
            MethodInfo native = latch is Pin ? PinBlocked : latch is AM3Slot ? AmdSocketBlocked :
                latch is LGASlot ? IntelSocketBlocked : null;
            // Unknown latch types are not forced past their native restrictions.
            return native == null || (bool)native.Invoke(latch, null);
        }

        private static CBlocker[] PinBlockers(CPin latch)
        {
            Pin ordinary = latch as Pin;
            if (ordinary != null) return ordinary.blockedBy;
            AM3Slot amd = latch as AM3Slot;
            if (amd != null) return amd.m_blockedBy;
            LGASlot intel = latch as LGASlot;
            return intel == null ? null : intel.m_blockedBy;
        }

        private void Trace(string line)
        { if (planTrace.Count < 200) planTrace.Add(line); }

        private void Stop()
        {
            owner.LogRemovalBlocked(Describe(target), planTrace);
            string reason = fullLoopBlocks ? "Empty the full water loop before removing this dependency." :
                "Dependency removal stopped. See BepInEx/LogOutput.log.";
            Cancel(); owner.Notify(reason);
        }

        private static string Describe(UnityEngine.Object item)
        {
            if (item == null) return "<missing>";
            ComponentPC component = item as ComponentPC;
            PartDesc desc = component == null ? null : component.GetPartDesc();
            PowerConnector connector = item as PowerConnector;
            return item.GetType().Name + " '" + item.name + "'" +
                (desc == null ? "" : " [" + desc.m_id + ": " + desc.m_uiName + "]") +
                (connector == null ? "" : " [connector=" + connector.id + "]");
        }

        private static ComponentPC Normalize(ComponentPC component)
        {
            if (component == null) return null;
            if (component.m_isProxy) component = Proxy.GetValue(component) as ComponentPC;
            return component == null ? null : component.FirstComponentInCombo;
        }
        private static bool FullLoop(WaterConnector connector)
        {
            ComponentPC component = connector == null ? null : connector.GetComponentInParent<ComponentPC>();
            return component != null && component.m_loop != null && component.m_loop.IsFull();
        }
        internal void Cancel()
        {
            if (work != null && WorkbenchAutomation.StateNow == work)
            {
                // Native AIO combo continuation must not run unattended after cancellation.
                GameController.Get().NextComponentToUninstall = null;
                DisassemblyMode.Invoke(work, null);
            }
            work = null; installing = null; target = null; pc = null; cable = null; pin = null;
            hold.Reset();
            fullLoopBlocks = false;
        }
        private sealed class Node
        {
            internal readonly UnityEngine.Object Item;
            internal readonly bool Open;
            internal Node(UnityEngine.Object item) : this(item, false) { }
            internal Node(UnityEngine.Object item, bool open) { Item = item; Open = open; }
        }
        private sealed class NodeComparer : IEqualityComparer<Node>
        {
            public bool Equals(Node a, Node b) { return a.Item == b.Item && a.Open == b.Open; }
            public int GetHashCode(Node node) { return node.Item == null ? 0 : node.Item.GetInstanceID() ^ node.Open.GetHashCode(); }
        }
    }
}
