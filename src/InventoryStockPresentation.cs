using System;
using UnityEngine;
using UnityEngine.UI;

namespace PCBSCalculatorHelper
{
    internal sealed class InventoryStockPresentation : MonoBehaviour, IDisposable
    {
        internal PartInstance Part;
        private RectTransform picture;
        private Image pictureGraphic;
        private bool originalAspect;
        private ShopRectSnapshot originalPicture;
        private GameObject badge;
        private ShopStockBadge contents;
        private bool shown;

        internal void Initialize(ItemDisplay item, ShopStockIcons icons)
        {
            if (item.image == null || item.title == null) return;
            picture = item.image.rectTransform;
            pictureGraphic = item.image; originalAspect = item.image.preserveAspect;
            originalPicture = new ShopRectSnapshot(picture);
            // The native card's picture ends immediately above its title.
            // Reserve a strip by reducing only the picture, preserving the
            // name, specs, price, buttons and the inventory grid's cell size.
            RectTransform row = ShopDescriptionFrame.Rect("PCBS Helper Inventory Stock", picture);
            row.anchorMin = Vector2.zero; row.anchorMax = new Vector2(1, 0);
            row.pivot = new Vector2(0.5f, 1); row.sizeDelta = new Vector2(0, 16);
            row.anchoredPosition = new Vector2(0, -2);
            badge = row.gameObject;
            Image background = badge.AddComponent<Image>();
            background.color = new Color(0.94f, 0.96f, 0.97f);
            contents = badge.AddComponent<ShopStockBadge>();
            contents.Initialize(item.title.font, icons);
            badge.SetActive(false);
        }

        internal void Show(ShopStockCounts counts, bool russian, bool enabled)
        {
            if (badge == null || picture == null) return;
            bool show = enabled && counts.Total > 0;
            if (show != shown)
            {
                originalPicture.Apply(picture);
                pictureGraphic.preserveAspect = show || originalAspect;
                if (show) picture.offsetMin += new Vector2(0, 24);
                badge.SetActive(show); shown = show;
            }
            if (show) contents.Show(counts, russian);
        }

        public void Dispose()
        {
            if (picture != null) originalPicture.Apply(picture);
            if (pictureGraphic != null) pictureGraphic.preserveAspect = originalAspect;
            if (badge != null) Destroy(badge);
            Destroy(this);
        }
    }
}
