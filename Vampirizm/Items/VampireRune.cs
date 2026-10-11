using System.IO;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Vampirizm.Items
{
    // The vampire rune: a tinted clone of the vanilla stone with an embedded icon.
    internal static class VampireRune
    {
        // Stable prefab ID; renaming it would delete crafted runes from saves.
        internal const string PrefabName = "VampirizmRune";
        private const string IconResource = "Vampirizm.Assets.vampire_rune.png";
        private static readonly Color Tint = new Color(0.35f, 0.05f, 0.07f, 1f);
        private static readonly Color Glow = new Color(0.9f, 0.05f, 0.08f, 1f);

        internal static void Register() => PrefabManager.OnVanillaPrefabsAvailable += AddItem;

        internal static bool IsRune(ItemDrop.ItemData item)
            => item?.m_dropPrefab != null && item.m_dropPrefab.name == PrefabName;

        private static void AddItem()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddItem;
            var config = new ItemConfig
            {
                Name = "$item_vampirizm_rune",
                Description = "$item_vampirizm_rune_description",
                CraftingStation = CraftingStations.Workbench,
                Weight = 1f,
                StackSize = 10,
                Icons = new[] { LoadIcon() },
                Requirements = new[]
                {
                    new RequirementConfig("TrophyLeech", 1, 0, true),
                    new RequirementConfig("Stone", 1, 0, true),
                    new RequirementConfig("Ruby", 2, 0, true)
                }
            };
            var item = new CustomItem(PrefabName, "Stone", config);
            ApplyTint(item.ItemPrefab);
            ItemManager.Instance.AddItem(item);
        }

        private static Sprite LoadIcon()
        {
            using (Stream stream = typeof(VampireRune).Assembly.GetManifestResourceStream(IconResource))
            using (var buffer = new MemoryStream())
            {
                if (stream == null)
                {
                    Plugin.Warn("Vampire rune icon resource is missing.");
                    return null;
                }
                stream.CopyTo(buffer);
                Texture2D texture = AssetUtils.LoadImage(buffer.ToArray());
                texture.name = "vampire_rune";
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }

        // New material instances: the vanilla stone keeps its own look.
        private static void ApplyTint(GameObject prefab)
        {
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    var material = new Material(materials[i]);
                    if (material.HasProperty("_Color")) material.color = Tint;
                    if (material.HasProperty("_EmissionColor"))
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", Glow * 0.6f);
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
        }
    }
}
