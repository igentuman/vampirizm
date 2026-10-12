namespace Vampirizm.Core
{
    public sealed class VampireState
    {
        public bool IsVampire { get; private set; }
        public float Blood { get; private set; }

        public VampireState(bool isVampire = false, float blood = 0f)
        {
            IsVampire = isVampire;
            Blood = isVampire && BloodRules.IsFinite(blood) ? System.Math.Max(0f, blood) : 0f;
        }

        public bool Convert(float capacity, float initialFraction)
        {
            if (IsVampire) return false;
            IsVampire = true;
            Blood = BloodRules.Clamp(capacity * BloodRules.Clamp(initialFraction, 1f), capacity);
            return true;
        }

        public void Cure()
        {
            IsVampire = false;
            Blood = 0f;
        }

        public void Normalize(float capacity) => Blood = IsVampire ? BloodRules.Clamp(Blood, capacity) : 0f;

        public float AddBlood(float amount, float capacity)
        {
            Normalize(capacity);
            if (!IsVampire || !BloodRules.IsFinite(amount) || amount <= 0f || !BloodRules.IsFinite(capacity)) return 0f;
            float accepted = System.Math.Min(amount, System.Math.Max(0f, capacity - Blood));
            Blood += accepted;
            return accepted;
        }

        // Passive loss never goes below zero and never fails.
        public float Drain(float amount)
        {
            if (!IsVampire || !BloodRules.IsFinite(amount) || amount <= 0f) return 0f;
            float drained = System.Math.Min(amount, Blood);
            Blood -= drained;
            return drained;
        }

        // Death keeps only a fraction of stored blood; returns the amount lost.
        public float LoseOnDeath(float keptFraction)
        {
            if (!IsVampire) return 0f;
            return Drain(Blood * (1f - BloodRules.Clamp(keptFraction, 1f)));
        }

        public bool TrySpend(float amount)
        {
            if (!IsVampire || !BloodRules.IsFinite(amount) || amount <= 0f || Blood < amount) return false;
            Blood -= amount;
            return true;
        }
    }
}
