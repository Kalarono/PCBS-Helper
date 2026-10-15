using System.Collections.Generic;
using UnityEngine;

namespace PCBSCalculatorHelper
{
    // F1's approved "Soft rows" theme is independent of the F2/F3 palette.
    internal sealed class TweakerTheme
    {
        internal GUIStyle Window, Footer, Row, Title, Muted, StatusOn, StatusOff, Keycap, Button, Close, Logo, CheckOn, CheckOff;
        internal readonly Color Accent = new Color(176 / 255f, 191 / 255f, 1);
        private readonly List<Texture2D> textures = new List<Texture2D>();

        internal void Ensure()
        {
            if (Window != null) return;
            Color panel = RGB(0x171c29), line = RGB(0x333b52), text = RGB(0xedf2f6), muted = RGB(0x9aaaba);
            Window = Surface(panel, line, 18);
            Footer = Surface(RGB(0x121725), RGB(0x121725), 18, true);
            Row = new GUIStyle(GUI.skin.button);
            Row.border = new RectOffset(8, 8, 8, 8);
            Row.padding = new RectOffset(40, 78, 8, 8);
            Row.fontSize = 15; Row.wordWrap = true; Row.alignment = TextAnchor.MiddleLeft;
            Row.normal.background = Rounded(RGB(0x22293a), RGB(0x22293a), 8);
            Row.hover.background = Rounded(RGB(0x283044), RGB(0x657195), 8);
            Row.active.background = Rounded(RGB(0x303a54), Accent, 8);
            Row.focused.background = Row.hover.background;
            Row.onNormal.background = Rounded(RGB(0x293148), RGB(0x293148), 8);
            Row.onHover.background = Rounded(RGB(0x303950), RGB(0x657195), 8);
            Row.onActive.background = Row.active.background; Row.onFocused.background = Row.onHover.background;
            foreach (GUIStyleState state in new[] { Row.normal, Row.hover, Row.active, Row.focused,
                Row.onNormal, Row.onHover, Row.onActive, Row.onFocused }) state.textColor = text;
            Title = Label(18, text, TextAnchor.MiddleLeft); Title.fontStyle = FontStyle.Bold;
            Muted = Label(12, muted, TextAnchor.MiddleLeft);
            StatusOn = Label(11, Accent, TextAnchor.MiddleRight); StatusOn.fontStyle = FontStyle.Bold;
            StatusOff = Label(11, muted, TextAnchor.MiddleRight); StatusOff.fontStyle = FontStyle.Bold;
            Keycap = Surface(RGB(0x242d40), line, 4); Keycap.fontSize = 11;
            Keycap.alignment = TextAnchor.MiddleCenter; Keycap.normal.textColor = muted;
            Button = new GUIStyle(GUI.skin.button); Button.fontSize = 13;
            Button.border = new RectOffset(7, 7, 7, 7); Button.padding = new RectOffset(12, 12, 5, 5);
            Button.normal.background = Rounded(RGB(0x202b37), line, 7);
            Button.hover.background = Rounded(RGB(0x2b3948), RGB(0x657195), 7);
            Button.active.background = Rounded(RGB(0x35435b), Accent, 7); Button.focused.background = Button.hover.background;
            Button.normal.textColor = Button.hover.textColor = Button.active.textColor = Button.focused.textColor = text;
            Close = new GUIStyle(Button); Close.fontSize = 20; Close.padding = new RectOffset();
            Close.normal.background = null; Close.normal.textColor = muted;
            Logo = Surface(RGB(0x272e42), RGB(0x272e42), 10);
            CheckOn = Surface(Accent, Accent, 3);
            CheckOff = Surface(RGB(0x1f2634), RGB(0x8a93a7), 3);
        }

        private static Color RGB(int hex)
        { return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f); }

        private static GUIStyle Label(int size, Color color, TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = size; style.normal.textColor = color; style.alignment = alignment;
            style.padding = new RectOffset(); style.richText = false; style.wordWrap = false;
            return style;
        }

        private GUIStyle Surface(Color fill, Color border, int radius, bool bottomOnly = false)
        {
            GUIStyle style = new GUIStyle(); style.normal.background = Rounded(fill, border, radius, bottomOnly);
            style.border = new RectOffset(radius, radius, radius, radius); return style;
        }

        private Texture2D Rounded(Color fill, Color border, int radius, bool bottomOnly = false)
        {
            int size = radius * 2 + 4;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave; texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++)
                {
                    float px = x + (sx + 0.5f) / 4, py = y + (sy + 0.5f) / 4;
                    if (!Inside(px, py, size, radius, bottomOnly)) continue;
                    Color sample = Inside(px - 1, py - 1, size - 2, Mathf.Max(0, radius - 1), bottomOnly) ? fill : border;
                    sum += sample;
                }
                Color pixel = sum / 16;
                // Keep edge RGB unpremultiplied for Unity's normal alpha blend.
                if (pixel.a > 0) { pixel.r /= pixel.a; pixel.g /= pixel.a; pixel.b /= pixel.a; }
                texture.SetPixel(x, y, pixel);
            }
            texture.Apply(false, true); textures.Add(texture); return texture;
        }

        private static bool Inside(float x, float y, int size, int radius, bool bottomOnly)
        {
            if (x < 0 || y < 0 || x > size || y > size) return false;
            if (bottomOnly && y >= size / 2f) return true;
            float dx = Mathf.Max(radius - x, x - (size - radius), 0);
            float dy = Mathf.Max(radius - y, y - (size - radius), 0);
            return dx * dx + dy * dy <= radius * radius;
        }

        public void Dispose()
        {
            foreach (Texture2D texture in textures) if (texture != null) Object.Destroy(texture);
            textures.Clear(); Window = null;
        }
    }
}
