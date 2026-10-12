using HarmonyLib;
using UnityEngine;
using Vampirizm.Configuration;

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
            float intensity = Intensity(player, settings);
            bool exposed = intensity > 0f && SunExposure.IsExposed(IsBurningDay(settings), IsSheltered(player), SlimeProtection.IsActive(player));
            switch (_exposure.Update(exposed, dt, settings.SunWarning.Value, settings.SunInterval.Value))
            {
                case SunEvent.Warning:
                    player.Message(MessageHud.MessageType.Center, "$vampirizm_sun_warning");
                    break;
                case SunEvent.Damage:
                    float damage = intensity * SunExposure.TickDamage(settings.SunDamage.Value, settings.SunDamageFraction.Value, player.GetMaxHealth());
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

        private static bool IsBurningDay(VampireConfig settings)
        {
            EnvMan env = EnvMan.instance;
            return EnvMan.IsDay() && env != null
                && !SunExposure.IsTwilight(env.GetDayFraction(), settings.SunTwilight.Value, env.m_dayLengthSec);
        }

        // Wet weather counts as rain; the thunderstorm is recognized by environment name.
        private static float Intensity(Player player, VampireConfig settings)
        {
            EnvSetup environment = EnvMan.instance != null ? EnvMan.instance.GetCurrentEnvironment() : null;
            return SunExposure.Intensity(SunExposure.IsThunderstorm(environment?.m_name), EnvMan.IsWet(),
                settings.ThunderstormSunMultiplier.Value, settings.RainSunMultiplier.Value,
                BiomeMultiplier(player.GetCurrentBiome(), settings));
        }

        private static float BiomeMultiplier(Heightmap.Biome biome, VampireConfig settings)
        {
            switch (biome)
            {
                case Heightmap.Biome.Swamp: return settings.SwampSunMultiplier.Value;
                case Heightmap.Biome.Mistlands: return settings.MistlandsSunMultiplier.Value;
                case Heightmap.Biome.AshLands: return settings.AshlandsSunMultiplier.Value;
                default: return 1f;
            }
        }
    }
}
