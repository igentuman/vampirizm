using System;
using System.Collections.Generic;
using UnityEngine;
using Vampirizm.Combat;
using Vampirizm.Core;
using Vampirizm.Networking;
using Vampirizm.Skills;

namespace Vampirizm.Abilities
{
    // One owner clock controls hit windows. Rendering/animation events never create damage.
    internal sealed class BloodRageCombat
    {
        private static readonly int HitMask = LayerMask.GetMask("character", "character_net", "character_noenv",
            "character_ghost", "hitbox", "Default", "Default_small", "static_solid", "piece", "piece_nonsolid", "vehicle");
        private static readonly int CharacterMask = LayerMask.GetMask("character", "character_net", "character_noenv", "character_ghost", "hitbox");
        internal static readonly int WallMask = LayerMask.GetMask("Default", "Default_small", "static_solid", "piece", "terrain", "blocker", "vehicle");
        private readonly Player _player;
        private readonly VampireController _controller;
        private readonly BloodRageAnimation _animation;
        private readonly BloodRageLunge _lunge;
        private readonly HashSet<GameObject> _hit = new HashSet<GameObject>();
        private readonly BloodRageRewards _rewards = new BloodRageRewards();
        private readonly BloodRageHitStop _hitStop = new BloodRageHitStop();
        private ItemDrop.ItemData _right, _left, _profile;
        private int _session, _sequence, _phase = -1;
        private float _start, _duration, _previous, _finishedAt, _secondaryReady;
        private readonly float[] _fistDurations = new float[4];
        private bool _warnedDuration;
        private bool _attacking, _buffered, _heldPrimary, _heldSecondary;
        private bool _inputAllowed = true;
        private Vector3 _direction;
        private HitData _damage;
        internal bool Busy => _attacking;
        internal bool VisualBusy => _animation.Playing;
        internal bool PrimaryVisualBusy => _animation.PrimaryPlaying;
        internal int Session => _session;

        internal BloodRageCombat(Player player, VampireController controller)
        {
            _player = player;
            _controller = controller;
            _animation = new BloodRageAnimation(player);
            _lunge = new BloodRageLunge(player);
        }

        internal void Begin()
        {
            _session = Guid.NewGuid().GetHashCode();
            if (_session == 0) _session = 1;
            _sequence = 0;
            _secondaryReady = 0f;
            _phase = -1;
            _buffered = _heldPrimary = _heldSecondary = false;
            _rewards.Reset(_session);
            _right = _player.RightItem;
            _left = _player.LeftItem;
            _player.UnequipItem(_right, false);
            _player.UnequipItem(_left, false);
            // Private item/shared-data copy: a blocker profile, never added to inventory or a shared prefab.
            _profile = _player.m_unarmedWeapon.m_itemData.Clone();
            _profile.m_shared = CloneShared(_profile.m_shared);
            _profile.m_shared.m_blockPower = 15f;
            _profile.m_shared.m_timedBlockBonus = 2f;
            _warnedDuration = false;
            for (int phase = 0; phase < 4; ++phase) _fistDurations[phase] = FistDuration(phase);
            var view = _player.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner())
            {
                view.GetZDO().Set(BloodRageNetwork.SessionKey, _session);
                view.GetZDO().Set(BloodRageNetwork.SessionStartedKey, ZNet.instance.GetTime().Ticks);
            }
        }

        private static ItemDrop.ItemData.SharedData CloneShared(ItemDrop.ItemData.SharedData shared)
            => (ItemDrop.ItemData.SharedData)typeof(object).GetMethod("MemberwiseClone",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(shared, null);

        internal ItemDrop.ItemData Blocker => _profile;

        internal void SetInputAllowed(bool allowed)
        {
            _inputAllowed = allowed;
            if (!allowed) _buffered = _heldPrimary = _heldSecondary = false;
        }

        internal void Input(bool primary, bool primaryHold, bool secondary, bool secondaryHold)
        {
            if (!_inputAllowed) return;
            _heldPrimary = primaryHold;
            _heldSecondary = secondaryHold;
            // Only one explicit primary can be buffered. Secondary never queues through its cooldown.
            if (_attacking) { if (primary && _phase != BloodRageCombatRules.Secondary) _buffered = true; return; }
            if (secondary || secondaryHold) TryStart(true);
            else if (primary || primaryHold) TryStart(false);
        }

        internal bool TryStart(bool secondary)
        {
            if (!_inputAllowed || _attacking || !_controller.BloodRage.Active || !_controller.CanChange || _controller.Dash.Active ||
                _player.IsStaggering() || _player.InDodge() || _player.InAttack() || _player.IsBlocking() ||
                _player.IsAttached() || _player.IsSwimming() || _player.IsTeleporting() || _player.InEmote() || _player.InPlaceMode() || _player.InMinorAction() ||
                _player.IsKnockedBack() || _player.InCutscene()) return false;
            if (secondary && Time.time < _secondaryReady) return false;
            var fist = _player.m_unarmedWeapon?.m_itemData;
            if (fist == null) return false;
            float stamina = Stamina(fist) + (secondary ? BloodRageCombatRules.SecondaryStaminaExtra : 0f);
            if (stamina > 0f && !_player.HaveStamina(stamina + 0.1f)) { Hud.instance?.StaminaBarEmptyFlash(); return false; }
            _player.UseStamina(stamina);
            _controller.Invisibility.End();
            _phase = secondary ? BloodRageCombatRules.Secondary : BloodRageCombatRules.NextPhase(_phase, Time.time - _finishedAt);
            _duration = BloodRageCombatRules.Duration(_phase, _fistDurations[Mathf.Min(_phase, 3)]);
            _hitStop.Clear();
            _start = Time.time;
            _previous = -1f;
            _attacking = true;
            _buffered = false;
            _hit.Clear();
            ++_sequence;
            _rewards.Open(_sequence, Time.time);
            _direction = BloodRageMotion.Forward(_player.GetLookDir(), _player.transform.forward);
            Character target = secondary ? FindTarget() : null;
            if (target != null)
                _direction = BloodRageMotion.Forward(target.GetCenterPoint() - _player.GetCenterPoint(), _direction);
            // Physics, claw contacts and every observer use the same target-adjusted horizontal heading.
            _player.transform.rotation = Quaternion.LookRotation(_direction);
            _damage = MakeHit(fist, secondary);
            if (secondary)
            {
                _secondaryReady = Time.time + BloodRageCombatRules.SecondaryCooldown;
                _lunge.Begin(_direction, target);
            }
            else _lunge.BeginStep(_direction, _phase, _duration);
            double started = BloodRageNetwork.Now;
            BloodRageNetwork.PublishAttack(_player, _session, _sequence, _phase, _duration, started, _direction);
            _animation.Start(_phase, _duration, Time.time, _direction);
            _player.AddNoise(10f);
            return true;
        }

        private float Stamina(ItemDrop.ItemData fist)
        {
            float cost = fist.m_shared.m_attack.m_attackStamina;
            if (cost <= 0f) return 0f;
            cost *= 1f + _player.GetEquipmentAttackStaminaModifier();
            _player.GetSEMan().ModifyAttackStaminaUsage(cost, ref cost);
            cost *= 1f - 0.33f * _player.GetSkillFactor(global::Skills.SkillType.Unarmed);
            cost -= (_player.GetMaxHealth() - _player.GetHealth()) * fist.m_shared.m_attack.m_staminaReturnPerMissingHP;
            return Mathf.Max(0f, cost);
        }

        private HitData MakeHit(ItemDrop.ItemData fist, bool secondary)
        {
            var attack = fist.m_shared.m_attack;
            var hit = new HitData
            {
                m_damage = fist.GetDamage(), m_blockable = true, m_dodgeable = true,
                m_skill = global::Skills.SkillType.Unarmed, m_skillRaiseAmount = 0f,
                m_skillLevel = _player.GetSkillLevel(global::Skills.SkillType.Unarmed),
                m_backstabBonus = fist.m_shared.m_backstabBonus, m_staggerMultiplier = attack.m_staggerMultiplier,
                m_hitType = HitData.HitType.PlayerHit, m_toolTier = (short)fist.m_shared.m_toolTier
            };
            hit.SetAttacker(_player);
            float skill = _player.GetRandomSkillFactor(global::Skills.SkillType.Unarmed);
            float scale = skill * attack.m_damageMultiplier * (1f + Mathf.Max(0, _player.GetLevel() - 1) * 0.5f);
            scale *= 1f + (_player.GetMaxHealth() - _player.GetHealth()) * attack.m_damageMultiplierPerMissingHP;
            scale *= 1f + (1f - _player.GetHealthPercentage()) * attack.m_damageMultiplierByTotalHealthMissing;
            scale *= VampireDamageRules.UnarmedMultiplier(VampirismSkill.Level(_player), Plugin.Instance.Settings.UnarmedDamageMultiplier.Value);
            hit.m_damage.Modify(scale);
            // Apply ordinary status attack modifiers to the bare-fist baseline, then add the flat claw bonus.
            _player.GetSEMan().ModifyAttack(global::Skills.SkillType.Unarmed, ref hit);
            float damage = BloodRageCombatRules.Damage(hit.GetTotalDamage(), secondary);
            hit.m_damage = new HitData.DamageTypes { m_slash = damage };
            hit.m_pushForce = fist.m_shared.m_attackForce * skill * attack.m_forceMultiplier;
            return hit;
        }

        internal void Update()
        {
            _lunge.UpdateGrounded();
            if (!_attacking) return;
            if (_player.IsKnockedBack() || _player.IsStaggering() || _player.InDodge() || _player.IsAttached() || _player.IsSwimming() || _player.IsDead())
            { Cancel(); return; }
            // Hit windows and the end of the commitment pause with the frozen pose.
            _start += _hitStop.Advance(Time.time);
            float elapsed = Time.time - _start;
            // Test interval crossing, not a single animation frame, including long frames.
            if (BloodRageCombatRules.InHitWindow(_previous, elapsed, _duration, _phase)) Hit(_previous, elapsed);
            _previous = elapsed;
            if (elapsed < _duration) return;
            _attacking = false;
            _finishedAt = Time.time;
            bool again = _buffered || _heldPrimary;
            _buffered = false;
            if (_heldSecondary) TryStart(true);
            else if (again) TryStart(false);
        }

        private void Hit(float previous, float elapsed)
        {
            float from = Mathf.Clamp(previous / _duration, BloodRageCombatRules.HitWindowStart(_phase), BloodRageCombatRules.HitWindowEnd(_phase));
            float to = Mathf.Clamp(elapsed / _duration, BloodRageCombatRules.HitWindowStart(_phase), BloodRageCombatRules.HitWindowEnd(_phase));
            int segments = Mathf.Max(1, Mathf.CeilToInt((to - from) / 0.035f));
            int before = _hit.Count;
            BloodRageMotion.Hands(_phase, from, out Vector3 right, out Vector3 left);
            for (int i = 1; i <= segments; ++i)
            {
                BloodRageMotion.Hands(_phase, Mathf.Lerp(from, to, i / (float)segments), out Vector3 nextRight, out Vector3 nextLeft);
                if (_phase != 1) Sweep(right, nextRight);
                if (_phase == 1 || _phase >= 3) Sweep(left, nextLeft);
                right = nextRight; left = nextLeft;
            }
            // Character capsules need a forgiving waist-to-chest contact volume, as native melee
            // uses extra character reach. Decorative hand arcs alone leave holes between the hands
            // and above low/quadruped bodies. Objects still require the actual claw sweep.
            Vector3 contact = _player.GetCenterPoint() + BloodRageMotion.Forward(_direction, _player.transform.forward) * 1f;
            DamageColliders(Physics.OverlapCapsule(contact - Vector3.up * 0.45f,
                contact + Vector3.up * 0.25f, BloodRageCombatRules.CharacterContactRadius, CharacterMask, QueryTriggerInteraction.Ignore), true);
            // Same impact beat as native melee: one freeze per update that reached a new mob or object,
            // before any damage confirmation. The RPC reaches every peer, including this owner.
            if (_hit.Count > before) _player.FreezeFrame(BloodRageCombatRules.HitStop);
        }

        // Character.RPC_FreezeFrame paused the vanilla animator; pause the claw clocks on this peer too.
        internal void FreezeFrame(float duration)
        {
            if (!_attacking && !_animation.Playing) return;
            if (_attacking) _hitStop.Begin(Time.time, duration);
            _animation.FreezeFrame(duration);
            _lunge.Freeze(duration);
        }

        private void Sweep(Vector3 from, Vector3 end)
        {
            // The same curves drive the rig and gameplay capsules, including skipped render frames.
            Collider[] colliders = Physics.OverlapCapsule(BloodRageMotion.WorldPoint(_player, from, _direction),
                BloodRageMotion.WorldPoint(_player, end, _direction), BloodRageCombatRules.ClawSweepRadius, HitMask, QueryTriggerInteraction.Ignore);
            DamageColliders(colliders, false);
        }

        private void DamageColliders(Collider[] colliders, bool charactersOnly)
        {
            Vector3 origin = _player.GetCenterPoint();
            Vector3 forward = BloodRageMotion.Forward(_direction, _player.transform.forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            foreach (Collider collider in colliders)
            {
                GameObject root = Projectile.FindHitObject(collider);
                if (root == null || root == _player.gameObject || _hit.Contains(root)) continue;
                var destructible = root.GetComponent<IDestructible>();
                if (destructible == null) continue;
                Character character = root.GetComponent<Character>();
                if (charactersOnly && character == null) continue;
                if (character != null && !ValidTarget(_player, character)) continue;
                Vector3 point = collider.ClosestPoint(origin);
                Vector3 to = point - origin;
                // A closest surface point can be below or beside us even when an animal's body
                // is straight ahead. Test facing against its centre in the horizontal attack frame.
                Vector3 facing = (character != null ? character.GetCenterPoint() : point) - origin;
                if (to.sqrMagnitude > BloodRageCombatRules.Reach * BloodRageCombatRules.Reach ||
                    !BloodRageCombatRules.InForwardArc(Vector3.Dot(facing, forward), Vector3.Dot(facing, right)) ||
                    Obstructed(origin, point, root)) continue;
                _hit.Add(root);
                HitData hit = _damage.Clone();
                hit.m_point = point;
                hit.m_dir = to.sqrMagnitude > 0.001f ? to.normalized : _direction;
                hit.m_hitCollider = collider;
                if (character != null)
                {
                    var view = character.GetComponent<ZNetView>();
                    if (view == null || !view.IsValid()) continue;
                    _rewards.Expect(_sequence, view.GetZDO().m_uid.ToString(), view.GetZDO().GetOwner());
                    BloodRageNetwork.RequestHit(character, _player, _session, _sequence, _phase, hit);
                }
                else destructible.Damage(hit);
            }
        }

        internal static bool ValidTarget(Player attacker, Character target)
        {
            if (target == null || target == attacker || target.IsDead() || target.IsTamed() ||
                target.GetComponent<Orb.SwarmBat>() != null || FriendlyRegistry.IsListed(target)) return false;
            if (target is Player player) return attacker.IsPVPEnabled() && player.IsPVPEnabled();
            // Passive wildlife (deer/hares) is attackable even when another mod changes hostility.
            // Donor eligibility is deliberately irrelevant to whether a claw can deal damage.
            return target.GetComponent<AnimalAI>() != null || BaseAI.IsEnemy(attacker, target);
        }

        internal static bool Obstructed(Vector3 origin, Vector3 point, GameObject target)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(origin, point - origin, Vector3.Distance(origin, point), WallMask, QueryTriggerInteraction.Ignore))
                if (Projectile.FindHitObject(hit.collider) != target) return true;
            return false;
        }

        private Character FindTarget()
        {
            Character best = null;
            float distance = float.PositiveInfinity, angle = 180f;
            Vector3 origin = _player.GetCenterPoint();
            Vector3 right = Vector3.Cross(Vector3.up, _direction);
            foreach (Character candidate in Character.GetAllCharacters())
            {
                if (!ValidTarget(_player, candidate)) continue;
                Vector3 to = candidate.GetCenterPoint() - origin;
                CapsuleCollider collider = candidate.GetCollider();
                float radius = collider != null ? collider.radius * Mathf.Max(Mathf.Abs(collider.transform.lossyScale.x),
                    Mathf.Abs(collider.transform.lossyScale.z)) : 0f;
                if (!BloodRageCombatRules.InDashCone(Vector3.Dot(to, _direction), Vector3.Dot(to, right), to.y, radius)) continue;
                float d = to.sqrMagnitude;
                float a = Vector3.Angle(_direction, Vector3.ProjectOnPlane(to, Vector3.up));
                if (d > distance || (Mathf.Approximately(d, distance) && a >= angle) ||
                    Obstructed(_player.GetEyePoint(), candidate.GetCenterPoint(), candidate.gameObject)) continue;
                best = candidate; distance = d; angle = a;
            }
            return best;
        }

        private float FistDuration(int phase)
        {
            Animator animator = _player.GetComponentInChildren<Animator>();
            AnimationClip clip = BloodRageAnimation.FindSource(animator, phase);
            if (clip != null) return clip.length;
            if (!_warnedDuration)
            {
                _warnedDuration = true;
                Plugin.Warn("Blood Rage: unarmed clip duration unavailable; using 0.8s fist baseline.");
            }
            return 0.8f;
        }

        internal void Confirm(long sender, int session, int sequence, ZDOID target, bool donor, Vector3 point)
        {
            if (!_controller.BloodRage.Active) return;
            bool blood = _rewards.Confirm(session, sequence, target.ToString(), sender, Time.time, donor, out bool xp, out bool splash);
            if (splash) BloodRageNetwork.PublishSplash(_player, point);
            if (blood) _controller.AddBlood(BloodRageCombatRules.Reward);
            if (xp) EffectSpawner.Spawn("sfx_unarmed_hit", point, Quaternion.identity, 1f, true);
            if (xp) _player.RaiseSkill(global::Skills.SkillType.Unarmed, _player.m_unarmedWeapon.m_itemData.m_shared.m_attack.m_raiseSkillAmount * 1.5f);
        }

        internal void FixedStep(float dt) => _lunge.FixedStep(dt);
        internal void PrimaryStep(float dt) => _lunge.PrimaryStep(dt);
        internal void RestorePose() => _animation.Restore();
        internal void Animate() => _animation.Apply();
        internal void Observe(int phase, float duration, float age, Vector3 direction) => _animation.Start(phase, duration, Time.time - age, direction);
        internal void Cancel()
        {
            if (_attacking && _player == Player.m_localPlayer)
                BloodRageNetwork.PublishCancel(_player, _session, _sequence);
            _attacking = _buffered = _heldPrimary = _heldSecondary = false;
            _phase = -1;
            _hitStop.Clear();
            _lunge.End();
            _animation.Clear();
        }
        internal void End()
        {
            Cancel();
            _rewards.Reset(0);
            _session = 0;
            _profile = null;
            // Clear references before equipping: repeated lifecycle cleanup cannot restore twice.
            ItemDrop.ItemData right = _right, left = _left;
            _right = _left = null;
            if (_player != Player.m_localPlayer || _player.IsDead() || _player.GetInventory() == null) return;
            if (right != null && _player.GetInventory().ContainsItem(right)) _player.EquipItem(right, false);
            if (left != null && left != right && _player.GetInventory().ContainsItem(left)) _player.EquipItem(left, false);
        }
    }
}
