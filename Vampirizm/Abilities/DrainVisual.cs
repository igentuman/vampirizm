using UnityEngine;
using Vampirizm.Networking;

namespace Vampirizm.Abilities
{
    // Cosmetic side of drinking, run on every client from the vampire's published drink target:
    // small blood splats between the victim and the vampire's face, and a periodic drinking sound.
    internal sealed class DrainVisual
    {
        private const float SplatInterval = 0.45f;
        private const float SplatScale = 0.5f;
        private const float SplatJitter = 0.08f;
        private const string GreenSplash = "slime";

        private readonly Player _player;
        private readonly ZNetView _view;
        private ZDOID _target = ZDOID.None;
        private Character _victim;
        private float _nextSplat;
        private float _nextSound;

        internal DrainVisual(Player player)
        {
            _player = player;
            _view = player.GetComponent<ZNetView>();
        }

        internal void Update()
        {
            ZDOID target = VampireNetwork.GetDrinkTarget(_view);
            if (target != _target)
            {
                _target = target;
                _victim = null;
                _nextSplat = _nextSound = Time.time;
            }
            if (_target.IsNone()) return;
            if (_victim == null)
            {
                GameObject instance = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(_target) : null;
                _victim = instance != null ? instance.GetComponent<Character>() : null;
                if (_victim == null || _victim.IsDead()) return;
            }
            var settings = Plugin.Instance.Settings;
            Vector3 mouth = _player.GetHeadPoint();
            CapsuleCollider capsule = _victim.GetCollider();
            Vector3 point = capsule != null ? capsule.ClosestPoint(mouth) : _victim.GetCenterPoint();
            Vector3 toMouth = mouth - point;
            Quaternion facing = toMouth.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toMouth) : Quaternion.identity;
            bool local = _player == Player.m_localPlayer;
            if (Time.time >= _nextSound)
            {
                _nextSound = Time.time + settings.BiteInterval.Value;
                EffectSpawner.Spawn(settings.BiteSound.Value, point, facing, 1f, local);
            }
            if (Time.time >= _nextSplat)
            {
                _nextSplat = Time.time + SplatInterval;
                BloodSplashEffect.Play(point, facing);
                EffectSpawner.SpawnLocalCopy(settings.BiteBloodEffect.Value, GreenSplash,
                    point + Random.insideUnitSphere * SplatJitter, Random.rotation, SplatScale);
            }
        }
    }
}
