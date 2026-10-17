using UnityEngine;
using Vampirizm.Core;

namespace Vampirizm.Abilities.Orb
{
    // Orb casts: the vanilla use/interact animation instead of a vanilla Attack (which needs weapon animation events).
    // Primary summons the attack swarm, secondary the shield. Eitr, durability and skill are handled here.
    internal sealed class OrbCaster
    {
        private const float CastLock = 0.8f;
        private const float MessageInterval = 1f;
        private readonly Player _player;
        private readonly VampireController _controller;
        private Animator _animator;
        private float _lockedUntil;
        private float _nextMessage;
        private bool _pending;
        private float _releaseAt;
        private bool _secondary;
        private int _quality;

        internal OrbCaster(Player player, VampireController controller)
        {
            _player = player;
            _controller = controller;
            Swarm = new BatSwarm(player, controller);
        }

        internal BatSwarm Swarm { get; }

        // Called every fixed step while attack input is held or queued, so failures are reported at most once a second.
        internal bool TryCast(ItemDrop.ItemData orb, bool secondary)
        {
            if (Time.time < _lockedUntil || _pending) return false;
            var settings = Plugin.Instance.Settings;
            float cost = OrbRules.EitrCost(settings.OrbBaseEitr.Value, _player.GetSkillFactor(global::Skills.SkillType.BloodMagic),
                settings.OrbSkillEitrReduction.Value);
            OrbBlock block = OrbRules.CheckCast(_controller.IsActiveVampire, IsBusy(), Swarm.Mode, secondary, orb.m_durability,
                orb.m_shared.m_useDurability, _player.GetEitr(), _player.GetMaxEitr(), cost);
            if (block != OrbBlock.None)
            {
                Report(block);
                return false;
            }

            if (cost > 0f) _player.UseEitr(cost);
            if (orb.m_shared.m_useDurability)
                orb.m_durability = Mathf.Max(0f, orb.m_durability - orb.m_shared.m_useDurabilityDrain * Game.m_durabilityRate);
            _player.RaiseSkill(global::Skills.SkillType.BloodMagic, settings.OrbSkillPerCast.Value);
            _controller.Invisibility.End();
            // Networked trigger: the hand moves forward on every client.
            _player.GetZAnim().SetTrigger("interact");
            _lockedUntil = Time.time + CastLock;
            _pending = true;
            _releaseAt = Time.time + settings.OrbCastDelay.Value;
            _secondary = secondary;
            _quality = orb.m_quality;
            return true;
        }

        internal void Update()
        {
            if (_pending && Time.time >= _releaseAt)
            {
                _pending = false;
                Release();
            }
            Swarm.Update();
        }

        // The hit is already on the player's owner. Returns true when a bat took all of it.
        internal bool Intercept(HitData hit)
        {
            if (hit == null || Swarm.Mode != SwarmMode.Shield || _player.IsDead() || _player.InDodge()) return false;
            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker is Player || attacker.GetComponent<SwarmBat>() != null) return false;
            SwarmBat bat = Swarm.TakeHit();
            if (bat == null) return false;
            HitData batHit = hit.Clone();
            batHit.m_point = bat.Position;
            batHit.m_hitCollider = null;
            bat.Character.Damage(batHit);
            float passThrough = OrbRules.PassThrough(Plugin.Instance.Settings.OrbShieldAbsorb.Value);
            if (passThrough <= 0f) return true;
            hit.m_damage.Modify(passThrough);
            return false;
        }

        internal void End(bool immediate)
        {
            _pending = false;
            if (immediate) Swarm.Clear();
            else Swarm.LeaveAll();
        }

        private void Release()
        {
            if (!_controller.IsActiveVampire || _player.IsDead()) return;
            Vector3 aim = _player.GetLookDir();
            Vector3 orb = OrbPoint();
            OrbEffects.Flash(orb);
            Vector3 origin = orb + aim * 0.5f;
            if (_secondary)
            {
                Swarm.StartShield(_quality, origin, aim);
                return;
            }
            var settings = Plugin.Instance.Settings;
            Character target = OrbTargeting.FindInCone(_player, _player.GetEyePoint(), aim, settings.OrbRange.Value, settings.OrbConeAngle.Value);
            Swarm.StartAttack(_quality, origin, aim, target);
        }

        private Vector3 OrbPoint()
        {
            if (_animator == null) _animator = _player.GetComponentInChildren<Animator>();
            Transform hand = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            return hand != null ? hand.position : _player.GetCenterPoint() + _player.transform.forward * 0.5f;
        }

        private bool IsBusy()
            => !_controller.CanChange || _controller.BatForm.Active || _controller.Dash.Active || _controller.Drain.Active ||
               _player.InAttack() || _player.InDodge() || _player.IsStaggering() || _player.IsAttached() ||
               _player.IsSwimming() || _player.IsTeleporting() || _player.InEmote() || _player.IsKnockedBack();

        private void Report(OrbBlock block)
        {
            if (block == OrbBlock.NoEitr)
            {
                Hud.instance?.EitrBarEmptyFlash();
                return;
            }
            if (Time.time < _nextMessage) return;
            _nextMessage = Time.time + MessageInterval;
            switch (block)
            {
                case OrbBlock.NotVampire:
                    _player.Message(MessageHud.MessageType.Center, "$vampirizm_orb_not_vampire");
                    break;
                case OrbBlock.SwarmBusy:
                    _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_orb_swarm_busy");
                    break;
                case OrbBlock.Broken:
                    _player.Message(MessageHud.MessageType.Center, "$vampirizm_orb_broken");
                    break;
                case OrbBlock.NoEitrBar:
                    _player.Message(MessageHud.MessageType.Center, "$hud_eitrrequired");
                    break;
                default:
                    _player.Message(MessageHud.MessageType.TopLeft, "$vampirizm_orb_busy");
                    break;
            }
        }
    }
}
