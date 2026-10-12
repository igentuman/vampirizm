using System;

namespace Vampirizm.Core
{
    // Pure day-sleep timing. Vanilla day is 0.25..0.75 of the cycle and a vanilla sleep wakes
    // everyone at 0.15 of the next day. Vampires may also sleep from that morning until nightfall
    // and wake just after dusk, when the sun no longer burns.
    public static class SleepRules
    {
        public const float MorningFraction = 0.15f;
        public const float NightFraction = 0.75f;
        public const float DuskFraction = 0.76f;

        public static bool IsDaySleep(float dayFraction)
            => dayFraction >= MorningFraction && dayFraction < NightFraction;

        // Vanilla always wakes sleepers in the morning, so a wake in the second half of the day is a dusk wake.
        public static bool IsDuskWake(float dayFraction) => dayFraction >= 0.5f;

        // Network time of the next dusk after now.
        public static double DuskTime(double now, double dayLength)
        {
            if (!(dayLength > 0) || double.IsNaN(now) || double.IsInfinity(now)) return now;
            double dusk = (Math.Floor(now / dayLength) + DuskFraction) * dayLength;
            return dusk > now ? dusk : dusk + dayLength;
        }
    }
}
