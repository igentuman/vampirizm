using System.Collections.Generic;
using BepInEx.Configuration;
using Vampirizm.Core;

namespace Vampirizm.Feeding
{
    // Runtime view of the server-synchronized donor lists. Exact prefab IDs only.
    internal static class DonorRegistry
    {
        private static ConfigEntry<string> _allowedSource;
        private static ConfigEntry<string> _deniedSource;
        private static ConfigEntry<string> _noHoldSource;
        private static HashSet<string> _allowed = new HashSet<string>();
        private static HashSet<string> _denied = new HashSet<string>();
        private static HashSet<string> _noHold = new HashSet<string>();

        internal static void Initialize(ConfigEntry<string> allowed, ConfigEntry<string> denied, ConfigEntry<string> noHold)
        {
            _allowedSource = allowed;
            _deniedSource = denied;
            _noHoldSource = noHold;
            allowed.SettingChanged += (_, __) => Rebuild();
            denied.SettingChanged += (_, __) => Rebuild();
            noHold.SettingChanged += (_, __) => Rebuild();
            Rebuild();
        }

        private static void Rebuild()
        {
            var errors = new List<string>();
            _allowed = PrefabIdList.Parse(_allowedSource.Value, errors);
            _denied = PrefabIdList.Parse(_deniedSource.Value, errors);
            _noHold = PrefabIdList.Parse(_noHoldSource.Value, errors);
            if (errors.Count > 0) Plugin.Warn("Donor lists: ignored entries: " + string.Join(", ", errors));
        }

        internal static string PrefabId(Character character) => Utils.GetPrefabName(character.gameObject);

        internal static bool CanFeed(Character character)
            => character != null && !(character is Player) && !character.IsDead() && !character.IsBoss() && !character.IsTamed() &&
               DrainRules.IsDonor(PrefabId(character), _allowed, _denied);

        internal static bool CanHold(Character character) => !_noHold.Contains(PrefabId(character));
    }
}
