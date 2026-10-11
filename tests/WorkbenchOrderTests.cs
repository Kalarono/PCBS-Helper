using System;
using System.Collections.Generic;
using System.Linq;
using PCBSCalculatorHelper;

internal static class WorkbenchOrderTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Main()
    {
        Dictionary<string, List<string>> graph = new Dictionary<string, List<string>> {
            { "PSU", new List<string> { "cover", "CPU power", "GPU power", "mains" } },
            { "cover", new List<string> { "rear panel" } }, { "rear panel", new List<string>() },
            { "CPU power", new List<string>() }, { "GPU power", new List<string>() }, { "mains", new List<string>() }
        };
        HashSet<string> removed = new HashSet<string>();
        Func<string, bool> ready = x => !removed.Contains(x) && graph[x].All(removed.Contains);
        Func<string, IEnumerable<string>> blockers = x => graph[x].Where(d => !removed.Contains(d));
        string first = DependencyOrder.Next("PSU", ready, blockers, StringComparer.Ordinal);
        Check(first == "rear panel", "Nested cover dependencies are resolved before the PSU.");
        removed.Add(first);
        Check(DependencyOrder.Next("PSU", ready, blockers, StringComparer.Ordinal) == "cover", "Live graph is recalculated after each animation.");
        removed.Add("cover");
        Check(DependencyOrder.Next("PSU", ready, blockers, StringComparer.Ordinal) == "CPU power", "Only required cables are removed next.");
        removed.UnionWith(new[] { "CPU power", "GPU power", "mains" });
        Check(DependencyOrder.Next("PSU", ready, blockers, StringComparer.Ordinal) == "PSU", "The target is removable only after all dependencies.");
        graph["cover"] = new List<string> { "PSU" }; removed.Clear();
        Check(DependencyOrder.Next("PSU", ready, blockers, StringComparer.Ordinal) == "CPU power", "A cyclic branch does not hide another actionable cable.");
        removed.UnionWith(new[] { "CPU power", "GPU power", "mains" });
        Check(DependencyOrder.Next("PSU", ready, blockers, StringComparer.Ordinal) == null, "An unresolved cycle is rejected, without forcing removal.");
        Check(DependencyOrder.Next<string>(null, ready, blockers, StringComparer.Ordinal) == null, "Destroyed targets produce no action.");
        graph["PSU"] = new List<string> { "cover", "cover" };
        graph["cover"] = new List<string>(); removed.Clear();
        Check(DependencyOrder.Next("PSU", ready, blockers, StringComparer.Ordinal) == "cover", "Shared dependencies produce one action.");

        Dictionary<string, List<string>> ramGraph = new Dictionary<string, List<string>> {
            { "board", new List<string> { "RAM" } }, { "RAM", new List<string> { "clips" } },
            { "clips", new List<string> { "top clip", "bottom clip" } },
            { "top clip", new List<string>() }, { "bottom clip", new List<string>() }
        };
        HashSet<string> completed = new HashSet<string>();
        Func<string, bool> ramReady = x => x != "clips" && !completed.Contains(x) &&
            ramGraph[x].All(d => d == "clips" ? ramGraph[d].All(completed.Contains) : completed.Contains(d));
        Func<string, IEnumerable<string>> ramBlockers = x => ramGraph[x].Where(d => !completed.Contains(d));
        Check(DependencyOrder.Next("board", ramReady, ramBlockers, StringComparer.Ordinal) == "top clip", "A compound RAM blocker opens its first clip before the RAM.");
        completed.Add("top clip");
        Check(DependencyOrder.Next("board", ramReady, ramBlockers, StringComparer.Ordinal) == "bottom clip", "An already-open clip is skipped; the second clip must open too.");
        completed.Add("bottom clip");
        Check(DependencyOrder.Next("board", ramReady, ramBlockers, StringComparer.Ordinal) == "RAM", "The compound blocker is never itself an uninstall action.");
        completed.Add("RAM");
        Check(DependencyOrder.Next("board", ramReady, ramBlockers, StringComparer.Ordinal) == "board", "Motherboard removal resumes after the RAM is removed.");

        // Reproduce CPU-first traversal on a fully built board. The socket
        // cannot open until its cooler and cooler cable have been removed.
        Dictionary<string, List<string>> cpuGraph = new Dictionary<string, List<string>> {
            { "board", new List<string> { "CPU", "cooler", "RAM", "GPU" } },
            { "CPU", new List<string> { "socket" } }, { "socket", new List<string> { "cooler" } },
            { "cooler", new List<string> { "fan cable" } }, { "fan cable", new List<string>() },
            { "RAM", new List<string>() }, { "GPU", new List<string>() }
        };
        HashSet<string> cpuDone = new HashSet<string>();
        bool socketOpen = false;
        Func<string, bool> cpuReady = x => x == "socket" ? !socketOpen && cpuDone.Contains("cooler") :
            !cpuDone.Contains(x) && cpuGraph[x].All(d => d == "socket" ? socketOpen : cpuDone.Contains(d));
        Func<string, IEnumerable<string>> cpuBlockers = x => cpuGraph[x].Where(d => d == "socket" ? !socketOpen : !cpuDone.Contains(d));
        Check(DependencyOrder.Next("board", cpuReady, cpuBlockers, StringComparer.Ordinal) == "fan cable", "A blocked CPU socket first resolves the cooler's cable.");
        cpuDone.Add("fan cable");
        Check(DependencyOrder.Next("board", cpuReady, cpuBlockers, StringComparer.Ordinal) == "cooler", "The cooler is removed before attempting the socket handle.");
        cpuDone.Add("cooler");
        Check(DependencyOrder.Next("board", cpuReady, cpuBlockers, StringComparer.Ordinal) == "socket", "The native socket animation becomes actionable only after the cooler.");
        socketOpen = true;
        Check(DependencyOrder.Next("board", cpuReady, cpuBlockers, StringComparer.Ordinal) == "CPU", "CPU removal follows the finished socket animation.");
        cpuDone.Add("CPU");
        Check(DependencyOrder.Next("board", cpuReady, cpuBlockers, StringComparer.Ordinal) == "RAM", "Motherboard traversal continues with the remaining installed parts.");
        cpuDone.UnionWith(new[] { "RAM", "GPU" });
        Check(DependencyOrder.Next("board", cpuReady, cpuBlockers, StringComparer.Ordinal) == "board", "Motherboard removal finishes after all live dependencies are removed.");

        Item old = new Item("old", 237), cheap = new Item("cheap", 117), duplicate = new Item("duplicate", 117), newer = new Item("new", 0);
        Item[] saved = { old, cheap, duplicate, newer };
        List<Item> shown = new List<Item> { duplicate, old, cheap };
        InventoryOrder.Sort(shown, saved, InventorySortMode.PriceLowFirst, x => x.Price);
        Check(shown.SequenceEqual(new[] { duplicate, cheap, old }), "Displayed resale prices ascend, with stable equal-price order.");
        InventoryOrder.Sort(shown, saved, InventorySortMode.PriceHighFirst, x => x.Price);
        Check(shown.SequenceEqual(new[] { old, duplicate, cheap }), "Prices descend when the price button is clicked again.");
        InventoryOrder.Sort(shown, saved, InventorySortMode.NewestFirst, x => x.Price);
        Check(shown.SequenceEqual(new[] { duplicate, cheap, old }), "Filtered results retain the saved arrival chronology.");
        InventoryOrder.Sort(shown, saved, InventorySortMode.OldestFirst, x => x.Price);
        Check(shown.SequenceEqual(new[] { old, cheap, duplicate }), "Arrival order can be reversed.");
        Check(saved.SequenceEqual(new[] { old, cheap, duplicate, newer }), "Sorting never changes the career inventory or hidden rows.");
        InventoryOrder.Sort(shown, saved, InventorySortMode.Original, x => { throw new Exception("No price lookup expected."); });
        Check(shown.SequenceEqual(new[] { old, cheap, duplicate }), "Original mode leaves native order intact.");
        shown = new List<Item> { newer, old };
        InventoryOrder.Sort(shown, saved, InventorySortMode.PriceLowFirst, x => x.Price);
        Check(shown[0] == newer, "Zero-value items sort correctly.");
        shown.Clear(); InventoryOrder.Sort(shown, saved, InventorySortMode.NewestFirst, x => x.Price);
        Check(shown.Count == 0, "Empty filtered inventory is supported.");
        Console.WriteLine("Workbench and inventory ordering: " + checks + " assertions passed.");
    }
    private sealed class Item
    {
        internal readonly string Name; internal readonly int Price;
        internal Item(string name, int price) { Name = name; Price = price; }
    }
}
