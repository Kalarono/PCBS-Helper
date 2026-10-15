using System;
using System.Collections.Generic;
using System.Globalization;

namespace PCBSCalculatorHelper
{
    internal struct ShopStockCounts
    {
        internal int New, Used, Broken;
        internal int Total { get { return New + Used + Broken; } }

        internal string Tooltip(bool russian)
        {
            return String.Format(CultureInfo.InvariantCulture,
                russian ? "Новые: {0}   Б/у: {1}   Сломанные: {2}" : "New: {0}   Used: {1}   Broken: {2}", New, Used, Broken);
        }

        internal static Dictionary<string, ShopStockCounts> Build<T>(IEnumerable<T> inventory,
            Func<T, string> id, Func<T, bool> isNew, Func<T, bool> isBroken) where T : class
        {
            var result = new Dictionary<string, ShopStockCounts>(StringComparer.Ordinal);
            var seen = new HashSet<T>();
            foreach (T part in inventory)
            {
                if (part == null || !seen.Add(part)) continue;
                string key = id(part);
                if (String.IsNullOrEmpty(key)) continue;
                ShopStockCounts counts;
                result.TryGetValue(key, out counts);
                // A failed item can still carry the native "new" flag.
                if (isBroken(part)) counts.Broken++;
                else if (isNew(part)) counts.New++;
                else counts.Used++;
                result[key] = counts;
            }
            return result;
        }
    }

    internal static class ShopStockCategories
    {
        internal static bool SupportsInventoryCounters(PartDesc.ShopCategory category)
        {
            // Case parts are individual covers/brackets generated for the PC
            // currently on the bench, rather than interchangeable stock.
            return category != PartDesc.ShopCategory.CaseParts && SupportsCounters(category);
        }

        internal static bool SupportsCounters(PartDesc.ShopCategory category)
        {
            switch (category)
            {
                case PartDesc.ShopCategory.Cables:
                case PartDesc.ShopCategory.Misc:
                case PartDesc.ShopCategory.Monitors:
                case PartDesc.ShopCategory.Keyboards:
                case PartDesc.ShopCategory.Mice:
                case PartDesc.ShopCategory.MousePads:
                case PartDesc.ShopCategory.Headsets:
                case PartDesc.ShopCategory.Microphones:
                case PartDesc.ShopCategory.Coolant:
                case PartDesc.ShopCategory.PipeConnectors:
                case PartDesc.ShopCategory.Pipes:
                    return false;
                default:
                    return true;
            }
        }
    }

    internal static class ShopScrollRouting
    {
        internal static bool ToCatalog(float contentHeight, float viewportHeight, float position, float wheelY)
        {
            return contentHeight <= viewportHeight + 1 ||
                (wheelY > 0 && position >= 0.999f) || (wheelY < 0 && position <= 0.001f);
        }
    }
}
