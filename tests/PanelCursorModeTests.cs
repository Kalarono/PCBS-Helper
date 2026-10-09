using System;
using PCBSCalculatorHelper;

internal static class PanelCursorModeTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Main()
    {
        PanelCursorMode mode = new PanelCursorMode();
        mode.Update(1, true, false);
        Check(!mode.Enabled, "Opening F2 starts with the cursor toggle off.");
        mode.Update(1, true, true);
        Check(mode.Enabled, "A single Alt press enables the F2 cursor.");
        for (int i = 0; i < 5; i++) mode.Update(1, true, false);
        Check(mode.Enabled, "Releasing or holding Alt does not reset the toggle.");
        mode.Update(1, true, true);
        Check(!mode.Enabled, "The next press disables the cursor.");
        mode.Update(1, true, true); mode.Update(2, true, false);
        Check(!mode.Enabled, "Switching from F2 to F3 releases cursor ownership.");
        mode.Update(2, true, true);
        Check(mode.Enabled, "F3 uses the same single-press toggle.");
        mode.Update(2, false, false);
        Check(!mode.Enabled, "Losing application focus releases the toggle.");
        mode.Update(2, true, false);
        Check(!mode.Enabled, "Returning focus does not unexpectedly capture the cursor.");
        mode.Update(2, true, true); mode.Update(0, true, false);
        Check(!mode.Enabled, "Closing or disabling the helper releases the toggle.");
        mode.Update(0, true, true);
        Check(!mode.Enabled, "Alt does nothing without a visible F2/F3 panel.");
        Console.WriteLine("Panel cursor mode: " + checks + " assertions passed.");
    }
}
