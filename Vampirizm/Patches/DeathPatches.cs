using HarmonyLib;
using Vampirizm.Core;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class DeathPatches
    {
        // Prefix: vanilla sets the dead flag inside OnDeath, and CheckDeath guarantees a single call per death.
        [HarmonyPrefix, HarmonyPatch(typeof(Player), "OnDeath")]
        private static void BeforeDeath(Player __instance) => __instance.GetComponent<VampireController>()?.OnDeath();
    }
}
