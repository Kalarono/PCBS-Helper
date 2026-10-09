using UnityEngine;

namespace PCBSCalculatorHelper
{
    // Shared palette for the requirements HUD, Tweaker and calculator frame.
    internal sealed class HelperTheme
    {
        public Texture2D Background { get; private set; }
        public GUIStyle Title { get; private set; }
        public GUIStyle Body { get; private set; }
        public GUIStyle Muted { get; private set; }
        public GUIStyle Button { get; private set; }
        public GUIStyle Toggle { get; private set; }
        private Texture2D button, hover, active;

        public void Ensure()
        {
            if (Background != null) return;
            Background = Texture(new Color(0.07f, 0.09f, 0.13f, 0.97f));
            button = Texture(new Color(0.16f, 0.20f, 0.27f));
            hover = Texture(new Color(0.23f, 0.30f, 0.40f));
            active = Texture(new Color(0.12f, 0.31f, 0.51f));
            Body = new GUIStyle(GUI.skin.label); Body.fontSize = 15; Body.wordWrap = true;
            Body.richText = false; Body.normal.textColor = new Color(0.94f, 0.96f, 1);
            Muted = new GUIStyle(Body); Muted.fontSize = 13;
            Muted.normal.textColor = new Color(0.70f, 0.76f, 0.84f);
            Title = new GUIStyle(Body); Title.fontSize = 18; Title.fontStyle = FontStyle.Bold;
            Button = new GUIStyle(GUI.skin.button); Button.fontSize = 13; Button.wordWrap = true;
            Button.normal.background = button; Button.hover.background = hover; Button.active.background = active;
            Button.focused.background = hover;
            Button.normal.textColor = Button.hover.textColor = Button.active.textColor = Button.focused.textColor = Color.white;
            Toggle = new GUIStyle(Button); Toggle.fontSize = 15; Toggle.alignment = TextAnchor.MiddleLeft;
            Toggle.padding = new RectOffset(14, 14, 8, 8);
            Toggle.onNormal.background = active; Toggle.onHover.background = hover;
            Toggle.onActive.background = active; Toggle.onFocused.background = hover;
            Toggle.onNormal.textColor = Toggle.onHover.textColor = Toggle.onActive.textColor = Toggle.onFocused.textColor = Color.white;
        }

        private static Texture2D Texture(Color color)
        { Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture; }

        public void Dispose()
        {
            foreach (Texture2D texture in new Texture2D[] { Background, button, hover, active })
                if (texture != null) Object.Destroy(texture);
        }
    }
}
