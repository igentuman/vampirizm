using System.Globalization;
using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Items;
using Vampirizm.Skills;

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
            if (!Allowed(player, controller, item)) return true;
            if (!inventory.RemoveOneItem(item)) return true;
            Consume(player, controller, item, blood);
            return true;
        }

        // Gate for eating food placed in the world: refuses with a message before vanilla removes the piece.
        internal static bool CanDrinkPlaced(Player player, ItemDrop.ItemData item)
        {
            var controller = player.GetComponent<VampireController>();
            if (controller == null || !controller.IsActiveVampire) return false;
            return Allowed(player, controller, item);
        }

        // Grants the blood of a placed food that vanilla ItemDrop.Eat has already consumed; no inventory involved.
        internal static void DrinkPlaced(Player player, ItemDrop.ItemData item)
        {
            if (!BloodItemRegistry.TryGetBlood(item, out float blood)) return;
            var controller = player.GetComponent<VampireController>();
            if (controller == null || !controller.IsActiveVampire) return;
            Consume(player, controller, item, blood);
        }

        // Shows its own refusal message when drinking is not possible right now.
        private static bool Allowed(Player player, VampireController controller, ItemDrop.ItemData item)
        {
            if (!controller.CanChange)
            {
                player.Message(MessageHud.MessageType.Center, "$vampirizm_unavailable");
                return false;
            }
            if (BloodRules.IsFull(controller.State.Blood, controller.Capacity))
            {
                // A full bar never wastes the item.
                player.Message(MessageHud.MessageType.Center, "$vampirizm_blood_full");
                return false;
            }
            // The vanilla food check refusal shows the vanilla "can't eat more" or "full" message.
            return VampireFood.CanDrink(player, item);
        }

        private static void Consume(Player player, VampireController controller, ItemDrop.ItemData item, float blood)
        {
            VampireFood.Fill(player, item);
            float accepted = controller.AddBlood(blood);
            float xp = DrainRules.Experience(accepted, Plugin.Instance.Settings.XpPerItemBlood.Value, true);
            if (xp > 0f) player.RaiseSkill(VampirismSkill.Type, xp);
            player.m_consumeItemEffects.Create(player.transform.position, Quaternion.identity, null, 1f, -1, player.GetZDOID());
            player.GetZAnim().SetTrigger("eat");
            player.Message(MessageHud.MessageType.Center, global::Localization.instance.Localize("$vampirizm_blood_gained",
                accepted.ToString("0.#", CultureInfo.InvariantCulture)));
        }

        internal static string Tooltip(ItemDrop.ItemData item)
        {
            var controller = VampireController.Local;
            if (controller == null || !controller.IsActiveVampire) return null;
            if (BloodItemRegistry.TryGetBlood(item, out float blood))
            {
                string text = global::Localization.instance.Localize("$vampirizm_tooltip_blood", blood.ToString("0.#", CultureInfo.InvariantCulture));
                global::Skills.SkillType[] buffs = VampireFoods.BuffsFor(item);
                if (buffs == null) return text;
                foreach (global::Skills.SkillType skill in buffs)
                    text += "\n" + global::Localization.instance.Localize("$vampirizm_tooltip_skill",
                        VampireFoods.SkillBonus.ToString("0", CultureInfo.InvariantCulture), global::Localization.instance.Localize("$skill_" + skill.ToString().ToLower()));
                float eitr = VampireFoods.EitrFor(item);
                if (eitr > 0f)
                    text += "\n" + global::Localization.instance.Localize("$vampirizm_tooltip_eitr", eitr.ToString("0", CultureInfo.InvariantCulture));
                float eitrRegen = VampireFoods.EitrRegenFor(item);
                if (eitrRegen > 0f)
                    text += "\n" + global::Localization.instance.Localize("$vampirizm_tooltip_eitr_regen",
                        (eitrRegen * 100f).ToString("0", CultureInfo.InvariantCulture));
                return text;
            }
            if (Effects.SlimeProtection.IsSlimeItem(item))
                return global::Localization.instance.Localize("$vampirizm_tooltip_slime",
                    Plugin.Instance.Settings.SlimeDuration.Value.ToString("0", CultureInfo.InvariantCulture));
            return BloodItemRegistry.IsForbiddenConsumable(item) ? global::Localization.instance.Localize("$vampirizm_tooltip_forbidden") : null;
        }
    }
}
