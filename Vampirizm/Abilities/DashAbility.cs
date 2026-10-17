using HarmonyLib;
using UnityEngine;
using Vampirizm.Core;

namespace Vampirizm.Abilities
{
    // A short burst of movement driven through the rigidbody, so the normal physics keeps resolving collisions.
    // The whole path is swept with the player's capsule before starting and again every physics step.
    // Jump animation only: no jump impulse and no invulnerability frames.
    internal sealed class DashAbility
    {
        private const float Clearance = 0.2f;
        private const float SpeedMultiplier = 2f;
        private const float GroundSampleSpacing = 0.25f;
        private const float GroundSkin = 0.02f;
        private const float MaxGroundSlope = 1.2f;
        private static readonly int GroundMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece",
            "terrain", "blocker", "vehicle");
        private const float StepHeight = 0.35f;
        private const float MinDistance = 1f;
        // Hits on surfaces flatter than ~50° are ground the dash can follow, not walls.
        private const float WalkableNormalY = 0.64f;
        private const float RadiusScale = 0.9f;
        private static readonly int ObstacleMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece",
            "terrain", "blocker", "vehicle", "character", "character_net", "character_noenv");
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly AccessTools.FieldRef<Character, Rigidbody> Body =
            AccessTools.FieldRefAccess<Character, Rigidbody>("m_body");
        private static readonly System.Action<Player, bool> SetCrouch =
            AccessTools.MethodDelegate<System.Action<Player, bool>>(AccessTools.Method(typeof(Player), "SetCrouch"));

        private readonly Player _player;
        private readonly VampireController _controller;
        private bool _active;
        private Vector3 _direction;
        private Vector3 _start;
        private float _distance;
        private float _speed;
        private float _endTime;
        private float _readyAt;
        private ParticleSystem _trail;

        internal DashAbility(Player player, VampireController controller)
        {
            _player = player;
            _controller = controller;
        }

        internal bool Active => _active;
        internal bool OnCooldown => Time.time < _readyAt;

        internal void TryStart()
        {
            var settings = Plugin.Instance.Settings;
            AbilityBlock block = AbilityRules.Check(_controller.IsActiveVampire, _controller.Level, settings.DashLevel.Value,
                IsBusy(), Time.time, _readyAt, _controller.State.Blood, settings.DashCost.Value);
            if (block == AbilityBlock.None)
            {
                Vector3 direction = Direction();
                float distance = TraceGround(direction, settings.DashDistance.Value, out _);
                if (distance < MinDistance) block = AbilityBlock.NoRoom;
                else if (!_controller.SpendBlood(settings.DashCost.Value)) block = AbilityBlock.NoBlood;
                else
                {
                    Begin(direction, distance, settings.DashSpeed.Value);
                    _readyAt = Time.time + settings.DashCooldown.Value;
                    return;
                }
            }
            string message = Message(block, settings.DashLevel.Value);
            if (message != null) _player.Message(MessageHud.MessageType.TopLeft, message);
        }

        private void Begin(Vector3 direction, float distance, float speed)
        {
            _active = true;
            _direction = direction;
            _start = _player.transform.position;
            _distance = distance;
            _speed = speed * SpeedMultiplier;
            // A blocked or stuck dash must not keep the controls locked.
            _endTime = Time.time + distance / _speed * 1.5f + 0.1f;
            SetCrouch(_player, false);
            _player.GetZAnim().SetBool("onGround", false);
            _player.GetZAnim().SetTrigger("jump");
            _trail = DashTrailEffect.Play(_player.transform);
        }

        internal void Update()
        {
            if (!_active) return;
            if (!_controller.IsActiveVampire || !_controller.CanChange || _player.IsTeleporting() || _player.IsStaggering() ||
                _player.IsAttached() || _player.IsSwimming() || _controller.Drain.Active || Time.time > _endTime)
                End();
        }

        // From Character.UpdateWalking, after the vanilla velocity for this physics step has been set.
        internal void FixedStep(float dt)
        {
            if (!_active || dt <= 0f) return;
            Vector3 travelled = _player.transform.position - _start;
            travelled.y = 0f;
            float remaining = _distance - travelled.magnitude;
            if (remaining <= 0.05f)
            {
                End();
                return;
            }
            float step = TraceGround(_direction, Mathf.Min(_speed * dt, remaining), out Vector3 target);
            Vector3 displacement = target - _player.transform.position;
            // The body travels a straight chord this tick; check it as well as the sampled ground path.
            if (step <= 0.01f || ObstacleDistance(_player.transform.position, displacement.normalized,
                    displacement.magnitude + Clearance) <= displacement.magnitude + Clearance)
            {
                End();
                return;
            }
            Rigidbody body = Body(_player);
            // Aim at the next ground height even when contacts briefly disappear over a crest.
            body.linearVelocity = displacement / dt;
            // Vanilla enables gravity before this postfix. Compensate for this step's gravity so the
            // body reaches the sampled height without changing its gravity setting or teleporting it.
            if (body.useGravity) body.linearVelocity -= Physics.gravity * dt;
            // Keep the jump pose active while the physical capsule stays grounded.
            _player.GetZAnim().SetBool("onGround", false);
            _player.GetZAnim().SetBool("falling", false);
            CapsuleCollider capsule = _player.GetCollider();
            if (capsule != null)
            {
                capsule.material.staticFriction = 0f;
                capsule.material.dynamicFriction = 0f;
                capsule.material.frictionCombine = PhysicsMaterialCombine.Minimum;
            }
        }

        // Trace in short increments so hills, stairs and downhill sections are sampled at their actual
        // heights. Stop at unsupported ledges instead of launching across them or snapping far below.
        private float TraceGround(Vector3 direction, float distance, out Vector3 target)
        {
            target = _player.transform.position;
            CapsuleCollider capsule = _player.GetCollider();
            if (capsule == null) return 0f;
            float feetOffset = capsule.bounds.min.y - target.y;
            float travelled = 0f;
            while (travelled < distance)
            {
                float step = Mathf.Min(GroundSampleSpacing, distance - travelled);
                float reach = StepHeight + step * MaxGroundSlope;
                Vector3 next = target + direction * step;
                Vector3 origin = next + Vector3.up * (feetOffset + reach);
                int count = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, reach * 2f,
                    GroundMask, QueryTriggerInteraction.Ignore);
                if (count == Hits.Length) break;
                float nearest = float.PositiveInfinity;
                RaycastHit ground = default;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = Hits[i];
                    if (IsSelf(hit) || hit.distance >= nearest) continue;
                    ground = hit;
                    nearest = hit.distance;
                }
                if (float.IsPositiveInfinity(nearest) || ground.normal.y < WalkableNormalY) break;
                // A capsule resting on a slope needs its bottom slightly above the center ray's hit.
                float slopeOffset = capsule.radius * (1f / ground.normal.y - 1f);
                next.y = ground.point.y + slopeOffset + GroundSkin - feetOffset;
                Vector3 segment = next - target;
                if (ObstacleDistance(target, segment.normalized, segment.magnitude + Clearance)
                    <= segment.magnitude + Clearance) break;
                target = next;
                travelled += step;
            }
            return travelled;
        }

        // Idempotent; the vanilla movement code brings the body back to walking speed on the next step.
        internal void End()
        {
            if (!_active) return;
            _active = false;
            DashTrailEffect.Stop(_trail);
            _trail = null;
            if (_player == null) return;
            _player.GetZAnim().SetBool("onGround", _player.IsOnGround());
            Rigidbody body = Body(_player);
            if (body != null) body.linearVelocity = Vector3.zero;
        }

        private bool IsBusy()
            => _active || !_controller.CanChange || _controller.Drain.Active || _controller.BatForm.Active || !_player.IsOnGround() || _player.IsSwimming() ||
               _player.InAttack() || _player.InDodge() || _player.IsStaggering() || _player.IsAttached() || _player.InEmote() ||
               _player.IsEncumbered() || _player.InPlaceMode() || _player.IsTeleporting() || _player.IsKnockedBack();

        // Movement input relative to the camera; without input the dash goes forward.
        private Vector3 Direction()
        {
            Vector3 direction = _player.GetMoveDir();
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = _player.GetLookDir();
                direction.y = 0f;
            }
            if (direction.sqrMagnitude < 0.0001f) direction = _player.transform.forward;
            direction.y = 0f;
            return direction.normalized;
        }

        // Distance to the first non-walkable obstacle along the path, swept with the player's capsule.
        private float ObstacleDistance(Vector3 position, Vector3 direction, float distance)
        {
            CapsuleCollider capsule = _player.GetCollider();
            if (capsule == null) return 0f;
            Transform transform = capsule.transform;
            float radius = capsule.radius * RadiusScale;
            float half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            Vector3 center = transform.TransformPoint(capsule.center) + position - _player.transform.position;
            Vector3 top = center + transform.up * half;
            // Raised bottom: small steps and slopes under the feet do not count as walls.
            Vector3 bottom = center - transform.up * half + Vector3.up * StepHeight;
            if (bottom.y > top.y) bottom = top;

            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, Hits, distance, ObstacleMask,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            if (count == Hits.Length) return 0f;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (IsSelf(hit))
                    continue;
                // Zero distance means an initial overlap with no usable normal.
                if (hit.distance <= 0f || hit.normal.y >= WalkableNormalY) continue;
                if (hit.distance < nearest) nearest = hit.distance;
            }
            return nearest;
        }

        private bool IsSelf(RaycastHit hit)
            => hit.collider == null || hit.collider.attachedRigidbody == Body(_player) ||
               hit.collider.transform.IsChildOf(_player.transform);

        private static string Message(AbilityBlock block, float level)
        {
            switch (block)
            {
                case AbilityBlock.Locked:
                    return global::Localization.instance.Localize("$vampirizm_ability_locked",
                        global::Localization.instance.Localize("$vampirizm_action_dash"), Mathf.CeilToInt(level).ToString());
                case AbilityBlock.Cooldown: return "$vampirizm_dash_cooldown";
                case AbilityBlock.NoBlood: return "$vampirizm_ability_no_blood";
                case AbilityBlock.NoRoom: return "$vampirizm_dash_no_room";
                case AbilityBlock.Busy: return "$vampirizm_dash_busy";
                default: return null;
            }
        }
    }
}
