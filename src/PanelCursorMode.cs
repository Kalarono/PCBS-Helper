namespace PCBSCalculatorHelper
{
    // One toggle shared by F2/F3; panel changes and focus loss release it.
    internal sealed class PanelCursorMode
    {
        private int context;
        internal bool Enabled { get; private set; }
        internal void Update(int nextContext, bool focused, bool altPressed)
        {
            if (context != nextContext || !focused || nextContext == 0) Enabled = false;
            context = nextContext;
            if (focused && context != 0 && altPressed) Enabled = !Enabled;
        }
    }
}
