using HarmonyLib;
using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Feeding;
using Vampirizm.Networking;
using Vampirizm.Skills;

namespace Vampirizm.Abilities
{
    // Vampire-side bite: hold the secondary attack with empty hands. Blood and experience arrive
    // only through confirmed ticks from the victim owner; every interruption ends the session.
    internal sealed class DrainAbility
    {
        private const float RangeTolerance = 0.75f;
        private const float SignificantHitFraction = 0.1f;
        private static readonly int SightMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "blocker");
        private static readonly AccessTools.FieldRef<Character, Vector3> LookDir = AccessTools.FieldRefAccess<Character, Vector3>("m_lookDir");
        private static readonly System.Action<Player> StopEmote =
            AccessTools.MethodDelegate<System.Action<Player>>(AccessTools.Method(typeof(Player), "StopEmote"));

        private readonly Player _player;
        private readonly VampireController _controller;
        private readonly DrainTracker _tracker = new DrainTracker();
        private Character _target;
        private bool _active;
        private bool _held;
        private bool _emote;
        private float _started;
        private float _nextTick;
        private float _sentAt;
        private float _cooldownUntil;
        private float _lastHealth;

        internal DrainAbility(Player player, VampireController controller)
        {
            _player = player;
            _controller = controller;
        }

        internal bool Active => _active;

        // From Player.SetControls: pressed starts a bite, releasing the button ends it.
        internal void Input(bool pressed, bool held)
        {
            _held = held;
            if (pressed && !_active) TryStart();
        }

        private void TryStart()
        {
            var settings = Plugin.Instance.Settings;
            if (Time.time < _cooldownUntil)
            {
                _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_bite_cooldown");
                return;
            }
            if (!_controller.CanChange || _player.InAttack() || _player.IsStaggering() || _player.IsTeleporting()) return;
            Character target = FindTarget(settings.BiteRange.Value);
            if (target == null)
            {
                _player.Message(MessageHud.MessageType.Center, "$vampirizm_bite_no_target");
                return;
            }
            if (target is Player human)
            {
                DrainNetwork.RequestBite(human, _player.GetZDOID(), _player.GetPlayerName());
                PlayAnimation();
                _cooldownUntil = Time.time + settings.BiteCooldown.Value;
                return;
            }
            if (!DonorRegistry.CanFeed(target))
            {
                _player.Message(MessageHud.MessageType.Center, "$vampirizm_bite_forbidden");
                return;
            }
            if (BloodRules.IsFull(_controller.State.Blood, _controller.Capacity))
            {
                _player.Message(MessageHud.MessageType.Center, "$vampirizm_blood_full");
                return;
            }
            _target = target;
            _active = true;
            _tracker.Start(Random.Range(1, int.MaxValue));
            _started = Time.time;
            _nextTick = Time.time;
            _lastHealth = _player.GetHealth();
            string emote = settings.BiteEmote.Value.Trim();
            _emote = emote.Length > 0 && _player.StartEmote(emote, false);
            if (!_emote) PlayAnimation();
        }

        // Runs every frame on the owning client.
        internal void Update()
        {
            if (!_active) return;
            string reason = InterruptReason();
            if (reason != null)
            {
                End(reason);
                return;
            }
            FaceTarget();
            float interval = Plugin.Instance.Settings.BiteInterval.Value;
            if (_tracker.Awaiting)
            {
                if (Time.time - _sentAt > DrainNetwork.Timeout(interval)) End("$vampirizm_bite_lost");
                return;
            }
            if (Time.time < _nextTick) return;
            _sentAt = Time.time;
            DrainNetwork.RequestDrain(_target, _player.GetZDOID(), _tracker.Session, _tracker.Next());
        }

        internal void OnResult(int session, int tick, DrainStatus status, float blood, bool xpAllowed, bool died)
        {
            // Duplicate, stale and foreign confirmations are ignored.
            if (!_active || !_tracker.Confirm(session, tick)) return;
            if (status != DrainStatus.Ok)
            {
                End(StatusMessage(status));
                return;
            }
            var settings = Plugin.Instance.Settings;
            float accepted = _controller.AddBlood(blood);
            float xp = DrainRules.Experience(accepted, settings.XpPerBlood.Value, xpAllowed);
            if (xp > 0f) _player.RaiseSkill(VampirismSkill.Type, xp);
            if (!_emote) PlayAnimation();
            _nextTick = Time.time + settings.BiteInterval.Value;
            if (died) End("$vampirizm_bite_victim_died");
            else if (BloodRules.IsFull(_controller.State.Blood, _controller.Capacity)) End("$vampirizm_blood_full");
        }

        // Idempotent: safe from death, teleport, cure and component destruction. Empty message ends silently.
        internal void End(string message = null)
        {
            if (!_active) return;
            _active = false;
            _target = null;
            if (Plugin.Instance != null) _cooldownUntil = Time.time + Plugin.Instance.Settings.BiteCooldown.Value;
            if (_emote && _player != null) StopEmote(_player);
            _emote = false;
            if (!string.IsNullOrEmpty(message) && _player != null) _player.Message(MessageHud.MessageType.Center, message);
        }

        private string InterruptReason()
        {
            var settings = Plugin.Instance.Settings;
            if (!_held) return "";
            if (!_controller.IsActiveVampire || !_controller.CanChange || _player.IsTeleporting()) return "";
            if (_player.IsStaggering()) return "$vampirizm_bite_interrupted";
            float health = _player.GetHealth();
            bool significantHit = _lastHealth - health > _player.GetMaxHealth() * SignificantHitFraction;
            _lastHealth = health;
            if (significantHit) return "$vampirizm_bite_interrupted";
            if (_target == null || _target.IsDead() || !_target.GetComponent<ZNetView>().IsValid()) return "";
            float range = settings.BiteRange.Value + RangeTolerance;
            if ((_target.transform.position - _player.transform.position).sqrMagnitude > range * range ||
                !InSight(_target)) return "$vampirizm_bite_escaped";
            if (Time.time - _started > settings.BiteMaxDuration.Value) return "";
            return null;
        }

        private Character FindTarget(float range)
        {
            Character hovered = _player.GetHoverCreature();
            if (IsCandidate(hovered, range)) return hovered;
            Character best = null;
            float bestDistance = float.MaxValue;
            Vector3 forward = _player.transform.forward;
            foreach (Character character in Character.GetAllCharacters())
            {
                if (!IsCandidate(character, range)) continue;
                Vector3 offset = character.transform.position - _player.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > 0.01f && Vector3.Dot(forward, offset.normalized) < 0.5f) continue;
                if (offset.sqrMagnitude < bestDistance)
                {
                    bestDistance = offset.sqrMagnitude;
                    best = character;
                }
            }
            return best;
        }

        private bool IsCandidate(Character character, float range)
        {
            if (character == null || character == _player || character.IsDead()) return false;
            var view = character.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return false;
            return (character.transform.position - _player.transform.position).sqrMagnitude <= range * range && InSight(character);
        }

        private bool InSight(Character character) => !Physics.Linecast(_player.GetEyePoint(), character.GetCenterPoint(), SightMask);

        private void FaceTarget()
        {
            Vector3 direction = _target.transform.position - _player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            LookDir(_player) = direction.normalized;
            _player.transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        private void PlayAnimation() => _player.GetZAnim().SetTrigger("interact");

        private static string StatusMessage(DrainStatus status)
        {
            switch (status)
            {
                case DrainStatus.Busy: return "$vampirizm_bite_busy";
                case DrainStatus.NotDonor: return "$vampirizm_bite_forbidden";
                case DrainStatus.TooFar: return "$vampirizm_bite_escaped";
                case DrainStatus.Dead: return "$vampirizm_bite_victim_died";
                case DrainStatus.Expired: return null;
                default: return "$vampirizm_bite_lost";
            }
        }
    }
}
