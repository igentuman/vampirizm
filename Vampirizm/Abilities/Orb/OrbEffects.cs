using UnityEngine;

namespace Vampirizm.Abilities.Orb
{
    // Self-contained red particle bursts for the orb; no external asset names required.
    internal static class OrbEffects
    {
        // Cast flash at the orb when the bats burst out.
        internal static void Flash(Vector3 position) => Burst(position, 48, 0.12f, 1.5f, 4.5f, 0.6f);

        // A bat appearing, returning or leaving.
        internal static void Poof(Vector3 position)
        {
            Burst(position, 18, 0.15f, 0.8f, 2.5f, 0.5f);
            var settings = Plugin.Instance?.Settings;
            if (settings != null) EffectSpawner.SpawnRandom(settings.BatFlapSounds.Value, position, true);
        }

        private static void Burst(Vector3 position, int count, float radius, float minSpeed, float maxSpeed, float lifetime)
        {
            var root = new GameObject("VampirizmOrbBurst");
            root.transform.position = position;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.03f, 0.08f), new Color(0.35f, 0.02f, 0.05f));
            main.gravityModifier = -0.2f;
            main.maxParticles = count * 2;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Color(1f, 0.1f, 0.15f, 1f), new Color(0.2f, 0.02f, 0.05f, 0f));
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            Material material = BloodSplashEffect.GetMaterial();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            particles.Play();
            Object.Destroy(root, lifetime + 1f);
        }
    }
}
