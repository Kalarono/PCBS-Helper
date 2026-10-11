using System;
using System.Collections.Generic;

namespace PCBSCalculatorHelper
{
    // Resolve one action at a time. Call again after its animation finishes because
    // removing a part changes the game's live blockers and collision geometry.
    internal static class DependencyOrder
    {
        internal static T Next<T>(T target, Func<T, bool> ready,
            Func<T, IEnumerable<T>> blockers, IEqualityComparer<T> comparer) where T : class
        {
            return Visit(target, ready, blockers, new HashSet<T>(comparer));
        }

        private static T Visit<T>(T item, Func<T, bool> ready,
            Func<T, IEnumerable<T>> blockers, HashSet<T> visited) where T : class
        {
            if (item == null || !visited.Add(item)) return null;
            if (ready(item)) return item;
            foreach (T dependency in blockers(item))
            {
                T next = Visit(dependency, ready, blockers, visited);
                if (next != null) return next;
            }
            return null;
        }
    }
}
