using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Character
{
    internal static class MorphClientActionResolver
    {
        // IDA (`CActionMan::Init`, 0x41beb0) still seeds the client morph action table by
        // iterating raw actions [0, 273) and skipping raw action 55 before morph lookup.
        private const int ClientMorphActionTableExclusiveUpperBound = 273;
        private const int ClientMorphActionTableSkippedRawActionCode = 55;

        private static readonly string[] PirateMorphAuthoredAttackAliases =
        {
            "fist",
            "straight",
            "somersault",
            "doublefire",
            "backspin",
            "doubleupper",
            "screw",
            "shockwave",
            "demolition",
            "snatch",
            "eburster",
            "edrain",
            "dragonstrike",
            "eorb",
            "timeleap",
            "wave"
        };

        private static readonly string[] ArcherMorphAuthoredAttackAliases =
        {
            "windshot",
            "windspear",
            "stormbreak",
            "arrowRain"
        };

        private static readonly string[] IceMorphAuthoredAttackAliases =
        {
            "icemanAttack",
            "iceAttack1",
            "iceAttack2",
            "iceSmash",
            "iceTempest",
            "iceChop",
            "icePanic"
        };

        private static readonly string[] GenericMorphRangedAttackAliases =
        {
            "shoot1",
            "shoot2",
            "shootF"
        };

        private static readonly string[] ClientPublishedRangedMorphFallbackAliases =
        {
            "arrowRain"
        };

        private static readonly string[] ClientPublishedIceSpellMorphFallbackAliases =
        {
            "iceAttack1",
            "iceAttack2",
            "iceSmash",
            "iceTempest",
            "iceChop",
            "icePanic",
            "icemanAttack"
        };

        private static readonly string[] ClientPublishedAlertMorphFallbackAliases =
        {
            "alert",
            "alert2",
            "alert3",
            "alert4",
            "alert5",
            "alert6",
            "alert7"
        };

        private static readonly IReadOnlyDictionary<string, string[]> ClientPublishedAuthoredMorphFallbackAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // Client raw morph action names still include `rain`/`arrowEruption` while
                // Morph/*.img publishes `arrowRain` as the authored archer branch.
                ["rain"] = new[] { "arrowRain" },
                ["arrowEruption"] = new[] { "arrowRain" },
                // Client raw piercing-family requests still include `piercing` and
                // `crossPiercing`, while the published archer morph surface uses
                // `windspear` rather than verbatim `*Piercing` nodes.
                ["piercing"] = new[] { "windspear" },
                ["crossPiercing"] = new[] { "windspear", "stabO1", "alert" },
                // Thief-family ranged raw actions still surface through the client table
                // and WZ skill rows like `Skill/411.img/skill/4111005/action/0 = avenger`,
                // `Skill/412.img/skill/4121008/action/0 = ninjastorm`, and
                // `Skill/1410.img/skill/14101006/action/0 = vampire`, while the checked
                // morph images still publish only generic `shoot*` plus archer-authored
                // `windshot` rather than verbatim thief ranged roots.
                ["avenger"] = new[] { "shoot1", "shoot2", "shootF", "windshot" },
                ["ninjastorm"] = new[] { "shoot1", "shoot2", "shootF", "windshot" },
                ["vampire"] = new[] { "shoot1", "shoot2", "shootF", "windshot" },
                // Character action codes 305-311 name the modern ice-family branches, while
                // older Morph/2001.img still only publishes `icemanAttack` / `icemandoubleJump`.
                ["iceStrike"] = new[]
                {
                    "iceAttack1",
                    "iceAttack2",
                    "iceSmash",
                    "iceTempest",
                    "iceChop",
                    "icePanic",
                    "icemanAttack"
                },
                // The client morph action table still exposes older magician spell-family
                // raw names, while Morph/2002.img and Morph/2001.img only publish the
                // authored ice-family combat roots plus the legacy `icemanAttack`.
                ["magic1"] = new[]
                {
                    "iceAttack1",
                    "iceAttack2",
                    "iceSmash",
                    "iceTempest",
                    "iceChop",
                    "icePanic",
                    "icemanAttack"
                },
                ["magic2"] = new[]
                {
                    "iceAttack1",
                    "iceAttack2",
                    "iceSmash",
                    "iceTempest",
                    "iceChop",
                    "icePanic",
                    "icemanAttack"
                },
                ["magic3"] = new[]
                {
                    "iceAttack1",
                    "iceAttack2",
                    "iceSmash",
                    "iceTempest",
                    "iceChop",
                    "icePanic",
                    "icemanAttack"
                },
                ["magic4"] = new[]
                {
                    "iceAttack1",
                    "iceAttack2",
                    "iceSmash",
                    "iceTempest",
                    "iceChop",
                    "icePanic",
                    "icemanAttack"
                },
                ["magic5"] = new[]
                {
                    "iceAttack1",
                    "iceAttack2",
                    "iceSmash",
                    "iceTempest",
                    "iceChop",
                    "icePanic",
                    "icemanAttack"
                },
                ["magic6"] = new[]
                {
                    "iceAttack1",
                    "iceAttack2",
                    "iceSmash",
                    "iceTempest",
                    "iceChop",
                    "icePanic",
                    "icemanAttack"
                },
                // Character/00002000.img keeps mage ultimate and chain rows on body-action
                // redirects (`explosion`/`meteor`/`blizzard`/`genesis` -> `alert`,
                // `chainlightning` -> `swingO3`, then `stabO2`) while checked Morph/*.img
                // still publishes no verbatim roots for those names.
                ["explosion"] = new[] { "alert" },
                ["meteor"] = new[] { "alert" },
                ["blizzard"] = new[] { "alert" },
                ["genesis"] = new[] { "alert" },
                ["chainlightning"] = new[] { "swingO3", "stabO2" },
                // Character/00002000.img keeps `paralyze` on explicit body redirects
                // (`swingO3`, then `stabO2`) and Morph/*.img still publishes no
                // verbatim `paralyze` branch.
                ["paralyze"] = new[] { "swingO3", "stabO2", "shootF", "shoot2", "shoot1" },
                // Evan skill rows in Skill/2200.img and Skill/2210.img through
                // Skill/2218.img still publish these raw spell and dragon roots.
                // Character/00002000.img keeps their body rows on explicit redirect
                // families, while current Morph/*.img publishes no verbatim roots.
                ["magicmissile"] = new[] { "walk1", "stabO1", "swingO3", "walk", "move", "stand" },
                ["fireCircle"] = new[] { "walk1", "stabO1", "swingO3", "alert", "walk", "move", "stand" },
                ["blaze"] = new[] { "alert", "stabO1" },
                ["magicFlare"] = new[] { "alert", "stabO1", "swingO2" },
                ["lightingBolt"] = new[] { "alert", "stabO1", "swingO2" },
                ["dragonBreathe"] = new[] { "stabO1", "alert" },
                ["dragonIceBreathe"] = new[] { "stabO1", "alert" },
                ["dragonThrust"] = new[] { "alert", "stabO1" },
                ["Earthquake"] = new[] { "alert", "swingO2" },
                ["darkFog"] = new[] { "alert", "swingO2" },
                ["illusion"] = new[] { "alert", "stabO1", "swingO3", "stabO2" },
                ["flameWheel"] = new[] { "alert", "stabO1", "swingO3", "stabO2" },
                ["killingWing"] = new[] { "stand1", "swingO3", "stabO1", "swingO1", "swingO2", "alert", "stand", "walk" },
                // Flame Gear rows under Skill/000.img, Skill/001.img, and Skill/1211.img
                // still publish `flamegear`, while checked Morph/*.img publishes no
                // verbatim branch.
                ["flamegear"] = ClientPublishedIceSpellMorphFallbackAliases,
                ["iceAttack1"] = new[] { "icemanAttack" },
                ["iceAttack2"] = new[] { "icemanAttack" },
                ["iceSmash"] = new[] { "icemanAttack" },
                ["iceTempest"] = new[] { "icemanAttack" },
                ["iceChop"] = new[] { "icemanAttack" },
                ["icePanic"] = new[] { "icemanAttack" },
                // Client raw morph requests still surface the broader pirate gun-family
                // names, while Morph/1000.img, 1001.img, 1100.img, and 1101.img only
                // publish the authored gun branch as `doublefire`.
                // Character/00002000.img keeps `handgun` on explicit body redirects
                // (`shoot2`, then `stabO1`) before the morph-authored pirate gun root.
                ["handgun"] = new[] { "shoot2", "stabO1", "doublefire" },
                // The client raw action table includes `triplefire`, while pirate
                // Morph/*.img publishes the same gun-family surface as `doublefire`.
                // Character/00002000.img keeps `triplefire` and related gun roots on
                // the same `shoot2 -> stabO1` redirect chain before authored roots.
                ["triplefire"] = new[] { "shoot2", "stabO1", "doublefire" },
                // Character/00002000.img keeps `airstrike` on an explicit chain:
                // `alert`, `swingT2`, `swingT1`, `swingT3`.
                ["airstrike"] = new[] { "alert", "swingT2", "swingT1", "swingT3", "doublefire" },
                // Character/00002000.img keeps `shot` on `shoot2`, then `stabO1`
                // before cross-family pirate/archer authored roots.
                ["shot"] = new[] { "shoot2", "stabO1", "doublefire", "windshot" },
                // Skill/520.img/skill/5201006 publishes raw `backstep`, while the
                // pirate morph family only authors that spin-back surface as `backspin`.
                // Character/00002000.img keeps this body row on `stabO1` first.
                ["backstep"] = new[] { "stabO1", "backspin" },
                // The client raw morph action table still publishes `burster1` and
                // `burster2`, while pirate Morph/*.img publishes that surface as
                // the authored `eburster` branch instead of verbatim `burster*`.
                // Character/00002000.img keeps explicit body redirects:
                // `burster1 -> stabT1, stabTF` and
                // `burster2 -> stabT1, stabTF, stabT2`.
                ["burster1"] = new[] { "stabT1", "stabTF", "eburster" },
                ["burster2"] = new[] { "stabT1", "stabTF", "stabT2", "eburster" },
                // `fake` and `octopus` remain pirate gun-family requests in the client
                // raw action table, but Morph/1001.img still only publishes the
                // authored gun surface as `doublefire`.
                ["fake"] = new[] { "shoot2", "stabO1", "doublefire" },
                ["fireburner"] = new[] { "shoot2", "stabO1", "doublefire" },
                ["coolingeffect"] = new[] { "shoot2", "stabO1", "doublefire" },
                ["homing"] = new[] { "swingO3", "doublefire" },
                ["rapidfire"] = new[] { "stabO1", "doublefire" },
                ["cannon"] = new[] { "alert", "doublefire" },
                ["torpedo"] = new[] { "alert", "doublefire" },
                ["octopus"] = new[] { "alert", "swingPF", "stabT2", "swingT2", "swingP2", "swingOF", "doublefire" },
                // Character/00002000.img cannon-family body rows keep a narrower ordered
                // redirect surface than a direct authored-morph gun collapse:
                // flamesplash -> shootF, stabO1, alert
                // swiftShot -> swingO3, stabO1, alert
                // cannonSmash -> stabO1, alert
                // giganticBackstep -> stabO1
                // rushBoom -> alert, swingOF, swingTF, swingT3
                // cannonSlam -> alert, swingPF, stabO2, swingP2, swingT2, swingP1
                // counterCannon -> swingT3
                // cannonSpike -> alert, swingO3
                // superCannon -> alert, swingOF, swingO3
                // magneticCannon -> swingO2, swingP2, swingPF
                // bombExplosion -> alert, stabO1, swingP1
                // monkeyBoomboom -> alert, swingP1
                // immolation -> shootF, stabO1
                // piratebless -> alert, swingP1
                // pirateSpirit -> alert, swingP2, swingO2, swingPF
                // cannonBooster -> swingT2, swingP1
                // noiseWave -> swingOF, swingTF, swingT3, alert
                // noiseWave_pre/noiseWave_ing -> alert
                // Keep authored `doublefire` as the final backstop for templates that
                // do not publish all body-redirect aliases.
                ["flamesplash"] = new[] { "shootF", "stabO1", "alert", "doublefire" },
                ["swiftShot"] = new[] { "swingO3", "stabO1", "alert", "doublefire" },
                ["cannonSmash"] = new[] { "stabO1", "alert", "doublefire" },
                ["giganticBackstep"] = new[] { "stabO1", "doublefire" },
                ["rushBoom"] = new[] { "alert", "swingOF", "swingTF", "swingT3", "doublefire" },
                ["cannonSlam"] = new[] { "alert", "swingPF", "stabO2", "swingP2", "swingT2", "swingP1", "doublefire" },
                ["counterCannon"] = new[] { "swingT3", "doublefire" },
                ["cannonSpike"] = new[] { "alert", "swingO3", "doublefire" },
                ["superCannon"] = new[] { "alert", "swingOF", "swingO3", "doublefire" },
                ["magneticCannon"] = new[] { "swingO2", "swingP2", "swingPF", "doublefire" },
                ["bombExplosion"] = new[] { "alert", "stabO1", "swingP1", "doublefire" },
                ["monkeyBoomboom"] = new[] { "alert", "swingP1", "doublefire" },
                ["immolation"] = new[] { "shootF", "stabO1", "doublefire" },
                ["piratebless"] = new[] { "alert", "swingP1", "doublefire" },
                ["pirateSpirit"] = new[] { "alert", "swingP2", "swingO2", "swingPF", "doublefire" },
                ["cannonBooster"] = new[] { "swingT2", "swingP1", "doublefire" },
                ["noiseWave"] = new[] { "swingOF", "swingTF", "swingT3", "alert", "doublefire" },
                ["noiseWave_pre"] = new[] { "alert", "doublefire" },
                ["noiseWave_ing"] = new[] { "alert", "doublefire" },
                // Character/00002000.img keeps the post-table Mercedes dual-vulcan rows
                // on explicit body redirects rather than only generic `shoot*`:
                // `dualVulcanPrep -> swingT1, shoot1, alert, swingP2`,
                // `dualVulcanLoop -> shoot1, shoot2, stabO1, alert, swingP2`,
                // and `dualVulcanEnd -> shoot2, alert`.
                ["dualVulcanPrep"] = new[] { "swingT1", "shoot1", "alert", "swingP2", "shoot2" },
                ["dualVulcanLoop"] = new[] { "shoot1", "shoot2", "stabO1", "alert", "swingP2" },
                ["dualVulcanEnd"] = new[] { "shoot2", "alert", "shoot1" },
                // Client-table and skill-side dual-shot requests still reach the morph
                // owner as raw names, while Character/00002000.img backs them with
                // ordinary shoot frames plus late swing backstops and Morph/*.img only
                // publishes generic shoot roots for the same surface.
                ["shoot6"] = new[] { "shootF", "shoot2", "shoot1" },
                ["speedDualShot"] = new[] { "shoot1", "swingT2" },
                ["shootDb1"] = new[] { "shoot1", "swingP2" },
                // Beginner capture skill rows still request the raw `capture` action,
                // while Character/00002000.img backs that sequence with shoot2 frames
                // and Morph/*.img publishes only generic shoot-family branches.
                ["capture"] = new[] { "shoot2", "shoot1", "shootF" },
                // The first profession create rows use the same shoot2 body surface.
                ["create0"] = new[] { "shoot2", "shoot1", "shootF" },
                ["create1"] = new[] { "shoot2", "shoot1", "shootF" },
                // Character/00002000.img keeps create2/create3/create4 as body-owned
                // authored frames (plus *_s/*_f single-frame tails) while Morph/*.img
                // still publishes no verbatim create* roots. Keep them on the same
                // checked shoot-family fallback surface as create0/create1.
                ["create2"] = new[] { "shoot2", "shoot1", "shootF" },
                ["create2_s"] = new[] { "shoot2", "shoot1", "shootF" },
                ["create2_f"] = new[] { "shoot2", "shoot1", "shootF" },
                ["create3"] = new[] { "shoot2", "shoot1", "shootF" },
                ["create3_s"] = new[] { "shoot2", "shoot1", "shootF" },
                ["create3_f"] = new[] { "shoot2", "shoot1", "shootF" },
                ["create4"] = new[] { "shoot2", "shoot1", "shootF" },
                ["create4_s"] = new[] { "shoot2", "shoot1", "shootF" },
                ["create4_f"] = new[] { "shoot2", "shoot1", "shootF" },
                // Mercedes `strikeDual` skill rows still request the raw action name,
                // while Character/00002000.img backs it with shoot-family frames before
                // mixed swing/stab backstops and Morph/*.img publishes only generic
                // `shoot*` plus authored archer roots for that surface.
                ["strikeDual"] = new[] { "shoot2", "swingT2", "stabO1", "alert", "shoot1", "shootF", "windshot", "swingT1", "swingT3", "stabO2", "proneStab" },
                // Support-family client raw actions like `smokeshell`, `holyshield`, and
                // `resurrection` still come from WZ skill rows, but the checked morph
                // templates only publish the alert-family surface rather than those verbatim
                // support roots.
                ["smokeshell"] = new[] { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" },
                ["holyshield"] = new[] { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" },
                ["resurrection"] = new[] { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" },
                // Beginner, event, and thief concealment/support rows still publish raw
                // `float`, `pyramid`, `bamboo`, and `darksight` requests on the skill
                // side, while Character/00002000.img backs the event pair with alert
                // and swing-family frames and the checked morph templates expose no
                // verbatim support roots for any of them.
                ["float"] = ClientPublishedAlertMorphFallbackAliases,
                ["pyramid"] = new[] { "alert", "swingPF", "swingOF", "swingP2", "swingTF" },
                ["bamboo"] = ClientPublishedAlertMorphFallbackAliases,
                ["darksight"] = ClientPublishedAlertMorphFallbackAliases,
                // Spell support/buff/debuff rows are also present as skill-side raw
                // requests in the current WZ export, but Morph/*.img publishes no
                // verbatim support roots for them.
                ["elementalReset"] = ClientPublishedAlertMorphFallbackAliases,
                ["magicRegistance"] = ClientPublishedAlertMorphFallbackAliases,
                ["magicBooster"] = ClientPublishedAlertMorphFallbackAliases,
                ["magicShield"] = ClientPublishedAlertMorphFallbackAliases,
                ["recoveryAura"] = ClientPublishedAlertMorphFallbackAliases,
                ["OnixBlessing"] = ClientPublishedAlertMorphFallbackAliases,
                ["soulStone"] = ClientPublishedAlertMorphFallbackAliases,
                ["ghostLettering"] = ClientPublishedAlertMorphFallbackAliases,
                ["slow"] = ClientPublishedAlertMorphFallbackAliases,
                ["mapleHero"] = ClientPublishedAlertMorphFallbackAliases,
                ["OnixProtection"] = ClientPublishedAlertMorphFallbackAliases,
                ["OnixWill"] = ClientPublishedAlertMorphFallbackAliases,
                ["Awakening"] = ClientPublishedAlertMorphFallbackAliases,
                // Later Demon / Mercedes support roots still resolve through the same
                // alert-family body surface in Character/00002000.img.
                ["demonGravity"] = new[] { "alert" },
                ["blessOfGaia"] = new[] { "alert" },
                // The latest checked support-like body rows (`mistEruption`,
                // `demolitionElf`, and `powerEndure`) also collapse entirely onto the
                // alert family, while Morph/*.img still publishes no verbatim roots.
                ["mistEruption"] = new[] { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" },
                ["demolitionElf"] = new[] { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" },
                ["powerEndure"] = new[] { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" },
                // Resistance revive/buff skill rows keep requesting raw roots, while
                // Character/00002000.img backs both surfaces entirely with alert frames
                // and Morph/*.img publishes no verbatim `revive` / `superBody` branches.
                ["revive"] = new[] { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" },
                ["superBody"] = new[] { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" },
                // Evan spell and dragon entries are kept in the earlier explicit
                // redirect block to avoid duplicate-key shadowing in this map.
            };

        private static readonly IReadOnlyDictionary<string, string[]> ClientPublishedGenericMorphFallbackAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // `savage` still appears on the client morph request surface, but pirate
                // morphs publish generic stab branches rather than a dedicated `savage` node.
                ["savage"] = new[] { "stabO1", "stabO2", "proneStab" },
                // WZ still publishes melee-only morph surfaces such as 1000/1100 on generic
                // `stabO*` / `proneStab`, while newer warrior slash-family raw requests stay on
                // skill-side action rows like 1121006 (`rush` / `rush2`), 1111010
                // (`brandish1` / `brandish2`), 1221009 (`blast`), and 1221011 (`sanctuary`).
                ["rush"] = new[] { "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["rush2"] = new[] { "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["brandish1"] = new[] { "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["brandish2"] = new[] { "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["blast"] = new[] { "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["sanctuary"] = new[] { "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                // Additional warrior / aran melee rows still publish raw action requests
                // like `souldriver`, `firestrike`, `blade`, `chargeBlow`,
                // `braveslash*`, `doubleSwing`, `tripleSwing`, `finalCharge`,
                // `finalToss`, `finalBlow`, `comboSmash`, `comboFenrir`,
                // `fullSwing*`, `overSwing*`, `rollingSpin`, `comboTempest`,
                // and `comboJudgement`, while checked morph images still expose
                // no verbatim Aran branches. Keep the Aran entries on the
                // Character/00002000.img body-action redirect order first, then
                // let the generic melee surface backstop templates that publish
                // only older swing/stab roots.
                ["souldriver"] = new[] { "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["firestrike"] = new[] { "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["blade"] = new[] { "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                // Character/00002000.img keeps these Aran roots on explicit redirect
                // chains instead of one generic melee family:
                // chargeBlow -> stabO1/alert;
                // braveslash1/2 -> alert/swingO2/swingOF/stabO1;
                // braveslash3/4 -> alert/swingT1/swingT3/stabO1.
                ["chargeBlow"] = new[] { "stabO1", "alert" },
                ["braveslash1"] = new[] { "alert", "swingO2", "swingOF", "stabO1" },
                ["braveslash2"] = new[] { "alert", "swingO2", "swingOF", "stabO1" },
                ["braveslash3"] = new[] { "alert", "swingT1", "swingT3", "stabO1" },
                ["braveslash4"] = new[] { "alert", "swingT1", "swingT3", "stabO1" },
                ["doubleSwing"] = new[] { "swingP2", "swingPF", "stabTF" },
                ["tripleSwing"] = new[] { "swingPF", "proneStab", "swingP2" },
                ["finalCharge"] = new[] { "stabTF", "stabT2" },
                ["finalToss"] = new[] { "swingPF", "swingP2" },
                ["finalBlow"] = new[] { "swingT2", "swingPF" },
                ["comboSmash"] = new[] { "swingOF", "stabT2" },
                ["comboFenrir"] = new[] { "alert", "swingT2", "swingPF", "swingP2", "stabTF", "stabT2" },
                ["fullSwingDouble"] = new[] { "stabT2", "stabT1" },
                ["fullSwingTriple"] = new[] { "swingT2", "swingPF" },
                ["overSwingDouble"] = new[] { "swingPF", "stabT1" },
                ["overSwingTriple"] = new[] { "stabTF", "swingPF" },
                ["rollingSpin"] = new[] { "stabT1", "stabTF" },
                ["comboTempest"] = new[] { "alert" },
                ["comboJudgement"] = new[] { "alert", "swingP2", "swingPF" },
                // WZ still publishes no verbatim `assaulter` / `assassination*` branches in
                // Morph/*.img, while skill-side rows like 4211002 and 4221001 still request
                // those raw action names. Keep them on the same generic melee surface.
                ["assaulter"] = new[] { "stabO1", "stabO2", "proneStab", "swingT1", "swingT3" },
                ["assassination"] = new[] { "stabO1", "stabO2", "proneStab", "swingT1", "swingT3" },
                ["assassinationS"] = new[] { "stabO1", "stabO2", "proneStab", "swingT1", "swingT3" },
                // Additional thief-family raw actions still surface in the client action table
                // and WZ skill rows like `Skill/412.img/skill/4121003/action/0 = showdown`
                // and `Skill/433.img/skill/4331005/action/0 = flyingAssaulter`, while the
                // checked morph templates keep only generic stab/swing families.
                ["showdown"] = new[] { "stabO1", "stabO2", "proneStab", "swingT1", "swingT3" },
                // Dual-blade skill rows still publish these raw action names, but Morph/*.img
                // keeps them on generic stab/swing roots instead of verbatim dual-blade nodes.
                ["slashStorm1"] = new[] { "stabO1", "stabO2", "proneStab", "swingT1", "swingT3" },
                ["slashStorm2"] = new[] { "stabO1", "stabO2", "proneStab", "swingT1", "swingT3" },
                ["bloodyStorm"] = new[] { "stabO1", "stabO2", "proneStab", "swingT1", "swingT3" },
                // Skill/421.img keeps the raw `prone2` request, while the checked
                // Character/00002000.img body row resolves it through proneStab frames
                // and Morph/*.img publishes no verbatim `prone2` branch.
                ["prone2"] = new[] { "proneStab" },
                // A targeted WZ pass over Character/00002000.img plus the corresponding
                // Dual Blade skill rows (430/431/432/433/434) shows these later raw
                // requests already authored as ordered generic body-action sequences.
                ["stabD1"] = new[] { "stabO1", "swingO3", "stabT1" },
                ["tripleStab"] = new[] { "alert", "swingO1", "swingO3" },
                ["flyingAssaulter"] = new[] { "swingPF", "swingOF", "stabT1", "swingO2" },
                ["tornadoDash"] = new[] { "swingO3", "fly", "jump", "stand" },
                ["tornadoRush"] = new[] { "alert", "swingOF" },
                ["tornadoDashStop"] = new[] { "swingOF", "fly", "jump", "stand" },
                ["fatalBlow"] = new[] { "alert", "stabO1", "swingO1", "swingOF" },
                ["upperStab"] = new[] { "alert", "swingPF" },
                ["chainPull"] = new[] { "stabO1", "swingO3" },
                ["chainAttack"] = new[] { "swingO1", "swingOF", "swingO2", "swingO3", "swingPF" },
                ["finalCutPrepare"] = new[] { "swingOF" },
                ["finalCut"] = new[] { "swingO1" },
                ["phantomBlow"] = new[] { "stabO1", "stabT1", "stabO2", "swingPF", "stabOF", "swingO1" },
                ["bladeFury"] = new[] { "alert", "swingOF" },
                ["finishAttack"] = new[] { "stabO1", "stabO2", "swingO1" },
                ["finishAttack_link"] = new[] { "stabO1", "stabO2", "swingO1" },
                ["finishAttack_link2"] = new[] { "stabO1", "stabO2", "swingOF", "alert" },
                // The remaining checked dual-blade / thief body rows still publish no
                // transform-only roots in Morph/*.img. Character/00002000.img backs
                // these client raw requests with ordinary stab, swing, or alert frames.
                ["swingD1"] = new[] { "swingO2", "swingOF" },
                ["swingD2"] = new[] { "swingO2", "swingOF" },
                ["owlDead"] = new[] { "stabO1", "swingO1" },
                ["suddenRaid"] = new[] { "alert", "swingOF" },
                // Later Demon / Mercedes / Resistance body actions in
                // Character/00002000.img still collapse onto generic swing/stab families
                // rather than unique morph roots.
                ["demonSlasher"] = new[] { "stand1", "swingO2" },
                ["bluntSmash"] = new[] { "alert", "swingO3", "swingOF" },
                ["demonTrace"] = new[] { "swingOF" },
                ["elfrush"] = new[] { "alert", "swingO1" },
                // Character/00002000.img keeps `elfrush2` on an explicit body redirect
                // chain (`swingO1`, then `swingP2`, `swingP1`, and `alert`) rather than
                // the narrower `elfrush` (`alert`, `swingO1`) pair.
                ["elfrush2"] = new[] { "swingO1", "swingP2", "swingP1", "alert" },
                // `elfrush_final` and `elfrush_final2` body rows now resolve entirely
                // through `swingTF` in Character/00002000.img. Keep that authored root
                // first while preserving nearby checked backstops for templates that do
                // not publish the full T-family swing surface.
                ["elfrush_final"] = new[] { "swingTF", "swingT1", "alert", "swingO1" },
                ["elfrush_final2"] = new[] { "swingTF", "swingT1", "alert", "swingO1" },
                ["deathDraw"] = new[] { "alert", "swingO2", "swingOF", "swingO3" },
                ["dealingRush"] = new[] { "swingPF", "swingO1" },
                ["elfTornado"] = new[] { "alert", "swingPF", "swingTF" },
                ["devilCry"] = new[] { "alert", "swingO2", "swingOF" },
                ["movebind"] = new[] { "alert", "swingO2", "swingOF" },
                ["darkSpin"] = new[] { "alert", "swingT1" },
                ["darkThrust"] = new[] { "alert", "stabT1", "swingT1" },
                ["healingAttack"] = new[] { "alert", "stabO1" },
                // These later client raw-action requests stay outside Morph/*.img
                // verbatim roots; Character/00002000.img backs them with ordinary
                // body-action families instead.
                ["swingRes"] = new[] { "swingO2" },
                ["lasergun"] = new[] { "shoot2", "stabO1", "shoot1" },
                ["battlecharge"] = new[] { "swingOF", "stabT1", "alert" },
                ["darkTornado_pre"] = new[] { "stabO1", "swingO2", "swingTF" },
                ["darkTornado"] = new[] { "swingTF", "swingO2" },
                ["darkTornado_after"] = new[] { "swingO2", "swingTF", "alert" },
                ["tripleBlow"] = new[] { "alert", "swingO1", "swingO3", "stabO1" },
                ["quadBlow"] = new[] { "alert", "swingO1", "swingO3", "stabO2", "stabO1" },
                ["deathBlow"] = new[] { "swingPF", "stabO2", "swingO1", "swingO2", "swingOF", "stabT2", "stabT1", "alert" },
                ["finishBlow"] = new[] { "swingPF", "stabO2", "swingO1", "swingO2", "swingOF", "stabT2" },
                // Later dark- and ice-family body rows still resolve onto ordinary
                // alert / swing / stab families, but the checked Character/00002000.img
                // rows keep a narrower order than generic melee collapse:
                // `darkImpale -> alert / swingT2 / stabT2 / stabT1` and
                // `glacialChain -> swingO3 / swingO2 / stabO1`.
                ["darkImpale"] = new[] { "alert", "swingT2", "stabT2", "stabT1" },
                ["glacialChain"] = new[] { "swingO3", "swingO2", "stabO1" },
                // `windEffect` remains a client raw request on the skill side, while the
                // checked body row keeps it on `swingT1` before `swingTF`.
                ["windEffect"] = new[] { "swingT1", "swingTF" },
                ["jShot"] = new[] { "swingT2", "swingPF", "swingOF" },
                // Skill/2312.img/skill/23121003/action/0 still publishes
                // `edgeSpiral`, while Character/00002000.img keeps no verbatim
                // `edgeSpiral` body branch and the client raw morph surface still
                // omits that name. Keep it on the checked dual-shot surface:
                // `jShot` redirects plus the nearby `shoot1` backstop.
                ["edgeSpiral"] = new[] { "swingT2", "swingPF", "swingOF", "shoot1" },
                ["multiSniping"] = new[] { "swingT1", "swingTF", "shoot1", "swingT2", "shoot2" },
                // Character/00002000.img keeps the three pole-arm raw roots as body
                // redirects (`swingT2PoleArm -> swingT2`, `swingP1PoleArm -> swingP1`,
                // `swingP2PoleArm -> swingP2`), while checked Morph/*.img publishes no
                // verbatim `*PoleArm` branches. Keep these requests on the same checked
                // swing-first, generic-melee fallback surface used by other Aran slices.
                ["swingT2PoleArm"] = new[] { "swingT2", "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["swingP1PoleArm"] = new[] { "swingP1", "swingP2", "swingPF", "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["swingP2PoleArm"] = new[] { "swingP2", "swingPF", "swingP1", "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                // Xenon max-force rows under Character/00002000.img also stay on a
                // narrower checked body-family order than generic melee fallback.
                ["maxForce0"] = new[] { "swingO3" },
                ["maxForce1"] = new[] { "stabO1" },
                ["maxForce2"] = new[] { "stabOF", "swingOF", "swingO2", "swingO1" },
                ["maxForce3"] = new[] { "swingO2", "swingO1", "swingO3", "stabOF" },
                // `shotC1` is present on the recovered client raw-action surface and
                // Character/00002000.img backs it with alert first, then stab frames.
                // Current Morph/*.img publishes no verbatim cannon-shot branch.
                ["shotC1"] = new[] { "alert", "stabO1" },
                // These dual-blade and resistance raw skill-side requests are absent
                // from Morph/*.img, while Character/00002000.img resolves them onto
                // ordinary swing/stab body surfaces.
                ["flashBang"] = new[] { "swingO3", "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["monsterBombPrepare"] = new[] { "swingPF", "swingT1", "swingT3", "stabO1", "stabO2", "proneStab" },
                ["monsterBombThrow"] = new[] { "swingOF", "swingO1", "swingPF", "swingT1", "swingT3", "stabO1", "stabO2", "proneStab", "alert" },
                // Battle Mage dark-chain body rows in Character/00002000.img still
                // start on `swingO3 -> swingO2 -> stabO1`; keep that checked order
                // first, then preserve broader stab/swing backstops for templates that
                // do not publish the complete O-family surface.
                ["darkChain"] = new[] { "swingO3", "swingO2", "stabO1", "stabO2", "proneStab", "swingT1", "swingT3" },
                ["darkLightning"] = new[] { "swingO2", "stabO1", "stabO2" },
                ["swingT2Giant"] = new[] { "alert", "stabO2" },
                // Profession gather rows are authored as body-action redirects in
                // Character/00002000.img, while Morph/*.img has no verbatim gather roots.
                ["gather0"] = new[] { "swingT2", "swingT1" },
                ["gather1"] = new[] { "swingT1" },
                // Character/00002000.img keeps the dual-blade C/Db tails on explicit body
                // redirects:
                // `swingC1 -> alert, swingT1`,
                // `swingC2 -> swingT3`,
                // `swingDb1 -> swingO2`,
                // `swingDb2 -> stabO1`.
                // Preserve those checked roots first before broader family fallback.
                ["swingC1"] = new[] { "alert", "swingT1", "swingT3" },
                ["swingC2"] = new[] { "swingT3", "swingT1" },
                ["swingDb1"] = new[] { "swingO2", "swingT1", "swingT3" },
                ["swingDb2"] = new[] { "stabO1", "swingT1", "swingT3" }
            };

        private static readonly string[] ClientPublishedMorphStabFallbackAliases =
        {
            "stabO1",
            "stabO2",
            "stabOF",
            "stabT1",
            "stabT2",
            "stabTF"
        };

        private static readonly string[] PublishedDoubleJumpAliases =
        {
            "archerDoubleJump",
            "iceDoubleJump",
            "icemandoubleJump"
        };

        private static readonly IReadOnlyDictionary<string, string[]> ClientPublishedJumpMorphFallbackAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // Skill/531.img/skill/5311003 still publishes `cannonJump`, while
                // Character/00002000.img keeps the body redirect on `swingOF` and the
                // checked Morph/*.img templates publish no verbatim `cannonJump` root.
                ["cannonJump"] = new[] { "swingOF", "jump" },
                // The client morph action table also carries `spiritJump`, and
                // Character/00002000.img keeps that body row on explicit redirects:
                // `jump`, then `swingOF`, then `swingPF`.
                ["spiritJump"] = new[] { "jump", "swingOF", "swingPF" },
                // WZ skill rows also keep non-table flash-jump roots
                // (`demonJump`, `demonJumpUpward`, `demonJumpFoward`,
                // and `HTswiftPhantom`/`swiftPhantom`) while checked Morph/*.img and
                // Character/00002000.img publish no verbatim branches for those names.
                // Keep them on the same jump-first fallback surface as `spiritJump`.
                ["demonJump"] = new[] { "jump", "swingOF", "swingPF" },
                ["demonJumpUpward"] = new[] { "jump", "swingOF", "swingPF" },
                ["demonJumpFoward"] = new[] { "jump", "swingOF", "swingPF" },
                ["HTswiftPhantom"] = new[] { "jump", "swingOF", "swingPF" },
                ["swiftPhantom"] = new[] { "jump", "swingOF", "swingPF" },
                // Character/00002000.img keeps `slayerDoubleJump` on `jump`, while
                // Morph/*.img still has no verbatim slayer branch.
                ["slayerDoubleJump"] = new[] { "jump" },
                // Client raw action code 242 remains `doubleJump`, and
                // Character/00002000.img keeps `doubleJump/0/action = sit`.
                // Keep that checked posture redirect first while preserving `jump`
                // as the nearby movement backstop for templates without `sit`.
                ["doubleJump"] = new[] { "sit", "jump" },
                // Post-s_sMorphAction raw action codes still include these late
                // double-jump names. Character/00002000.img keeps
                // `archerDoubleJump/0/action = jump` and keeps all checked
                // `iceDoubleJump/*/action = alert`, while Morph/*.img only
                // publishes verbatim roots on template-specific branches.
                // Keep exact authored morph roots first (handled by
                // ShouldPreferExactPublishedAction), then apply these checked
                // body redirects before broader double-jump family fallback.
                ["archerDoubleJump"] = new[] { "jump" },
                ["iceDoubleJump"] = new[] { "alert", "jump" }
            };

        private static readonly IReadOnlyDictionary<string, string[]> ClientPublishedMovementMorphFallbackAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // Client raw locomotion names still include walk/stand variants (`walk1`,
                // `walk2`, `stand1`, `stand2`) while checked Morph/*.img templates
                // publish those surfaces as `walk` and `stand`.
                ["walk1"] = new[] { "walk", "move", "stand" },
                ["walk2"] = new[] { "walk", "move", "stand" },
                ["stand1"] = new[] { "stand", "walk" },
                ["stand2"] = new[] { "stand", "walk" },
                // Skill/2002.img/skill/20020111 still publishes the raw action `fastest`
                // with flag-only morph metadata, while Character/00002000.img keeps
                // `fastest` body redirects on `rope`, `swingPF`, and `swingOF` before
                // generic movement fallback. The suffix-resolved Morph/0111.img still
                // publishes no verbatim `fastest` branch.
                ["fastest"] = new[] { "rope", "swingPF", "swingOF", "fly", "jump", "stand" },
                // Skill/2100.img/skill/21001001 still publishes `combatStep`, and
                // Character/00002000.img keeps that row on `walk2` body redirects
                // before generic locomotion fallback. Morph/*.img still publishes no
                // dedicated `walk2` branch in the checked templates.
                ["combatStep"] = new[] { "walk2", "walk", "move", "stand" },
                // Client raw action code 87 remains `dash` and Character/00002000.img
                // still redirects that branch through `walk1`, while checked Morph/*.img
                // keeps no verbatim `dash` root.
                ["dash"] = new[] { "walk", "move", "stand" },
                // Character/00002000.img still publishes the `ghost*` raw movement
                // family (`ghostwalk`, `ghostjump`, `ghostladder`, `ghostrope`,
                // `ghostfly`) while Morph/*.img publishes only generic locomotion roots.
                ["ghostwalk"] = new[] { "walk", "move", "stand" },
                ["ghostjump"] = new[] { "jump", "stand" },
                ["ghostladder"] = new[] { "ladder", "ladder2", "stand" },
                ["ghostrope"] = new[] { "rope", "rope2", "stand" },
                ["ghostfly"] = new[] { "fly", "fly2", "jump", "stand" },
                // Skill/2215..2218 and 2212..2218 keep these Evan dragon prepare roots,
                // while Character/00002000.img redirects both to `walk1` and checked
                // Morph/*.img publishes no verbatim `*_prepare` movement roots.
                ["breathe_prepare"] = new[] { "walk", "move", "stand" },
                ["icebreathe_prepare"] = new[] { "walk", "move", "stand" }
            };

        private static readonly IReadOnlyDictionary<string, string[]> ClientPublishedPostureMorphFallbackAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // These client raw action names are published by WZ skill rows and
                // Character/00002000.img backs them with `sit` body frames, while
                // Morph/*.img publishes no verbatim branches for the same surface.
                ["gatlingshot"] = new[] { "sit" },
                ["gatlingshot2"] = new[] { "sit" },
                ["flamethrower"] = new[] { "sit" },
                ["flamethrower_pre"] = new[] { "sit" },
                ["flamethrower_after"] = new[] { "sit" },
                ["flamethrower2"] = new[] { "sit" },
                ["flamethrower_pre2"] = new[] { "sit" },
                ["flamethrower_after2"] = new[] { "sit" },
                ["clawCut"] = new[] { "sit" },
                ["sonicBoom"] = new[] { "sit" },
                ["wildbeast"] = new[] { "sit" },
                ["rpunch"] = new[] { "sit" },
                ["mRush"] = new[] { "sit" },
                ["swallow_pre"] = new[] { "sit" },
                ["swallow_loop"] = new[] { "sit" },
                ["swallow"] = new[] { "sit" },
                ["swallow_attack"] = new[] { "sit" },
                // The remaining checked Mechanic and Wild Hunter skill-side rows keep
                // these raw action names, while Character/00002000.img backs them with
                // `sit` frames and Morph/*.img publishes no verbatim branches.
                ["siege_pre"] = new[] { "sit" },
                ["tank_pre"] = new[] { "sit" },
                ["tank_laser"] = new[] { "sit" },
                ["siege"] = new[] { "sit" },
                ["siege_stand"] = new[] { "sit" },
                ["siege_after"] = new[] { "sit" },
                ["tank"] = new[] { "sit" },
                ["tank_walk"] = new[] { "sit" },
                ["tank_stand"] = new[] { "sit" },
                ["tank_prone"] = new[] { "sit" },
                ["tank_after"] = new[] { "sit" },
                ["tank_siegepre"] = new[] { "sit" },
                ["tank_siegeattack"] = new[] { "sit" },
                ["tank_siegestand"] = new[] { "sit" },
                ["tank_siegeafter"] = new[] { "sit" },
                ["tank_msummon"] = new[] { "sit" },
                ["tank_rbooster_pre"] = new[] { "sit" },
                ["tank_rbooster_after"] = new[] { "sit" },
                ["tank_msummon2"] = new[] { "sit" },
                ["tank_mRush"] = new[] { "sit" },
                ["rbooster_pre"] = new[] { "sit" },
                // Character/00002000.img keeps `rbooster` on `alert` first
                // (`rbooster/0/action = alert`), with `sit` remaining as the
                // checked fallback posture surface.
                ["rbooster"] = new[] { "alert", "sit" },
                ["rbooster_after"] = new[] { "sit" },
                ["drillrush"] = new[] { "sit" },
                ["mbooster"] = new[] { "sit" },
                ["crossRoad"] = new[] { "sit" },
                ["earthslug"] = new[] { "sit" },
                ["msummon"] = new[] { "sit" },
                ["msummon2"] = new[] { "sit" },
                ["mine"] = new[] { "sit" },
                // Skill/3310.img still publishes `knockback`, while Character/00002000.img
                // backs the body row with `sit` frames and Morph/*.img publishes no
                // verbatim knockback branch.
                ["knockback"] = new[] { "sit" },
                // Skill/3212.img still publishes the raw Battle Mage root, while
                // Character/00002000.img backs it entirely with alert frames.
                ["nemesis"] = new[] { "alert" },
                // Skill/3212.img still publishes `cyclone_pre`, while the client raw
                // surface also keeps `cyclone` / `cyclone_after`; Character/00002000.img
                // routes those body rows through `swingO2` + `swingTF` (with alert-tail
                // recovery on `cyclone_after`) and Morph/*.img publishes no verbatim roots.
                ["cyclone_pre"] = new[] { "alert", "stabO1", "swingO2", "swingTF" },
                ["cyclone"] = new[] { "swingO2", "swingTF" },
                ["cyclone_after"] = new[] { "swingO2", "swingTF", "alert" },
                // Character/00002000.img backs the checked full-screen jaguar rain
                // branch with `alert` rather than a morph-owned `*Rain` attack root.
                ["flashRain"] = new[] { "alert" },
                // Ride/getoff and jaguar-only profession rows are body-action redirects
                // or sit-only mounted surfaces in Character/00002000.img. Morph/*.img
                // publishes no verbatim branches for these raw requests.
                ["ride"] = new[] { "stand1", "alert", "jump", "sit" },
                ["getoff"] = new[] { "sit", "jump", "swingPF", "alert", "stand2" },
                ["ride2"] = new[] { "alert", "swingPF", "jump", "swingOF", "sit" },
                ["getoff2"] = new[] { "sit", "jump", "swingOF", "swingPF", "alert" },
                // Character/00002000.img also publishes the later mount pair as body-action
                // redirects (`ride3 -> stand1/alert/fly`, `getoff3 -> fly/alert`) while
                // checked Morph/*.img still exposes no verbatim `ride3` / `getoff3` roots.
                ["ride3"] = new[] { "stand1", "alert", "fly" },
                ["getoff3"] = new[] { "fly", "alert" },
                // The same body row keeps raw `alert8` authored as a jump redirect and
                // raw `giant` authored as a sit redirect, with no dedicated Morph/*.img
                // branches for either name.
                ["alert8"] = new[] { "jump", "alert" },
                ["giant"] = new[] { "sit" },
                // Character/00002000.img keeps `recovery/0/action = alert`, while
                // several checked Morph/*.img templates (1002/1003/1103/2000..2003)
                // publish no verbatim `recovery` branch. Keep authored `recovery`
                // exact when present, then follow the checked alert redirect surface.
                ["recovery"] = new[] { "alert", "stand" },
                // Post-s_sMorphAction client raw action code 302 remains `pvpko`.
                // Character/00002000.img keeps `pvpko/0/action = alert`, and current
                // Morph coverage only publishes a verbatim `pvpko` branch on 2002.img.
                // Keep exact `pvpko` first when authored, then fall back through the
                // checked body-redirect surface for other morph templates.
                ["pvpko"] = new[] { "alert", "dead" },
                // Character/00002000.img also keeps `heal`, `ghoststand`, `ghostsit`,
                // and `ghostproneStab` as client raw roots while checked Morph/*.img
                // publishes no verbatim branches for those names.
                ["heal"] = new[] { "stand", "alert" },
                ["ghoststand"] = new[] { "stand", "alert" },
                ["ghostsit"] = new[] { "sit", "stand" },
                ["ghostproneStab"] = new[] { "proneStab", "prone", "sit" },
                ["proneStab_jaguar"] = new[] { "sit" },
                ["herbalism_jaguar"] = new[] { "sit" },
                ["mining_jaguar"] = new[] { "sit" },
                ["herbalism_mechanic"] = new[] { "sit" },
                ["mining_mechanic"] = new[] { "sit" }
            };

        public static IEnumerable<string> EnumerateClientActionAliases(CharacterPart morphPart, string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (ShouldPreferExactPublishedAction(morphPart, actionName) && yielded.Add(actionName))
            {
                yield return actionName;
            }

            if (actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string alertAlias in EnumerateAlertAliases(morphPart, actionName))
                {
                    if (yielded.Add(alertAlias))
                    {
                        yield return alertAlias;
                    }
                }
            }

            foreach (string jumpAlias in EnumerateClientPublishedJumpAliases(morphPart, actionName))
            {
                if (yielded.Add(jumpAlias))
                {
                    yield return jumpAlias;
                }
            }

            foreach (string movementAlias in EnumerateClientPublishedMovementAliases(morphPart, actionName))
            {
                if (yielded.Add(movementAlias))
                {
                    yield return movementAlias;
                }
            }

            foreach (string postureAlias in EnumerateClientPublishedPostureAliases(morphPart, actionName))
            {
                if (yielded.Add(postureAlias))
                {
                    yield return postureAlias;
                }
            }

            if (ShouldEnumerateDoubleJumpAliases(actionName))
            {
                foreach (string doubleJumpAlias in EnumerateDoubleJumpAliases(morphPart))
                {
                    if (yielded.Add(doubleJumpAlias))
                    {
                        yield return doubleJumpAlias;
                    }
                }
            }

            if (IsGenericMorphAttackAction(actionName))
            {
                foreach (string authoredAttackAlias in EnumerateAuthoredAttackAliases(morphPart, actionName))
                {
                    if (yielded.Add(authoredAttackAlias))
                    {
                        yield return authoredAttackAlias;
                    }
                }
            }

            foreach (string candidate in CharacterPart.GetActionLookupStrings(actionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateClientPublishedPostureAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (!ClientPublishedPostureMorphFallbackAliases.TryGetValue(actionName, out string[] aliases)
                || aliases == null)
            {
                yield break;
            }

            foreach (string alias in aliases)
            {
                foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                {
                    yield return resolvedAlias;
                }
            }
        }

        private static IEnumerable<string> EnumerateClientPublishedMovementAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (!ClientPublishedMovementMorphFallbackAliases.TryGetValue(actionName, out string[] aliases)
                || aliases == null)
            {
                yield break;
            }

            foreach (string alias in aliases)
            {
                foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                {
                    yield return resolvedAlias;
                }
            }
        }

        private static bool ShouldPreferExactPublishedAction(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null
                || string.IsNullOrWhiteSpace(actionName)
                || !HasPublishedAction(morphPart, actionName))
            {
                return false;
            }

            // Keep the client-shaped generic jump request surface promotable onto
            // authored morph double-jump branches instead of pinning plain jump first.
            return !string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "doubleJump", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsJumpActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase)
                   || actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("demonjump", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("swiftphantom", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> EnumerateClientPublishedJumpAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (!ClientPublishedJumpMorphFallbackAliases.TryGetValue(actionName, out string[] aliases)
                || aliases == null)
            {
                yield break;
            }

            foreach (string alias in aliases)
            {
                foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                {
                    yield return resolvedAlias;
                }
            }
        }

        private static IEnumerable<string> EnumerateAlertAliases(CharacterPart morphPart, string actionName)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
            {
                yield return actionName;
            }

            foreach (string authoredAlertAlias in EnumeratePresentAlertAliases(morphPart, actionName))
            {
                if (yielded.Add(authoredAlertAlias))
                {
                    yield return authoredAlertAlias;
                }
            }

            if (yielded.Add("alert"))
            {
                yield return "alert";
            }
        }

        private static IEnumerable<string> EnumerateAuthoredAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool hasExplicitClientPublishedAliasMap = HasClientPublishedAuthoredMorphFallbackAliases(actionName);
            bool yieldedClientPublishedAlias = false;
            foreach (string clientPublishedAlias in EnumerateClientPublishedAuthoredAttackAliases(morphPart, actionName))
            {
                if (yielded.Add(clientPublishedAlias))
                {
                    yieldedClientPublishedAlias = true;
                    yield return clientPublishedAlias;
                }
            }

            if (hasExplicitClientPublishedAliasMap
                && !yieldedClientPublishedAlias
                && KeepsClientPublishedMorphAliasInsideMappedFamily(actionName))
            {
                yield break;
            }

            foreach (string genericMeleeAlias in EnumerateGenericMeleeAttackAliases(morphPart, actionName))
            {
                if (yielded.Add(genericMeleeAlias))
                {
                    yield return genericMeleeAlias;
                }
            }

            bool prefersArcherAliases = PrefersArcherAttackAliases(actionName);
            bool prefersGenericRangedFallback = PrefersGenericRangedFallbackAliases(actionName);
            bool prefersIceAttackAliases = PrefersIceAttackAliases(morphPart, actionName);

            IEnumerable<string> primaryAuthoredAliases;
            IEnumerable<string> secondaryAuthoredAliases;
            IEnumerable<string> tertiaryAuthoredAliases;

            if (prefersArcherAliases)
            {
                primaryAuthoredAliases = ArcherMorphAuthoredAttackAliases;
                secondaryAuthoredAliases = PirateMorphAuthoredAttackAliases;
                tertiaryAuthoredAliases = IceMorphAuthoredAttackAliases;
            }
            else if (prefersIceAttackAliases)
            {
                primaryAuthoredAliases = IceMorphAuthoredAttackAliases;
                secondaryAuthoredAliases = PirateMorphAuthoredAttackAliases;
                tertiaryAuthoredAliases = ArcherMorphAuthoredAttackAliases;
            }
            else
            {
                primaryAuthoredAliases = PirateMorphAuthoredAttackAliases;
                secondaryAuthoredAliases = ArcherMorphAuthoredAttackAliases;
                tertiaryAuthoredAliases = IceMorphAuthoredAttackAliases;
            }

            if (prefersGenericRangedFallback)
            {
                foreach (string genericAlias in EnumerateGenericAttackAliases(morphPart, actionName))
                {
                    if (yielded.Add(genericAlias))
                    {
                        yield return genericAlias;
                    }
                }
            }

            foreach (string authoredAlias in EnumeratePreferredAuthoredAttackAliases(
                         morphPart,
                         actionName,
                         primaryAuthoredAliases))
            {
                if (yielded.Add(authoredAlias))
                {
                    yield return authoredAlias;
                }
            }

            if (prefersArcherAliases)
            {
                foreach (string genericAlias in EnumerateGenericAttackAliases(morphPart, actionName))
                {
                    if (yielded.Add(genericAlias))
                    {
                        yield return genericAlias;
                    }
                }
            }

            foreach (string authoredAlias in EnumeratePreferredAuthoredAttackAliases(
                         morphPart,
                         actionName,
                         secondaryAuthoredAliases))
            {
                if (yielded.Add(authoredAlias))
                {
                    yield return authoredAlias;
                }
            }

            foreach (string authoredAlias in EnumeratePreferredAuthoredAttackAliases(
                         morphPart,
                         actionName,
                         tertiaryAuthoredAliases))
            {
                if (yielded.Add(authoredAlias))
                {
                    yield return authoredAlias;
                }
            }

            foreach (string genericAlias in EnumerateGenericAttackAliases(morphPart, actionName))
            {
                if (yielded.Add(genericAlias))
                {
                    yield return genericAlias;
                }
            }

            foreach (string authoredAlias in EnumerateRemainingPublishedCombatAliases(morphPart, actionName))
            {
                if (yielded.Add(authoredAlias))
                {
                    yield return authoredAlias;
                }
            }
        }

        private static IEnumerable<string> EnumerateGenericMeleeAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || !IsGenericMeleeAttackAction(actionName))
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in EnumerateClientPublishedGenericMeleeFallbackAliases(morphPart, actionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (string candidate in CharacterPart.GetActionLookupStrings(actionName))
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && IsPublishedGenericMeleeAttackAlias(candidate)
                    && HasPublishedAction(morphPart, candidate))
                {
                    yielded.Add(candidate);
                    yield return candidate;
                }
            }

            foreach (string candidate in EnumeratePublishedGenericMeleeFallbackSurface(morphPart, actionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (string candidate in EnumeratePublishedCrossFamilyMeleeFallbackSurface(morphPart, actionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateClientPublishedGenericMeleeFallbackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (!ClientPublishedGenericMorphFallbackAliases.TryGetValue(actionName, out string[] aliases)
                || aliases == null)
            {
                yield break;
            }

            foreach (string alias in aliases)
            {
                foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                {
                    yield return resolvedAlias;
                }
            }
        }

        private static IEnumerable<string> EnumeratePublishedGenericMeleeFallbackSurface(CharacterPart morphPart, string requestedActionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(requestedActionName))
            {
                yield break;
            }

            foreach (string candidate in EnumeratePublishedActionNames(morphPart))
            {
                if (IsPublishedGenericMeleeAttackAlias(candidate)
                    && IsMatchingGenericMeleeFamily(requestedActionName, candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static bool IsMatchingGenericMeleeFamily(string requestedActionName, string candidateActionName)
        {
            if (string.IsNullOrWhiteSpace(requestedActionName) || string.IsNullOrWhiteSpace(candidateActionName))
            {
                return false;
            }

            if (string.Equals(requestedActionName, "proneStab", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(candidateActionName, "proneStab", StringComparison.OrdinalIgnoreCase);
            }

            bool requestedIsSwing = requestedActionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!requestedIsSwing && IsClientPublishedMeleeMorphFallbackAction(requestedActionName))
            {
                requestedIsSwing = true;
            }

            bool candidateIsSwing = candidateActionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0;
            if (requestedIsSwing || candidateIsSwing)
            {
                return requestedIsSwing && candidateIsSwing;
            }

            bool requestedIsStab = requestedActionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0;
            bool candidateIsStab = candidateActionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0;
            return requestedIsStab && candidateIsStab;
        }

        private static IEnumerable<string> EnumeratePublishedCrossFamilyMeleeFallbackSurface(CharacterPart morphPart, string requestedActionName)
        {
            if (morphPart?.Animations == null
                || !IsClientPublishedStabMorphFallbackAction(requestedActionName))
            {
                yield break;
            }

            // Client s_sMorphAction still requests stab-family raw names while common
            // Morph/*.img variants such as 1003/1103 only publish generic swing branches.
            foreach (string candidate in EnumeratePublishedActionNames(morphPart))
            {
                if (candidate.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return candidate;
                }
            }
        }

        private static bool IsClientPublishedStabMorphFallbackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            foreach (string alias in ClientPublishedMorphStabFallbackAliases)
            {
                if (string.Equals(alias, actionName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PrefersArcherAttackAliases(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "piercing", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "crossPiercing", StringComparison.OrdinalIgnoreCase)
                   || actionName.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("eruption", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool PrefersGenericRangedFallbackAliases(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            // CAvatar::MoveAction2RawAction still promotes attackable morph move-action 18
            // to raw action 42 (`paralyze`), while s_sMorphAction also exposes generic
            // shot-family raw names like `shoot6`, `shotC1`, `shootDb1`, `jShot`, and
            // `speedDualShot`. Current Morph/*.img archer surfaces publish generic
            // `shoot*` nodes for those non-authored requests rather than verbatim
            // branches or archer-only authored aliases like `windshot`.
            return string.Equals(actionName, "paralyze", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "shoot6", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "shotC1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "shootDb1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "jShot", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "speedDualShot", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> EnumerateGenericAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (!IsGenericRangedAttackAction(actionName))
            {
                yield break;
            }

            bool prefersPublishedRangedMorphFallback =
                string.Equals(actionName, "arrowEruption", StringComparison.OrdinalIgnoreCase);

            if (prefersPublishedRangedMorphFallback)
            {
                foreach (string alias in ClientPublishedRangedMorphFallbackAliases)
                {
                    foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                    {
                        yield return resolvedAlias;
                    }
                }
            }

            foreach (string alias in GenericMorphRangedAttackAliases)
            {
                foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                {
                    yield return resolvedAlias;
                }
            }

            if (!prefersPublishedRangedMorphFallback)
            {
                foreach (string alias in ClientPublishedRangedMorphFallbackAliases)
                {
                    foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                    {
                        yield return resolvedAlias;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumeratePresentAliases(CharacterPart morphPart, IEnumerable<string> aliases)
        {
            if (morphPart?.Animations == null || aliases == null)
            {
                yield break;
            }

            foreach (string alias in aliases)
            {
                foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                {
                    yield return resolvedAlias;
                }
            }
        }

        private static IEnumerable<string> EnumerateClientOrderedPresentAliases(
            CharacterPart morphPart,
            IEnumerable<string> aliases)
        {
            if (morphPart?.Animations == null || aliases == null)
            {
                yield break;
            }

            foreach (var entry in aliases
                         .Where(alias => !string.IsNullOrWhiteSpace(alias) && HasPublishedAction(morphPart, alias))
                         .Select((alias, index) => new
                         {
                             Alias = alias,
                             Index = index,
                             RawActionCode = GetClientMorphActionCodeOrDefault(alias)
                         })
                         .OrderBy(entry => entry.RawActionCode)
                         .ThenBy(entry => entry.Index))
            {
                yield return entry.Alias;
            }
        }

        private static IEnumerable<string> EnumeratePreferredAuthoredAttackAliases(
            CharacterPart morphPart,
            string requestedActionName,
            IEnumerable<string> aliases)
        {
            if (morphPart?.Animations == null || aliases == null)
            {
                yield break;
            }

            foreach (var aliasEntry in aliases
                         .Where(alias => !string.IsNullOrWhiteSpace(alias) && HasPublishedAction(morphPart, alias))
                         .Select((alias, index) => new
                         {
                             Alias = alias,
                             Index = index,
                             Score = GetRequestedAuthoredAliasScore(requestedActionName, alias),
                             RawActionCode = GetClientMorphActionCodeOrDefault(alias)
                          })
                          .OrderByDescending(entry => entry.Score)
                         .ThenBy(entry => entry.RawActionCode)
                         .ThenBy(entry => entry.Index))
            {
                yield return aliasEntry.Alias;
            }
        }

        private static int GetRequestedAuthoredAliasScore(string requestedActionName, string authoredAlias)
        {
            if (string.IsNullOrWhiteSpace(requestedActionName) || string.IsNullOrWhiteSpace(authoredAlias))
            {
                return 0;
            }

            string normalizedRequestedAction = requestedActionName.Trim();
            string normalizedAuthoredAlias = authoredAlias.Trim();

            if (string.Equals(normalizedRequestedAction, normalizedAuthoredAlias, StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }

            if (string.Equals(normalizedRequestedAction, "arrowEruption", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedAuthoredAlias, "arrowRain", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (string.Equals(normalizedRequestedAction, "stormbreak", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedAuthoredAlias, "stormbreak", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (string.Equals(normalizedRequestedAction, "windspear", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedAuthoredAlias, "windspear", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (ContainsIgnoreCase(normalizedRequestedAction, "shot")
                && string.Equals(normalizedAuthoredAlias, "windshot", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (ContainsIgnoreCase(normalizedRequestedAction, "rain")
                && string.Equals(normalizedAuthoredAlias, "arrowRain", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            return 0;
        }

        private static bool ContainsIgnoreCase(string text, string fragment)
        {
            return !string.IsNullOrWhiteSpace(text)
                   && !string.IsNullOrWhiteSpace(fragment)
                   && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> EnumerateClientPublishedAuthoredAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (!ClientPublishedAuthoredMorphFallbackAliases.TryGetValue(actionName, out string[] aliases)
                || aliases == null)
            {
                yield break;
            }

            foreach (string alias in aliases)
            {
                foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                {
                    yield return resolvedAlias;
                }
            }
        }

        private static IEnumerable<string> EnumeratePresentAlertAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            string[] allAlertAliases = { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" };
            if (!TryParseAlertActionIndex(actionName, out int requestedAlertIndex))
            {
                foreach (string alias in EnumeratePresentAliases(morphPart, allAlertAliases))
                {
                    yield return alias;
                }

                yield break;
            }

            // Keep the requested indexed alert family nearest-first when the concrete
            // branch does not exist in Morph/*.img and the resolver falls back.
            foreach (string alias in allAlertAliases
                         .OrderBy(alias =>
                         {
                             if (!TryParseAlertActionIndex(alias, out int aliasIndex))
                             {
                                 return int.MaxValue;
                             }

                             return Math.Abs(aliasIndex - requestedAlertIndex);
                         })
                         .ThenByDescending(alias =>
                             TryParseAlertActionIndex(alias, out int aliasIndex) ? aliasIndex : int.MinValue))
            {
                foreach (string resolvedAlias in EnumeratePublishedAliasLookupMatches(morphPart, alias))
                {
                    yield return resolvedAlias;
                }
            }
        }

        private static IEnumerable<string> EnumeratePublishedAliasLookupMatches(CharacterPart morphPart, string alias)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(alias))
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string lookupAlias in CharacterPart.GetActionLookupStrings(alias))
            {
                if (!string.IsNullOrWhiteSpace(lookupAlias)
                    && HasPublishedAction(morphPart, lookupAlias)
                    && yielded.Add(lookupAlias))
                {
                    yield return lookupAlias;
                }
            }
        }

        private static bool TryParseAlertActionIndex(string actionName, out int alertIndex)
        {
            alertIndex = 0;
            if (string.IsNullOrWhiteSpace(actionName)
                || !actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(actionName, "alert", StringComparison.OrdinalIgnoreCase))
            {
                alertIndex = 1;
                return true;
            }

            string suffix = actionName["alert".Length..];
            if (!int.TryParse(suffix, out int parsedIndex) || parsedIndex < 1)
            {
                return false;
            }

            alertIndex = parsedIndex;
            return true;
        }

        private static bool ShouldEnumerateDoubleJumpAliases(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            // Keep jump-special promotion tied to the currently confirmed client request
            // surface instead of widening every future `*DoubleJump` string into the
            // morph-owned double-jump family without evidence. Skill rows publish
            // `iceDoubleJump`, the client table publishes `slayerDoubleJump`, and the
            // checked morph images publish only archer/ice/iceman-authored double jumps.
            return string.Equals(actionName, "doubleJump", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "slayerDoubleJump", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "iceDoubleJump", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "archerDoubleJump", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> EnumerateDoubleJumpAliases(CharacterPart morphPart)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in PublishedDoubleJumpAliases)
            {
                if (HasPublishedAction(morphPart, actionName) && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in EnumeratePublishedActionNames(morphPart)
                         .Where(actionName => actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0)
                         .OrderBy(actionName => actionName, StringComparer.OrdinalIgnoreCase))
            {
                if (yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            if (HasPublishedAction(morphPart, "jump") && yielded.Add("jump"))
            {
                yield return "jump";
            }
        }

        private static IEnumerable<string> EnumerateRemainingPublishedCombatAliases(CharacterPart morphPart, string requestedActionName)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in EnumerateOrderedPublishedCombatAliases(morphPart, requestedActionName))
            {
                if (yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in EnumeratePublishedActionNames(morphPart)
                         .Where(IsHeuristicCombatAlias)
                         .OrderBy(actionName => actionName, StringComparer.OrdinalIgnoreCase))
            {
                if (yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }
        }

        private static IEnumerable<string> EnumerateOrderedPublishedCombatAliases(CharacterPart morphPart, string requestedActionName)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            IEnumerable<string>[] orderedFamilies;
            if (PrefersArcherAttackAliases(requestedActionName))
            {
                orderedFamilies =
                [
                    ArcherMorphAuthoredAttackAliases,
                    GenericMorphRangedAttackAliases,
                    PirateMorphAuthoredAttackAliases,
                    IceMorphAuthoredAttackAliases
                ];
            }
            else if (PrefersGenericRangedFallbackAliases(requestedActionName))
            {
                orderedFamilies =
                [
                    GenericMorphRangedAttackAliases,
                    ArcherMorphAuthoredAttackAliases,
                    PirateMorphAuthoredAttackAliases,
                    IceMorphAuthoredAttackAliases
                ];
            }
            else if (PrefersIceAttackAliases(morphPart, requestedActionName))
            {
                orderedFamilies =
                [
                    IceMorphAuthoredAttackAliases,
                    PirateMorphAuthoredAttackAliases,
                    ArcherMorphAuthoredAttackAliases,
                    GenericMorphRangedAttackAliases
                ];
            }
            else
            {
                orderedFamilies =
                [
                    PirateMorphAuthoredAttackAliases,
                    ArcherMorphAuthoredAttackAliases,
                    IceMorphAuthoredAttackAliases,
                    GenericMorphRangedAttackAliases
                ];
            }

            foreach (IEnumerable<string> familyAliases in orderedFamilies)
            {
                foreach (string actionName in EnumerateClientOrderedPresentAliases(morphPart, familyAliases))
                {
                    yield return actionName;
                }
            }
        }

        private static bool IsHeuristicCombatAlias(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName)
                || IsStandardMorphActionName(actionName))
            {
                return false;
            }

            return actionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("leap", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("smash", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("panic", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("chop", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("tempest", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("strike", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("burst", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("drain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("orb", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("wave", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("upper", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spin", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("demolition", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("snatch", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "fist", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "screw", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "straight", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "somersault", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStandardMorphActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return true;
            }

            return string.Equals(actionName, "walk", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "stand1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "stand2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "fly", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "fly2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "fly2Move", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "fly2Skill", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "sit", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "prone", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "swim", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "recovery", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "dead", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "pvpko", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGenericMorphAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
                   || IsClientPublishedAuthoredMorphFallbackAction(actionName)
                   || IsClientPublishedMeleeMorphFallbackAction(actionName)
                   || IsGenericMeleeAttackAction(actionName)
                   || IsGenericRangedAttackAction(actionName);
        }

        private static bool IsClientPublishedAuthoredMorphFallbackAction(string actionName)
        {
            return HasClientPublishedAuthoredMorphFallbackAliases(actionName);
        }

        private static bool HasClientPublishedAuthoredMorphFallbackAliases(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && ClientPublishedAuthoredMorphFallbackAliases.ContainsKey(actionName);
        }

        private static bool KeepsClientPublishedMorphAliasInsideMappedFamily(string actionName)
        {
            // `shot` is the one currently evidenced cross-family request: pirate morphs
            // publish `doublefire`, while archer morphs publish `windshot`.
            return !string.Equals(actionName, "shot", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGenericMeleeAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase)
                   || IsClientPublishedMeleeMorphFallbackAction(actionName);
        }

        private static bool IsClientPublishedMeleeMorphFallbackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            // Keep this in sync with the client-confirmed generic melee alias map so
            // newly evidenced raw requests stay inside the morph-owned melee fallback
            // surface without duplicating the action-name list in two places.
            return ClientPublishedGenericMorphFallbackAliases.ContainsKey(actionName);
        }

        private static bool IsPublishedGenericMeleeAttackAlias(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGenericRangedAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "shoot6", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "paralyze", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "arrowEruption", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PrefersIceAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (!actionName.Contains("attack", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool hasIceAliases = HasAnyPublishedAction(morphPart, IceMorphAuthoredAttackAliases);
            bool hasPirateAliases = HasAnyPublishedAction(morphPart, PirateMorphAuthoredAttackAliases);
            bool hasArcherAliases = HasAnyPublishedAction(morphPart, ArcherMorphAuthoredAttackAliases);
            return hasIceAliases && !hasPirateAliases && !hasArcherAliases;
        }

        private static IEnumerable<string> EnumeratePublishedActionNames(CharacterPart morphPart)
        {
            if (morphPart == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string actionName in morphPart.Animations?.Keys ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in morphPart.AvailableAnimations ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName))
                {
                    yield return actionName;
                }
            }
        }

        private static bool HasPublishedAction(CharacterPart morphPart, string actionName)
        {
            if (morphPart == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return (morphPart.Animations?.ContainsKey(actionName) == true)
                   || (morphPart.AvailableAnimations?.Contains(actionName) == true);
        }

        private static bool HasAnyPublishedAction(CharacterPart morphPart, IEnumerable<string> actionNames)
        {
            if (morphPart == null || actionNames == null)
            {
                return false;
            }

            foreach (string actionName in actionNames)
            {
                if (HasPublishedAction(morphPart, actionName))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetClientMorphActionCodeOrDefault(string actionName)
        {
            if (!IsClientConfirmedMorphActionName(actionName))
            {
                return int.MaxValue;
            }

            return CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                ? rawActionCode
                : int.MaxValue;
        }

        private static bool IsClientConfirmedMorphActionName(string actionName)
        {
            if (!CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
            {
                return false;
            }

            return rawActionCode >= 0
                   && rawActionCode < ClientMorphActionTableExclusiveUpperBound
                   && rawActionCode != ClientMorphActionTableSkippedRawActionCode;
        }
    }
}
