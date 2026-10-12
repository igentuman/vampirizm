using HarmonyLib;
using Vampirizm.Core;

namespace Vampirizm.Patches
{
    // Vanilla UpdateStats runs UpdateFood and then eitr regen, both based on m_eiterRegen. The vampire rate is written
    // between the two and the vanilla base is restored after the tick, so humans and cured players never see it.
    [HarmonyPatch]
    internal static class EitrPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Player), "UpdateStats", typeof(float))]
        private static void BeforeUpdateStats(Player __instance, out float __state) => __state = __instance.m_eiterRegen;

        [HarmonyPostfix, HarmonyPatch(typeof(Player), "UpdateFood")]
        private static void AfterUpdateFood(Player __instance, bool forceUpdate)
        {
            if (forceUpdate || __instance != Player.m_localPlayer) return;
            var controller = __instance.GetComponent<VampireController>();
            if (controller != null && controller.IsActiveVampire) controller.ApplyEitrRegen();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Player), "UpdateStats", typeof(float))]
        private static void AfterUpdateStats(Player __instance, float __state) => __instance.m_eiterRegen = __state;
    }
}
