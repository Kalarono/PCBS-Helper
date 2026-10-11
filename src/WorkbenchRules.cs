namespace PCBSCalculatorHelper
{
    internal sealed class RemovalHold
    {
        private float elapsed;
        private bool started;
        internal bool Advance(bool held, float delta)
        {
            if (!held) { elapsed = 0; return false; }
            if (started) return true;
            elapsed += delta;
            if (elapsed < 0.3f) return false;
            started = true;
            return true;
        }
        internal void Reset() { elapsed = 0; started = false; }
    }

    internal static class WorkbenchRules
    {
        internal static float ScrewSpeed(bool standOff, bool autoBuild, bool autoStandOff, bool oneClickScrew)
        { return (standOff ? autoStandOff || oneClickScrew : autoBuild || oneClickScrew) ? 4f : 1f; }

        // An existing cable's history is not its current customization policy.
        internal static bool CanRecolour(bool alwaysDefault, bool nonInteractive, string fixedVisuals)
        { return !alwaysDefault && !nonInteractive && System.String.IsNullOrEmpty(fixedVisuals); }
    }
}
