using UnityEngine;

namespace Vampirizm.Abilities
{
    // Shared wispy texture and transparent material for transformation and summon fog.
    internal static class SmokeParticleMaterial
    {
        private static Material _smokeMaterial;

        internal static Material GetMaterial()
        {
            if (_smokeMaterial != null) return _smokeMaterial;
            // Borrow a shader shipped by Valheim, but replace the blood material's cutout/depth
            // settings below: retaining them turns even a soft alpha texture into solid discs.
            Material source = BloodSplashEffect.GetMaterial();
            if (source == null) return null;
            const int resolution = 128;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "VampirizmSmoke",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float u = x / (resolution - 1f);
                float v = y / (resolution - 1f);
                // Warp the falloff and vary density at two scales to break up the round silhouette.
                float offsetX = (Mathf.PerlinNoise(u * 3f + 7f, v * 3f + 13f) - 0.5f) * 0.3f;
                float offsetY = (Mathf.PerlinNoise(u * 3f + 23f, v * 3f + 5f) - 0.5f) * 0.3f;
                float radius = new Vector2((u - 0.5f) * 2f + offsetX, (v - 0.5f) * 2f + offsetY).magnitude;
                float density = Mathf.PerlinNoise(u * 5f + 11f, v * 5f + 17f) * 0.65f
                    + Mathf.PerlinNoise(u * 11f + 31f, v * 11f + 3f) * 0.35f;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.6f)
                    * Mathf.Lerp(0.2f, 1f, density) * Mathf.SmoothStep(0f, 1f, edge * 10f);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _smokeMaterial = new Material(source) { name = "VampirizmSmoke", mainTexture = texture };
            _smokeMaterial.mainTextureScale = Vector2.one;
            _smokeMaterial.mainTextureOffset = Vector2.zero;
            // Standard particle Fade mode: preserve fractional texture and vertex alpha.
            _smokeMaterial.SetFloat("_Mode", 2f);
            _smokeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _smokeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _smokeMaterial.SetInt("_ZWrite", 0);
            _smokeMaterial.DisableKeyword("_ALPHATEST_ON");
            _smokeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _smokeMaterial.DisableKeyword("_ALPHAMODULATE_ON");
            _smokeMaterial.EnableKeyword("_ALPHABLEND_ON");
            _smokeMaterial.SetOverrideTag("RenderType", "Transparent");
            _smokeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (_smokeMaterial.HasProperty("_Color")) _smokeMaterial.SetColor("_Color", Color.white);
            if (_smokeMaterial.HasProperty("_TintColor")) _smokeMaterial.SetColor("_TintColor", Color.white);
            if (_smokeMaterial.HasProperty("_Glossiness")) _smokeMaterial.SetFloat("_Glossiness", 0f);
            return _smokeMaterial;
        }
    }
}
