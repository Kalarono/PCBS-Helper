using System.Collections.Generic;
using BepInEx.Configuration;

namespace PCBSCalculatorHelper
{
    internal sealed class ShopSortSettings
    {
        private readonly ConfigFile config;
        private readonly Dictionary<string, ConfigEntry<int>> settings = new Dictionary<string, ConfigEntry<int>>();
        internal ShopSortSettings(ConfigFile config) { this.config = config; }
        internal ConfigEntry<int> For(string shopID)
        {
            ConfigEntry<int> setting;
            if (!settings.TryGetValue(shopID, out setting))
            {
                setting = config.Bind("Shop", "Sort_" + shopID, -1,
                    "Remember this shop's Sort by selection. -1 uses the game's default; 0 relevance, 1 price ascending, 2 price descending.");
                settings.Add(shopID, setting);
            }
            return setting;
        }
    }
}
