using HarmonyLib;
using UnityEngine;
using Vampirizm.Conversion;
using Vampirizm.Core;
using Vampirizm.Effects;
using Vampirizm.Feeding;
using Vampirizm.Items;

namespace Vampirizm.Patches
{
    // Food is replaced only for the local vampire; ordinary characters keep every vanilla path.
    [HarmonyPatch]
    internal static class FoodPatches
    {
        private static VampireController ActiveVampire(Character character)
        {
            if (!(character is Player player) || player != Player.m_localPlayer) return null;
            var controller = player.GetComponent<VampireController>();
            return controller != null && controller.IsActiveVampire ? controller : null;
        }

        // Inventory, hotbar and gamepad use all arrive here: the rune for anyone, blood items and slime for vampires.
        [HarmonyPrefix, HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static bool BeforeUseItem(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui)
        {
            if (item == null || Plugin.Instance == null || !(__instance is Player player) || player != Player.m_localPlayer) return true;
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

        // Last guard for callers that skip CanConsumeItem: blood items go to BloodFeeding, ordinary food is refused.
        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
        private static bool BeforeEatFood(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (item == null || ActiveVampire(__instance) == null) return true;
            __result = BloodItemRegistry.IsBloodItem(item) && BloodFeeding.TryDrink(__instance, __instance.GetInventory(), item);
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
        private static void AfterGetTotalFoodValue(Player __instance, ref float hp, ref float stamina, ref float eitr)
            => ActiveVampire(__instance)?.ApplyFoodValues(ref hp, ref stamina, ref eitr);

        [HarmonyPostfix, HarmonyPatch(typeof(Player), "UpdateFood")]
        private static void AfterUpdateFood(Player __instance, float dt, bool forceUpdate)
        {
            if (!forceUpdate) ActiveVampire(__instance)?.TickFood(dt);
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
