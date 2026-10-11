# Vampirizm

A vampirism mod for Valheim. Become a vampire, give up food, live on blood, hide from the sun, and level up the **Vampirism** skill to unlock a dash, invisibility and a bat form.

## Features

- **Vampire state and skill.** A new `Vampirism` skill (0–100) that grows by drinking blood. It is saved with the character and synced to other players.
- **Blood instead of food.** Vampires cannot eat food or drink meads. Maximum health, stamina and regeneration come from stored blood. Blood drains slowly over time.
- **Blood HUD.** A `Blood B/Bmax · Vampirism L` bar above the health panel replaces the food icons. It pulses when blood is low and shows "SUN" while you burn.
- **Drinking from items.** Raw meat and blood items can be drunk from the inventory or hotbar.
- **Bite.** Drink blood directly from living creatures with a bare-handed bite. Works in multiplayer.
- **Weaknesses.**
  - Sunlight burns you during the day when you are not under a roof.
  - Silver weapons and spirit damage deal ×2 damage to vampires.
- **Slime protection.** Smear yourself with Ooze to resist the sun for a while. Water and rain wash it off.
- **Conversion.**
  - Bite a willing player to turn them.
  - Use the craftable **Vampire Rune** on a clear night under open sky.
- **Abilities**, unlocked by skill level:
  - **Dash** (level 10): a short, fast burst of movement.
  - **Invisibility** (level 25): mobs and bosses ignore you and other players can't see you.
  - **Bat form** (level 50): fly as a bat and bite from the air.
- **Rebindable controls** on Valheim's Controls tab, plus optional gamepad buttons.
- **English and Russian** localization.
- **Server-synced settings.** Gameplay settings come from the server (via Jotunn), so every player uses the same rules.

## Installation

1. Install [BepInEx 5](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) (tested with 5.4.23.3).
2. Install [Jotunn 2.29.2](https://github.com/Valheim-Modding/Jotunn/releases/tag/v2.29.2).
3. Copy `Vampirizm.dll` into `BepInEx/plugins/Vampirizm/`.

The server and all clients must run the same Vampirizm version, and Jotunn checks this when you connect. The mod has been tested with Valheim 1.0.17.

## How to play

### Becoming a vampire

You can be turned in one of two ways:

- **Vampire Rune.** Craft it at a **Workbench** from `Leech trophy ×1 + Stone ×1 + Ruby ×2`. Use it from the inventory or hotbar **at night, in clear weather (no rain, storm, snow, mist, fog or ash), under open sky** (not under a roof or in a dungeon). If any of these conditions isn't met, you'll see the reason and the rune is not spent.
- **Bite from another vampire.** A vampire can turn you by biting, unless the server has disabled it or you turned off `Accept conversion by bite` in your local config.

After you turn, your food is cleared and you start with 25% of your blood capacity.

### Living on blood

- **No food.** Food and meads are refused and not used up. Bait and other non-food items still work.
- **Blood capacity** is `100 + 2 × skill level`.
- **More blood means more health and stamina:**
  - max HP = base HP + 0.8 × blood
  - max stamina = base stamina + 0.5 × blood
  - Every 10 seconds you regenerate 0.04 × blood HP.
  - Drinking does not heal you instantly.
- **Drain.** You lose 3 blood per minute while playing. At zero blood you keep only your base HP and stamina.
- **Drinking items.** Use raw meat or blood items from the inventory or hotbar. The tooltip shows how much blood each item gives.
  - With a full bar, nothing is consumed.
  - Blood beyond your capacity is lost.
  - Items give no skill experience.
  - You can still put raw meat on a cooking station normally.

| Item | Blood | Item | Blood |
|---|---|---|---|
| Neck tail | 6 | Lox meat | 20 |
| Raw meat (boar) | 10 | Serpent meat | 20 |
| Deer meat | 10 | Asksvin meat | 22 |
| Chicken meat | 10 | Volture meat | 22 |
| Hare meat | 12 | Bloodbag | 25 |
| Wolf meat | 15 | Giant blood sack | 35 |
| Bjorn meat | 18 | | |

Cooked dishes, fish and blood pudding give no blood.

### Biting

To bite, have nothing in your hands (no weapon, shield or torch). Look at a creature within 2.5 m and **hold the secondary attack**.

- **How it works.**
  - While you bite, you hold still and face the victim.
  - Every second the victim takes 6 damage, which armor does not reduce, and you gain 1.5 blood per point of damage.
  - Most donors are held in place while you drink.
- **When it stops.** The bite ends when:
  - you release the button
  - your blood bar fills
  - 8 seconds pass
  - you are staggered or take a big hit
  - the target moves out of range or out of sight
  - either of you dies
  - You then have to wait 4 seconds before biting again.
- **Who you can bite.**
  - Boars, deer, necks, hares, chickens/hens and goblins (Fulings) are donors.
  - Undead, greydwarves, blobs, surtlings, seekers, bosses and tamed creatures are never donors.
  - Goblin Brutes give blood but are too big to hold.
- **Experience.** You get skill experience only from bites, at 0.2 XP per unit of blood that actually entered your bar. A creature stops giving XP once you have drunk more blood from it than its max HP.
- **Players.** Biting a player never gives blood. Instead, it can convert them (see above).

### Weaknesses

- **Sun.**
  - **When it burns:** during the day, outside, not under a roof and not in a dungeon. Clouds and rain don't protect you.
  - **Timing:** you get a warning first, then after 3 seconds you take damage every 2 seconds.
  - **Damage:** 1 + 2% of max HP per tick. Armor, blocking and dodging don't help.
- **Slime.** Use **Ooze** from the inventory to get 300 s of sun protection.
  - Using it again refreshes the timer; it does not stack.
  - Getting wet (rain, swimming) removes it at once, and you can't apply it while wet.
  - It does not survive relogging.
- **Silver and spirit.** Silver weapons and arrows (sword, mace, knife, arrows), and any hit with spirit damage, deal ×2 damage to you. Silver plus spirit together is still ×2, not ×4.

### Abilities

Abilities unlock as your Vampirism skill grows. They work only when you are a vampire and are not in a menu, chat or inventory.

| Ability | Default key | Unlocks at | Cost |
|---|---|---|---|
| Dash | `Left Alt` | level 10 | 5 blood, 2 s cooldown |
| Invisibility | `H` (toggle) | level 25 | 3 blood/min while active |
| Bat form | `B` (toggle) | level 50 | 5 extra blood/min while active |

- **Dash.**
  - You dash up to 8 m in the direction you are moving, or forward if you aren't moving.
  - It stops just before walls, steep slopes and creatures. If there is no room, it doesn't start and costs nothing.
  - You can't dash in the air, in water, while biting, attacking, dodging, sitting, overweight or in build mode.
  - There is no invulnerability during a dash.
- **Invisibility.**
  - Other players can't see you, and mobs and bosses won't target you. You see yourself as semi-transparent.
  - It ends when you attack, use an item, take damage, bite, dash, die or teleport, or when you press the key again.
- **Bat form.**
  - You turn into a bat and fly. Movement keys steer, jump climbs and crouch descends.
  - Items, interaction, building, attacks, parry and dodge are blocked.
  - Secondary attack bites for 20 damage and restores 5 blood per tick.
  - The form ends when you die, teleport, enter water, are staggered, are cured or run out of blood.

Rebind the abilities in **Settings → Controls** (rows named "Vampire: …"). Gamepad buttons can be set only in the config file (see [Controls](#controls)).

### Death and cure

Death keeps your vampire status and blood. If your skill drops on death, your blood is capped to the new capacity. There is no in-game cure yet, only the `vampirizm cure` debug command.

## Commands

Commands are for testing and admins. To use them:

1. Enable the in-game console, e.g. with the `-console` launch option.
2. Run `devcommands`.
3. For any command other than `status`, set `[Debug] Enable commands = true` in the server config (the local config in single-player). You must also be an admin.

The `vampirizm` command is hidden from `help` and autocomplete, and it only ever affects your own character.

| Command | Action |
|---|---|
| `vampirizm status` | Show your blood, capacity and skill level. No admin rights needed. |
| `vampirizm convert` | Turn yourself into a vampire (at least level 1), grant the starting blood and clear current food. Running it again does not refill blood. |
| `vampirizm cure` | Remove vampirism and reset blood to zero. The skill level is kept. |
| `vampirizm blood <amount>` | Add up to `<amount>` blood, up to your capacity. |
| `vampirizm level <0..100>` | Set the Vampirism skill level and reset XP progress for that level. |

Commands that change state are refused if:

- you are not an admin
- debug is off on the server
- your character isn't loaded or is dead
- you run them from a dedicated server console with no character

This is a co-op safeguard. It doesn't stop a modified client.

## Configuration

The config file is `BepInEx/config/com.igentuman.vampirizm.cfg`, and it is created on first launch. Settings marked **server** are admin-only: in multiplayer, the server's values are synced to all clients through Jotunn. Settings marked **local** belong to each player.

Item and creature lists are comma-separated, case-sensitive Valheim prefab IDs.

### `[Debug]`

| Setting | Default | Scope | Description |
|---|---|---|---|
| Enable commands | `false` | server | Allow admins to run the `vampirizm` commands that change state. |

### `[Blood]`

| Setting | Default | Scope | Description |
|---|---|---|---|
| Base capacity | `100` | server | Blood capacity at skill level 0. |
| Capacity per level | `2` | server | Extra capacity per skill level. |
| Initial blood fraction | `0.25` | server | Fraction of capacity given once on conversion. |
| Health per blood | `0.8` | server | Max HP per unit of stored blood. |
| Stamina per blood | `0.5` | server | Max stamina per unit of stored blood. |
| Health regen per blood | `0.04` | server | HP restored every 10 s per unit of stored blood. |
| Drain per minute | `3` | server | Blood lost per minute of play while alive. Scaled by the world food-rate modifier. Not lost while offline. |

### `[Feeding]`

| Setting | Default | Scope | Description |
|---|---|---|---|
| Blood items | see [table](#living-on-blood) | server | `PrefabId:blood` pairs that vampires can drink from the inventory or hotbar. |

### `[Weaknesses]`

| Setting | Default | Scope | Description |
|---|---|---|---|
| Sun damage | `1` | server | Flat damage per sun tick. |
| Sun damage fraction | `0.02` | server | Extra damage per tick as a fraction of max HP. |
| Sun interval | `2` | server | Seconds between sun ticks. |
| Sun warning | `3` | server | Seconds between the warning and the first tick. |
| Slime item | `Ooze` | server | Prefab ID of the sun-protection item. |
| Slime duration | `300` | server | Seconds of protection per application. |
| Slime emote | *(empty)* | server | Vanilla emote to play when applying it, e.g. `flex`. Empty uses the eat animation. |
| Silver items | `SwordSilver, MaceSilver, KnifeSilver, ArrowSilver` | server | Silver weapons and ammo. You could add e.g. `SpearWolfFang`, `FistFenrirClaw` or `BowDraugrFang`. |
| Silver and spirit multiplier | `2` | server | Damage multiplier for silver or spirit hits. It is applied only once. |
| Spirit rule | `WholeHit` | server | `WholeHit`: any spirit damage multiplies the whole hit. `SpiritOnly`: without silver, only the spirit part is multiplied. |

### `[Bite]`

| Setting | Default | Scope | Description |
|---|---|---|---|
| Donors | `Boar, Deer, Deer_White, Neck, Hare, Hen, Chicken, Goblin, GoblinArcher, GoblinShaman, GoblinBrute` | server | Creatures you can drink from. Any creature not listed is forbidden. Bosses and tamed creatures never count. |
| Never donors | greydwarves, skeletons, draugr, ghosts, wraiths, blobs, surtlings, golems, seekers, ticks, deathsquitos | server | Always forbidden, even if also listed in Donors. |
| No hold donors | `GoblinBrute` | server | Donors that give blood but are not held in place. |
| Range | `2.5` | server | Maximum bite distance in meters. |
| Tick interval | `1` | server | Seconds between drain ticks. |
| Damage per tick | `6` | server | Damage dealt to the victim per tick. Armor does not reduce it. |
| Blood per damage | `1.5` | server | Blood gained per point of damage dealt. |
| Experience per blood | `0.2` | server | Skill XP per unit of blood that enters the bar. |
| Max duration | `8` | server | Maximum length of one bite, in seconds. |
| Cooldown | `4` | server | Seconds before the next bite. |
| Emote | *(empty)* | server | Vanilla emote looped while drinking, e.g. `kneel`. Empty uses a short interact animation. |
| Bite converts players | `true` | server | Whether biting a willing player turns them into a vampire. |
| Accept conversion by bite | `true` | **local** | Your consent to being turned by a bite. |

### `[Rune]`

| Setting | Default | Scope | Description |
|---|---|---|---|
| Blocked weather | `Rain, Storm, Snow, Mist, Fog, Ash` | server | Keywords (case-insensitive). If the current environment's name contains one, the rune won't work. Wet weather always blocks it. |
| Requires open sky | `true` | server | Whether the ritual fails under a roof or in a dungeon. |

### `[Abilities]`

| Setting | Default | Scope | Description |
|---|---|---|---|
| Dash level | `10` | server | Skill level that unlocks the dash. |
| Dash blood cost | `5` | server | Blood spent per dash. |
| Dash cooldown | `2` | server | Seconds between dashes. |
| Dash distance | `8` | server | Maximum dash length in meters. |
| Dash speed | `30` | server | Dash speed in m/s. A full 8 m dash takes about 0.27 s. |
| Invisibility level | `25` | server | Skill level that unlocks invisibility. |
| Invisibility blood drain per minute | `3` | server | Blood spent per minute while invisible. |
| Bat form level | `50` | server | Skill level that unlocks bat form. |
| Bat flight speed | `8` | server | Flight speed in m/s. |
| Bat form blood drain per minute | `5` | server | Extra blood lost per minute as a bat. |
| Bat bite damage | `20` | server | Damage per bat bite tick. |
| Blood per bat bite | `5` | server | Blood restored per bat bite tick. |

### `[HUD]`

| Setting | Default | Scope | Description |
|---|---|---|---|
| Offset X | `0` | local | Horizontal offset of the blood bar from the health panel. |
| Offset Y | `8` | local | Vertical offset of the blood bar above the health panel. |
| Width | `220` | local | Blood bar width (80–800). |

### `[Controls]`

| Setting | Default | Scope | Description |
|---|---|---|---|
| Dash key | `LeftAlt` | local | Keyboard or mouse key. Rebinding in the Controls menu saves here, and clearing a key saves `None`. |
| Invisibility key | `H` | local | Same as above. |
| Bat form key | `B` | local | Same as above. |
| Dash / Invisibility / Bat form gamepad button | `None` | local | Gamepad button. It can be set only here, because Valheim cancels rebinding when a gamepad button is pressed. Every gamepad button already has a vanilla action, so pick one you don't use. |

## Development

### Environment

- **.NET SDK 8** (`global.json`). The plugin targets `net462` and uses C# 8.
- **JotunnLib 2.29.2**, pinned in the project and in `packages.lock.json`.
- **Valheim 1.0.17** and **BepInEx 5.4.23.3** are verified for building and loading.
- The game uses the [official Jotunn 2.29.2 release DLL](https://github.com/Valheim-Modding/Jotunn/releases/tag/v2.29.2). The NuGet DLL contains debug components and is not meant for installing into the game.
- The game assemblies (`assembly_valheim`, `assembly_utils`, `assembly_guiutils`, `SoftReferenceableAssets`) and Unity's `netstandard.dll` are referenced from the installed game, not from `publicized_assemblies`, so the build can't compile against stale copies.

Jotunn looks for Valheim in the default Steam directory. To use a different directory, set `VALHEIM_INSTALL`, and if needed `VALHEIM_MANAGED` and `BEPINEX_PATH`, as environment variables or MSBuild properties.

### Build and test

```bash
dotnet restore Vampirizm.sln --locked-mode
dotnet build Vampirizm.sln -c Release --no-restore
dotnet run --project Vampirizm.Tests -c Release
```

The output is `Vampirizm/bin/Release/net462/Vampirizm.dll`. After every Debug or Release build, including builds from Rider, the DLL is copied to `~/.local/share/Steam/steamapps/common/Valheim/BepInEx/plugins/Vampirizm/`. Only the mod DLL is deployed; dependencies are not.

```bash
# Custom deploy directory (or set MOD_DEPLOYPATH)
dotnet build Vampirizm.sln -c Release -p:VampirizmDeployPath=/path/to/Valheim/BepInEx/plugins/Vampirizm
# Build without deploying
dotnet build Vampirizm.sln -c Release -p:DeployVampirizmOnBuild=false
```

`Vampirizm.Tests` is a small executable test suite with no external test framework. It runs without Valheim or Unity and covers:

- conversion, blood overflow and consumption, drain
- blood item and ID list parsing
- silver/spirit multiplier, sun timer and damage
- donor rules, bite blood cap, tick ordering and session length
- ritual conditions, ability thresholds and block reasons, dash obstacle distance
- curing, capacity changes, save locale, migration
- restoring invisibility materials, checked against a Unity API model

These tests do not replace running the game.

### Project layout

| Folder | Contents |
|---|---|
| `Core/` | `VampireController` (per-player state machine), `VampireState`, blood rules |
| `Feeding/` | Blood item table, donor registry, drain rules |
| `Abilities/` | Bite (`DrainAbility`), dash, invisibility (+ visual), bat form, unlock rules |
| `Effects/` | Sunlight and slime protection |
| `Combat/` | Silver registry, hit source tracking, damage multiplier rules |
| `Conversion/` | Conversion and rune ritual rules |
| `Items/` | Vampire Rune prefab and recipe |
| `Networking/` | ZDO state replication, bite/drain RPCs |
| `Persistence/` | Save format |
| `Controls/` | Jotunn key bindings and Controls menu rows |
| `UI/` | Blood HUD bar |
| `Patches/` | Harmony patches (food, damage, HUD, persistence, abilities, bite, bat form, invisibility) |
| `Configuration/`, `Localization/`, `Skills/`, `Commands/` | Config, EN/RU strings, skill registration, console command |
| `Assets/` | Rune icon (`vampire_rune.png`, generated by `generate_rune_icon.py`, embedded in the DLL) |

The plugin installs each Harmony patch class on its own. If one patch fails because a game method signature changed, the error is logged and the other systems still load.

### Technical notes

- **Saving.**
  - `Player.m_customData["com.igentuman.vampirizm.state"]` stores `1|0/1|blood` using the invariant locale. The skill level is saved by vanilla `Skills.Save`.
  - The prototype format `0|True/False|blood` is migrated.
  - Corrupted strings and unknown versions are left untouched, and state changes for that character are blocked with a warning in the log.
  - Regular characters get no state record.
- **Replication.**
  - The owner writes status, blood, level and bat form to the player's ZDO, only when they change. Blood updates about once per second because of drain.
  - Remote clients read this snapshot. For example, the silver multiplier detects a vampire target from its ZDO flag.
- **Bite networking.**
  - The victim's owner coordinates the bite. Every tick, the vampire's client sends the `Vampirizm_Drain` RPC with a session number and tick number.
  - The owner checks the sender, the vampire flag, distance, donor rules, session uniqueness and tick order. It then applies the damage and replies with the blood actually gained.
  - Duplicate and late replies are ignored.
  - The victim's AI is held only while the session is alive. The hold is released on timeout, owner change or death, so a vampire disconnecting doesn't leave the mob frozen.
  - Biting a player sends a one-off `Vampirizm_Bite` request instead.
- **Silver and spirit detection.**
  - `HitData` doesn't record the weapon, so the mod remembers the weapon and ammo while the hit is built (`Attack.DoMeleeAttack/DoAreaAttack`, `Projectile.OnHit/DoAOE`, `Aoe.OnHit`).
  - It applies the multiplier in `Character.Damage` on the attacker's machine, on a copy of the `HitData`.
- **Invisibility visuals.**
  - Invisibility uses the `Custom/Fallen Warrior` shader from the `FallenWarrior` material, loaded via SoftRef.
  - The temporary materials are swapped in only during camera rendering, then the originals are restored.

### In-game test checklist

1. Client loads Jotunn 2.29.2 and Vampirizm 0.1.0 (check the log). Dedicated server: not tested yet.
2. Convert, add blood and set the level. Then save, restart, and compare `status` and the skills menu.
3. Cure, switch character or world, die, and lower capacity. A regular character must eat and have stats normally.
4. Server: commands are refused without admin rights or with debug off, and settings sync.
5. Two clients: ZDO state on a remote player, late join, rejoin.
6. Blood: meat from the inventory and hotbar, a full bar, mead/food/feasts refused, cooking station with meat in hand, HUD position.
7. Sun: dawn and dusk (`skiptime`), roof edges, dungeons, rain and swimming with slime. Silver sword/arrow, spirit weapon, silver + spirit, armor vs. sun.
8. Rune: crafting, and refusals (day, rain, roof, already a vampire). Bite: allowed and banned donors, last HP, releasing the button, target moving away, stagger. Two clients: bite a mob owned by the other client, two vampires on one target, logging out mid-bite, biting a player with and without consent.
9. Bat form and invisibility: toggling, flight and collisions, bat bite, drain, blocked actions, exit conditions, and what a remote or late-joining player sees.
