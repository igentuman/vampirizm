using HarmonyLib;
using Vampirizm.Core;
using Vampirizm.Feeding;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class PlayerPersistencePatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static void AfterLoad(Player __instance)
        {
            VampireController.Ensure(__instance).LoadState();
            VampireFood.Rewrap(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Player), nameof(Player.SetLocalPlayer))]
        private static void AfterSetLocalPlayer(Player __instance) => VampireController.Ensure(__instance);

        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.Save))]
        private static void BeforeSave(Player __instance) => __instance.GetComponent<VampireController>()?.SaveState();
    }
}
