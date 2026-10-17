using System;
using Vampirizm.Core;

namespace Vampirizm.Abilities.Orb
{
    public enum SwarmMode { None, Shield, Attack }

    public enum OrbBlock { None, NotVampire, Busy, SwarmBusy, Broken, NoEitrBar, NoEitr }

    // Pure Orb of Vampire rules: counts, durations, costs and cast checks.
    public static class OrbRules
    {
        public const int MaxQuality = 3;

        public static int Quality(int quality) => Math.Max(1, Math.Min(MaxQuality, quality));

        // Swarm and shield share the same count; every upgrade adds batsPerLevel.
        public static int BatCount(int quality, int baseBats, int batsPerLevel)
            => Math.Max(1, baseBats + Math.Max(0, batsPerLevel) * (Quality(quality) - 1));

        public static float ShieldDuration(int quality, float baseDuration, float perLevel)
        {
            float duration = Finite(baseDuration) + Math.Max(0f, Finite(perLevel)) * (Quality(quality) - 1);
            return Math.Max(1f, duration);
        }

        // Mirrors the vanilla staff formula: cost - cost * reduction * skillFactor.
        public static float EitrCost(float baseCost, float skillFactor, float reduction)
        {
            float cost = Math.Max(0f, Finite(baseCost));
            float factor = Clamp01(skillFactor);
            return cost - cost * Clamp01(reduction) * factor;
        }

        // A primary cast with an active shield turns the shield bats into the swarm instead of spawning new ones.
        public static bool ConvertsShield(SwarmMode mode, bool secondary) => !secondary && mode == SwarmMode.Shield;

        // Checked in this order so the player sees the most relevant reason; eitr and durability are only spent after all pass.
        public static OrbBlock CheckCast(bool isVampire, bool busy, SwarmMode mode, bool secondary, float durability,
            bool useDurability, float eitr, float maxEitr, float cost)
        {
            if (!isVampire) return OrbBlock.NotVampire;
            if (busy) return OrbBlock.Busy;
            // One swarm at a time; the shield waits until the swarm has returned, left or died.
            if (mode == SwarmMode.Attack) return OrbBlock.SwarmBusy;
            if (useDurability && !(durability > 0f)) return OrbBlock.Broken;
            float price = Math.Max(0f, Finite(cost));
            if (price <= 0f) return OrbBlock.None;
            if (!(maxEitr > 0f)) return OrbBlock.NoEitrBar;
            // Vanilla TryUseEitr keeps the same 0.1 margin.
            if (!BloodRules.IsFinite(eitr) || eitr < price + 0.1f) return OrbBlock.NoEitr;
            return OrbBlock.None;
        }

        public static bool InCone(float angleDegrees, float coneDegrees)
            => BloodRules.IsFinite(angleDegrees) && angleDegrees >= 0f && angleDegrees <= Math.Max(0f, Finite(coneDegrees)) * 0.5f;

        // First dives are staggered across the interval so bites ripple instead of landing all at once.
        public static float FirstDiveDelay(int index, int count, float interval)
        {
            if (count <= 0 || index < 0) return 0f;
            return Math.Max(0f, Finite(interval)) * (index % count) / count;
        }

        public static float ReturnBlood(int alive, float perBat) => Math.Max(0, alive) * Math.Max(0f, Finite(perBat));

        // Fraction of a hit the player still takes once a bat has absorbed it.
        public static float PassThrough(float absorb) => 1f - Clamp01(absorb);

        private static float Finite(float value) => BloodRules.IsFinite(value) ? value : 0f;

        private static float Clamp01(float value) => BloodRules.IsFinite(value) ? Math.Max(0f, Math.Min(1f, value)) : 0f;
    }
}
