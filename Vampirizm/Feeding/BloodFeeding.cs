using System.Globalization;
using UnityEngine;
using Vampirizm.Core;

namespace Vampirizm.Feeding
{
    // The single path for drinking a blood item from the inventory or hotbar.
    internal static class BloodFeeding
    {
        // Returns true when the use request was handled here (successfully or with a refusal message).
        internal static bool TryDrink(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            if (!BloodItemRegistry.TryGetBlood(item, out float blood)) return false;
            var controller = player.GetComponent<VampireController>();
            if (controller == null || !controller.IsActiveVampire) return false;
            if (!controller.CanChange)
            {
                player.Message(MessageHud.MessageType.Center, "$vampirizm_unavailable");
                return true;
            }
            if (BloodRules.IsFull(controller.State.Blood, controller.Capacity))
            {
                // A full bar never wastes the item.
                player.Message(MessageHud.MessageType.Center, "$vampirizm_blood_full");
                return true;
            }
            if (!inventory.RemoveOneItem(item)) return true;
            float accepted = controller.AddBlood(blood);
            player.m_consumeItemEffects.Create(player.transform.position, Quaternion.identity, null, 1f, -1, player.GetZDOID());
            player.GetZAnim().SetTrigger("eat");
            player.Message(MessageHud.MessageType.Center, global::Localization.instance.Localize("$vampirizm_blood_gained",
                accepted.ToString("0.#", CultureInfo.InvariantCulture)));
            return true;
        }

        internal static string Tooltip(ItemDrop.ItemData item)
        {
            var controller = VampireController.Local;
            if (controller == null || !controller.IsActiveVampire) return null;
            if (BloodItemRegistry.TryGetBlood(item, out float blood))
                return global::Localization.instance.Localize("$vampirizm_tooltip_blood", blood.ToString("0.#", CultureInfo.InvariantCulture));
            if (Effects.SlimeProtection.IsSlimeItem(item))
                return global::Localization.instance.Localize("$vampirizm_tooltip_slime",
                    Plugin.Instance.Settings.SlimeDuration.Value.ToString("0", CultureInfo.InvariantCulture));
            return BloodItemRegistry.IsForbiddenConsumable(item) ? global::Localization.instance.Localize("$vampirizm_tooltip_forbidden") : null;
        }
    }
}
