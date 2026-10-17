using System.Reflection;
using Jotunn.Managers;
using UnityEngine;

namespace Vampirizm.Items
{
    // Makes a custom food placeable the way vanilla feasts are: the item prefab gets Piece and WearNTear
    // components (ItemDrop.IsPiece needs both) and joins the Serving Tray ("Feaster") piece table, with the food
    // item itself as the single recoverable build resource. Only the tray can remove ItemDrop pieces, so the hammer
    // is not an option. Eating the placed food is routed to blood feeding in FoodPatches.
    internal static class FoodPlacement
    {
        // Vanilla feasts used as a settings template; any of them will do.
        private static readonly string[] Templates = { "FeastMeadows", "FeastBlackforest", "FeastSwamps", "FeastMountains", "FeastPlains", "FeastAshlands", "FeastDeepNorth" };

        // Vanilla Serving Tray tool prefab.
        private const string TrayPrefab = "Feaster";

        internal static void MakePlaceable(GameObject prefab, Sprite icon)
        {
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                Plugin.Warn("Food placement skipped: no ItemDrop on " + (prefab != null ? prefab.name : "null"));
                return;
            }
            // Vanilla foods are already placeable, so a cloned prefab carries the vanilla Piece (icon, name, refund
            // item). Adding a second one would leave GetComponent returning the vanilla data, so reuse it instead.
            Piece piece = prefab.GetComponent<Piece>();
            WearNTear wearNTear = prefab.GetComponent<WearNTear>();
            GameObject template = piece == null || wearNTear == null ? FindTemplate() : null;
            if (template == null && (piece == null || wearNTear == null))
                Plugin.Warn("No vanilla feast template found; placed food uses default piece settings.");

            if (piece == null)
            {
                piece = prefab.AddComponent<Piece>();
                CopyPublicFields(template != null ? template.GetComponent<Piece>() : null, piece);
            }
            piece.m_name = drop.m_itemData.m_shared.m_name;
            piece.m_description = drop.m_itemData.m_shared.m_description;
            piece.m_icon = icon;
            piece.m_enabled = true;
            PieceTable table = FindTrayTable(prefab.name);
            piece.m_category = CategoryFor(table);
            piece.m_comfort = 0;
            piece.m_comfortObject = null;
            piece.m_resources = new[] { new Piece.Requirement { m_resItem = drop, m_amount = 1, m_recover = true } };

            if (wearNTear == null)
            {
                wearNTear = prefab.AddComponent<WearNTear>();
                CopyPublicFields(template != null ? template.GetComponent<WearNTear>() : null, wearNTear);
            }

            if (table != null && !table.m_pieces.Contains(prefab)) table.m_pieces.Add(prefab);
        }

        private static GameObject FindTemplate()
        {
            foreach (string name in Templates)
            {
                GameObject template = PrefabManager.Instance.GetPrefab(name);
                if (template != null && template.GetComponent<Piece>() != null) return template;
            }
            return null;
        }

        private static void CopyPublicFields<T>(T source, T target) where T : Component
        {
            if (source == null || target == null) return;
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
                field.SetValue(target, field.GetValue(source));
        }

        private static PieceTable FindTrayTable(string itemName)
        {
            GameObject tray = PrefabManager.Instance.GetPrefab(TrayPrefab);
            PieceTable table = tray != null ? tray.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces : null;
            if (table == null) Plugin.Warn("Serving tray piece table not found; " + itemName + " cannot be placed.");
            return table;
        }

        // The tray only shows tabs listed in its m_categories, so prefer the food tab and fall back to whatever it has.
        private static Piece.PieceCategory CategoryFor(PieceTable table)
        {
            if (table == null || table.m_categories.Count == 0 || table.m_categories.Contains(Piece.PieceCategory.Food))
                return Piece.PieceCategory.Food;
            if (table.m_categories.Contains(Piece.PieceCategory.Feasts)) return Piece.PieceCategory.Feasts;
            return table.m_categories[0];
        }
    }
}
