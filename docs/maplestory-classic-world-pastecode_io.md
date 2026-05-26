# MapleStory Classic World — Datamined Metadata Analysis

> **Source:** WZ metadata dump (3,859 files, 416 MB across 46 WZ archives)  
> **Binary context:** `MapleStory_0_zucx.exe` v1.1.0.0, Themida-protected, signed April 6, 2026  
> **Status:** Unreleased — likely Closed Beta or internal QA build

---

## Executive Summary

MapleStory Classic World is **dramatically more restrictive than anyone expected**. This is not a "v62" or "v83" recreation. It's closer to **Beta MapleStory / GMS v.0.0x–v.28** — the earliest era of the game, rebuilt on the modern engine. The data confirms:

- **Only 4 Explorer classes** (Warrior, Magician, Bowman, Thief) — **no Pirates**
- **Only 1st and 2nd job advancement** — no 3rd job, no 4th job
- **Only Victoria Island + Maple Island** — no Ossyria, no El Nath, no Ludibrium, no Orbis
- **91 monsters total** (live MapleStory has thousands)
- **304 maps total** (live has tens of thousands)
- **188 NPCs** — classic Victoria Island cast
- **Redesigned scroll system** (Lesser/Intermediate/Greater/Chaos tiers)
- **Zero cubes, zero starforce** in the consumables data
- **Free Market is IN** with 11 channels
- **Kerning PQ confirmed** (1st Accompaniment maps present)
- **Brand new exclusive content:** "Shallow Passage" and "Deep Passage" areas with new monsters not from any prior version

---

## 1. File Inventory

### WZ Archives (46 total)

| Category | Archives | Files | Size | Description |
|---|---|---:|---:|---|
| **Equipment** | Cap, Cape, Coat, Glove, Longcoat, Pants, Shoes, Shield, Weapon, Accessory, Ring | 1,301 | 130 MB | Armor, weapons, accessories |
| **Character** | Character, Hair, Face, Morph | 953 | 47 MB | Character appearance data |
| **Maps** | Map, Map0, Map9 | 311 | 205 MB | All game maps with objects/portals |
| **Monsters** | Mob, MobSkill | 103 | 1.7 MB | Monster definitions and skills |
| **NPCs** | Npc | 188 | 1.1 MB | NPC data |
| **Items** | Consume, Item, Install, Etc | 147 | 20 MB | Consumables, scrolls, etc items, game config |
| **Skills** | Skill | 23 | 2.6 MB | Job skill data |
| **Quests** | Quest, QuestData | 183 | 1.9 MB | Quest definitions |
| **Strings** | String | 26 | 4.8 MB | All text labels (names, descriptions) |
| **Audio/Visual** | Sound, Effect, Afterimage, Obj, Tile, Back | 109 | 5.2 MB | Sound references, visual effects |
| **UI/System** | UI, Special, WorldMap, Base, Canvas | 141 | 17 MB | UI elements, world map, base config |
| **Pets** | Pet, PetEquip, TamingMob | 47 | 2.5 MB | Pet system data |

---

## 2. Classes & Skills

### Job System: Explorers Only, Up to 2nd Job

| Class | 1st Job | 2nd Job Branches | 3rd Job | 4th Job |
|---|---|---|---|---|
| **Warrior** | ✅ Warrior | ✅ Fighter, Page, Spearman | ❌ | ❌ |
| **Magician** | ✅ Magician | ✅ F/P Wizard, I/L Wizard, Cleric | ❌ | ❌ |
| **Bowman** | ✅ Bowman | ✅ Hunter, Crossbowman | ❌ | ❌ |
| **Thief** | ✅ Thief | ✅ Assassin, Bandit | ❌ | ❌ |
| **Pirate** | ❌ | ❌ | ❌ | ❌ |

**What's NOT here:**
- ❌ No Pirates at all (no Brawler, no Gunslinger, no Corsair, no Buccaneer)
- ❌ No 3rd job advancement (no Crusader, no Hermit, no Ranger, no Priest, no Dragon Knight)
- ❌ No 4th job (no Hero, no Paladin, no Night Lord, no Bishop)
- ❌ No Cygnus Knights, no Aran, no Evan, no Dual Blade, no Phantom
- ❌ No 5th job, no 6th job

### Full Skill List

**Beginner (3 skills):**
- Three Snails, Recovery, Nimble Feet

**Warrior 1st Job (6 skills):**
- Improved HP Recovery, Max HP Increase, Precise Strikes, Iron Body, Power Strike, Slash Blast

**Fighter (8 skills):** Sword Mastery, Axe Mastery, Final Attack: Sword/Axe, Sword/Axe Booster, Rage, Power Guard

**Page (8 skills):** Sword/Blunt Weapon Mastery, Final Attack: Sword/Blunt, Sword/Blunt Booster, Threaten, Power Guard

**Spearman (8 skills):** Spear/Polearm Mastery, Final Attack: Spear/Polearm, Spear/Polearm Booster, Iron Will, Hyper Body

**Magician 1st Job (6 skills):** Improved MP Recovery, Max MP Increase, Magic Guard, Magic Armor, Energy Bolt, Magic Claw

**F/P Wizard (6 skills):** MP Eater, Meditation, Teleport, Slow, Fire Arrow, Poison Breath

**I/L Wizard (5 skills):** MP Eater, Meditation, Teleport, Slow, Cold Beam, Thunder Bolt

**Cleric (5 skills):** MP Eater, Teleport, Heal, Invincible, Bless, Holy Arrow

**Bowman 1st Job (6 skills):** Critical Shot, Amazon's Judgement, The Eye of Amazon, Focus, Arrow Blow, Double Shot

**Hunter (6 skills):** Bow Mastery, Final Attack: Bow, Bow Booster, Power Knockback, Soul Arrow: Bow, Arrow Bomb: Bow

**Crossbowman (5 skills):** Crossbow Mastery, Final Attack: Crossbow, Crossbow Booster, Power Knockback, Soul Arrow: Crossbow, Iron Arrow: Crossbow

**Thief 1st Job (6 skills):** Nimble Body, Keen Eyes, Disorder, Dark Sight, Double Stab, Lucky Seven

**Assassin (5 skills):** Claw Mastery, Critical Throw, Critical Recovery, Claw Booster, Haste, Drain

**Bandit (6 skills):** Dagger Mastery, Nimble Recovery, Dagger Booster, Haste, Steal, Savage Blow

---

## 3. World & Maps

### Geography: Victoria Island Only

| Region | Maps | Areas |
|---|---:|---:|
| **Maple Island** | 25 | Maple Road, Rainbow Street (Amherst) |
| **Victoria Island** | 240 | 9 areas (see below) |
| **Other (Instances)** | 24 | Hidden Streets, Free Market, Job Advancement |
| **Event** | 15 | Physical Fitness Test, test maps |
| **Total** | **304** | |

### Victoria Island Areas

| Area | Maps | Key Locations |
|---|---:|---|
| **Victoria Road** | 100 | Lith Harbor, Henesys, Ellinia, Kerning City, Perion + all field maps |
| **Dungeon** | 41 | Sleepywood, Ant Tunnel, Evil Eye Cave, Drake area, Cursed Sanctuary |
| **Hidden Street** | 42 | Pig Beach, Blue Mushroom Forest, Forest of Evil, Monkey Swamp |
| **Warning Street** | 16 | Swamp of Despair, Burnt Land, Deep Valley |
| **Shallow Passage** | 12 | ⭐ **NEW** — Forgotten Hollow, Cave Fairy Sanctuary, Primeval Forest |
| **Deep Passage** | 5 | ⭐ **NEW** — Perilous Descent, Decayed Tunnel, Bygone Days |
| **Line 3 Construction** | 10 | B1-B3 subway dungeon |
| **Kerning Subway** | 8 | Line 1 & Line 2 areas |
| **Florina Road** | 6 | Florina Beach, Lorang/Clang area |

### What's NOT Here
- ❌ **No Ossyria** (no Orbis, no El Nath, no Aqua Road, no Ludibrium, no Omega Sector)
- ❌ **No Leafre** (no Temple of Time)
- ❌ **No Masteria** (no NLC, no Haunted House)
- ❌ **No Mu Lung/Herb Town**
- ❌ **No Edelstein/Resistance areas**
- ❌ **No Arcane River, no Grandis**

---

## 4. Monsters

### 91 Total Monsters — All Victoria Island Era

**Regular Mobs (IDs 1-63):**

| Level Range | Monsters |
|---|---|
| Tutorial/Low | Snail, Blue Snail, Red Snail, Shroom, Slime, Pig, Ribbon Pig, Orange Mushroom, Stump |
| Low-Mid | Green Mushroom, Dark Stump, Axe Stump, Blue Mushroom, Horny Mushroom, Octopus, Bubbling |
| Mid | Stirge, Jr. Necki, Dark Axe Stump, Zombie Mushroom, Wild Boar, Evil Eye, Iron Hog |
| Mid-High | Ligator, Fire Boar, Curse Eye, Jr. Wraith, Jr. Boogie 1/2, Lupin, Cold Eye, Zombie Lupin |
| High | Nependeath, Copper Drake, Tortie, Dark Nependeath, Wraith, Clang, Drake, Croco, Malady |
| End-game | Stone Golem, Dark Stone Golem, Red Drake, Wild Kargo, Tauromacis, Taurospear |

**⭐ NEW Monsters (not in any prior version):**
| ID | Name | Likely Location |
|---|---|---|
| 54 | Glowshroom | Shallow/Deep Passage |
| 55 | Raffle | Shallow/Deep Passage |
| 56 | Golden Stirge | Shallow/Deep Passage |
| 57 | Aqumander | Shallow/Deep Passage |
| 58 | Echopus | Shallow/Deep Passage |
| 59 | Rafflesia | Shallow/Deep Passage |
| 60 | Duskmander | Shallow/Deep Passage |
| 61 | Myewood | Shallow/Deep Passage |
| 62 | Rotten Mushroom | Shallow/Deep Passage |
| 63 | Sporewood | Shallow/Deep Passage |

**Field Bosses:**
| ID | Name | Notes |
|---|---|---|
| 700000 | Mushmom | Classic field boss |
| 700001 | Jr. Balrog | Sleepywood mini-boss |
| 700002 | Zombie Mushmom | Ant Tunnel variant |
| 700003 | Rotten Mushmom | ⭐ NEW — Classic World exclusive? |
| 700004 | Mano | Rare snail boss |

**"Super" Mobs (IDs 90-93):** Super Jr. Necki, Super Slime, Super Ribbon-Pig, Super Stirge — likely PQ or event mobs

### What's NOT Here
- ❌ No Zakum (boss entry in BossParty.img but no map data — remnant from live client)
- ❌ No Horntail, no Pink Bean, no Papulatus
- ❌ No Balrog (full), no Ergoth
- ❌ No El Nath mobs (Lycanthrope, Yeti, etc.)
- ❌ No Ludibrium mobs (Toy Trojan, Teddies, etc.)

---

## 5. Items & Economy

### Scrolling System — REDESIGNED

The classic 10%/60%/100% scroll system has been **completely replaced** with a 4-tier system:

| Tier | Name | Success Rate | Effect | Notes |
|---|---|---:|---|---|
| **Lesser** | e.g. "Hat Accuracy Scroll: Lesser" | 100% | +1 stat | Always succeeds, lowest bonus |
| **Intermediate** | "Hat Accuracy Scroll: Intermediate" | 60% | +2 stat | Medium risk/reward |
| **Greater** | "Hat Accuracy Scroll: Greater" | 10% | +3 stat | High risk, high reward |
| **Chaos** | "Hat Accuracy Scroll: Chaos" | 10% | +5 stat | Highest reward, **cursed: 100** (destroys on fail) |

**Key observation:** Lesser scrolls link to `itemID1`, `itemID2`, `itemID3` — pointing to the Intermediate, Greater, and Chaos versions. This suggests a **scroll upgrade/fusion system** where you can combine Lesser scrolls into higher tiers.

**216 total scrolls** covering: Hats, Earrings, Topwear, Overall Armor, Bottomwear, Shoes, Gloves, Shields, Capes, 1H Swords, 1H Axes, 1H Blunts, Daggers, Wands, Staves, 2H Swords/Axes/Blunts, Spears, Polearms, Bows, Crossbows, Claws, Pet Equipment

### What's NOT in Consumables
- ❌ **No Cubes** — zero cube items in the entire consumable database
- ❌ **No Starforce items** — no Star Enhancement scrolls
- ❌ **No Flames / Bonus Stats items**
- ❌ **No Nebulites**
- ❌ **No Spell Traces**

### Potions — Classic Set Only
Red Potion, Orange Potion, White Potion, Blue Potion, Elixir, Power Elixir, Mana Elixir, Warrior/Magic/Sniper/Dexterity/Speed Potions

### Throwing Stars
Subi, Wolbi, Mokbi, Kumbi, Tobi, Ilbi, Hwabi — the classic star progression

### Free Market
**Confirmed present** — Free Market Entrance + 11 channels (FM 1-11)

---

## 6. New Exclusive Content

### Shallow Passage (12 maps)
A completely new underground area accessible from Sleepywood. Contains:
- **Forgotten Hollow** — hub town with Cave Fairy NPCs
- **Cave Fairy Sanctuary** — services/shops
- **Cave Fairy Department Store**
- **Primeval Forest I-III** — field maps
- **Forgotten Entrance I-III** — dungeon maps
- **The End of Fleeting Light** — deep area
- **Forgotten Dungeon Tunnel**
- **Forbidden Entrance** — deepest point, connects to Deep Passage

### Deep Passage (5 maps)
The "endgame" area beyond Shallow Passage:
- **Perilous Descent I-III** — vertical descent maps
- **Decayed Tunnel I** — tunnel area
- **Bygone Days** — the final map (name suggests a nostalgia theme)

### New NPCs in These Areas
- **Zelya, Myen, Myra** — Cave Fairy NPCs
- **Luma** (Grocer), **Riel, Roel, Rael** — shopkeepers
- **Cave Fairy's Storage** — storage NPC
- **Nyroth** (Arcforger) — crafting NPC
- **Chrishrama** (Arcforger), **Eurek the Alchemist** — Sleepywood crafting NPCs
- **Pison** (Tour Guide) — navigation NPC

### New Monsters
10 completely new monsters (IDs 54-63): Glowshroom, Raffle, Golden Stirge, Aqumander, Echopus, Rafflesia, Duskmander, Myewood, Rotten Mushroom, Sporewood

Plus a new field boss: **Rotten Mushmom** (ID 700003)

---

## 7. Modern Remnants (Live Client Artifacts)

The metadata contains numerous artifacts from the modern live client that are **NOT active in Classic World** but exist because Classic World shares the modern client codebase:

### BossParty.img — 36 Boss Entries (Remnants)
Zakum, Horntail, Hilla, Pierre, Von Bon, Crimson Queen, Vellum, Von Leon, Arkarium, and more are defined in the boss party system. However, **none of their maps exist** in the Classic World map data — these are data remnants carried over from the shared codebase.

### SpecialServerInfo.img — World Type Definitions
Contains definitions for: **Reboot** ("Heroic World"), **Burning World**, **Challenge World**, **Yeti x Pink Bean World**, and **PL_SP** (Private Server?). There is **no "Classic" entry** in this file — confirming Classic World is handled differently (likely via the `classic` flag found in the binary).

### potentialCostTable.img — Potential System Data
Full potential cost tables exist (Rare/Epic/Unique/Legendary tiers, meso costs by level). However, since there are **zero cubes** in the consumable data, the potential system appears to be inactive/disabled. The data is a remnant.

### CraftRecipe.img (954 KB), RuneStone.img (4.1 MB)
Modern crafting recipes and Rune Stone data are present but likely non-functional — crafting stations exist as NPC data (Weaponcrafting, Sewing, Woodworking, Leatherworking, Arcane) but these may be simplified versions.

### DamageSkin.img, ForceAtomInfo.img
Modern damage skin and force atom (projectile) systems are present as data but likely only used for the classic-compatible subset.

---

## 8. Community Wishlist Cross-Reference

The MapleStory community has been vocal about what they want (and don't want) in Classic World. Sources include Reddit, Steam forums, MMOBomb, DigitalTQ playtest coverage, maplestoryclassicworld.com community analyses, and MapleCon 2025 press coverage. Here's how the datamined reality compares.

### ✅ Confirmed — Community Gets What They Want

| Community Want | Status | Evidence | Community Context |
|---|---|---|---|
| Manual AP allocation | ✅ **CONFIRMED** | Binary: `SID_MSCW_ASK_STATUP_AP_AMOUNT` | Universally requested. Community also wants HP Washing removed — Nexon playtests showed higher Mage HP than expected, suggesting rebalanced HP formulas |
| Original 4 Explorers | ✅ **CONFIRMED** | Only Warrior/Mage/Bowman/Thief skill data | Exact match to community expectation for launch |
| No Pirates at launch | ✅ **CONFIRMED** | No job 500+ skills, no Knuckle/Gun weapons | Divisive — purists happy, pragmatists wanted them. MMOBomb: "Pirates should be in from the beginning. They're an Explorer." Expected in a later phase |
| No 5th/6th job | ✅ **CONFIRMED** | Zero 5th/6th job skill data in WZ files | Unanimously rejected by community. Binary has 6th job strings but these are engine remnants, not active content |
| No Starforce | ✅ **CONFIRMED** | Zero starforce items in consumables | Unanimously rejected |
| No Potential/Cubes | ✅ **CONFIRMED** | Zero cube items; `potentialCostTable.img` exists but is an unused remnant | Unanimously rejected. Community's #1 anti-P2W demand |
| No Flames/Bonus Stats | ✅ **CONFIRMED** | No flame items found | Unanimously rejected — "never in early GMS" |
| No Link Skills | ✅ **CONFIRMED** | Binary: `linkSkillBlock` flag active for Classic | Unanimously rejected as modern alt-army mechanic |
| No Union/Legion | ✅ **CONFIRMED** | Binary: `unionBlock` flag active for Classic | Unanimously rejected |
| Free Market | ✅ **CONFIRMED** | FM Entrance + 11 channels in map data | Strong community demand for player-driven economy over Auction House |
| Kerning PQ | ✅ **CONFIRMED** | "1st Accompaniment" maps (7 stages) present | #1 most-requested content category. Community wants instanced PQs (not channel-camping) |
| Victoria Island maps | ✅ **CONFIRMED** | Pig Beach, Ant Tunnel I-IV, Evil Eye Cave, Sleepywood, all classic towns | Deeply nostalgic training spots confirmed |
| Level 200 cap | ✅ **LIKELY** | Beta scoped for levels 1-200 per Nexon | Community expects 200 cap, wants it to feel monumental again |
| No Arcane River/Grandis | ✅ **CONFIRMED** | Zero post-Victoria content in map data | Unanimously rejected |
| No Cygnus Knights/Aran/Evan | ✅ **CONFIRMED** | Only Explorer skill files exist | Community sees non-Explorer classes as post-classic |

### ⚠️ Modified — Present But Different Than Expected

| Community Want | Status | Evidence | Community Context |
|---|---|---|---|
| Classic 10%/60%/100% scrolls | ⚠️ **REDESIGNED** | New Lesser (100%)/Intermediate (60%)/Greater (10%)/Chaos (10%, cursed) tiers with fusion mechanic | Community nostalgic for old scrolling RNG. The new system preserves the risk/reward element but adds an upgrade path (fuse Lesser → Intermediate → Greater). The Chaos tier's `cursed: 100` means guaranteed item destruction on failure — preserving the classic gamble. **This will be controversial** — purists want the exact old system |
| No P2W Cash Shop | ⚠️ **UNKNOWN** | Cash Shop item data not in WZ metadata dump | Community's **#1 fear**. Significant portion would prefer $10-15/mo subscription over F2P. Unanimous rejection of: Gachapon, 2x EXP coupons, AP Reset scrolls, Vicious Hammer, Marvel Machine. "The target audience isn't children. We're 'old heads' with disposable income." |
| Phased content rollout | ⚠️ **STRONGLY IMPLIED** | Only 2nd job + Victoria Island = must be Phase 1 of many | Community wanted phased approach (WoW Classic style). Nexon's "Origins, Evolution, Beyond" pillars confirm this. But starting at **2nd job** is earlier than most expected — community assumed 3rd or 4th job at launch |
| EXP curve (old rates) | ⚠️ **UNKNOWN** | Binary loads EXP table from WZ via `expTableNo` — actual values not in this dump | Most heated community debate. Purists want 1x; moderates want slight boost ("Us OGs have lives now"). DigitalTQ playtests suggest original EXP tables. Binary has `ExpBuffRate`, `PlusExpRate`, and `playtime_initial_exprate` (fatigue system?) |

### ❌ Missing — Community Wants It But It's Not Here (Yet)

| Community Want | Status | Evidence | Community Context |
|---|---|---|---|
| Up to 3rd job at launch | ❌ **ONLY 2ND JOB** | No 3rd job skill files (no Crusader, Hermit, Ranger, Priest, etc.) | Most speculation centered on v62 (3rd job cap) or v83 (4th job). 2nd job cap is earlier than almost anyone expected |
| Up to 4th job eventually | ❌ **NOT YET** | No 4th job skill files | Majority wants 4th job in a later phase. Small purist minority wants permanent 3rd job cap at level 120 |
| Zakum | ❌ **NOT PLAYABLE** | Boss entry in `BossParty.img` but no El Nath maps | Universally expected as endgame. Will require Ossyria expansion |
| Horntail | ❌ **NOT PLAYABLE** | Boss entry exists but no Leafre maps | Expected as ultimate endgame in later phase |
| Ossyria (El Nath, Ludi, Orbis) | ❌ **NOT PRESENT** | Zero Ossyria maps in data | Expected in Phase 2/3 |
| Ludibrium PQ, Orbis PQ | ❌ **NOT PRESENT** | No Ludibrium/Orbis maps | Expected with Ossyria expansion |
| Balanced classes | ❌ **UNKNOWN** | Skill data exists but damage values need deeper analysis | Strong demand to buff Bowmen, address Bishop being valued only for Holy Symbol, tune Night Lord damage, rework Chief Bandit meso mechanics |
| Anti-bot/anti-cheat | ✅ **INFRASTRUCTURE EXISTS** | Binary: XignCode3, lie detector, GM stalker mode, anti-macro timers | Community's #2 fear after P2W. Infrastructure is present in the binary |
| Leeching changes | ❓ **UNKNOWN** | No EXP sharing rules visible in metadata | Extremely divisive. Steam thread OP suggests EXP restriction based on level difference (15-20 level range) |

### 🆕 Surprises — Nobody Asked For This, But It's Here

| Finding | Evidence | Community Impact |
|---|---|---|
| **10 brand-new monsters** | Glowshroom, Raffle, Golden Stirge, Aqumander, Echopus, Rafflesia, Duskmander, Myewood, Rotten Mushroom, Sporewood | "Classic Plus" confirmed — not a museum piece restoration. Community generally open to tasteful new content |
| **17 new maps** (Shallow/Deep Passage) | Forgotten Hollow, Cave Fairy Sanctuary, Primeval Forest, Perilous Descent, Bygone Days | Entirely new underground area from Sleepywood. Extends endgame beyond what existed in early MapleStory |
| **Rotten Mushmom** (new field boss) | ID 700003 in mob data | New Classic World exclusive boss |
| **Arcforger crafting system** | NPCs: Nyroth (Arcforger), Chrishrama (Arcforger), Eurek the Alchemist | New crafting system — nobody asked for this but it could fill the endgame gear progression gap |
| **Scroll fusion mechanic** | Lesser scrolls link to `itemID1/2/3` pointing to higher tiers | New progression mechanic within the scrolling system — combine lower scrolls into higher tiers |

### Sources

| Source | URL |
|---|---|
| MMOBomb: Everything We Know | https://www.mmobomb.com/everything-know-about-maplestory-classic-world-thus-far |
| MMOBomb: 13 Changes | https://www.mmobomb.com/13-things-i-would-change-maplestory-classic-world |
| MapleCon 2025 (ANN) | https://www.animenewsnetwork.com/press-release/2025-10-31 |
| DigitalTQ: Playtest Update | https://www.digitaltq.com/maplestory-classic-playtest-update |
| DigitalTQ: 2nd Job Playtest | https://www.digitaltq.com/maplestory-classic-playtest-second-job |
| Steam Suggestions Thread | https://steamcommunity.com/app/216150/discussions/0/600782655868412260 |
| MassivelyOP Coverage | https://massivelyop.com/2025/08/18/maplestory-plans-a-public-test-server |
| MSCW Community: QoL | https://www.maplestoryclassicworld.com/news/updates/quality-of-life-maplestory-classic-world |
| MSCW Community: Skills | https://www.maplestoryclassicworld.com/news/updates/skill-balancing-insights |
| MSCW Community: F2P | https://www.maplestoryclassicworld.com/news/updates/free-to-play-perk-or-problem |

---

## 9. Key Surprises & Observations

### 1. This is Earlier Than Anyone Expected
Most community speculation centered on v62 (3rd job cap) or v83 (4th job). This data suggests Classic World launches at roughly **v.0.28 era** — pre-Ossyria, pre-3rd job, pre-Pirates. This is "Maple Island + Victoria Island and nothing else" era.

### 2. The Scroll System is Completely New
The old 10%/60%/100% scroll system that players are nostalgic for has been **redesigned**. The new Lesser/Intermediate/Greater/Chaos tier system with the `itemID1/2/3` linking suggests a scroll fusion/upgrade mechanic. The Chaos tier's `cursed: 100` means guaranteed destruction on failure — preserving the gambling element but with a new framework.

### 3. "Classic Plus" is Real
The 10 new monsters and 17 new maps (Shallow/Deep Passage) confirm this isn't a pure restoration — it's "Classic Plus" with original content designed to extend endgame beyond what existed in early MapleStory.

### 4. Crafting Infrastructure Exists
Cave Fairy NPCs include an "Arcforger" (Nyroth) and Sleepywood has "Chrishrama" (Arcforger) and "Eurek the Alchemist." These suggest a new crafting system specific to Classic World — possibly for the Shallow/Deep Passage gear.

### 5. Content Will Be Phased
Starting at 2nd job cap with Victoria Island only strongly implies a **phased content release** plan — 3rd job + Ossyria in a later patch, 4th job even later. This mirrors how the original game grew and how WoW Classic operated.

### 6. "Rotten Mushmom" is the Top Boss
With no Zakum or Horntail maps, the highest-tier boss appears to be **Rotten Mushmom** (700003) and **Jr. Balrog** (700001). The Deep Passage's "Bygone Days" may contain a new endgame boss not yet revealed in the string data.

### 7. The Binary Has Everything, The Data Has Almost Nothing
The executable binary contains the full modern damage formula engine (damR, bossDamR, finalDamR, 6th job skill references, etc.), but the WZ metadata is stripped down to only what Classic World needs. This confirms the "modern engine, classic content" architecture.

---

## 10. Summary Statistics

| Metric | Classic World | Live MapleStory (est.) |
|---|---|---|
| Classes | 4 | 50+ |
| Job Tiers | 2 (1st + 2nd) | 6 (1st through 6th) |
| Total Skills | ~70 | Thousands |
| Monsters | 91 | 10,000+ |
| Maps | 304 | 50,000+ |
| NPCs | 188 | 10,000+ |
| Scroll Types | 216 | Thousands |
| Bosses | ~5 field bosses | 30+ instanced bosses |
| Continents | 1 (Victoria) | 10+ |
| Cubes | 0 | Dozens |
| Starforce | No | Yes |
| Free Market | Yes (11 ch) | Removed (Auction House) |
| Potential System | Disabled | Core progression |

---

*Report generated from datamined WZ metadata, cross-referenced with binary analysis of MapleStory_0_zucx.exe (v1.1.0.0, signed 2026-04-06).*