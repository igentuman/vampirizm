using HarmonyLib;
using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Items;

namespace Vampirizm.Patches
{
    [HarmonyPatch]
    internal static class OrbPatches
    {
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HiddenRightItem =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_hiddenRightItem");

        private static string _capStation;
        private static int _capLevel;

        // Vanilla hangs hidden (R) weapons on the back. The orb has no back pose, so a hidden orb is not shown at all.
        // SetRightBackItem also writes the ZDO, so other players see the same.
        [HarmonyPostfix, HarmonyPatch(typeof(Humanoid), "SetupVisEquipment")]
        private static void AfterSetupVisEquipment(Humanoid __instance, VisEquipment visEq, bool isRagdoll)
        {
            if (isRagdoll || visEq == null || !VampireOrb.IsOrb(HiddenRightItem(__instance))) return;
            visEq.SetRightBackItem(0, 0);
        }

        // The orb never starts a vanilla Attack: its casts use the interact animation, which has no attack events.
        [HarmonyPrefix, HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        private static bool BeforeStartAttack(Humanoid __instance, bool secondaryAttack, ref bool __result)
        {
            if (__instance != Player.m_localPlayer || Plugin.Instance == null) return true;
            ItemDrop.ItemData orb = __instance.RightItem;
            if (!VampireOrb.IsOrb(orb)) return true;
            VampireController controller = __instance.GetComponent<VampireController>();
            __result = controller != null && controller.Orb.TryCast(orb, secondaryAttack);
            return false;
        }

        // Runs on the player's owner, where the hit is applied. A shield bat takes creature hits for the player.
        [HarmonyPrefix, HarmonyPatch(typeof(Character), "RPC_Damage")]
        private static bool BeforeRpcDamage(Character __instance, HitData hit)
        {
            if (__instance != Player.m_localPlayer || Plugin.Instance == null) return true;
            VampireController controller = __instance.GetComponent<VampireController>();
            return controller == null || !controller.Orb.Intercept(hit);
        }

        // Vanilla needs station level minLevel + quality - 1 for upgrades. Cap it at what the station can reach,
        // so the top quality stays craftable when the Galdr table has fewer extensions than upgrades.
        [HarmonyPostfix, HarmonyPatch(typeof(Recipe), nameof(Recipe.GetRequiredStationLevel))]
        private static void AfterGetRequiredStationLevel(Recipe __instance, ref int __result)
        {
            if (__instance == null || __instance.m_item == null || __instance.m_item.name != VampireOrb.PrefabName) return;
            __result = Mathf.Min(__result, MaxStationLevel(__instance.m_craftingStation));
        }

        private static int MaxStationLevel(CraftingStation station)
        {
            if (station == null || ZNetScene.instance == null) return int.MaxValue;
            if (_capStation == station.m_name) return _capLevel;
            int extensions = 0;
            bool stacking = false;
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                StationExtension extension = prefab != null ? prefab.GetComponent<StationExtension>() : null;
                if (extension == null || extension.m_craftingStation == null || extension.m_craftingStation.m_name != station.m_name) continue;
                extensions++;
                stacking |= extension.m_stack;
            }
            _capStation = station.m_name;
            _capLevel = stacking ? int.MaxValue : 1 + extensions;
            return _capLevel;
        }
    }
}
