using System.Collections.Generic;
using BepInEx.Configuration;
using Vampirizm.Core;
using Vampirizm.Networking;

namespace Vampirizm.Combat
{
    // Creatures that never treat a vampire as an enemy. Exact prefab IDs only.
    internal static class FriendlyRegistry
    {
        private static ConfigEntry<string> _source;
        private static HashSet<string> _ids = new HashSet<string>();

        internal static void Initialize(ConfigEntry<string> source)
        {
            _source = source;
            _source.SettingChanged += (_, __) => Rebuild();
            Rebuild();
        }

        private static void Rebuild()
        {
            var errors = new List<string>();
            _ids = PrefabIdList.Parse(_source.Value, errors);
            if (errors.Count > 0) Plugin.Warn("Friendly creatures: ignored entries: " + string.Join(", ", errors));
        }

        // One-way: the creature leaves the vampire alone, the vampire may still attack it.
        internal static bool IsFriendlyTo(Character creature, Character target)
            => _ids.Count > 0 && target is Player player && !(creature is Player) &&
               VampireNetwork.IsVampire(player) && _ids.Contains(Utils.GetPrefabName(creature.gameObject));
    }
}
