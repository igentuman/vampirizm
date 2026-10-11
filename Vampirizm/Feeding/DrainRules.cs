using System;
using System.Collections.Generic;
using Vampirizm.Core;

namespace Vampirizm.Feeding
{
    public enum DrainVerdict { Accepted, Busy, Stale, Expired }

    // Pure feeding rules shared by the vampire client and the victim owner.
    public static class DrainRules
    {
        // Unknown creatures are forbidden; an explicit ban always wins over permission.
        public static bool IsDonor(string prefabId, ISet<string> allowed, ISet<string> denied)
            => !string.IsNullOrEmpty(prefabId) && !denied.Contains(prefabId) && allowed.Contains(prefabId);

        // Damage actually dealt can never exceed the remaining health, so the last bite gives no extra blood.
        public static float DealtDamage(float requested, float health)
        {
            if (!BloodRules.IsFinite(requested) || !BloodRules.IsFinite(health)) return 0f;
            return Math.Max(0f, Math.Min(requested, health));
        }

        public static float Blood(float dealtDamage, float bloodPerDamage)
            => BloodRules.IsFinite(dealtDamage) && BloodRules.IsFinite(bloodPerDamage)
                ? Math.Max(0f, dealtDamage) * Math.Max(0f, bloodPerDamage) : 0f;

        // Experience only for blood that actually entered a non-full bar.
        public static float Experience(float acceptedBlood, float xpPerBlood, bool victimAllowsXp)
            => victimAllowsXp ? Blood(acceptedBlood, xpPerBlood) : 0f;

        public static int MaxTicks(float maxDuration, float interval)
        {
            if (!BloodRules.IsFinite(maxDuration) || !BloodRules.IsFinite(interval) || interval <= 0f) return 1;
            return Math.Max(1, (int)Math.Ceiling(maxDuration / interval));
        }
    }

    // Victim-owner side: one active session per victim, ticks strictly in order, each processed once.
    public sealed class DrainLedger
    {
        private bool _active;
        private long _attacker;
        private int _session;
        private int _tick;
        private float _last;

        public bool IsActive(float now, float timeout) => _active && now - _last <= timeout;
        public long Attacker => _attacker;

        public DrainVerdict Accept(long attacker, int session, int tick, float now, float timeout, int maxTicks)
        {
            bool active = IsActive(now, timeout);
            if (active && (attacker != _attacker || session != _session)) return DrainVerdict.Busy;
            if (!active)
            {
                // A new owner or a timed-out session cannot be resumed mid-way.
                if (tick != 1) return DrainVerdict.Stale;
                _active = true;
                _attacker = attacker;
                _session = session;
                _tick = 0;
            }
            if (tick != _tick + 1) return DrainVerdict.Stale;
            if (tick > maxTicks) return DrainVerdict.Expired;
            _tick = tick;
            _last = now;
            return DrainVerdict.Accepted;
        }

        public void Release() => _active = false;
    }

    // Vampire side: only the confirmation for the outstanding tick is accepted, once.
    public sealed class DrainTracker
    {
        public int Session { get; private set; }
        public int Tick { get; private set; }
        public bool Awaiting { get; private set; }

        public void Start(int session)
        {
            Session = session;
            Tick = 0;
            Awaiting = false;
        }

        public int Next()
        {
            Tick++;
            Awaiting = true;
            return Tick;
        }

        public bool Confirm(int session, int tick)
        {
            if (!Awaiting || session != Session || tick != Tick) return false;
            Awaiting = false;
            return true;
        }
    }
}
