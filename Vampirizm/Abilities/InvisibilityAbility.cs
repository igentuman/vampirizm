using HarmonyLib;
using UnityEngine;
using Vampirizm.Core;
using Vampirizm.Networking;

namespace Vampirizm.Abilities
{
    // The owner controls the ability and publishes it through ZDO for other peers.
    internal sealed class InvisibilityAbility
    {
        private static readonly System.Action<Character, bool> SetCharacterVisible =
            AccessTools.MethodDelegate<System.Action<Character, bool>>(AccessTools.Method(typeof(Character), "SetVisible"));
        private readonly Player _player;
        private readonly VampireController _controller;
        private readonly InvisibilityVisual _visual;
        private float _drainTimer;
        private float _syncTimer;
        private bool _active;

        internal InvisibilityAbility(Player player, VampireController controller)
        {
            _player = player;
            _controller = controller;
            _visual = new InvisibilityVisual(player);
        }

        internal bool Active => _active;

        internal void BeforeEquipmentUpdate() => _visual.BeforeEquipmentUpdate();

        internal void UpdateVisual()
        {
            _visual.RecoverFrame();
        }

        internal void Toggle()
        {
            var settings = Plugin.Instance.Settings;
            if (_active)
            {
                End();
                _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_invisibility_off");
                return;
            }

            AbilityBlock block = AbilityRules.Check(_controller.IsActiveVampire, _controller.Level,
                settings.InvisibilityLevel.Value, IsBusy(), Time.time, 0f, _controller.State.Blood, 0f);
            if (block != AbilityBlock.None)
            {
                _player.Message(MessageHud.MessageType.TopLeft, Message(block, settings.InvisibilityLevel.Value));
                return;
            }

            _active = true;
            _drainTimer = 0f;
            _visual.SetActive(true);
            VampireNetwork.SetInvisible(_player.GetComponent<ZNetView>(), true);
            _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_invisibility_on");
        }

        internal void UpdateLocal(float dt)
        {
            _visual.SetActive(_active);

            if (!_active) return;
            if (!_controller.IsActiveVampire || !_controller.CanChange || _player.IsDead() || IsBusy() ||
                _controller.State.Blood <= 0f)
            {
                End();
                return;
            }

            float costPerMinute = Plugin.Instance.Settings.InvisibilityDrainPerMinute.Value;
            if (costPerMinute <= 0f) return;
            _drainTimer += dt;
            if (_drainTimer < 1f) return;
            float elapsed = _drainTimer;
            _drainTimer = 0f;
            if (!_controller.SpendBlood(costPerMinute * elapsed / 60f) || _controller.State.Blood <= 0f)
                End();
        }

        internal void UpdateRemote()
        {
            _visual.SetActive(false);
            _syncTimer -= Time.unscaledDeltaTime;
            if (_syncTimer > 0f) return;
            _syncTimer = 0.2f;
            bool invisible = VampireNetwork.IsInvisible(_player);
            if (_active == invisible) return;
            _active = invisible;
            if (_player != Player.m_localPlayer) SetCharacterVisible(_player, !invisible);
        }

        internal void End()
        {
            _active = false;
            _drainTimer = 0f;
            _visual.SetActive(false);
            if (_player == null) return;
            VampireNetwork.SetInvisible(_player.GetComponent<ZNetView>(), false);
            if (_player != Player.m_localPlayer) SetCharacterVisible(_player, true);
        }

        private bool IsBusy()
            => _controller.Drain.Active || _controller.Dash.Active || _controller.BatForm.Active || _player.InAttack() || _player.InDodge() ||
               _player.IsStaggering() || _player.IsAttached() || _player.IsTeleporting() || _player.InEmote();

        private static string Message(AbilityBlock block, float level)
        {
            if (block == AbilityBlock.Locked)
                return global::Localization.instance.Localize("$vampirizm_ability_locked",
                    global::Localization.instance.Localize("$vampirizm_action_invisibility"), Mathf.CeilToInt(level).ToString());
            return "$vampirizm_unavailable";
        }
    }
}
