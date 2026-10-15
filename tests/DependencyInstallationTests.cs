using System;
using System.Linq;
using PCBSCalculatorHelper;

// Runs the actual live planner against a small native-state/inventory double.
// Game API signatures and hook wiring are checked separately against the installed DLLs.
internal static class DependencyInstallationTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }

    private sealed class Rig
    {
        internal readonly WorkbenchAutomation Owner = new WorkbenchAutomation();
        internal readonly Case PC = new Case();
        internal readonly WorkingOnComputerState Work;
        internal readonly DependencyRemoval Queue;
        internal readonly GameController Ctrl = new GameController();
        internal Rig()
        {
            GameController.Instance = Ctrl;
            PCBSInput.Reset(); WorkbenchAutomation.Held = true;
            Work = new WorkingOnComputerState(PC);
            Queue = new DependencyRemoval(Owner);
            Ctrl.Work = Work; Ctrl.Queue = Queue; Ctrl.Current = Work;
        }
        internal Slot Slot(string name, Slot.SlotType type)
        {
            Slot slot = new Slot { name = name, m_slotType = type };
            slot.transform.parent = PC.transform; Work.Slots.Add(slot); return slot;
        }
        internal ComponentPC Part(Slot slot, string id)
        {
            ComponentPC part = new ComponentPC { Part = new PartInstance(id), Slot = slot, name = id };
            part.transform.parent = slot.transform; slot.Component = part; return part;
        }
        internal void Begin(Slot slot, string id)
        {
            Ctrl.selectedItem = new PartInstance(id); Ctrl.Inventory.Add(Ctrl.selectedItem);
            Work.Regenerate();
            Check(Queue.BeginInstall(Work, slot.GetComponent()), "Blocked compatible preview starts installation assistance.");
        }
        internal void Tick() { Queue.WorkTick(Work); }
        internal InstallingPartState Step()
        {
            Tick();
            InstallingPartState state = Ctrl.Current as InstallingPartState;
            Check(state != null, "The next action uses a native part state."); return state;
        }
        internal void Finish() { Ctrl.Finish(); Queue.CheckContext(); }
        internal void DrainPreviews() { while (Work.PendingCount > 0) Tick(); }
    }

    private static void Main()
    {
        RestorePartsInReverseOrder();
        SkipImpossibleRestoration();
        RestoreClipsAndSkipMissingSlots();
        OpenRamClipsAndPause();
        RespectNativeChecksAndCancel();
        CompleteNativeCombos();
        RetainRemovalBehaviour();
        Console.WriteLine("Dependency installation and restoration: " + checks + " assertions passed.");
    }

    private static void RestorePartsInReverseOrder()
    {
        Rig r = new Rig();
        Slot psu = r.Slot("PSU", Slot.SlotType.PSU), cover = r.Slot("shroud", Slot.SlotType.CASE_PART_OPTIONAL),
            panel = r.Slot("panel", Slot.SlotType.CASE_PART_OPTIONAL), pci = r.Slot("PCI blank", Slot.SlotType.PCI_COVER);
        ComponentPC oldPanel = r.Part(panel, "panel"), oldCover = r.Part(cover, "shroud");
        r.Part(pci, "PCI blank");
        psu.blockedBy.Add(cover); psu.blockedBy.Add(pci); cover.blockedBy.Add(panel);
        r.Begin(psu, "new PSU");
        PartInstance chosen = r.Ctrl.selectedItem;
        UnityEngine.Time.deltaTime = 0.2f; r.Tick();
        Check(r.Ctrl.Current == r.Work, "A short hold does not remove anything.");
        UnityEngine.Time.deltaTime = 0.11f;
        Check(r.Step().Component == oldPanel, "The outer panel is removed first."); r.Finish(); r.DrainPreviews();
        Check(r.Ctrl.selectedItem == chosen, "The exact chosen inventory instance survives a native removal.");
        Check(r.Step().Component == oldCover, "The nested shroud is removed next without another hold delay."); r.Finish(); r.DrainPreviews();
        Check(r.Step().Component.Part.Id == "PCI blank", "A blocking PCI blank can be removed."); r.Finish(); r.DrainPreviews();
        InstallingPartState install = r.Step();
        Check(install.Direction > 0 && install.Part == chosen && install.Slot == psu, "The requested part installs in the clicked slot.");
        Check(!r.Ctrl.Inventory.Contains(chosen), "Only the native installation consumes the chosen item.");
        WorkbenchAutomation.Held = false;
        Check(r.Queue.Owns(install), "The final installation is owned so its animation can pause.");
        WorkbenchAutomation.Held = true; r.Finish(); r.DrainPreviews();
        install = r.Step();
        Check(install.Direction > 0 && install.Part == oldCover.Part && install.Slot == cover,
            "The original shroud inventory instance returns before the outer panel.");
        r.Finish(); r.DrainPreviews();
        install = r.Step();
        Check(install.Part == oldPanel.Part && install.Slot == panel, "The outer panel returns to its original slot."); r.Finish();
        Check(!r.Queue.Active, "The queue finishes after restoring the last part.");
        Check(r.Ctrl.Inventory.Count(x => x.Id == "PCI blank") == 1 && pci.GetComponent() == null, "PCI blanks stay in inventory.");
        Check(r.Ctrl.Inventory.Count == 1, "Restoration neither duplicates parts nor loses the PCI blank.");
        Check(r.Owner.Notices.Count == 0, "Installation and restoration produce no bottom notices.");
    }

    private static void SkipImpossibleRestoration()
    {
        Rig r = new Rig();
        Slot gpu = r.Slot("GPU", Slot.SlotType.PCI), cover = r.Slot("cover", Slot.SlotType.CASE_PART_OPTIONAL),
            panel = r.Slot("panel", Slot.SlotType.CASE_PART_OPTIONAL);
        ComponentPC oldCover = r.Part(cover, "cover"); r.Part(panel, "panel");
        gpu.blockedBy.Add(cover); cover.blockedBy.Add(panel);
        r.Begin(gpu, "wide GPU"); UnityEngine.Time.deltaTime = 0.31f;
        r.Step(); r.Finish(); r.DrainPreviews(); r.Step(); r.Finish(); r.DrainPreviews();
        r.Step(); r.Finish(); r.DrainPreviews();
        // The installed GPU prevents this cover returning. Do not remove it again.
        cover.blockedBy.Add(gpu);
        r.Tick(); r.DrainPreviews();
        Check(r.Ctrl.Current == r.Work && r.Ctrl.Inventory.Contains(oldCover.Part), "A blocked cover stays in inventory.");
        Check(gpu.GetComponent() != null && !gpu.GetComponent().m_isPlaceholder, "Restoration never uninstalls the new GPU.");
        Check(r.Step().Slot == panel, "A skipped cover does not prevent restoring the remaining panel."); r.Finish();
        Check(!r.Queue.Active && r.Owner.Notices.Count == 0, "Partial restoration finishes silently.");
    }

    private static void OpenRamClipsAndPause()
    {
        Rig r = new Rig(); Slot ram = r.Slot("RAM", Slot.SlotType.RAM);
        Pin top = new Pin(), bottom = new Pin();
        ram.blockedBy.Add(new RAMSlot { pin1 = top.gameObject, pin2 = bottom.gameObject });
        r.Begin(ram, "RAM stick"); UnityEngine.Time.deltaTime = 0.31f;
        r.Tick(); Check(top.IsOpen() && !bottom.IsOpen(), "The first RAM clip opens before installation.");
        WorkbenchAutomation.Held = false; r.Tick();
        Check(!bottom.IsOpen() && r.Ctrl.Current == r.Work, "Release pauses the queue between clips.");
        WorkbenchAutomation.Held = true; UnityEngine.Time.deltaTime = 0.001f; r.Tick();
        Check(bottom.IsOpen(), "Resume immediately opens the second clip.");
        Check(r.Step().Slot == ram, "RAM installs after both clips have opened."); r.Finish();
        Check(!r.Queue.Active && r.Owner.Notices.Count == 0, "A pin-only installation finishes silently.");
    }

    private static void RestoreClipsAndSkipMissingSlots()
    {
        Rig r = new Rig(); Slot psu = r.Slot("PSU", Slot.SlotType.PSU), cover = r.Slot("cover", Slot.SlotType.CASE_PART_OPTIONAL),
            panel = r.Slot("panel", Slot.SlotType.CASE_PART_OPTIONAL);
        r.Part(panel, "panel"); r.Part(cover, "cover");
        Pin latch = new Pin(); cover.closePins = new CPin[] { latch };
        psu.blockedBy.Add(cover); psu.blockedBy.Add(latch); cover.blockedBy.Add(panel);
        r.Begin(psu, "PSU"); UnityEngine.Time.deltaTime = 0.31f;
        r.Step(); r.Finish(); r.DrainPreviews(); r.Step(); r.Finish(); r.DrainPreviews();
        r.Tick(); Check(latch.Open, "Installation opens the case latch after removing the cover.");
        r.Step();
        UnityEngine.Object.DestroyImmediate(panel.gameObject);
        r.Finish(); r.DrainPreviews();
        r.Tick(); Check(!latch.Open, "Restoration can close the latch required by the original cover slot.");
        Check(r.Step().Slot == cover, "The cover is restored after its latch reaches the required state."); r.Finish();
        Check(!r.Queue.Active && r.Ctrl.Inventory.Any(x => x.Id == "panel"), "A missing restoration destination is skipped without losing its part.");
        Check(r.Owner.Notices.Count == 0, "Skipped destinations and latch restoration remain silent.");
    }

    private static void RespectNativeChecksAndCancel()
    {
        Rig r = new Rig(); Slot target = r.Slot("SSD", Slot.SlotType.STORAGE), obstruction = r.Slot("tray", Slot.SlotType.CASE_PART_OPTIONAL);
        r.Part(obstruction, "tray");
        target.m_dynamicComponentCollidesWith.Add(obstruction.GetComponent());
        r.Begin(target, "SSD"); UnityEngine.Time.deltaTime = 0.31f;
        Check(r.Step().Slot == obstruction, "Installation resolves native component collisions as well as static blockers.");
        r.Finish(); target.m_dynamicComponentCollidesWith.Clear(); r.DrainPreviews();
        r.Owner.RemoveDependencies.Value = false; r.Tick();
        Check(!r.Queue.Active && target.GetComponent().m_isPlaceholder, "Disabling the option cancels before inventory consumption.");
        Check(r.Ctrl.Inventory.Any(x => x.Id == "SSD"), "Cancellation retains the uninstalled item.");

        r = new Rig(); target = r.Slot("CPU", Slot.SlotType.CPU);
        Slot boardSlot = r.Slot("board", Slot.SlotType.MOTHERBOARD);
        ComponentPC board = r.Part(boardSlot, "board"); target.transform.parent = board.transform;
        target.blockedBy.Add(boardSlot);
        r.Begin(target, "CPU"); r.Tick();
        Check(!r.Queue.Active && boardSlot.GetComponent() == board, "A destination's parent board is never removed.");
        Check(r.Owner.Notices.Count == 0, "Even an unresolved installation stops without a bottom message.");

        r = new Rig(); target = r.Slot("HDD", Slot.SlotType.STORAGE); target.NativeBlocked = true;
        r.Begin(target, "HDD"); target.Fits = false; r.Tick();
        Check(!r.Queue.Active && r.Ctrl.Inventory.Count == 1, "Changed compatibility is rechecked before installation.");

        r = new Rig(); target = r.Slot("PSU", Slot.SlotType.PSU); target.NativeBlocked = true;
        r.Begin(target, "PSU"); PCBSInput.m_disassemblyMode.Down = true; r.Tick();
        Check(!r.Queue.Active, "Changing mode cancels the installation queue.");

        r = new Rig(); target = r.Slot("CPU", Slot.SlotType.CPU); obstruction = r.Slot("water block", Slot.SlotType.CASE_COOLER);
        r.Part(obstruction, "water block").m_loop = new WaterLoop { Full = true }; target.blockedBy.Add(obstruction);
        r.Begin(target, "CPU"); r.Tick();
        Check(!r.Queue.Active && obstruction.GetComponent() != null && r.Owner.Notices.Count == 0,
            "A full loop is not dismantled and produces no installation HUD message.");

        r = new Rig(); target = r.Slot("PSU", Slot.SlotType.PSU); target.NativeBlocked = true;
        r.Begin(target, "PSU"); r.Ctrl.Current = new State(); r.Queue.CheckContext();
        Check(!r.Queue.Active && r.Ctrl.Inventory.Count == 1, "Leaving the work context cancels without consuming the chosen item.");

        r = new Rig(); target = r.Slot("RAM", Slot.SlotType.RAM);
        r.Ctrl.selectedItem = new PartInstance("RAM"); r.Work.Regenerate();
        Check(!r.Queue.BeginInstall(r.Work, target.GetComponent()), "An unblocked slot keeps the native installation flow.");
        target.NativeBlocked = true; target.GetComponent().m_isPlaceholder = false;
        Check(!r.Queue.BeginInstall(r.Work, target.GetComponent()), "An occupied slot never starts assisted replacement.");
    }

    private static void CompleteNativeCombos()
    {
        Rig r = new Rig(); Slot aio = r.Slot("AIO", Slot.SlotType.CASE_COOLER), cover = r.Slot("cover", Slot.SlotType.CASE_PART_OPTIONAL),
            radiator = r.Slot("radiator", Slot.SlotType.RADIATOR);
        r.Part(cover, "cover"); aio.blockedBy.Add(cover); r.Begin(aio, "AIO pump"); UnityEngine.Time.deltaTime = 0.31f;
        r.Step(); r.Finish(); r.DrainPreviews();
        InstallingPartState pump = r.Step(); pump.Component.NextPartIdInCombo = "AIO radiator";
        r.Finish();
        Check(r.Queue.Active && r.Queue.NativeComboPending && r.Ctrl.selectedItem.Id == "AIO radiator",
            "A native combo continues before restoration starts.");
        WorkbenchAutomation.Held = false;
        Check(!r.Queue.WorkTick(r.Work), "A pending combo cannot run while LMB is released.");
        WorkbenchAutomation.Held = true;
        InstallingPartState next = new InstallingPartState(r.PC, new UnityEngine.Camera(), r.Ctrl.selectedItem, radiator);
        r.Ctrl.SetCurrentState(next);
        Check(r.Queue.Owns(next), "The native combo's next installation inherits hold ownership.");
        r.Finish(); r.DrainPreviews();
        Check(r.Step().Slot == cover, "Restoration begins after the last combo component."); r.Finish();
        Check(!r.Queue.Active, "The completed combo and its cover leave no active queue.");
    }

    private static void RetainRemovalBehaviour()
    {
        Rig r = new Rig(); Slot psu = r.Slot("PSU", Slot.SlotType.PSU), cover = r.Slot("cover", Slot.SlotType.CASE_PART_OPTIONAL);
        ComponentPC part = r.Part(psu, "PSU"); r.Part(cover, "cover"); psu.blockedBy.Add(cover);
        Check(r.Queue.Begin(r.Work, part), "Blocked removal still starts."); UnityEngine.Time.deltaTime = 0.31f;
        r.Step(); r.Finish(); r.Step(); r.Finish(); r.Tick();
        Check(!r.Queue.Active && r.Ctrl.Inventory.Count == 2, "Removal still removes the target and leaves dependencies in inventory.");
        Check(r.Owner.Notices.Count == 1, "Removal retains its existing completion notice.");
    }
}
