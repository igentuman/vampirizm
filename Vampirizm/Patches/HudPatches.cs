using HarmonyLib;
using Vampirizm.UI;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class HudPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(Hud), "Awake")]
        private static void AfterHudAwake(Hud __instance)
        {
            if (__instance.GetComponent<BloodHud>() == null) __instance.gameObject.AddComponent<BloodHud>();
        }
    }
}
