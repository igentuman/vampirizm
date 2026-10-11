using HarmonyLib;
using UnityEngine;

namespace Vampirizm.Effects
{
    // Daytime outdoors hurts a vampire. Runs only on the local owner, so each tick is applied once.
    internal sealed class SunlightController
    {
        private static readonly AccessTools.FieldRef<Player, bool> UnderRoof =
            AccessTools.FieldRefAccess<Player, bool>("m_underRoof");

        private readonly SunExposure _exposure = new SunExposure();

        internal bool Exposed => _exposure.Exposed;

        // Roofs and interiors (dungeons, crypts) protect; clouds, fog and tree shade do not.
        internal static bool IsSheltered(Player player) => player.InInterior() || UnderRoof(player);

        internal void Tick(Player player, float dt)
        {
            var settings = Plugin.Instance.Settings;
            bool exposed = SunExposure.IsExposed(EnvMan.IsDay(), IsSheltered(player), SlimeProtection.IsActive(player));
            switch (_exposure.Update(exposed, dt, settings.SunWarning.Value, settings.SunInterval.Value))
            {
                case SunEvent.Warning:
                    player.Message(MessageHud.MessageType.Center, "$vampirizm_sun_warning");
                    break;
                case SunEvent.Damage:
                    float damage = SunExposure.TickDamage(settings.SunDamage.Value, settings.SunDamageFraction.Value, player.GetMaxHealth());
                    if (damage <= 0f) break;
                    // Generic damage: armor and resistances only reduce typed damage.
                    var hit = new HitData();
                    hit.m_damage.m_damage = damage;
                    hit.m_hitType = HitData.HitType.Burning;
                    hit.m_point = player.GetCenterPoint();
                    hit.m_dir = Vector3.down;
                    player.Damage(hit);
                    break;
            }
        }

        internal void Reset() => _exposure.Reset();
    }
}
