using System;
using System.Collections.Generic;
using PCBSCalculatorHelper;

// Game API doubles keep cart behavior testable without a running Unity player.
internal class PartDesc
{
    internal enum ShopID { None, PartsShop, WaterShop, PeripheralShop }
    internal enum ShopCategory {
        Case, PSU, Cooling, Motherboard, Misc, GPU, Storage, Memory, CPU, CaseParts,
        Cables, CaseCooling, Radiators, Reservoir, CPUBlock, WaterCooledGPU, Pipes,
        PipeConnectors, Coolant, Monitors, Keyboards, Mice, MousePads, Headsets, Microphones, All, None
    }
    internal string m_id;
    internal enum Type { CASE, MOTHERBOARD, OTHER }
    internal Type m_type = Type.OTHER;
    internal enum Class { NORMAL, DUMMY }
    internal Class m_class = Class.NORMAL;
    internal string m_manufacturer = "", m_partName = "", m_uiName = "";
    internal int m_price;
    internal bool m_inShop = true, Available = true, OneTime;
    internal ShopCategory m_shopCategory;
    internal bool IsOneTimePurchase() { return OneTime; }
}
internal sealed class PartDescCPU : PartDesc { }
internal sealed class PartDescGPU : PartDesc { }
internal sealed class PartDescRAM : PartDesc { internal int m_sizeGb = 0, m_speedMhz = 0; }
internal sealed class ShopEntry
{
    internal PartDesc m_part;
    internal int m_cost, m_delivery;
    internal ShopEntry(PartDesc part) { m_part = part; m_cost = part.m_price; m_delivery = -1; }
}
internal sealed class CareerStatus
{
    internal readonly Dictionary<PartDesc.ShopID, List<ShopEntry>> Carts = new Dictionary<PartDesc.ShopID, List<ShopEntry>> {
        { PartDesc.ShopID.PartsShop, new List<ShopEntry>() },
        { PartDesc.ShopID.WaterShop, new List<ShopEntry>() },
        { PartDesc.ShopID.PeripheralShop, new List<ShopEntry>() }
    };
    internal readonly HashSet<string> Locked = new HashSet<string>(), Owned = new HashSet<string>();
    internal bool IsAvailableInShop(PartDesc part) { return part.Available; }
    internal bool IsPartUnlocked(string id) { return !Locked.Contains(id); }
    internal bool EverHad(string id) { return Owned.Contains(id); }
    internal List<ShopEntry> GetShopTrolley(PartDesc.ShopID shop) { return Carts[shop]; }
}

internal static class ClickPurchaseCartTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Main()
    {
        CareerStatus career = new CareerStatus();
        PartDesc ram = new PartDesc { m_id = "ram-exact-variant", m_price = 190, m_shopCategory = PartDesc.ShopCategory.Memory };
        List<ShopEntry> cart = career.GetShopTrolley(PartDesc.ShopID.PartsShop);
        PartDesc old = new PartDesc { m_id = "existing-cart-item", m_price = 25 };
        ShopEntry oldEntry = new ShopEntry(old); cart.Add(oldEntry);
        Check(ClickPurchaseCart.Add(career, ram) == ClickPurchaseResult.Added, "Clicking available RAM adds it.");
        Check(cart.Count == 2 && Object.ReferenceEquals(cart[0], oldEntry), "The existing cart is preserved.");
        Check(Object.ReferenceEquals(cart[1].m_part, ram), "The exact clicked model and variant are used.");
        Check(cart[1].m_cost == 190 && cart[1].m_delivery == -1, "A new entry uses shop price and pending delivery.");
        Check(ClickPurchaseCart.Add(career, ram) == ClickPurchaseResult.Added && cart.Count == 3,
            "A second click adds one more unit of the same RAM.");
        Check(!Object.ReferenceEquals(cart[1], cart[2]), "Each click creates a separate cart item.");
        career.Locked.Add(ram.m_id);
        Check(ClickPurchaseCart.Add(career, ram) == ClickPurchaseResult.Unavailable && cart.Count == 3,
            "A locked model cannot be added even when visible in the shop.");
        career.Locked.Clear(); ram.Available = false;
        Check(ClickPurchaseCart.Add(career, ram) == ClickPurchaseResult.Unavailable && cart.Count == 3,
            "Shop availability restrictions are respected.");
        ram.Available = true; ram.m_inShop = false;
        Check(ClickPurchaseCart.Add(career, ram) == ClickPurchaseResult.Unavailable && cart.Count == 3,
            "An item absent from shop stock cannot be bought by clicking.");
        ram.m_inShop = true;
        Check(ClickPurchaseCart.Add(career, null) == ClickPurchaseResult.InvalidPart, "Missing parts do not mutate a cart.");
        Check(ClickPurchaseCart.Add(career, new PartDesc()) == ClickPurchaseResult.InvalidPart, "A part must have a model ID.");
        Check(ClickPurchaseCart.Add(null, ram) == ClickPurchaseResult.NoCareer, "No cart is created outside a career.");
        PartDesc tool = new PartDesc { m_id = "one-time-tool", m_price = 50, OneTime = true, m_shopCategory = PartDesc.ShopCategory.Misc };
        Check(ClickPurchaseCart.Add(career, tool) == ClickPurchaseResult.Added, "An unowned one-time item can be added.");
        Check(ClickPurchaseCart.Add(career, tool) == ClickPurchaseResult.AlreadyOwned && cart.Count == 4,
            "One-time items cannot be duplicated in the cart.");
        career.Owned.Add(tool.m_id); cart.RemoveAt(3);
        Check(ClickPurchaseCart.Add(career, tool) == ClickPurchaseResult.AlreadyOwned && cart.Count == 3,
            "An already purchased one-time item cannot be added again.");
        career.Owned.Add(ram.m_id);
        Check(ClickPurchaseCart.Add(career, ram) == ClickPurchaseResult.Added && cart.Count == 4,
            "Previously owning normal hardware does not prevent buying another unit.");
        foreach (PartDesc.ShopCategory category in (PartDesc.ShopCategory[])Enum.GetValues(typeof(PartDesc.ShopCategory)))
        {
            if (category == PartDesc.ShopCategory.All || category == PartDesc.ShopCategory.None) continue;
            PartDesc.ShopID expected = category >= PartDesc.ShopCategory.Monitors ? PartDesc.ShopID.PeripheralShop :
                category >= PartDesc.ShopCategory.Radiators ? PartDesc.ShopID.WaterShop : PartDesc.ShopID.PartsShop;
            PartDesc part = new PartDesc { m_id = category.ToString(), m_price = 100, m_shopCategory = category };
            int before = career.Carts[expected].Count;
            Check(ClickPurchaseCart.Add(career, part) == ClickPurchaseResult.Added && career.Carts[expected].Count == before + 1,
                "Hardware is routed to its shop cart: " + category);
        }
        foreach (PartDesc.ShopCategory category in new PartDesc.ShopCategory[] { PartDesc.ShopCategory.All, PartDesc.ShopCategory.None })
            Check(ClickPurchaseCart.Add(career, new PartDesc { m_id = "not-a-product", m_shopCategory = category }) == ClickPurchaseResult.Unavailable,
                "Non-product categories do not create an untracked cart.");
        career.Carts[PartDesc.ShopID.WaterShop] = null;
        Check(ClickPurchaseCart.Add(career, new PartDesc { m_id = "radiator", m_shopCategory = PartDesc.ShopCategory.Radiators }) == ClickPurchaseResult.Unavailable,
            "Missing persistent carts are handled without buying an item.");
        Console.WriteLine("Click purchase cart: " + checks + " assertions passed.");
    }
}
