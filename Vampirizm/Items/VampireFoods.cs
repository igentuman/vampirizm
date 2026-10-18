using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Vampirizm.Items
{
    // Prepared vampire foods share the same blood feeding path as Bloody Stew.
    // Each clones a matching vanilla prefab for its world model, tinted blood-red, and can be placed like a feast.
    internal static class VampireFoods
    {
        internal const string JerkyId = "VampirizmCrimsonJerky";
        internal const string TartareId = "VampirizmHuntersTartare";
        internal const string PuddingId = "VampirizmBlackBloodPudding";
        internal const string JellyId = "VampirizmSeekerBloodJelly";
        internal const string AspicId = "VampirizmSerpentBloodAspic";

        private sealed class Definition
        {
            internal string Id, Name, Description, Icon, Station;
            // Vanilla prefab whose world model (and drop physics) the item clones. Textures maps the
            // model's vanilla texture names to embedded blood variants; Tint is only the fallback.
            internal string Model;
            internal Color Tint;
            internal KeyValuePair<string, string>[] Textures;
            internal float Blood;
            internal string[] Ingredients;
            internal int[] Amounts;
            // Skills raised by SkillBonus while the food stays in the food list.
            internal global::Skills.SkillType[] Buffs;
            // Max eitr added and eitr regen fraction (0.2 = +20%) while the food stays in the food list.
            internal float Eitr, EitrRegen;
        }

        internal const float SkillBonus = 15f;

        private static readonly Definition[] Foods =
        {
            new Definition { Id = JerkyId, Name = "item_vampirizm_crimson_jerky", Description = "item_vampirizm_crimson_jerky_description", Icon = "crimson_jerky", Station = "piece_preptable", Blood = 20f,
                Model = "WolfJerky", Tint = new Color(0.9f, 0.3f, 0.35f), Textures = new[] { Tex("WolfJerky_d", "tex_crimson_jerky") },
                Ingredients = new[] { "DeerMeat", "Bloodbag" }, Amounts = new[] { 2, 1 },
                Buffs = new[] { global::Skills.SkillType.Run } },
            new Definition { Id = TartareId, Name = "item_vampirizm_hunters_tartare", Description = "item_vampirizm_hunters_tartare_description", Icon = "hunters_tartare", Station = "piece_preptable", Blood = 30f,
                Model = "MinceMeatSauce", Tint = new Color(0.85f, 0.25f, 0.3f), Textures = new[] { Tex("MinceMeatSauce_d", "tex_hunters_tartare") },
                Ingredients = new[] { "WolfMeat", "Bloodbag", "Onion" }, Amounts = new[] { 1, 1, 1 },
                Buffs = new[] { global::Skills.SkillType.Swords, global::Skills.SkillType.Knives, global::Skills.SkillType.Spears, global::Skills.SkillType.Bows } },
            new Definition { Id = PuddingId, Name = "item_vampirizm_black_blood_pudding", Description = "item_vampirizm_black_blood_pudding_description", Icon = "black_blood_pudding", Station = "piece_cauldron", Blood = 35f,
                Model = "BloodPudding", Tint = new Color(0.45f, 0.25f, 0.3f), Textures = new[] { Tex("bloodpudding_d", "tex_black_blood_pudding") },
                Ingredients = new[] { "GiantBloodSack", "BarleyFlour", "Cloudberry" }, Amounts = new[] { 1, 2, 2 },
                Buffs = new[] { global::Skills.SkillType.Blocking } },
            new Definition { Id = JellyId, Name = "item_vampirizm_seeker_blood_jelly", Description = "item_vampirizm_seeker_blood_jelly_description", Icon = "seeker_blood_jelly", Station = "piece_cauldron", Blood = 45f,
                Model = "RoyalJelly", Tint = new Color(0.95f, 0.3f, 0.35f), Textures = new[] { Tex("Jelly_d", "tex_seeker_blood_jelly"), Tex("JellyOuter", "tex_seeker_blood_jelly_outer") },
                Ingredients = new[] { "GiantBloodSack", "BugMeat", "RoyalJelly" }, Amounts = new[] { 1, 2, 2 },
                Buffs = new[] { global::Skills.SkillType.BloodMagic, global::Skills.SkillType.Jump }, Eitr = 20f, EitrRegen = 0.2f },
            new Definition { Id = AspicId, Name = "item_vampirizm_serpent_blood_aspic", Description = "item_vampirizm_serpent_blood_aspic_description", Icon = "serpent_blood_aspic", Station = "piece_cauldron", Blood = 40f,
                Model = "SeekerAspic", Tint = new Color(0.9f, 0.25f, 0.3f), Textures = new[] { Tex("Seeker Aspic_d", "tex_serpent_blood_aspic"), Tex("JellyOuter", "tex_seeker_blood_jelly_outer") },
                Ingredients = new[] { "SerpentMeat", "GiantBloodSack" }, Amounts = new[] { 2, 1 },
                Buffs = new[] { global::Skills.SkillType.Swim }, Eitr = 25f, EitrRegen = 0.2f }
        };

        internal static bool IsFood(ItemDrop.ItemData item) => Get(item?.m_dropPrefab != null ? item.m_dropPrefab.name : null) != null;
        internal static float BloodFor(string id) => Get(id)?.Blood ?? 0f;
        internal static bool IsFoodId(string id) => Get(id) != null;

        // Vanilla removes a food entry once it burns out, so only undepleted foods count.
        internal static float SkillLevelBonus(Player player, global::Skills.SkillType skill)
        {
            float bonus = 0f;
            foreach (Player.Food food in player.GetFoods())
            {
                global::Skills.SkillType[] buffs = Get(food.m_name)?.Buffs;
                if (buffs != null && System.Array.IndexOf(buffs, skill) >= 0) bonus += SkillBonus;
            }
            return bonus;
        }

        internal static float EitrBonus(Player player)
        {
            float bonus = 0f;
            foreach (Player.Food food in player.GetFoods()) bonus += Get(food.m_name)?.Eitr ?? 0f;
            return bonus;
        }

        // Fractions from different foods add up: two +20% foods give +40%.
        internal static float EitrRegenBonus(Player player)
        {
            float bonus = 0f;
            foreach (Player.Food food in player.GetFoods()) bonus += Get(food.m_name)?.EitrRegen ?? 0f;
            return bonus;
        }

        internal static global::Skills.SkillType[] BuffsFor(ItemDrop.ItemData item)
            => Get(item?.m_dropPrefab != null ? item.m_dropPrefab.name : null)?.Buffs;

        internal static float EitrFor(ItemDrop.ItemData item)
            => Get(item?.m_dropPrefab != null ? item.m_dropPrefab.name : null)?.Eitr ?? 0f;

        internal static float EitrRegenFor(ItemDrop.ItemData item)
            => Get(item?.m_dropPrefab != null ? item.m_dropPrefab.name : null)?.EitrRegen ?? 0f;

        internal static void Register() => PrefabManager.OnVanillaPrefabsAvailable += AddItems;

        private static void AddItems()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddItems;
            foreach (Definition food in Foods)
            {
                var requirements = new RequirementConfig[food.Ingredients.Length];
                for (int i = 0; i < requirements.Length; i++) requirements[i] = new RequirementConfig(food.Ingredients[i], food.Amounts[i]);
                Sprite icon = ItemVisuals.LoadIcon(food.Icon);
                var config = new ItemConfig
                {
                    Name = "$" + food.Name,
                    Description = "$" + food.Description,
                    CraftingStation = food.Station,
                    MinStationLevel = 1,
                    Amount = 1,
                    Weight = 1f,
                    StackSize = 20,
                    Icons = new[] { icon },
                    Requirements = requirements
                };
                var custom = new CustomItem(food.Id, food.Model, config);
                ItemDrop.ItemData.SharedData shared = custom.ItemDrop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
                shared.m_food = shared.m_foodStamina = shared.m_foodEitr = shared.m_foodRegen = 0f;
                shared.m_foodBurnTime = 20f * 60f;
                shared.m_consumeStatusEffect = null;
                ItemVisuals.Retexture(custom.ItemPrefab, food.Tint, food.Textures);
                FoodPlacement.MakePlaceable(custom.ItemPrefab, icon);
                ItemManager.Instance.AddItem(custom);
            }
        }


        private static KeyValuePair<string, string> Tex(string vanilla, string resource)
            => new KeyValuePair<string, string>(vanilla, resource);

        private static Definition Get(string id)
        {
            foreach (Definition food in Foods) if (food.Id == id) return food;
            return null;
        }
    }
}
