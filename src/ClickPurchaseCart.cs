using System;
using System.Collections.Generic;

namespace PCBSCalculatorHelper
{
    internal enum ClickPurchaseResult { Added, InvalidPart, InvalidQuantity, Unavailable, AlreadyOwned, NoCareer }

    internal sealed class CartPartQuantity
    {
        internal PartDesc Part { get; set; }
        internal int Quantity { get; set; }
    }

    internal static class ClickPurchaseCart
    {
        internal static PartDesc.ShopID ShopFor(PartDesc.ShopCategory category)
        {
            switch (category)
            {
                case PartDesc.ShopCategory.Radiators:
                case PartDesc.ShopCategory.Reservoir:
                case PartDesc.ShopCategory.CPUBlock:
                case PartDesc.ShopCategory.WaterCooledGPU:
                case PartDesc.ShopCategory.Pipes:
                case PartDesc.ShopCategory.PipeConnectors:
                case PartDesc.ShopCategory.Coolant:
                    return PartDesc.ShopID.WaterShop;
                case PartDesc.ShopCategory.Monitors:
                case PartDesc.ShopCategory.Keyboards:
                case PartDesc.ShopCategory.Mice:
                case PartDesc.ShopCategory.MousePads:
                case PartDesc.ShopCategory.Headsets:
                case PartDesc.ShopCategory.Microphones:
                    return PartDesc.ShopID.PeripheralShop;
                case PartDesc.ShopCategory.None:
                case PartDesc.ShopCategory.All:
                    return PartDesc.ShopID.None;
                default:
                    return PartDesc.ShopID.PartsShop;
            }
        }

        internal static ClickPurchaseResult Add(CareerStatus career, PartDesc part)
        {
            List<ShopEntry> trolley;
            ClickPurchaseResult result = Validate(career, part, out trolley);
            if (result != ClickPurchaseResult.Added) return result;
            trolley.Add(new ShopEntry(part));
            return ClickPurchaseResult.Added;
        }

        private static ClickPurchaseResult Validate(CareerStatus career, PartDesc part, out List<ShopEntry> trolley)
        {
            trolley = null;
            if (career == null) return ClickPurchaseResult.NoCareer;
            if (part == null || String.IsNullOrEmpty(part.m_id)) return ClickPurchaseResult.InvalidPart;
            if (!part.m_inShop || !career.IsAvailableInShop(part) || !career.IsPartUnlocked(part.m_id))
                return ClickPurchaseResult.Unavailable;
            PartDesc.ShopID shop = ShopFor(part.m_shopCategory);
            if (shop == PartDesc.ShopID.None) return ClickPurchaseResult.Unavailable;
            trolley = career.GetShopTrolley(shop);
            if (trolley == null) return ClickPurchaseResult.Unavailable;
            if (part.IsOneTimePurchase())
            {
                if (career.EverHad(part.m_id)) return ClickPurchaseResult.AlreadyOwned;
                foreach (ShopEntry entry in trolley)
                    if (entry.m_part != null && entry.m_part.m_id == part.m_id) return ClickPurchaseResult.AlreadyOwned;
            }
            return ClickPurchaseResult.Added;
        }

        internal static ClickPurchaseResult AddBatch(CareerStatus career, IList<CartPartQuantity> parts, out int added)
        {
            added = 0;
            if (parts == null || parts.Count == 0) return ClickPurchaseResult.InvalidPart;
            List<KeyValuePair<List<ShopEntry>, ShopEntry>> pending = new List<KeyValuePair<List<ShopEntry>, ShopEntry>>();
            HashSet<string> oneTime = new HashSet<string>(StringComparer.Ordinal);
            foreach (CartPartQuantity item in parts)
            {
                if (item == null || item.Part == null) return ClickPurchaseResult.InvalidPart;
                if (item.Quantity < 1 || item.Quantity > 4) return ClickPurchaseResult.InvalidQuantity;
                List<ShopEntry> cart;
                ClickPurchaseResult result = Validate(career, item.Part, out cart);
                if (result != ClickPurchaseResult.Added) return result;
                if (item.Part.IsOneTimePurchase() && (item.Quantity != 1 || !oneTime.Add(item.Part.m_id)))
                    return ClickPurchaseResult.AlreadyOwned;
                for (int i = 0; i < item.Quantity; i++)
                    pending.Add(new KeyValuePair<List<ShopEntry>, ShopEntry>(cart, new ShopEntry(item.Part)));
            }
            // Resolve and validate the entire build before modifying any persistent cart.
            foreach (KeyValuePair<List<ShopEntry>, ShopEntry> item in pending) item.Key.Add(item.Value);
            added = pending.Count;
            return ClickPurchaseResult.Added;
        }
    }
}
