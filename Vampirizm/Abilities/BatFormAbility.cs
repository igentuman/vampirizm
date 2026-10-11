using HarmonyLib;
using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Networking;

namespace Vampirizm.Abilities
{
    // Keeps the Player/ZDO and its collider intact. Only the rendered model changes; flight
    // velocity is applied to the normal rigidbody so world collision and player networking remain active.
    internal sealed class BatFormAbility
    {
        private static readonly AccessTools.FieldRef<Character, Rigidbody> Body =
            AccessTools.FieldRefAccess<Character, Rigidbody>("m_body");
        private readonly Player _player;
        private readonly VampireController _controller;
        private GameObject _humanVisual;
        private GameObject _batVisual;
        private bool _active;
        private bool _oldGravity;
        private bool _rise;
        private bool _descend;
        private bool _inputAllowed = true;
        private Vector3 _move;
        private float _drainTimer;

        internal BatFormAbility(Player player, VampireController controller)
        {
            _player = player;
            _controller = controller;
        }

        internal bool Active => _active;

        internal void Toggle()
        {
            if (_active)
            {
                End(true);
                return;
            }

            var settings = Plugin.Instance.Settings;
            AbilityBlock block = AbilityRules.Check(_controller.IsActiveVampire, _controller.Level,
                settings.BatFormLevel.Value, IsBusy(), Time.time, 0f, _controller.State.Blood, 0f);
            if (block != AbilityBlock.None)
            {
                string message = block == AbilityBlock.Locked
                    ? global::Localization.instance.Localize("$vampirizm_ability_locked",
                        global::Localization.instance.Localize("$vampirizm_action_batform"), Mathf.CeilToInt(settings.BatFormLevel.Value).ToString())
                    : block == AbilityBlock.NoBlood ? "$vampirizm_ability_no_blood" : "$vampirizm_bat_busy";
                _player.Message(MessageHud.MessageType.TopLeft, message);
                return;
            }

            _controller.Invisibility.End();
            _active = true;
            _drainTimer = 0f;
            Rigidbody body = Body(_player);
            _oldGravity = body.useGravity;
            body.useGravity = false;
            _humanVisual = _player.GetVisual();
            _batVisual = CreateBatVisual();
            if (_humanVisual != null && _batVisual != null) _humanVisual.SetActive(false);
            if (_batVisual == null) Plugin.Warn("Bat prefab/model is unavailable; flight will retain the player model.");
            VampireNetwork.SetBatForm(_player.GetComponent<ZNetView>(), true);
            BatTransformationEffect.Play(_player.transform.position);
            _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_batform_on");
        }

        // Set from Player.SetControls. movedir is raw keyboard/stick input, not a world direction.
        internal void SetInputAllowed(bool allowed) => _inputAllowed = allowed;

        internal void SetControls(Vector3 movedir, bool jump, bool crouch)
        {
            if (!_active) return;
            _move = _inputAllowed ? movedir : Vector3.zero;
            _move.y = 0f;
            // PlayerController passes jump/crouch as key-down events. Poll held state so
            // flight continues for the whole time the key is held.
            _rise = _inputAllowed && (ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump"));
            _descend = _inputAllowed && (ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch"));
        }

        internal void Update(float dt)
        {
            if (!_active)
            {
                UpdateRemoteVisual();
                return;
            }
            if (!_controller.IsActiveVampire || !_controller.CanChange || _player.IsDead() ||
                _player.IsTeleporting() || _player.IsStaggering() || _player.IsSwimming())
            {
                End(false);
                return;
            }

            float rate = Plugin.Instance.Settings.BatFormDrainPerMinute.Value;
            if (rate > 0f)
            {
                _drainTimer += dt;
                if (_drainTimer >= 1f)
                {
                    float elapsed = _drainTimer;
                    _drainTimer = 0f;
                    if (!_controller.SpendBlood(rate * elapsed / 60f) || _controller.State.Blood <= 0f)
                    {
                        End(true);
                        return;
                    }
                }
            }
            else if (_controller.State.Blood <= 0f)
            {
                End(true);
            }
        }

        // Called immediately after vanilla walking has written its velocity.
        internal void FixedStep()
        {
            if (!_active) return;
            float speed = Plugin.Instance.Settings.BatFormSpeed.Value;
            Vector3 forward = GameCamera.instance != null ? GameCamera.instance.transform.forward : _player.GetLookDir();
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = _player.transform.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 horizontal = (forward * _move.z + right * _move.x) * speed;
            float vertical = (_rise ? 1f : 0f) - (_descend ? 1f : 0f);
            Rigidbody body = Body(_player);
            body.linearVelocity = new Vector3(horizontal.x, vertical * speed, horizontal.z);
            body.angularVelocity = Vector3.zero;
        }

        internal void End(bool effect)
        {
            if (!_active)
            {
                UpdateRemoteVisual();
                return;
            }
            _active = false;
            _move = Vector3.zero;
            _rise = _descend = false;
            _drainTimer = 0f;
            if (_player != null)
            {
                Rigidbody body = Body(_player);
                if (body != null)
                {
                    body.useGravity = _oldGravity;
                    body.linearVelocity = new Vector3(0f, Mathf.Min(0f, body.linearVelocity.y), 0f);
                }
                VampireNetwork.SetBatForm(_player.GetComponent<ZNetView>(), false);
                if (_humanVisual != null) _humanVisual.SetActive(true);
                if (_batVisual != null) Object.Destroy(_batVisual);
                if (effect) BatTransformationEffect.Play(_player.transform.position);
                if (effect) _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_batform_off");
            }
            _batVisual = null;
        }

        private bool IsBusy()
            => !_controller.CanChange || _controller.Drain.Active || _controller.Dash.Active || _active ||
               _player.InAttack() || _player.InDodge() || _player.IsStaggering() || _player.IsAttached() ||
               _player.IsSwimming() || _player.IsTeleporting() || _player.InEmote() || _player.IsKnockedBack();

        private GameObject CreateBatVisual()
        {
            if (ZNetScene.instance == null) return null;
            GameObject prefab = ZNetScene.instance.GetPrefab("Bat");
            GameObject source = FindVisual(prefab);
            // Some modpacks rename the creature prefab; search the loaded creature list
            // by its display prefab name before giving up.
            if (source == null)
            {
                foreach (GameObject candidate in ZNetScene.instance.m_prefabs)
                {
                    if (candidate == null || candidate == prefab ||
                        candidate.name.IndexOf("bat", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    source = FindVisual(candidate);
                    if (source != null)
                    {
                        Plugin.Warn($"Bat form: using creature prefab '{candidate.name}' for the bat visual.");
                        break;
                    }
                }
            }
            if (source == null)
            {
                string prefabName = prefab != null ? prefab.name : "<missing>";
                Plugin.Warn($"Bat form: no Visual model found (Bat prefab: {prefabName}).");
                return null;
            }
            GameObject visual = Object.Instantiate(source, _player.transform);
            visual.name = "VampirizmBatVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(visual.transform, _player.gameObject.layer);
            visual.SetActive(true);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody rigidbody in visual.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(rigidbody);
            foreach (Animator animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = true;
            return visual;
        }

        private static GameObject FindVisual(GameObject prefab)
        {
            if (prefab == null) return null;
            Character character = prefab.GetComponent<Character>();
            GameObject visual = character != null ? character.GetVisual() : null;
            if (visual != null) return visual;
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (child != prefab.transform && child.name == "Visual") return child.gameObject;
            }
            return null;
        }

        private static void SetLayerRecursively(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform child in transform) SetLayerRecursively(child, layer);
        }

        private void UpdateRemoteVisual()
        {
            if (_player == null || _player == Player.m_localPlayer) return;
            bool bat = VampireNetwork.IsBatForm(_player);
            if (bat == (_batVisual != null)) return;
            if (bat)
            {
                _humanVisual = _player.GetVisual();
                _batVisual = CreateBatVisual();
                if (_humanVisual != null && _batVisual != null) _humanVisual.SetActive(false);
            }
            else
            {
                if (_humanVisual != null) _humanVisual.SetActive(true);
                if (_batVisual != null) Object.Destroy(_batVisual);
                _batVisual = null;
            }
        }
    }

    internal static class BatTransformationEffect
    {
        // A self-contained blood-red and violet burst: no external asset names or extra DLL resources required.
        internal static void Play(Vector3 position)
        {
            var root = new GameObject("VampirizmBatTransformation");
            root.transform.position = position + Vector3.up * 0.8f;
            var particles = root.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.04f, 0.12f), new Color(0.42f, 0.08f, 0.75f));
            main.gravityModifier = -0.35f;
            main.maxParticles = 128;
            var emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 96) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Color(1f, 0.12f, 0.18f, 1f), new Color(0.3f, 0.05f, 0.6f, 0f));
            particles.Play();
            Object.Destroy(root, 2f);
        }
    }
}
