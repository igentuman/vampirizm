using HarmonyLib;
using UnityEngine;

namespace Vampirizm.Abilities
{
    // Owner physics only. Gravity/fall altitude bookkeeping stays with Character.
    internal sealed class BloodRageLunge
    {
        private static readonly AccessTools.FieldRef<Character, Rigidbody> Body = AccessTools.FieldRefAccess<Character, Rigidbody>("m_body");
        private static readonly AccessTools.FieldRef<Character, Vector3> GroundNormal = AccessTools.FieldRefAccess<Character, Vector3>("m_lastGroundNormal");
        private static readonly AccessTools.FieldRef<Character, Vector3> CurrentVel = AccessTools.FieldRefAccess<Character, Vector3>("m_currentVel");
        private static readonly int Mask = LayerMask.GetMask("Default", "Default_small", "static_solid", "piece", "terrain",
            "blocker", "vehicle", "character", "character_net", "character_noenv");
        // Walkable surfaces (normal within 60 degrees of up) never block a grounded step.
        private const float WalkableNormalY = 0.5f, StepClearance = 0.3f;
        private readonly Player _player;
        private bool _active, _airUsed, _primary, _hopPending, _dashing;
        private Character _target;
        private Vector3 _direction;
        private float _remaining, _until, _started, _speed, _frozenUntil, _stepDistance, _stepTravelled, _stepDuration;
        private int _stepPhase, _stepFrames;
        private string _blockedBy;
        private Vector3 _stepOrigin;
        internal BloodRageLunge(Player player) => _player = player;
        internal void UpdateGrounded() { if (_player.IsOnGround()) _airUsed = false; }
        // Primary micro-lunge: one collision-swept step whose travel follows BloodRageStrikeRules.Travel on the
        // attack clock, so the body is still during the wind-up, fastest on the impact beat and at rest
        // when recovery begins. Hit stop pauses it with the pose.
        internal void BeginStep(Vector3 aim, int phase, float duration)
        {
            if (!_player.IsOnGround() || duration <= 0f) return;
            _primary = true;
            _frozenUntil = 0f;
            _direction = BloodRageMotion.Forward(aim, _player.transform.forward);
            _stepDistance = BloodRageStrikeRules.StepDistance(phase);
            _stepTravelled = 0f;
            _stepPhase = phase; _stepFrames = 0; _blockedBy = null;
            _stepOrigin = _player.transform.position;
            _stepDuration = duration;
            _started = Time.time;
            _until = _started + duration * BloodRageStrikeRules.Settle;
            _active = true;
        }
        internal void Begin(Vector3 aim, Character target)
        {
            bool grounded = _player.IsOnGround();
            if (!grounded && _airUsed) return;
            if (!grounded) _airUsed = true;
            _primary = false;
            _frozenUntil = 0f;
            // Camera pitch and a target below the jump must never turn this into a dive.
            _direction = BloodRageMotion.Forward(aim, _player.transform.forward);
            if (_direction.sqrMagnitude < 0.001f) return;
            _started = Time.time;
            _until = _started + BloodRageCombatRules.SecondaryDashAt + BloodRageCombatRules.LungeDuration;
            _target = target;
            _hopPending = grounded;
            _dashing = false;
            _active = true;
        }

        private void StartDash()
        {
            _dashing = true;
            // Measure from the actual takeoff position, after the wind-up.
            Character target = _target;
            _target = null;
            // Character.GetRadius scales non-player radii by the magnitude of all three scale
            // axes (sqrt(3) even at unit scale), stopping too far from deer. Use actual surfaces.
            CapsuleCollider targetCollider = target != null ? target.GetCollider() : null;
            Vector3 surface = targetCollider != null ? targetCollider.ClosestPoint(_player.GetCenterPoint())
                : target != null ? target.GetCenterPoint() : _player.GetCenterPoint() + _direction;
            _remaining = target != null ? Mathf.Clamp(Vector3.Dot(surface - _player.GetCenterPoint(), _direction)
                - _player.GetRadius() - 0.1f, 0f, BloodRageCombatRules.LungeDistance) : BloodRageCombatRules.LungeDistance;
            _speed = BloodRageCombatRules.LungeDistance / BloodRageCombatRules.LungeDuration;
        }
        // Vanilla root motion stops with the frozen animator; hold horizontal propulsion the same way
        // and push the clock so the remaining travel still happens. Gravity and collisions continue.
        internal void Freeze(float duration)
        {
            if (_active && duration > 0f) _frozenUntil = Time.time + duration;
        }

        private bool Frozen(float dt)
        {
            if (Time.time >= _frozenUntil) return false;
            _started += dt;
            _until += dt;
            return true;
        }

        internal void FixedStep(float dt)
        {
            if (_primary || !_active || dt <= 0f) return;
            if (Interrupted() || Time.time >= _until) { End(); return; }
            Rigidbody body = Body(_player);
            if (body == null) { End(); return; }
            if (Frozen(dt)) { body.linearVelocity = Vector3.up * body.linearVelocity.y; return; }
            float elapsed = Time.time - _started;
            if (_hopPending && elapsed >= BloodRageCombatRules.SecondaryHopAt)
            {
                _hopPending = false;
                // Never add lift to an attack triggered in midair, or after walking off a ledge.
                if (_player.IsOnGround())
                {
                    Vector3 takeoff = body.linearVelocity;
                    takeoff.y = BloodRageCombatRules.SecondaryHopSpeed;
                    _player.ForceJump(takeoff, false);
                    _player.GetZAnim().SetBool("onGround", false);
                    _player.GetZAnim().SetTrigger("jump");
                    _airUsed = true;
                    // Allow at least one physics step of lift, even after a delayed tick.
                    return;
                }
            }
            if (elapsed < BloodRageCombatRules.SecondaryDashAt) return;
            if (!_dashing) StartDash();
            if (!TryAdvance(dt, Mathf.Min(_remaining, _speed * Mathf.Min(dt, _until - Time.time)), out Vector3 velocity, out float moved)) return;
            _remaining -= moved;
            // Propel horizontally; native gravity and collisions own the entire vertical trajectory.
            velocity.y = body.linearVelocity.y;
            body.linearVelocity = velocity;
        }

        private bool Interrupted() => _player.IsDead() || _player.IsStaggering() || _player.InDodge() ||
            _player.IsKnockedBack() || _player.IsTeleporting() || _player.IsAttached() || _player.IsSwimming();

        // Runs from a postfix on Character.UpdateWalking, after vanilla has queued its own velocity change
        // for this physics step. A direct Rigidbody write here wins, exactly as the shipped Dash and Bat
        // Form motion do; a patch on the tiny private ApplyRootMotion never fired in-game (inlined).
        internal void PrimaryStep(float dt)
        {
            if (!_primary || !_active) return;
            // Once a step leaves a ledge, ordinary airborne momentum/gravity take over.
            if (!_player.IsOnGround()) { End("airborne"); return; }
            Rigidbody body = Body(_player);
            if (body == null) { End("no body"); return; }
            ++_stepFrames;
            if (Frozen(dt)) { Write(body, Vector3.zero); return; }
            float progress = (Time.time - _started) / _stepDuration;
            if (progress >= BloodRageStrikeRules.Settle) { End("settled"); return; }
            // Vanilla braking owns the wind-up; nothing to add until the step starts.
            float wanted = BloodRageStrikeRules.Travel(progress) * _stepDistance - _stepTravelled;
            if (wanted <= 0.0005f) return;
            if (!TryAdvance(dt, wanted, out Vector3 step, out float moved)) return;
            _stepTravelled += moved;
            Write(body, step);
        }

        // UpdateWalking has already queued AddForce(currentVel - bodyVel, VelocityChange): a brake toward
        // its (zero) walking velocity. Left in place it survives a plain velocity write and the body ends
        // each physics step at step - previousStep, a half-speed 25 Hz stutter. Queue its exact negation
        // first, then write: if the setter discards queued changes both vanish, if it keeps them they
        // cancel, and either way the body leaves this step at exactly the requested velocity.
        private void Write(Rigidbody body, Vector3 horizontal)
        {
            Vector3 before = body.linearVelocity, walking = CurrentVel(_player);
            Vector3 queued = new Vector3(walking.x - before.x, 0f, walking.z - before.z);
            if (queued.sqrMagnitude > 0.0001f && queued.magnitude <= 20f) body.AddForce(-queued, ForceMode.VelocityChange);
            Vector3 velocity = before;
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
            if (horizontal.y > 0f) velocity.y = Mathf.Max(velocity.y, horizontal.y);
            body.linearVelocity = velocity;
        }

        private bool TryAdvance(float dt, float distance, out Vector3 velocity, out float moved)
        {
            velocity = Vector3.zero;
            moved = 0f;
            if (!_active || dt <= 0f || distance <= 0f) return false;
            if (Time.time >= _until || Interrupted())
            { End("interrupted"); return false; }
            Rigidbody body = Body(_player);
            CapsuleCollider capsule = _player.GetCollider();
            if (body == null || capsule == null) { End("no capsule"); return false; }
            Vector3 direction = _primary ? Vector3.ProjectOnPlane(_direction, GroundNormal(_player)).normalized : _direction;
            Vector3 scale = capsule.transform.lossyScale;
            float fullRadius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float radius = fullRadius * 0.95f;
            float half = Mathf.Max(0f, capsule.height * Mathf.Abs(scale.y) * 0.5f - fullRadius);
            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            Vector3 top = center + Vector3.up * half;
            // Grounded steps query from step height upward: terrain bumps, stairs and the ground itself
            // are vanilla's business (the rigidbody walks over them); only walls and bodies block.
            Vector3 bottom = _primary ? center - Vector3.up * Mathf.Max(-half, half - StepClearance) : center - Vector3.up * half;
            // Fail closed in an initial blocking overlap, while allowing movement away from the ground.
            foreach (Collider other in Physics.OverlapCapsule(top, bottom, radius, Mask, QueryTriggerInteraction.Ignore))
            {
                if (other.attachedRigidbody == body || other.transform.IsChildOf(_player.transform)) continue;
                if (Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out Vector3 normal, out float depth) &&
                    depth > 0.02f && Vector3.Dot(normal, direction) < -0.05f && normal.y < WalkableNormalY)
                { Block(other); return false; }
            }
            Collider blocker = null;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(top, bottom, radius, direction, distance + 0.05f, Mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody == body || hit.collider.transform.IsChildOf(_player.transform)) continue;
                if (Vector3.Dot(hit.normal, direction) >= -0.01f || (_primary && hit.normal.y >= WalkableNormalY)) continue;
                float limit = Mathf.Max(0f, hit.distance - 0.05f);
                if (limit < distance) { distance = limit; blocker = hit.collider; }
            }
            if (distance <= 0.005f) { Block(blocker); return false; }
            moved = distance;
            velocity = direction * (distance / dt);
            return true;
        }
        // A blocked primary step holds this frame and retries; the secondary dash stops for good.
        private void Block(Collider blocker)
        {
            if (_blockedBy == null) _blockedBy = blocker != null ? blocker.name : "overlap";
            if (!_primary) End("blocked");
        }
        internal void End() => End("cancel");
        private void End(string reason)
        {
            if (_active && _primary)
            {
                // TEMPORARY diagnostics for the primary step; remove once in-game travel is confirmed.
                float actual = Vector3.Dot(_player.transform.position - _stepOrigin, _direction);
                Plugin.LogInfo($"[RageStep] phase={_stepPhase} requested={_stepTravelled:0.000} actual={actual:0.000} target={_stepDistance:0.00}m " +
                    $"frames={_stepFrames} end={reason} blocked={(_blockedBy ?? "none")} grounded={_player.IsOnGround()}");
            }
            // Primary steps use vanilla walking/gravity, including braking on the next tick.
            if (_active && !_primary && _dashing)
            {
                Rigidbody body = Body(_player);
                // Remove dash momentum without cancelling the hop, a fall, or a ceiling collision.
                if (body != null) body.linearVelocity = Vector3.up * body.linearVelocity.y;
            }
            _active = _hopPending = _dashing = false;
            _frozenUntil = 0f;
            _target = null;
        }
    }
}
