using BepInEx.Configuration;
using Jotunn.Utils;
using Vampirizm.Combat;
using Vampirizm.Core;

namespace Vampirizm.Configuration
{
    internal sealed class VampireConfig
    {
        // Verified against the Valheim 1.0.17 item prefabs. Unknown IDs are simply never matched.
        internal const string DefaultBloodItems =
            "RawMeat:10, DeerMeat:10, NeckTail:6, WolfMeat:15, LoxMeat:20, SerpentMeat:20, ChickenMeat:10, " +
            "HareMeat:12, BjornMeat:18, AsksvinMeat:22, VoltureMeat:22, " +
            "Bloodbag:25, GiantBloodSack:35";

        internal ConfigEntry<bool> DebugCommands { get; }
        internal ConfigEntry<float> BaseCapacity { get; }
        internal ConfigEntry<float> CapacityPerLevel { get; }
        internal ConfigEntry<float> InitialBloodFraction { get; }
        internal ConfigEntry<float> HealthPerBlood { get; }
        internal ConfigEntry<float> StaminaPerBlood { get; }
        internal ConfigEntry<float> RegenPerBlood { get; }
        internal ConfigEntry<float> DrainPerMinute { get; }
        internal ConfigEntry<string> BloodItems { get; }
        internal ConfigEntry<float> SunDamage { get; }
        internal ConfigEntry<float> SunDamageFraction { get; }
        internal ConfigEntry<float> SunInterval { get; }
        internal ConfigEntry<float> SunWarning { get; }
        internal ConfigEntry<string> SlimeItem { get; }
        internal ConfigEntry<float> SlimeDuration { get; }
        internal ConfigEntry<string> SlimeEmote { get; }
        internal ConfigEntry<string> SilverItems { get; }
        internal ConfigEntry<float> WeaknessMultiplier { get; }
        internal ConfigEntry<SpiritRule> SpiritDamageRule { get; }
        internal ConfigEntry<string> Donors { get; }
        internal ConfigEntry<string> NeverDonors { get; }
        internal ConfigEntry<string> NoHoldDonors { get; }
        internal ConfigEntry<float> BiteRange { get; }
        internal ConfigEntry<float> BiteInterval { get; }
        internal ConfigEntry<float> BiteDamage { get; }
        internal ConfigEntry<float> BloodPerDamage { get; }
        internal ConfigEntry<float> XpPerBlood { get; }
        internal ConfigEntry<float> BiteMaxDuration { get; }
        internal ConfigEntry<float> BiteCooldown { get; }
        internal ConfigEntry<string> BiteEmote { get; }
        internal ConfigEntry<bool> BiteConversion { get; }
        internal ConfigEntry<bool> AcceptBiteConversion { get; }
        internal ConfigEntry<string> RuneBlockedWeather { get; }
        internal ConfigEntry<bool> RuneRequiresOpenSky { get; }
        internal ConfigEntry<float> HudOffsetX { get; }
        internal ConfigEntry<float> HudOffsetY { get; }
        internal ConfigEntry<float> HudWidth { get; }
        internal ConfigEntry<float> DashLevel { get; }
        internal ConfigEntry<float> DashCost { get; }
        internal ConfigEntry<float> DashCooldown { get; }
        internal ConfigEntry<float> DashDistance { get; }
        internal ConfigEntry<float> DashSpeed { get; }
        internal ConfigEntry<float> InvisibilityLevel { get; }
        internal ConfigEntry<float> InvisibilityDrainPerMinute { get; }
        internal ConfigEntry<float> BatFormLevel { get; }
        internal ConfigEntry<float> BatFormSpeed { get; }
        internal ConfigEntry<float> BatFormDrainPerMinute { get; }
        internal ConfigEntry<float> BatBiteDamage { get; }
        internal ConfigEntry<float> BatBiteBlood { get; }

        internal VampireConfig(ConfigFile config)
        {
            DebugCommands = config.Bind("Debug", "Enable commands", false,
                new ConfigDescription("Allow admins to change their own vampirism, blood and skill for testing.",
                    null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            BaseCapacity = Bind(config, "Base capacity", 100f, 1f, 10000f, "Blood capacity at skill level zero.");
            CapacityPerLevel = Bind(config, "Capacity per level", 2f, 0f, 1000f, "Additional blood capacity per skill level.");
            InitialBloodFraction = Bind(config, "Initial blood fraction", 0.25f, 0f, 1f, "Fraction of capacity granted once on conversion.");
            HealthPerBlood = Bind(config, "Health per blood", 0.8f, 0f, 100f,
                "Maximum health added per unit of stored blood. Replaces food health for vampires.");
            StaminaPerBlood = Bind(config, "Stamina per blood", 0.5f, 0f, 100f,
                "Maximum stamina added per unit of stored blood. Replaces food stamina for vampires.");
            RegenPerBlood = Bind(config, "Health regen per blood", 0.04f, 0f, 100f,
                "Health restored every 10 seconds per unit of stored blood. Replaces food regeneration.");
            DrainPerMinute = Bind(config, "Drain per minute", 3f, 0f, 1000f,
                "Blood lost per minute of active play while alive. Scaled by the world food rate.");
            BloodItems = AdminString(config, "Feeding", "Blood items", DefaultBloodItems,
                "Comma-separated PrefabId:blood pairs that a vampire can drink from the inventory or hotbar. " +
                "One item is consumed per use; blood above capacity is lost.");
            SunDamage = Weakness(config, "Sun damage", 1f, 0f, 1000f,
                "Flat damage per sunlight tick. Armor and resistances do not reduce it.");
            SunDamageFraction = Weakness(config, "Sun damage fraction", 0.02f, 0f, 1f,
                "Additional damage per sunlight tick as a fraction of maximum health.");
            SunInterval = Weakness(config, "Sun interval", 2f, 0.25f, 60f, "Seconds between sunlight ticks.");
            SunWarning = Weakness(config, "Sun warning", 3f, 0f, 60f, "Seconds between the warning and the first sunlight tick.");
            SlimeItem = AdminString(config, "Weaknesses", "Slime item", "Ooze", "Prefab ID of the item applied as sun protection.");
            SlimeDuration = Weakness(config, "Slime duration", 300f, 1f, 7200f,
                "Seconds of sun protection per application. Reapplying refreshes, never stacks. Any Wet effect removes it.");
            SlimeEmote = AdminString(config, "Weaknesses", "Slime emote", "",
                "Vanilla emote played when applying slime (for example: flex, cheer). Empty plays the eat animation.");
            SilverItems = AdminString(config, "Weaknesses", "Silver items", "SwordSilver, MaceSilver, KnifeSilver, ArrowSilver",
                "Comma-separated prefab IDs of silver weapons and ammunition.");
            WeaknessMultiplier = Weakness(config, "Silver and spirit multiplier", 2f, 1f, 100f,
                "Damage multiplier for silver or spirit hits on a vampire. Applied once; silver plus spirit does not stack.");
            SpiritDamageRule = config.Bind("Weaknesses", "Spirit rule", SpiritRule.WholeHit,
                new ConfigDescription("WholeHit: any spirit damage multiplies the whole hit. SpiritOnly: without silver only the spirit part is multiplied.",
                    null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Donors = AdminString(config, "Bite", "Donors",
                "Boar, Deer, Deer_White, Neck, Hare, Hen, Chicken, Goblin, GoblinArcher, GoblinShaman, GoblinBrute",
                "Exact creature prefab IDs a vampire can drink from. Unknown creatures are forbidden. Bosses and tamed creatures never qualify.");
            NeverDonors = AdminString(config, "Bite", "Never donors",
                "Greydwarf, Greydwarf_Elite, Greydwarf_Shaman, Greyling, Skeleton, Skeleton_Poison, Draugr, Draugr_Elite, " +
                "Draugr_Ranged, Ghost, Wraith, Blob, BlobElite, Surtling, StoneGolem, Seeker, SeekerBrute, SeekerBrood, Tick, Deathsquito",
                "Creatures that are never donors, even if also listed in Donors.");
            NoHoldDonors = AdminString(config, "Bite", "No hold donors", "GoblinBrute",
                "Donors that can be drunk from but are too large to immobilize.");
            BiteRange = Bind(config, "Bite", "Range", 2.5f, 1f, 6f, "Maximum distance to the bite target.");
            BiteInterval = Bind(config, "Bite", "Tick interval", 1f, 0.25f, 5f, "Seconds between confirmed drain ticks.");
            BiteDamage = Bind(config, "Bite", "Damage per tick", 6f, 0.1f, 1000f, "Damage dealt to the victim per tick; ignores armor.");
            BloodPerDamage = Bind(config, "Bite", "Blood per damage", 1.5f, 0f, 100f, "Blood gained per point of damage actually dealt.");
            XpPerBlood = Bind(config, "Bite", "Experience per blood", 0.2f, 0f, 100f,
                "Vampirism skill experience per unit of blood that entered a non-full bar. Inventory items never give experience.");
            BiteMaxDuration = Bind(config, "Bite", "Max duration", 8f, 1f, 60f, "Maximum length of one feeding session.");
            BiteCooldown = Bind(config, "Bite", "Cooldown", 4f, 0f, 120f, "Seconds after a session before the next bite.");
            BiteEmote = AdminString(config, "Bite", "Emote", "",
                "Vanilla emote looped while drinking (for example: kneel). Empty plays a short interact animation.");
            BiteConversion = config.Bind("Bite", "Bite converts players", true,
                new ConfigDescription("A vampire bite turns a willing human player into a vampire.",
                    null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            AcceptBiteConversion = config.Bind("Bite", "Accept conversion by bite", true,
                "Local consent: allow other vampires to turn this player by biting.");
            RuneBlockedWeather = AdminString(config, "Rune", "Blocked weather",
                "Rain, Storm, Snow, Mist, Fog, Ash",
                "Case-insensitive keywords; an environment whose name contains one is not a clear night. Wet weather always blocks.");
            RuneRequiresOpenSky = config.Bind("Rune", "Requires open sky", true,
                new ConfigDescription("The ritual fails under a roof or inside a dungeon.",
                    null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            HudOffsetX = config.Bind("HUD", "Offset X", 0f, "Blood bar horizontal offset from the health panel.");
            HudOffsetY = config.Bind("HUD", "Offset Y", 8f, "Blood bar vertical offset above the health panel.");
            HudWidth = config.Bind("HUD", "Width", 220f, new ConfigDescription("Blood bar width.",
                new AcceptableValueRange<float>(80f, 800f)));
            DashLevel = Bind(config, "Abilities", "Dash level", 10f, 0f, 100f, "Vampirism skill level that unlocks the dash.");
            DashCost = Bind(config, "Abilities", "Dash blood cost", 5f, 0f, 1000f, "Blood spent per dash.");
            DashCooldown = Bind(config, "Abilities", "Dash cooldown", 2f, 0f, 60f, "Seconds between dashes.");
            DashDistance = Bind(config, "Abilities", "Dash distance", 8f, 1f, 20f,
                "Maximum dash length in meters. The dash stops before walls, steep slopes and creatures on its path.");
            DashSpeed = Bind(config, "Abilities", "Dash speed", 30f, 5f, 60f,
                "Dash speed in meters per second. At 30 m/s the default 6 m dash takes about 0.2 seconds.");
            InvisibilityLevel = Bind(config, "Abilities", "Invisibility level", 25f, 0f, 100f, "Vampirism skill level that unlocks invisibility.");
            InvisibilityDrainPerMinute = Bind(config, "Abilities", "Invisibility blood drain per minute", 3f, 0f, 1000f,
                "Blood continuously spent while invisibility is active.");
            BatFormLevel = Bind(config, "Abilities", "Bat form level", 50f, 0f, 100f, "Vampirism skill level that unlocks the bat form.");
            BatFormSpeed = Bind(config, "Abilities", "Bat flight speed", 8f, 1f, 30f, "Horizontal and vertical flight speed in meters per second.");
            BatFormDrainPerMinute = Bind(config, "Abilities", "Bat form blood drain per minute", 5f, 0f, 1000f,
                "Additional blood lost per minute while flying as a bat.");
            BatBiteDamage = Bind(config, "Abilities", "Bat bite damage", 20f, 0.1f, 1000f,
                "Damage requested for each successful bat bite tick.");
            BatBiteBlood = Bind(config, "Abilities", "Blood per bat bite", 5f, 0f, 1000f,
                "Blood restored for each successful bat bite tick, up to remaining capacity.");
        }

        internal float Capacity(float level) => BloodRules.Capacity(level, BaseCapacity.Value, CapacityPerLevel.Value);

        private static ConfigEntry<float> Bind(ConfigFile config, string key, float value, float min, float max, string description)
            => Bind(config, "Blood", key, value, min, max, description);

        private static ConfigEntry<float> Weakness(ConfigFile config, string key, float value, float min, float max, string description)
            => Bind(config, "Weaknesses", key, value, min, max, description);

        private static ConfigEntry<float> Bind(ConfigFile config, string section, string key, float value, float min, float max, string description)
        {
            return config.Bind(section, key, value, new ConfigDescription(description,
                new AcceptableValueRange<float>(min, max), new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }

        private static ConfigEntry<string> AdminString(ConfigFile config, string section, string key, string value, string description)
        {
            return config.Bind(section, key, value, new ConfigDescription(description,
                null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }
    }
}
