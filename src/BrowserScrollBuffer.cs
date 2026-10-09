namespace PCBSCalculatorHelper
{
    // IMGUI wheel events arrive after Browser.Update; keep them until the next input snapshot.
    internal sealed class BrowserScrollBuffer
    {
        private float pendingX, pendingY;
        internal float X { get; private set; }
        internal float Y { get; private set; }
        internal void Enqueue(float guiX, float guiY)
        { pendingX -= guiX; pendingY -= guiY; }
        internal void Advance(bool focused)
        {
            X = focused ? pendingX : 0;
            Y = focused ? pendingY : 0;
            pendingX = pendingY = 0;
        }
        internal void Clear()
        { pendingX = pendingY = X = Y = 0; }
    }
}
