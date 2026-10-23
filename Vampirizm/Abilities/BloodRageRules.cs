namespace Vampirizm.Abilities
{
    // A separate clock keeps the post-exit cooldown independent of the status countdown.
    public sealed class BloodRageRules
    {
        public const float Duration = 90f, Cooldown = 60f, Cost = 25f, SkillBonus = 20f;
        public bool Active { get; private set; }
        public float ReadyAt { get; private set; }
        public void Begin() => Active = true;
        public void End(float now)
        {
            if (!Active) return;
            Active = false;
            ReadyAt = now + Cooldown;
        }
    }
}
