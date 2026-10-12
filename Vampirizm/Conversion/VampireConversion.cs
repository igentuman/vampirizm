using Vampirizm.Core;
using Vampirizm.Effects;
using Vampirizm.Items;

namespace Vampirizm.Conversion
{
    public enum BiteConversionResult { Converted, Refused, Disabled, AlreadyVampire, Unavailable, Rejected }

    // Both ways to become a vampire end in the same VampireController.Convert transition.
    internal static class VampireConversion
    {
        // Rune use from the inventory or hotbar. Returns true when handled; the rune is spent only on success.
        internal static bool TryRitual(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            if (!VampireRune.IsRune(item)) return false;
            var controller = VampireController.Ensure(player);
            var settings = Plugin.Instance.Settings;
            EnvSetup environment = EnvMan.instance != null ? EnvMan.instance.GetCurrentEnvironment() : null;
            RitualResult result = RitualRules.Check(controller.CanChange, controller.State.IsVampire, EnvMan.IsNight(),
                EnvMan.IsWet(), environment?.m_name, RitualRules.SplitKeywords(settings.RuneBlockedWeather.Value),
                settings.RuneRequiresOpenSky.Value, !SunlightController.IsSheltered(player));
            if (result != RitualResult.Ok)
            {
                player.Message(MessageHud.MessageType.Center, Message(result));
                return true;
            }
            // Convert is idempotent, so a repeated request cannot spend a second rune.
            if (!controller.Convert()) return true;
            inventory.RemoveOneItem(item);
            player.GetZAnim().SetTrigger("interact");
            RitualEffect.Broadcast(player);
            player.Message(MessageHud.MessageType.Center, "$vampirizm_converted_ritual");
            return true;
        }

        // Runs on the bitten player's own client after the request was authenticated.
        internal static BiteConversionResult AcceptBite(Player victim, string attackerName)
        {
            var settings = Plugin.Instance.Settings;
            if (!settings.BiteConversion.Value) return BiteConversionResult.Disabled;
            var controller = VampireController.Ensure(victim);
            if (!controller.CanChange) return BiteConversionResult.Unavailable;
            if (controller.State.IsVampire) return BiteConversionResult.AlreadyVampire;
            if (!settings.AcceptBiteConversion.Value) return BiteConversionResult.Refused;
            if (!controller.Convert()) return BiteConversionResult.Unavailable;
            victim.Message(MessageHud.MessageType.Center,
                global::Localization.instance.Localize("$vampirizm_converted_bite", attackerName));
            return BiteConversionResult.Converted;
        }

        private static string Message(RitualResult result)
        {
            switch (result)
            {
                case RitualResult.AlreadyVampire: return "$vampirizm_rune_already";
                case RitualResult.NotNight: return "$vampirizm_rune_not_night";
                case RitualResult.BadWeather: return "$vampirizm_rune_weather";
                case RitualResult.NoOpenSky: return "$vampirizm_rune_sky";
                default: return "$vampirizm_unavailable";
            }
        }
    }
}
