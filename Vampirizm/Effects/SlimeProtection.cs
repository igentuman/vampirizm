using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Feeding;

namespace Vampirizm.Effects
{
    // Sun protection from a slime item. Shown as a status effect with the vanilla timer.
    // It is not saved: relogging loses the protection instead of refreshing it.
    internal static class SlimeProtection
    {
        internal const string EffectName = "VampirizmSlime";
        internal static readonly int EffectHash = EffectName.GetStableHashCode();
        private static StatusEffect _template;

        internal static bool IsSlimeItem(ItemDrop.ItemData item)
        {
            string id = BloodItemRegistry.PrefabId(item);
            return id != null && Plugin.Instance != null && id == Plugin.Instance.Settings.SlimeItem.Value.Trim();
        }

        internal static bool IsActive(Player player) => player.GetSEMan().HaveStatusEffect(EffectHash);

        internal static bool IsWet(Player player) => player.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectWet);

        // Returns true when the use request was handled here.
        internal static bool TryApply(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            var controller = player.GetComponent<VampireController>();
            if (controller == null || !controller.IsActiveVampire || !IsSlimeItem(item)) return false;
            if (!controller.CanChange)
            {
                player.Message(MessageHud.MessageType.Center, "$vampirizm_unavailable");
                return true;
            }
            if (IsWet(player))
            {
                // Wet skin washes slime off at once, so the item is kept.
                player.Message(MessageHud.MessageType.Center, "$vampirizm_slime_wet");
                return true;
            }
            if (!inventory.RemoveOneItem(item)) return true;
            float duration = Mathf.Max(600f, Plugin.Instance.Settings.SlimeDuration.Value);
            SEMan seman = player.GetSEMan();
            StatusEffect active = seman.GetStatusEffect(EffectHash);
            if (active != null)
            {
                active.m_ttl = duration;
                active.ResetTime();
            }
            else
            {
                StatusEffect template = Template(item);
                template.m_ttl = duration;
                seman.AddStatusEffect(template, resetTime: true);
            }
            string emote = Plugin.Instance.Settings.SlimeEmote.Value.Trim();
            if (emote.Length == 0 || !player.StartEmote(emote)) player.GetZAnim().SetTrigger("eat");
            player.Message(MessageHud.MessageType.Center, "$vampirizm_slime_applied");
            return true;
        }

        // Called every vampire tick: any Wet source removes the protection immediately.
        internal static void Tick(Player player)
        {
            if (IsActive(player) && IsWet(player))
            {
                player.GetSEMan().RemoveStatusEffect(EffectHash, quiet: true);
                player.Message(MessageHud.MessageType.Center, "$vampirizm_slime_washed");
            }
        }

        internal static void Remove(Player player) => player.GetSEMan().RemoveStatusEffect(EffectHash, quiet: true);

        private static StatusEffect Template(ItemDrop.ItemData item)
        {
            if (_template == null)
            {
                _template = ScriptableObject.CreateInstance<StatusEffect>();
                _template.name = EffectName;
                _template.m_name = "$vampirizm_se_slime";
                _template.m_tooltip = "$vampirizm_se_slime_tooltip";
                _template.hideFlags = HideFlags.HideAndDontSave;
            }
            if (_template.m_icon == null) _template.m_icon = item.GetIcon();
            return _template;
        }
    }
}
