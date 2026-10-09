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
        mode.Update(1, true, false, false);
        Check(!mode.Enabled, "Opening F2 starts with the cursor toggle off.");
        mode.Update(1, true, true, false);
        Check(mode.Enabled, "A single Alt press enables the F2 cursor.");
        for (int i = 0; i < 5; i++) mode.Update(1, true, false, false);
        Check(mode.Enabled, "Releasing or holding Alt does not reset the toggle.");
        mode.Update(1, true, true, false);
        Check(!mode.Enabled, "The next press disables the cursor.");
        mode.Update(1, true, true, false); mode.Update(2, true, false, false);
        Check(!mode.Enabled, "Switching from F2 to F3 releases cursor ownership.");
        mode.Update(2, true, true, false);
        Check(mode.Enabled, "F3 uses the same single-press toggle.");
        mode.Update(2, false, false, false);
        Check(!mode.Enabled, "Losing application focus releases the toggle.");
        mode.Update(2, true, false, false);
        Check(!mode.Enabled, "Returning focus does not unexpectedly capture the cursor.");
        mode.Update(2, true, true, false); mode.Update(0, true, false, false);
        Check(!mode.Enabled, "Closing or disabling the helper releases the toggle.");
        mode.Update(0, true, true, false);
        Check(!mode.Enabled, "Alt does nothing without a visible F2/F3 panel.");

        foreach (int panel in new int[] { 1, 2 })
        {
            mode.Update(panel, true, false, true);
            Check(!mode.Enabled, "Opening a helper with the game cursor active does not capture it.");
            mode.Update(panel, true, true, true);
            Check(!mode.Enabled, "Alt in the shop or PC assembly leaves game input available.");
            mode.Update(panel, true, true, true);
            Check(!mode.Enabled, "Repeated Alt presses never capture an existing game cursor.");
            mode.Update(panel, true, false, false);
            Check(!mode.Enabled, "Returning to character control does not capture automatically.");
            mode.Update(panel, true, true, false);
            Check(mode.Enabled, "Alt still captures the cursor during character control.");
            mode.Update(panel, true, false, false);
            Check(mode.Enabled, "The helper's own cursor does not count as an existing game cursor.");
            mode.Update(panel, true, false, true);
            Check(!mode.Enabled, "An available game cursor clears the helper toggle.");
            mode.Update(panel, true, true, true);
            Check(!mode.Enabled, "Alt cannot recapture after the game cursor becomes available.");
        }
        Console.WriteLine("Panel cursor mode: " + checks + " assertions passed.");
    }
}
