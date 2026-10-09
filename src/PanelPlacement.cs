using BepInEx.Configuration;
using UnityEngine;

namespace PCBSCalculatorHelper
{
    internal sealed class PanelPlacement
    {
        private readonly ConfigEntry<string> setting;
        private readonly int controlHint;
        private int dragControl;
        private Vector2 grabOffset;
        private WindowPoint current;
        private bool moved;

        public PanelPlacement(ConfigFile config, string name)
        {
            setting = config.Bind("Layout", name + "Position", "default",
                "Saved panel position. Drag its header to move it. Set to default to reset. Relative to screen size.");
            controlHint = ("PCBSHelper.Position." + name).GetHashCode();
        }

        public bool IsDragging { get; private set; }

        public Rect Resolve(Rect defaultRect)
        {
            WindowPoint point;
            if (IsDragging) point = current;
            else if (WindowPositionMath.TryParse(setting.Value, out point))
                point = WindowPositionMath.Resolve(point, Screen.width, Screen.height, defaultRect.width, defaultRect.height);
            else point = new WindowPoint(defaultRect.x, defaultRect.y);
            point = WindowPositionMath.Clamp(point, Screen.width, Screen.height, defaultRect.width, defaultRect.height);
            defaultRect.x = Mathf.Round(point.X); defaultRect.y = Mathf.Round(point.Y);
            return defaultRect;
        }

        // Call in screen coordinates before applying a panel's GUI.matrix scale/translation.
        public bool HandleDrag(ref Rect rect, float headerHeight, bool interactive)
        {
            int control = GUIUtility.GetControlID(controlHint, FocusType.Passive);
            Event ev = Event.current;
            EventType kind = ev.GetTypeForControl(control);
            if (kind == EventType.MouseDown && ev.button == 0 && interactive && GUIUtility.hotControl == 0 &&
                new Rect(rect.x, rect.y, rect.width, headerHeight).Contains(ev.mousePosition))
            {
                IsDragging = true; moved = false; dragControl = control;
                current = new WindowPoint(rect.x, rect.y);
                grabOffset = ev.mousePosition - new Vector2(rect.x, rect.y);
                GUIUtility.hotControl = control;
                ev.Use();
            }
            else if (IsDragging && GUIUtility.hotControl == dragControl && kind == EventType.MouseDrag)
            {
                WindowPoint next = WindowPositionMath.Clamp(new WindowPoint(ev.mousePosition.x - grabOffset.x,
                    ev.mousePosition.y - grabOffset.y), Screen.width, Screen.height, rect.width, rect.height);
                moved |= next.X != current.X || next.Y != current.Y;
                current = next;
                rect.x = Mathf.Round(next.X); rect.y = Mathf.Round(next.Y);
                ev.Use();
                return true;
            }
            else if (IsDragging && ev.rawType == EventType.MouseUp && ev.button == 0)
            {
                Finish(); ev.Use();
            }
            return false;
        }

        public void CancelDrag() { if (IsDragging) Finish(); }

        private void Finish()
        {
            if (GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0;
            IsDragging = false;
            if (moved) setting.Value = WindowPositionMath.Serialize(WindowPositionMath.Normalize(current, Screen.width, Screen.height));
            moved = false;
        }
    }
}
