namespace Vampirizm.Combat
{
    // HitData carries no weapon or material. While vanilla code that builds a hit runs
    // (melee/area attack, projectile or AoE impact), the weapon and ammo are remembered here
    // so Character.Damage can classify the hit before it is sent to the target owner.
    internal static class HitSource
    {
        private static int _depth;
        private static bool _silver;

        internal static bool IsSilver => _depth > 0 && _silver;

        internal static bool Enter(ItemDrop.ItemData weapon, ItemDrop.ItemData ammo)
        {
            bool previous = _silver;
            _depth++;
            _silver = SilverRegistry.IsSilver(weapon) || SilverRegistry.IsSilver(ammo);
            return previous;
        }

        internal static void Exit(bool previous)
        {
            if (_depth > 0) _depth--;
            _silver = _depth > 0 && previous;
        }
    }
}
