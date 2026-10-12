namespace Vampirizm.Effects
{
    public enum SunEvent { None, Warning, Damage }

    // Pure timer: a warning when exposure starts, the first tick after the warning delay,
    // then one tick per interval. Leaving the sun resets the sequence.
    public sealed class SunExposure
    {
        private float _timer;
        public bool Exposed { get; private set; }

        public SunEvent Update(bool exposed, float dt, float warningDelay, float interval)
        {
            if (!exposed || float.IsNaN(dt) || dt < 0f)
            {
                Exposed = false;
                return SunEvent.None;
            }
            if (!Exposed)
            {
                Exposed = true;
                _timer = System.Math.Max(0f, warningDelay);
                return SunEvent.Warning;
            }
            _timer -= dt;
            if (_timer > 0f) return SunEvent.None;
            _timer += System.Math.Max(0.1f, interval);
            if (_timer <= 0f) _timer = System.Math.Max(0.1f, interval);
            return SunEvent.Damage;
        }

        public void Reset() => Exposed = false;

        public static bool IsExposed(bool day, bool sheltered, bool protectedBySlime)
            => day && !sheltered && !protectedBySlime;

        // Vanilla daytime is 0.25..0.75 of the smoothed day fraction and lasts 0.7 of the day length in real seconds.
        public const float DayStart = 0.25f;
        public const float DayEnd = 0.75f;
        public const float DaylightShare = 0.7f;

        // The sun is too low to burn for graceSeconds of real time after sunrise and before sunset.
        public static bool IsTwilight(float dayFraction, float graceSeconds, float dayLengthSeconds)
        {
            if (!(graceSeconds > 0f) || !(dayLengthSeconds > 0f) || float.IsNaN(dayFraction)) return false;
            float grace = graceSeconds * (DayEnd - DayStart) / (DaylightShare * dayLengthSeconds);
            return dayFraction < DayStart + grace || dayFraction > DayEnd - grace;
        }

        public static float TickDamage(float flat, float fractionOfMax, float maxHealth)
        {
            float damage = System.Math.Max(0f, flat) + System.Math.Max(0f, fractionOfMax) * System.Math.Max(0f, maxHealth);
            return float.IsNaN(damage) || float.IsInfinity(damage) ? 0f : damage;
        }

        // Weather and biome scale sunlight; a thunderstorm overrides rain. Zero means no burning at all.
        public static float Intensity(bool thunderstorm, bool rain, float stormMultiplier, float rainMultiplier, float biomeMultiplier)
        {
            float weather = thunderstorm ? stormMultiplier : rain ? rainMultiplier : 1f;
            float intensity = System.Math.Max(0f, weather) * System.Math.Max(0f, biomeMultiplier);
            return float.IsNaN(intensity) || float.IsInfinity(intensity) ? 0f : intensity;
        }

        // Vanilla thunderstorms: "ThunderStorm" and "Mistlands_thunder".
        public static bool IsThunderstorm(string environment)
            => !string.IsNullOrEmpty(environment) && environment.IndexOf("Thunder", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
