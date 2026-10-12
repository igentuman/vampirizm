using System;
using System.Reflection;
using HarmonyLib;
using Vampirizm.Core;

namespace Vampirizm.Patches
{
    // Vampires never get Cold or Freezing. The environment flags are hidden only while vanilla
    // updates the local vampire's status effects, so cold also stops blocking Rested.
    [HarmonyPatch]
    internal static class ColdPatches
    {
        private static readonly FieldInfo Cold = AccessTools.Field(typeof(EnvMan), "s_isCold");
        private static readonly FieldInfo Freezing = AccessTools.Field(typeof(EnvMan), "s_isFreezing");

        internal struct EnvFlags
        {
            internal bool Hidden;
            internal bool Cold;
            internal bool Freezing;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Player), "UpdateEnvStatusEffects")]
        private static void BeforeUpdateEnvStatusEffects(Player __instance, out EnvFlags __state)
        {
            __state = default;
            if (__instance != Player.m_localPlayer) return;
            var controller = __instance.GetComponent<VampireController>();
            if (controller == null || !controller.IsActiveVampire) return;
            __state = new EnvFlags { Hidden = true, Cold = EnvMan.IsCold(), Freezing = EnvMan.IsFreezing() };
            Cold.SetValue(null, false);
            Freezing.SetValue(null, false);
        }

        [HarmonyFinalizer, HarmonyPatch(typeof(Player), "UpdateEnvStatusEffects")]
        private static Exception AfterUpdateEnvStatusEffects(Exception __exception, EnvFlags __state)
        {
            if (__state.Hidden)
            {
                Cold.SetValue(null, __state.Cold);
                Freezing.SetValue(null, __state.Freezing);
            }
            return __exception;
        }
    }
}
