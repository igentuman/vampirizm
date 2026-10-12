using System.Collections.Generic;
using Vampirizm.Core;

namespace Vampirizm.Networking
{
    internal static class VampireNetwork
    {
        internal const string VampireKey = "com.igentuman.vampirizm.vampire";
        internal const string BloodKey = "com.igentuman.vampirizm.blood";
        internal const string LevelKey = "com.igentuman.vampirizm.level";
        internal const string InvisibleKey = "com.igentuman.vampirizm.invisible";
        internal const string BatFormKey = "com.igentuman.vampirizm.batform";
        internal const string DrinkTargetKey = "com.igentuman.vampirizm.drinktarget";
        private static readonly KeyValuePair<int, int> DrinkTargetHash = ZDO.GetHashZDOID(DrinkTargetKey);

        // Cosmetic only: tells every client whom this vampire is drinking from, for the blood splats and sound.
        internal static void SetDrinkTarget(ZNetView view, ZDOID target)
        {
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            ZDO zdo = view.GetZDO();
            if (zdo.GetZDOID(DrinkTargetHash) != target) zdo.Set(DrinkTargetHash, target);
        }

        internal static ZDOID GetDrinkTarget(ZNetView view)
            => view != null && view.IsValid() ? view.GetZDO().GetZDOID(DrinkTargetHash) : ZDOID.None;

        internal static void SetInvisible(ZNetView view, bool invisible)
        {
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            ZDO zdo = view.GetZDO();
            if (zdo.GetBool(InvisibleKey) != invisible) zdo.Set(InvisibleKey, invisible);
        }

        internal static bool IsInvisible(Player player)
        {
            if (player == null) return false;
            var controller = player.GetComponent<VampireController>();
            if (player == Player.m_localPlayer && controller != null)
                return controller.Invisibility?.Active == true;
            var view = player.GetComponent<ZNetView>();
            return view != null && view.IsValid() && view.GetZDO().GetBool(InvisibleKey);
        }

        // The owning player publishes a passive snapshot through the existing ZDO.
        // This is not an RPC that grants blood or authorizes a feeding session.
        internal static void Publish(ZNetView view, VampireState state, float level)
        {
            if (!view.IsValid() || !view.IsOwner()) return;
            ZDO zdo = view.GetZDO();
            if (zdo.GetBool(VampireKey) != state.IsVampire) zdo.Set(VampireKey, state.IsVampire);
            if (zdo.GetFloat(BloodKey) != state.Blood) zdo.Set(BloodKey, state.Blood);
            if (zdo.GetFloat(LevelKey) != level) zdo.Set(LevelKey, level);
        }

        internal static bool IsVampire(Player player)
        {
            var controller = player.GetComponent<VampireController>();
            if (player == Player.m_localPlayer && controller != null) return controller.State.IsVampire;
            var view = player.GetComponent<ZNetView>();
            return view != null && view.IsValid() && view.GetZDO().GetBool(VampireKey);
        }

        internal static void SetBatForm(ZNetView view, bool active)
        {
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            ZDO zdo = view.GetZDO();
            if (zdo.GetBool(BatFormKey) != active) zdo.Set(BatFormKey, active);
        }

        internal static bool IsBatForm(Player player)
        {
            if (player == null) return false;
            var controller = player.GetComponent<VampireController>();
            if (player == Player.m_localPlayer && controller != null) return controller.BatForm?.Active == true;
            var view = player.GetComponent<ZNetView>();
            return view != null && view.IsValid() && view.GetZDO().GetBool(BatFormKey);
        }
    }
}
