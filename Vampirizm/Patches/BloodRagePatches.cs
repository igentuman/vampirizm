using HarmonyLib;
using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Effects;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class BloodRagePatches
    {
        // After AbilityPatches suppresses dash inputs, before BitePatches routes feeding.
        [HarmonyPrefix, HarmonyPriority(Priority.High), HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        private static void BeforeControls(Player __instance, ref Vector3 movedir, ref bool attack, ref bool attackHold,
            ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold,
            ref bool jump, ref bool crouch, ref bool dodge)
        {
            if (__instance != Player.m_localPlayer) return;
            var rage = __instance.GetComponent<VampireController>()?.BloodRage;
            if (rage?.Active != true) return;
            rage.Combat.Input(attack, attackHold, secondaryAttack, secondaryAttackHold);
            attack = attackHold = secondaryAttack = secondaryAttackHold = false;
            if (!rage.Combat.Busy) return;
            block = blockHold = jump = crouch = dodge = false;
            movedir = Vector3.zero;
        }

        // Let vanilla gate interactions, guardian powers and queued dodges during the commitment.
        [HarmonyPostfix, HarmonyPatch(typeof(Player), nameof(Player.InAttack))]
        private static void AfterInAttack(Player __instance, ref bool __result)
        {
            if (__instance == Player.m_localPlayer && __instance.GetComponent<VampireController>()?.BloodRage?.Combat.Busy == true)
                __result = true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Character), "UpdateRotation")]
        private static bool BeforeRotation(Character __instance, ref float __result)
        {
            if (__instance != Player.m_localPlayer || __instance.GetComponent<VampireController>()?.BloodRage?.Combat.Busy != true) return true;
            __result = 0f;
            return false;
        }

        // Vanilla look-at fades out slowly on the owner and stays enabled on observers (they have no
        // native Attack). Remove its camera-driven torso twist while our primary pose owns the rig.
        [HarmonyPrefix, HarmonyPatch(typeof(CharacterAnimEvent), "UpdateLookat")]
        private static bool BeforeLookAt(Character ___m_character, Animator ___m_animator, ref float ___m_lookAtWeight)
        {
            if (!(___m_character is Player player) ||
                player.GetComponent<VampireController>()?.BloodRage?.Combat.PrimaryVisualBusy != true) return true;
            ___m_lookAtWeight = 0f;
            ___m_animator.SetLookAtWeight(0f);
            return false;
        }

        // Covers queued vanilla controls, controller paths and other mods calling StartAttack directly.
        [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        private static bool BeforeAttack(Humanoid __instance, ref bool __result)
        {
            if (__instance != Player.m_localPlayer || __instance.GetComponent<VampireController>()?.BloodRage?.Active != true) return true;
            __result = false;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        private static bool BeforeEquip(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (__instance != Player.m_localPlayer || __instance.GetComponent<VampireController>()?.BloodRage?.Active != true || item == null) return true;
            var type = item.m_shared.m_itemType;
            if (!item.IsWeapon() && type != ItemDrop.ItemData.ItemType.Shield && type != ItemDrop.ItemData.ItemType.Tool &&
                type != ItemDrop.ItemData.ItemType.Torch) return true;
            __result = false;
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Humanoid), "GetCurrentBlocker")]
        private static void AfterBlocker(Humanoid __instance, ref ItemDrop.ItemData __result)
        {
            if (__instance != Player.m_localPlayer) return;
            var rage = __instance.GetComponent<VampireController>()?.BloodRage;
            if (rage?.Active == true && rage.Combat.Blocker != null) __result = rage.Combat.Blocker;
        }

        // Primary footwork: write the step velocity after vanilla walking has queued its braking change,
        // the same seam the shipped Dash uses. (A postfix on the tiny private ApplyRootMotion never fired.)
        [HarmonyPostfix, HarmonyPatch(typeof(Character), "UpdateWalking")]
        private static void AfterWalking(Character __instance, float dt)
        {
            if (__instance == Player.m_localPlayer)
                __instance.GetComponent<VampireController>()?.BloodRage?.Combat.PrimaryStep(dt);
        }

        // Secondary propulsion runs for airborne characters too, after vanilla walking/gravity.
        [HarmonyPostfix, HarmonyPatch(typeof(Character), nameof(Character.CustomFixedUpdate))]
        private static void AfterPhysics(Character __instance, float dt)
        {
            if (__instance == Player.m_localPlayer)
                __instance.GetComponent<VampireController>()?.BloodRage?.Combat.FixedStep(dt);
        }

        // Vanilla broadcasts this trigger to the owner and observers for successful jumps.
        [HarmonyPostfix, HarmonyPatch(typeof(ZSyncAnimation), "RPC_SetTrigger")]
        private static void AfterAnimationTrigger(ZSyncAnimation __instance, string name)
        {
            if (name != "jump") return;
            __instance.GetComponentInParent<Player>()?.GetComponent<VampireController>()?.BloodRage?.OnJump();
        }

        // Vanilla melee hit stop: Attack.DoMeleeAttack -> Character.FreezeFrame -> this RPC on every peer.
        // Rage claws drive their own clocks, so pause them alongside the animator the RPC just froze.
        [HarmonyPostfix, HarmonyPatch(typeof(Character), "RPC_FreezeFrame")]
        private static void AfterFreezeFrame(Character __instance, float duration)
        {
            if (__instance is Player player) player.GetComponent<VampireController>()?.BloodRage?.Combat.FreezeFrame(duration);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SEMan), nameof(SEMan.ApplyStatusEffectJumpMods))]
        private static void AfterJumpMods(Character ___m_character, ref Vector3 jump)
        {
            if (!(___m_character is Player player) || player != Player.m_localPlayer ||
                !BloodRageEffect.IsActive(player) || jump.y <= 0f) return;
            // Height is proportional to launch speed squared; keep horizontal momentum intact.
            jump.y *= Mathf.Sqrt(1.25f);
        }
    }
}
