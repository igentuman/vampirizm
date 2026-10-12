using HarmonyLib;
using UnityEngine;
using Vampirizm.Conversion;
using Vampirizm.Core;
using Vampirizm.Networking;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class BitePatches
    {
        // The alternative attack with empty hands becomes the bite; while drinking the vampire cannot move or fight.
        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        private static void BeforeSetControls(Player __instance, ref Vector3 movedir, ref bool attack, ref bool attackHold,
            ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold, ref bool jump, ref bool crouch, ref bool dodge,
            ItemDrop.ItemData ___m_rightItem, ItemDrop.ItemData ___m_leftItem)
        {
            if (__instance != Player.m_localPlayer) return;
            var controller = __instance.GetComponent<VampireController>();
            if (controller == null || !controller.IsActiveVampire) return;
            bool unarmed = ___m_rightItem == null && ___m_leftItem == null;
            bool batForm = controller.BatForm.Active;
            if (unarmed || batForm || controller.Drain.Active)
            {
                controller.Drain.Input((unarmed || batForm) && secondaryAttack);
                secondaryAttack = false;
                secondaryAttackHold = false;
            }
            if (!controller.Drain.Active) return;
            if (batForm) controller.BatForm.SetControls(Vector3.zero, false);
            movedir = Vector3.zero;
            attack = attackHold = false;
            block = blockHold = false;
            // Vanilla stops an emote on any of these inputs.
            jump = crouch = false;
            dodge = false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Character), "Awake")]
        private static void AfterCharacterAwake(Character __instance)
        {
            DrainNetwork.RegisterCharacter(__instance);
            if (__instance is Player player) RitualEffect.Register(player);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Game), "Start")]
        private static void AfterGameStart() => DrainNetwork.RegisterRouted();

        // A held victim stands still; its AI resumes as soon as the session ends or times out.
        [HarmonyPrefix, HarmonyPatch(typeof(BaseAI), nameof(BaseAI.UpdateAI))]
        private static bool BeforeBaseAI(BaseAI __instance, ref bool __result) => Hold(__instance, ref __result);

        [HarmonyPrefix, HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
        private static bool BeforeMonsterAI(BaseAI __instance, ref bool __result) => Hold(__instance, ref __result);

        [HarmonyPrefix, HarmonyPatch(typeof(AnimalAI), nameof(AnimalAI.UpdateAI))]
        private static bool BeforeAnimalAI(BaseAI __instance, ref bool __result) => Hold(__instance, ref __result);

        // A held victim is pinned to the drinking vampire after vanilla motion has run for this step.
        [HarmonyPostfix, HarmonyPatch(typeof(Character), nameof(Character.CustomFixedUpdate))]
        private static void AfterCharacterFixedUpdate(Character __instance, float dt)
        {
            if (__instance.TryGetComponent(out DrainVictim victim)) victim.FixedStep(dt);
        }

        private static bool Hold(BaseAI ai, ref bool result)
        {
            if (!DrainVictim.IsHeld(ai)) return true;
            ai.StopMoving();
            result = false;
            return false;
        }
    }
}
