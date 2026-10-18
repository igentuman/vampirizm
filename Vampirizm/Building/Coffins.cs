using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Vampirizm.Items;

namespace Vampirizm.Building
{
    internal static class Coffins
    {
        internal static void Register() => PrefabManager.OnVanillaPrefabsAvailable += AddPieces;

        private static void AddPieces()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;
            TryAdd(CoffinRules.WoodenPrefab, "wooden_coffin", CoffinRules.WoodenComfort, CoffinRules.WoodenBlood,
                new RequirementConfig("Wood", 7, 0, true));
            TryAdd(CoffinRules.VampirePrefab, "vampire_coffin", CoffinRules.VampireComfort, CoffinRules.VampireBlood,
                new RequirementConfig("ElderBark", 7, 0, true), new RequirementConfig("Iron", 4, 0, true),
                new RequirementConfig("Chain", 2, 0, true));
        }

        private static void TryAdd(string prefabName, string asset, int comfort, int blood, params RequirementConfig[] requirements)
        {
            try
            {
                Add(prefabName, asset, comfort, blood, requirements);
            }
            catch (Exception exception)
            {
                Plugin.Warn($"Could not register coffin {prefabName}: {exception}");
            }
        }

        private static void Add(string prefabName, string asset, int comfort, int blood, params RequirementConfig[] requirements)
        {
            var custom = new CustomPiece(prefabName, "bed", new PieceConfig
            {
                Name = "$piece_vampirizm_" + asset,
                Description = "$piece_vampirizm_" + asset + "_description",
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "piece_workbench",
                Icon = ItemVisuals.LoadIcon(asset),
                Requirements = requirements
            });
            GameObject prefab = custom.PiecePrefab;
            Piece piece = prefab.GetComponent<Piece>();
            piece.m_comfort = comfort;
            piece.m_comfortGroup = Piece.ComfortGroup.Bed;
            piece.m_comfortObject = null;
            var coffin = prefab.AddComponent<CoffinBed>();
            coffin.Blood = blood;
            try
            {
                CoffinModels.Build(prefab, asset, coffin);
            }
            catch (Exception exception)
            {
                Plugin.Warn($"Using the vanilla bed model for {prefabName}: {exception.Message}");
            }
            Bed bed = prefab.GetComponent<Bed>();
            // The exported coffin points its head end along +Z, opposite the vanilla bed attachment.
            // Turn the attachment around so a sleeper lies in the same direction as the coffin.
            bed.m_spawnPoint.localPosition = new Vector3(0f, .38f, 0f);
            bed.m_spawnPoint.localRotation *= Quaternion.Euler(0f, 180f, 0f);
            PieceManager.Instance.AddPiece(custom);
        }
    }
}
