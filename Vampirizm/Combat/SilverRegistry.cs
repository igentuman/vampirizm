using System.Collections.Generic;
using BepInEx.Configuration;
using Vampirizm.Core;
using Vampirizm.Feeding;

namespace Vampirizm.Combat
{
    // Silver is recognized by exact prefab ID, never by display name.
    internal static class SilverRegistry
    {
        private static ConfigEntry<string> _weaponSource;
        private static ConfigEntry<string> _contactSource;
        private static HashSet<string> _weapons = new HashSet<string>();
        private static HashSet<string> _contact = new HashSet<string>();

        internal static void Initialize(ConfigEntry<string> weapons, ConfigEntry<string> contact)
        {
            _weaponSource = weapons;
            _contactSource = contact;
            _weaponSource.SettingChanged += (_, __) => _weapons = Rebuild(_weaponSource, "Silver items");
            _contactSource.SettingChanged += (_, __) => _contact = Rebuild(_contactSource, "Silver contact items");
            _weapons = Rebuild(_weaponSource, "Silver items");
            _contact = Rebuild(_contactSource, "Silver contact items");
        }

        private static HashSet<string> Rebuild(ConfigEntry<string> source, string name)
        {
            var errors = new List<string>();
            HashSet<string> ids = PrefabIdList.Parse(source.Value, errors);
            if (errors.Count > 0) Plugin.Warn(name + ": ignored entries: " + string.Join(", ", errors));
            return ids;
        }

        internal static bool IsSilver(ItemDrop.ItemData item) => Matches(_weapons, item);

        // Every inventory slot counts as one stack, equipped items included.
        internal static int ContactStacks(Inventory inventory)
        {
            if (inventory == null || _contact.Count == 0) return 0;
            int stacks = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                if (Matches(_contact, item)) stacks++;
            return stacks;
        }

        private static bool Matches(HashSet<string> ids, ItemDrop.ItemData item)
        {
            string id = BloodItemRegistry.PrefabId(item);
            return id != null && ids.Contains(id);
        }
    }
}
