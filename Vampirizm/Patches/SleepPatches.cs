using System.Collections.Generic;
using HarmonyLib;
using Vampirizm.Core;
using Vampirizm.Networking;

namespace Vampirizm.Patches
{
    // Vampires sleep through the day. The local vampire may use its bed in daylight, and when every
    // player in the world is a vampire in bed during the day, the server skips time to dusk instead of
    // the next morning. Any other case falls through to vanilla sleeping.
    [HarmonyPatch]
    internal static class SleepPatches
    {
        // Vanilla Game.UpdateSleeping waits this long after a sleep before allowing another.
        private const double SleepRetrySeconds = 10.0;
        // Vanilla EnvMan.SkipToMorning plays the time skip over this many seconds.
        private const double SkipDurationSeconds = 12.0;

        private static readonly AccessTools.FieldRef<Game, bool> GameSleeping =
            AccessTools.FieldRefAccess<Game, bool>("m_sleeping");
        private static readonly AccessTools.FieldRef<Game, double> LastSleepTime =
            AccessTools.FieldRefAccess<Game, double>("m_lastSleepTime");
        private static readonly AccessTools.FieldRef<EnvMan, bool> SkipTime =
            AccessTools.FieldRefAccess<EnvMan, bool>("m_skipTime");
        private static readonly AccessTools.FieldRef<EnvMan, double> SkipToTime =
            AccessTools.FieldRefAccess<EnvMan, double>("m_skipToTime");
        private static readonly AccessTools.FieldRef<EnvMan, double> TimeSkipSpeed =
            AccessTools.FieldRefAccess<EnvMan, double>("m_timeSkipSpeed");

        // EnvMan.CanSleep() is a tiny getter that may be inlined into Bed.Interact, so patch the
        // per-frame calculation behind it instead.
        [HarmonyPostfix, HarmonyPatch(typeof(EnvMan), "CalculateCanSleep")]
        private static void AfterCalculateCanSleep(EnvMan __instance, ref bool __result)
        {
            if (__result) return;
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null) return;
            var controller = player.GetComponent<VampireController>();
            if (controller == null || !controller.IsActiveVampire) return;
            if (!SleepRules.IsDaySleep(__instance.GetDayFraction())) return;
            __result = ZNet.instance.GetTimeSeconds() > player.m_wakeupTime + __instance.m_sleepCooldownSeconds;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Game), "UpdateSleeping")]
        private static bool BeforeUpdateSleeping(Game __instance)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || GameSleeping(__instance)) return true;
            EnvMan env = EnvMan.instance;
            if (env == null || env.IsTimeSkipping() || !SleepRules.IsDaySleep(env.GetDayFraction())) return true;
            double now = ZNet.instance.GetTimeSeconds();
            if (now - LastSleepTime(__instance) < SleepRetrySeconds || !EveryoneIsVampireInBed()) return true;
            double dusk = SleepRules.DuskTime(now, env.m_dayLengthSec);
            SkipToTime(env) = dusk;
            TimeSkipSpeed(env) = (dusk - now) / SkipDurationSeconds;
            SkipTime(env) = true;
            GameSleeping(__instance) = true;
            ZRoutedRpc.instance.InvokeRoutedRPC(0L, "SleepStart");
            Plugin.LogInfo($"Vampire day sleep: time {now:0}, skipping to dusk {dusk:0}");
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.SetSleeping))]
        private static void BeforeSetSleeping(Player __instance, out bool __state) => __state = __instance.IsSleeping();

        // Vanilla greets every sleeper with "Good morning"; a vampire waking at dusk gets its own line.
        [HarmonyPostfix, HarmonyPatch(typeof(Player), nameof(Player.SetSleeping))]
        private static void AfterSetSleeping(Player __instance, bool sleep, bool __state)
        {
            if (!__state || sleep || __instance != Player.m_localPlayer || EnvMan.instance == null) return;
            var controller = __instance.GetComponent<VampireController>();
            if (controller == null || !controller.IsActiveVampire) return;
            if (SleepRules.IsDuskWake(EnvMan.instance.GetDayFraction()))
                __instance.Message(MessageHud.MessageType.Center, "$vampirizm_good_evening");
        }

        private static bool EveryoneIsVampireInBed()
        {
            List<ZDO> characters = ZNet.instance.GetAllCharacterZDOS();
            if (characters.Count == 0) return false;
            foreach (ZDO zdo in characters)
                if (!zdo.GetBool(ZDOVars.s_inBed) || !zdo.GetBool(VampireNetwork.VampireKey)) return false;
            return true;
        }
    }
}
