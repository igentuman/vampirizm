using HarmonyLib;
using UnityEngine;
using Vampirizm.Conversion;
using Vampirizm.Core;
using Vampirizm.Effects;
using Vampirizm.Feeding;
using Vampirizm.Items;

namespace Vampirizm.Patches
{
    // Blood feeding is local to the vampire; custom vampire food is also refused for ordinary characters.
    [HarmonyPatch]
    internal static class FoodPatches
    {
        private static VampireController ActiveVampire(Character character)
        {
            if (!(character is Player player) || player != Player.m_localPlayer) return null;
            var controller = player.GetComponent<VampireController>();
            return controller != null && controller.IsActiveVampire ? controller : null;
        }

        private static bool RefuseVampireFood(Player player, ItemDrop.ItemData item)
        {
            if (!BloodyStew.IsStew(item) && !VampireFoods.IsFood(item)) return false;
            var controller = player.GetComponent<VampireController>();
            if (controller != null && controller.IsActiveVampire) return false;
            player.Message(MessageHud.MessageType.Center, "$vampirizm_food_vampire_only");
            return true;
        }

        // Inventory, hotbar and gamepad use all arrive here: the rune for anyone, blood items and slime for vampires.
        [HarmonyPrefix, HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static bool BeforeUseItem(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui)
        {
            if (item == null || Plugin.Instance == null || !(__instance is Player player) || player != Player.m_localPlayer) return true;
            if (RefuseVampireFood(player, item)) return false;
            bool rune = VampireRune.IsRune(item);
            bool blood = !rune && ActiveVampire(player) != null && BloodItemRegistry.IsBloodItem(item);
            bool slime = !rune && !blood && ActiveVampire(player) != null && SlimeProtection.IsSlimeItem(item);
            if (!rune && !blood && !slime) return true;
            if (inventory == null) inventory = player.GetInventory();
            if (!inventory.ContainsItem(item)) return true;
            // Hovered interactables (cooking stations, item stands) keep their vanilla item use.
            if (!fromInventoryGui)
            {
                GameObject hover = player.GetHoverObject();
                if (hover != null && hover.GetComponentInParent<Interactable>() != null) return true;
            }
            if (rune) return !VampireConversion.TryRitual(player, inventory, item);
            return blood ? !BloodFeeding.TryDrink(player, inventory, item) : !SlimeProtection.TryApply(player, inventory, item);
        }

        // Covers inventory consumption, placed food, feasts and consumable ammo checks.
        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.CanConsumeItem))]
        private static bool BeforeCanConsumeItem(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (RefuseVampireFood(__instance, item))
            {
                __result = false;
                return false;
            }
            if (item == null || ActiveVampire(__instance) == null) return true;
            // Keep the inventory action enabled so Humanoid.UseItem can route the request to BloodFeeding.
            // The UseItem prefix consumes it here and EatFood reroutes it as a final vanilla guard.
            if (BloodItemRegistry.IsBloodItem(item))
            {
                __result = true;
                return false;
            }
            if (!BloodItemRegistry.IsForbiddenConsumable(item)) return true;
            __instance.Message(MessageHud.MessageType.Center, "$vampirizm_food_forbidden");
            __result = false;
            return false;
        }

        // Direct consumers must also go through blood feeding: this food has no vanilla health stat,
        // so vanilla ConsumeItem would otherwise remove it without calling EatFood.
        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
        private static bool BeforeConsumeItem(Player __instance, Inventory inventory, ItemDrop.ItemData item, ref bool __result)
        {
            if (!BloodyStew.IsStew(item) && !VampireFoods.IsFood(item)) return true;
            __result = false;
            if (RefuseVampireFood(__instance, item) || ActiveVampire(__instance) == null || Plugin.Instance == null) return false;
            if (inventory == null) inventory = __instance.GetInventory();
            if (!inventory.ContainsItem(item)) return false;
            int stack = item.m_stack;
            BloodFeeding.TryDrink(__instance, inventory, item);
            __result = item.m_stack < stack || !inventory.ContainsItem(item);
            return false;
        }

        // Last guard for callers that skip CanConsumeItem: blood items go to BloodFeeding, ordinary food is refused.
        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
        private static bool BeforeEatFood(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (RefuseVampireFood(__instance, item))
            {
                __result = false;
                return false;
            }
            if (item == null || ActiveVampire(__instance) == null) return true;
            __result = BloodItemRegistry.IsBloodItem(item) && BloodFeeding.TryDrink(__instance, __instance.GetInventory(), item);
            return false;
        }

        // Vampire food placed in the world: vanilla ItemDrop.Eat removes the piece but grants nothing
        // (the item has no vanilla food stats), so gate it first and route the gain to blood feeding after.
        [HarmonyPrefix, HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Eat))]
        private static bool BeforeDropEat(ItemDrop __instance, ref bool __result)
        {
            ItemDrop.ItemData item = __instance.m_itemData;
            if (!BloodyStew.IsStew(item) && !VampireFoods.IsFood(item)) return true;
            Player player = Player.m_localPlayer;
            // Non-vampires are refused with a message by the CanConsumeItem prefix inside vanilla Eat.
            if (player == null || ActiveVampire(player) == null) return true;
            if (BloodFeeding.CanDrinkPlaced(player, item)) return true;
            __result = false;
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Eat))]
        private static void AfterDropEat(ItemDrop __instance, bool __result)
        {
            if (!__result) return;
            ItemDrop.ItemData item = __instance.m_itemData;
            if (!BloodyStew.IsStew(item) && !VampireFoods.IsFood(item)) return;
            Player player = Player.m_localPlayer;
            if (player != null) BloodFeeding.DrinkPlaced(player, item);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
        private static void AfterGetTotalFoodValue(Player __instance, ref float hp, ref float stamina, ref float eitr)
            => ActiveVampire(__instance)?.ApplyFoodValues(ref hp, ref stamina, ref eitr);

        [HarmonyPostfix, HarmonyPatch(typeof(Player), "UpdateFood")]
        private static void AfterUpdateFood(Player __instance, float dt, bool forceUpdate)
        {
            if (!forceUpdate) ActiveVampire(__instance)?.TickFood(dt);
        }

        // Vanilla Skills.GetSkillLevel and GetSkillFactor both pass through here, so the skills dialog shows the bonus too.
        [HarmonyPostfix, HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifySkillLevel))]
        private static void AfterModifySkillLevel(Character ___m_character, global::Skills.SkillType skill, ref float level)
        {
            if (ActiveVampire(___m_character) != null)
                level += VampireFoods.SkillLevelBonus((Player)___m_character, skill);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
             typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
        private static void AfterGetTooltip(ItemDrop.ItemData item, ref string __result)
        {
            string extra = BloodFeeding.Tooltip(item);
            if (extra != null) __result += "\n" + extra;
        }
    }
}
