using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCBSCalculatorHelper
{
    internal sealed class ShopStockBadge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private readonly Text[] quantities = new Text[3];
        private GameObject tooltip;
        private Text tooltipText;

        internal void Initialize(Font font, ShopStockIcons icons)
        {
            Color[] colors = { new Color(0.16f, 0.53f, 0.27f), new Color(0.70f, 0.49f, 0.02f), new Color(0.78f, 0.23f, 0.22f) };
            for (int i = 0; i < 3; i++)
            {
                RectTransform group = ShopDescriptionFrame.Rect("Grade " + i, transform);
                group.anchorMin = new Vector2(i / 3f, 0); group.anchorMax = new Vector2((i + 1) / 3f, 1);
                group.offsetMin = new Vector2(4, 0); group.offsetMax = new Vector2(-2, 0);
                RectTransform iconRect = ShopDescriptionFrame.Rect("Icon", group);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 0.5f);
                iconRect.pivot = new Vector2(0, 0.5f); iconRect.sizeDelta = new Vector2(9, 9);
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = icons.Get(i); icon.color = colors[i]; icon.raycastTarget = false;
                RectTransform number = ShopDescriptionFrame.Rect("Quantity", group);
                ShopDescriptionFrame.Stretch(number, new Vector2(13, 0), Vector2.zero);
                quantities[i] = MakeText(number, font, new Color(0.16f, 0.21f, 0.25f));
            }
            RectTransform tip = ShopDescriptionFrame.Rect("Stock legend", transform);
            tip.anchorMin = new Vector2(0, 1); tip.anchorMax = Vector2.one; tip.pivot = new Vector2(0.5f, 0);
            tip.anchoredPosition = new Vector2(0, 3); tip.sizeDelta = new Vector2(0, 36);
            tooltip = tip.gameObject;
            Image background = tooltip.AddComponent<Image>();
            background.color = new Color(0.09f, 0.12f, 0.16f, 0.98f); background.raycastTarget = false;
            RectTransform label = ShopDescriptionFrame.Rect("Legend", tip);
            ShopDescriptionFrame.Stretch(label, new Vector2(6, 2), new Vector2(-6, -2));
            tooltipText = MakeText(label, font, Color.white);
            tooltipText.resizeTextMinSize = 10;
            tooltip.SetActive(false);
        }

        private static Text MakeText(RectTransform rect, Font font, Color color)
        {
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = 11; text.color = color;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 8; text.resizeTextMaxSize = 11;
            text.alignment = TextAnchor.MiddleLeft; text.supportRichText = false; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        internal void Show(ShopStockCounts counts, bool russian)
        {
            int[] values = { counts.New, counts.Used, counts.Broken };
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i].ToString(CultureInfo.InvariantCulture);
                if (quantities[i].text != value) quantities[i].text = value;
            }
            string label = counts.Tooltip(russian);
            if (tooltipText.text != label) tooltipText.text = label;
        }

        public void OnPointerEnter(PointerEventData eventData) { if (tooltip != null) tooltip.SetActive(true); }
        public void OnPointerExit(PointerEventData eventData) { if (tooltip != null) tooltip.SetActive(false); }
        private void OnDisable() { if (tooltip != null) tooltip.SetActive(false); }
    }

    // Shared, code-drawn UI silhouettes avoid dependence on glyph coverage in
    // the game's font. Shape and hover labels supplement the requested colours.
    internal sealed class ShopStockIcons : IDisposable
    {
        private readonly Sprite[] sprites = new Sprite[3];
        internal Sprite Get(int grade)
        {
            if (sprites[grade] != null) return sprites[grade];
            const int size = 24, samples = 4;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "PCBS Helper Stock " + grade; texture.hideFlags = HideFlags.DontSave;
            texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int filled = 0;
                for (int sy = 0; sy < samples; sy++) for (int sx = 0; sx < samples; sx++)
                {
                    float px = (x + (sx + 0.5f) / samples) / size * 2 - 1;
                    float py = (y + (sy + 0.5f) / samples) / size * 2 - 1;
                    bool inside = grade == 0 ? px * px + py * py <= 0.8f * 0.8f :
                        grade == 1 ? Mathf.Abs(px) + Mathf.Abs(py) <= 1 :
                        Mathf.Max(Mathf.Abs(px), Mathf.Abs(py)) <= 0.8f && Mathf.Abs(Mathf.Abs(px) - Mathf.Abs(py)) <= 0.25f;
                    if (inside) filled++;
                }
                texture.SetPixel(x, y, new Color(1, 1, 1, filled / (float)(samples * samples)));
            }
            texture.Apply(false, true);
            sprites[grade] = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprites[grade].hideFlags = HideFlags.DontSave;
            return sprites[grade];
        }

        public void Dispose()
        {
            for (int i = 0; i < sprites.Length; i++) if (sprites[i] != null)
            {
                UnityEngine.Object.Destroy(sprites[i].texture);
                UnityEngine.Object.Destroy(sprites[i]); sprites[i] = null;
            }
        }
    }
}
