using System.Collections.Generic;
using BepInEx.Configuration;
using Vampirizm.Core;
using Vampirizm.Feeding;

namespace Vampirizm.Combat
{
    // Silver is recognized by exact prefab ID, never by display name.
    internal static class SilverRegistry
    {
        private static ConfigEntry<string> _source;
        private static HashSet<string> _ids = new HashSet<string>();

        internal static void Initialize(ConfigEntry<string> source)
        {
            _source = source;
            _source.SettingChanged += (_, __) => Rebuild();
            Rebuild();
        }

        private static void Rebuild()
        {
            var errors = new List<string>();
            _ids = PrefabIdList.Parse(_source.Value, errors);
            if (errors.Count > 0) Plugin.Warn("Silver items: ignored entries: " + string.Join(", ", errors));
        }

        internal static bool IsSilver(ItemDrop.ItemData item)
        {
            string id = BloodItemRegistry.PrefabId(item);
            return id != null && _ids.Contains(id);
        }
    }
}
