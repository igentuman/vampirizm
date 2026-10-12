using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace Vampirizm.Abilities
{
    // Spawns vanilla effect prefabs by name for cosmetic ability feedback.
    internal static class EffectSpawner
    {
        private const float EffectLifetime = 10f;
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, GameObject> LocalCopies = new Dictionary<string, GameObject>();
        private static GameObject _templateRoot;

        // Most vanilla effects carry a non-persistent ZNetView and replicate themselves, as with EffectList.Create:
        // only the acting player's own client spawns those. Plain local effects are spawned by every client.
        internal static void Spawn(string name, Vector3 point, Quaternion rotation, float scale, bool local)
        {
            GameObject prefab = Find(name);
            if (prefab == null) return;
            bool networked = prefab.GetComponent<ZNetView>() != null;
            if (networked && !local) return;
            Instantiate(prefab, point, rotation, scale, networked);
        }

        // Spawns a client-side copy of the prefab on every client, without its ZNetView. Particle layers whose
        // material name contains the excluded keyword are removed: vanilla blood hits carry a green slime splash.
        internal static void SpawnLocalCopy(string name, string excludedMaterial, Vector3 point, Quaternion rotation, float scale)
        {
            GameObject template = LocalCopy(name, excludedMaterial);
            if (template != null) Instantiate(template, point, rotation, scale, false);
        }

        private static void Instantiate(GameObject prefab, Vector3 point, Quaternion rotation, float scale, bool networked)
        {
            GameObject effect = Object.Instantiate(prefab, point, rotation);
            if (scale != 1f)
            {
                effect.transform.localScale *= scale;
                // Child systems only follow the root scale in hierarchy mode.
                foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = particles.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            // Networked effects end through their own timed destruction, which also removes the ZDO.
            if (!networked) Object.Destroy(effect, EffectLifetime);
        }

        // Comma-separated prefab list: every entry is spawned.
        internal static void SpawnAll(string names, Vector3 point, bool local)
        {
            if (string.IsNullOrEmpty(names)) return;
            foreach (string name in names.Split(',')) Spawn(name, point, Quaternion.identity, 1f, local);
        }

        // Comma-separated prefab list: one random entry is spawned.
        internal static void SpawnRandom(string names, Vector3 point, bool local)
        {
            if (string.IsNullOrEmpty(names)) return;
            string[] list = names.Split(',');
            Spawn(list[Random.Range(0, list.Length)], point, Quaternion.identity, 1f, local);
        }

        private static GameObject LocalCopy(string name, string excludedMaterial)
        {
            string key = name?.Trim() + "|" + excludedMaterial;
            if (LocalCopies.TryGetValue(key, out GameObject cached))
            {
                if (cached != null) return cached;
                if (ReferenceEquals(cached, null)) return null;
            }
            GameObject prefab = Find(name);
            GameObject copy = null;
            if (prefab != null)
            {
                if (_templateRoot == null)
                {
                    // Children of an inactive root never wake up, so the copied ZNetView never registers a ZDO.
                    _templateRoot = new GameObject("Vampirizm effect templates");
                    _templateRoot.SetActive(false);
                    Object.DontDestroyOnLoad(_templateRoot);
                }
                copy = Object.Instantiate(prefab, _templateRoot.transform);
                copy.name = prefab.name;
                foreach (ZNetView view in copy.GetComponentsInChildren<ZNetView>(true)) Object.DestroyImmediate(view);
                if (!string.IsNullOrEmpty(excludedMaterial))
                {
                    foreach (ParticleSystemRenderer renderer in copy.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        if (renderer == null || renderer.gameObject == copy || !UsesMaterial(renderer, excludedMaterial)) continue;
                        Object.DestroyImmediate(renderer.gameObject);
                    }
                }
            }
            LocalCopies[key] = copy;
            return copy;
        }

        private static bool UsesMaterial(Renderer renderer, string keyword)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && material.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // Missing prefabs are remembered as null, so a bad config warns once.
        private static GameObject Find(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) return null;
            if (Prefabs.TryGetValue(name, out GameObject cached))
            {
                if (cached != null) return cached;
                if (ReferenceEquals(cached, null)) return null;
            }
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) Plugin.Warn($"Effect prefab '{name}' not found.");
            Prefabs[name] = prefab;
            return prefab;
        }
    }
}
