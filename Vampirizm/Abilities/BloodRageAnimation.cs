using System.Collections.Generic;
using UnityEngine;

namespace Vampirizm.Abilities
{
    // Primary swings: a layered full-body strike pose (hips, spine chain, shoulders, head, legs) authored
    // by BloodRageStrikeRules, native fist-clip footwork underneath it, and curved claw-arm sweeps on top.
    // The secondary raises and rakes both claws in place. No animator attack triggers or inherited
    // damage/footstep events are dispatched; only physics moves the character.
    // Primary hand goals follow the authored torso; all offsets use the committed attack heading.
    internal sealed class BloodRageAnimation
    {
        private struct Bone
        {
            internal Transform Transform;
            internal Quaternion Original, Applied;
            internal Vector3 OriginalPosition, AppliedPosition;
            internal Vector3 OriginalScale;
            internal bool Leg;
        }
        // Native fist clips contribute only leg/foot rotations (footwork); the torso is authored.
        private const float FootworkWeight = 0.4f, WristWeight = 0.6f;
        private readonly Player _player;
        private readonly List<Bone> _applied = new List<Bone>(128);
        private readonly BloodRageHitStop _hitStop = new BloodRageHitStop();
        private Transform _root, _hips, _spine, _spine1, _chest, _neck, _head, _leftShoulder, _rightShoulder;
        private Transform _leftUpLeg, _leftLeg, _leftFoot, _rightUpLeg, _rightLeg, _rightFoot;
        private Transform _leftArm, _leftForearm, _leftHand, _rightArm, _rightForearm, _rightHand, _leftFinger, _rightFinger;
        private float _start, _duration;
        private int _phase;
        private Vector3 _direction;
        private Quaternion _attackFrame, _armFrame;
        private bool _active, _sounded, _dashEffects;
        private ParticleSystem _bodyTrail, _rightTrail, _leftTrail;
        private Animator _animator;
        private Transform[] _bodyBones;
        private bool[] _legBones;
        private AnimationClip _source;
        private readonly Dictionary<AnimationClip, AnimationClip> _clips = new Dictionary<AnimationClip, AnimationClip>();

        // These are clip names in the shipped Player_animator, not its differently named triggers.
        // Never use Kickstep for a claw combo phase.
        internal static AnimationClip FindSource(Animator animator, int phase)
        {
            // Knife JumpAttack translates the skeleton ahead of the capsule, then pulls it back.
            // The lunge already moves the rigidbody; never layer a visual leap over it.
            if (phase == BloodRageCombatRules.Secondary || animator == null || animator.runtimeAnimatorController == null) return null;
            string expected = phase % 2 == 0 ? "Punchstep 1" : "Punchstep 2";
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == expected) return clip;
            return null;
        }
        internal bool Playing => _active && Time.time - _start < _duration;
        internal bool PrimaryPlaying => Playing && _phase != BloodRageCombatRules.Secondary;
        internal BloodRageAnimation(Player player) => _player = player;
        internal void Start(int phase, float duration, float start, Vector3 direction)
        {
            _phase = phase; _duration = duration; _start = start; _direction = direction; _active = true;
            _attackFrame = Quaternion.LookRotation(BloodRageMotion.Forward(direction, _player.transform.forward));
            _armFrame = _attackFrame;
            _sounded = false;
            _hitStop.Clear();
            StopTrails();
            _animator = _player.GetComponentInChildren<Animator>();
            AnimationClip original = FindSource(_animator, phase);
            _source = null;
            if (original != null)
            {
                if (!_clips.TryGetValue(original, out _source))
                {
                    _source = Object.Instantiate(original);
                    _source.name = "BloodRage " + original.name;
                    _source.events = System.Array.Empty<AnimationEvent>();
                    _clips.Add(original, _source);
                }
            }
        }
        internal void Apply()
        {
            Restore();
            if (!_active) return;
            // The pose holds on the contact frame; the clip resumes where it stopped.
            _start += _hitStop.Advance(Time.time);
            float t = (Time.time - _start) / _duration;
            if (t >= 1f || _player.IsDead() || _player.IsStaggering() || _player.InDodge()) { _active = false; StopTrails(); return; }
            bool secondary = _phase == BloodRageCombatRules.Secondary;
            if (!_sounded && t >= BloodRageCombatRules.HitWindowStart(_phase))
            {
                _sounded = true;
                // Each observer plays once, even when its body mesh is unavailable. Skip stale attacks.
                if (t <= BloodRageCombatRules.HitWindowEnd(_phase))
                {
                    Vector3 center = _player.GetCenterPoint();
                    if (secondary)
                    {
                        // Sword whoosh and a short camera kick on takeoff; attenuated by distance for observers.
                        // Knife/unarmed swing clips are nearly inaudible; two-handed clips carry a wind-up pre-roll.
                        EffectSpawner.SpawnLocalSound("sfx_sword_swing", center, 1f);
                        if (GameCamera.instance != null) GameCamera.instance.AddShake(center, 10f, 1.5f, false);
                    }
                    else EffectSpawner.SpawnLocalSound("sfx_sword_swing", center, 0.8f);
                }
            }
            if (secondary && _dashEffects && t >= BloodRageCombatRules.HitWindowEnd(_phase)) StopTrails();
            var equipment = _player.GetComponent<VisEquipment>();
            Transform root = equipment != null && equipment.m_bodyModel != null ? equipment.m_bodyModel.rootBone : null;
            if (root == null) return;
            if (_root != root) Bind(root, equipment);
            if (secondary) ApplySecondary(t);
            else ApplyPrimary(Mathf.Clamp01(t));
            if (_dashEffects) { DashTrailEffect.Step(_rightTrail); DashTrailEffect.Step(_leftTrail); }
        }

        private void Bind(Transform root, VisEquipment equipment)
        {
            _root = root;
            var bones = new List<Transform>(equipment.m_bodyModel.bones);
            // Some clips animate armature parents that are absent from the renderer's bone palette.
            for (Transform parent = root; parent != null && parent != _animator?.transform; parent = parent.parent)
            {
                if (parent == _player.transform) break;
                if (!bones.Contains(parent)) bones.Add(parent);
            }
            _bodyBones = bones.ToArray();
            _legBones = new bool[_bodyBones.Length];
            for (int i = 0; i < _bodyBones.Length; ++i)
            {
                string name = _bodyBones[i] != null ? _bodyBones[i].name : "";
                _legBones[i] = name.Contains("Leg") || name.Contains("Foot") || name.Contains("Toe");
            }
            _hips = Find("Hips"); _spine = Find("Spine"); _spine1 = Find("Spine1"); _chest = Find("Spine2");
            _neck = Find("Neck"); _head = Find("Head");
            _leftShoulder = Find("LeftShoulder"); _rightShoulder = Find("RightShoulder");
            _leftUpLeg = Find("LeftUpLeg"); _leftLeg = Find("LeftLeg"); _rightUpLeg = Find("RightUpLeg"); _rightLeg = Find("RightLeg");
            _leftFoot = Find("LeftFoot"); _rightFoot = Find("RightFoot");
            _leftArm = Find("LeftArm"); _leftForearm = Find("LeftForeArm"); _leftHand = Find("LeftHand");
            _rightArm = Find("RightArm"); _rightForearm = Find("RightForeArm"); _rightHand = Find("RightHand");
            _leftFinger = FingerBase(_leftHand); _rightFinger = FingerBase(_rightHand);
        }

        // A finger root (preferably the middle finger) gives a rig-independent "fingers point this way" axis.
        private static Transform FingerBase(Transform hand)
        {
            if (hand == null) return null;
            Transform first = null;
            foreach (Transform child in hand)
            {
                if (child.name.Contains("Middle")) return child;
                if (first == null) first = child;
            }
            return first;
        }

        private void ApplySecondary(float t)
        {
            if (!_dashEffects && t >= BloodRageCombatRules.HitWindowStart(_phase) && t < BloodRageCombatRules.HitWindowEnd(_phase))
            {
                _dashEffects = true;
                // Faint blood haze behind the body plus barely visible claw streaks; the trail must not
                // read as an effect on its own, only as a hint of motion. Hand trails step after the pose below.
                _bodyTrail = DashTrailEffect.Play(_player.transform, Vector3.up * 0.9f, 1.2f, 8f, new Color(0.5f, 0.02f, 0.04f, 0.1f), false, false);
                Color claw = new Color(0.8f, 0.04f, 0.07f, 0.18f);
                if (_rightHand != null) _rightTrail = DashTrailEffect.Play(_rightHand, Vector3.zero, 0.4f, 24f, claw, true, true);
                if (_leftHand != null) _leftTrail = DashTrailEffect.Play(_leftHand, Vector3.zero, 0.4f, 24f, claw, true, true);
            }
            // Anticipation, quick rake, then an early recovery into locomotion.
            float sweep = BloodRageMotion.SecondarySweepProgress(t);
            float recover = BloodRageCombatRules.HitWindowEnd(_phase);
            float weight = Mathf.SmoothStep(0f, 1f, t / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, (t - recover) / (1f - recover)));
            Offset(_chest, new Vector3(Mathf.Lerp(-16f, 36f, sweep), 0f, 0f), weight);
            BloodRageMotion.Hands(_phase, Mathf.Clamp01(t), out Vector3 right, out Vector3 left);
            Arm(_rightArm, _rightForearm, _rightHand, right, 1f, weight);
            Arm(_leftArm, _leftForearm, _leftHand, left, -1f, weight);
        }

        // Layer order: fist-clip footwork, authored body (hips → spine → shoulders → head, legs), claw arms, wrists.
        private void ApplyPrimary(float t)
        {
            // The authored pose carries its own anticipation/recovery; this envelope only blends the layers
            // into locomotion at the very start and after the swing is spent.
            float weight = BloodRageStrikeRules.PoseWeight(t);
            // Real-time sampling: the clip's own stepping speed, never a 2x shuffle compressed into the swing.
            SampleFootwork(Mathf.Clamp01(t * _duration / Mathf.Max(0.01f, _source != null ? _source.length : 1f)), weight * FootworkWeight);
            bool grounded = _player.IsOnGround();
            Vector3 leftPlant = _leftFoot != null ? _leftFoot.position : Vector3.zero;
            Vector3 rightPlant = _rightFoot != null ? _rightFoot.position : Vector3.zero;
            Quaternion leftSole = _leftFoot != null ? _leftFoot.rotation : Quaternion.identity;
            Quaternion rightSole = _rightFoot != null ? _rightFoot.rotation : Quaternion.identity;
            StrikePose pose = BloodRageStrikeRules.Pose(_phase, t);
            _armFrame = _attackFrame * BloodRageMotion.BodyRotation(pose, weight);
            Shift(_hips, new Vector3(pose.HipsSide, pose.HipsDrop, 0f) * weight);
            Offset(_hips, new Vector3(pose.HipsPitch, pose.HipsYaw, pose.HipsRoll), weight);
            if (grounded)
            {
                // Thighs undo the pelvis twist/tilt so the feet keep pointing at the target.
                Offset(_leftUpLeg, new Vector3(pose.ThighCounterPitch, pose.ThighCounterYaw, 0f), weight);
                Offset(_rightUpLeg, new Vector3(pose.ThighCounterPitch, pose.ThighCounterYaw, 0f), weight);
                Transform leadThigh = pose.Lead < 0 ? _leftUpLeg : _rightUpLeg, leadKnee = pose.Lead < 0 ? _leftLeg : _rightLeg;
                Transform rearThigh = pose.Lead < 0 ? _rightUpLeg : _leftUpLeg, rearKnee = pose.Lead < 0 ? _rightLeg : _leftLeg;
                Offset(leadThigh, new Vector3(pose.LeadThighPitch, 0f, 0f), weight);
                Offset(leadKnee, new Vector3(pose.LeadKneeBend, 0f, 0f), weight);
                Offset(rearThigh, new Vector3(pose.RearThighPitch, 0f, 0f), weight);
                Offset(rearKnee, new Vector3(pose.RearKneeBend, 0f, 0f), weight);
                // The pelvis can now shift and dip without dragging either sole through the floor.
                Plant(_leftUpLeg, _leftLeg, _leftFoot, leftPlant, leftSole, -1f, weight);
                Plant(_rightUpLeg, _rightLeg, _rightFoot, rightPlant, rightSole, 1f, weight);
            }
            Offset(_spine, new Vector3(pose.SpinePitch, pose.SpineYaw, 0f), weight);
            Offset(_spine1, new Vector3(pose.Spine1Pitch, pose.Spine1Yaw, 0f), weight);
            Offset(_chest, new Vector3(pose.ChestPitch, pose.ChestYaw, 0f), weight);
            Offset(_rightShoulder, new Vector3(0f, pose.RightShoulderYaw, pose.RightShoulderRoll), weight);
            Offset(_leftShoulder, new Vector3(0f, pose.LeftShoulderYaw, pose.LeftShoulderRoll), weight);
            Offset(_neck, new Vector3(0f, pose.NeckYaw, 0f), weight);
            Offset(_head, new Vector3(pose.HeadPitch, pose.HeadYaw, 0f), weight);
            // Both arms participate: the striking hand sweeps while the free hand follows in guard.
            // The hit volumes share these torso-driven curves.
            BloodRageMotion.Hands(_phase, t, out Vector3 right, out Vector3 left);
            bool rightHand = _phase != 1, leftHand = _phase == 1 || _phase >= 3;
            Arm(_rightArm, _rightForearm, _rightHand, right, 1f, weight);
            Arm(_leftArm, _leftForearm, _leftHand, left, -1f, weight);
            BloodRageStrikeRules.ClawDirection(_phase, out float up, out float forward);
            Vector3 claws = _attackFrame * new Vector3(0f, up, forward);
            if (rightHand) Wrist(_rightHand, _rightFinger, claws, weight * WristWeight);
            if (leftHand) Wrist(_leftHand, _leftFinger, claws, weight * WristWeight);
        }

        // Rotations only, legs/feet only: the fist clip supplies stepping footwork under the authored body.
        // Sampled translations are pinned throughout the armature; only our bounded pelvis weight shift
        // can move the mesh inside the capsule. Airborne swings leave the legs to the jump pose.
        private void SampleFootwork(float progress, float weight)
        {
            if (_source == null || _animator == null || _bodyBones == null || weight <= 0f || !_player.IsOnGround()) return;
            int first = _applied.Count;
            for (int i = 0; i < _bodyBones.Length; ++i)
            {
                Transform bone = _bodyBones[i];
                if (bone != null) _applied.Add(new Bone { Transform = bone, Original = bone.localRotation, Leg = _legBones[i],
                    OriginalPosition = bone.localPosition, OriginalScale = bone.localScale });
            }
            Vector3 playerPosition = _player.transform.position, animatorPosition = _animator.transform.localPosition;
            Vector3 playerScale = _player.transform.localScale, animatorScale = _animator.transform.localScale;
            Quaternion playerRotation = _player.transform.rotation, animatorRotation = _animator.transform.localRotation;
            _source.SampleAnimation(_animator.gameObject, progress * _source.length);
            _player.transform.SetPositionAndRotation(playerPosition, playerRotation);
            _player.transform.localScale = playerScale;
            _animator.transform.localPosition = animatorPosition;
            _animator.transform.localRotation = animatorRotation;
            _animator.transform.localScale = animatorScale;
            for (int i = first; i < _applied.Count; ++i)
            {
                Bone bone = _applied[i];
                float blend = bone.Leg ? weight : 0f;
                bone.Transform.localRotation = Quaternion.Slerp(bone.Original, bone.Transform.localRotation, blend);
                bone.Transform.localPosition = bone.OriginalPosition;
                bone.Transform.localScale = bone.OriginalScale;
                bone.Applied = bone.Transform.localRotation;
                bone.AppliedPosition = bone.Transform.localPosition;
                _applied[i] = bone;
            }
        }
        private Transform Find(string name)
        {
            foreach (Transform bone in _root.GetComponentsInChildren<Transform>(true)) if (bone.name == name) return bone;
            return null;
        }
        private void Save(Transform bone, Quaternion rotation)
        {
            var saved = new Bone { Transform = bone, Original = bone.localRotation, OriginalPosition = bone.localPosition };
            bone.rotation = rotation;
            saved.Applied = bone.localRotation;
            saved.AppliedPosition = bone.localPosition;
            _applied.Add(saved);
        }
        private void Offset(Transform bone, Vector3 angles, float weight)
        {
            if (bone == null || (angles.sqrMagnitude < 0.0001f)) return;
            Quaternion frame = _attackFrame;
            Quaternion delta = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(angles), weight);
            Save(bone, frame * delta * Quaternion.Inverse(frame) * bone.rotation);
        }
        private void Shift(Transform bone, Vector3 offset)
        {
            if (bone == null || offset.sqrMagnitude < 0.000001f) return;
            var saved = new Bone { Transform = bone, Original = bone.localRotation, Applied = bone.localRotation,
                OriginalPosition = bone.localPosition };
            bone.position += _attackFrame * offset;
            saved.AppliedPosition = bone.localPosition;
            _applied.Add(saved);
        }
        private void Plant(Transform upper, Transform lower, Transform foot, Vector3 position, Quaternion rotation, float side, float weight)
        {
            if (upper == null || lower == null || foot == null || weight <= 0f) return;
            Limb(upper, lower, foot, position, _attackFrame * new Vector3(side * 0.08f, 0f, 1f), weight);
            Save(foot, Quaternion.Slerp(foot.rotation, rotation, weight));
        }
        private void Arm(Transform upper, Transform lower, Transform hand, Vector3 localGoal, float side, float weight)
        {
            if (upper == null || lower == null || hand == null) return;
            Vector3 goal = BloodRageMotion.WorldPoint(_player, localGoal, _direction);
            // Elbows turn with the shoulder line, rather than being pulled toward the orbiting camera.
            Vector3 pole = _armFrame * new Vector3(side, 0f, -0.3f);
            Limb(upper, lower, hand, goal, pole, weight);
        }
        private void Limb(Transform upper, Transform lower, Transform hand, Vector3 goal, Vector3 bend, float weight)
        {
            Vector3 to = goal - upper.position;
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, hand.position);
            if (a < 0.01f || b < 0.01f || to.sqrMagnitude < 0.0001f) return;
            float d = Mathf.Clamp(to.magnitude, Mathf.Abs(a - b) + 0.01f, a + b - 0.01f);
            Vector3 direction = to.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(bend, direction);
            if (pole.sqrMagnitude < 0.0001f) pole = Vector3.ProjectOnPlane(lower.position - upper.position, direction);
            if (pole.sqrMagnitude < 0.0001f) pole = Vector3.Cross(direction, _attackFrame * Vector3.up);
            pole.Normalize();
            // Clamp the actual goal as well as the elbow triangle; never force a hyperextended joint.
            goal = upper.position + direction * d;
            float along = (a * a - b * b + d * d) / (2f * d);
            Vector3 elbow = upper.position + direction * along + pole * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Quaternion rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            Save(upper, Quaternion.Slerp(upper.rotation, rotation, weight));
            rotation = Quaternion.FromToRotation(hand.position - lower.position, goal - lower.position) * lower.rotation;
            Save(lower, Quaternion.Slerp(lower.rotation, rotation, weight));
        }
        // Turn the wrist so the fingers (and claws) point at the target instead of trailing the forearm.
        private void Wrist(Transform hand, Transform finger, Vector3 clawDirection, float weight)
        {
            if (hand == null || finger == null || weight <= 0f) return;
            Vector3 fingers = finger.position - hand.position;
            if (fingers.sqrMagnitude < 0.000001f || clawDirection.sqrMagnitude < 0.000001f) return;
            Quaternion rotation = Quaternion.FromToRotation(fingers, clawDirection) * hand.rotation;
            Save(hand, Quaternion.Slerp(hand.rotation, rotation, weight));
        }
        internal void Restore()
        {
            for (int i = _applied.Count - 1; i >= 0; --i)
            {
                Bone bone = _applied[i];
                if (bone.Transform != null)
                {
                    if (Quaternion.Angle(bone.Transform.localRotation, bone.Applied) < 0.01f)
                        bone.Transform.localRotation = bone.Original;
                    if ((bone.Transform.localPosition - bone.AppliedPosition).sqrMagnitude < 0.000001f)
                        bone.Transform.localPosition = bone.OriginalPosition;
                }
            }
            _applied.Clear();
        }
        private void StopTrails()
        {
            _dashEffects = false;
            DashTrailEffect.Stop(_bodyTrail); DashTrailEffect.Stop(_rightTrail); DashTrailEffect.Stop(_leftTrail);
            _bodyTrail = _rightTrail = _leftTrail = null;
        }
        internal void FreezeFrame(float duration)
        {
            if (_active) _hitStop.Begin(Time.time, duration);
        }
        internal void Clear()
        {
            Restore(); _active = false; _source = null;
            _hitStop.Clear();
            StopTrails();
            foreach (AnimationClip clip in _clips.Values) if (clip != null) Object.Destroy(clip);
            _clips.Clear();
        }
    }
}
