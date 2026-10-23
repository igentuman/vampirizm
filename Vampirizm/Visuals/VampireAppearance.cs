using System;
using System.Collections.Generic;
using Jotunn.Managers;
using SoftReferenceableAssets;
using UnityEngine;
using UnityEngine.Rendering;
using Vampirizm.Networking;
using Object = UnityEngine.Object;

namespace Vampirizm.Visuals
{
    // Per-character resources only: never mutate the game's shared meshes or skin textures.
    internal sealed class VampireAppearance
    {
        private readonly Player _player;
        private SkinnedMeshRenderer _body;
        private Mesh _bodyMesh;
        private GameObject _fangs;
        private Mesh _fangMesh;
        private Material _fangMaterial;
        private Material _skin;
        private Texture _originalTexture;
        private Texture2D _redEyes;
        private Texture2D _eyeMask;
        private SkinnedMeshRenderer _eyeGlow;
        private Mesh _eyeMesh;
        private Material _eyeMaterial;
        private SoftReference<Material> _eyeTemplate;
        private bool _holdsEyeTemplate;
        private bool _warned;

        internal VampireAppearance(Player player) => _player = player;

        internal void Update()
        {
            if (_warned) return;
            if (!VampireNetwork.IsVampire(_player))
            {
                Clear();
                return;
            }
            var equipment = _player.GetComponent<VisEquipment>();
            SkinnedMeshRenderer body = equipment != null ? equipment.m_bodyModel : null;
            if (body == null || body.sharedMesh == null) return;
            // These UVs and fang positions belong to the two vanilla player meshes.
            // Custom race models may have entirely different faces.
            string model = body.sharedMesh.name;
            if (model != "body" && model != "bodyfem")
            {
                Clear();
                return;
            }
            try
            {
                if (_body != body || _bodyMesh != body.sharedMesh)
                {
                    Clear();
                    _body = body;
                    _bodyMesh = body.sharedMesh;
                    CreateFangs(model == "bodyfem");
                }
                UpdateEyes();
                bool visible = body.enabled && !body.forceRenderingOff &&
                    !VampireNetwork.IsBatForm(_player) && !VampireNetwork.IsInvisible(_player);
                if (_eyeGlow != null)
                {
                    _eyeGlow.gameObject.SetActive(visible);
                    _eyeGlow.localBounds = body.localBounds;
                    for (int i = 0; i < _bodyMesh.blendShapeCount; i++)
                        _eyeGlow.SetBlendShapeWeight(i, body.GetBlendShapeWeight(i));
                }
                if (_fangs != null)
                {
                    // The body hierarchy handles bat form; explicit visibility also covers
                    // Character.SetVisible, which only toggles vanilla's cached renderers.
                    _fangs.SetActive(visible);
                }
            }
            catch (Exception exception)
            {
                Clear();
                if (!_warned) Plugin.Warn($"Vampire appearance unavailable: {exception.Message}");
                _warned = true;
            }
        }

        private void UpdateEyes()
        {
            Material[] materials = _body.sharedMaterials;
            Material skin = materials.Length > 0 ? materials[0] : null;
            if (skin == null || !skin.HasProperty("_MainTex")) return;
            Texture current = skin.GetTexture("_MainTex");
            if (skin == _skin && current == _redEyes && _redEyes != null)
            {
                if (_eyeGlow == null) CreateEyeGlow();
                return;
            }
            RestoreEyes();
            if (current == null) return;
            _skin = skin;
            _originalTexture = current;
            // Imported game textures are not CPU-readable. Read a private GPU copy once
            // per skin change; retain its size, filter and wrap settings.
            RenderTexture previous = RenderTexture.active;
            RenderTexture copy = RenderTexture.GetTemporary(current.width, current.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(current, copy);
                RenderTexture.active = copy;
                _redEyes = new Texture2D(current.width, current.height, TextureFormat.RGBA32, true)
                {
                    name = "VampirizmRedEyes",
                    filterMode = current.filterMode,
                    wrapMode = current.wrapMode,
                    anisoLevel = current.anisoLevel
                };
                _redEyes.ReadPixels(new Rect(0, 0, current.width, current.height), 0, 0);
                Color[] pixels = _redEyes.GetPixels();
                var emission = new Color[pixels.Length];
                // Both vanilla faces map their irises to this small blue texture island.
                // Recolor only blue pixels, preserving eyelids, skin and original shading.
                for (int y = Mathf.FloorToInt(current.height * 0.89f); y < Mathf.CeilToInt(current.height * 0.93f); y++)
                for (int x = Mathf.FloorToInt(current.width * 0.45f); x < Mathf.CeilToInt(current.width * 0.51f); x++)
                {
                    int index = y * current.width + x;
                    Color color = pixels[index];
                    if (color.b <= color.r * 1.15f || color.b <= color.g * 1.05f) continue;
                    pixels[index] = new Color(color.b * 0.85f, color.g * 0.10f, color.r * 0.15f, color.a);
                    emission[index] = new Color(color.b, color.b, color.b, 1f);
                }
                _redEyes.SetPixels(pixels);
                _redEyes.Apply(true, true);
                skin.SetTexture("_MainTex", _redEyes);
                _eyeMask = new Texture2D(current.width, current.height, TextureFormat.RGBA32, true)
                {
                    name = "VampirizmEyeEmissionMask",
                    filterMode = current.filterMode,
                    wrapMode = current.wrapMode,
                    anisoLevel = current.anisoLevel
                };
                _eyeMask.SetPixels(emission);
                _eyeMask.Apply(true, true);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(copy);
            }
            CreateEyeGlow();
        }

        private void CreateEyeGlow()
        {
            if (_eyeMask == null || !AssetManager.Instance.IsReady()) return;
            // Custom/Player has no emission properties. Use a depth-tested additive
            // pass just above the skinned surface; black outside the irises adds nothing.
            // Explicitly load a vanilla material because Shader.Find cannot load bundles.
            _eyeTemplate = AssetManager.Instance.GetSoftReference<Material>("BloodLightning_Splat");
            if (!_eyeTemplate.IsValid) return;
            _holdsEyeTemplate = true;
            LoadResult result = _eyeTemplate.Load();
            Material template = result == LoadResult.Succeeded ? _eyeTemplate.Asset : null;
            if (template == null || template.shader == null || !template.shader.isSupported ||
                template.shader.name != "Particles/Standard Unlit2")
                throw new InvalidOperationException("Eye glow shader could not be loaded.");

            _eyeMaterial = new Material(template) { name = "VampirizmEyeGlow" };
            _eyeMaterial.shaderKeywords = Array.Empty<string>();
            _eyeMaterial.SetTexture("_MainTex", _eyeMask);
            _eyeMaterial.SetTextureScale("_MainTex", _skin.GetTextureScale("_MainTex"));
            _eyeMaterial.SetTextureOffset("_MainTex", _skin.GetTextureOffset("_MainTex"));
            _eyeMaterial.SetColor("_Color", new Color(3f, 0.015f, 0.025f, 1f));
            _eyeMaterial.SetFloat("_Mode", 4f); // Additive, without soft-particle or camera fading.
            _eyeMaterial.SetInt("_BlendOp", (int)BlendOp.Add);
            _eyeMaterial.SetInt("_SrcBlend", (int)BlendMode.One);
            _eyeMaterial.SetInt("_DstBlend", (int)BlendMode.One);
            _eyeMaterial.SetInt("_ZWrite", 0);
            _eyeMaterial.SetInt("_Cull", (int)CullMode.Back);
            _eyeMaterial.EnableKeyword("_ALPHABLEND_ON");
            _eyeMaterial.SetOverrideTag("RenderType", "Transparent");
            _eyeMaterial.renderQueue = (int)RenderQueue.Transparent;

            _eyeMesh = Object.Instantiate(_bodyMesh);
            _eyeMesh.name = "VampirizmEyeGlow";
            // Different shaders can produce slightly different depths for coincident
            // skinned vertices. Separate the surfaces instead of disabling depth tests,
            // which would make the eyes visible through the head and other objects.
            // Vanilla mesh units are scaled by 100: 0.00001 is 1 mm in world space.
            const float eyeSurfaceOffset = 0.00001f;
            Vector3[] vertices = _eyeMesh.vertices;
            Vector3[] normals = _eyeMesh.normals;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] += normals[i].normalized * eyeSurfaceOffset;
            _eyeMesh.vertices = vertices;
            _eyeMesh.subMeshCount = 1; // Skin only; omit the hair/other material submeshes.
            _eyeMesh.colors = Array.Empty<Color>();
            var eyes = new GameObject("VampirizmEyeGlow") { layer = _body.gameObject.layer };
            eyes.transform.SetParent(_body.transform, false);
            _eyeGlow = eyes.AddComponent<SkinnedMeshRenderer>();
            _eyeGlow.sharedMesh = _eyeMesh;
            _eyeGlow.sharedMaterial = _eyeMaterial;
            _eyeGlow.bones = _body.bones;
            _eyeGlow.rootBone = _body.rootBone;
            _eyeGlow.quality = _body.quality;
            _eyeGlow.updateWhenOffscreen = _body.updateWhenOffscreen;
            _eyeGlow.localBounds = _body.localBounds;
            _eyeGlow.shadowCastingMode = ShadowCastingMode.Off;
            _eyeGlow.receiveShadows = false;
        }

        private void CreateFangs(bool female)
        {
            Transform[] bones = _body.bones;
            int head = Array.FindIndex(bones, bone => bone != null && bone.name == "Head");
            if (head < 0 || head >= _bodyMesh.bindposes.Length)
            {
                Plugin.Warn("Cannot create vampire fangs: body mesh has no Head bind pose.");
                return;
            }
            Material[] materials = _body.sharedMaterials;
            Material source = materials.Length > 0 ? materials[0] : null;
            if (source == null)
            {
                Plugin.Warn("Cannot create vampire fangs: body material is unavailable.");
                return;
            }
            // Standard is stripped from the game build. Reuse the body's loaded shader,
            // with a private material so teeth do not inherit skin or equipment textures.
            _fangMaterial = new Material(source) { name = "VampirizmIvory" };
            foreach (string property in _fangMaterial.GetTexturePropertyNames())
                _fangMaterial.SetTexture(property, property == "_MainTex" ? Texture2D.whiteTexture : null);
            Color ivory = new Color(0.95f, 0.91f, 0.78f);
            foreach (string property in new[] { "_Color", "_SkinColor", "_SkinColor1", "_SkinColor2" })
                if (_fangMaterial.HasProperty(property)) _fangMaterial.SetColor(property, ivory);
            _fangMaterial.DisableKeyword("_NORMALMAP");
            _fangMaterial.DisableKeyword("_EMISSION");
            if (_fangMaterial.HasProperty("_Glossiness")) _fangMaterial.SetFloat("_Glossiness", 0.3f);
            _fangs = new GameObject("VampirizmFangs") { layer = _body.gameObject.layer };
            _fangs.transform.SetParent(bones[head], false);
            // The vanilla armature has a 100x scale. Its mesh coordinates are Z-up,
            // facing -Y, while the head bone has its own orientation and origin.
            // A bind pose maps mesh coordinates into that bone's local space.
            Matrix4x4 meshToHead = _bodyMesh.bindposes[head];
            float mouthZ = female ? 0.01804f : 0.01808f;
            float mouthY = female ? -0.00142f : -0.00154f;
            // Model units are 1/100 metre. Keep the base at the upper lip and extend
            // only downward: 2.4 cm long, 1.2 cm wide, at the canine positions.
            const float length = 0.00024f;
            const float halfWidth = 0.00006f;
            const float halfDepth = 0.00005f;
            const float spacing = 0.00022f;
            const int sides = 6;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 center = new Vector3(side * spacing, mouthY, mouthZ);
                Vector3 tip = center + new Vector3(-side * 0.00001f, -0.000005f, -length);
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    float nextAngle = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 edge = center + new Vector3(Mathf.Cos(angle) * halfWidth, Mathf.Sin(angle) * halfDepth, 0f);
                    Vector3 next = center + new Vector3(Mathf.Cos(nextAngle) * halfWidth, Mathf.Sin(nextAngle) * halfDepth, 0f);
                    // Separate vertices give each facet a crisp normal, matching the face.
                    foreach (Vector3 vertex in new[] { edge, tip, next, center, edge, next })
                    {
                        triangles.Add(vertices.Count);
                        vertices.Add(meshToHead.MultiplyPoint3x4(vertex));
                    }
                }
            }
            _fangMesh = new Mesh
            {
                name = "VampirizmFangs",
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray(),
                uv = new Vector2[vertices.Count]
            };
            _fangMesh.RecalculateNormals();
            _fangMesh.RecalculateBounds();
            _fangs.AddComponent<MeshFilter>().sharedMesh = _fangMesh;
            MeshRenderer renderer = _fangs.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _fangMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Plugin.LogInfo($"Created vampire fangs using {_fangMaterial.shader.name} on {bones[head].name} (head scale {bones[head].lossyScale}).");
        }

        private void RestoreEyes()
        {
            if (_eyeGlow != null)
            {
                _eyeGlow.gameObject.SetActive(false);
                Object.Destroy(_eyeGlow.gameObject);
            }
            if (_eyeMaterial != null) Object.Destroy(_eyeMaterial);
            if (_eyeMesh != null) Object.Destroy(_eyeMesh);
            if (_eyeMask != null) Object.Destroy(_eyeMask);
            if (_holdsEyeTemplate) _eyeTemplate.Release();
            _eyeGlow = null;
            _eyeMaterial = null;
            _eyeMesh = null;
            _eyeMask = null;
            _holdsEyeTemplate = false;
            _eyeTemplate = default;
            // An equipment or model update may already have installed a different skin.
            if (_skin != null && _redEyes != null && _skin.GetTexture("_MainTex") == _redEyes)
                _skin.SetTexture("_MainTex", _originalTexture);
            if (_redEyes != null) Object.Destroy(_redEyes);
            _skin = null;
            _redEyes = null;
            _originalTexture = null;
        }

        internal void Clear()
        {
            RestoreEyes();
            if (_fangs != null)
            {
                _fangs.SetActive(false);
                Object.Destroy(_fangs);
            }
            if (_fangMaterial != null) Object.Destroy(_fangMaterial);
            if (_fangMesh != null) Object.Destroy(_fangMesh);
            _fangs = null;
            _fangMesh = null;
            _fangMaterial = null;
            _body = null;
            _bodyMesh = null;
        }
    }
}
