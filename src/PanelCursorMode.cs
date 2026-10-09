namespace PCBSCalculatorHelper
{
    // One toggle shared by F2/F3; an existing game cursor never needs capture.
    internal sealed class PanelCursorMode
    {
        private int context;
        internal bool Enabled { get; private set; }
        internal void Update(int nextContext, bool focused, bool altPressed, bool gameCursorAvailable)
        {
            if (context != nextContext || !focused || nextContext == 0 || gameCursorAvailable) Enabled = false;
            context = nextContext;
            if (focused && context != 0 && altPressed && !gameCursorAvailable) Enabled = !Enabled;
        }
    }
}
