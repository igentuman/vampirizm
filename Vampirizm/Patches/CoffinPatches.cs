using HarmonyLib;
using Vampirizm.Building;
using Vampirizm.Core;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class CoffinPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
        private static bool BeforeInteract(Bed __instance, Humanoid human, bool repeat, ref bool __result)
        {
            if (__instance.GetComponent<CoffinBed>() == null) return true;
            var controller = human.GetComponent<VampireController>();
            if (controller != null && controller.IsActiveVampire) return true;
            if (!repeat) human.Message(MessageHud.MessageType.Center, "$vampirizm_coffin_vampire_only");
            __result = false;
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Bed), nameof(Bed.GetHoverName))]
        private static void AfterHoverName(Bed __instance, ref string __result)
        {
            if (__instance.GetComponent<CoffinBed>() != null)
                __result = global::Localization.instance.Localize(__instance.GetComponent<Piece>().m_name);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Bed), nameof(Bed.GetHoverText))]
        private static void AfterHoverText(Bed __instance, ref string __result)
        {
            if (__instance.GetComponent<CoffinBed>() != null)
                __result = global::Localization.instance.Localize(__instance.GetComponent<Piece>().m_name) + "\n" + __result;
        }
    }
}
