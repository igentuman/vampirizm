using System;

namespace Vampirizm.Core
{
    // Pure rules, independent of Unity and network ownership.
    public static class BloodRules
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static float Clamp(float value, float maximum)
        {
            if (!IsFinite(value) || !IsFinite(maximum) || maximum <= 0f) return 0f;
            return Math.Max(0f, Math.Min(value, maximum));
        }

        public static float Capacity(float level, float baseCapacity, float perLevel)
        {
            float basis = IsFinite(baseCapacity) ? Math.Max(1f, Math.Min(baseCapacity, 10000f)) : 100f;
            float growth = IsFinite(perLevel) ? Math.Max(0f, Math.Min(perLevel, 1000f)) : 2f;
            return basis + Clamp(level, 100f) * growth;
        }

        // A constant bonus per blood unit prevents loss of stats when leveling up.
        public static float StatBonus(float blood, float capacity, float bonusPerBlood)
            => Clamp(blood, capacity) * Clamp(bonusPerBlood, 100f);

        // Items are refused at a full bar; a small epsilon absorbs float drift.
        public static bool IsFull(float blood, float capacity) => Clamp(blood, capacity) >= Clamp(capacity, float.MaxValue) - 0.01f;

        // Passive loss for an active-play interval, scaled like vanilla food burn.
        public static float PassiveDrain(float perMinute, float seconds, float rate)
        {
            if (!IsFinite(perMinute) || !IsFinite(seconds) || !IsFinite(rate)) return 0f;
            return Math.Max(0f, perMinute) / 60f * Math.Max(0f, seconds) * Math.Max(0f, rate);
        }
    }
}
