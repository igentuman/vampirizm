using System;

namespace Vampirizm.Conversion
{
    public enum RitualResult { Ok, Unavailable, AlreadyVampire, NotNight, BadWeather, NoOpenSky }

    // Pure conditions for the vampire rune ritual: a clear night under the open sky.
    public static class RitualRules
    {
        public static RitualResult Check(bool canChange, bool isVampire, bool isNight, bool wetWeather,
            string environment, string[] blockedKeywords, bool requireOpenSky, bool openSky)
        {
            if (!canChange) return RitualResult.Unavailable;
            if (isVampire) return RitualResult.AlreadyVampire;
            if (!isNight) return RitualResult.NotNight;
            if (wetWeather || IsBlocked(environment, blockedKeywords)) return RitualResult.BadWeather;
            if (requireOpenSky && !openSky) return RitualResult.NoOpenSky;
            return RitualResult.Ok;
        }

        // Environment names are matched by case-insensitive keyword: "ThunderStorm" contains "Storm".
        public static bool IsBlocked(string environment, string[] blockedKeywords)
        {
            if (string.IsNullOrEmpty(environment) || blockedKeywords == null) return false;
            foreach (string keyword in blockedKeywords)
            {
                if (keyword == null) continue;
                string text = keyword.Trim();
                if (text.Length > 0 && environment.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static string[] SplitKeywords(string raw)
            => string.IsNullOrEmpty(raw) ? new string[0] : raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
