using Vampirizm.Core;

namespace Vampirizm.Combat
{
    public enum SpiritRule
    {
        // Any spirit damage multiplies the whole hit.
        WholeHit,
        // Without silver only the spirit component is multiplied.
        SpiritOnly
    }

    public readonly struct DamageScale
    {
        public readonly float Whole;
        public readonly float Spirit;

        public DamageScale(float whole, float spirit)
        {
            Whole = whole;
            Spirit = spirit;
        }

        public bool IsIdentity => Whole == 1f && Spirit == 1f;
    }

    // Pure rules: silver or spirit weakness is applied exactly once per hit, never stacked.
    public static class VampireDamageRules
    {
        public static DamageScale Scale(bool silver, bool hasSpirit, SpiritRule rule, float multiplier)
        {
            float factor = BloodRules.IsFinite(multiplier) ? System.Math.Max(1f, System.Math.Min(multiplier, 100f)) : 1f;
            if (silver || (hasSpirit && rule == SpiritRule.WholeHit)) return new DamageScale(factor, 1f);
            if (hasSpirit) return new DamageScale(1f, factor);
            return new DamageScale(1f, 1f);
        }
    }
}
