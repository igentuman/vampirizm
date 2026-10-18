namespace Vampirizm.Building
{
    // Stable saved prefab IDs and gameplay values shared by registration and sleep rewards.
    internal static class CoffinRules
    {
        internal const string WoodenPrefab = "VampirizmWoodenCoffin";
        internal const string VampirePrefab = "VampirizmVampireCoffin";
        internal const int WoodenComfort = 2;
        internal const int VampireComfort = 5;
        internal const int WoodenBlood = 15;
        internal const int VampireBlood = 55;

        // Entering a bed, waiting for other players, or repeatedly receiving SleepStop grants nothing.
        internal static int WakeBlood(bool wasSleeping, bool sleeping, bool vampire, bool alive, int blood)
            => wasSleeping && !sleeping && vampire && alive ? blood : 0;
    }
}
