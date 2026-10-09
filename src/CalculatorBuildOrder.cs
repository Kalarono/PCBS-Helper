using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ZenFulcrum.EmbeddedBrowser;

namespace PCBSCalculatorHelper
{
    internal sealed class BuildOrderItem
    {
        internal string Kind, Name;
        internal int Quantity;
    }

    internal sealed class BuildOrderSkip
    {
        internal BuildOrderItem Item;
        internal string Reason;
    }

    internal static class CalculatorBuildOrder
    {
        internal static List<BuildOrderItem> Read(string json, out string id)
        {
            id = null;
            JSONNode request = JSONNode.Parse(json);
            if (request.Type != JSONNode.NodeType.Object) throw new FormatException("Invalid build order.");
            id = request["id"].Value as string;
            if (id == null || !Regex.IsMatch(id, "^[a-zA-Z0-9-]{1,96}$") || Number(request["version"]) != 1)
                throw new FormatException("Invalid build order ID or version.");
            JSONNode items = request["items"];
            if (items.Type != JSONNode.NodeType.Array || items.Count < 1 || items.Count > 5)
                throw new FormatException("Invalid build order items.");
            List<BuildOrderItem> result = new List<BuildOrderItem>();
            HashSet<string> kinds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                JSONNode node = items[i];
                if (node.Type != JSONNode.NodeType.Object) throw new FormatException("Invalid build order item.");
                string kind = node["kind"].Value as string, name = node["name"].Value as string;
                int quantity = Number(node["quantity"]);
                int maximum = kind == "ram" ? 4 : kind == "gpu" ? 2 : 1;
                if (kind != "case" && kind != "cpu" && kind != "ram" && kind != "gpu" && kind != "motherboard")
                    throw new FormatException("Unsupported build part.");
                if (!kinds.Add(kind) || name == null || name.Trim().Length == 0 || name.Length > 256 || quantity < 1 || quantity > maximum)
                    throw new FormatException("Invalid build part or quantity.");
                result.Add(new BuildOrderItem { Kind = kind, Name = name.Trim(), Quantity = quantity });
            }
            return result;
        }
        private static int Number(JSONNode node)
        {
            if (node.Type != JSONNode.NodeType.Number) throw new FormatException("Expected a number.");
            double value = Convert.ToDouble(node.Value, CultureInfo.InvariantCulture);
            if (Double.IsNaN(value) || Double.IsInfinity(value) || value != Math.Floor(value) || value < 0 || value > 4)
                throw new FormatException("Invalid count.");
            return (int)value;
        }
        internal static string Normalize(string name)
        {
            // The original HTML contains legacy-encoded trademarks that UTF-8 readers expose as U+FFFD.
            string value = (name ?? "").Replace("\u2122", "").Replace("\u00AE", "").Replace("\uFFFD", "");
            // Meshify C's decorative spaced en-dash is also legacy-encoded in the calculator.
            // Keep ordinary hyphens and model/variant tokens intact.
            value = Regex.Replace(value, "(?<=\\s)[\u2013\u2014](?=\\s)", "");
            return Regex.Replace(Regex.Replace(value, "<[^>]*>", ""), "\\s+", " ").Trim();
        }
        private static bool KindMatches(PartDesc part, string kind)
        {
            return kind == "case" ? part.m_type == PartDesc.Type.CASE :
                kind == "motherboard" ? part.m_type == PartDesc.Type.MOTHERBOARD :
                kind == "cpu" ? part is PartDescCPU : kind == "gpu" ? part is PartDescGPU :
                kind == "ram" && part is PartDescRAM;
        }
        internal static PartDesc Resolve(IEnumerable<PartDesc> catalog, BuildOrderItem item, out bool ambiguous)
        {
            ambiguous = false;
            PartDesc found = null;
            string wanted = Normalize(item.Name);
            foreach (PartDesc part in catalog)
            {
                if (part == null || !part.m_inShop || part.m_class == PartDesc.Class.DUMMY || !KindMatches(part, item.Kind)) continue;
                // RAM's displayed capacity/frequency is localized; reconstruct its canonical name.
                string canonical = (part.m_manufacturer ?? "") + " " + (part.m_partName ?? "");
                PartDescRAM ram = part as PartDescRAM;
                if (ram != null) canonical += " " + ram.m_sizeGb.ToString(CultureInfo.InvariantCulture) + " GB " +
                    ram.m_speedMhz.ToString(CultureInfo.InvariantCulture) + " MHz";
                if (!String.Equals(wanted, Normalize(part.m_uiName), StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(wanted, Normalize(canonical), StringComparison.OrdinalIgnoreCase)) continue;
                if (found != null && found.m_id != part.m_id) { ambiguous = true; return null; }
                found = part;
            }
            return found;
        }

        internal static int AddAvailable(CareerStatus career, IEnumerable<PartDesc> catalog,
            IList<BuildOrderItem> request, out List<BuildOrderSkip> skipped)
        {
            skipped = new List<BuildOrderSkip>();
            int added = 0;
            foreach (BuildOrderItem item in request)
            {
                string reason = null;
                try
                {
                    bool ambiguous;
                    PartDesc part = Resolve(catalog, item, out ambiguous);
                    if (part == null) reason = ambiguous ? "ambiguous" : "missing";
                    else
                    {
                        int count;
                        ClickPurchaseResult result = ClickPurchaseCart.AddBatch(career,
                            new CartPartQuantity[] { new CartPartQuantity { Part = part, Quantity = item.Quantity } }, out count);
                        if (result == ClickPurchaseResult.Added) added += count;
                        else reason = result == ClickPurchaseResult.AlreadyOwned ? "owned" : "unavailable";
                    }
                }
                catch (Exception) { reason = "error"; }
                if (reason != null) skipped.Add(new BuildOrderSkip { Item = item, Reason = reason });
            }
            return added;
        }
    }
}
