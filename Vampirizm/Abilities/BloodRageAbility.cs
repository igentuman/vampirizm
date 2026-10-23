using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Effects;
using Vampirizm.Networking;
using Vampirizm.Visuals;

namespace Vampirizm.Abilities
{
    // Replicated mode state; the visual owns the cosmetic transformation sequence.
    internal sealed class BloodRageAbility
    {
        private readonly Player _player;
        private readonly VampireController _controller;
        private readonly BloodRageVisual _visual;
        private readonly BloodRageRules _clock = new BloodRageRules();
        private bool _remoteActive;
        internal BloodRageCombat Combat { get; }
        internal bool Active => _player == Player.m_localPlayer ? _clock.Active : _remoteActive;
        internal bool OnCooldown => Time.time < _clock.ReadyAt;

        internal BloodRageAbility(Player player, VampireController controller)
        {
            _player = player;
            _controller = controller;
            _visual = new BloodRageVisual(player);
            Combat = new BloodRageCombat(player, controller);
        }

        internal void Toggle()
        {
            // Repeated ability input must not cancel or restart the timed rage.
            if (Active) return;
            var settings = Plugin.Instance.Settings;
            bool busy = !_controller.CanChange || _controller.BatForm.Active || _controller.Dash.Active ||
                _controller.Drain.Active || _player.InAttack() || _player.InDodge() || _player.IsStaggering() ||
                _player.IsAttached() || _player.IsSwimming() || _player.IsTeleporting() || _player.InEmote() || _player.InPlaceMode();
            AbilityBlock block = AbilityRules.Check(_controller.IsActiveVampire, _controller.Level,
                settings.BloodRageLevel.Value, busy, Time.time, _clock.ReadyAt, _controller.State.Blood, BloodRageEffect.Cost);
            if (block != AbilityBlock.None)
            {
                string message = block == AbilityBlock.Locked
                    ? global::Localization.instance.Localize("$vampirizm_ability_locked",
                        global::Localization.instance.Localize("$vampirizm_action_bloodrage"), Mathf.CeilToInt(settings.BloodRageLevel.Value).ToString())
                    : block == AbilityBlock.NoBlood ? "$vampirizm_ability_no_blood"
                    : block == AbilityBlock.Cooldown ? "$vampirizm_bloodrage_cooldown" : "$vampirizm_bloodrage_busy";
                _player.Message(MessageHud.MessageType.TopLeft, message);
                return;
            }
            if (_player.m_unarmedWeapon == null) return;
            // Establish the effect before charging: a failed status application is free.
            if (!BloodRageEffect.Apply(_player)) return;
            if (!_controller.SpendBlood(BloodRageEffect.Cost)) { BloodRageEffect.Remove(_player); return; }
            _controller.Invisibility.End();
            _controller.Orb.End(true);
            _clock.Begin();
            Combat.Begin();
            VampireNetwork.SetBloodRage(_player.GetComponent<ZNetView>(), true);
            BloodRageNetwork.PublishSession(_player, Combat.Session);
        }

        internal void UpdateLocal()
        {
            if (Active && (!_controller.IsActiveVampire || !_controller.CanChange || _player.IsDead() ||
                _controller.BatForm.Active || _player.IsTeleporting() || !BloodRageEffect.IsActive(_player))) End();
            if (Active) Combat.Update();
        }

        internal void UpdateRemote()
        {
            bool active = !_player.IsDead() && VampireNetwork.IsBloodRage(_player);
            if (_remoteActive && !active) Combat.Cancel();
            _remoteActive = active;
        }
        internal void UpdateVisual()
        {
            // Remove the previous additive attack pose before the transformation/finger layers run.
            Combat.RestorePose();
            _visual.Update(Active && !_player.IsDead(), Combat.Animate);
        }

        internal void OnJump()
        {
            if (Active) _visual.OnJump();
        }

        internal void End()
        {
            bool wasActive = Active;
            if (_player == Player.m_localPlayer) _clock.End(Time.time);
            _remoteActive = false;
            BloodRageEffect.Remove(_player);
            VampireNetwork.SetBloodRage(_player.GetComponent<ZNetView>(), false);
            if (wasActive && _player == Player.m_localPlayer) BloodRageNetwork.PublishSession(_player, 0);
            Combat.End();
            _visual.Clear();
        }
    }
}
