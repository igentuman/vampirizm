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
        internal ConfigEntry<float> DeathBloodKept { get; }
        internal ConfigEntry<string> BloodItems { get; }
        internal ConfigEntry<bool> AllowMeads { get; }
        internal ConfigEntry<float> FoodBurnMultiplier { get; }
        internal ConfigEntry<float> BloodFoodDuration { get; }
        internal ConfigEntry<float> XpPerItemBlood { get; }
        internal ConfigEntry<float> SunDamage { get; }
        internal ConfigEntry<float> SunDamageFraction { get; }
        internal ConfigEntry<float> SunInterval { get; }
        internal ConfigEntry<float> SunWarning { get; }
        internal ConfigEntry<float> SunTwilight { get; }
        internal ConfigEntry<float> RainSunMultiplier { get; }
        internal ConfigEntry<float> ThunderstormSunMultiplier { get; }
        internal ConfigEntry<float> SwampSunMultiplier { get; }
        internal ConfigEntry<float> MistlandsSunMultiplier { get; }
        internal ConfigEntry<float> AshlandsSunMultiplier { get; }
        internal ConfigEntry<string> SlimeItem { get; }
        internal ConfigEntry<float> SlimeDuration { get; }
        internal ConfigEntry<string> SlimeEmote { get; }
        internal ConfigEntry<string> SilverItems { get; }
        internal ConfigEntry<float> WeaknessMultiplier { get; }
        internal ConfigEntry<string> SilverContactItems { get; }
        internal ConfigEntry<float> SilverContactDamage { get; }
        internal ConfigEntry<float> SilverContactInterval { get; }
        internal ConfigEntry<SpiritRule> SpiritDamageRule { get; }
        internal ConfigEntry<float> UnarmedDamageMultiplier { get; }
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
        internal ConfigEntry<float> BiteEmoteSpeed { get; }
        internal ConfigEntry<string> BiteSound { get; }
        internal ConfigEntry<string> BiteBloodEffect { get; }
        internal ConfigEntry<bool> BiteConversion { get; }
        internal ConfigEntry<bool> AcceptBiteConversion { get; }
        internal ConfigEntry<string> RuneBlockedWeather { get; }
        internal ConfigEntry<bool> RuneRequiresOpenSky { get; }
        internal ConfigEntry<string> RuneStartEffects { get; }
        internal ConfigEntry<string> RuneStrikeEffects { get; }
        internal ConfigEntry<float> EitrLevel { get; }
        internal ConfigEntry<float> EitrBase { get; }
        internal ConfigEntry<float> EitrPerLevel { get; }
        internal ConfigEntry<float> EitrRegenBase { get; }
        internal ConfigEntry<float> EitrRegenBonus { get; }
        internal ConfigEntry<float> EitrRegenLevels { get; }
        internal ConfigEntry<float> HudOffsetX { get; }
        internal ConfigEntry<float> HudOffsetY { get; }
        internal ConfigEntry<float> DashLevel { get; }
        internal ConfigEntry<float> DashCost { get; }
        internal ConfigEntry<float> DashCooldown { get; }
        internal ConfigEntry<float> DashDistance { get; }
        internal ConfigEntry<float> DashSpeed { get; }
        internal ConfigEntry<float> InvisibilityLevel { get; }
        internal ConfigEntry<float> InvisibilityCost { get; }
        internal ConfigEntry<float> InvisibilityCooldown { get; }
        internal ConfigEntry<float> InvisibilityDrainPerMinute { get; }
        internal ConfigEntry<string> InvisibilityEffect { get; }
        internal ConfigEntry<string> InvisibilitySound { get; }
        internal ConfigEntry<float> BatFormLevel { get; }
        internal ConfigEntry<float> BatFormCost { get; }
        internal ConfigEntry<float> BatFormCooldown { get; }
        internal ConfigEntry<float> BatFormSpeed { get; }
        internal ConfigEntry<float> BatSprintMultiplier { get; }
        internal ConfigEntry<float> BatFormDrainPerMinute { get; }
        internal ConfigEntry<string> BatTransformSounds { get; }
        internal ConfigEntry<string> BatNoiseSounds { get; }
        internal ConfigEntry<float> BatNoiseInterval { get; }
        internal ConfigEntry<string> BatFlapSounds { get; }
        internal ConfigEntry<float> BatFlapInterval { get; }
        internal ConfigEntry<float> BatBiteDamage { get; }
        internal ConfigEntry<float> BatBiteBlood { get; }
        internal ConfigEntry<string> FriendlyCreatures { get; }
        internal ConfigEntry<float> OrbBaseEitr { get; }
        internal ConfigEntry<float> OrbSkillEitrReduction { get; }
        internal ConfigEntry<int> OrbBaseBats { get; }
        internal ConfigEntry<int> OrbBatsPerLevel { get; }
        internal ConfigEntry<float> OrbBatDamage { get; }
        internal ConfigEntry<float> OrbBiteInterval { get; }
        internal ConfigEntry<float> OrbSwarmDuration { get; }
        internal ConfigEntry<float> OrbShieldDuration { get; }
        internal ConfigEntry<float> OrbShieldPerLevel { get; }
        internal ConfigEntry<float> OrbBatHealth { get; }
        internal ConfigEntry<float> OrbConeAngle { get; }
        internal ConfigEntry<float> OrbRange { get; }
        internal ConfigEntry<float> OrbBloodPerBat { get; }
        internal ConfigEntry<float> OrbShieldAbsorb { get; }
        internal ConfigEntry<float> OrbCastDelay { get; }
        internal ConfigEntry<float> OrbSkillPerCast { get; }
        internal ConfigEntry<float> OrbSkillPerBite { get; }
        internal ConfigEntry<bool> OrbTargetBosses { get; }

        internal VampireConfig(ConfigFile config)
        {
            DebugCommands = config.Bind("Debug", "Enable commands", false,
                new ConfigDescription("Allow admins to change their own vampirism, blood and skill for testing.",
                    null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            BaseCapacity = Bind(config, "Base capacity", 100f, 1f, 10000f, "Blood capacity at skill level zero.");
            CapacityPerLevel = Bind(config, "Capacity per level", 2f, 0f, 1000f, "Additional blood capacity per skill level.");
            InitialBloodFraction = Bind(config, "Initial blood fraction", 0.25f, 0f, 1f, "Fraction of capacity granted once on conversion.");
            HealthPerBlood = Bind(config, "Health per blood", 0.8f, 0f, 100f,
                "Maximum health added per unit of stored blood. Added on top of food health.");
            StaminaPerBlood = Bind(config, "Stamina per blood", 0.5f, 0f, 100f,
                "Maximum stamina added per unit of stored blood. Added on top of food stamina.");
            RegenPerBlood = Bind(config, "Health regen per blood", 0.04f, 0f, 100f,
                "Health restored every 10 seconds per unit of stored blood. Added on top of food regeneration.");
            DrainPerMinute = Bind(config, "Drain per minute", 3f, 0f, 1000f,
                "Blood lost per minute of active play while alive. Scaled by the world food rate.");
            DeathBloodKept = Bind(config, "Blood kept on death", 0.5f, 0f, 1f,
                "Fraction of stored blood kept when a vampire dies. 0.5 halves the blood, 1 keeps all of it.");
            BloodItems = AdminString(config, "Feeding", "Blood items", DefaultBloodItems,
                "Comma-separated PrefabId:blood pairs that a vampire can drink from the inventory or hotbar. " +
                "One item is consumed per use; blood above capacity is lost. Bloody stew always grants 35 blood independently of this list.");
            AllowMeads = config.Bind("Feeding", "Allow meads", true,
                new ConfigDescription("Vampires can drink meads and other consumables that only apply a status effect. Food is always refused.",
                    null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            FoodBurnMultiplier = Bind(config, "Feeding", "Food burn multiplier", 2f, 1f, 10f,
                "How many times faster food bar entries run out for a vampire. 2 halves durations except Bloody stew, which keeps its own timer.");
            BloodFoodDuration = Bind(config, "Feeding", "Blood item food time", 1200f, 10f, 7200f,
                "Food timer in seconds that a drunk blood item puts in the food list, before the burn multiplier. " +
                "The same item can be drunk again once half of it has burnt, like vanilla food. The entry adds no stats. Bloody stew uses 1500 seconds instead.");
            XpPerItemBlood = Bind(config, "Feeding", "Experience per blood", 2f, 0f, 100f,
                "Vampirism skill experience per unit of item blood that entered a non-full bar.");
            SunDamage = Weakness(config, "Sun damage", 2f, 0f, 1000f,
                "Flat damage per sunlight tick. Armor and resistances do not reduce it.");
            SunDamageFraction = Weakness(config, "Sun damage fraction", 0.02f, 0f, 1f,
                "Additional damage per sunlight tick as a fraction of maximum health.");
            SunInterval = Weakness(config, "Sun interval", 2f, 0.25f, 60f, "Seconds between sunlight ticks.");
            SunWarning = Weakness(config, "Sun warning", 3f, 0f, 60f, "Seconds between the warning and the first sunlight tick.");
            SunTwilight = Weakness(config, "Sun twilight", 60f, 0f, 600f,
                "Real seconds after sunrise and before sunset when the sun does not burn yet.");
            RainSunMultiplier = Weakness(config, "Rain sun multiplier", 0.5f, 0f, 10f,
                "Sunlight damage multiplier in wet weather. Zero disables burning.");
            ThunderstormSunMultiplier = Weakness(config, "Thunderstorm sun multiplier", 0f, 0f, 10f,
                "Sunlight damage multiplier during a thunderstorm; replaces the rain multiplier. Zero disables burning.");
            SwampSunMultiplier = Weakness(config, "Swamp sun multiplier", 0f, 0f, 10f,
                "Sunlight damage multiplier in the Swamp. Combined with the weather multiplier. Zero disables burning.");
            MistlandsSunMultiplier = Weakness(config, "Mistlands sun multiplier", 0f, 0f, 10f,
                "Sunlight damage multiplier in the Mistlands. Combined with the weather multiplier. Zero disables burning.");
            AshlandsSunMultiplier = Weakness(config, "Ashlands sun multiplier", 2f, 0f, 10f,
                "Sunlight damage multiplier in the Ashlands. Combined with the weather multiplier. Zero disables burning.");
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
            SilverContactItems = AdminString(config, "Weaknesses", "Silver contact items",
                "Silver, SilverOre, SilverNecklace, SwordSilver, MaceSilver, KnifeSilver, ArrowSilver, ShieldSilver",
                "Comma-separated prefab IDs of items that burn a vampire while carried anywhere in the inventory.");
            SilverContactDamage = Weakness(config, "Silver contact damage", 5f, 0f, 1000f,
                "Damage per silver contact tick for each inventory stack of silver. Armor does not reduce it. Zero disables it.");
            SilverContactInterval = Weakness(config, "Silver contact interval", 1f, 0.25f, 60f,
                "Seconds between silver contact ticks.");
            Donors = AdminString(config, "Bite", "Donors",
                "Boar, Deer, Deer_White, Neck, Hare, Hen, Chicken, Wolf, Wolf_cub, Bjorn, Lox, Lox_Calf, Goblin, GoblinArcher, GoblinShaman, GoblinBrute",
                "Exact creature prefab IDs a vampire can drink from. Unknown creatures are forbidden. Bosses and tamed creatures never qualify.");
            NeverDonors = AdminString(config, "Bite", "Never donors",
                "Greydwarf, Greydwarf_Elite, Greydwarf_Shaman, Greyling, Skeleton, Skeleton_Poison, Draugr, Draugr_Elite, " +
                "Draugr_Ranged, Unbjorn, Ghost, Wraith, Blob, BlobElite, Surtling, StoneGolem, Seeker, SeekerBrute, SeekerBrood, Tick, Deathsquito",
                "Creatures that are never donors, even if also listed in Donors.");
            NoHoldDonors = AdminString(config, "Bite", "No hold donors", "Bjorn, Lox, GoblinBrute",
                "Donors that can be drunk from but are too large to immobilize.");
            BiteRange = Bind(config, "Bite", "Range", 2.5f, 1f, 6f, "Maximum distance to the bite target.");
            BiteInterval = Bind(config, "Bite", "Tick interval", 1f, 0.25f, 5f, "Seconds between confirmed drain ticks.");
            BiteDamage = Bind(config, "Bite", "Damage per tick", 6f, 0.1f, 1000f, "Damage dealt to the victim per tick; ignores armor.");
            BloodPerDamage = Bind(config, "Bite", "Blood per damage", 1.5f, 0f, 100f, "Blood gained per point of damage actually dealt.");
            XpPerBlood = Bind(config, "Bite", "Experience per blood", 4f, 0f, 100f,
                "Vampirism skill experience per unit of bitten blood that entered a non-full bar.");
            BiteMaxDuration = Bind(config, "Bite", "Max duration", 2f, 1f, 60f, "Length of one feeding session in seconds.");
            BiteCooldown = Bind(config, "Bite", "Cooldown", 4f, 0f, 120f, "Seconds after a session before the next bite.");
            BiteEmote = AdminString(config, "Bite", "Emote", "cry",
                "Vanilla emote played while drinking (for example: cry, kneel). Empty plays a short interact animation.");
            BiteEmoteSpeed = Bind(config, "Bite", "Emote speed", 0f, 0f, 2f,
                "Playback speed of the drinking emote. 0 stretches the whole emote over the session length.");
            BiteSound = AdminString(config, "Bite", "Sound", "sfx_leech_attack",
                "Vanilla sound prefab played when drinking starts and on every tick interval. Empty disables it.");
            BiteBloodEffect = AdminString(config, "Bite", "Blood effect", "vfx_player_hit",
                "Vanilla effect prefab spawned as small blood splats while drinking. Empty disables it.");
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
            RuneStartEffects = AdminString(config, "Rune", "Start effects", "fx_GP_Activation",
                "Comma-separated vanilla effect prefabs spawned when the ritual begins. Empty disables them.");
            RuneStrikeEffects = AdminString(config, "Rune", "Strike effects", "fx_Lightning_red, sfx_thunder",
                "Comma-separated vanilla effect prefabs spawned at the ritual's climax. Empty disables them.");
            EitrLevel = Bind(config, "Eitr", "Unlock level", 50f, 0f, 100f, "Vampirism skill level that grants a passive eitr bar.");
            EitrBase = Bind(config, "Eitr", "Base eitr", 50f, 0f, 1000f, "Maximum eitr at the unlock level.");
            EitrPerLevel = Bind(config, "Eitr", "Eitr per level", 5f, 0f, 100f, "Additional maximum eitr per skill level above the unlock level.");
            EitrRegenBase = Bind(config, "Eitr", "Base regen", 1f, 0f, 100f,
                "Eitr restored per second at the unlock level. Replaces the vanilla rate; armor and status effect bonuses still apply.");
            EitrRegenBonus = Bind(config, "Eitr", "Regen bonus", 1f, 0f, 100f, "Additional eitr per second for every regen step.");
            EitrRegenLevels = Bind(config, "Eitr", "Levels per regen bonus", 5f, 1f, 100f, "Skill levels above the unlock level per regen step.");
            HudOffsetX = config.Bind("HUD", "Offset X", 0f, "Blood bar horizontal offset from its spot right of the health bar and forsaken power.");
            HudOffsetY = config.Bind("HUD", "Offset Y", 0f, "Blood bar vertical offset from the bottom of the health bar.");
            UnarmedDamageMultiplier = Bind(config, "Abilities", "Unarmed damage multiplier", 5f, 1f, 100f,
                "Bare-handed damage multiplier at Vampirism skill 100. Scales linearly from 1 at skill 0.");
            DashLevel = Bind(config, "Abilities", "Dash level", 10f, 0f, 100f, "Vampirism skill level that unlocks the dash.");
            DashCost = Bind(config, "Abilities", "Dash blood cost", 5f, 0f, 1000f, "Blood spent per dash.");
            DashCooldown = Bind(config, "Abilities", "Dash cooldown", 2f, 0f, 60f, "Seconds between dashes.");
            DashDistance = Bind(config, "Abilities", "Dash distance", 10f, 1f, 20f,
                "Maximum dash length in meters. The dash stops before walls, steep slopes and creatures on its path.");
            DashSpeed = Bind(config, "Abilities", "Dash speed", 40f, 5f, 80f,
                "Base dash speed in meters per second, applied at 2x speed. The default 40 gives 80 m/s and a 10 m dash takes about 0.125 seconds.");
            InvisibilityLevel = Bind(config, "Abilities", "Invisibility level", 25f, 0f, 100f, "Vampirism skill level that unlocks invisibility.");
            InvisibilityCost = Bind(config, "Abilities", "Invisibility blood cost", 5f, 0f, 1000f, "Blood spent when activating invisibility.");
            InvisibilityCooldown = Bind(config, "Abilities", "Invisibility cooldown", 2f, 0f, 60f, "Seconds after invisibility ends before it can be activated again.");
            InvisibilityDrainPerMinute = Bind(config, "Abilities", "Invisibility blood drain per minute", 15f, 0f, 1000f,
                "Blood continuously spent while invisibility is active.");
            InvisibilityEffect = AdminString(config, "Abilities", "Invisibility effect", "vfx_odin_despawn",
                "Vanilla effect prefab puffed when the vampire fades or reappears. Empty disables it.");
            InvisibilitySound = AdminString(config, "Abilities", "Invisibility sound", "sfx_ghost_death",
                "Vanilla sound prefab played when the vampire fades or reappears. Empty disables it.");
            BatFormLevel = Bind(config, "Abilities", "Bat form level", 50f, 0f, 100f, "Vampirism skill level that unlocks the bat form.");
            BatFormCost = Bind(config, "Abilities", "Bat form blood cost", 5f, 0f, 1000f, "Blood spent when entering bat form.");
            BatFormCooldown = Bind(config, "Abilities", "Bat form cooldown", 2f, 0f, 60f, "Seconds after bat form ends before it can be activated again.");
            BatFormSpeed = Bind(config, "Abilities", "Bat flight speed", 8f, 1f, 30f, "Horizontal and vertical flight speed in meters per second.");
            BatSprintMultiplier = Bind(config, "Abilities", "Bat sprint speed multiplier", 2f, 1f, 5f,
                "Flight speed multiplier while the sprint key is held.");
            BatFormDrainPerMinute = Bind(config, "Abilities", "Bat form blood drain per minute", 20f, 0f, 1000f,
                "Additional blood lost per minute while flying as a bat.");
            BatTransformSounds = AdminString(config, "Abilities", "Bat transform sounds", "sfx_raven_poof, sfx_bat_alerted",
                "Comma-separated vanilla sound prefabs all played when turning into a bat or back. Empty disables them.");
            BatNoiseSounds = AdminString(config, "Abilities", "Bat noise sounds", "sfx_bat_idle",
                "Comma-separated vanilla sound prefabs; one is picked at random for each bat squeak. Empty disables them.");
            BatNoiseInterval = Bind(config, "Abilities", "Bat noise interval", 6f, 1f, 120f,
                "Average seconds between bat squeaks while flying. Each gap is randomized between half and one and a half times this.");
            BatFlapSounds = AdminString(config, "Abilities", "Bat flap sounds", "sfx_raven_flap",
                "Comma-separated vanilla sound prefabs; one is picked at random for each wing flap. Empty disables them.");
            BatFlapInterval = Bind(config, "Abilities", "Bat flap interval", 2.5f, 0.25f, 60f,
                "Average seconds between wing flap sounds while flying. Randomized like the noise interval.");
            BatBiteDamage = Bind(config, "Abilities", "Bat bite damage", 20f, 0.1f, 1000f,
                "Damage requested for each successful bat bite tick.");
            BatBiteBlood = Bind(config, "Abilities", "Blood per bat bite", 5f, 0f, 1000f,
                "Blood restored for each successful bat bite tick, up to remaining capacity.");
            FriendlyCreatures = AdminString(config, "Creatures", "Friendly to vampires", "Bat",
                "Comma-separated creature prefab IDs that never attack vampires. Vampires can still attack them.");
            OrbBaseEitr = Bind(config, "Orb", "Base eitr", 55f, 0f, 1000f,
                "Eitr per orb cast (swarm and shield) before the Blood magic skill reduction.");
            OrbSkillEitrReduction = Bind(config, "Orb", "Skill eitr reduction", 0.33f, 0f, 1f,
                "Fraction of the eitr cost removed at Blood magic 100, like vanilla staffs. Scales linearly with the skill.");
            OrbBaseBats = BindInt(config, "Orb", "Base bats", 7, 1, 50, "Bats summoned by a base quality orb, for both swarm and shield.");
            OrbBatsPerLevel = BindInt(config, "Orb", "Bats per level", 2, 0, 20, "Additional bats per orb upgrade.");
            OrbBatDamage = Bind(config, "Orb", "Bat damage", 7f, 0f, 1000f, "Pierce damage of one bat bite.");
            OrbBiteInterval = Bind(config, "Orb", "Bite interval", 3f, 0.5f, 60f, "Seconds between dives of one bat.");
            OrbSwarmDuration = Bind(config, "Orb", "Swarm duration", 15f, 1f, 300f,
                "Seconds the swarm attacks before it returns with blood or leaves.");
            OrbShieldDuration = Bind(config, "Orb", "Shield duration", 200f, 1f, 3600f, "Seconds a base quality shield lasts.");
            OrbShieldPerLevel = Bind(config, "Orb", "Shield duration per level", 25f, 0f, 3600f, "Additional shield seconds per orb upgrade.");
            OrbBatHealth = Bind(config, "Orb", "Bat health", 15f, 1f, 10000f, "Health of one summoned bat.");
            OrbConeAngle = Bind(config, "Orb", "Cone angle", 30f, 1f, 180f, "Full aiming cone angle in degrees for the swarm target.");
            OrbRange = Bind(config, "Orb", "Range", 45f, 2f, 100f, "Maximum swarm target distance in meters.");
            OrbBloodPerBat = Bind(config, "Orb", "Blood per bat", 3f, 0f, 1000f,
                "Blood each surviving bat brings back from a donor creature.");
            OrbShieldAbsorb = Bind(config, "Orb", "Shield absorb", 1f, 0f, 1f,
                "Fraction of a creature hit taken by the nearest shield bat instead of the player. One bat absorbs at most once per 0.5 seconds.");
            OrbCastDelay = Bind(config, "Orb", "Cast delay", 0.35f, 0f, 2f, "Seconds from the cast animation start until the bats leave the orb.");
            OrbSkillPerCast = Bind(config, "Orb", "Skill per cast", 1f, 0f, 100f, "Blood magic experience per orb cast.");
            OrbSkillPerBite = Bind(config, "Orb", "Skill per bite", 0.25f, 0f, 100f, "Blood magic experience per bat bite.");
            OrbTargetBosses = config.Bind("Orb", "Target bosses", true,
                new ConfigDescription("The swarm may be cast at bosses.", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }

        internal float Capacity(float level) => BloodRules.Capacity(level, BaseCapacity.Value, CapacityPerLevel.Value);

        internal float MaxEitr(float level) => EitrRules.MaxEitr(level, EitrLevel.Value, EitrBase.Value, EitrPerLevel.Value);

        internal float EitrRegen(float level)
            => EitrRules.Regen(level, EitrLevel.Value, EitrRegenBase.Value, EitrRegenBonus.Value, EitrRegenLevels.Value);

        private static ConfigEntry<float> Bind(ConfigFile config, string key, float value, float min, float max, string description)
            => Bind(config, "Blood", key, value, min, max, description);

        private static ConfigEntry<float> Weakness(ConfigFile config, string key, float value, float min, float max, string description)
            => Bind(config, "Weaknesses", key, value, min, max, description);

        private static ConfigEntry<float> Bind(ConfigFile config, string section, string key, float value, float min, float max, string description)
        {
            return config.Bind(section, key, value, new ConfigDescription(description,
                new AcceptableValueRange<float>(min, max), new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }

        private static ConfigEntry<int> BindInt(ConfigFile config, string section, string key, int value, int min, int max, string description)
        {
            return config.Bind(section, key, value, new ConfigDescription(description,
                new AcceptableValueRange<int>(min, max), new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }

        private static ConfigEntry<string> AdminString(ConfigFile config, string section, string key, string value, string description)
        {
            return config.Bind(section, key, value, new ConfigDescription(description,
                null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }
    }
}
