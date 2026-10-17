using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Vampirizm.Abilities.Orb;

namespace Vampirizm.Items
{
    // Orb of Vampire: a one-handed item held in the palm. Cloned from the vanilla club only for the equip slot,
    // drop physics and ItemDrop setup; the club model is replaced by a glowing blood orb. Casting is handled by
    // OrbPatches/OrbCaster, never by a vanilla Attack.
    internal static class VampireOrb
    {
        // Stable prefab ID; renaming it would delete crafted orbs from saves.
        internal const string PrefabName = "VampirizmOrb";
        internal const string Station = "piece_magetable";
        private const string BasePrefab = "Club";
        private const string MaterialSource = "SurtlingCore";
        private const float Diameter = 0.2f;
        // Held position relative to the right-hand grip point; tune in-game so the orb rests in the palm.
        private static readonly Vector3 HeldOffset = new Vector3(0f, 0.04f, 0.02f);
        private static readonly Color Emission = new Color(0.85f, 0.04f, 0.06f, 1f);

        internal static void Register() => PrefabManager.OnVanillaPrefabsAvailable += AddItem;

        internal static bool IsOrb(ItemDrop.ItemData item)
            => item?.m_dropPrefab != null && item.m_dropPrefab.name == PrefabName;

        private static void AddItem()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddItem;
            var config = new ItemConfig
            {
                Name = "$item_vampirizm_orb",
                Description = "$item_vampirizm_orb_description",
                CraftingStation = Station,
                RepairStation = Station,
                MinStationLevel = 2,
                Weight = 1.5f,
                Icons = new[] { ItemVisuals.LoadIcon("orb_of_vampire") },
                // Upgrades use vanilla scaling: amountPerLevel * (quality - 1).
                Requirements = new[]
                {
                    new RequirementConfig("Ruby", 10, 5, true),
                    new RequirementConfig("Eitr", 5, 5, true),
                    new RequirementConfig("SurtlingCore", 1, 0, true)
                }
            };
            var item = new CustomItem(PrefabName, BasePrefab, config);
            if (item.ItemDrop == null)
            {
                Plugin.Warn($"Orb of Vampire: base prefab {BasePrefab} is unavailable; the orb is not registered.");
                return;
            }
            ConfigureShared(item.ItemDrop.m_itemData.m_shared);
            BuildModel(item.ItemPrefab);
            ItemManager.Instance.AddItem(item);
        }

        private static void ConfigureShared(ItemDrop.ItemData.SharedData shared)
        {
            shared.m_itemType = ItemDrop.ItemData.ItemType.OneHandedWeapon;
            shared.m_skillType = global::Skills.SkillType.BloodMagic;
            shared.m_maxQuality = OrbRules.MaxQuality;
            shared.m_useDurability = true;
            shared.m_canBeReparied = true;
            shared.m_maxDurability = 200f;
            shared.m_durabilityPerLevel = 50f;
            shared.m_useDurabilityDrain = 1f;
            shared.m_damages = new HitData.DamageTypes();
            shared.m_damagesPerLevel = new HitData.DamageTypes();
            shared.m_attackForce = 0f;
            shared.m_blockPower = 0f;
            shared.m_blockPowerPerLevel = 0f;
            shared.m_deflectionForce = 0f;
            shared.m_toolTier = 0;
            // The attacks only exist so input reaches Humanoid.StartAttack; the eitr value feeds the vanilla tooltip.
            shared.m_attack.m_attackEitr = Plugin.Instance.Settings.OrbBaseEitr.Value;
            shared.m_attack.m_attackStamina = 0f;
            if (string.IsNullOrEmpty(shared.m_secondaryAttack?.m_attackAnimation))
                shared.m_secondaryAttack = shared.m_attack.Clone();
            shared.m_secondaryAttack.m_attackEitr = shared.m_attack.m_attackEitr;
            shared.m_secondaryAttack.m_attackStamina = 0f;
        }

        // Replaces the club meshes and long colliders with a textured, glowing sphere. The orb sits under "attach",
        // which vanilla VisEquipment instantiates in the hand, so the held and dropped item look the same.
        private static void BuildModel(GameObject prefab)
        {
            Material source = FindMaterial(prefab);
            Transform attach = prefab.transform.Find("attach") ?? prefab.transform;
            foreach (LODGroup lod in prefab.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(lod);
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObject owner = renderer.gameObject;
                Object.DestroyImmediate(renderer);
                MeshFilter filter = owner.GetComponent<MeshFilter>();
                if (filter != null) Object.DestroyImmediate(filter);
            }
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.name = "vampirizm_orb";
            sphere.layer = attach.gameObject.layer;
            sphere.transform.SetParent(attach, false);
            sphere.transform.localPosition = HeldOffset;
            float scale = Mathf.Max(0.0001f, attach.lossyScale.x);
            sphere.transform.localScale = Vector3.one * (Diameter / scale);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(source);

            var glow = new GameObject("vampirizm_orb_light") { layer = sphere.layer };
            glow.transform.SetParent(attach, false);
            glow.transform.localPosition = HeldOffset;
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.12f, 0.12f);
            light.range = 2f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            OrbGlow pulse = attach.gameObject.AddComponent<OrbGlow>();
            pulse.Setup(sphere.transform, light);

            // One sphere collider keeps the dropped orb rolling and pickable.
            var drop = prefab.AddComponent<SphereCollider>();
            drop.center = prefab.transform.InverseTransformPoint(sphere.transform.position);
            drop.radius = Diameter * 0.5f / Mathf.Max(0.0001f, prefab.transform.lossyScale.x);
        }

        private static Material FindMaterial(GameObject fallback)
        {
            GameObject core = PrefabManager.Cache.GetPrefab<GameObject>(MaterialSource);
            foreach (GameObject candidate in new[] { core, fallback })
            {
                if (candidate == null) continue;
                foreach (Renderer renderer in candidate.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterial != null) return renderer.sharedMaterial;
            }
            return null;
        }

        // A vanilla material copy keeps the in-game shader and lighting; the swirl texture and red emission make it the orb.
        private static Material CreateMaterial(Material source)
        {
            Material material = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
            material.name = "vampirizm_orb";
            Texture2D texture = ItemVisuals.LoadTexture("tex_orb_of_vampire");
            if (texture != null)
            {
                material.mainTexture = texture;
                material.mainTextureScale = Vector2.one;
                material.mainTextureOffset = Vector2.zero;
                if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", texture);
            }
            if (material.HasProperty("_Color")) material.color = Color.white;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Emission);
            }
            return material;
        }
    }
}
