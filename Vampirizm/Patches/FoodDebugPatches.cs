using System.Linq;
using HarmonyLib;

namespace Vampirizm.Patches
{
    // TEMPORARY diagnostics for the vampire food slot bug. Remove once fixed.
    [HarmonyPatch]
    internal static class FoodDebugPatches
    {
        private static int _lastCount = -1;

        private static string Foods(Player player)
            => string.Join(", ", player.GetFoods().Select(f => $"{f.m_name}:{f.m_time:0.0}/{f.m_item.m_shared.m_foodBurnTime:0}"));

        private static bool Local(Player player) => player != null && player == Player.m_localPlayer;

        [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        private static void LogUseItem(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (!(__instance is Player player) || !Local(player) || item == null) return;
            Plugin.LogInfo($"[FoodDebug] UseItem {item.m_dropPrefab?.name} type={item.m_shared.m_itemType} food={item.m_shared.m_food} " +
                           $"foods=[{Foods(player)}]");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Player), nameof(Player.CanConsumeItem))]
        private static void LogCanConsume(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (Local(__instance)) Plugin.LogInfo($"[FoodDebug] CanConsumeItem {item?.m_dropPrefab?.name} => {__result}");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Player), nameof(Player.CanEat))]
        private static void LogCanEat(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (Local(__instance)) Plugin.LogInfo($"[FoodDebug] CanEat {item?.m_dropPrefab?.name} => {__result} foods=[{Foods(__instance)}]");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
        private static void LogEatFood(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (Local(__instance)) Plugin.LogInfo($"[FoodDebug] EatFood {item?.m_dropPrefab?.name} => {__result} foods=[{Foods(__instance)}]");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Player), nameof(Player.ClearFood))]
        private static void LogClearFood(Player __instance)
        {
            if (Local(__instance)) Plugin.LogInfo("[FoodDebug] ClearFood\n" + System.Environment.StackTrace);
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last), HarmonyPatch(typeof(Player), "UpdateFood")]
        private static void LogUpdateFood(Player __instance, bool forceUpdate)
        {
            if (!Local(__instance)) return;
            int count = __instance.GetFoods().Count;
            if (count != _lastCount)
                Plugin.LogInfo($"[FoodDebug] food count {_lastCount} -> {count} (force={forceUpdate}) foods=[{Foods(__instance)}]");
            _lastCount = count;
        }
    }
}
