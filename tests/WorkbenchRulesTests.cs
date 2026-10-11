using System;
using PCBSCalculatorHelper;

internal static class WorkbenchRulesTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Main()
    {
        RemovalHold hold = new RemovalHold();
        Check(!hold.Advance(true, 0.2f), "A short click must not start dependency removal.");
        Check(!hold.Advance(false, 1f), "Releasing a short click clears its partial hold.");
        Check(!hold.Advance(true, 0.2f), "A new click must establish a fresh hold.");
        Check(hold.Advance(true, 0.11f), "A sustained click starts the queue.");
        Check(hold.Advance(true, 0.001f), "The next dependency has no extra startup delay.");
        Check(!hold.Advance(false, 0.1f), "Release pauses an active queue.");
        Check(hold.Advance(true, 0.001f), "Resume continues immediately.");
        hold.Reset();
        Check(!hold.Advance(true, 0.1f), "Cancel removes the queue's started state.");
        Check(WorkbenchRules.ScrewSpeed(false, false, false, false) == 1f, "Unupgraded screws retain native speed.");
        Check(WorkbenchRules.ScrewSpeed(false, true, false, false) == 4f, "Automatic assembly accelerates ordinary screws.");
        Check(WorkbenchRules.ScrewSpeed(true, true, false, false) == 1f, "Assembly alone does not accelerate standoffs.");
        Check(WorkbenchRules.ScrewSpeed(true, false, true, false) == 4f, "Automatic standoffs retain their native speed.");
        Check(WorkbenchRules.ScrewSpeed(false, false, true, false) == 1f, "Standoff upgrade does not accelerate ordinary screws.");
        Check(WorkbenchRules.ScrewSpeed(false, false, false, true) == 4f, "One-click screws retain their native animation speed.");
        Check(WorkbenchRules.ScrewSpeed(true, false, false, true) == 4f, "One-click also applies to standoffs.");
        Check(WorkbenchRules.CanRecolour(false, false, null), "Original customizable cables are eligible.");
        Check(WorkbenchRules.CanRecolour(false, false, ""), "An empty override does not fix a cable's style.");
        Check(!WorkbenchRules.CanRecolour(true, false, null), "Default-only cables cannot change colour.");
        Check(!WorkbenchRules.CanRecolour(false, true, null), "Noninteractive cables cannot change colour.");
        Check(!WorkbenchRules.CanRecolour(false, false, "PSU fixed wires"), "Nonmodular PSU wiring keeps its fixed visuals.");
        Console.WriteLine("Workbench behavior: " + checks + " assertions passed.");
    }
}
