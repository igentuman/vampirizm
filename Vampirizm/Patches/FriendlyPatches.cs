using HarmonyLib;
using Vampirizm.Combat;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class FriendlyPatches
    {
        // Runs on the creature's owner. Target search, the MonsterAI target drop and the creature's own
        // attack hits all ask IsEnemy(creature, other), so a false result keeps friendly creatures harmless.
        [HarmonyPostfix, HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), typeof(Character), typeof(Character))]
        private static void AfterIsEnemy(Character a, Character b, ref bool __result)
        {
            if (__result && Plugin.Instance != null && FriendlyRegistry.IsFriendlyTo(a, b)) __result = false;
        }
    }
}
