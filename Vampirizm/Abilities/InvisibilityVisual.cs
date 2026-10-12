#nullable disable
using System;
using System.Collections.Generic;
using Jotunn.Managers;
using SoftReferenceableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampirizm.Abilities
{
    // VisEquipment reads Renderer.materials every update, which can instantiate our
    // replacements. Never leave them installed for gameplay to mistake for originals.
    // Only the camera sees ghost materials; each render restores its exact input.
    internal sealed class InvisibilityVisual
    {
        private sealed class RendererState
        {
            internal Renderer Renderer;
            internal Material[] Original;
            internal Material[] Ghost;
            internal bool Applied;
            internal bool Hidden;
            internal bool WasForcedOff;
        }

        private readonly Player _player;
        private readonly Dictionary<Renderer, RendererState> _states = new Dictionary<Renderer, RendererState>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Renderer> _removed = new List<Renderer>();
        private readonly HashSet<Renderer> _seen = new HashSet<Renderer>();
        private readonly List<Camera> _cameras = new List<Camera>();
        private Shader _shader;
        private SoftReference<Material> _spiritMaterial;
        private bool _holdsSpiritMaterial;
        private bool _shaderAttempted;
        private bool _reportedVisual;
        private bool _active;
        private bool _warned;
        private int _renderFrame = -1;
        private const string SpiritShader = "Custom/Fallen Warrior";
        private const float LocalAlpha = 0.38f;

        internal InvisibilityVisual(Player player) => _player = player;

        internal void SetActive(bool active)
        {
            active &= _player != null && _player == Player.m_localPlayer;
            if (!active)
            {
                if (!_active && _states.Count == 0 && _cameras.Count == 0) return;
                Camera.onPreCull -= BeforeCamera;
                Camera.onPostRender -= AfterCamera;
                _active = false;
                Restore();
                foreach (RendererState state in _states.Values) DestroyMaterials(state.Ghost);
                _states.Clear();
                _renderers.Clear();
                _removed.Clear();
                _seen.Clear();
                // Keep the bundle alive until every renderer has its originals back.
                if (_holdsSpiritMaterial) _spiritMaterial.Release();
                _holdsSpiritMaterial = false;
                _spiritMaterial = default;
                _shader = null;
                _shaderAttempted = false;
                _reportedVisual = false;
                _warned = false;
                return;
            }
            // Load outside camera callbacks. Shader.Find does not load SoftRef assets.
            PrepareShader();
            if (_active) return;
            _active = true;
            Camera.onPreCull += BeforeCamera;
            Camera.onPostRender += AfterCamera;
        }

        private void PrepareShader()
        {
            if (_shader != null || _shaderAttempted || !AssetManager.Instance.IsReady()) return;
            _shaderAttempted = true;
            string assetStatus = "material not found in SoftRef manifest";
            try
            {
                // Jotunn resolves the material by filename (FallenWarrior.mat), not
                // the shader's display name. Load acquires a reference even on failure.
                _spiritMaterial = AssetManager.Instance.GetSoftReference<Material>("FallenWarrior");
                if (_spiritMaterial.IsValid)
                {
                    _holdsSpiritMaterial = true;
                    LoadResult result = _spiritMaterial.Load();
                    Material template = result == LoadResult.Succeeded ? _spiritMaterial.Asset : null;
                    Shader shader = template != null ? template.shader : null;
                    assetStatus = $"material load={result}, shader={(shader != null ? shader.name : "null")}, supported={shader != null && shader.isSupported}";
                    if (shader != null && shader.name == SpiritShader && shader.isSupported) _shader = shader;
                }
            }
            catch (Exception exception)
            {
                assetStatus = $"material load error: {exception.Message}";
            }

            if (_shader == null)
            {
                _shader = Shader.Find(SpiritShader);
                if (_shader == null || !_shader.isSupported) _shader = Shader.Find("Standard TwoSided");
            }
            if (_shader != null && _shader.isSupported)
            {
                Plugin.LogInfo($"Invisibility shader ready: {_shader.name}; {assetStatus}");
                return;
            }
            _shader = null;
            Plugin.Warn($"Invisibility visual unavailable: {assetStatus}; no supported fallback shader.");
            _warned = true;
        }

        // Also recovers an interrupted camera render before the next frame is drawn.
        internal void RecoverFrame()
        {
            if (_cameras.Count > 0 && _renderFrame != Time.frameCount) Restore();
            if (_active && (_player == null || _player != Player.m_localPlayer)) SetActive(false);
        }

        // A failed camera callback must not let the next VisEquipment update write
        // new armor textures into a temporary material that will be thrown away.
        internal void BeforeEquipmentUpdate()
        {
            if (_cameras.Count > 0) Restore();
        }

        private void BeforeCamera(Camera camera)
        {
            RecoverFrame();
            if (!_active) return;
            // A repeated pre-cull without post-render means the old render was aborted.
            if (_cameras.Contains(camera)) Restore();
            _cameras.Add(camera);
            if (_cameras.Count > 1) return; // Nested reflection cameras share this scope.
            _renderFrame = Time.frameCount;
            try
            {
                Apply();
            }
            catch (Exception exception)
            {
                // A failed visual must never strand a partially replaced character.
                SetActive(false);
                if (!_warned) Plugin.Warn($"Invisibility visual restored after a render error: {exception}");
                _warned = true;
            }
        }

        private void AfterCamera(Camera camera)
        {
            int index = _cameras.LastIndexOf(camera);
            if (index < 0) return;
            _cameras.RemoveRange(index, _cameras.Count - index);
            if (_cameras.Count == 0) Restore();
        }

        private void Apply()
        {
            if (_shader == null) return;

            _renderers.Clear();
            _seen.Clear();
            _player.GetComponentsInChildren(true, _renderers);
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null) continue;
                _seen.Add(renderer);
                if (!_states.TryGetValue(renderer, out RendererState state))
                {
                    state = new RendererState { Renderer = renderer };
                    _states.Add(renderer, state);
                }
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    // Particles and trails have no surface to ghost: the spirit shader draws
                    // their billboards as solid quads over the body. Hide them instead.
                    state.WasForcedOff = renderer.forceRenderingOff;
                    state.Hidden = true;
                    state.Applied = true;
                    renderer.forceRenderingOff = true;
                    continue;
                }
                Material[] originals = renderer.sharedMaterials;
                if (!SameMaterials(state.Original, originals))
                {
                    DestroyMaterials(state.Ghost);
                    state.Original = originals;
                    state.Ghost = new Material[originals.Length];
                }
                for (int i = 0; i < originals.Length; i++)
                {
                    Material source = originals[i];
                    if (source == null) continue;
                    if (state.Ghost[i] == null) state.Ghost[i] = new Material(_shader);
                    UpdateMaterial(source, state.Ghost[i]);
                }
                // Capture originals only outside a render scope, never from a ghost copy.
                state.Applied = true;
                renderer.sharedMaterials = state.Ghost;
            }

            _removed.Clear();
            foreach (var pair in _states)
            {
                if (_seen.Contains(pair.Key)) continue;
                DestroyMaterials(pair.Value.Ghost);
                _removed.Add(pair.Key);
            }
            foreach (Renderer renderer in _removed) _states.Remove(renderer);
            if (!_reportedVisual && _states.Count > 0)
            {
                Plugin.LogInfo($"Invisibility visual applied to {_states.Count} renderers using {_shader.name}.");
                _reportedVisual = true;
            }
        }

        private void Restore()
        {
            foreach (RendererState state in _states.Values)
            {
                if (!state.Applied) continue;
                if (state.Renderer != null)
                {
                    if (state.Hidden) state.Renderer.forceRenderingOff = state.WasForcedOff;
                    else state.Renderer.sharedMaterials = state.Original;
                }
                state.Applied = false;
            }
            _cameras.Clear();
        }

        private static void UpdateMaterial(Material source, Material ghost)
        {
            // Includes _ChestTex/_LegsTex and their normals, unlike a generic diffuse
            // replacement. Equipment/skin changes update originals between camera renders.
            ghost.CopyPropertiesFromMaterial(source);
            ghost.shaderKeywords = Array.Empty<string>();
            ghost.SetOverrideTag("RenderType", "Transparent");
            ghost.renderQueue = 3000;
            if (ghost.shader.name == SpiritShader)
            {
                // The spirit shader uses a fixed blend and a tint, not Standard's _Mode
                // or _Color.a. Keep its native passes and use a subdued neutral spirit tint.
                ghost.SetColor("_TintColor", new Color(LocalAlpha, LocalAlpha, LocalAlpha, 1f));
                if (!source.HasProperty("_SkinColor"))
                    ghost.SetColor("_SkinColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
                if (!source.HasProperty("_SkinBumpMap") && source.HasProperty("_BumpMap"))
                    ghost.SetTexture("_SkinBumpMap", source.GetTexture("_BumpMap"));
                return;
            }

            Color color = source.HasProperty("_Color") ? source.GetColor("_Color") :
                source.HasProperty("_SkinColor") ? source.GetColor("_SkinColor") : Color.white;
            color.a = LocalAlpha;
            ghost.SetColor("_Color", color);
            ghost.SetFloat("_Mode", 2f); // Standard Fade, rather than premultiplied Transparent.
            ghost.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            ghost.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ghost.SetInt("_ZWrite", 0);
            ghost.EnableKeyword("_ALPHABLEND_ON");
        }

        private static bool SameMaterials(Material[] left, Material[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i]) return false;
            return true;
        }

        private static void DestroyMaterials(Material[] materials)
        {
            if (materials == null) return;
            foreach (Material material in materials) if (material != null) Object.Destroy(material);
        }
    }
}
