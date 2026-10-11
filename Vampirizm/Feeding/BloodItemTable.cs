using System;
using System.Collections.Generic;
using System.Globalization;
using Vampirizm.Core;

namespace Vampirizm.Feeding
{
    // Pure parser for "PrefabId:blood" lists. Exact, case-sensitive prefab IDs only.
    public static class BloodItemTable
    {
        public const float MaxAmount = 10000f;
        private static readonly char[] EntrySeparators = { ',', ';', '\n', '\r' };

        public static Dictionary<string, float> Parse(string raw) => Parse(raw, new List<string>());

        // Rejected entries and overridden duplicates are appended to errors.
        public static Dictionary<string, float> Parse(string raw, List<string> errors)
        {
            var table = new Dictionary<string, float>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(raw)) return table;
            foreach (string entry in raw.Split(EntrySeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                string text = entry.Trim();
                if (text.Length == 0) continue;
                int colon = text.IndexOf(':');
                string id = colon > 0 ? text.Substring(0, colon).Trim() : "";
                string number = colon > 0 ? text.Substring(colon + 1).Trim() : "";
                if (id.Length == 0 || id.IndexOf(' ') >= 0 ||
                    !float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float amount) ||
                    !BloodRules.IsFinite(amount) || amount <= 0f)
                {
                    errors.Add(text);
                    continue;
                }
                if (table.ContainsKey(id)) errors.Add(text);
                // The last duplicate wins so a server admin can append an override.
                table[id] = Math.Min(amount, MaxAmount);
            }
            return table;
        }
    }
}
