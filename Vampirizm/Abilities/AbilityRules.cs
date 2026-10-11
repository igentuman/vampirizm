using System;
using Vampirizm.Core;

namespace Vampirizm.Abilities
{
    public enum AbilityBlock { None, NotVampire, Locked, Busy, Cooldown, NoBlood, NoRoom }

    // Pure ability rules shared by all bound vampire actions.
    public static class AbilityRules
    {
        public static bool IsUnlocked(float level, float required)
            => BloodRules.IsFinite(level) && BloodRules.IsFinite(required) && level >= Math.Max(0f, required);

        // Checked in this order so the player sees the most relevant reason; blood is only spent after all checks pass.
        public static AbilityBlock Check(bool isVampire, float level, float required, bool busy, float now, float readyAt,
            float blood, float cost)
        {
            if (!isVampire) return AbilityBlock.NotVampire;
            if (!IsUnlocked(level, required)) return AbilityBlock.Locked;
            if (busy) return AbilityBlock.Busy;
            if (now < readyAt) return AbilityBlock.Cooldown;
            float price = BloodRules.IsFinite(cost) ? Math.Max(0f, cost) : float.MaxValue;
            if (!BloodRules.IsFinite(blood) || blood < price) return AbilityBlock.NoBlood;
            return AbilityBlock.None;
        }

        // The dash stops short of the first obstacle on its path; obstacle is +Infinity for a clear path.
        public static float DashDistance(float maxDistance, float obstacle, float clearance)
        {
            if (!BloodRules.IsFinite(maxDistance) || maxDistance <= 0f || float.IsNaN(obstacle)) return 0f;
            float stop = float.IsPositiveInfinity(obstacle) ? maxDistance : obstacle - Math.Max(0f, clearance);
            return Math.Max(0f, Math.Min(maxDistance, stop));
        }
    }
}
