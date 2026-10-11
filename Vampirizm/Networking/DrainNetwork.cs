using UnityEngine;
using Vampirizm.Conversion;
using Vampirizm.Core;
using Vampirizm.Feeding;

namespace Vampirizm.Networking
{
    public enum DrainStatus { Ok, Rejected, TooFar, NotDonor, Dead, Busy, Stale, Expired }

    // Feeding protocol. The victim owner is the single coordinator: it validates the request,
    // deals the damage and replies with the blood actually produced. The vampire's own client
    // adds blood and experience only for a confirmation matching its outstanding tick.
    internal static class DrainNetwork
    {
        internal const string DrainRpc = "Vampirizm_Drain";
        internal const string DrainResultRpc = "Vampirizm_DrainResult";
        internal const string BiteRpc = "Vampirizm_Bite";
        internal const string BiteResultRpc = "Vampirizm_BiteResult";
        internal const string DrainedKey = "com.igentuman.vampirizm.drained";
        private const float DistanceTolerance = 2f;
        private static ZRoutedRpc _registeredOn;

        internal static void RegisterCharacter(Character character)
        {
            var view = character.GetComponent<ZNetView>();
            if (view == null || view.GetZDO() == null) return;
            if (character is Player player)
                view.Register<ZPackage>(BiteRpc, (sender, package) => OnBite(player, sender, package));
            else
                view.Register<ZPackage>(DrainRpc, (sender, package) => OnDrain(character, sender, package));
        }

        internal static void RegisterRouted()
        {
            ZRoutedRpc routed = ZRoutedRpc.instance;
            if (routed == null || routed == _registeredOn) return;
            _registeredOn = routed;
            routed.Register<ZPackage>(DrainResultRpc, OnDrainResult);
            routed.Register<ZPackage>(BiteResultRpc, OnBiteResult);
        }

        internal static float Timeout(float interval) => interval * 2.5f + 1f;

        // ---- Vampire side: requests ----

        internal static void RequestDrain(Character target, ZDOID attacker, int session, int tick)
        {
            var package = new ZPackage();
            package.Write(attacker);
            package.Write(session);
            package.Write(tick);
            target.GetComponent<ZNetView>().InvokeRPC(DrainRpc, package);
        }

        internal static void RequestBite(Player target, ZDOID attacker, string attackerName)
        {
            var package = new ZPackage();
            package.Write(attacker);
            package.Write(attackerName ?? "");
            target.GetComponent<ZNetView>().InvokeRPC(BiteRpc, package);
        }

        // ---- Victim owner side ----

        // The sender must own the attacking player's ZDO, so peers cannot bite on behalf of others.
        private static bool Authenticate(long sender, ZDOID attacker, Vector3 victimPosition, out ZDO attackerZdo)
        {
            attackerZdo = ZDOMan.instance?.GetZDO(attacker);
            if (attackerZdo == null || attackerZdo.GetOwner() != sender || !attackerZdo.GetBool(VampireNetwork.VampireKey)) return false;
            float range = Plugin.Instance.Settings.BiteRange.Value + DistanceTolerance;
            return (attackerZdo.GetPosition() - victimPosition).sqrMagnitude <= range * range;
        }

        private static void OnDrain(Character victim, long sender, ZPackage package)
        {
            var view = victim.GetComponent<ZNetView>();
            if (Plugin.Instance == null || view == null || !view.IsValid() || !view.IsOwner()) return;
            ZDOID attacker = package.ReadZDOID();
            int session = package.ReadInt();
            int tick = package.ReadInt();
            DrainStatus status = Drain(victim, view, sender, attacker, session, tick, out float blood, out bool xpAllowed, out bool died);
            var reply = new ZPackage();
            reply.Write(session);
            reply.Write(tick);
            reply.Write((int)status);
            reply.Write(blood);
            reply.Write(xpAllowed);
            reply.Write(died);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, DrainResultRpc, reply);
        }

        private static DrainStatus Drain(Character victim, ZNetView view, long sender, ZDOID attacker, int session, int tick,
            out float blood, out bool xpAllowed, out bool died)
        {
            blood = 0f;
            xpAllowed = false;
            died = false;
            var settings = Plugin.Instance.Settings;
            if (victim.IsDead() || victim.GetHealth() <= 0f) return DrainStatus.Dead;
            if (!Authenticate(sender, attacker, victim.transform.position, out ZDO attackerZdo))
            {
                ZDO zdo = ZDOMan.instance?.GetZDO(attacker);
                return zdo != null && zdo.GetOwner() == sender && zdo.GetBool(VampireNetwork.VampireKey) ? DrainStatus.TooFar : DrainStatus.Rejected;
            }
            if (!DonorRegistry.CanFeed(victim)) return DrainStatus.NotDonor;

            var state = DrainVictim.Ensure(victim);
            float interval = settings.BiteInterval.Value;
            // One tick of slack absorbs network jitter at the session limit.
            int maxTicks = DrainRules.MaxTicks(settings.BiteMaxDuration.Value, interval) + 1;
            DrainVerdict verdict = state.Ledger.Accept(sender, session, tick, Time.time, Timeout(interval), maxTicks);
            if (verdict != DrainVerdict.Accepted)
                return verdict == DrainVerdict.Busy ? DrainStatus.Busy : verdict == DrainVerdict.Stale ? DrainStatus.Stale : DrainStatus.Expired;
            state.Held = DonorRegistry.CanHold(victim);

            float before = victim.GetHealth();
            var hit = new HitData();
            bool batBite = attackerZdo.GetBool(VampireNetwork.BatFormKey);
            float requestedDamage = batBite ? settings.BatBiteDamage.Value : settings.BiteDamage.Value;
            hit.m_damage.m_damage = DrainRules.DealtDamage(requestedDamage, before);
            hit.m_attacker = attacker;
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.m_point = victim.GetCenterPoint();
            hit.m_dir = (victim.transform.position - ZDOMan.instance.GetZDO(attacker).GetPosition()).normalized;
            victim.Damage(hit);
            // The local owner handles its own RPC synchronously, so the health change is already applied.
            float dealt = Mathf.Max(0f, before - victim.GetHealth());
            blood = batBite
                ? settings.BatBiteBlood.Value * Mathf.Clamp01(dealt / Mathf.Max(0.1f, requestedDamage))
                : DrainRules.Blood(dealt, settings.BloodPerDamage.Value);

            // Experience stops once a creature has given more than its maximum health in total.
            ZDO victimZdo = view.GetZDO();
            float drained = victimZdo.GetFloat(DrainedKey) + dealt;
            victimZdo.Set(DrainedKey, drained);
            xpAllowed = drained <= victim.GetMaxHealth();
            died = victim.IsDead() || victim.GetHealth() <= 0f;
            if (died) state.Release();
            return DrainStatus.Ok;
        }

        private static void OnBite(Player victim, long sender, ZPackage package)
        {
            if (Plugin.Instance == null || victim != Player.m_localPlayer) return;
            ZDOID attacker = package.ReadZDOID();
            string attackerName = package.ReadString();
            BiteConversionResult result = Authenticate(sender, attacker, victim.transform.position, out _)
                ? VampireConversion.AcceptBite(victim, attackerName)
                : BiteConversionResult.Rejected;
            var reply = new ZPackage();
            reply.Write((int)result);
            reply.Write(victim.GetPlayerName());
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, BiteResultRpc, reply);
        }

        // ---- Vampire side: confirmations ----

        private static void OnDrainResult(long sender, ZPackage package)
        {
            var controller = VampireController.Local;
            if (controller == null) return;
            int session = package.ReadInt();
            int tick = package.ReadInt();
            var status = (DrainStatus)package.ReadInt();
            float blood = package.ReadSingle();
            bool xpAllowed = package.ReadBool();
            bool died = package.ReadBool();
            controller.Drain.OnResult(session, tick, status, blood, xpAllowed, died);
        }

        private static void OnBiteResult(long sender, ZPackage package)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            var result = (BiteConversionResult)package.ReadInt();
            string victimName = package.ReadString();
            string key;
            switch (result)
            {
                case BiteConversionResult.Converted: key = "$vampirizm_bite_converted"; break;
                case BiteConversionResult.Refused: key = "$vampirizm_bite_refused"; break;
                case BiteConversionResult.AlreadyVampire: key = "$vampirizm_bite_already"; break;
                case BiteConversionResult.Disabled: key = "$vampirizm_bite_disabled"; break;
                default: key = "$vampirizm_bite_failed"; break;
            }
            player.Message(MessageHud.MessageType.Center, global::Localization.instance.Localize(key, victimName));
        }
    }

    // Owner-only feeding state of a victim. Holding expires with the session, so a lost vampire
    // never leaves the creature's AI disabled.
    internal sealed class DrainVictim : MonoBehaviour
    {
        private ZNetView _view;
        internal readonly DrainLedger Ledger = new DrainLedger();
        internal bool Held { get; set; }

        internal static DrainVictim Ensure(Character character)
            => character.GetComponent<DrainVictim>() ?? character.gameObject.AddComponent<DrainVictim>();

        internal static bool IsHeld(Component ai)
        {
            var victim = ai.GetComponent<DrainVictim>();
            return victim != null && victim.Held;
        }

        private void Awake() => _view = GetComponent<ZNetView>();

        private void Update()
        {
            if (!Held) return;
            float timeout = Plugin.Instance != null ? DrainNetwork.Timeout(Plugin.Instance.Settings.BiteInterval.Value) : 0f;
            if (_view == null || !_view.IsValid() || !_view.IsOwner() || !Ledger.IsActive(Time.time, timeout)) Release();
        }

        internal void Release()
        {
            Ledger.Release();
            Held = false;
        }
    }
}
