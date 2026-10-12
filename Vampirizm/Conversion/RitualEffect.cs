using UnityEngine;
using Vampirizm.Abilities;

namespace Vampirizm.Conversion
{
    // Cosmetic rune ritual, played on every client near the new vampire: a blood vortex gathers around
    // the player under a swelling red glow, then a strike bursts a blood column and a shockwave ring.
    internal sealed class RitualEffect : MonoBehaviour
    {
        internal const string RitualRpc = "Vampirizm_Ritual";
        private const float ChargeTime = 1.8f;
        private const float FadeTime = 1.2f;
        private const float Lifetime = ChargeTime + 2.5f;
        private const float ChargeLight = 3f;
        private const float StrikeLight = 9f;
        private static readonly Color Glow = new Color(1f, 0.08f, 0.1f);

        private Player _player;
        private Light _light;
        private float _start;
        private bool _local;
        private bool _struck;

        internal static void Register(Player player)
        {
            var view = player.GetComponent<ZNetView>();
            if (view == null || view.GetZDO() == null) return;
            view.Register(RitualRpc, sender => OnRitual(player, view, sender));
        }

        // Called by the converting player's own client; the RPC also reaches that client.
        internal static void Broadcast(Player player)
        {
            var view = player.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner()) view.InvokeRPC(ZNetView.Everybody, RitualRpc);
        }

        // Only the player's owner may start its ritual, so peers cannot spam effects on others.
        private static void OnRitual(Player player, ZNetView view, long sender)
        {
            if (player == null || !view.IsValid() || view.GetZDO().GetOwner() != sender) return;
            if (player.GetComponentInChildren<RitualEffect>() != null) return;
            var root = new GameObject("VampirizmRitual");
            root.transform.SetParent(player.transform, false);
            root.AddComponent<RitualEffect>().Begin(player);
        }

        private void Begin(Player player)
        {
            _player = player;
            _local = player == Player.m_localPlayer;
            _start = Time.time;
            _light = gameObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Glow;
            _light.range = 7f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
            transform.localPosition = Vector3.up * 0.1f;

            CreateVortex(transform);
            var settings = Plugin.Instance?.Settings;
            if (settings != null) EffectSpawner.SpawnAll(settings.RuneStartEffects.Value, _player.GetCenterPoint(), _local);
            Destroy(gameObject, Lifetime);
        }

        private void Update()
        {
            float elapsed = Time.time - _start;
            if (!_struck)
            {
                float charge = Mathf.Clamp01(elapsed / ChargeTime);
                // A slow heartbeat flicker under the rising glow.
                float pulse = 0.75f + 0.25f * Mathf.Sin(elapsed * 9f);
                _light.intensity = ChargeLight * charge * charge * pulse;
                if (elapsed >= ChargeTime) Strike();
                return;
            }
            _light.intensity = StrikeLight * (1f - Mathf.Clamp01((elapsed - ChargeTime) / FadeTime));
        }

        private void Strike()
        {
            _struck = true;
            CreateColumn(transform);
            CreateShockwave(transform);
            Vector3 point = _player != null ? _player.GetCenterPoint() : transform.position;
            if (GameCamera.instance != null) GameCamera.instance.AddShake(point, 20f, 2.5f, false);
            var settings = Plugin.Instance?.Settings;
            if (settings != null) EffectSpawner.SpawnAll(settings.RuneStrikeEffects.Value, point, _local);
        }

        // Blood drawn in from a wide ring, spiralling around the player and rising.
        private static void CreateVortex(Transform parent)
        {
            var particles = CreateSystem(parent, "Vortex");
            var main = particles.main;
            main.duration = ChargeTime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.03f, 0.07f), new Color(0.3f, 0.005f, 0.02f));
            main.maxParticles = 200;

            var emission = particles.emission;
            emission.rateOverTime = 90f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.6f;
            shape.radiusThickness = 0.15f;
            // The circle lies in the XY plane; turn it flat on the ground.
            shape.rotation = new Vector3(90f, 0f, 0f);

            // Every axis of a velocity module must share one curve mode, so the zero axes are set explicitly.
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f;
            velocity.y = 1.4f;
            velocity.z = 0f;
            velocity.orbitalX = 0f;
            velocity.orbitalY = 2.5f;
            velocity.orbitalZ = 0f;
            velocity.radial = -0.9f;

            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(new Color(1f, 0.05f, 0.08f), new Color(0.15f, 0f, 0.01f)));

            particles.Play();
        }

        // A fountain of blood shooting up from the player's feet.
        private static void CreateColumn(Transform parent)
        {
            var particles = CreateSystem(parent, "Column");
            // A cone emits along its local Z; point it up.
            particles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = particles.main;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.04f, 0.08f), new Color(0.45f, 0.01f, 0.03f));
            main.gravityModifier = 0.6f;
            main.maxParticles = 160;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 120) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.4f;

            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(new Color(1f, 0.06f, 0.1f), new Color(0.25f, 0.003f, 0.015f)));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.5f;
            renderer.velocityScale = 0.05f;

            particles.Play();
        }

        // A flat ring of blood mist racing outward along the ground.
        private static void CreateShockwave(Transform parent)
        {
            var particles = CreateSystem(parent, "Shockwave");
            var main = particles.main;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.02f, 0.06f, 0.7f), new Color(0.35f, 0.005f, 0.03f, 0.5f));
            main.maxParticles = 120;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2f));

            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(new Color(0.9f, 0.03f, 0.07f), new Color(0.2f, 0f, 0.01f)));

            particles.Play();
        }

        private static ParticleSystem CreateSystem(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            // Stop before configuring: AddComponent can auto-play, and duration can't change while playing.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            // Local space keeps the effect on the player if they step during the ritual.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = BloodSplashEffect.GetMaterial();
            return particles;
        }

        private static Gradient Fade(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
