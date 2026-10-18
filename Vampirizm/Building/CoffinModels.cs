using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Vampirizm.Items;

namespace Vampirizm.Building
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CoffinModelData
    {
        [JsonProperty("parts")]
        public CoffinModelPart[] parts;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CoffinModelPart
    {
        [JsonProperty("material")]
        public string material;
        [JsonProperty("lid")]
        public bool lid;
        [JsonProperty("vertices")]
        public float[] vertices;
        [JsonProperty("normals")]
        public float[] normals;
        [JsonProperty("uv")]
        public float[] uv;
        [JsonProperty("triangles")]
        public int[] triangles;
    }

    // Blender exports a small, triangulated mesh format, so no Unity editor or asset bundle is required.
    // Meshes and textures are embedded in the DLL; placed pieces share the prefab's immutable assets.
    internal static class CoffinModels
    {
        internal static void Build(GameObject prefab, string asset, CoffinBed coffin)
        {
            WearNTear wear = prefab.GetComponent<WearNTear>();
            // The first renderer on a vanilla bed can be its snow overlay. Use the intact wood model.
            Renderer intact = wear.m_new != null ? wear.m_new.GetComponentInChildren<Renderer>(true) : null;
            Material source = intact != null ? intact.sharedMaterial : null;
            LODGroup[] oldLods = prefab.GetComponentsInChildren<LODGroup>(true);
            Renderer[] oldRenderers = prefab.GetComponentsInChildren<Renderer>(true);
            MeshFilter[] oldFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Collider[] oldColliders = prefab.GetComponentsInChildren<Collider>(true);
            GameObject staging = Child("coffin_model_staging", prefab.transform);

            try
            {
                CoffinModelData model;
                using (Stream stream = typeof(CoffinModels).Assembly.GetManifestResourceStream("Vampirizm.Assets." + asset + ".json"))
                {
                    if (stream == null) throw new InvalidOperationException("Missing coffin model: " + asset);
                    using (var reader = new StreamReader(stream)) model = JsonConvert.DeserializeObject<CoffinModelData>(reader.ReadToEnd());
                }
                if (model?.parts == null || model.parts.Length == 0)
                    throw new InvalidOperationException("Empty coffin model: " + asset);

                var materials = new Dictionary<string, Material>();
                foreach (CoffinModelPart part in model.parts)
                {
                    if (!materials.TryGetValue(part.material, out Material material))
                    {
                        material = CreateMaterial(source, part.material);
                        materials.Add(part.material, material);
                    }
                    Transform parent = staging.transform;
                    if (part.lid)
                    {
                        if (coffin.Lid == null)
                        {
                            coffin.Lid = Child("lid_hinge", parent).transform;
                            coffin.Lid.localPosition = new Vector3(-0.60f, 0.77f, 0f);
                            coffin.Lid.localRotation = Quaternion.AngleAxis(105f, Vector3.forward);
                        }
                        parent = coffin.Lid;
                    }
                    GameObject child = Child((part.lid ? "lid_" : "body_") + part.material, parent);
                    var vertices = new Vector3[part.vertices.Length / 3];
                    var uv = new Vector2[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = new Vector3(part.vertices[i * 3], part.vertices[i * 3 + 1], part.vertices[i * 3 + 2]);
                        uv[i] = new Vector2(part.uv[i * 2], part.uv[i * 2 + 1]);
                    }
                    var mesh = new Mesh { name = asset + "_" + child.name };
                    mesh.SetVertices(new List<Vector3>(vertices));
                    mesh.SetUVs(0, new List<Vector2>(uv));
                    mesh.SetTriangles(part.triangles, 0, false);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    child.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                AddColliders(staging.transform);
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(staging);
                coffin.Lid = null;
                throw;
            }

            // Keep the cloned bed intact until the replacement has been built successfully.
            // If mesh construction fails, the caller can still register a usable bed-shaped fallback.
            foreach (LODGroup lod in oldLods) UnityEngine.Object.DestroyImmediate(lod);
            foreach (Renderer renderer in oldRenderers) UnityEngine.Object.DestroyImmediate(renderer);
            foreach (MeshFilter filter in oldFilters) UnityEngine.Object.DestroyImmediate(filter);
            foreach (Collider collider in oldColliders)
                if (!collider.isTrigger) UnityEngine.Object.DestroyImmediate(collider);
            wear.m_new = wear.m_worn = wear.m_broken = wear.m_wet = null;
            wear.m_snow = wear.m_snowWorn = wear.m_snowBroken = null;
            wear.m_nonSolidRenderers?.Clear();
            wear.m_fragmentRoots = Array.Empty<GameObject>();
            staging.name = "coffin_model";
        }

        private static Material CreateMaterial(Material source, string name)
        {
            var material = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
            material.name = "vampirizm_coffin_" + name;
            material.mainTexture = ItemVisuals.LoadTexture("tex_coffin_" + name);
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            // Remove the cloned bed's straw/wood auxiliary maps; they don't match our UVs.
            foreach (string property in new[] { "_BumpMap", "_MetallicGlossMap", "_EmissionMap", "_MossTex" })
                if (material.HasProperty(property)) material.SetTexture(property, null);
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_EMISSION");
            if (material.HasProperty("_Color")) material.color = Color.white;
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", name == "iron" ? .8f : 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", name == "iron" ? .45f : .12f);
            return material;
        }

        private static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name) { layer = parent.gameObject.layer };
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void AddColliders(Transform root)
        {
            Vector2[] outline = { new Vector2(-.33f, -1.15f), new Vector2(.33f, -1.15f), new Vector2(.60f, .48f),
                new Vector2(.42f, 1.15f), new Vector2(-.42f, 1.15f), new Vector2(-.60f, .48f) };
            // A solid floor and six thin sides leave the sleeping cavity hollow. No moving lid collider
            // can catch the waking player or obstruct the vanilla detach/respawn point.
            BoxCollider floor = Child("coffin_floor", root).AddComponent<BoxCollider>();
            floor.center = new Vector3(0f, .15f, 0f);
            floor.size = new Vector3(.68f, .30f, 2.3f);
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                GameObject wall = Child("coffin_wall_" + i, root);
                wall.transform.localPosition = new Vector3((a.x + b.x) * .5f, .5f, (a.y + b.y) * .5f);
                wall.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, 0f);
                wall.AddComponent<BoxCollider>().size = new Vector3(Vector2.Distance(a, b), .55f, .09f);
            }
        }
    }
}
