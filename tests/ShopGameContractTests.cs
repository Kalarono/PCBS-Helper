using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

internal static class ShopGameContractTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static TypeDefinition Type(ModuleDefinition module, string name)
    { TypeDefinition result = module.Types.FirstOrDefault(t => t.Name == name); Check(result != null, "Missing type: " + name); return result; }
    private static void Field(TypeDefinition type, string name, string kind)
    { Check(type.Fields.Any(f => f.Name == name && f.FieldType.FullName == kind), "Missing native field: " + type.Name + "." + name); }
    private static void Method(TypeDefinition type, string name, params string[] args)
    { Check(type.Methods.Any(m => m.Name == name && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(args)), "Missing native signature: " + type.Name + "." + name); }
    private static void Main(string[] args)
    {
        using (ModuleDefinition game = ModuleDefinition.ReadModule(Path.Combine(args[0], "Assembly-CSharp-firstpass.dll")))
        using (ModuleDefinition ui = ModuleDefinition.ReadModule(Path.Combine(args[0], "UnityEngine.UI.dll")))
        {
            TypeDefinition shop = Type(game, "Shop"), item = Type(game, "ShopItem"), basis = Type(game, "BaseShopItem");
            Method(shop, "Start"); Field(shop, "m_shopID", "PartDesc/ShopID"); Field(shop, "m_sortBy", "UnityEngine.UI.Dropdown");
            Check(item.BaseType.Name == "BaseShopItem", "Shop cards inherit the native spec text fields.");
            Method(item, "Init", "ShopEntry"); Method(item, "ChangeVariant", "System.Int32"); Method(item, "UpdateInventoryCountText"); Method(item, "get_Entry");
            Field(item, "m_inventoryCount", "UnityEngine.UI.Text"); Field(basis, "m_desc", "UnityEngine.UI.Text");
            TypeDefinition career = Type(game, "CareerStatus"), part = Type(game, "PartInstance");
            Method(career, "GetInventory");
            Check(career.Events.Any(e => e.Name == "s_onInventoryUpdated" && e.EventType.FullName == "System.Action"), "Native inventory invalidation event is available.");
            Method(part, "GetPartId"); Method(part, "IsNew"); Method(part, "IsBroken"); Method(part, "IsProxy");
            TypeDefinition dropdown = Type(ui, "Dropdown"), scroll = Type(ui, "ScrollRect");
            Field(dropdown, "m_Value", "System.Int32"); Method(dropdown, "RefreshShownValue");
            foreach (string name in new[] { "OnScroll", "OnBeginDrag", "OnDrag", "OnEndDrag", "OnInitializePotentialDrag" })
                Check(scroll.Methods.Any(m => m.Name == name && m.IsVirtual), "Native nested scrolling can override " + name + ".");
            TypeDefinition text = Type(ui, "Text"); Method(text, "get_preferredHeight");
        }
        using (ModuleDefinition helper = ModuleDefinition.ReadModule(args[1]))
        {
            TypeDefinition hooks = Type(helper, "ShopEnhancements");
            Method(hooks, "AfterShopStarted", "Shop"); Method(hooks, "AfterItemChanged", "ShopItem"); Method(hooks, "AfterNativeCount", "ShopItem");
            TypeDefinition scroll = Type(helper, "ShopDescriptionScroll");
            Check(scroll.BaseType.FullName == "UnityEngine.UI.ScrollRect", "Specs use the native UI scrolling and clipping system.");
        }
        Console.WriteLine("Installed game shop/UI API: " + checks + " assertions passed.");
    }
}
