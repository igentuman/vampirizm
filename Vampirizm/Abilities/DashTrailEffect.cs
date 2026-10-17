using UnityEngine;

namespace Vampirizm.Abilities
{
    internal static class DashTrailEffect
    {
        private const float Lifetime = 0.22f;

        internal static ParticleSystem Play(Transform player)
        {
            Material material = BloodSplashEffect.GetMaterial();
            if (material == null) return null;
            var root = new GameObject("VampirizmDashTrail");
            root.transform.SetParent(player, false);
            root.transform.localPosition = Vector3.up * 0.8f;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startColor = new Color(0.45f, 0.015f, 0.035f, 0.22f);
            main.maxParticles = 48;
            main.gravityModifier = 0f;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 3f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            particles.Play();
            return particles;
        }

        internal static void Stop(ParticleSystem particles)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            particles.transform.SetParent(null, true);
            Object.Destroy(particles.gameObject, Lifetime + 0.1f);
        }
    }
}
