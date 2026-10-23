using Jotunn.Managers;
using UnityEngine;

namespace Vampirizm.Abilities
{
    // Client-side blood spray emitted at the donor while a bite is active: a few short
    // streaking droplets plus a faint wisp of dark red mist around the wound.
    internal static class BloodSplashEffect
    {
        private static readonly string[] SourcePrefabs = { "vfx_BloodHit", "vfx_player_hit" };
        private static Material _material;
        private static bool _materialResolved;

        internal static void Play(Vector3 position, Quaternion direction)
        {
            var root = new GameObject("VampirizmBloodSplash");
            root.transform.SetPositionAndRotation(position, direction);

            CreateDroplets(root.transform);
            CreateMist(root.transform);

            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>())
                particles.Play();
            Object.Destroy(root, 2f);
        }

        internal static void PlayRage(Vector3 position)
        {
            // Independent preset: bite splashes retain their existing size and particle counts.
            var root = new GameObject("VampirizmRageBloodSplash");
            root.transform.position = position;
            CreateDroplets(root.transform);
            CreateMist(root.transform);
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>())
            {
                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                var main = particles.main;
                main.startSpeedMultiplier *= 2.5f;
                main.startSizeMultiplier *= 1.8f;
                main.maxParticles = 48;
                var emission = particles.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18, 28) });
                particles.Play();
            }
            Object.Destroy(root, 2f);
        }

        private static void CreateDroplets(Transform parent)
        {
            var particles = CreateSystem(parent, "Droplets");
            var main = particles.main;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.95f, 0.02f, 0.05f, 1f),
                new Color(0.5f, 0.006f, 0.02f, 1f));
            main.gravityModifier = 1.8f;
            main.maxParticles = 12;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4, 8) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.05f;

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.55f));

            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(
                new Color(1f, 0.03f, 0.06f), new Color(0.3f, 0.004f, 0.015f), 0.95f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.8f;
            renderer.velocityScale = 0.1f;
        }

        private static void CreateMist(Transform parent)
        {
            var particles = CreateSystem(parent, "Mist");
            var main = particles.main;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.75f, 0.015f, 0.04f, 0.35f),
                new Color(0.35f, 0.004f, 0.02f, 0.25f));
            main.gravityModifier = 0.25f;
            main.maxParticles = 4;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1, 3) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));

            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(
                new Color(0.8f, 0.02f, 0.05f), new Color(0.25f, 0.003f, 0.012f), 0.6f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
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
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = GetMaterial();
            return particles;
        }

        private static Gradient Fade(Color start, Color end, float holdAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(holdAlpha, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        // Unity's builtin particle shaders are stripped from the Valheim build, so a material made from
        // Shader.Find renders magenta. Borrow a particle material from a vanilla blood effect instead.
        internal static Material GetMaterial()
        {
            if (_material != null || _materialResolved) return _material;
            _materialResolved = true;

            Material source = null;
            foreach (string prefab in SourcePrefabs)
            {
                source = FindParticleMaterial(prefab);
                if (source != null) break;
            }
            if (source != null)
            {
                _material = new Material(source) { name = "Vampirizm blood droplets" };
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null && shader.isSupported)
                    _material = new Material(shader) { name = "Vampirizm blood droplets" };
            }

            if (_material != null)
                Plugin.LogInfo($"Blood splash material ready: {(source != null ? source.name : "fallback")}, shader={_material.shader.name}");
            else
                Plugin.Warn("Blood splash material unavailable: no vanilla particle material or supported fallback shader.");
            return _material;
        }

        private static Material FindParticleMaterial(string prefabName)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (prefab == null) return null;
            foreach (var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null || !material.shader.isSupported) continue;
                // vfx_player_hit also carries a green slime layer.
                if (material.name.IndexOf("slime", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return material;
            }
            return null;
        }
    }
}
