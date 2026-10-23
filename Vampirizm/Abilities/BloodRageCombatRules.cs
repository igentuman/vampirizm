using System;
using System.Collections.Generic;

namespace Vampirizm.Abilities
{
    // Shared by the attack clock, animation and owner-side hit validation. Phase 4 is secondary.
    public static class BloodRageCombatRules
    {
        public const int Secondary = 4;
        public const float SecondaryCooldown = 1f, SecondaryDuration = 0.65f, ComboTimeout = 0.65f;
        public const float PrimaryDurationCap = 0.6f, HitStart = 0.2f, HitEnd = 0.55f;
        public const float Reward = 5f, Reach = 2.8f, LungeDistance = 7f, LungeDuration = 0.3f;
        public const float ClawSweepRadius = 0.6f, CharacterContactRadius = 0.75f;
        public const float SecondaryStaminaExtra = 3f;
        public const float TargetRange = 10f;
        // Vanilla melee pauses the animator for 0.15 s on every landed swing (Attack.DoMeleeAttack).
        public const float HitStop = 0.15f;
        // Seconds on the secondary attack clock: raise, hop, airborne rake, recovery.
        public const float SecondaryHopAt = 0.12f, SecondaryDashAt = 0.18f, SecondaryHopSpeed = 3.5f;
        // The claws snap down within the first part of the dash; the hit window still covers the whole dash
        // because the extended claws keep sweeping forward with the body.
        public const float SecondaryRakeDuration = 0.14f;
        public const float SecondarySwingEnd = (SecondaryDashAt + LungeDuration) / SecondaryDuration;
        public const float SecondaryRakeEnd = (SecondaryDashAt + SecondaryRakeDuration) / SecondaryDuration;
        public static float HitWindowStart(int phase) => phase == Secondary ? SecondaryDashAt / SecondaryDuration : HitStart;
        public static float HitWindowEnd(int phase) => phase == Secondary ? SecondarySwingEnd : HitEnd;
        public static float Duration(int phase, float fistDuration)
            // Phase 2 is the third primary: halve its duration to bring the fourth attack forward.
            => phase == Secondary ? SecondaryDuration : Math.Min(PrimaryDurationCap, fistDuration / 1.25f) * (phase == 2 ? 0.5f : 1f);
        public static float Damage(float equivalentFist, bool secondary) => (Math.Max(0f, equivalentFist) + 15f) * (secondary ? 3f : 1f);
        public static float ReleaseVerticalVelocity(float initial, float elapsed, float gravity, float current)
            => Math.Min(current, initial + gravity * Math.Max(0f, elapsed));
        public static int NextPhase(int previous, float elapsed) => previous < 0 || previous >= 3 || elapsed > ComboTimeout ? 0 : previous + 1;
        public static bool InHitWindow(float previous, float current, float duration, int phase = 0)
            => previous < duration * HitWindowEnd(phase) && current >= duration * HitWindowStart(phase);

        // Camera pitch must not reject a close animal already touched by the melee volume.
        public static bool InForwardArc(float forward, float sideways)
            => forward >= 0f && Math.Abs(sideways) <= forward * 5.671282f + 0.1f; // 80 degrees, with contact tolerance.

        // A horizontal 18-degree cone expanded by the mob's collider radius. Height only limits
        // range: jumping over a deer or pitching the camera must not make it disappear from aim assist.
        public static bool InDashCone(float forward, float sideways, float height, float radius)
            => forward > 0f && forward * forward + sideways * sideways + height * height <= TargetRange * TargetRange &&
               Math.Abs(sideways) <= forward * 0.3249197f + Math.Max(0f, radius);
    }

    // Pauses a Time.time based clock the way vanilla's CharacterAnimEvent freezes the animator:
    // a repeated hit restarts the pause, and the clock resumes exactly where it stopped.
    public sealed class BloodRageHitStop
    {
        private double _until = double.NegativeInfinity, _last = double.NaN;
        public bool Frozen(double now) => now < _until;
        public void Begin(double now, float duration)
        {
            if (duration > 0f && !float.IsNaN(duration)) _until = now + Math.Min(duration, 1f);
        }
        public void Clear() { _until = double.NegativeInfinity; _last = double.NaN; }
        // Seconds the clock must skip since the previous call; call once per frame before reading elapsed time.
        public float Advance(double now)
        {
            double frozen = double.IsNaN(_last) ? 0d : Math.Max(0d, Math.Min(now, _until) - _last);
            _last = now;
            return (float)frozen;
        }
    }

    // An acknowledgment must match a live attack and a target owner recorded before sending.
    // Session reset drops all old replies; separate flags allow non-donors to grant ordinary Unarmed XP.
    public sealed class BloodRageRewards
    {
        private sealed class Swing
        {
            internal float Expires;
            internal bool Blood, Experience;
            internal int Splashes;
            internal readonly Dictionary<string, long> Targets = new Dictionary<string, long>();
        }
        private readonly Dictionary<int, Swing> _swings = new Dictionary<int, Swing>();
        public int Session { get; private set; }
        public void Reset(int session) { Session = session; _swings.Clear(); }
        public void Open(int sequence, float now)
        {
            var expired = new List<int>();
            foreach (var pair in _swings) if (pair.Value.Expires < now) expired.Add(pair.Key);
            foreach (int key in expired) _swings.Remove(key);
            if (_swings.Count >= 32) return;
            _swings[sequence] = new Swing { Expires = now + 5f };
        }
        public void Expect(int sequence, string target, long owner)
        {
            if (_swings.TryGetValue(sequence, out var swing)) swing.Targets[target] = owner;
        }
        public bool Confirm(int session, int sequence, string target, long sender, float now, bool donor, out bool experience)
            => Confirm(session, sequence, target, sender, now, donor, out experience, out _);
        public bool Confirm(int session, int sequence, string target, long sender, float now, bool donor, out bool experience, out bool splash)
        {
            experience = splash = false;
            if (session != Session || !_swings.TryGetValue(sequence, out var swing) || now > swing.Expires ||
                !swing.Targets.TryGetValue(target, out long owner) || owner != sender) return false;
            swing.Targets.Remove(target);
            experience = !swing.Experience;
            swing.Experience = true;
            splash = donor && swing.Splashes < 4;
            if (splash) ++swing.Splashes;
            if (!donor || swing.Blood) return false;
            swing.Blood = true;
            return true;
        }
    }

    // Per-instance, bounded ledgers. No static references to characters/worlds survive disconnects.
    internal sealed class BloodRageAttackLedger
    {
        private sealed class Ticket
        {
            internal int Phase;
            internal double Start;
            internal float Duration;
        }
        private readonly Dictionary<int, Ticket> _attacks = new Dictionary<int, Ticket>();
        private readonly Dictionary<string, double> _victimHits = new Dictionary<string, double>();
        private int _session, _sequence;
        private double _expires, _nextAttack, _secondaryReady;
        internal int Session => _session;
        internal bool Cancel(int session, int sequence, double now)
        {
            if (session != _session || sequence != _sequence) return false;
            _attacks.Remove(sequence);
            _nextAttack = now;
            return true;
        }
        internal void Begin(int session, double now)
        {
            if (session != 0 && session == _session) return;
            _session = session; _sequence = 0; _expires = now + BloodRageRules.Duration + 2d;
            _nextAttack = _secondaryReady = 0d;
            _attacks.Clear();
        }
        internal bool Attack(int session, int sequence, int phase, float duration, double started, double now)
        {
            if (session == 0 || session != _session || now > _expires || sequence <= _sequence || phase < 0 || phase > 4 ||
                float.IsNaN(duration) || duration < 0.1f || duration > 3f || double.IsNaN(started) || double.IsInfinity(started) ||
                Math.Abs(now - started) > 4d || started + 0.06d < _nextAttack ||
                (phase == BloodRageCombatRules.Secondary && started + 0.02d < _secondaryReady)) return false;
            var expired = new List<int>();
            foreach (var pair in _attacks) if (now - pair.Value.Start > 5d) expired.Add(pair.Key);
            foreach (int key in expired) _attacks.Remove(key);
            if (_attacks.Count >= 64) return false;
            _sequence = sequence;
            _nextAttack = started + duration;
            if (phase == BloodRageCombatRules.Secondary) _secondaryReady = started + BloodRageCombatRules.SecondaryCooldown;
            _attacks[sequence] = new Ticket { Phase = phase, Start = started, Duration = duration };
            return true;
        }
        internal bool CanHit(int session, int sequence, int phase, double now)
            => session != 0 && session == _session && now <= _expires && _attacks.TryGetValue(sequence, out var ticket) &&
               ticket.Phase == phase && now >= ticket.Start + ticket.Duration * 0.25d - 0.1d && now <= ticket.Start + ticket.Duration + 3d;
        internal bool AcceptVictim(string attacker, int session, int sequence, double now)
        {
            var expired = new List<string>();
            foreach (var pair in _victimHits) if (pair.Value < now) expired.Add(pair.Key);
            foreach (string key in expired) _victimHits.Remove(key);
            string id = attacker + ":" + session + ":" + sequence;
            if (_victimHits.Count >= 128 || _victimHits.ContainsKey(id)) return false;
            _victimHits[id] = now + 10d;
            return true;
        }
    }
}
