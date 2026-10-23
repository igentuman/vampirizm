using System;
using UnityEngine;
using Vampirizm.Abilities;
using Vampirizm.Core;
using Vampirizm.Feeding;

namespace Vampirizm.Networking
{
    // Same peer-owner authority model as ordinary Valheim melee. One dedicated request enters
    // Character.Damage exactly once; the attacker never also sends a vanilla damaging RPC.
    internal static class BloodRageNetwork
    {
        internal const string SessionStartedKey = "com.igentuman.vampirizm.ragestart";
        internal const string SessionKey = "com.igentuman.vampirizm.ragesession";
        private const string SessionRpc = "Vampirizm_RageSession", AttackRpc = "Vampirizm_RageAttack",
            CancelRpc = "Vampirizm_RageCancel", HitRpc = "Vampirizm_RageHit", ResultRpc = "Vampirizm_RageResult", SplashRpc = "Vampirizm_RageSplash";
        private static ZRoutedRpc _registered;
        internal static double Now => ZNet.instance != null ? ZNet.instance.GetTime().Ticks / (double)TimeSpan.TicksPerSecond : 0d;

        internal static void Register(Character character)
        {
            var view = character.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || character.GetComponent<BloodRagePeerState>() != null) return;
            BloodRageAttackLedger state = character.gameObject.AddComponent<BloodRagePeerState>().Ledger;
            view.Register<ZPackage>(HitRpc, (sender, package) => OnHit(character, state, sender, package));
            if (!(character is Player player)) return;
            view.Register<Vector3>(SplashRpc, (sender, point) =>
            {
                if (sender == view.GetZDO().GetOwner() && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    BloodSplashEffect.PlayRage(point);
            });
            view.Register<ZPackage>(SessionRpc, (sender, package) =>
            {
                if (sender != view.GetZDO().GetOwner()) return;
                int session = package.ReadInt();
                state.Begin(session, Now);
            });
            view.Register<ZPackage>(AttackRpc, (sender, package) =>
            {
                if (sender != view.GetZDO().GetOwner()) return;
                int session = package.ReadInt(), sequence = package.ReadInt(), phase = package.ReadInt();
                float duration = package.ReadSingle();
                double started = package.ReadDouble();
                Vector3 direction = package.ReadVector3();
                if (float.IsNaN(direction.sqrMagnitude) || float.IsInfinity(direction.sqrMagnitude) || direction.sqrMagnitude < 0.5f) return;
                direction.Normalize();
                // A late joiner reconstructs the current session, without replaying earlier swings.
                ZDO zdo = view.GetZDO();
                if (session != state.Session && zdo.GetBool(VampireNetwork.BloodRageKey) && zdo.GetInt(SessionKey) == session)
                {
                    double began = zdo.GetLong(SessionStartedKey) / (double)TimeSpan.TicksPerSecond;
                    if (began <= Now && Now - began <= BloodRageRules.Duration) state.Begin(session, began);
                }
                if (!state.Attack(session, sequence, phase, duration, started, Now)) return;
                if (player != Player.m_localPlayer)
                    player.GetComponent<VampireController>()?.BloodRage?.Combat.Observe(phase, duration, (float)Math.Max(0d, Now - started), direction);
            });
            view.Register<ZPackage>(CancelRpc, (sender, package) =>
            {
                if (sender != view.GetZDO().GetOwner()) return;
                int session = package.ReadInt(), sequence = package.ReadInt();
                if (!state.Cancel(session, sequence, Now)) return;
                if (player != Player.m_localPlayer) player.GetComponent<VampireController>()?.BloodRage?.Combat.Cancel();
            });
        }

        internal static void RegisterRouted()
        {
            if (ZRoutedRpc.instance == null || _registered == ZRoutedRpc.instance) return;
            _registered = ZRoutedRpc.instance;
            _registered.Register<ZPackage>(ResultRpc, (sender, package) =>
            {
                int session = package.ReadInt(), sequence = package.ReadInt();
                ZDOID target = package.ReadZDOID();
                bool donor = package.ReadBool();
                Vector3 point = package.ReadVector3();
                VampireController.Local?.BloodRage?.Combat.Confirm(sender, session, sequence, target, donor, point);
            });
        }

        internal static void PublishSession(Player player, int session)
        {
            var view = player.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            var package = new ZPackage();
            package.Write(session);
            view.InvokeRPC(ZNetView.Everybody, SessionRpc, package);
        }

        internal static void PublishAttack(Player player, int session, int sequence, int phase, float duration, double started, Vector3 direction)
        {
            var package = new ZPackage();
            package.Write(session); package.Write(sequence); package.Write(phase); package.Write(duration); package.Write(started); package.Write(direction);
            player.GetComponent<ZNetView>().InvokeRPC(ZNetView.Everybody, AttackRpc, package);
        }

        internal static void PublishSplash(Player player, Vector3 point)
            => player.GetComponent<ZNetView>().InvokeRPC(ZNetView.Everybody, SplashRpc, point);

        internal static void PublishCancel(Player player, int session, int sequence)
        {
            var view = player.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            var package = new ZPackage();
            package.Write(session); package.Write(sequence);
            view.InvokeRPC(ZNetView.Everybody, CancelRpc, package);
        }

        internal static void RequestHit(Character target, Player player, int session, int sequence, int phase, HitData hit)
        {
            var package = new ZPackage();
            package.Write(player.GetZDOID()); package.Write(session); package.Write(sequence); package.Write(phase);
            hit.Serialize(ref package);
            target.GetComponent<ZNetView>().InvokeRPC(HitRpc, package);
        }

        private static void OnHit(Character victim, BloodRageAttackLedger victimState, long sender, ZPackage package)
        {
            var view = victim.GetComponent<ZNetView>();
            if (Plugin.Instance == null || view == null || !view.IsValid() || !view.IsOwner() || victim.IsDead()) return;
            ZDOID attackerId = package.ReadZDOID();
            int session = package.ReadInt(), sequence = package.ReadInt(), phase = package.ReadInt();
            GameObject instance = ZNetScene.instance?.FindInstance(attackerId);
            Player attacker = instance != null ? instance.GetComponent<Player>() : null;
            var attackerView = attacker != null ? attacker.GetComponent<ZNetView>() : null;
            var attackState = attacker != null ? attacker.GetComponent<BloodRagePeerState>()?.Ledger : null;
            if (attackerView == null || !attackerView.IsValid() || attackerView.GetZDO().GetOwner() != sender || attacker.IsDead() ||
                !VampireNetwork.IsVampire(attacker) || attackState == null || !attackState.CanHit(session, sequence, phase, Now) ||
                !BloodRageCombat.ValidTarget(attacker, victim)) return;
            Vector3 origin = attacker.GetCenterPoint();
            Vector3 point = victim.GetCollider() != null ? victim.GetCollider().ClosestPoint(origin) : victim.GetCenterPoint();
            if (Vector3.Distance(origin, point) > BloodRageCombatRules.Reach + 1.5f ||
                BloodRageCombat.Obstructed(origin, point, victim.gameObject)) return;
            var hit = new HitData();
            hit.Deserialize(ref package);
            // Damage comes from the attacking owner, as in native melee. Bound and sanitize transport fields.
            float damage = hit.m_damage.m_slash;
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f || damage > 100000f ||
                !victimState.AcceptVictim(attackerId.ToString(), session, sequence, Now)) return;
            hit = new HitData { m_damage = new HitData.DamageTypes { m_slash = damage } };
            hit.SetAttacker(attacker);
            hit.m_ignorePVP = false;
            hit.m_blockable = hit.m_dodgeable = true;
            hit.m_statusEffectHash = 99999; // Native Unarmed kill classification.
            hit.m_skill = global::Skills.SkillType.Unarmed;
            hit.m_skillRaiseAmount = 0f; // Award once on the attacker, after a positive confirmation.
            hit.m_backstabBonus = attacker.m_unarmedWeapon.m_itemData.m_shared.m_backstabBonus;
            hit.m_staggerMultiplier = attacker.m_unarmedWeapon.m_itemData.m_shared.m_attack.m_staggerMultiplier;
            hit.m_pushForce = attacker.m_unarmedWeapon.m_itemData.m_shared.m_attackForce;
            hit.m_healthReturn = hit.m_eitrAdd = 0f;
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.m_point = point;
            hit.m_dir = (point - origin).normalized;
            bool donor = DonorRegistry.CanFeed(victim); // Capture before a potentially lethal hit.
            float health = victim.GetHealth();
            ZDOID victimId = view.GetZDO().m_uid;
            victim.Damage(hit);
            if (victim.GetHealth() >= health) return;
            var reply = new ZPackage();
            reply.Write(session); reply.Write(sequence); reply.Write(victimId); reply.Write(donor); reply.Write(point);
            ZRoutedRpc.instance?.InvokeRoutedRPC(sender, ResultRpc, reply);
        }
    }

    internal sealed class BloodRagePeerState : MonoBehaviour
    {
        internal readonly BloodRageAttackLedger Ledger = new BloodRageAttackLedger();
    }
}
