using System;
using System.Collections.Generic;
using System.Linq;

namespace PCBSCalculatorHelper
{
    internal enum InventorySortMode { Original, PriceLowFirst, PriceHighFirst, NewestFirst, OldestFirst }

    internal static class InventoryOrder
    {
        internal static void Sort<T>(List<T> shown, IEnumerable<T> arrivalOrder,
            InventorySortMode mode, Func<T, int> price) where T : class
        {
            if (mode == InventorySortMode.Original) return;
            Dictionary<T, int> arrival = new Dictionary<T, int>();
            int index = 0;
            foreach (T item in arrivalOrder)
            {
                if (item != null && !arrival.ContainsKey(item)) arrival.Add(item, index);
                index++;
            }
            // Keep ties stable, including temporary case parts absent from the save.
            var rows = shown.Select((item, i) => new { Item = item, Index = i,
                Arrival = item != null && arrival.ContainsKey(item) ? arrival[item] : -1,
                Price = item == null ? 0 : price(item) });
            IEnumerable<T> sorted;
            if (mode == InventorySortMode.PriceLowFirst)
                sorted = rows.OrderBy(x => x.Price).ThenBy(x => x.Index).Select(x => x.Item);
            else if (mode == InventorySortMode.PriceHighFirst)
                sorted = rows.OrderByDescending(x => x.Price).ThenBy(x => x.Index).Select(x => x.Item);
            else if (mode == InventorySortMode.NewestFirst)
                sorted = rows.OrderByDescending(x => x.Arrival).ThenBy(x => x.Index).Select(x => x.Item);
            else sorted = rows.OrderBy(x => x.Arrival).ThenBy(x => x.Index).Select(x => x.Item);
            T[] result = sorted.ToArray();
            shown.Clear(); shown.AddRange(result);
        }
    }
}
