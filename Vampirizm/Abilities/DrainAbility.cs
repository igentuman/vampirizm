using HarmonyLib;
using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Feeding;
using Vampirizm.Networking;
using Vampirizm.Skills;

namespace Vampirizm.Abilities
{
    // Vampire-side bite: one press of the secondary attack with empty hands plays the whole drinking action,
    // which always lasts the session length. The first tick only claims the victim; damage, blood and experience
    // arrive through confirmed ticks at the end of each interval, the last one at the session end.
    // When draining stops early (full bar, expired, lost) the action still plays out.
    // Only real interruptions (stagger, big hit, escape, death, teleport) end it before time.
    internal sealed class DrainAbility
    {
        private const float RangeTolerance = 0.75f;
        private const float SignificantHitFraction = 0.1f;
        private static readonly int SightMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "blocker");
        private static readonly AccessTools.FieldRef<Character, Vector3> LookDir = AccessTools.FieldRefAccess<Character, Vector3>("m_lookDir");
        private static readonly System.Action<Player> StopEmote =
            AccessTools.MethodDelegate<System.Action<Player>>(AccessTools.Method(typeof(Player), "StopEmote"));
        private static readonly AccessTools.FieldRef<Character, Animator> AnimatorRef = AccessTools.FieldRefAccess<Character, Animator>("m_animator");

        private readonly Player _player;
        private readonly ZNetView _view;
        private readonly VampireController _controller;
        private readonly DrainTracker _tracker = new DrainTracker();
        private Character _target;
        private bool _active;
        private bool _emote;
        private float _emoteSpeed;
        private bool _draining;
        private float _started;
        private float _nextTick;
        private float _sentAt;
        private float _cooldownUntil;
        private float _lastHealth;

        internal DrainAbility(Player player, VampireController controller)
        {
            _player = player;
            _view = player.GetComponent<ZNetView>();
            _controller = controller;
        }

        internal bool Active => _active;

        // From Player.SetControls: a press starts a bite that runs to the end of the session.
        // Releasing the button must not end it, or the drinking emote is stopped before it plays.
        internal void Input(bool pressed)
        {
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
            _draining = true;
            _emoteSpeed = 0f;
            _tracker.Start(Random.Range(1, int.MaxValue));
            _started = Time.time;
            _nextTick = Time.time;
            _lastHealth = _player.GetHealth();
            VampireNetwork.SetDrinkTarget(_view, target.GetZDOID());
            // A bat has no visible body to emote with, and emoting would end the bat form.
            string emote = settings.BiteEmote.Value.Trim().ToLowerInvariant();
            _emote = emote.Length > 0 && !_controller.BatForm.Active && _player.StartEmote(emote, IsOneShot(emote));
            if (!_emote) PlayAnimation();
        }

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
            UpdateEmoteSpeed();
            var settings = Plugin.Instance.Settings;
            if (!_draining)
            {
                if (Time.time - _started > settings.BiteMaxDuration.Value) End("");
                return;
            }
            float interval = settings.BiteInterval.Value;
            // The final tick is due at the session end, so the session waits for its confirmation.
            if (_tracker.Awaiting)
            {
                if (Time.time - _sentAt > DrainNetwork.Timeout(interval)) StopDraining("$vampirizm_bite_lost");
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
            if (status == DrainStatus.Expired || status == DrainStatus.Stale)
            {
                StopDraining(null);
                return;
            }
            if (status != DrainStatus.Ok)
            {
                End(StatusMessage(status));
                return;
            }
            var settings = Plugin.Instance.Settings;
            float accepted = _controller.AddBlood(blood);
            float xp = DrainRules.Experience(accepted, settings.XpPerBlood.Value, xpAllowed);
            if (xp > 0f) _player.RaiseSkill(VampirismSkill.Type, xp);
            if (!_emote && tick > DrainRules.ClaimTick) PlayAnimation();
            float duration = settings.BiteMaxDuration.Value;
            float interval = settings.BiteInterval.Value;
            if (died) End("$vampirizm_bite_victim_died");
            else if (BloodRules.IsFull(_controller.State.Blood, _controller.Capacity)) StopDraining("$vampirizm_blood_full");
            else if (tick >= DrainRules.SessionTicks(duration, interval)) StopDraining(null);
            else _nextTick = _started + DrainRules.TickOffset(tick + 1, duration, interval);
        }

        // No more ticks are requested; the animation, hold and effects continue until the session length.
        private void StopDraining(string message)
        {
            _draining = false;
            if (!string.IsNullOrEmpty(message)) _player.Message(MessageHud.MessageType.Center, message);
        }

        // Animator speed replicates through ZSyncAnimation; vanilla resets it to 1 once the emote is over.
        // With the automatic speed, the emote clip is stretched once its state starts so it ends with the session.
        private void UpdateEmoteSpeed()
        {
            if (!_emote || !_player.InEmote()) return;
            var settings = Plugin.Instance.Settings;
            if (_emoteSpeed <= 0f)
            {
                _emoteSpeed = settings.BiteEmoteSpeed.Value;
                Animator animator = AnimatorRef(_player);
                float remaining = settings.BiteMaxDuration.Value - (Time.time - _started);
                if (_emoteSpeed <= 0f && animator != null && remaining > 0.1f)
                {
                    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                    float left = state.length * (1f - Mathf.Repeat(state.normalizedTime, 1f));
                    _emoteSpeed = left > 0f ? Mathf.Clamp(left / remaining, 0.05f, 2f) : 1f;
                }
                if (_emoteSpeed <= 0f) _emoteSpeed = 1f;
            }
            _player.GetZAnim().SetSpeed(_emoteSpeed);
        }

        // Idempotent: safe from death, teleport, cure and component destruction. Empty message ends silently.
        internal void End(string message = null)
        {
            if (!_active) return;
            _active = false;
            DrainNetwork.RequestRelease(_target, _tracker.Session);
            _target = null;
            if (Plugin.Instance != null) _cooldownUntil = Time.time + Plugin.Instance.Settings.BiteCooldown.Value;
            VampireNetwork.SetDrinkTarget(_view, ZDOID.None);
            if (_emote && _view != null && _view.IsValid())
            {
                StopEmote(_player);
                _player.GetZAnim().SetSpeed(1f);
            }
            _emote = false;
            if (!string.IsNullOrEmpty(message) && _player != null) _player.Message(MessageHud.MessageType.Center, message);
        }

        private string InterruptReason()
        {
            var settings = Plugin.Instance.Settings;
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

        // Faces the victim's center, which a lifted victim keeps on the vampire's forward line.
        private void FaceTarget()
        {
            Vector3 direction = _target.GetCenterPoint() - _player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            LookDir(_player) = direction.normalized;
            _player.transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        private void PlayAnimation() => _player.GetZAnim().SetTrigger("interact");

        // Vanilla loops only emotes marked as such (sit); the rest exist in the animator as one-shot triggers.
        private static bool IsOneShot(string emote)
            => !System.Enum.TryParse(emote, true, out Emotes parsed) || (parsed.GetAttributeOfType<Emote>()?.OneShot ?? true);

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
