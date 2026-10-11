using System;
using System.Collections.Generic;

namespace Vampirizm.Core
{
    // Pure parser for comma-separated exact prefab IDs.
    public static class PrefabIdList
    {
        private static readonly char[] Separators = { ',', ';', '\n', '\r' };

        public static HashSet<string> Parse(string raw) => Parse(raw, new List<string>());

        // Rejected entries and duplicates are appended to errors.
        public static HashSet<string> Parse(string raw, List<string> errors)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(raw)) return ids;
            foreach (string entry in raw.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                string id = entry.Trim();
                if (id.Length == 0) continue;
                if (id.IndexOf(' ') >= 0 || id.IndexOf(':') >= 0 || !ids.Add(id)) errors.Add(id);
            }
            return ids;
        }
    }
}
