using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCBSCalculatorHelper
{
    internal sealed class ShopCardPresentation : MonoBehaviour, IDisposable
    {
        private ShopItem item;
        private ShopDescriptionFrame description;
        private GameObject badge;
        private ShopStockBadge badgeContents;
        private bool hasStock;

        internal void Initialize(ShopItem item, ShopStockIcons icons)
        {
            this.item = item;
            if (item.m_desc == null) return;
            description = new ShopDescriptionFrame(item.m_desc);
            RectTransform badgeRect;
            if (item.m_inventoryCount != null)
            {
                // The native prefab reserves a stock row between the description
                // and price. Use it rather than overlaying the name or specs.
                badgeRect = ShopDescriptionFrame.Rect("PCBS Helper Stock", item.m_inventoryCount.transform.parent);
                new ShopRectSnapshot(item.m_inventoryCount.rectTransform).Apply(badgeRect);
                badgeRect.sizeDelta = new Vector2(badgeRect.sizeDelta.x, 16);
                badgeRect.SetSiblingIndex(item.m_inventoryCount.transform.GetSiblingIndex());
            }
            else
            {
                // The separate detail page has no native stock label.
                badgeRect = ShopDescriptionFrame.Rect("PCBS Helper Stock", description.Frame);
                badgeRect.anchorMin = new Vector2(0, 1); badgeRect.anchorMax = Vector2.one;
                badgeRect.pivot = new Vector2(0.5f, 1); badgeRect.sizeDelta = new Vector2(0, 18);
            }
            badge = badgeRect.gameObject;
            Image background = badge.AddComponent<Image>();
            background.color = new Color(0.94f, 0.96f, 0.97f); background.raycastTarget = true;
            badgeContents = badge.AddComponent<ShopStockBadge>();
            badgeContents.Initialize(item.m_desc.font, icons);
            badge.SetActive(false);
            HideNativeCount();
        }

        internal void HideNativeCount()
        { if (item != null && item.m_inventoryCount != null) item.m_inventoryCount.gameObject.SetActive(false); }

        internal void ShowStock(ShopStockCounts counts, bool russian, bool enabled)
        {
            if (badge == null) return;
            bool show = enabled && counts.Total > 0;
            if (hasStock != show)
            {
                hasStock = show; badge.SetActive(show);
                if (item.m_inventoryCount == null) description.SetHeader(show ? 20 : 0);
            }
            if (show) badgeContents.Show(counts, russian);
            HideNativeCount();
        }

        internal void ResetScroll() { if (description != null) description.Reset(); }
        private void LateUpdate() { if (description != null) description.Layout(); }

        public void Dispose()
        {
            if (description != null) { description.Dispose(); description = null; }
            if (badge != null) Destroy(badge);
            if (item != null) item.UpdateInventoryCountText();
            Destroy(this);
        }
    }

    internal sealed class ShopDescriptionFrame : IDisposable
    {
        internal readonly RectTransform Frame;
        private readonly RectTransform viewport, content;
        private readonly ShopDescriptionScroll scroll;
        private readonly Text text;
        private readonly ShopRectSnapshot original;
        private readonly Transform originalParent;
        private readonly int sibling;
        private readonly bool raycast, bestFit;
        private readonly HorizontalWrapMode horizontal;
        private readonly VerticalWrapMode vertical;
        private readonly TextAnchor alignment;
        private readonly Scrollbar scrollbar;
        private float lastWidth = -1, lastHeight = -1;
        private int lastFontSize;
        private Font lastFont;
        private float lastLineSpacing;
        private string lastText;
        private bool reset = true;

        internal ShopDescriptionFrame(Text text)
        {
            this.text = text; content = text.rectTransform;
            originalParent = content.parent; sibling = content.GetSiblingIndex();
            original = new ShopRectSnapshot(content);
            raycast = text.raycastTarget; bestFit = text.resizeTextForBestFit;
            horizontal = text.horizontalOverflow; vertical = text.verticalOverflow; alignment = text.alignment;
            ScrollRect catalog = text.GetComponentInParent<ScrollRect>();
            Frame = Rect("PCBS Helper Specs", originalParent); original.Apply(Frame); Frame.SetSiblingIndex(sibling);
            viewport = Rect("Viewport", Frame); Stretch(viewport, Vector2.zero, Vector2.zero);
            Image hitArea = viewport.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ShopDescriptionScroll>();
            scroll.Catalog = catalog; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.inertia = false;
            scroll.scrollSensitivity = Mathf.Max(16, text.fontSize * 1.5f);
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
            content.localScale = Vector3.one; content.localRotation = Quaternion.identity;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft; text.resizeTextForBestFit = false; text.raycastTarget = false;
            scroll.viewport = viewport; scroll.content = content;

            RectTransform track = Rect("Scrollbar", viewport);
            track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
            track.pivot = Vector2.one; track.sizeDelta = new Vector2(5, 0);
            Image trackImage = track.gameObject.AddComponent<Image>(); trackImage.color = new Color(0.9f, 0.93f, 0.95f);
            RectTransform handle = Rect("Handle", track); Stretch(handle, Vector2.zero, Vector2.zero);
            Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(0.38f, 0.46f, 0.53f);
            scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
        }

        internal void SetHeader(float height)
        { viewport.offsetMax = new Vector2(0, -height); lastHeight = -1; }

        internal void Reset() { reset = true; lastText = null; }

        internal void Layout()
        {
            if (text == null || viewport == null) return;
            float width = viewport.rect.width, height = viewport.rect.height;
            if (width <= 1 || height <= 1) return;
            if (width != lastWidth || height != lastHeight || text.text != lastText || text.fontSize != lastFontSize ||
                text.font != lastFont || text.lineSpacing != lastLineSpacing)
            {
                content.sizeDelta = new Vector2(-10, content.sizeDelta.y);
                float preferredHeight = Mathf.Max(height, text.preferredHeight + 2);
                content.sizeDelta = new Vector2(-10, preferredHeight);
                scrollbar.gameObject.SetActive(preferredHeight > height + 1);
                lastWidth = width; lastHeight = height; lastText = text.text;
                lastFontSize = text.fontSize; lastFont = text.font; lastLineSpacing = text.lineSpacing;
            }
            if (reset) { scroll.verticalNormalizedPosition = 1; reset = false; }
        }

        public void Dispose()
        {
            if (text != null)
            {
                content.SetParent(originalParent, false); original.Apply(content); content.SetSiblingIndex(sibling);
                text.raycastTarget = raycast; text.resizeTextForBestFit = bestFit;
                text.horizontalOverflow = horizontal; text.verticalOverflow = vertical; text.alignment = alignment;
            }
            if (Frame != null) UnityEngine.Object.Destroy(Frame.gameObject);
        }

        internal static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer; return rect;
        }
        internal static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max; }
    }

    internal sealed class ShopDescriptionScroll : ScrollRect
    {
        internal ScrollRect Catalog;
        private bool catalogDrag;
        private bool ShortText
        { get { return content != null && viewport != null && content.rect.height <= viewport.rect.height + 1; } }
        public override void OnScroll(PointerEventData eventData)
        {
            if (eventData == null || !IsActive()) return;
            if (Catalog != null && Catalog.isActiveAndEnabled && content != null && viewport != null &&
                ShopScrollRouting.ToCatalog(content.rect.height, viewport.rect.height, verticalNormalizedPosition, eventData.scrollDelta.y))
                Catalog.OnScroll(eventData);
            else base.OnScroll(eventData);
        }
        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (Catalog != null && ShortText) Catalog.OnInitializePotentialDrag(eventData);
            else base.OnInitializePotentialDrag(eventData);
        }
        public override void OnBeginDrag(PointerEventData eventData)
        {
            catalogDrag = Catalog != null && Catalog.isActiveAndEnabled && ShortText;
            if (catalogDrag) Catalog.OnBeginDrag(eventData); else base.OnBeginDrag(eventData);
        }
        public override void OnDrag(PointerEventData eventData)
        { if (catalogDrag && Catalog != null) Catalog.OnDrag(eventData); else base.OnDrag(eventData); }
        public override void OnEndDrag(PointerEventData eventData)
        {
            if (catalogDrag && Catalog != null) Catalog.OnEndDrag(eventData); else base.OnEndDrag(eventData);
            catalogDrag = false;
        }
    }

    internal sealed class ShopRectSnapshot
    {
        private readonly Vector2 min, max, pivot, position, size;
        private readonly Vector3 scale;
        private readonly Quaternion rotation;
        internal ShopRectSnapshot(RectTransform rect)
        {
            min = rect.anchorMin; max = rect.anchorMax; pivot = rect.pivot;
            position = rect.anchoredPosition; size = rect.sizeDelta; scale = rect.localScale; rotation = rect.localRotation;
        }
        internal void Apply(RectTransform rect)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.sizeDelta = size; rect.anchoredPosition = position; rect.localScale = scale; rect.localRotation = rotation;
        }
    }
}
