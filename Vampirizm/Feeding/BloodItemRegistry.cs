using System.Collections.Generic;
using BepInEx.Configuration;

namespace Vampirizm.Feeding
{
    // Runtime view of the server-synchronized blood item list.
    internal static class BloodItemRegistry
    {
        private static ConfigEntry<string> _source;
        private static Dictionary<string, float> _table = new Dictionary<string, float>();

        internal static void Initialize(ConfigEntry<string> source)
        {
            _source = source;
            _source.SettingChanged += (_, __) => Rebuild();
            Rebuild();
        }

        private static void Rebuild()
        {
            var errors = new List<string>();
            _table = BloodItemTable.Parse(_source.Value, errors);
            if (errors.Count > 0) Plugin.Warn("Blood items: ignored or overridden entries: " + string.Join(", ", errors));
        }

        internal static string PrefabId(ItemDrop.ItemData item)
            => item?.m_dropPrefab != null ? item.m_dropPrefab.name : null;

        internal static bool TryGetBlood(ItemDrop.ItemData item, out float blood)
        {
            blood = 0f;
            string id = PrefabId(item);
            return id != null && _table.TryGetValue(id, out blood);
        }

        internal static bool IsBloodItem(ItemDrop.ItemData item) => TryGetBlood(item, out _);

        // Every vanilla food and mead: anything consumable that feeds or applies a status effect.
        // Consumable ammo such as fishing bait has neither and stays usable.
        internal static bool IsForbiddenConsumable(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable) return false;
            if (IsBloodItem(item)) return false;
            ItemDrop.ItemData.SharedData shared = item.m_shared;
            return shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f ||
                   shared.m_consumeStatusEffect != null;
        }
    }
}
