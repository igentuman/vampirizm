using System;
using HarmonyLib;
using Vampirizm.Combat;
using Vampirizm.Networking;
using Vampirizm.Skills;

namespace Vampirizm.Patches
{
    // Silver/spirit weakness is applied once, on the machine that creates the hit,
    // before it is routed to the target owner where armor and resistances apply.
    [HarmonyPatch]
    internal static class DamagePatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
        private static void BeforeMelee(ItemDrop.ItemData ___m_weapon, ItemDrop.ItemData ___m_ammoItem, out bool __state)
            => __state = HitSource.Enter(___m_weapon, ___m_ammoItem);

        [HarmonyFinalizer, HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
        private static Exception AfterMelee(Exception __exception, bool __state) => Leave(__exception, __state);

        [HarmonyPrefix, HarmonyPatch(typeof(Attack), "DoAreaAttack")]
        private static void BeforeArea(ItemDrop.ItemData ___m_weapon, ItemDrop.ItemData ___m_ammoItem, out bool __state)
            => __state = HitSource.Enter(___m_weapon, ___m_ammoItem);

        [HarmonyFinalizer, HarmonyPatch(typeof(Attack), "DoAreaAttack")]
        private static Exception AfterArea(Exception __exception, bool __state) => Leave(__exception, __state);

        [HarmonyPrefix, HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
        private static void BeforeProjectile(ItemDrop.ItemData ___m_weapon, ItemDrop.ItemData ___m_ammo, out bool __state)
            => __state = HitSource.Enter(___m_weapon, ___m_ammo);

        [HarmonyFinalizer, HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
        private static Exception AfterProjectile(Exception __exception, bool __state) => Leave(__exception, __state);

        // Area explosion on projectile timeout, outside OnHit.
        [HarmonyPrefix, HarmonyPatch(typeof(Projectile), "DoAOE")]
        private static void BeforeProjectileAoe(ItemDrop.ItemData ___m_weapon, ItemDrop.ItemData ___m_ammo, out bool __state)
            => __state = HitSource.Enter(___m_weapon, ___m_ammo);

        [HarmonyFinalizer, HarmonyPatch(typeof(Projectile), "DoAOE")]
        private static Exception AfterProjectileAoe(Exception __exception, bool __state) => Leave(__exception, __state);

        [HarmonyPrefix, HarmonyPatch(typeof(Aoe), "OnHit")]
        private static void BeforeAoe(ItemDrop.ItemData ___m_itemData, ItemDrop.ItemData ___m_ammo, out bool __state)
            => __state = HitSource.Enter(___m_itemData, ___m_ammo);

        [HarmonyFinalizer, HarmonyPatch(typeof(Aoe), "OnHit")]
        private static Exception AfterAoe(Exception __exception, bool __state) => Leave(__exception, __state);

        private static Exception Leave(Exception exception, bool state)
        {
            HitSource.Exit(state);
            return exception;
        }

        // Runs on the attacker's client for every hit an attack builds. Bare hands use the humanoid's unarmed item.
        [HarmonyPostfix, HarmonyPatch(typeof(Attack), "ModifyDamage")]
        private static void AfterModifyDamage(HitData hitData, Humanoid ___m_character, ItemDrop.ItemData ___m_weapon)
        {
            if (hitData == null || Plugin.Instance == null || !(___m_character is Player player)) return;
            if (___m_weapon == null || player.m_unarmedWeapon == null || ___m_weapon != player.m_unarmedWeapon.m_itemData) return;
            if (!VampireNetwork.IsVampire(player)) return;
            float factor = VampireDamageRules.UnarmedMultiplier(VampirismSkill.Level(player),
                Plugin.Instance.Settings.UnarmedDamageMultiplier.Value);
            if (factor != 1f) hitData.m_damage.Modify(factor);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        private static void BeforeDamage(Character __instance, ref HitData hit)
        {
            if (__instance == Player.m_localPlayer && hit != null)
                __instance.GetComponent<Vampirizm.Core.VampireController>()?.Invisibility.End();
            if (hit == null || Plugin.Instance == null || !(__instance is Player player) || !VampireNetwork.IsVampire(player)) return;
            var settings = Plugin.Instance.Settings;
            DamageScale scale = VampireDamageRules.Scale(HitSource.IsSilver, hit.m_damage.m_spirit > 0f,
                settings.SpiritDamageRule.Value, settings.WeaknessMultiplier.Value);
            if (scale.IsIdentity) return;
            // The caller may reuse the same HitData for several targets.
            hit = hit.Clone();
            hit.m_damage.Modify(scale.Whole);
            hit.m_damage.m_spirit *= scale.Spirit;
        }
    }
}
