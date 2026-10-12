using UnityEngine;
using Vampirizm.Combat;

namespace Vampirizm.Effects
{
    // Silver in the inventory burns a vampire. Runs only on the local owner, so each tick is applied once.
    internal sealed class SilverContactController
    {
        private readonly SilverContact _contact = new SilverContact();

        internal void Tick(Player player, float dt)
        {
            var settings = Plugin.Instance.Settings;
            int stacks = settings.SilverContactDamage.Value > 0f ? SilverRegistry.ContactStacks(player.GetInventory()) : 0;
            bool started = stacks > 0 && !_contact.Touching;
            bool due = _contact.Update(stacks > 0, dt, settings.SilverContactInterval.Value);
            if (started) player.Message(MessageHud.MessageType.Center, "$vampirizm_silver_warning");
            if (!due) return;
            float damage = SilverContact.TickDamage(stacks, settings.SilverContactDamage.Value);
            if (damage <= 0f) return;
            // Generic damage: armor and resistances only reduce typed damage.
            var hit = new HitData();
            hit.m_damage.m_damage = damage;
            hit.m_hitType = HitData.HitType.Burning;
            hit.m_point = player.GetCenterPoint();
            hit.m_dir = Vector3.down;
            player.Damage(hit);
        }

        internal void Reset() => _contact.Reset();
    }
}
