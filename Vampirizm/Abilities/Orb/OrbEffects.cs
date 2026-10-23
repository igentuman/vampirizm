using UnityEngine;

namespace Vampirizm.Abilities.Orb
{
    // Soft red fog for the orb and its bats, using the transformation smoke material.
    internal static class OrbEffects
    {
        // Cast fog at the orb when the bats burst out.
        internal static void Flash(Vector3 position) => Burst(position, 18, 0.18f, 0.35f, 0.9f, 1.2f);

        // A bat appearing, returning or leaving.
        internal static void Poof(Vector3 position)
        {
            Burst(position, 8, 0.15f, 0.15f, 0.45f, 0.9f);
            var settings = Plugin.Instance?.Settings;
            if (settings != null) EffectSpawner.SpawnRandom(settings.BatFlapSounds.Value, position, true);
        }

        private static void Burst(Vector3 position, int count, float radius, float minSpeed, float maxSpeed, float lifetime)
        {
            Material material = SmokeParticleMaterial.GetMaterial();
            if (material == null) return;
            var root = new GameObject("VampirizmOrbBurst");
            root.transform.position = position;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.65f, 0.18f, 0.24f, 0.24f), new Color(0.42f, 0.1f, 0.18f, 0.32f));
            main.gravityModifier = -0.025f;
            main.maxParticles = count;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            Object.Destroy(root, lifetime + 1f);
        }
    }
}
