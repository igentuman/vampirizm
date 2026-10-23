using UnityEngine;

namespace Vampirizm.Abilities
{
    // Short-lived blood streak behind a moving transform. Particles are placed by hand along the
    // path travelled each frame: rate-over-distance emission never produced anything in game.
    internal static class DashTrailEffect
    {
        private const float Lifetime = 0.22f;

        // Dash: a faint red haze, barely noticeable rather than a visible streak.
        internal static ParticleSystem Play(Transform player)
            => Play(player, Vector3.up * 0.8f, 0.8f, 10f, new Color(0.5f, 0.015f, 0.035f, 0.1f), false, false);

        // scale multiplies particle size; perMetre is particle density along the path. Manual trails only emit
        // when Step is called, so a trail attached to a procedurally posed bone samples the final pose.
        internal static ParticleSystem Play(Transform parent, Vector3 localOffset, float scale, float perMetre, Color tint, bool stretch, bool manual)
        {
            Material material = BloodSplashEffect.GetMaterial();
            if (material == null || parent == null) return null;
            var root = new GameObject("VampirizmDashTrail");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localOffset;
            root.transform.localScale = Vector3.one;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * scale, 0.22f * scale);
            main.startColor = tint;
            main.maxParticles = 512;
            main.gravityModifier = 0f;

            // No automatic emission at all; the emitter places every particle.
            var emission = particles.emission;
            emission.enabled = false;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, stretch ? 0.5f : 0.25f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (stretch)
            {
                // Slivers elongated along the direction of travel read as claw streaks.
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 3f;
                renderer.velocityScale = 0f;
            }
            else renderer.renderMode = ParticleSystemRenderMode.Billboard;
            particles.Play();

            var emitter = root.AddComponent<DashTrailEmitter>();
            emitter.Particles = particles;
            emitter.Spacing = 1f / Mathf.Max(1f, perMetre);
            emitter.MinSize = 0.12f * scale;
            emitter.MaxSize = 0.22f * scale;
            emitter.Tint = tint;
            emitter.Lifetime = Lifetime;
            emitter.Stretch = stretch;
            emitter.Manual = manual;
            return particles;
        }

        internal static void Step(ParticleSystem particles)
        {
            if (particles == null) return;
            var emitter = particles.GetComponent<DashTrailEmitter>();
            if (emitter != null) emitter.Step();
        }

        internal static void Stop(ParticleSystem particles)
        {
            if (particles == null) return;
            var emitter = particles.GetComponent<DashTrailEmitter>();
            if (emitter != null) emitter.enabled = false;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            particles.transform.SetParent(null, true);
            Object.Destroy(particles.gameObject, Lifetime + 0.1f);
        }
    }

    internal sealed class DashTrailEmitter : MonoBehaviour
    {
        internal ParticleSystem Particles;
        internal float Spacing = 0.1f, MinSize = 0.1f, MaxSize = 0.2f, Lifetime = 0.22f;
        internal Color Tint = Color.red;
        internal bool Stretch, Manual;
        private Vector3 _last;
        private bool _hasLast;
        private float _carry;

        private void OnEnable() { _hasLast = false; _carry = 0f; }
        private void LateUpdate() { if (!Manual) Step(); }

        internal void Step()
        {
            if (!enabled || Particles == null) return;
            Vector3 position = transform.position;
            if (!_hasLast) { _last = position; _hasLast = true; return; }
            Vector3 delta = position - _last;
            float length = delta.magnitude;
            // Teleports and respawns restart the trail instead of drawing a streak across the world.
            if (length > 3f) { _last = position; _carry = 0f; return; }
            if (length < 0.0005f) return;
            Vector3 direction = delta / length;
            var emit = new ParticleSystem.EmitParams { startLifetime = Lifetime, startColor = Tint };
            // Stretch mode orients along velocity; keep it tiny so streaks stay where they were drawn.
            if (Stretch) emit.velocity = direction * 0.2f;
            int emitted = 0;
            for (float along = Spacing - _carry; along <= length && emitted < 64; along += Spacing, ++emitted)
            {
                emit.position = _last + direction * along;
                emit.startSize = Random.Range(MinSize, MaxSize);
                if (!Stretch) emit.rotation = Random.Range(0f, 360f);
                Particles.Emit(emit, 1);
            }
            _carry = (_carry + length) % Spacing;
            _last = position;
        }
    }
}
