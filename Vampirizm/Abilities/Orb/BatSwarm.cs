using System.Collections.Generic;
using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Feeding;

namespace Vampirizm.Abilities.Orb
{
    // The caster's bats and their mode. Runs only on the caster's client, which owns every bat ZDO.
    internal sealed class BatSwarm
    {
        private const float LaunchTime = 0.45f;
        private const float LaunchSpeed = 10f;
        private const float LeaveTime = 4f;
        private const float ReturnTimeout = 12f;
        private const float ReturnReach = 1.3f;
        private const float DiveTimeout = 2f;
        private const float DiveGiveUpReach = 2f;
        private const float BiteReach = 0.9f;
        private const float ShieldRadius = 1.8f;
        private const float GuardRange = 8f;
        private const float GuardRetry = 0.5f;
        private const float RetargetRange = 10f;
        private const float AbsorbCooldown = 0.5f;
        private const float SteerGain = 3f;

        private readonly Player _player;
        private readonly VampireController _controller;
        private readonly List<SwarmBat> _bats = new List<SwarmBat>();
        private Character _target;
        private bool _targetDonor;
        private Vector3 _targetPoint;
        private float _endsAt;
        private bool _ending;
        private float _bloodGained;

        internal BatSwarm(Player player, VampireController controller)
        {
            _player = player;
            _controller = controller;
        }

        internal SwarmMode Mode { get; private set; }
        internal float Remaining => Mode == SwarmMode.None ? 0f : Mathf.Max(0f, _endsAt - Time.time);

        // A recast refreshes the duration and tops the circle up to the full bat count.
        internal void StartShield(int quality, Vector3 origin, Vector3 aim)
        {
            var settings = Plugin.Instance.Settings;
            bool refresh = Mode == SwarmMode.Shield;
            Mode = SwarmMode.Shield;
            _endsAt = Time.time + OrbRules.ShieldDuration(quality, settings.OrbShieldDuration.Value, settings.OrbShieldPerLevel.Value);
            int missing = OrbRules.BatCount(quality, settings.OrbBaseBats.Value, settings.OrbBatsPerLevel.Value) - ActiveCount();
            for (int i = 0; i < missing; i++) Launch(Spawn(origin), aim);
            LayoutShield();
            _player.Message(MessageHud.MessageType.TopLeft, refresh ? "$vampirizm_orb_shield_refresh" : "$vampirizm_orb_shield_on");
        }

        // Shield bats become the swarm without new spawns; otherwise a fresh swarm bursts out of the orb.
        // Without a target the bats fly along the aim and scatter away.
        internal void StartAttack(int quality, Vector3 origin, Vector3 aim, Character target)
        {
            var settings = Plugin.Instance.Settings;
            bool convert = OrbRules.ConvertsShield(Mode, false);
            if (!convert)
            {
                int count = OrbRules.BatCount(quality, settings.OrbBaseBats.Value, settings.OrbBatsPerLevel.Value);
                for (int i = 0; i < count; i++) Launch(Spawn(origin), aim);
            }
            Mode = SwarmMode.Attack;
            _target = target;
            _targetDonor = target != null && DonorRegistry.CanFeed(target);
            _targetPoint = target != null ? target.GetCenterPoint() : origin;
            _endsAt = Time.time + settings.OrbSwarmDuration.Value;
            _ending = target == null;
            _bloodGained = 0f;

            List<SwarmBat> active = Active();
            float interval = settings.OrbBiteInterval.Value;
            for (int i = 0; i < active.Count; i++)
            {
                SwarmBat bat = active[i];
                bat.Index = i;
                bat.Phase = Mathf.PI * 2f * i / active.Count;
                bat.Radius = Random.Range(2f, 3f);
                bat.Height = Random.Range(-0.2f, 0.9f);
                bat.Spin = (i % 2 == 0 ? 1f : -1f) * Random.Range(1.1f, 1.7f);
                // Time to reach the target first, then dives ripple across the interval.
                bat.NextDive = Time.time + 1.5f + OrbRules.FirstDiveDelay(i, active.Count, interval);
                if (target == null) Leave(bat, Scatter(aim));
                else if (convert) bat.SetState(BatState.Seek);
            }
            if (convert) _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_orb_shield_attack");
            if (target == null) _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_orb_miss");
        }

        internal void Update()
        {
            if (_bats.Count == 0)
            {
                if (Mode != SwarmMode.None) Finish();
                return;
            }
            _bats.RemoveAll(bat => bat == null || !bat.Alive);
            if (!CasterValid()) LeaveAll();
            else if (Mode == SwarmMode.Shield && Time.time >= _endsAt)
            {
                LeaveAll();
                _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_orb_shield_ended");
            }
            else if (Mode == SwarmMode.Attack && !_ending) UpdateTarget();

            for (int i = _bats.Count - 1; i >= 0; i--)
            {
                SwarmBat bat = _bats[i];
                bat.Claim();
                if (!Tick(bat)) _bats.RemoveAt(i);
            }
            if (_bats.Count == 0) Finish();
        }

        // The nearest ready shield bat takes the hit; each bat absorbs at most once per AbsorbCooldown.
        internal SwarmBat TakeHit()
        {
            if (Mode != SwarmMode.Shield) return null;
            Vector3 center = _player.GetCenterPoint();
            SwarmBat best = null;
            float bestDistance = float.MaxValue;
            foreach (SwarmBat bat in _bats)
            {
                if (bat == null || !bat.Alive || bat.Leaving || bat.AbsorbReadyAt > Time.time) continue;
                float distance = (bat.Position - center).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = bat;
                bestDistance = distance;
            }
            if (best != null) best.AbsorbReadyAt = Time.time + AbsorbCooldown;
            return best;
        }

        internal void LeaveAll()
        {
            foreach (SwarmBat bat in _bats)
            {
                if (bat == null || bat.Leaving) continue;
                Vector3 outward = bat.Position - _player.GetCenterPoint();
                outward.y = 0f;
                Leave(bat, Scatter(outward.sqrMagnitude > 0.01f ? outward.normalized : _player.transform.forward));
            }
            if (Mode == SwarmMode.Shield) Mode = SwarmMode.None;
            _ending = true;
        }

        // Logout or unload: bats vanish at once.
        internal void Clear()
        {
            foreach (SwarmBat bat in _bats)
                if (bat != null) bat.Despawn(false);
            _bats.Clear();
            Mode = SwarmMode.None;
            _target = null;
            _ending = false;
        }

        private bool Tick(SwarmBat bat)
        {
            var settings = Plugin.Instance.Settings;
            float now = Time.time;
            switch (bat.State)
            {
                case BatState.Launch:
                    bat.Steer(bat.Heading * LaunchSpeed);
                    if (bat.StateTime >= LaunchTime)
                    {
                        if (Mode == SwarmMode.Shield) bat.SetState(BatState.Circle);
                        else if (Mode == SwarmMode.Attack && !_ending) bat.SetState(BatState.Seek);
                        else Leave(bat, bat.Heading);
                    }
                    return true;
                case BatState.Circle:
                {
                    Vector3 home = _player.GetCenterPoint();
                    float angle = bat.Phase + bat.Spin * now;
                    SteerTo(bat, home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * bat.Radius + Vector3.up * bat.Height);
                    if (now < bat.NextDive) return true;
                    Character threat = OrbTargeting.Nearest(_player, home, GuardRange, true);
                    if (threat == null)
                    {
                        bat.NextDive = now + GuardRetry;
                        return true;
                    }
                    bat.DiveTarget = threat;
                    bat.SetState(BatState.Dive);
                    return true;
                }
                case BatState.Seek:
                    SteerTo(bat, OrbitPoint(bat, now));
                    if ((bat.Position - _targetPoint).magnitude <= bat.Radius + 1.5f) bat.SetState(BatState.Orbit);
                    return true;
                case BatState.Orbit:
                    SteerTo(bat, OrbitPoint(bat, now));
                    if (now < bat.NextDive || _target == null) return true;
                    bat.DiveTarget = _target;
                    bat.SetState(BatState.Dive);
                    return true;
                case BatState.Dive:
                    Dive(bat, settings.OrbBiteInterval.Value);
                    return true;
                case BatState.Return:
                {
                    Vector3 home = _player.GetCenterPoint();
                    SteerTo(bat, home);
                    if ((bat.Position - home).magnitude <= ReturnReach)
                    {
                        _bloodGained += _controller.AddBlood(OrbRules.ReturnBlood(1, settings.OrbBloodPerBat.Value));
                        bat.Despawn(true);
                        return false;
                    }
                    if (bat.StateTime < ReturnTimeout) return true;
                    bat.Despawn(true);
                    return false;
                }
                default:
                    bat.Steer((bat.Heading + Vector3.up * 0.6f).normalized * bat.Character.m_flyFastSpeed);
                    if (bat.StateTime < LeaveTime) return true;
                    bat.Despawn(true);
                    return false;
            }
        }

        // Bite on contact, or on dive timeout when still close; then back to the circle or the orbit.
        private void Dive(SwarmBat bat, float interval)
        {
            Character target = bat.DiveTarget;
            if (target == null || !OrbTargeting.IsValid(_player, target))
            {
                EndDive(bat, Time.time + interval * 0.5f);
                return;
            }
            Vector3 to = target.GetCenterPoint() - bat.Position;
            float distance = to.magnitude;
            bat.Steer(to.normalized * bat.Character.m_flyFastSpeed);
            float reach = BiteReach + target.GetRadius();
            bool timedOut = bat.StateTime >= DiveTimeout;
            if (distance <= reach || (timedOut && distance <= reach + DiveGiveUpReach))
            {
                Bite(bat, target);
                EndDive(bat, Time.time + interval);
            }
            else if (timedOut) EndDive(bat, Time.time + interval * 0.5f);
        }

        private void EndDive(SwarmBat bat, float nextDive)
        {
            bat.NextDive = nextDive;
            bat.DiveTarget = null;
            bat.SetState(Mode == SwarmMode.Shield ? BatState.Circle : BatState.Orbit);
        }

        // Hits are credited to the bat, so the victim fights back against the bats; the caster gets the skill.
        private void Bite(SwarmBat bat, Character target)
        {
            var settings = Plugin.Instance.Settings;
            Vector3 point = target.GetCenterPoint();
            Vector3 direction = point - bat.Position;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : bat.transform.forward;
            var hit = new HitData();
            hit.m_damage.m_pierce = settings.OrbBatDamage.Value;
            hit.m_point = point;
            hit.m_dir = direction;
            hit.m_skill = global::Skills.SkillType.BloodMagic;
            hit.m_hitType = target is Player ? HitData.HitType.EnemyHit : HitData.HitType.PlayerHit;
            hit.SetAttacker(bat.Character);
            target.Damage(hit);
            _player.RaiseSkill(global::Skills.SkillType.BloodMagic, settings.OrbSkillPerBite.Value);
            BloodSplashEffect.Play(point, Quaternion.LookRotation(-direction));
            EffectSpawner.SpawnRandom(settings.BatNoiseSounds.Value, point, true);
        }

        private void UpdateTarget()
        {
            if (_target != null && OrbTargeting.IsValid(_player, _target)) _targetPoint = _target.GetCenterPoint();
            else
            {
                // The ending keeps the last target's donor status when nothing is left to retarget.
                Character next = OrbTargeting.Nearest(_player, _targetPoint, RetargetRange, false);
                if (next == null)
                {
                    EndAttack();
                    return;
                }
                _target = next;
                _targetDonor = DonorRegistry.CanFeed(next);
                _targetPoint = next.GetCenterPoint();
            }
            if (Time.time >= _endsAt) EndAttack();
        }

        // Bats bring blood home from a donor, otherwise they leave.
        private void EndAttack()
        {
            _ending = true;
            _target = null;
            foreach (SwarmBat bat in _bats)
            {
                if (bat == null || bat.Leaving) continue;
                if (_targetDonor) bat.SetState(BatState.Return);
                else Leave(bat, Scatter(bat.Position - _targetPoint));
            }
        }

        private void Finish()
        {
            if (_bloodGained > 0f)
                _player.Message(MessageHud.MessageType.TopLeft,
                    global::Localization.instance.Localize("$vampirizm_blood_gained", Mathf.RoundToInt(_bloodGained).ToString()));
            Mode = SwarmMode.None;
            _target = null;
            _ending = false;
            _bloodGained = 0f;
        }

        private bool CasterValid()
            => _player != null && _controller.IsActiveVampire && !_player.IsDead() && !_player.IsTeleporting();

        private SwarmBat Spawn(Vector3 origin)
        {
            GameObject prefab = SwarmBatPrefab.Get(Plugin.Instance.Settings.OrbBatHealth.Value);
            if (prefab == null) return null;
            Vector3 forward = _player.transform.forward;
            GameObject instance = Object.Instantiate(prefab, origin + Random.insideUnitSphere * 0.25f, Quaternion.LookRotation(forward));
            SwarmBat bat = instance.GetComponent<SwarmBat>();
            if (bat == null)
            {
                Object.Destroy(instance);
                return null;
            }
            _bats.Add(bat);
            return bat;
        }

        // Fresh bats fan out of the orb before taking their places.
        private static void Launch(SwarmBat bat, Vector3 aim)
        {
            if (bat == null) return;
            bat.Heading = (Quaternion.Euler(Random.Range(-25f, 10f), Random.Range(-60f, 60f), 0f) * aim.normalized + Vector3.up * 0.25f).normalized;
            bat.SetState(BatState.Launch);
        }

        private static void Leave(SwarmBat bat, Vector3 heading)
        {
            bat.Heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.up;
            bat.DiveTarget = null;
            bat.SetState(BatState.Leave);
        }

        private static Vector3 Scatter(Vector3 direction)
        {
            Vector3 basis = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            return (Quaternion.Euler(Random.Range(-20f, 5f), Random.Range(-35f, 35f), 0f) * basis).normalized;
        }

        private void LayoutShield()
        {
            List<SwarmBat> active = Active();
            float interval = Plugin.Instance.Settings.OrbBiteInterval.Value;
            for (int i = 0; i < active.Count; i++)
            {
                SwarmBat bat = active[i];
                bat.Index = i;
                bat.Phase = Mathf.PI * 2f * i / active.Count;
                bat.Radius = ShieldRadius + Random.Range(-0.15f, 0.15f);
                bat.Height = ((i % 3) - 1) * 0.45f + 0.2f;
                bat.Spin = (i % 2 == 0 ? 1f : -1f) * 1.8f;
                bat.NextDive = Time.time + 1f + OrbRules.FirstDiveDelay(i, active.Count, interval);
                bat.DiveTarget = null;
                if (bat.State != BatState.Launch) bat.SetState(BatState.Circle);
            }
        }

        // Each bat circles the target on its own radius, height and direction, with a gentle bob; none hovers in place.
        private Vector3 OrbitPoint(SwarmBat bat, float now)
        {
            float angle = bat.Phase + bat.Spin * now;
            float bob = 0.3f * Mathf.Sin(now * 2f + bat.Phase);
            return _targetPoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * bat.Radius + Vector3.up * (bat.Height + bob);
        }

        private static void SteerTo(SwarmBat bat, Vector3 goal)
            => bat.Steer(Vector3.ClampMagnitude((goal - bat.Position) * SteerGain, bat.Character.m_flyFastSpeed));

        private int ActiveCount()
        {
            int count = 0;
            foreach (SwarmBat bat in _bats)
                if (bat != null && bat.Alive && !bat.Leaving) count++;
            return count;
        }

        private List<SwarmBat> Active()
        {
            var active = new List<SwarmBat>();
            foreach (SwarmBat bat in _bats)
                if (bat != null && bat.Alive && !bat.Leaving) active.Add(bat);
            return active;
        }
    }
}
