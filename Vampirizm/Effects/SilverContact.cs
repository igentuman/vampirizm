namespace Vampirizm.Effects
{
    // Pure timer: one silver tick per interval while the inventory holds silver.
    // Damage scales with the number of stacks, not with the number of items in a stack.
    public sealed class SilverContact
    {
        private float _timer;
        public bool Touching { get; private set; }

        // Returns true when a damage tick is due. The first tick comes one interval after the first contact.
        public bool Update(bool touching, float dt, float interval)
        {
            if (!touching || float.IsNaN(dt) || dt < 0f)
            {
                Reset();
                return false;
            }
            float step = System.Math.Max(0.1f, interval);
            if (!Touching)
            {
                Touching = true;
                _timer = step;
            }
            _timer -= dt;
            if (_timer > 0f) return false;
            _timer += step;
            if (_timer <= 0f) _timer = step;
            return true;
        }

        public void Reset()
        {
            Touching = false;
            _timer = 0f;
        }

        public static float TickDamage(int stacks, float perStack)
        {
            float damage = System.Math.Max(0, stacks) * System.Math.Max(0f, perStack);
            return float.IsNaN(damage) || float.IsInfinity(damage) ? 0f : damage;
        }
    }
}
