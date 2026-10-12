using HarmonyLib;
using UnityEngine;
using Vampirizm.Core;

namespace Vampirizm.Abilities
{
    // A short burst of movement driven through the rigidbody, so the normal physics keeps resolving collisions.
    // The whole path is swept with the player's capsule before starting and again every physics step.
    // No dodge animation and no invulnerability frames.
    internal sealed class DashAbility
    {
        private const float Clearance = 0.2f;
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

        private readonly Player _player;
        private readonly VampireController _controller;
        private bool _active;
        private Vector3 _direction;
        private Vector3 _start;
        private float _distance;
        private float _speed;
        private float _endTime;
        private float _readyAt;

        internal DashAbility(Player player, VampireController controller)
        {
            _player = player;
            _controller = controller;
        }

        internal bool Active => _active;

        internal void TryStart()
        {
            var settings = Plugin.Instance.Settings;
            AbilityBlock block = AbilityRules.Check(_controller.IsActiveVampire, _controller.Level, settings.DashLevel.Value,
                IsBusy(), Time.time, _readyAt, _controller.State.Blood, settings.DashCost.Value);
            if (block == AbilityBlock.None)
            {
                Vector3 direction = Direction();
                float distance = AbilityRules.DashDistance(settings.DashDistance.Value,
                    ObstacleDistance(direction, settings.DashDistance.Value), Clearance);
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
            _speed = speed;
            // A blocked or stuck dash must not keep the controls locked.
            _endTime = Time.time + distance / speed * 1.5f + 0.1f;
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
            if (!_active) return;
            Vector3 travelled = _player.transform.position - _start;
            travelled.y = 0f;
            float remaining = _distance - travelled.magnitude;
            float step = Mathf.Min(_speed * dt, remaining);
            if (remaining <= 0.05f || ObstacleDistance(_direction, step + Clearance) <= step)
            {
                End();
                return;
            }
            Rigidbody body = Body(_player);
            Vector3 velocity = _direction * Mathf.Min(_speed, remaining / dt);
            if (_player.IsOnGround())
            {
                // Follow slopes instead of launching off them or pushing into them.
                velocity = Vector3.ProjectOnPlane(velocity, _player.GetLastGroundNormal()).normalized * velocity.magnitude;
                // The dash zeroes the move input, so vanilla applies standing friction, which drags the body
                // against the ground for the whole step. Vanilla resets the material at the start of the next step.
                CapsuleCollider capsule = _player.GetCollider();
                if (capsule != null)
                {
                    capsule.material.staticFriction = 0f;
                    capsule.material.dynamicFriction = 0f;
                    capsule.material.frictionCombine = PhysicsMaterialCombine.Minimum;
                }
            }
            else
            {
                velocity.y = body.linearVelocity.y;
            }
            body.linearVelocity = velocity;
        }

        // Idempotent; the vanilla movement code brings the body back to walking speed on the next step.
        internal void End()
        {
            if (!_active) return;
            _active = false;
            if (_player == null) return;
            Rigidbody body = Body(_player);
            if (body != null) body.linearVelocity = new Vector3(0f, Mathf.Min(0f, body.linearVelocity.y), 0f);
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
        private float ObstacleDistance(Vector3 direction, float distance)
        {
            CapsuleCollider capsule = _player.GetCollider();
            if (capsule == null) return 0f;
            Transform transform = capsule.transform;
            float radius = capsule.radius * RadiusScale;
            float half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            Vector3 center = transform.TransformPoint(capsule.center);
            Vector3 top = center + transform.up * half;
            // Raised bottom: small steps and slopes under the feet do not count as walls.
            Vector3 bottom = center - transform.up * half + Vector3.up * StepHeight;
            if (bottom.y > top.y) bottom = top;

            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, Hits, distance, ObstacleMask,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Rigidbody own = Body(_player);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider == null || hit.collider.attachedRigidbody == own || hit.collider.transform.IsChildOf(_player.transform))
                    continue;
                // Zero distance means an initial overlap with no usable normal.
                if (hit.distance <= 0f || hit.normal.y >= WalkableNormalY) continue;
                if (hit.distance < nearest) nearest = hit.distance;
            }
            return nearest;
        }

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
