using UnityEngine;
using Vampirizm.Abilities;
using Object = UnityEngine.Object;

namespace Vampirizm.Visuals
{
    // Every observer plays a local burst when the replicated mode becomes visible.
    internal static class BloodRageActivationEffect
    {
        private static Material _smokeMaterial;

        internal static void Play(Player player)
        {
            EffectSpawner.SpawnLocalCopy("sfx_ghost_death", null, player.GetCenterPoint(), Quaternion.identity, 1f);
            Transform playerTransform = player.transform;
            Vector3 chest = playerTransform.position + Vector3.up * 1.25f;
            for (int side = -1; side <= 1; side++)
            {
                Vector3 point = chest + playerTransform.right * (side * 0.28f);
                Quaternion direction = Quaternion.LookRotation((playerTransform.right * side + Vector3.up * 0.4f - playerTransform.forward).normalized);
                EffectSpawner.SpawnLocalCopy("vfx_BloodHit", "slime", point, direction, 1.3f);
            }

            Material blood = BloodSplashEffect.GetMaterial();
            if (blood == null) return;
            var root = new GameObject("BloodRageActivation");
            root.transform.position = playerTransform.position;
            // World-space bursts linger at the transformation point as the player moves away.
            CreateBurst(root.transform, "CrimsonSparks", blood, 64, 0.75f, 0.035f, 0.10f, 1.5f, 3.6f,
                new Color(1f, 0.025f, 0.055f, 1f), -0.25f, false);
            Material smoke = SmokeMaterial(blood);
            CreateBurst(root.transform, "RisingBloodSmoke", smoke, 28, 1.6f, 0.35f, 0.7f, 0.2f, 0.65f,
                new Color(0.13f, 0.025f, 0.035f, 0.6f), -0.12f, true);
            Object.Destroy(root, 3f);
        }

        private static void CreateBurst(Transform parent, string name, Material material, short count, float lifetime,
            float minSize, float maxSize, float minSpeed, float maxSpeed, Color tint, float gravity, bool smoke)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * (smoke ? 0.65f : 1.1f);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.4f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.65f, lifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = tint;
            main.gravityModifier = gravity;
            main.maxParticles = count;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(smoke ? 0.08f : 0.22f, count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = smoke ? 0.65f : 0.4f;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, smoke ? 0.5f : 1f, 1f, smoke ? 2.2f : 0.1f));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = smoke ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.4f;
            renderer.velocityScale = 0.08f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }

        private static Material SmokeMaterial(Material source)
        {
            if (_smokeMaterial != null) return _smokeMaterial;
            // Reuse a shader shipped by the game; a soft radial texture makes clouds instead of blood flecks.
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "BloodRageSmoke", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _smokeMaterial = new Material(source) { name = "BloodRageSmoke", mainTexture = texture };
            return _smokeMaterial;
        }
    }
}
