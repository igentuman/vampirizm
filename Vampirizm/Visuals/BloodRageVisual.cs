using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Jotunn.Managers;
using SoftReferenceableAssets;
using UnityEngine;
using UnityEngine.Rendering;
using Vampirizm.Networking;
using Object = UnityEngine.Object;

namespace Vampirizm.Visuals
{
    // Per-player cosmetic attachments, transformation poses and additive surface copies.
    internal sealed class BloodRageVisual
    {
        private readonly Player _player;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Attachment> _attachments = new List<Attachment>();
        private readonly List<FingerPose> _fingerPoses = new List<FingerPose>();
        private readonly List<ActivationPose> _activationPoses = new List<ActivationPose>();
        private bool _wasActive;
        private float _activationTime = ActivationDuration;
        private const float ActivationDuration = 1.25f;
        private readonly Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> _glints = new Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>();
        private SkinnedMeshRenderer _body;
        private Mesh _bodyMesh;
        private Material _ivory, _wing, _glint;
        private Texture2D _glintTexture;
        private SoftReference<Material> _glintTemplate;
        private bool _holdsTemplate;
        private bool _failed;
        private float _refreshAt;
        private Mesh _wingMesh;
        private Vector3[] _wingRest, _wingVertices, _wingRestNormals, _wingNormals;
        private float _wingReach, _wingPhase, _wingSpeed, _wingTurn, _wingLift;
        private Vector3 _lastPosition, _lastForward;
        private bool _hasWingMotion;
        private bool _wingJumpPending;
        private float _wingJumpTime = 1f;
        private const float WingJumpDuration = 0.65f;
        private static readonly Vector3 WingRoot = new Vector3(0.10f, -0.04f, -0.08f);
        // Explicit equipment instances: shoulder/cape, utility, hair, back and held items are excluded.
        private static readonly FieldInfo RightItem = AccessTools.Field(typeof(Humanoid), "m_rightItem");
        private static readonly FieldInfo LeftItem = AccessTools.Field(typeof(Humanoid), "m_leftItem");
        private static readonly FieldInfo[] ArmorFields = {
            AccessTools.Field(typeof(VisEquipment), "m_chestItemInstances"),
            AccessTools.Field(typeof(VisEquipment), "m_legItemInstances"),
            AccessTools.Field(typeof(VisEquipment), "m_helmetItemInstance")
        };

        private sealed class Attachment
        {
            internal Transform Root, Bone;
            internal bool Finger;
            internal Vector3 Direction, Up;
            internal float TipOffset;
            internal Quaternion BoneRotation;
        }

        private sealed class FingerPose
        {
            internal Transform Bone;
            internal Quaternion Rest, Original, Applied;
            internal bool HasApplied;
        }

        private sealed class ActivationPose
        {
            internal Transform Bone;
            internal Vector3 Angles;
            internal Quaternion Original, Applied;
            internal bool HasApplied;
        }

        internal BloodRageVisual(Player player) => _player = player;

        internal void Update(bool active, Action attackPose = null)
        {
            if (!active) { Clear(); _failed = false; return; }
            if (_failed || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var equipment = _player.GetComponent<VisEquipment>();
            var body = equipment != null ? equipment.m_bodyModel : null;
            if (body == null || body.sharedMesh == null) return;
            try
            {
                if (_body != body || _bodyMesh != body.sharedMesh)
                {
                    Clear(false);
                    _body = body;
                    _bodyMesh = body.sharedMesh;
                    Build();
                }
                bool visible = body.enabled && !body.forceRenderingOff && body.gameObject.activeInHierarchy &&
                    !VampireNetwork.IsInvisible(_player) && !VampireNetwork.IsBatForm(_player);
                if (!_wasActive && visible)
                {
                    _wasActive = true;
                    _activationTime = 0f;
                    BloodRageActivationEffect.Play(_player);
                }
                UpdateActivationPose(visible);
                attackPose?.Invoke();
                UpdateFingerPose(visible);
                UpdateWings(visible);
                foreach (Attachment attachment in _attachments)
                {
                    if (attachment.Root == null || attachment.Bone == null) continue;
                    // Compensate for the vanilla armature's 100x scale; mesh dimensions are metres.
                    Vector3 scale = attachment.Bone.lossyScale;
                    attachment.Root.localScale = new Vector3(1f / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                        1f / Mathf.Max(Mathf.Abs(scale.y), 0.0001f), 1f / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
                    Vector3 direction = attachment.Bone.TransformDirection(attachment.Direction);
                    if (!attachment.Finger)
                        attachment.Root.rotation = attachment.Bone.rotation * attachment.BoneRotation;
                    else if (direction.sqrMagnitude > 0.000001f)
                        attachment.Root.rotation = Quaternion.LookRotation(direction, attachment.Bone.TransformDirection(attachment.Up));
                    attachment.Root.position = attachment.Bone.position +
                        (attachment.Finger ? direction.normalized * attachment.TipOffset : Vector3.zero);
                    attachment.Root.gameObject.SetActive(visible);
                }
                EnsureGlint();
                if (Time.unscaledTime >= _refreshAt)
                {
                    _refreshAt = Time.unscaledTime + 0.5f;
                    RefreshGlints(equipment);
                }
                foreach (var pair in _glints)
                {
                    if (pair.Value == null) continue;
                    bool shown = visible && pair.Key != null && pair.Key.enabled && !pair.Key.forceRenderingOff &&
                        pair.Key.gameObject.activeInHierarchy && pair.Key.sharedMesh != null &&
                        pair.Key.sharedMesh == pair.Value.sharedMesh;
                    pair.Value.gameObject.SetActive(shown);
                    if (!shown) continue;
                    pair.Value.localBounds = pair.Key.localBounds;
                    for (int i = 0; i < pair.Key.sharedMesh.blendShapeCount; i++)
                        pair.Value.SetBlendShapeWeight(i, pair.Key.GetBlendShapeWeight(i));
                }
                float pulse = Mathf.Sin(Mathf.Clamp01(_activationTime / ActivationDuration) * Mathf.PI);
                if (_glint != null)
                {
                    float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
                    _glint.SetColor("_Color", new Color(0.65f + pulse * 0.65f + shimmer * 0.2f, 0.012f, 0.025f, 1f));
                    _glint.SetTextureOffset("_MainTex", new Vector2(0f, Mathf.Repeat(-Time.time * 0.45f, 1f)));
                }
                _activationTime = Mathf.Min(_activationTime + Time.deltaTime, ActivationDuration);
            }
            catch (Exception exception)
            {
                Clear();
                _failed = true;
                Plugin.Warn("Blood Rage visuals unavailable: " + exception.Message);
            }
        }

        private void Build()
        {
            if (_body.sharedMaterials.Length == 0) return;
            _ivory = SurfaceMaterial(_body.sharedMaterials[0], "BloodRageClaws", new Color(0.88f, 0.76f, 0.64f));
            _wing = SurfaceMaterial(_body.sharedMaterials[0], "BloodRageWings", new Color(0.16f, 0.012f, 0.025f));
            // Humanoid mappings first; skeleton-name fallback also supports Valheim's generic rig.
            var fingers = new HashSet<Transform>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftIndexDistal,
                    HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftLittleDistal,
                    HumanBodyBones.RightThumbDistal, HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleDistal,
                    HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleDistal })
                {
                    Transform finger = animator.GetBoneTransform(bone);
                    if (finger != null) fingers.Add(finger);
                }
            }
            if (fingers.Count == 0)
            {
                foreach (Transform bone in _body.rootBone.GetComponentsInChildren<Transform>(true))
                {
                    string name = bone.name.ToLowerInvariant().Replace("_", "").Replace(".", "");
                    bool finger = name.Contains("thumb") || name.Contains("index") || name.Contains("middle") ||
                        name.Contains("ring") || name.Contains("pinky") || name.Contains("little") || name.Contains("finger");
                    if (finger && (name.EndsWith("3") || name.EndsWith("distal"))) fingers.Add(bone);
                }
            }
            Mesh claw = ClawMesh();
            foreach (Transform finger in fingers)
            {
                Attach("BloodRageClaw", finger, claw, _ivory, true);
                AddFingerPose(finger);
            }
            if (fingers.Count == 0) Plugin.Warn("Blood Rage: no distal finger bones found; claws unavailable on this model.");
            Transform spine = Array.Find(_body.bones, bone => bone != null && bone.name == "Spine2") ??
                Array.Find(_body.bones, bone => bone != null && bone.name == "Spine1") ?? _body.rootBone;
            AddActivationPose(spine, new Vector3(0f, 0f, 0f));
            AddActivationPose(Array.Find(_body.bones, bone => bone != null && bone.name == "Head"), new Vector3(-22f, 0f, 0f));
            AddActivationPose(Array.Find(_body.bones, bone => bone != null && bone.name == "LeftArm"), new Vector3(-18f, -12f, -32f));
            AddActivationPose(Array.Find(_body.bones, bone => bone != null && bone.name == "RightArm"), new Vector3(-18f, 12f, 32f));
            if (!AttachBatWings(spine))
            {
                Plugin.Warn("Blood Rage: bat wing mesh unavailable; using procedural wings.");
                Attach("BloodRageWings", spine, WingsMesh(), _wing, false);
            }
        }

        private void EnsureGlint()
        {
            if (_glint != null || _holdsTemplate || !AssetManager.Instance.IsReady()) return;
            _glintTemplate = AssetManager.Instance.GetSoftReference<Material>("BloodLightning_Splat");
            if (!_glintTemplate.IsValid) return;
            _holdsTemplate = true;
            if (_glintTemplate.Load() != LoadResult.Succeeded) return;
            _glint = new Material(_glintTemplate.Asset) { name = "BloodRageGlint" };
            _glint.shaderKeywords = Array.Empty<string>();
            // A repeating diagonal highlight travels over the skinned surfaces instead
            // of washing the entire character in a flat red tint. Keep a faint red base.
            const int size = 64;
            _glintTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BloodRageGlintMask",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float phase = Mathf.Repeat((x + y) / (float)size, 1f);
                float distance = Mathf.Min(phase, 1f - phase);
                float highlight = Mathf.Pow(Mathf.Clamp01(1f - distance / 0.18f), 3f);
                float brightness = 0.07f + highlight * 0.93f;
                pixels[y * size + x] = new Color(brightness, brightness, brightness, 1f);
            }
            _glintTexture.SetPixels(pixels);
            _glintTexture.Apply(false, true);
            _glint.SetTexture("_MainTex", _glintTexture);
            _glint.SetTextureScale("_MainTex", Vector2.one);
            // Strip particle-only features inherited from the blood splat template.
            _glint.SetFloat("_EmissionEnabled", 0f);
            _glint.SetFloat("_SoftParticlesEnabled", 0f);
            _glint.SetFloat("_CameraFadingEnabled", 0f);
            _glint.SetFloat("_DistortionEnabled", 0f);
            _glint.SetFloat("_ColorMode", 0f);
            _glint.SetFloat("_Mode", 4f);
            _glint.SetInt("_BlendOp", (int)BlendOp.Add);
            _glint.SetInt("_SrcBlend", (int)BlendMode.One);
            _glint.SetInt("_DstBlend", (int)BlendMode.One);
            _glint.SetInt("_ZWrite", 0);
            _glint.SetInt("_Cull", (int)CullMode.Back);
            _glint.EnableKeyword("_ALPHABLEND_ON");
            _glint.SetOverrideTag("RenderType", "Transparent");
            _glint.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Material SurfaceMaterial(Material source, string name, Color color)
        {
            var material = new Material(source) { name = name };
            foreach (string property in material.GetTexturePropertyNames())
                material.SetTexture(property, property == "_MainTex" ? Texture2D.whiteTexture : null);
            foreach (string property in new[] { "_Color", "_SkinColor", "_SkinColor1", "_SkinColor2" })
                if (material.HasProperty(property)) material.SetColor(property, color);
            material.DisableKeyword("_NORMALMAP");
            return material;
        }

        private void Attach(string name, Transform bone, Mesh mesh, Material material, bool finger)
        {
            var go = new GameObject(name) { layer = _body.gameObject.layer };
            _objects.Add(go);
            go.transform.SetParent(bone, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            // Distal bone origins are at the last knuckle, not at the nail. Use the
            // terminal child to locate the fingertip and follow the distal bone's own axis.
            Vector3 tip = bone.parent != null ? bone.position - bone.parent.position : bone.up * 0.03f;
            if (finger)
            {
                foreach (Transform child in bone)
                    if (child.name.EndsWith("_end", StringComparison.OrdinalIgnoreCase))
                    {
                        tip = child.position - bone.position;
                        break;
                    }
                if (tip.sqrMagnitude < 0.000001f && bone.parent != null)
                    tip = bone.position - bone.parent.position;
            }
            Vector3 localDirection = bone.InverseTransformDirection(tip.normalized);
            Vector3 localUp = Vector3.ProjectOnPlane(Vector3.forward, localDirection).normalized;
            if (localUp.sqrMagnitude < 0.001f) localUp = Vector3.ProjectOnPlane(Vector3.right, localDirection).normalized;
            _attachments.Add(new Attachment { Root = go.transform, Bone = bone, Finger = finger,
                Direction = localDirection, Up = localUp, TipOffset = Mathf.Max(0f, tip.magnitude - 0.008f),
                BoneRotation = AttachmentRotation(bone) });
            if (!finger)
            {
                _wingMesh = mesh;
                _wingRest = mesh.vertices;
                _wingRestNormals = mesh.normals;
                _wingVertices = new Vector3[_wingRest.Length];
                _wingNormals = new Vector3[_wingRestNormals.Length];
                _wingReach = Mathf.Max(mesh.bounds.extents.x - WingRoot.x, 0.01f);
                mesh.MarkDynamic();
            }
        }

        private Quaternion AttachmentRotation(Transform bone)
        {
            int index = Array.IndexOf(_body.bones, bone);
            Matrix4x4[] poses = _bodyMesh.bindposes;
            // Reference the authored rest pose, not the lean/twist at ability activation.
            Quaternion rest = index >= 0 && index < poses.Length
                ? _body.transform.rotation * poses[index].inverse.rotation
                : bone.rotation;
            return Quaternion.Inverse(rest) * _player.transform.rotation;
        }

        internal void OnJump() => _wingJumpPending = true;

        private void UpdateWings(bool visible)
        {
            if (_wingMesh == null) return;
            Vector3 position = _player.transform.position, forward = _player.transform.forward;
            float dt = Time.deltaTime;
            bool airborne = !_player.IsOnGround() && !_player.IsSwimming() && !_player.IsAttached();
            if (!_hasWingMotion || !visible)
            {
                _lastPosition = position;
                _lastForward = forward;
                _hasWingMotion = true;
                _wingSpeed = _wingTurn = _wingLift = 0f;
                _wingJumpTime = 1f;
            }
            if (!visible) { _wingJumpPending = false; return; }
            if (dt <= 0f) return;
            if (_wingJumpPending)
            {
                _wingJumpTime = 0f;
                _wingJumpPending = false;
            }
            Vector3 displacement = position - _lastPosition;
            // Ignore teleport corrections; position sampling also works for remote players.
            Vector3 velocity = displacement.sqrMagnitude < 4f ? displacement / dt : Vector3.zero;
            float blend = 1f - Mathf.Exp(-8f * dt);
            _wingSpeed = Mathf.Lerp(_wingSpeed, Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / 6f), blend);
            _wingTurn = Mathf.Lerp(_wingTurn,
                Mathf.Clamp(Vector3.SignedAngle(_lastForward, forward, Vector3.up) / dt / 180f, -1f, 1f), blend);
            _wingLift = Mathf.Lerp(_wingLift, Mathf.Clamp(velocity.y / 5f, -1f, 1f), blend);
            _lastPosition = position;
            _lastForward = forward;
            _wingJumpTime = Mathf.Min(_wingJumpTime + dt, 1f);
            // A quick upstroke, a pronounced downstroke, then a smooth recovery.
            float jump = Mathf.Clamp01(_wingJumpTime / WingJumpDuration);
            float jumpSwing = jump < 0.2f ? Mathf.SmoothStep(0f, 32f, jump / 0.2f)
                : jump < 0.55f ? Mathf.SmoothStep(32f, -42f, (jump - 0.2f) / 0.35f)
                : Mathf.SmoothStep(-42f, 0f, (jump - 0.55f) / 0.45f);
            float jumpWeight = Mathf.Sin(jump * Mathf.PI);
            _wingPhase = Mathf.Repeat(_wingPhase + dt * (2.8f + _wingSpeed * 4.5f + (airborne ? 2f : 0f)), Mathf.PI * 2f);
            float flap = Mathf.Sin(_wingPhase) * (14f + _wingSpeed * 14f + (airborne ? 14f : 0f));
            float activation = Mathf.Clamp01(_activationTime / ActivationDuration);
            float unfold = Mathf.SmoothStep(0f, 1f, activation / 0.65f);
            float activationFlap = Mathf.Sin(Mathf.Clamp01((activation - 0.3f) / 0.7f) * Mathf.PI * 2f) * 34f;
            for (int i = 0; i < _wingRest.Length; i++)
            {
                Vector3 point = _wingRest[i];
                float side = point.x < 0f ? -1f : 1f;
                Vector3 root = new Vector3(side * WingRoot.x, WingRoot.y, WingRoot.z);
                float reach = Mathf.Clamp01((Mathf.Abs(point.x) - WingRoot.x) / _wingReach);
                // Progressively fold the outer membrane so the span curves behind the back.
                float sweep = (1f - unfold) * 65f + 12f + reach * 20f + _wingSpeed * 18f + side * _wingTurn * 14f - jumpWeight * 16f;
                float rise = 8f + activationFlap + (flap - _wingLift * 10f) * (1f - jumpWeight) + jumpSwing + side * _wingTurn * 9f;
                Quaternion bend = Quaternion.Euler(0f, side * sweep, side * rise);
                _wingVertices[i] = root + bend * (point - root);
                if (i < _wingRestNormals.Length) _wingNormals[i] = bend * _wingRestNormals[i];
            }
            _wingMesh.vertices = _wingVertices;
            // Preserve authored front normals on the double-sided membrane.
            if (_wingNormals.Length == _wingVertices.Length) _wingMesh.normals = _wingNormals;
            _wingMesh.RecalculateBounds();
        }

        private void AddActivationPose(Transform bone, Vector3 angles)
        {
            if (bone != null) _activationPoses.Add(new ActivationPose { Bone = bone, Angles = angles });
        }

        private void RestoreActivationPose()
        {
            // Do not undo a pose the animator has already replaced this frame.
            for (int i = _activationPoses.Count - 1; i >= 0; i--)
            {
                ActivationPose pose = _activationPoses[i];
                if (pose.HasApplied && pose.Bone != null && Quaternion.Angle(pose.Bone.localRotation, pose.Applied) < 0.01f)
                    pose.Bone.localRotation = pose.Original;
                pose.HasApplied = false;
            }
        }

        private void UpdateActivationPose(bool visible)
        {
            RestoreActivationPose();
            if (!visible || _activationTime >= ActivationDuration ||
                _player.GetComponent<Vampirizm.Core.VampireController>()?.BloodRage?.Combat.VisualBusy == true) return;
            // Yield immediately to gameplay animations, including actions begun during transformation.
            if (_player.InAttack() || _player.IsBlocking() || _player.InDodge() || _player.IsStaggering() ||
                _player.InEmote() || _player.IsSwimming() || _player.IsAttached()) return;
            float t = Mathf.Clamp01(_activationTime / ActivationDuration);
            float weight = Mathf.Sin(t * Mathf.PI);
            foreach (ActivationPose pose in _activationPoses)
            {
                if (pose.Bone == null) continue;
                pose.Original = pose.Bone.localRotation;
                Vector3 angles = pose.Angles;
                // Hunch in anticipation, then throw the chest and head back as the wings open.
                if (pose == _activationPoses[0]) angles.x = Mathf.Lerp(24f, -18f, Mathf.SmoothStep(0f, 1f, t));
                Quaternion offset = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(angles), weight);
                Quaternion frame = _player.transform.rotation;
                pose.Bone.rotation = frame * offset * Quaternion.Inverse(frame) * pose.Bone.rotation;
                pose.Applied = pose.Bone.localRotation;
                pose.HasApplied = true;
            }
        }

        private void AddFingerPose(Transform distal)
        {
            // Only the inspected vanilla rig: derive rest rotations from the mesh bind poses,
            // rather than assuming the animator happened to be in an open-hand pose at activation.
            if (_bodyMesh.name != "body" && _bodyMesh.name != "bodyfem") return;
            for (Transform bone = distal; bone != null && bone.name.Contains("Hand") &&
                bone.name != "LeftHand" && bone.name != "RightHand"; bone = bone.parent)
            {
                if (_fingerPoses.Exists(pose => pose.Bone == bone)) continue;
                int index = Array.IndexOf(_body.bones, bone);
                int parent = Array.IndexOf(_body.bones, bone.parent);
                Matrix4x4[] poses = _bodyMesh.bindposes;
                if (index < 0 || parent < 0 || index >= poses.Length || parent >= poses.Length) continue;
                _fingerPoses.Add(new FingerPose { Bone = bone, Rest = (poses[parent] * poses[index].inverse).rotation });
            }
        }

        private void RestoreFingerPose()
        {
            foreach (FingerPose pose in _fingerPoses)
            {
                // Leave a newer animator pose intact. Also handles an animator culled between frames.
                if (pose.HasApplied && pose.Bone != null && Quaternion.Angle(pose.Bone.localRotation, pose.Applied) < 0.01f)
                    pose.Bone.localRotation = pose.Original;
                pose.HasApplied = false;
            }
        }

        private void UpdateFingerPose(bool visible)
        {
            RestoreFingerPose();
            bool clawAttack = _player.GetComponent<Vampirizm.Core.VampireController>()?.BloodRage?.Combat.VisualBusy == true;
            if (!visible || (_player.InAttack() && !clawAttack) || _player.IsBlocking() || _player.InEmote() ||
                _player.IsSwimming() || _player.IsAttached() || _player.InDodge() || _player.IsStaggering() ||
                RightItem?.GetValue(_player) != null || LeftItem?.GetValue(_player) != null) return;
            foreach (FingerPose pose in _fingerPoses)
            {
                if (pose.Bone == null) continue;
                pose.Original = pose.Bone.localRotation;
                pose.Applied = Quaternion.Slerp(pose.Original, pose.Rest, clawAttack ? 1f : 0.8f);
                pose.Bone.localRotation = pose.Applied;
                pose.HasApplied = true;
            }
        }

        private bool AttachBatWings(Transform spine)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Bat") : null;
            if (prefab == null) return false;
            foreach (SkinnedMeshRenderer source in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (source.sharedMesh == null || source.sharedMesh.name != "Bat" ||
                    source.sharedMesh.subMeshCount != 1 || source.sharedMaterials.Length == 0) continue;
                // Imported game meshes have CPU reads disabled. Baking a private static copy
                // works without touching import settings or instantiating a creature/its scripts.
                var original = new Mesh { name = "BloodRageBakedBat" };
                _meshes.Add(original);
                source.BakeMesh(original);
                Vector3[] vertices = original.vertices;
                if (vertices.Length == 0) continue;
                var keep = new bool[vertices.Length];
                float bodyWidth = original.bounds.extents.x * 0.10f;
                for (int i = 0; i < vertices.Length; i++)
                    keep[i] = Mathf.Abs(vertices[i].x - original.bounds.center.x) > bodyWidth;
                var triangles = new List<int>();
                // The body, head and eyes occupy the narrow centre strip. Keep only
                // triangles wholly outside it; the wing roots tuck into the player's back.
                int[] indices = original.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                    if (keep[indices[i]] && keep[indices[i + 1]] && keep[indices[i + 2]])
                    {
                        triangles.Add(indices[i]); triangles.Add(indices[i + 1]); triangles.Add(indices[i + 2]);
                        triangles.Add(indices[i + 2]); triangles.Add(indices[i + 1]); triangles.Add(indices[i]);
                    }
                if (triangles.Count == 0) continue;
                // CaveBat's mesh is in Blender XY with a 100x import scale. Stand its
                // spread wings vertically, with a two-metre span and the roots behind the shoulders.
                float scale = 2f / Mathf.Max(original.bounds.size.x, 0.0001f);
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 point = vertices[i] - original.bounds.center;
                    vertices[i] = new Vector3(point.x * scale, -point.y * scale + WingRoot.y, point.z * scale + WingRoot.z);
                }
                Vector3[] normals = original.normals;
                original.name = "BloodRageBatWings";
                original.vertices = vertices;
                original.triangles = triangles.ToArray();
                original.RecalculateBounds();
                // Keep authored normals: reversed back faces must not cancel out the front normals.
                if (normals.Length == vertices.Length)
                {
                    for (int i = 0; i < normals.Length; i++) normals[i] = new Vector3(normals[i].x, -normals[i].y, normals[i].z);
                    original.normals = normals;
                }
                Object.Destroy(_wing);
                _wing = new Material(source.sharedMaterials[0]) { name = "BloodRageBatWings" };
                Attach("BloodRageWings", spine, original, _wing, false);
                return true;
            }
            return false;
        }

        private void RefreshGlints(VisEquipment equipment)
        {
            if (_glint == null) return;
            var wanted = new HashSet<SkinnedMeshRenderer> { _body };
            foreach (FieldInfo field in ArmorFields)
            {
                object value = field?.GetValue(equipment);
                if (value is GameObject instance) Collect(instance, wanted);
                if (value is List<GameObject> instances)
                    foreach (GameObject item in instances) Collect(item, wanted);
            }
            var stale = new List<SkinnedMeshRenderer>();
            foreach (var pair in _glints)
                if (pair.Key == null || !wanted.Contains(pair.Key) || pair.Value == null || pair.Key.sharedMesh != pair.Value.sharedMesh)
                {
                    if (pair.Value != null) Object.Destroy(pair.Value.gameObject);
                    stale.Add(pair.Key);
                }
            foreach (var renderer in stale) _glints.Remove(renderer);
            foreach (var source in wanted)
            {
                if (source == null || source.sharedMesh == null || _glints.ContainsKey(source)) continue;
                var go = new GameObject("BloodRageGlint") { layer = source.gameObject.layer };
                go.transform.SetParent(source.transform, false);
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = source.sharedMesh;
                copy.bones = source.bones;
                copy.rootBone = source.rootBone;
                copy.quality = source.quality;
                copy.updateWhenOffscreen = source.updateWhenOffscreen;
                copy.localBounds = source.localBounds;
                copy.shadowCastingMode = ShadowCastingMode.Off;
                copy.receiveShadows = false;
                var materials = new Material[source.sharedMesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = _glint;
                copy.sharedMaterials = materials;
                _glints.Add(source, copy);
            }
        }

        private static void Collect(GameObject instance, HashSet<SkinnedMeshRenderer> result)
        {
            if (instance == null) return;
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name != "BloodRageGlint") result.Add(renderer);
        }

        private Mesh ClawMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // Longer talons with a gradual hooked taper, seated just behind the fingertip.
            for (int ring = 0; ring < 6; ring++)
            for (int side = 0; side < 6; side++)
            {
                float t = ring / 5f, angle = side * Mathf.PI / 3f;
                float radius = 0.008f * Mathf.Pow(1f - t, 1.25f) + 0.00015f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius - t * t * 0.045f,
                    t * 0.15f));
            }
            for (int ring = 0; ring < 5; ring++)
            for (int side = 0; side < 6; side++)
            {
                int a = ring * 6 + side, b = ring * 6 + (side + 1) % 6;
                triangles.AddRange(new[] { a, b, a + 6, b, b + 6, a + 6 });
            }
            return Mesh("BloodRageClaw", vertices, triangles);
        }

        private Mesh WingsMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (float side in new[] { -1f, 1f })
            {
                // Scalloped bat membrane; animated by the same deformation as the baked wings.
                Vector3[] outline = {
                    new Vector3(0.08f, 0.1f, -0.15f), new Vector3(0.35f, 0.52f, -0.24f),
                    new Vector3(0.9f, 0.75f, -0.30f), new Vector3(1.4f, 0.45f, -0.35f),
                    new Vector3(1.02f, 0.32f, -0.28f), new Vector3(0.90f, -0.14f, -0.25f),
                    new Vector3(0.62f, 0.03f, -0.22f), new Vector3(0.48f, -0.47f, -0.20f),
                    new Vector3(0.26f, -0.22f, -0.17f), new Vector3(0.08f, -0.55f, -0.15f)
                };
                int start = vertices.Count;
                vertices.Add(new Vector3(side * 0.245f, 0.055f + WingRoot.y, -0.007f + WingRoot.z));
                foreach (Vector3 point in outline) vertices.Add(new Vector3(side * point.x * 0.7f,
                    point.y * 0.7f - 0.05f + WingRoot.y, (point.z + 0.22f) * 0.7f + WingRoot.z));
                for (int i = 0; i < outline.Length; i++)
                {
                    int a = start, b = start + 1 + i, c = start + 1 + (i + 1) % outline.Length;
                    triangles.AddRange(new[] { a, b, c, a, c, b });
                }
            }
            return Mesh("BloodRageWings", vertices, triangles);
        }

        private Mesh Mesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray(), uv = new Vector2[vertices.Count] };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            return mesh;
        }

        internal void Clear() => Clear(true);

        private void Clear(bool resetActivation)
        {
            RestoreActivationPose();
            _activationPoses.Clear();
            if (resetActivation)
            {
                _wasActive = false;
                _activationTime = ActivationDuration;
            }
            RestoreFingerPose();
            _fingerPoses.Clear();
            foreach (var go in _objects) if (go != null) { go.SetActive(false); Object.Destroy(go); }
            foreach (var pair in _glints) if (pair.Value != null) { pair.Value.gameObject.SetActive(false); Object.Destroy(pair.Value.gameObject); }
            foreach (var mesh in _meshes) if (mesh != null) Object.Destroy(mesh);
            if (_ivory != null) Object.Destroy(_ivory);
            if (_wing != null) Object.Destroy(_wing);
            if (_glint != null) Object.Destroy(_glint);
            if (_glintTexture != null) Object.Destroy(_glintTexture);
            _glintTexture = null;
            if (_holdsTemplate) _glintTemplate.Release();
            _holdsTemplate = false;
            _objects.Clear(); _meshes.Clear(); _attachments.Clear(); _glints.Clear();
            _body = null; _bodyMesh = null; _ivory = _wing = _glint = null; _refreshAt = 0f;
            _wingMesh = null;
            _wingRest = _wingVertices = _wingRestNormals = _wingNormals = null;
            _hasWingMotion = false;
            _wingJumpPending = false;
            _wingJumpTime = 1f;
            _wingPhase = _wingSpeed = _wingTurn = _wingLift = 0f;
        }
    }
}
