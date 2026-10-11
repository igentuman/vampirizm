using HarmonyLib;
using Vampirizm.Core;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class BatFormPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static bool BeforeUseItem(Humanoid __instance)
            => !(__instance is Player player && player == Player.m_localPlayer &&
                 player.GetComponent<VampireController>()?.BatForm.Active == true);

        [HarmonyPrefix, HarmonyPatch(typeof(Player), "Interact")]
        private static bool BeforeInteract(Player __instance)
            => __instance != Player.m_localPlayer || __instance.GetComponent<VampireController>()?.BatForm.Active != true;

        [HarmonyPrefix, HarmonyPatch(typeof(Player), "UpdatePlacement")]
        private static bool BeforeUpdatePlacement(Player __instance)
            => __instance != Player.m_localPlayer || __instance.GetComponent<VampireController>()?.BatForm.Active != true;

    }
}
