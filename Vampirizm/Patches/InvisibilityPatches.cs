using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Vampirizm.Abilities;
using Vampirizm.Core;
using Vampirizm.Networking;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class InvisibilityPatches
    {
        private static readonly AccessTools.FieldRef<MonsterAI, Character> MonsterTarget =
            AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");
        private static readonly AccessTools.FieldRef<AnimalAI, Character> AnimalTarget =
            AccessTools.FieldRefAccess<AnimalAI, Character>("m_target");
        private static readonly MethodInfo ClearMonsterTarget = AccessTools.Method(typeof(MonsterAI), "SetTarget");

        [HarmonyPrefix, HarmonyPatch(typeof(VisEquipment), "UpdateVisuals")]
        private static void BeforeEquipmentVisuals(VisEquipment __instance)
        {
            Player player = __instance.m_playerComponent;
            if (player == null) player = __instance.GetComponent<Player>();
            if (player != null)
                player.GetComponent<VampireController>()?.Invisibility?.BeforeEquipmentUpdate();
        }

        // Character.CustomFixedUpdate calls SetVisible(HasOwner) every physics tick.
        // Combine concealment with that decision so vanilla cannot undo remote hiding.
        [HarmonyPrefix, HarmonyPatch(typeof(Character), "SetVisible")]
        private static void BeforeSetVisible(Character __instance, ref bool visible)
        {
            if (__instance is Player player && player != Player.m_localPlayer && VampireNetwork.IsInvisible(player))
                visible = false;
        }

        // Add a controller to remote players too; their ZDO state drives this client's rendering.
        [HarmonyPostfix, HarmonyPatch(typeof(Character), "Awake")]
        private static void AfterCharacterAwake(Character __instance)
        {
            if (__instance is Player player) VampireController.Ensure(player);
        }

        // Any visible action breaks concealment. Damage received is handled in DamagePatches.
        [HarmonyPrefix, HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static void BeforeUseItem(Humanoid __instance)
        {
            if (__instance is Player player && player == Player.m_localPlayer)
                player.GetComponent<VampireController>()?.Invisibility.End();
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.GetHoverName))]
        private static bool BeforeGetHoverName(Player __instance, ref string __result)
        {
            if (__instance != Player.m_localPlayer && VampireNetwork.IsInvisible(__instance))
            {
                __result = "";
                return false;
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.OnTargeted))]
        private static bool BeforeOnTargeted(Player __instance)
            => __instance == Player.m_localPlayer || !VampireNetwork.IsInvisible(__instance);

        [HarmonyPrefix, HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
        private static void BeforeUpdateCrosshair(ref Player __0)
        {
            if (__0 != null && __0 != Player.m_localPlayer && VampireNetwork.IsInvisible(__0)) __0 = null;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSenseTarget), new[] { typeof(Character) })]
        private static bool BeforeCanSenseTarget(Character target, ref bool __result)
            => NotInvisible(target, ref __result);

        [HarmonyPrefix, HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSeeTarget), new[] { typeof(Character) })]
        private static bool BeforeCanSeeTarget(Character target, ref bool __result)
            => NotInvisible(target, ref __result);

        [HarmonyPrefix, HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanHearTarget), new[] { typeof(Character) })]
        private static bool BeforeCanHearTarget(Character target, ref bool __result)
            => NotInvisible(target, ref __result);

        [HarmonyPostfix, HarmonyPatch(typeof(BaseAI), nameof(BaseAI.GetTargetCreature))]
        private static void AfterGetBaseTarget(ref Character __result)
        {
            if (__result is Player player && VampireNetwork.IsInvisible(player)) __result = null;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.GetTargetCreature))]
        private static void AfterGetMonsterTarget(ref Character __result)
        {
            if (__result is Player player && VampireNetwork.IsInvisible(player)) __result = null;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MonsterAI), "SetTarget")]
        private static bool BeforeSetMonsterTarget(Character attacker)
            => !(attacker is Player player) || !VampireNetwork.IsInvisible(player);

        // Drop old aggro immediately: merely suppressing fresh sight and hearing would leave
        // monsters walking to the last known position of a player they had already spotted.
        [HarmonyPostfix, HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
        private static void AfterMonsterAi(MonsterAI __instance)
        {
            if (!(MonsterTarget(__instance) is Player player) || !VampireNetwork.IsInvisible(player)) return;
            ClearMonsterTarget.Invoke(__instance, new object[] { null });
            __instance.StopMoving();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(AnimalAI), nameof(AnimalAI.UpdateAI))]
        private static void AfterAnimalAi(AnimalAI __instance)
        {
            if (!(AnimalTarget(__instance) is Player player) || !VampireNetwork.IsInvisible(player)) return;
            AnimalTarget(__instance) = null;
            __instance.StopMoving();
        }

        private static bool NotInvisible(Character target, ref bool result)
        {
            if (!(target is Player player) || !VampireNetwork.IsInvisible(player)) return true;
            result = false;
            return false;
        }
    }
}
