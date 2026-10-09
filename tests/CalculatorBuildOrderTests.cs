using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PCBSCalculatorHelper;

internal static class CalculatorBuildOrderTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Reject(string items, string message)
    {
        bool rejected = false;
        try { string id; CalculatorBuildOrder.Read("{\"version\":1,\"id\":\"test-1\",\"items\":" + items + "}", out id); }
        catch (FormatException) { rejected = true; }
        Check(rejected, message);
    }
    private static void Main(string[] args)
    {
        // Load only managed JSON parsing from the game's browser DLL, with no running game.
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs ev) {
            string path = Path.Combine(args[0], new AssemblyName(ev.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Run();
        if (args.Length > 1 && File.Exists(args[1])) CheckLocalCatalog(args[1]);
        Console.WriteLine("Calculator build orders: " + checks + " assertions passed.");
    }
    private static void Run()
    {
        string id;
        List<BuildOrderItem> request = CalculatorBuildOrder.Read("{\"version\":1,\"id\":\"order-1\",\"items\":[" +
            "{\"kind\":\"case\",\"name\":\"ADATA XPG Battlecruiser (Black)\",\"quantity\":1}," +
            "{\"kind\":\"ram\",\"name\":\"ADATA Premier Series 4 GB 2133 MHz\",\"quantity\":4}," +
            "{\"kind\":\"gpu\",\"name\":\"Two GPUs\",\"quantity\":2}]}", out id);
        Check(id == "order-1" && request.Count == 3, "The real JSONNode parser accepts the bridge envelope.");
        Check(request[1].Quantity == 4 && request[2].Quantity == 2, "RAM and GPU quantities survive native JSON parsing.");
        Reject("[]", "Empty builds are rejected.");
        Reject("{}", "Items must be an array.");
        Reject("[null]", "Items must be objects.");
        Reject("[{\"kind\":\"psu\",\"name\":\"PSU\",\"quantity\":1}]", "PSU is not ordered by Build Maker.");
        Reject("[{\"kind\":\"storage\",\"name\":\"SSD\",\"quantity\":1}]", "Storage stays a manual purchase.");
        Reject("[{\"kind\":\"ram\",\"name\":\"RAM\",\"quantity\":5}]", "Excess RAM counts are rejected.");
        Reject("[{\"kind\":\"gpu\",\"name\":\"GPU\",\"quantity\":3}]", "More than two GPUs are rejected.");
        Reject("[{\"kind\":\"cpu\",\"name\":\"CPU\",\"quantity\":2}]", "A build has only one CPU.");
        Reject("[{\"kind\":\"ram\",\"name\":\"RAM\",\"quantity\":0}]", "Zero quantities are rejected.");
        Reject("[{\"kind\":\"ram\",\"name\":\"RAM\",\"quantity\":1.5}]", "Fractional quantities are rejected.");
        Reject("[{\"kind\":\"ram\",\"name\":\"RAM\",\"quantity\":\"2\"}]", "Numeric strings are not accepted as quantities.");
        Reject("[{\"kind\":\"cpu\",\"name\":\" \",\"quantity\":1}]", "Empty model names are rejected.");
        Reject("[{\"kind\":\"cpu\",\"name\":\"CPU\",\"quantity\":1},{\"kind\":\"cpu\",\"name\":\"CPU\",\"quantity\":1}]",
            "Duplicate part roles are rejected.");

        PartDescRAM ram = new PartDescRAM { m_id = "ram-4-2133", m_manufacturer = "ADATA", m_partName = "Premier Series",
            m_sizeGb = 4, m_speedMhz = 2133, m_uiName = "ADATA Premier Series 4 ГБ 2133 МГц", m_shopCategory = PartDesc.ShopCategory.Memory };
        PartDescRAM otherRam = new PartDescRAM { m_id = "ram-8-2133", m_manufacturer = "ADATA", m_partName = "Premier Series",
            m_sizeGb = 8, m_speedMhz = 2133, m_uiName = "ADATA Premier Series 8 ГБ 2133 МГц" };
        bool ambiguous;
        PartDesc found = CalculatorBuildOrder.Resolve(new PartDesc[] { otherRam, ram }, request[1], out ambiguous);
        Check(found == ram && !ambiguous, "RAM matches by exact canonical capacity and speed in a Russian game.");
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { otherRam }, request[1], out ambiguous) == null,
            "A different RAM capacity is never substituted.");
        PartDesc pcCase = new PartDesc { m_id = "case-black", m_manufacturer = "ADATA", m_partName = "XPG Battlecruiser (Black)",
            m_uiName = "ADATA XPG Battlecruiser (Чёрный)", m_type = PartDesc.Type.CASE, m_shopCategory = PartDesc.ShopCategory.Case };
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { pcCase }, request[0], out ambiguous) == pcCase,
            "Exact case color variants match without changing the game's language.");
        BuildOrderItem cpuRequest = new BuildOrderItem { Kind = "cpu", Name = "<b>Intel  Core i7-7700K</b>", Quantity = 1 };
        PartDescCPU cpu = new PartDescCPU { m_id = "cpu", m_manufacturer = "Intel", m_partName = "Core i7-7700K", m_shopCategory = PartDesc.ShopCategory.CPU };
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { cpu }, cpuRequest, out ambiguous) == cpu,
            "Formatting tags and spacing do not prevent exact model matching.");
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { new PartDescGPU { m_id = "wrong-kind", m_uiName = "Intel Core i7-7700K" } }, cpuRequest, out ambiguous) == null,
            "A matching name in another hardware category cannot be ordered.");
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { cpu, new PartDescCPU { m_id = "ambiguous", m_uiName = "Intel Core i7-7700K" } }, cpuRequest, out ambiguous) == null && ambiguous,
            "Ambiguous models are rejected instead of selecting an arbitrary part.");
        PartDescCPU ryzen = new PartDescCPU { m_id = "CPU_AMD_6", m_manufacturer = "AMD", m_partName = "Ryzen 7 Eight Core 1700" };
        PartDescCPU dummy = new PartDescCPU { m_id = "DUMMY Ryzen 7 Eight Core 1700", m_manufacturer = "AMD", m_partName = "Ryzen 7 Eight Core 1700",
            m_class = PartDesc.Class.DUMMY, m_inShop = false };
        BuildOrderItem ryzenRequest = new BuildOrderItem { Kind = "cpu", Name = "AMD Ryzen 7 Eight Core 1700", Quantity = 1 };
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { dummy, ryzen }, ryzenRequest, out ambiguous) == ryzen && !ambiguous,
            "The actual Ryzen 1700 dummy collision resolves to CPU_AMD_6.");
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { ryzen, dummy }, ryzenRequest, out ambiguous) == ryzen && !ambiguous,
            "Catalog iteration order cannot change the chosen Ryzen ID.");
        dummy.m_inShop = true;
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { dummy, ryzen }, ryzenRequest, out ambiguous) == ryzen,
            "DUMMY class entries never shadow real models even when marked in-shop.");
        PartDescGPU trademarkGpu = new PartDescGPU { m_id = "msi-3090", m_manufacturer = "MSI", m_partName = "GeForce RTX\u2122 3090 VENTUS 3X 24G OC" };
        BuildOrderItem trademarkRequest = new BuildOrderItem { Kind = "gpu", Name = "MSI GeForce RTX\uFFFD 3090 VENTUS 3X 24G OC", Quantity = 2 };
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { trademarkGpu }, trademarkRequest, out ambiguous) == trademarkGpu,
            "Legacy HTML trademark replacement matches the real GPU model.");
        Check(CalculatorBuildOrder.Normalize("  EVGA\u00AE\u00A0GeForce  GTX 1080 GAMING ") == "EVGA GeForce GTX 1080 GAMING",
            "Trademark symbols and non-breaking spaces normalize without changing the model number.");
        PartDesc clearCase = new PartDesc { m_id = "Case_FractalDesign_2", m_manufacturer = "Fractal Design", m_partName = "Meshify C \u2013 TG", m_type = PartDesc.Type.CASE };
        PartDesc darkCase = new PartDesc { m_id = "Case_FractalDesign_3", m_manufacturer = "Fractal Design", m_partName = "Meshify C \u2013 Dark TG", m_type = PartDesc.Type.CASE };
        PartDesc plainCase = new PartDesc { m_id = "Case_FractalDesign_1", m_manufacturer = "Fractal Design", m_partName = "Meshify C", m_type = PartDesc.Type.CASE };
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { plainCase, darkCase, clearCase }, new BuildOrderItem { Kind = "case", Name = "Fractal Design Meshify C \uFFFD TG" }, out ambiguous) == clearCase && !ambiguous,
            "A corrupted decorative dash resolves Meshify C TG to the correct case ID.");
        Check(CalculatorBuildOrder.Resolve(new PartDesc[] { clearCase, plainCase, darkCase }, new BuildOrderItem { Kind = "case", Name = "Fractal Design Meshify C \uFFFD Dark TG" }, out ambiguous) == darkCase && !ambiguous,
            "Dark TG is never substituted by plain Meshify C or clear TG.");

        CareerStatus career = new CareerStatus();
        List<ShopEntry> cart = career.Carts[PartDesc.ShopID.PartsShop];
        ShopEntry existing = new ShopEntry(new PartDesc { m_id = "existing" }); cart.Add(existing);
        int added;
        List<CartPartQuantity> batch = new List<CartPartQuantity> { new CartPartQuantity { Part = cpu, Quantity = 1 }, new CartPartQuantity { Part = ram, Quantity = 4 } };
        Check(ClickPurchaseCart.AddBatch(career, batch, out added) == ClickPurchaseResult.Added && added == 5,
            "A batch adds exactly one CPU and four RAM modules.");
        Check(cart.Count == 6 && Object.ReferenceEquals(cart[0], existing), "Existing cart entries are preserved by ordering.");
        Check(cart[2].m_part == ram && cart[5].m_part == ram && !Object.ReferenceEquals(cart[2], cart[5]),
            "Every requested RAM unit becomes a separate new shop entry.");
        career.Locked.Add(ram.m_id);
        Check(ClickPurchaseCart.AddBatch(career, batch, out added) == ClickPurchaseResult.Unavailable && added == 0 && cart.Count == 6,
            "A late unavailable part prevents adding the earlier valid CPU too.");
        career.Locked.Clear();
        PartDescGPU water = new PartDescGPU { m_id = "water-gpu", m_shopCategory = PartDesc.ShopCategory.WaterCooledGPU };
        batch = new List<CartPartQuantity> { new CartPartQuantity { Part = water, Quantity = 2 }, new CartPartQuantity { Part = cpu, Quantity = 1 } };
        Check(ClickPurchaseCart.AddBatch(career, batch, out added) == ClickPurchaseResult.Added && added == 3 && career.Carts[PartDesc.ShopID.WaterShop].Count == 2,
            "Two water-cooled GPUs and a CPU go to their respective persistent carts.");
        career.Locked.Add(cpu.m_id);
        Check(ClickPurchaseCart.AddBatch(career, batch, out added) == ClickPurchaseResult.Unavailable && added == 0 && career.Carts[PartDesc.ShopID.WaterShop].Count == 2,
            "Validation failure leaves all shop carts unchanged.");
        career.Locked.Clear();
        Check(ClickPurchaseCart.AddBatch(career, new CartPartQuantity[] { new CartPartQuantity { Part = ram, Quantity = 0 } }, out added) == ClickPurchaseResult.InvalidQuantity,
            "The cart layer independently rejects invalid quantities.");
        Check(ClickPurchaseCart.AddBatch(null, batch, out added) == ClickPurchaseResult.NoCareer && added == 0,
            "Ordering without a career cannot create a detached cart.");

        CareerStatus partialCareer = new CareerStatus();
        PartDescGPU gpu = new PartDescGPU { m_id = "GPU_EVGA_4", m_manufacturer = "EVGA", m_partName = "GeForce GTX 1080 GAMING", m_shopCategory = PartDesc.ShopCategory.GPU };
        List<BuildOrderItem> partialRequest = new List<BuildOrderItem> { ryzenRequest,
            new BuildOrderItem { Kind = "case", Name = "Unknown exact case", Quantity = 1 },
            new BuildOrderItem { Kind = "gpu", Name = "EVGA GeForce GTX 1080 GAMING", Quantity = 2 }, request[1] };
        partialCareer.Locked.Add(ram.m_id);
        List<BuildOrderSkip> skipped;
        added = CalculatorBuildOrder.AddAvailable(partialCareer, new PartDesc[] { dummy, ryzen, gpu, ram }, partialRequest, out skipped);
        Check(added == 3 && partialCareer.Carts[PartDesc.ShopID.PartsShop].Count == 3,
            "A missing case and locked RAM do not prevent ordering the real CPU and both GPUs.");
        Check(partialCareer.Carts[PartDesc.ShopID.PartsShop][0].m_part == ryzen && partialCareer.Carts[PartDesc.ShopID.PartsShop][1].m_part == gpu &&
            partialCareer.Carts[PartDesc.ShopID.PartsShop][2].m_part == gpu, "Partial orders contain exact real CPU/GPU models and quantities.");
        Check(skipped.Count == 2 && skipped[0].Reason == "missing" && skipped[0].Item.Name == "Unknown exact case" &&
            skipped[1].Reason == "unavailable" && skipped[1].Item.Quantity == 4, "Warnings retain each skipped model, reason and quantity.");
        partialCareer.Locked.Clear();
        added = CalculatorBuildOrder.AddAvailable(partialCareer, new PartDesc[] { dummy, ryzen, gpu, ram }, partialRequest, out skipped);
        Check(added == 7 && skipped.Count == 1, "A known RAM model joins the partial order as soon as it becomes available.");
        added = CalculatorBuildOrder.AddAvailable(partialCareer, new PartDesc[0], partialRequest, out skipped);
        Check(added == 0 && skipped.Count == 4, "An entirely missing build reports all skipped items without adding anything.");
    }
    private static void CheckLocalCatalog(string path)
    {
        var fixture = ZenFulcrum.EmbeddedBrowser.JSONNode.Parse(File.ReadAllText(path));
        Check(fixture["unmatched"].Count == 0, "All five ordinary in-shop part groups match the local game catalog.");
        var entries = fixture["catalog"]; var expected = fixture["expected"];
        List<PartDesc> catalog = new List<PartDesc>();
        for (int i = 0; i < entries.Count; i++)
        {
            var data = entries[i];
            string kind = (string)data["kind"];
            PartDesc part = kind == "cpu" ? (PartDesc)new PartDescCPU() : kind == "gpu" ? new PartDescGPU() : kind == "ram" ? new PartDescRAM() : new PartDesc();
            if (kind == "case") part.m_type = PartDesc.Type.CASE;
            if (kind == "motherboard") part.m_type = PartDesc.Type.MOTHERBOARD;
            PartDescRAM ram = part as PartDescRAM;
            if (ram != null) { ram.m_sizeGb = Convert.ToInt32(data["sizeGb"].Value); ram.m_speedMhz = Convert.ToInt32(data["speedMhz"].Value); }
            part.m_id = (string)data["id"]; part.m_manufacturer = (string)data["manufacturer"]; part.m_partName = (string)data["partName"];
            part.m_inShop = (bool)data["inShop"]; part.m_class = (bool)data["dummy"] ? PartDesc.Class.DUMMY : PartDesc.Class.NORMAL;
            catalog.Add(part);
        }
        for (int i = 0; i < expected.Count; i++)
        {
            var data = expected[i]; bool ambiguous;
            var item = new BuildOrderItem { Kind = (string)data["kind"], Name = (string)data["name"], Quantity = 1 };
            var part = CalculatorBuildOrder.Resolve(catalog, item, out ambiguous);
            Check(part != null && !ambiguous && part.m_id == (string)data["id"], "Local game catalog matches the calculator name to the real ID: " + item.Name);
        }
        Console.WriteLine("Local five-group catalog: " + expected.Count + " exact model IDs verified.");
    }
}
