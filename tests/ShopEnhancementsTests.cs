using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using PCBSCalculatorHelper;

internal static class ShopEnhancementsTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Main(string[] args)
    {
        // Resolve config dependencies from the user's installed loader/game,
        // without initializing Unity or reading/writing a game save.
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs ev) {
            string filename = new AssemblyName(ev.Name).Name + ".dll";
            foreach (string dir in new[] { Path.Combine(args[0], "BepInEx", "core"), Path.Combine(args[0], "PCBS_Data", "Managed") })
            {
                string path = Path.Combine(dir, filename);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        CheckStock(); CheckCategoryVisibility(); CheckScrolling(); CheckSavedSort();
        Console.WriteLine("Shop stock, scroll routing and persisted sort: " + checks + " assertions passed.");
    }

    private sealed class Part
    {
        internal string ID;
        internal bool New, Broken;
        internal Part(string id, bool fresh, bool broken) { ID = id; New = fresh; Broken = broken; }
    }
    private static Dictionary<string, ShopStockCounts> Count(IEnumerable<Part> parts)
    { return ShopStockCounts.Build(parts, p => p.ID, p => p.New, p => p.Broken); }

    private static void CheckStock()
    {
        Part fresh = new Part("RAM_A", true, false), fresh2 = new Part("RAM_A", true, false),
            used = new Part("RAM_A", false, false), broken = new Part("RAM_A", false, true),
            brokenNew = new Part("RAM_A", true, true), cooler = new Part("Cooler", false, false);
        Part[] parts = { fresh, fresh2, used, broken, brokenNew, null, new Part(null, false, false),
            new Part("", false, false), new Part("RAM_A_Red", false, false), new Part("ram_a", true, false) };
        Part[] before = parts.ToArray();
        Dictionary<string, ShopStockCounts> counts = Count(parts.Concat(new[] { cooler, fresh }));
        Check(counts["RAM_A"].New == 2, "Distinct new inventory instances of the same model all count.");
        Check(counts["RAM_A"].Used == 1, "Working used parts have their own grade.");
        Check(counts["RAM_A"].Broken == 2, "Broken takes precedence over the new flag.");
        Check(counts["RAM_A"].Total == 5, "A repeated inventory reference is not counted twice.");
        Check(counts["Cooler"].Used == 1, "Different product models keep separate stock counts.");
        Check(counts["RAM_A_Red"].Total == 1 && counts["ram_a"].New == 1, "Exact model/variant IDs do not merge.");
        Check(counts.Count == 4, "Null entries and missing IDs do not create phantom inventory.");
        Check(parts.SequenceEqual(before), "Counting never mutates inventory order.");
        Check(Count(new Part[0]).Count == 0, "An empty career has no stock badges.");
        fresh.New = false; fresh.Broken = true;
        counts = Count(parts);
        Check(counts["RAM_A"].New == 1 && counts["RAM_A"].Broken == 3, "A condition change is reflected in the next snapshot.");
        counts = Count(parts.Where(p => p != used));
        Check(counts["RAM_A"].Used == 0 && counts["RAM_A"].Total == 4, "Installation or sale removes the item from stock.");
        var label = new ShopStockCounts { New = 12, Used = 3, Broken = 0 };
        Check(label.Tooltip(true).Contains("Новые: 12") && label.Tooltip(true).Contains("Б/у: 3") && label.Tooltip(true).Contains("Сломанные: 0"),
            "Russian hover legends identify all three icons and include zero counts.");
        Check(label.Tooltip(false).Contains("New: 12") && label.Tooltip(false).Contains("Used: 3") && label.Tooltip(false).Contains("Broken: 0"),
            "English hover legends explain the icons without relying on colour.");
        CultureInfo previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("ar-SA");
            Check(label.Tooltip(false).Contains("12"), "Inventory quantities use stable formatting.");
        }
        finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
    }

    private static void CheckCategoryVisibility()
    {
        foreach (PartDesc.ShopCategory category in new[] {
            PartDesc.ShopCategory.Cables, PartDesc.ShopCategory.Misc,
            PartDesc.ShopCategory.Monitors, PartDesc.ShopCategory.Keyboards,
            PartDesc.ShopCategory.Mice, PartDesc.ShopCategory.MousePads,
            PartDesc.ShopCategory.Headsets, PartDesc.ShopCategory.Microphones,
            PartDesc.ShopCategory.Coolant, PartDesc.ShopCategory.PipeConnectors,
            PartDesc.ShopCategory.Pipes })
            Check(!ShopStockCategories.SupportsCounters(category), "One-time purchases have no counter: " + category);
        foreach (PartDesc.ShopCategory category in new[] {
            PartDesc.ShopCategory.CPU, PartDesc.ShopCategory.GPU, PartDesc.ShopCategory.Memory,
            PartDesc.ShopCategory.Motherboard, PartDesc.ShopCategory.Storage, PartDesc.ShopCategory.PSU,
            PartDesc.ShopCategory.Case, PartDesc.ShopCategory.Cooling, PartDesc.ShopCategory.CaseCooling,
            PartDesc.ShopCategory.CaseParts, PartDesc.ShopCategory.Radiators, PartDesc.ShopCategory.Reservoir,
            PartDesc.ShopCategory.CPUBlock, PartDesc.ShopCategory.WaterCooledGPU })
            Check(ShopStockCategories.SupportsCounters(category), "Installable hardware retains its counter: " + category);
        foreach (PartDesc.ShopCategory category in new[] {
            PartDesc.ShopCategory.Cables, PartDesc.ShopCategory.Misc, PartDesc.ShopCategory.CaseParts,
            PartDesc.ShopCategory.Pipes, PartDesc.ShopCategory.PipeConnectors, PartDesc.ShopCategory.Coolant,
            PartDesc.ShopCategory.Monitors, PartDesc.ShopCategory.Keyboards, PartDesc.ShopCategory.Mice,
            PartDesc.ShopCategory.MousePads, PartDesc.ShopCategory.Headsets, PartDesc.ShopCategory.Microphones })
            Check(!ShopStockCategories.SupportsInventoryCounters(category), "Inventory exclusions: " + category);
        foreach (PartDesc.ShopCategory category in new[] {
            PartDesc.ShopCategory.CPU, PartDesc.ShopCategory.GPU, PartDesc.ShopCategory.Memory,
            PartDesc.ShopCategory.Motherboard, PartDesc.ShopCategory.Storage, PartDesc.ShopCategory.PSU,
            PartDesc.ShopCategory.Case, PartDesc.ShopCategory.Cooling, PartDesc.ShopCategory.CaseCooling,
            PartDesc.ShopCategory.Radiators, PartDesc.ShopCategory.Reservoir,
            PartDesc.ShopCategory.CPUBlock, PartDesc.ShopCategory.WaterCooledGPU })
            Check(ShopStockCategories.SupportsInventoryCounters(category), "Inventory hardware retains counts: " + category);
    }

    private static void CheckScrolling()
    {
        Check(ShopScrollRouting.ToCatalog(80, 140, 1, -1), "Short specifications do not trap catalog scrolling.");
        Check(ShopScrollRouting.ToCatalog(140, 140, 1, -1), "Exactly fitting text scrolls the catalog.");
        Check(!ShopScrollRouting.ToCatalog(280, 140, 1, -1), "Wheel down enters an overflowing spec block at its top.");
        Check(ShopScrollRouting.ToCatalog(280, 140, 1, 1), "Wheel up at the first spec line continues through the catalog.");
        Check(!ShopScrollRouting.ToCatalog(280, 140, 0.5f, -0.1f), "Small wheel steps remain within the overflowing text.");
        Check(!ShopScrollRouting.ToCatalog(280, 140, 0.5f, 0.1f), "Text scrolls in both directions before reaching its edge.");
        Check(ShopScrollRouting.ToCatalog(280, 140, 0, -1), "Wheel down after the last spec line continues through the catalog.");
        Check(!ShopScrollRouting.ToCatalog(280, 140, 0, 1), "Wheel up from the last line returns through the text.");
        Check(ShopScrollRouting.ToCatalog(140.1f, 140, 0.5f, -1), "Subpixel layout error does not create a one-pixel scroll trap.");
    }

    private static void CheckSavedSort()
    {
        string folder = Path.Combine(Path.GetTempPath(), "PCBSHelper-ShopTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "helper.cfg");
        try
        {
            var config = new ConfigFile(file, true);
            var first = new ShopSortSettings(config);
            Check(first.For("PartsShop").Value == -1, "The first launch retains the game's default sort.");
            first.For("PartsShop").Value = 2;
            first.For("WaterShop").Value = 1;
            first.For("PeripheralShop").Value = 0;
            config.Save();
            var reopened = new ShopSortSettings(new ConfigFile(file, true));
            Check(reopened.For("PartsShop").Value == 2, "Descending price survives a new config/runtime instance.");
            Check(reopened.For("WaterShop").Value == 1, "The water shop retains its own ascending price sort.");
            Check(reopened.For("PeripheralShop").Value == 0, "Explicit relevance persists instead of being treated as unset.");
            reopened.For("PartsShop").Value = 1;
            var nextLaunch = new ShopSortSettings(new ConfigFile(file, true));
            Check(nextLaunch.For("PartsShop").Value == 1, "The changed choice is saved automatically for the next launch.");
            Check(nextLaunch.For("WaterShop").Value == 1, "A different store's setting is unchanged.");
        }
        finally { if (File.Exists(file)) File.Delete(file); Directory.Delete(folder); }
    }
}
