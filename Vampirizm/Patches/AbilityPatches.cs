using HarmonyLib;
using UnityEngine;
using Vampirizm.Core;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class AbilityPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(PlayerController), "TakeInput")]
        private static void AfterTakeInput(PlayerController __instance, bool __result)
        {
            if (__instance == null) return;
            Player player = __instance.GetComponent<Player>();
            if (player == Player.m_localPlayer)
                player.GetComponent<VampireController>()?.BatForm.SetInputAllowed(__result);
        }

        // During a dash the player only travels: no steering, attacks, bite, block, jump or dodge roll.
        // Runs before the bite prefix so a held secondary attack cannot start a bite mid-dash.
        [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        private static void BeforeSetControls(Player __instance, ref Vector3 movedir, ref bool attack, ref bool attackHold,
            ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold, ref bool jump,
            ref bool crouch, ref bool run, ref bool autoRun, ref bool dodge)
        {
            if (__instance != Player.m_localPlayer) return;
            var controller = __instance.GetComponent<VampireController>();
            if (controller == null) return;
            if (controller.BatForm.Active)
            {
                controller.BatForm.SetControls(movedir, jump, crouch);
                attack = attackHold = false;
                block = blockHold = false;
                dodge = run = autoRun = false;
                jump = crouch = false;
                return;
            }
            if (!controller.Dash.Active) return;
            movedir = Vector3.zero;
            attack = attackHold = false;
            secondaryAttack = secondaryAttackHold = false;
            block = blockHold = false;
            jump = autoRun = dodge = false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Character), "UpdateWalking")]
        private static void AfterUpdateWalking(Character __instance, float dt)
        {
            if (__instance != Player.m_localPlayer) return;
            __instance.GetComponent<VampireController>()?.Dash.FixedStep(dt);
            __instance.GetComponent<VampireController>()?.BatForm.FixedStep();
        }
    }
}
