using System;

namespace Vampirizm.Core
{
    // Passive eitr granted by the Vampirism skill, added on top of eitr from food.
    public static class EitrRules
    {
        // Whole skill levels gained since the unlock level, or -1 while still locked.
        public static int LevelsAboveUnlock(float level, float unlockLevel)
        {
            if (!BloodRules.IsFinite(level) || !BloodRules.IsFinite(unlockLevel)) return -1;
            float unlock = Math.Max(0f, Math.Min(unlockLevel, 100f));
            float current = (float)Math.Floor(BloodRules.Clamp(level, 100f));
            return current < unlock ? -1 : (int)(current - (float)Math.Ceiling(unlock));
        }

        public static float MaxEitr(float level, float unlockLevel, float baseEitr, float perLevel)
        {
            int levels = LevelsAboveUnlock(level, unlockLevel);
            if (levels < 0) return 0f;
            return Clamp(baseEitr, 1000f) + levels * Clamp(perLevel, 100f);
        }

        public static float Regen(float level, float unlockLevel, float baseRegen, float bonus, float levelsPerBonus)
        {
            int levels = LevelsAboveUnlock(level, unlockLevel);
            if (levels < 0) return 0f;
            float step = BloodRules.IsFinite(levelsPerBonus) ? Math.Max(1f, levelsPerBonus) : 5f;
            return Clamp(baseRegen, 100f) + (float)Math.Floor(levels / step) * Clamp(bonus, 100f);
        }

        // Vanilla regen per second is base × (2 − eitr/max). Dividing by that factor keeps the configured rate flat;
        // equipment and status effect multipliers are applied by vanilla afterwards.
        public static float VanillaRegenBase(float regen, float eitr, float maxEitr)
        {
            if (!BloodRules.IsFinite(regen) || regen <= 0f || !BloodRules.IsFinite(maxEitr) || maxEitr <= 0f) return 0f;
            float fill = BloodRules.IsFinite(eitr) ? Math.Max(0f, Math.Min(eitr / maxEitr, 1f)) : 1f;
            return regen / (2f - fill);
        }

        private static float Clamp(float value, float maximum)
            => BloodRules.IsFinite(value) ? Math.Max(0f, Math.Min(value, maximum)) : 0f;
    }
}
