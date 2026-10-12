using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Vampirizm.Feeding
{
    // A drunk blood item also goes to the vanilla food list, so it follows the vanilla food rules: it shows in the
    // food HUD, blinks at half time and blocks drinking the same item again until then. The food entry gets its own
    // shared data with no food stats, because stored blood already grants health and stamina, and a configured burn
    // time, because raw meat has none.
    internal static class VampireFood
    {
        private static readonly MethodInfo CloneShared = AccessTools.Method(typeof(object), "MemberwiseClone");
        private static readonly Dictionary<string, ItemDrop.ItemData> Entries = new Dictionary<string, ItemDrop.ItemData>();

        // Vanilla Player.EatFood food limit.
        private const int Slots = 3;
        private static float BurnTime => Plugin.Instance.Settings.BloodFoodDuration.Value;

        // The most burnt food that vanilla would let a new food replace, or null.
        internal static Player.Food Replaceable(List<Player.Food> foods)
        {
            Player.Food replaced = null;
            foreach (Player.Food food in foods)
                if (food.CanEatAgain() && (replaced == null || food.m_time < replaced.m_time)) replaced = food;
            return replaced;
        }

        internal static bool CanDrink(Player player, ItemDrop.ItemData item)
        {
            string id = BloodItemRegistry.PrefabId(item);
            return id != null && player.CanEat(Entry(id, item), true);
        }

        // Called after CanDrink allowed the item: refreshes the same blood food or takes a free or replaceable slot, like vanilla EatFood.
        internal static void Fill(Player player, ItemDrop.ItemData item)
        {
            string id = BloodItemRegistry.PrefabId(item);
            if (id == null) return;
            ItemDrop.ItemData entry = Entry(id, item);
            List<Player.Food> foods = player.GetFoods();
            foreach (Player.Food food in foods)
            {
                if (food.m_item.m_shared.m_name != entry.m_shared.m_name) continue;
                food.m_item = entry;
                food.m_time = entry.m_shared.m_foodBurnTime;
                return;
            }
            if (foods.Count >= Slots) foods.Remove(Replaceable(foods));
            foods.Add(new Player.Food { m_name = id, m_item = entry, m_time = entry.m_shared.m_foodBurnTime });
        }

        // Vanilla Load points saved foods at the item prefab, and a changed burn time leaves stale entries behind.
        internal static void Rewrap(Player player)
        {
            if (Plugin.Instance == null) return;
            foreach (Player.Food food in player.GetFoods())
            {
                if (food.m_item == null || !BloodItemRegistry.IsBloodId(food.m_name)) continue;
                food.m_item = Entry(food.m_name, food.m_item);
            }
        }

        private static ItemDrop.ItemData Entry(string id, ItemDrop.ItemData source)
        {
            float burnTime = BurnTime;
            if (Entries.TryGetValue(id, out ItemDrop.ItemData cached) && cached.m_shared.m_foodBurnTime == burnTime) return cached;
            ItemDrop.ItemData entry = source.Clone();
            var shared = (ItemDrop.ItemData.SharedData)CloneShared.Invoke(source.m_shared, null);
            shared.m_food = 0f;
            shared.m_foodStamina = 0f;
            shared.m_foodEitr = 0f;
            shared.m_foodRegen = 0f;
            shared.m_foodBurnTime = burnTime;
            entry.m_shared = shared;
            entry.m_stack = 1;
            Entries[id] = entry;
            return entry;
        }
    }
}
