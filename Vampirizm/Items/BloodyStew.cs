using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Vampirizm.Items
{
    // The serpent stew bowl, tinted blood-red; placeable in the world like a feast.
    internal static class BloodyStew
    {
        // Stable prefab ID used by inventories and saved food entries.
        internal const string PrefabName = "VampirizmBloodyStew";
        internal const float Blood = 35f;
        internal const float Duration = 25f * 60f;
        private static readonly Color Tint = new Color(0.9f, 0.3f, 0.3f);

        internal static bool IsStew(ItemDrop.ItemData item)
            => item?.m_dropPrefab != null && item.m_dropPrefab.name == PrefabName;

        internal static void Register() => PrefabManager.OnVanillaPrefabsAvailable += AddItem;

        private static void AddItem()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddItem;
            Sprite icon = ItemVisuals.LoadIcon("bloody_stew");
            var config = new ItemConfig
            {
                Name = "$item_vampirizm_bloody_stew",
                Description = "$item_vampirizm_bloody_stew_description",
                CraftingStation = "piece_preptable",
                MinStationLevel = 1,
                Amount = 1,
                Weight = 1f,
                StackSize = 20,
                Icons = new[] { icon },
                Requirements = new[]
                {
                    new RequirementConfig("DeerMeat", 1),
                    new RequirementConfig("RawMeat", 1),
                    new RequirementConfig("BjornMeat", 1),
                    new RequirementConfig("Bloodbag", 1)
                }
            };
            var item = new CustomItem(PrefabName, "SerpentStew", config);
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            shared.m_food = 0f;
            shared.m_foodStamina = 0f;
            shared.m_foodEitr = 0f;
            shared.m_foodRegen = 0f;
            shared.m_foodBurnTime = Duration;
            shared.m_consumeStatusEffect = null;
            ItemVisuals.Retexture(item.ItemPrefab, Tint,
                new KeyValuePair<string, string>("serpentstew", "tex_bloody_stew"));
            FoodPlacement.MakePlaceable(item.ItemPrefab, icon);
            ItemManager.Instance.AddItem(item);
        }
    }
}
