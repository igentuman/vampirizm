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

        public static float TickDamage(float flat, float fractionOfMax, float maxHealth)
        {
            float damage = System.Math.Max(0f, flat) + System.Math.Max(0f, fractionOfMax) * System.Math.Max(0f, maxHealth);
            return float.IsNaN(damage) || float.IsInfinity(damage) ? 0f : damage;
        }
    }
}
