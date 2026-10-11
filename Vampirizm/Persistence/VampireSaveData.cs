using System.Collections.Generic;
using System.Globalization;
using Vampirizm.Core;

namespace Vampirizm.Persistence
{
    public enum SaveReadResult { Missing, Loaded, Migrated, Invalid, UnsupportedVersion }

    public static class VampireSaveData
    {
        public const string Key = "com.igentuman.vampirizm.state";
        public const int CurrentVersion = 1;

        // Version 1: version|vampire flag|absolute blood. Level belongs to vanilla Skills.
        public static string Serialize(VampireState state) => "1|" + (state.IsVampire ? "1|" : "0|") +
            state.Blood.ToString("R", CultureInfo.InvariantCulture);

        public static SaveReadResult Read(IDictionary<string, string> data, out VampireState state)
        {
            state = new VampireState();
            if (!data.TryGetValue(Key, out var raw)) return SaveReadResult.Missing;
            if (string.IsNullOrEmpty(raw)) return SaveReadResult.Invalid;
            string[] parts = raw.Split('|');
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int version))
                return SaveReadResult.Invalid;
            if (version > CurrentVersion) return SaveReadResult.UnsupportedVersion;
            // Reserved legacy prototype format: 0|True/False|blood.
            if (parts.Length != 3) return SaveReadResult.Invalid;
            bool vampire;
            if (version == 0)
            {
                if (!bool.TryParse(parts[1], out vampire)) return SaveReadResult.Invalid;
            }
            else
            {
                if (parts[1] != "0" && parts[1] != "1") return SaveReadResult.Invalid;
                vampire = parts[1] == "1";
            }
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float blood) ||
                !BloodRules.IsFinite(blood)) return SaveReadResult.Invalid;
            state = new VampireState(vampire, blood);
            return version == 0 ? SaveReadResult.Migrated : SaveReadResult.Loaded;
        }
    }
}
