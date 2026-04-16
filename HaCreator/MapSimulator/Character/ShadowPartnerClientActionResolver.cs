using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character
{
    internal static class ShadowPartnerClientActionResolver
    {
        internal readonly record struct ShadowPartnerActionPiece(
            int SlotIndex,
            string PieceActionName,
            int? SourceFrameIndex,
            int? DelayOverrideMs = null,
            bool Flip = false,
            Point? Move = null,
            int RotationDegrees = 0);

        private static readonly string[] SwingHeuristicFragments =
        {
            "swing",
            "doubleswing",
            "tripleswing",
            "smash",
            "panic",
            "chop",
            "tempest",
            "strike",
            "wave",
            "upper",
            "spin",
            "demolition",
            "snatch",
            "shockwave",
            "dragonstrike",
            "backspin",
            "doubleupper",
            "screw",
            "straight",
            "somersault",
            "fist"
        };

        private static readonly string[] StabHeuristicFragments =
        {
            "stab",
            "pierce",
            "thrust",
            "assaulter",
            "assassination",
            "savage",
            "showdown"
        };

        private static readonly string[] RangedHeuristicFragments =
        {
            "shoot",
            "shot",
            "arrow",
            "rain",
            "orb",
            "fire",
            "burst",
            "drain",
            "spear",
            "windshot",
            "windspear",
            "stormbreak",
            "arrowrain",
            "eburster",
            "edrain",
            "eorb",
            "ninjastorm",
            "vampire"
        };

        private static readonly IReadOnlyDictionary<string, string[]> SharedAliasMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["move"] = new[] { "walk1", "walk2" },
                ["walk"] = new[] { "walk1", "walk2" },
                ["stand"] = new[] { "stand1", "stand2" },
                ["ghostwalk"] = new[] { "walk1", "walk2", "stand1", "stand2" },
                ["ghoststand"] = new[] { "stand1", "stand2" },
                ["ghostjump"] = new[] { "jump", "fly", "stand1", "stand2" },
                ["ghostladder"] = new[] { "ladder", "rope", "stand1", "stand2" },
                ["ghostrope"] = new[] { "rope", "ladder", "stand1", "stand2" },
                ["ghostprone"] = new[] { "prone", "proneStab", "stand1", "stand2" },
                ["ghostpronestab"] = new[] { "proneStab", "prone", "stand1", "stand2" },
                ["ghost"] = new[] { "stand1", "stand2", "dead" },
                // action_mapping_for_ghost@0x406500 remaps the ghost heal raw action
                // onto raw action 48 before LoadShadowPartnerAction falls back to the
                // plain action-name lookup, so keep `heal` ahead of idle fallback here too.
                ["ghostheal"] = new[] { "heal", "stand1", "stand2" },
                // `special/*` does not publish ghost or fly2/swim-specific branches for the
                // confirmed Shadow Partner skills, so keep the client-shaped stand/fly/jump
                // collapse ahead of broader fallback when those raw-action families surface.
                ["ghostfly"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["ghostsit"] = new[] { "sit", "stand1", "stand2" },
                ["hit"] = new[] { "alert", "stand1", "stand2" },
                ["dead"] = new[] { "dead", "stand1" },
                ["swim"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2Move"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2Skill"] = new[] { "stand1", "stand2", "fly", "jump" },
                // The Shadow Partner jobs still publish non-generic raw action names in WZ:
                // `Skill/411.img/skill/4111005/action/0 = avenger`,
                // `Skill/1411.img/skill/14111002/action/0 = avenger`,
                // `Skill/421.img/skill/4211002/action/0 = assaulter`,
                // and `Skill/421.img/skill/4211006/action/0 = prone2`.
                // `special/*` only authors generic shoot/stab/prone families, so mirror the
                // client-owned alias seam here before falling back to the plain raw action name.
                ["avenger"] = new[] { "shoot1", "shoot2", "shootF" },
                ["assaulter"] = new[] { "stabO1", "stabO2", "stabOF" },
                // `get_action_name_from_code(182) = flyingAssaulter` is client-owned rather
                // than WZ-authored in the mounted helper `action/0` rows, but it still resolves
                // to the same helper branch family as `assaulter`.
                ["flyingAssaulter"] = new[] { "assaulter", "stabO1", "stabO2", "stabOF" },
                ["prone2"] = new[] { "prone", "proneStab", "stand1", "stand2" },
                // Additional thief/night-walker helper raw action names recovered from WZ:
                // `Skill/420.img/skill/4201005/action/0 = savage`,
                // `Skill/412.img/skill/4121003/action/0 = showdown`,
                // `Skill/422.img/skill/4221001/action/0 = assassination`,
                // `Skill/422.img/skill/4221006/action/0 = smokeshell`,
                // `Skill/412.img/skill/4121008/action/0 = ninjastorm`,
                // and `Skill/1410.img/skill/14101006/action/0 = vampire`.
                // Shadow Partner still only publishes generic `special/*` families, so keep these
                // on client-owned helper rows before the plain raw-action fallback.
                ["savage"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["showdown"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["assassination"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["assassinationS"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["assassinations"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["ninjastorm"] = new[] { "shoot1", "shoot2", "shootF" },
                ["vampire"] = new[] { "shoot1", "shoot2", "shootF" },
                ["smokeshell"] = new[] { "alert", "stand1", "stand2" },
                // Client raw actions still include broader attack families such as the
                // dual-blade, polearm, and crossbow-specific aliases below. Shadow
                // Partner only authors the generic `special/*` families, so keep
                // collapsing these raw names onto those authored branches before
                // broader fallback.
                ["stabD1"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["swingD1"] = new[] { "swingO1", "swingO2", "swingO3", "swingOF" },
                ["swingD2"] = new[] { "swingO1", "swingO2", "swingO3", "swingOF" },
                ["doubleSwing"] = new[] { "swingP1", "swingP2", "swingPF" },
                ["tripleSwing"] = new[] { "swingP1", "swingP2", "swingPF" },
                ["shotC1"] = new[] { "shoot1", "shoot2", "shootF" }
            };

        private static readonly IReadOnlyDictionary<string, ShadowPartnerActionPiece[]> PiecedShadowPartnerActionPlans =
            new Dictionary<string, ShadowPartnerActionPiece[]>(StringComparer.OrdinalIgnoreCase)
            {
                // CActionMan::Init builds the helper table from Character/00002000.img
                // action-piece rows before LoadShadowPartnerAction copies source canvases.
                ["avenger"] = CreateIndexedPieces(
                    ("swingO3", 0, -720),
                    ("swingO3", 1, 60),
                    ("swingO3", 2, 60)),
                ["assaulter"] = CreateIndexedPieces(
                    ("swingO1", 0, -600),
                    ("swingOF", 3, 600)),
                ["flyingAssaulter"] = CreateIndexedPieces(
                    ("swingPF", 2, 180, false),
                    ("swingOF", 3, 90, false),
                    ("swingOF", 3, 90, false),
                    ("stabT1", 2, 90, false),
                    ("stabT1", 2, 120, true),
                    ("stabT1", 2, 450, false),
                    ("swingO2", 0, 360, false)),
                ["savage"] = CreateIndexedPieces(
                    ("stabO1", 0, -120),
                    ("swingOF", 3, -120),
                    ("proneStab", 0, -120),
                    ("swingO1", 2, 120),
                    ("swingO2", 0, 120),
                    ("stabOF", 2, 120),
                    ("alert", 1, 120),
                    ("swingO3", 2, 120)),
                ["showdown"] = CreateIndexedPieces(
                    ("proneStab", 0, -900),
                    ("swingOF", 0, -90),
                    ("swingOF", 1, -90),
                    ("swingOF", 2, -90),
                    ("swingOF", 3, 180)),
                ["assassination"] = CreateIndexedPieces(
                    ("stand1", 0, -540),
                    ("swingOF", 0, -80),
                    ("swingOF", 1, -80),
                    ("swingOF", 2, 80),
                    ("swingOF", 3, 320),
                    ("swingO3", 0, 80),
                    ("swingO3", 1, 80),
                    ("swingO3", 2, 80),
                    ("swingO2", 0, 80),
                    ("swingO2", 1, 80),
                    ("swingO2", 2, 320)),
                // `get_action_name_from_code(62)` resolves to `assassinationS` in the client
                // action table; keep the earlier compatibility alias too until the simulator
                // no longer carries call sites or saved state that can surface the older name.
                ["assassinationS"] = CreateIndexedPieces(
                    ("stabOF", 0, 60),
                    ("stabOF", 2, 180),
                    ("stabOF", 2, 180)),
                ["assassinations"] = CreateIndexedPieces(
                    ("stabOF", 0, 60),
                    ("stabOF", 2, 180),
                    ("stabOF", 2, 180)),
                ["ninjastorm"] = CreateIndexedPieces(
                    ("alert", 0, -480),
                    ("alert", 1, 210),
                    ("alert", 2, 240)),
                ["vampire"] = CreateIndexedPieces(
                    ("alert", 0, -450),
                    ("alert", 1, -450),
                    ("alert", 2, -450)),
                // `Skill/421.img/skill/4211006/action/0 = prone2`; the loader
                // disassembly shows hidden piece entries carry source frame slots.
                ["prone2"] = CreateIndexedPieces(
                    ("proneStab", 0, -700),
                    ("proneStab", 0, 300)),
                // `Character/00002000.img` publishes full helper-piece rows for these
                // indexed alert aliases, including per-piece frame delays.
                ["alert2"] = CreateIndexedPieces(
                    ("alert", 0, 200),
                    ("alert", 1, 200),
                    ("alert", 2, 200)),
                ["alert3"] = CreateIndexedPieces(
                    ("alert", 0, -500),
                    ("alert", 1, -500),
                    ("alert", 2, 500)),
                ["alert4"] = CreateIndexedPieces(
                    ("alert", 0, -300),
                    ("alert", 1, -300),
                    ("alert", 2, 300)),
                ["alert5"] = CreateIndexedPieces(
                    ("alert", 0, -300),
                    ("alert", 1, 300),
                    ("alert", 2, 300)),
                ["alert6"] = CreateIndexedPieces(
                    ("alert", 0, 330),
                    ("alert", 1, 330),
                    ("alert", 2, 330)),
                ["alert7"] = CreateIndexedPieces(
                    ("alert", 0, 360),
                    ("alert", 1, 300)),
                // `Character/00002000.img/alert8/0` is another mounted indexed-alert
                // helper row; it reuses the authored jump helper frame with its own delay.
                ["alert8"] = CreateIndexedPieces(
                    ("jump", 0, 30)),
                // The mounted character action table also keeps `ladder2` and `rope2`
                // as two-step helper rows instead of a single frame remap.
                ["ladder2"] = CreateIndexedPieces(
                    ("ladder", 0, 300),
                    ("ladder", 1, 300)),
                ["rope2"] = CreateIndexedPieces(
                    ("rope", 0, 300),
                    ("rope", 1, 300)),
                // `Skill/422.img/skill/4221006/action/0 = smokeshell`, and
                // `Character/00002000.img/smokeshell/*` shows the exact helper-piece
                // transition from `swingOF` into `alert` with authored delays.
                ["smokeshell"] = CreateIndexedPieces(
                    ("swingOF", 0, -120),
                    ("swingOF", 1, -120),
                    ("swingOF", 2, -120),
                    ("swingOF", 3, -180),
                    ("alert", 0, -360),
                    ("alert", 1, 270),
                    ("alert", 2, 270)),
                // The mounted `fly2*` rows are not WZ-authored Shadow Partner branches,
                // but their source actions are part of the generic helper surface.
                ["fly2"] = CreateIndexedPieces(
                    ("fly", 1)),
                ["fly2Move"] = CreateIndexedPieces(
                    ("fly", 0)),
                ["fly2Skill"] = CreateIndexedPieces(
                    ("alert", 0, -150),
                    ("stabTF", 1, -210)),
                ["stabD1"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 0, -180),
                    CreateIndexedPiece(1, "stabO1", 1, 240),
                    CreateIndexedPiece(2, "swingO3", 2, 150, move: new Point(15, 0)),
                    CreateIndexedPiece(3, "stabT1", 2, 330, move: new Point(5, 0))
                },
                ["swingD1"] = new[]
                {
                    CreateIndexedPiece(0, "swingO2", 0, -180),
                    CreateIndexedPiece(1, "swingO2", 2, 300),
                    CreateIndexedPiece(2, "swingO2", 1, 150),
                    CreateIndexedPiece(3, "swingOF", 3, 300, move: new Point(21, 0))
                },
                ["swingD2"] = new[]
                {
                    CreateIndexedPiece(0, "swingO2", 0, -180),
                    CreateIndexedPiece(1, "swingO2", 2, 300, move: new Point(-5, 0)),
                    CreateIndexedPiece(2, "swingO2", 1, 120, move: new Point(-10, 0)),
                    CreateIndexedPiece(3, "swingOF", 0, 330, move: new Point(-15, 0))
                },
                ["doubleSwing"] = new[]
                {
                    CreateIndexedPiece(0, "swingP2", 2, -90, move: new Point(11, -1)),
                    CreateIndexedPiece(1, "swingPF", 0, 90, flip: true, move: new Point(-2, 0)),
                    CreateIndexedPiece(2, "stabTF", 0, 60, flip: true, move: new Point(-53, 11)),
                    CreateIndexedPiece(3, "stabTF", 2, 90, flip: true, move: new Point(-54, 11)),
                    CreateIndexedPiece(4, "stabTF", 2, 90, flip: true, move: new Point(-56, 14))
                },
                ["tripleSwing"] = new[]
                {
                    CreateIndexedPiece(0, "swingPF", 1, -60, move: new Point(-32, 0)),
                    CreateIndexedPiece(1, "proneStab", 0, -60, move: new Point(-55, -31), rotationDegrees: 90),
                    CreateIndexedPiece(2, "proneStab", 0, -60, flip: true, move: new Point(31, -45), rotationDegrees: 90),
                    CreateIndexedPiece(3, "swingPF", 2, -60, move: new Point(-41, -30)),
                    CreateIndexedPiece(4, "swingPF", 2, -90, move: new Point(-45, -36)),
                    CreateIndexedPiece(5, "swingP2", 2, 120, move: new Point(-41, 0)),
                    CreateIndexedPiece(6, "swingP2", 2, 120, move: new Point(-41, 1)),
                    CreateIndexedPiece(7, "swingP2", 2, 90, move: new Point(-41, 2))
                },
                ["shotC1"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -210),
                    CreateIndexedPiece(1, "stabO1", 0, 120, move: new Point(8, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, 120, move: new Point(9, 0)),
                    CreateIndexedPiece(3, "stabO1", 0, 150, move: new Point(10, 0))
                }
            };

        private static readonly HashSet<string> ClientAttackAliasActionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // These raw-action names are not authored as Shadow Partner `special/*`
            // branches, but the client loader maps them onto attack helper rows.
            "avenger",
            "assaulter",
            "flyingAssaulter",
            "prone2",
            "savage",
            "showdown",
            "assassination",
            "assassinationS",
            "assassinations",
            "ninjastorm",
            "vampire",
            "stabD1",
            "swingD1",
            "swingD2",
            "doubleSwing",
            "tripleSwing",
            "shotC1"
        };

        private static readonly HashSet<string> ClientInitializedAttackActionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // CActionMan::Init builds these action-specific helper rows from
            // Character/00002000.img before LoadShadowPartnerAction does its plain
            // action-name fallback. Keep them on the attack/blocking path when the
            // loader has recovered the matching helper animation dynamically.
            "rain",
            "paralyze",
            "shoot6",
            "arrowRain",
            "arrowEruption",
            "magic1",
            "magic2",
            "magic3",
            "magic4",
            "magic5",
            "magic6",
            "explosion",
            "iceStrike",
            "burster1",
            "burster2",
            "rush",
            "rush2",
            "sanctuary",
            "meteor",
            "blizzard",
            "genesis",
            "brandish1",
            "brandish2",
            "chainlightning",
            "blast",
            "straight",
            "handgun",
            "somersault",
            "doublefire",
            "triplefire",
            "doubleupper",
            "eburster",
            "screw",
            "backspin",
            "eorb",
            "dragonstrike",
            "airstrike",
            "edrain",
            "shot",
            "fist",
            "fireburner",
            "coolingeffect",
            "rapidfire",
            "cannon",
            "torpedo",
            "bamboo",
            "wave",
            "blade",
            "souldriver",
            "firestrike",
            "flamegear",
            "stormbreak",
            "shockwave",
            "demolition",
            "snatch",
            "windspear",
            "windshot",
            "swingT2PoleArm",
            "swingP1PoleArm",
            "swingP2PoleArm",
            "combatStep",
            "finalCharge",
            "finalToss",
            "finalBlow",
            "comboSmash",
            "comboFenrir",
            "fullSwingDouble",
            "fullSwingTriple",
            "overSwingDouble",
            "overSwingTriple",
            "rollingSpin",
            "comboTempest",
            "comboJudgement",
            "magicmissile",
            "fireCircle",
            "lightingBolt",
            "dragonBreathe",
            "blaze",
            "illusion",
            "dragonIceBreathe",
            "magicFlare",
            "killingWing",
            "Earthquake",
            "dragonThrust",
            "darkFog",
            "flameWheel",
            "tripleStab",
            "flyingAssaulter",
            "tornadoDash",
            "tornadoRush",
            "tornadoDashStop",
            "fatalBlow",
            "slashStorm1",
            "slashStorm2",
            "bloodyStorm",
            "upperStab",
            "chainPull",
            "chainAttack",
            "monsterBombPrepare",
            "monsterBombThrow",
            "suddenRaid",
            "finalCutPrepare",
            "finalCut",
            "phantomBlow",
            "bladeFury",
            "darkChain",
            "finishAttack",
            "finishAttack_link",
            "finishAttack_link2",
            "tripleBlow",
            "quadBlow",
            "deathBlow",
            "finishBlow",
            "darkLightning",
            "cyclone_pre",
            "cyclone",
            "cyclone_after",
            "lasergun",
            "siege_pre",
            "siege",
            "siege_after",
            "tank_laser",
            "tank_siegepre",
            "tank_siegeattack",
            "tank_siegeafter",
            "tank_mRush",
            "drillrush",
            "giant",
            "rpunch",
            "flashRain",
            "clawCut",
            "mine",
            "braveslash1",
            "braveslash2",
            "braveslash3",
            "braveslash4",
            "chargeBlow"
        };

        private static readonly string[] LoaderSynthesizedRemappedActionNames =
        {
            // The remaining synthesized rows still collapse directly onto one authored
            // helper branch without a recovered multi-piece action row of their own.
        };

        private static readonly HashSet<string> GenericHelperSurfaceActionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "alert2",
            "alert3",
            "alert4",
            "alert5",
            "alert6",
            "alert7",
            "alert8",
            "fly2",
            "fly2Move",
            "fly2Skill"
        };

        private const int ClientInitializedShadowPartnerActionCodeLimitExclusive = 0x111;
        private const int ClientActionManInitVariantRowStartRawActionCode = 124;
        private const int ClientActionManInitVariantRowEndRawActionCode = 131;
        internal const int ClientActionManInitDefaultPieceDelayMs = 150;

        private static readonly HashSet<int> ClientActionManInitSkippedRawActionCodes = new()
        {
            // CActionMan::Init walks get_action_name_from_code(action) for action < 0x111,
            // but skips raw action code 55 before reading Character/00002000.
            55
        };

        private static readonly IReadOnlyDictionary<string, string> SupportedRawActionCanonicalNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["swingO1"] = "swingO1",
                ["avenger"] = "avenger",
                ["assaulter"] = "assaulter",
                ["flyingAssaulter"] = "assaulter",
                ["prone2"] = "prone2",
                ["savage"] = "savage",
                ["showdown"] = "showdown",
                ["assassination"] = "assassination",
                ["assassinationS"] = "assassination",
                ["assassinations"] = "assassination",
                ["smokeshell"] = "smokeshell",
                ["ninjastorm"] = "ninjastorm",
                ["vampire"] = "vampire",
                // These client-only helper raw rows are still not authored directly in the
                // mounted `action/0` WZ surface, so gate them through the nearest recovered
                // family-specific raw row instead of letting every helper family claim them.
                ["shotC1"] = "avenger",
                ["stabD1"] = "assaulter",
                ["swingD1"] = "swingO1",
                ["swingD2"] = "swingO1",
                ["doubleSwing"] = "swingO1",
                ["tripleSwing"] = "swingO1"
            };

        public static IEnumerable<string> EnumerateClientMappedCandidates(
            string playerActionName,
            PlayerState state,
            string fallbackActionName,
            string weaponType = null,
            int? rawActionCode = null,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string rawActionName = null;

            // `LoadShadowPartnerAction` walks action-specific remap rows before it falls back
            // to the plain `get_action_name_from_code(raw)` lookup. Mirror that client order
            // for the alias-backed helper families we have recovered so far instead of only
            // special-casing ghost-family actions.
            if (IsPiecedShadowPartnerActionName(playerActionName)
                && IsSupportedRawActionName(playerActionName, supportedRawActionNames)
                && yielded.Add(playerActionName))
            {
                yield return playerActionName;
            }

            if (ShouldPreferActionSpecificAliasCandidates(playerActionName)
                && IsSupportedRawActionName(playerActionName, supportedRawActionNames))
            {
                foreach (string candidate in EnumerateAliasCandidates(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (rawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out rawActionName)
                && !string.IsNullOrWhiteSpace(rawActionName))
            {
                bool rawActionSupported = IsSupportedRawActionName(rawActionName, supportedRawActionNames);
                bool rawActionUsesPiecedPlan = rawActionSupported && IsPiecedShadowPartnerActionName(rawActionName);
                if (rawActionUsesPiecedPlan && yielded.Add(rawActionName))
                {
                    yield return rawActionName;
                }

                foreach (string candidate in rawActionSupported
                             ? EnumerateActionSpecificAliasCandidates(rawActionName)
                             : Array.Empty<string>())
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                if (rawActionSupported && !rawActionUsesPiecedPlan && yielded.Add(rawActionName))
                {
                    yield return rawActionName;
                }
            }

            bool playerActionFamilyUnsupported = IsFamilyUnsupportedClientRawActionName(playerActionName, supportedRawActionNames);
            if (!string.IsNullOrWhiteSpace(playerActionName))
            {
                if (!playerActionFamilyUnsupported && yielded.Add(playerActionName))
                {
                    yield return playerActionName;
                }

                foreach (string candidate in playerActionFamilyUnsupported
                             ? Array.Empty<string>()
                             : EnumerateAliasCandidates(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                foreach (string candidate in playerActionFamilyUnsupported
                             ? Array.Empty<string>()
                             : EnumerateHeuristicAttackAliases(playerActionName, state, weaponType))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (state is PlayerState.Swimming or PlayerState.Flying)
            {
                foreach (string candidate in new[] { "stand1", "stand2", "fly", "jump" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state is PlayerState.Jumping or PlayerState.Falling)
            {
                foreach (string candidate in new[] { "jump", "fly", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Ladder)
            {
                foreach (string candidate in new[] { "ladder", "rope", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Rope)
            {
                foreach (string candidate in new[] { "rope", "ladder", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Prone)
            {
                foreach (string candidate in new[] { "prone", "proneStab", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Sitting)
            {
                foreach (string candidate in new[] { "sit", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Hit)
            {
                foreach (string candidate in new[] { "alert", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Dead)
            {
                foreach (string candidate in new[] { "dead", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Walking)
            {
                foreach (string candidate in new[] { "walk1", "walk2", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Standing)
            {
                foreach (string candidate in new[] { "stand1", "stand2", "alert" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackActionName) && yielded.Add(fallbackActionName))
            {
                yield return fallbackActionName;
            }
        }

        public static IEnumerable<string> EnumerateClientActionAliases(
            string playerActionName,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            if (IsFamilyUnsupportedClientRawActionName(playerActionName, supportedRawActionNames))
            {
                yield break;
            }

            foreach (string candidate in EnumerateAliasCandidates(playerActionName))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateAliasCandidates(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            if (playerActionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                yield return "alert";
                yield break;
            }

            if (string.Equals(playerActionName, "ladder2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "ladder";
                yield break;
            }

            if (string.Equals(playerActionName, "rope2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "rope";
                yield break;
            }

            if (SharedAliasMap.TryGetValue(playerActionName, out string[] aliases))
            {
                foreach (string alias in aliases)
                {
                    yield return alias;
                }
            }
        }

        public static IReadOnlyList<int> ResolveClientFrameRemap(
            string playerActionName,
            string resolvedActionName,
            int availableFrameCount)
        {
            return ResolveClientFrameRemap(playerActionName, null, resolvedActionName, availableFrameCount);
        }

        public static IReadOnlyList<int> ResolveClientFrameRemap(
            string playerActionName,
            string rawActionName,
            string resolvedActionName,
            int availableFrameCount)
        {
            if (availableFrameCount <= 0
                || string.IsNullOrWhiteSpace(resolvedActionName))
            {
                return Array.Empty<int>();
            }

            IReadOnlyList<int> rawActionFrameRemap = ResolveSingleActionFrameRemap(rawActionName, resolvedActionName, availableFrameCount);
            if (rawActionFrameRemap.Count > 0)
            {
                return rawActionFrameRemap;
            }

            IReadOnlyList<int> playerActionFrameRemap = ResolveSingleActionFrameRemap(playerActionName, resolvedActionName, availableFrameCount);
            if (playerActionFrameRemap.Count > 0)
            {
                return playerActionFrameRemap;
            }

            return Array.Empty<int>();
        }

        public static SkillAnimation ResolvePlaybackAnimation(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string resolvedActionName,
            string playerActionName,
            string rawActionName = null,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            if (actionAnimations == null
                || string.IsNullOrWhiteSpace(resolvedActionName)
                || !actionAnimations.TryGetValue(resolvedActionName, out SkillAnimation baseAnimation)
                || baseAnimation?.Frames == null
                || baseAnimation.Frames.Count == 0)
            {
                return null;
            }

            if (!IsSupportedRawActionName(playerActionName, supportedRawActionNames))
            {
                playerActionName = null;
            }

            if (!IsSupportedRawActionName(rawActionName, supportedRawActionNames))
            {
                rawActionName = null;
            }

            IReadOnlyList<int> frameRemap = ResolveClientFrameRemap(
                playerActionName,
                rawActionName,
                resolvedActionName,
                baseAnimation.Frames.Count);
            if (frameRemap == null || frameRemap.Count == 0)
            {
                return baseAnimation;
            }

            var remappedFrames = new List<SkillFrame>(frameRemap.Count);
            foreach (int requestedIndex in frameRemap)
            {
                int frameIndex = Math.Clamp(requestedIndex, 0, baseAnimation.Frames.Count - 1);
                remappedFrames.Add(baseAnimation.Frames[frameIndex]);
            }

            if (remappedFrames.Count == 0)
            {
                return baseAnimation;
            }

            var remappedAnimation = new SkillAnimation
            {
                Name = baseAnimation.Name,
                Frames = remappedFrames,
                Loop = baseAnimation.Loop,
                Origin = baseAnimation.Origin,
                ZOrder = baseAnimation.ZOrder,
                PositionCode = baseAnimation.PositionCode,
                ClientEventDelayMs = baseAnimation.ClientEventDelayMs
            };
            remappedAnimation.CalculateDuration();
            return remappedAnimation;
        }

        internal static IEnumerable<string> EnumeratePiecedShadowPartnerActionNames()
        {
            foreach (string actionName in PiecedShadowPartnerActionPlans.Keys)
            {
                yield return actionName;
            }
        }

        internal static IEnumerable<string> EnumerateRemappedShadowPartnerActionNames()
        {
            foreach (string actionName in LoaderSynthesizedRemappedActionNames)
            {
                yield return actionName;
            }
        }

        internal static IEnumerable<string> EnumerateCharacterOwnedMountedActionCandidateNames(
            IReadOnlySet<string> supportedRawActionNames)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in EnumeratePiecedShadowPartnerActionNames())
            {
                if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in EnumerateRemappedShadowPartnerActionNames())
            {
                if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            if (supportedRawActionNames == null || supportedRawActionNames.Count == 0)
            {
                yield break;
            }

            foreach (string actionName in supportedRawActionNames)
            {
                if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach ((string actionName, string canonicalActionName) in SupportedRawActionCanonicalNames)
            {
                if (!string.IsNullOrWhiteSpace(actionName)
                    && !string.IsNullOrWhiteSpace(canonicalActionName)
                    && supportedRawActionNames.Contains(canonicalActionName)
                    && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }
        }

        internal static IEnumerable<string> EnumerateMountedShadowPartnerActionCandidateNames(
            IReadOnlySet<string> supportedRawActionNames)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // CActionMan::Init seeds the mounted Shadow Partner helper table by walking
            // raw action codes 0..272 against Character/00002000.img, skipping raw code 55.
            foreach (string actionName in EnumerateClientInitializedShadowPartnerRawActionNames())
            {
                if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in GenericHelperSurfaceActionNames)
            {
                if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in EnumerateCharacterOwnedMountedActionCandidateNames(supportedRawActionNames))
            {
                if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }
        }

        internal static bool IsGenericHelperSurfaceActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (GenericHelperSurfaceActionNames.Contains(actionName))
            {
                return true;
            }

            return TryParseIndexedAlertNumber(actionName, out int indexedAlert)
                   && indexedAlert > 1;
        }

        internal static bool ShouldSynthesizeMountedCharacterActionName(
            string actionName,
            IReadOnlySet<string> supportedRawActionNames)
        {
            if (IsGenericHelperSurfaceActionName(actionName))
            {
                return true;
            }

            if (IsClientInitializedShadowPartnerRawActionName(actionName))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(actionName)
                   && SupportedRawActionCanonicalNames.ContainsKey(actionName)
                   && IsSupportedRawActionName(actionName, supportedRawActionNames);
        }

        internal static bool IsFamilyGatedMountedAliasActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName)
                || !SupportedRawActionCanonicalNames.TryGetValue(actionName, out string canonicalActionName)
                || string.IsNullOrWhiteSpace(canonicalActionName))
            {
                return false;
            }

            return !string.Equals(actionName, canonicalActionName, StringComparison.OrdinalIgnoreCase);
        }

        internal static IEnumerable<string> EnumerateClientInitializedShadowPartnerRawActionNames()
        {
            for (int rawActionCode = 0;
                 rawActionCode < ClientInitializedShadowPartnerActionCodeLimitExclusive;
                 rawActionCode++)
            {
                if (ClientActionManInitSkippedRawActionCodes.Contains(rawActionCode)
                    || !CharacterPart.TryGetActionStringFromCode(rawActionCode, out string actionName)
                    || string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                yield return actionName;
            }
        }

        internal static bool IsClientInitializedShadowPartnerRawActionName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                   && rawActionCode < ClientInitializedShadowPartnerActionCodeLimitExclusive
                   && !ClientActionManInitSkippedRawActionCodes.Contains(rawActionCode);
        }

        private static bool IsClientActionManInitSkippedActionName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                   && ClientActionManInitSkippedRawActionCodes.Contains(rawActionCode);
        }

        internal static WzImageProperty ResolveClientActionManInitPieceOwnerNode(
            string actionName,
            WzImageProperty actionNode)
        {
            if (actionNode == null
                || string.IsNullOrWhiteSpace(actionName)
                || !CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                || rawActionCode < ClientActionManInitVariantRowStartRawActionCode
                || rawActionCode > ClientActionManInitVariantRowEndRawActionCode)
            {
                return actionNode;
            }

            WzImageProperty variantNode = actionNode["1"];
            if (variantNode?.WzProperties == null || variantNode.WzProperties.Count == 0)
            {
                return actionNode;
            }

            // CActionMan::Init switches raw actions 124..131 to child row "1" before
            // reading action-piece metadata. The mounted export used by the simulator is
            // sometimes already flattened, so only descend when "1" is a variant
            // container rather than a piece row with its own `action` property.
            return IsClientActionPieceNode(variantNode)
                ? actionNode
                : variantNode;
        }

        internal static bool IsClientActionPieceNode(WzImageProperty pieceNode)
        {
            return !string.IsNullOrWhiteSpace(pieceNode?["action"]?.GetString());
        }

        internal static SkillAnimation TryBuildPiecedShadowPartnerActionAnimation(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string actionName,
            IReadOnlySet<string> supportedRawActionNames = null,
            IReadOnlyList<ShadowPartnerActionPiece> piecePlanOverride = null,
            bool requireSupportedRawActionName = true)
        {
            if (actionAnimations == null
                || actionAnimations.Count == 0
                || string.IsNullOrWhiteSpace(actionName)
                || (requireSupportedRawActionName && !IsSupportedRawActionName(actionName, supportedRawActionNames)))
            {
                return null;
            }

            IReadOnlyList<ShadowPartnerActionPiece> piecePlan = piecePlanOverride;
            if (piecePlan == null || piecePlan.Count == 0)
            {
                if (!PiecedShadowPartnerActionPlans.TryGetValue(actionName, out ShadowPartnerActionPiece[] builtInPiecePlan)
                    || builtInPiecePlan == null
                    || builtInPiecePlan.Length == 0)
                {
                    return null;
                }

                piecePlan = builtInPiecePlan;
            }

            var frames = new List<SkillFrame>();
            SkillAnimation firstPieceAnimation = null;
            foreach (ShadowPartnerActionPiece piece in piecePlan.OrderBy(static entry => entry.SlotIndex))
            {
                if (string.IsNullOrWhiteSpace(piece.PieceActionName)
                    || !actionAnimations.TryGetValue(piece.PieceActionName, out SkillAnimation pieceAnimation)
                    || pieceAnimation?.Frames == null
                    || pieceAnimation.Frames.Count == 0)
                {
                    continue;
                }

                firstPieceAnimation ??= pieceAnimation;
                if (piece.SourceFrameIndex.HasValue)
                {
                    int frameIndex = Math.Clamp(piece.SourceFrameIndex.Value, 0, pieceAnimation.Frames.Count - 1);
                    SkillFrame frame = CloneSkillFrame(
                        pieceAnimation.Frames[frameIndex],
                        piece.DelayOverrideMs,
                        piece.Flip,
                        piece.Move,
                        piece.RotationDegrees);
                    if (frame != null)
                    {
                        frames.Add(frame);
                    }

                    continue;
                }

                foreach (SkillFrame frame in pieceAnimation.Frames)
                {
                    SkillFrame clonedFrame = CloneSkillFrame(
                        frame,
                        piece.DelayOverrideMs,
                        piece.Flip,
                        piece.Move,
                        piece.RotationDegrees);
                    if (clonedFrame != null)
                    {
                        frames.Add(clonedFrame);
                    }
                }
            }

            if (frames.Count == 0)
            {
                return null;
            }

            var piecedAnimation = new SkillAnimation
            {
                Name = actionName,
                Frames = frames,
                Loop = false,
                Origin = firstPieceAnimation?.Origin ?? Point.Zero,
                ZOrder = firstPieceAnimation?.ZOrder ?? 0,
                PositionCode = firstPieceAnimation?.PositionCode,
                ClientEventDelayMs = ResolveClientActionManInitEventDelayMs(piecePlan)
            };
            piecedAnimation.CalculateDuration();
            return piecedAnimation;
        }

        internal static int ResolveClientActionManInitEventDelayMs(
            IReadOnlyList<ShadowPartnerActionPiece> piecePlan)
        {
            if (piecePlan == null || piecePlan.Count == 0)
            {
                return 0;
            }

            int eventDelayMs = 0;
            foreach (ShadowPartnerActionPiece piece in piecePlan)
            {
                if (!piece.DelayOverrideMs.HasValue || piece.DelayOverrideMs.Value >= 0)
                {
                    continue;
                }

                eventDelayMs += Math.Abs(piece.DelayOverrideMs.Value);
            }

            return eventDelayMs;
        }

        private static SkillFrame CloneSkillFrame(
            SkillFrame sourceFrame,
            int? delayOverrideMs = null,
            bool pieceFlip = false,
            Point? pieceMove = null,
            int pieceRotationDegrees = 0)
        {
            if (sourceFrame == null)
            {
                return null;
            }

            Point origin = sourceFrame.Origin;
            if (pieceMove.HasValue)
            {
                origin = new Point(origin.X - pieceMove.Value.X, origin.Y - pieceMove.Value.Y);
            }

            return new SkillFrame
            {
                Texture = sourceFrame.Texture,
                Origin = origin,
                Delay = delayOverrideMs ?? sourceFrame.Delay,
                Bounds = pieceMove.HasValue
                    ? new Rectangle(-origin.X, -origin.Y, sourceFrame.Bounds.Width, sourceFrame.Bounds.Height)
                    : sourceFrame.Bounds,
                Flip = pieceFlip ? !sourceFrame.Flip : sourceFrame.Flip,
                Z = sourceFrame.Z,
                AlphaStart = sourceFrame.AlphaStart,
                AlphaEnd = sourceFrame.AlphaEnd,
                HasAlphaStart = sourceFrame.HasAlphaStart,
                HasAlphaEnd = sourceFrame.HasAlphaEnd,
                ZoomStart = sourceFrame.ZoomStart,
                ZoomEnd = sourceFrame.ZoomEnd,
                HasZoomStart = sourceFrame.HasZoomStart,
                HasZoomEnd = sourceFrame.HasZoomEnd,
                RotationDegrees = sourceFrame.RotationDegrees + pieceRotationDegrees
            };
        }

        internal static bool TryResolveFrameDrawTransform(
            SkillFrame frame,
            int anchorX,
            int anchorY,
            bool flip,
            out Vector2 position,
            out Vector2 origin,
            out float rotationRadians,
            out SpriteEffects effects)
        {
            position = Vector2.Zero;
            origin = Vector2.Zero;
            rotationRadians = 0f;
            effects = SpriteEffects.None;

            if (frame?.Texture == null)
            {
                return false;
            }

            position = new Vector2(anchorX, anchorY);
            origin = flip
                ? new Vector2(frame.Texture.Width - frame.Origin.X, frame.Origin.Y)
                : new Vector2(frame.Origin.X, frame.Origin.Y);
            rotationRadians = MathHelper.ToRadians(frame.RotationDegrees);
            effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            return true;
        }

        internal static IReadOnlyList<ShadowPartnerActionPiece> GetPiecedShadowPartnerActionPlan(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName)
                || !PiecedShadowPartnerActionPlans.TryGetValue(actionName, out ShadowPartnerActionPiece[] plan)
                || plan == null
                || plan.Length == 0)
            {
                return Array.Empty<ShadowPartnerActionPiece>();
            }

            return Array.AsReadOnly(plan);
        }

        internal static bool IsPiecedShadowPartnerActionName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && PiecedShadowPartnerActionPlans.ContainsKey(actionName);
        }

        internal static SkillAnimation TryBuildRemappedShadowPartnerActionAnimation(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string actionName,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            if (actionAnimations == null
                || actionAnimations.Count == 0
                || string.IsNullOrWhiteSpace(actionName)
                || !IsSupportedRawActionName(actionName, supportedRawActionNames)
                || actionAnimations.ContainsKey(actionName))
            {
                return null;
            }

            foreach (string resolvedActionName in EnumerateActionSpecificAliasCandidates(actionName))
            {
                if (!actionAnimations.TryGetValue(resolvedActionName, out SkillAnimation authoredAnimation)
                    || authoredAnimation?.Frames == null
                    || authoredAnimation.Frames.Count == 0)
                {
                    continue;
                }

                SkillAnimation playbackAnimation = ResolvePlaybackAnimation(
                    actionAnimations,
                    resolvedActionName,
                    actionName,
                    rawActionName: actionName,
                    supportedRawActionNames: supportedRawActionNames);
                if (playbackAnimation?.Frames == null || playbackAnimation.Frames.Count == 0)
                {
                    continue;
                }

                var remappedAnimation = new SkillAnimation
                {
                    Name = actionName,
                    Frames = playbackAnimation.Frames.ToList(),
                    Loop = playbackAnimation.Loop,
                    Origin = playbackAnimation.Origin,
                    ZOrder = playbackAnimation.ZOrder,
                    PositionCode = playbackAnimation.PositionCode,
                    ClientEventDelayMs = playbackAnimation.ClientEventDelayMs
                };
                remappedAnimation.CalculateDuration();
                return remappedAnimation;
            }

            return null;
        }

        public static Point ResolveClientTargetOffset(
            string observedPlayerActionName,
            PlayerState state,
            bool facingRight,
            int sideOffsetPx,
            int backActionOffsetYPx)
        {
            return ResolveClientTargetOffset(
                observedPlayerActionName,
                state,
                facingRight,
                sideOffsetPx,
                backActionOffsetYPx,
                rawActionCode: null,
                hasMorphTransform: false);
        }

        public static Point ResolveClientTargetOffset(
            string observedPlayerActionName,
            PlayerState state,
            bool facingRight,
            int sideOffsetPx,
            int backActionOffsetYPx,
            int? rawActionCode,
            bool hasMorphTransform)
        {
            if (IsClientBackAction(observedPlayerActionName, state, rawActionCode, hasMorphTransform))
            {
                return new Point(0, backActionOffsetYPx);
            }

            return new Point(facingRight ? -sideOffsetPx : sideOffsetPx, 0);
        }

        public static int ResolveHorizontalOffsetPx(SkillAnimation currentAnimation, int baselineOffsetPx)
        {
            int normalizedBaselineOffsetPx = Math.Max(0, baselineOffsetPx);
            if (currentAnimation?.Frames == null || currentAnimation.Frames.Count == 0)
            {
                return normalizedBaselineOffsetPx;
            }

            // Shadow Partner special actions author slightly different first-frame origins per branch.
            // Preserve that authored horizontal cadence instead of pinning every action to the idle spacing.
            int baselineOriginX = normalizedBaselineOffsetPx + 2;
            int actionOriginX = currentAnimation.Frames[0]?.Origin.X ?? baselineOriginX;
            return Math.Max(0, normalizedBaselineOffsetPx + (actionOriginX - baselineOriginX));
        }

        public static Point InterpolateClientOffset(
            Point startOffset,
            Point targetOffset,
            int transitionStartTime,
            int currentTime,
            int transitionDurationMs)
        {
            if (transitionDurationMs <= 0)
            {
                return targetOffset;
            }

            int elapsedTime = Math.Max(0, currentTime - transitionStartTime);
            float progress = MathHelper.Clamp(elapsedTime / (float)transitionDurationMs, 0f, 1f);
            return new Point(
                (int)Math.Round(MathHelper.Lerp(startOffset.X, targetOffset.X, progress)),
                (int)Math.Round(MathHelper.Lerp(startOffset.Y, targetOffset.Y, progress)));
        }

        public static bool ShouldRenderClientShadowPartner(int? skillId, int? rawActionCode)
        {
            if (skillId == SkillData.MirrorImageSkillId)
            {
                return false;
            }

            return rawActionCode != 47;
        }

        public static int ResolveAttackDelayMs(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string actionName,
            SkillAnimation playbackAnimation,
            int defaultDelayMs)
        {
            SkillAnimation animation = playbackAnimation;
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                if (actionAnimations != null
                    && !string.IsNullOrWhiteSpace(actionName)
                    && actionAnimations.TryGetValue(actionName, out SkillAnimation resolvedAnimation))
                {
                    animation = resolvedAnimation;
                }
            }

            if (animation?.Frames != null && animation.Frames.Count > 0)
            {
                if (animation.ClientEventDelayMs.GetValueOrDefault() > 0)
                {
                    return animation.ClientEventDelayMs.Value;
                }

                int frameDelay = ResolvePlaybackFrameDurationMs(animation.Frames[0]?.Delay ?? 0);
                if (frameDelay > 0)
                {
                    return frameDelay;
                }
            }

            return defaultDelayMs;
        }

        public static int ResolveAttackDelayMs(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string actionName,
            int defaultDelayMs)
        {
            return ResolveAttackDelayMs(actionAnimations, actionName, null, defaultDelayMs);
        }

        public static bool TryGetPlaybackFrameAtTime(
            SkillAnimation animation,
            int timeMs,
            out SkillFrame frame,
            out int frameElapsedMs)
        {
            frame = null;
            frameElapsedMs = 0;

            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return false;
            }

            int totalDuration = ResolvePlaybackTotalDurationMs(animation);
            if (totalDuration <= 0)
            {
                frame = animation.Frames[0];
                return true;
            }

            int resolvedTime = animation.Loop
                ? Math.Max(0, timeMs) % totalDuration
                : Math.Min(Math.Max(0, timeMs), totalDuration - 1);
            int elapsed = 0;

            foreach (SkillFrame currentFrame in animation.Frames)
            {
                int frameDuration = ResolvePlaybackFrameDurationMs(currentFrame?.Delay ?? 0);
                elapsed += frameDuration;
                if (resolvedTime < elapsed)
                {
                    frame = currentFrame;
                    frameElapsedMs = resolvedTime - (elapsed - frameDuration);
                    return true;
                }
            }

            frame = animation.Frames[^1];
            frameElapsedMs = ResolvePlaybackFrameDurationMs(frame?.Delay ?? 0) - 1;
            return true;
        }

        public static bool IsPlaybackComplete(SkillAnimation animation, int timeMs)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return true;
            }

            if (animation.Loop)
            {
                return false;
            }

            return Math.Max(0, timeMs) >= ResolvePlaybackTotalDurationMs(animation);
        }

        public static int ResolvePlaybackFrameDurationMs(int frameDelayMs)
        {
            return Math.Max(1, Math.Abs(frameDelayMs));
        }

        private static int ResolvePlaybackTotalDurationMs(SkillAnimation animation)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return 0;
            }

            int totalDuration = 0;
            foreach (SkillFrame frame in animation.Frames)
            {
                totalDuration += ResolvePlaybackFrameDurationMs(frame?.Delay ?? 0);
            }

            return totalDuration;
        }

        internal static string ResolveCreateActionName(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            PlayerState state)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return null;
            }

            foreach (string candidate in EnumerateCreateActionCandidates(state))
            {
                if (actionAnimations.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        internal static IEnumerable<string> EnumerateCreateActionCandidates(PlayerState state)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool airborne = state is PlayerState.Jumping or PlayerState.Falling or PlayerState.Swimming or PlayerState.Flying;
            bool stationary = state is PlayerState.Standing or PlayerState.Walking or PlayerState.Sitting or PlayerState.Prone;

            foreach (string candidate in new[] { "create2", "create3", "create4" })
            {
                string stateVariant = airborne
                    ? candidate + "_f"
                    : stationary
                        ? candidate + "_s"
                        : null;

                if (!string.IsNullOrWhiteSpace(stateVariant) && yielded.Add(stateVariant))
                {
                    yield return stateVariant;
                }

                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }

                string alternateVariant = airborne ? candidate + "_s" : candidate + "_f";
                if (yielded.Add(alternateVariant))
                {
                    yield return alternateVariant;
                }
            }
        }

        public static bool IsBlockingAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && (IsAttackAction(actionName)
                       || actionName.StartsWith("create", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (IsClientActionManInitSkippedActionName(actionName))
            {
                return false;
            }

            return IsAuthoredAttackAction(actionName)
                   || ClientAttackAliasActionNames.Contains(actionName)
                   || ClientInitializedAttackActionNames.Contains(actionName);
        }

        public static bool IsAttackAction(string actionName, int? rawActionCode)
        {
            if (IsAttackAction(actionName))
            {
                return true;
            }

            return rawActionCode.HasValue
                   && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out string rawActionName)
                   && !string.Equals(rawActionName, actionName, StringComparison.OrdinalIgnoreCase)
                   && IsAttackAction(rawActionName);
        }

        private static bool IsAuthoredAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsClientBackAction(string observedPlayerActionName, PlayerState state)
        {
            return IsClientBackAction(
                observedPlayerActionName,
                state,
                rawActionCode: null,
                hasMorphTransform: false);
        }

        public static bool IsClientBackAction(
            string observedPlayerActionName,
            PlayerState state,
            int? rawActionCode,
            bool hasMorphTransform)
        {
            if (state is PlayerState.Ladder or PlayerState.Rope)
            {
                return true;
            }

            if (rawActionCode.HasValue && IsClientBackRawAction(rawActionCode.Value, hasMorphTransform))
            {
                return true;
            }

            return observedPlayerActionName?.ToLowerInvariant() switch
            {
                "ladder" or "ladder2" or "ghostladder" => true,
                "rope" or "rope2" or "ghostrope" => true,
                _ => false
            };
        }

        internal static bool IsClientBackRawAction(int rawActionCode, bool hasMorphTransform)
        {
            if (hasMorphTransform)
            {
                return rawActionCode is 9 or 10;
            }

            return rawActionCode is 45 or 46 or 129 or 130;
        }

        private static bool ShouldPreferActionSpecificAliasCandidates(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                return false;
            }

            if (playerActionName.StartsWith("ghost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (SharedAliasMap.ContainsKey(playerActionName))
            {
                return true;
            }

            return string.Equals(playerActionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(playerActionName, "rope2", StringComparison.OrdinalIgnoreCase)
                   || playerActionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseIndexedAlertFrame(string playerActionName, int availableFrameCount, out int frameIndex)
        {
            frameIndex = 0;
            if (!TryParseIndexedAlertNumber(playerActionName, out int indexedAlert)
                || indexedAlert <= 1)
            {
                return false;
            }

            frameIndex = Math.Clamp(indexedAlert - 1, 0, availableFrameCount - 1);
            return true;
        }

        private static bool TryParseIndexedAlertNumber(string actionName, out int indexedAlert)
        {
            indexedAlert = 0;
            if (string.IsNullOrWhiteSpace(actionName)
                || !actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase)
                || actionName.Length <= "alert".Length)
            {
                return false;
            }

            return int.TryParse(actionName["alert".Length..], out indexedAlert);
        }

        private static IEnumerable<string> EnumerateActionSpecificAliasCandidates(string actionName)
        {
            if (!ShouldPreferActionSpecificAliasCandidates(actionName))
            {
                yield break;
            }

            foreach (string candidate in EnumerateAliasCandidates(actionName))
            {
                yield return candidate;
            }
        }

        public static IEnumerable<string> EnumerateClientIdentityCandidates(
            string playerActionName,
            PlayerState state,
            string weaponType = null,
            int? rawActionCode = null)
        {
            foreach (string candidate in EnumerateHelperIdentityCandidates(
                         playerActionName,
                         state,
                         weaponType,
                         rawActionCode))
            {
                yield return candidate;
            }
        }

        public static IEnumerable<string> EnumerateHelperIdentityCandidates(
            string playerActionName,
            PlayerState state,
            string weaponType = null,
            int? rawActionCode = null,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string rawActionName = null;

            if (IsPiecedShadowPartnerActionName(playerActionName)
                && IsSupportedRawActionName(playerActionName, supportedRawActionNames)
                && yielded.Add(playerActionName))
            {
                yield return playerActionName;
            }

            if (ShouldPreferActionSpecificAliasCandidates(playerActionName)
                && IsSupportedRawActionName(playerActionName, supportedRawActionNames))
            {
                foreach (string candidate in EnumerateAliasCandidates(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (rawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out rawActionName)
                && !string.IsNullOrWhiteSpace(rawActionName))
            {
                bool rawActionSupported = IsSupportedRawActionName(rawActionName, supportedRawActionNames);
                bool rawActionUsesPiecedPlan = rawActionSupported && IsPiecedShadowPartnerActionName(rawActionName);
                if (rawActionUsesPiecedPlan && yielded.Add(rawActionName))
                {
                    yield return rawActionName;
                }

                foreach (string candidate in rawActionSupported
                             ? EnumerateActionSpecificAliasCandidates(rawActionName)
                             : Array.Empty<string>())
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                if (rawActionSupported && !rawActionUsesPiecedPlan && yielded.Add(rawActionName))
                {
                    yield return rawActionName;
                }

                foreach (string candidate in rawActionSupported
                             ? EnumerateHeuristicAttackAliases(rawActionName, state, weaponType)
                             : Array.Empty<string>())
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            bool playerActionFamilyUnsupported = IsFamilyUnsupportedClientRawActionName(playerActionName, supportedRawActionNames);
            if (!playerActionFamilyUnsupported
                && !ShouldSuppressRawBackedGenericAttackIdentityCandidate(playerActionName, rawActionCode)
                && yielded.Add(playerActionName))
            {
                yield return playerActionName;
            }

            foreach (string candidate in playerActionFamilyUnsupported
                         ? Array.Empty<string>()
                         : EnumerateAliasCandidates(playerActionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (string candidate in playerActionFamilyUnsupported
                         ? Array.Empty<string>()
                         : EnumerateHeuristicAttackAliases(playerActionName, state, weaponType))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        internal static bool ShouldSuppressRawBackedGenericAttackIdentityCandidate(
            string playerActionName,
            int? rawActionCode)
        {
            if (string.IsNullOrWhiteSpace(playerActionName)
                || !rawActionCode.HasValue
                || !playerActionName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                || !CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out string rawActionName)
                || string.IsNullOrWhiteSpace(rawActionName))
            {
                return false;
            }

            return !string.Equals(playerActionName, rawActionName, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<int> ResolveSingleActionFrameRemap(
            string actionName,
            string resolvedActionName,
            int availableFrameCount)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return Array.Empty<int>();
            }

            if (string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                && string.Equals(resolvedActionName, "ladder", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { Math.Min(1, availableFrameCount - 1) };
            }

            if (string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase)
                && string.Equals(resolvedActionName, "rope", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { Math.Min(1, availableFrameCount - 1) };
            }

            if (string.Equals(actionName, "prone2", StringComparison.OrdinalIgnoreCase)
                && string.Equals(resolvedActionName, "prone", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { Math.Min(1, availableFrameCount - 1) };
            }

            if (actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase)
                && string.Equals(resolvedActionName, "alert", StringComparison.OrdinalIgnoreCase)
                && TryParseIndexedAlertFrame(actionName, availableFrameCount, out int alertFrameIndex))
            {
                return new[] { alertFrameIndex };
            }

            return Array.Empty<int>();
        }

        private static bool IsSupportedRawActionName(
            string actionName,
            IReadOnlySet<string> supportedRawActionNames)
        {
            if (string.IsNullOrWhiteSpace(actionName)
                || supportedRawActionNames == null
                || supportedRawActionNames.Count == 0)
            {
                return true;
            }

            if (IsGenericHelperSurfaceActionName(actionName))
            {
                return true;
            }

            if (!SupportedRawActionCanonicalNames.TryGetValue(actionName, out string canonicalActionName))
            {
                return true;
            }

            return supportedRawActionNames.Contains(canonicalActionName);
        }

        internal static bool IsSupportedRawActionForFamily(
            string actionName,
            IReadOnlySet<string> supportedRawActionNames)
        {
            return IsSupportedRawActionName(actionName, supportedRawActionNames);
        }

        private static bool IsFamilyUnsupportedClientRawActionName(
            string actionName,
            IReadOnlySet<string> supportedRawActionNames)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && supportedRawActionNames != null
                   && supportedRawActionNames.Count > 0
                   && SupportedRawActionCanonicalNames.ContainsKey(actionName)
                   && !IsSupportedRawActionName(actionName, supportedRawActionNames);
        }

        private static IEnumerable<string> EnumerateHeuristicAttackAliases(
            string playerActionName,
            PlayerState state,
            string weaponType)
        {
            if (!IsHeuristicAttackAction(playerActionName))
            {
                yield break;
            }

            bool floating = state is PlayerState.Jumping or PlayerState.Falling or PlayerState.Swimming or PlayerState.Flying;
            string normalizedWeaponType = weaponType?.Trim().ToLowerInvariant();

            if (playerActionName.IndexOf("prone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                yield return "proneStab";
            }

            bool useRangedShootFamily = IsRangedWeaponType(normalizedWeaponType)
                                        || ContainsAnyFragment(playerActionName, RangedHeuristicFragments);
            bool usePolearmSwingFamily = IsPolearmWeaponType(normalizedWeaponType);
            bool useTwoHandedMeleeFamily = IsTwoHandedMeleeWeaponType(normalizedWeaponType);
            bool preferStabFamily = ContainsAnyFragment(playerActionName, StabHeuristicFragments);

            if (useRangedShootFamily)
            {
                foreach (string candidate in EnumerateRangedAttackCandidates(playerActionName, floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (preferStabFamily)
            {
                foreach (string candidate in EnumerateStabCandidates(useTwoHandedMeleeFamily, floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (usePolearmSwingFamily)
            {
                foreach (string candidate in EnumerateSwingCandidates("swingP", "swingT", floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (ContainsAnyFragment(playerActionName, SwingHeuristicFragments)
                || playerActionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (string candidate in EnumerateSwingCandidates(
                             useTwoHandedMeleeFamily ? "swingT" : "swingO",
                             useTwoHandedMeleeFamily ? "swingO" : "swingT",
                             floating))
                {
                    yield return candidate;
                }
            }
        }

        private static ShadowPartnerActionPiece[] CreateIndexedPieces(params string[] pieceActionNames)
        {
            if (pieceActionNames == null || pieceActionNames.Length == 0)
            {
                return Array.Empty<ShadowPartnerActionPiece>();
            }

            var pieces = new ShadowPartnerActionPiece[pieceActionNames.Length];
            for (int i = 0; i < pieceActionNames.Length; i++)
            {
                pieces[i] = new ShadowPartnerActionPiece(i, pieceActionNames[i], 0);
            }

            return pieces;
        }

        private static ShadowPartnerActionPiece[] CreateIndexedPieces(
            params (string PieceActionName, int SourceFrameIndex)[] pieceFrames)
        {
            if (pieceFrames == null || pieceFrames.Length == 0)
            {
                return Array.Empty<ShadowPartnerActionPiece>();
            }

            var pieces = new ShadowPartnerActionPiece[pieceFrames.Length];
            for (int i = 0; i < pieceFrames.Length; i++)
            {
                pieces[i] = new ShadowPartnerActionPiece(
                    i,
                    pieceFrames[i].PieceActionName,
                    pieceFrames[i].SourceFrameIndex);
            }

            return pieces;
        }

        private static ShadowPartnerActionPiece[] CreateIndexedPieces(
            params (string PieceActionName, int SourceFrameIndex, int DelayOverrideMs)[] pieceFrames)
        {
            if (pieceFrames == null || pieceFrames.Length == 0)
            {
                return Array.Empty<ShadowPartnerActionPiece>();
            }

            var pieces = new ShadowPartnerActionPiece[pieceFrames.Length];
            for (int i = 0; i < pieceFrames.Length; i++)
            {
                pieces[i] = new ShadowPartnerActionPiece(
                    i,
                    pieceFrames[i].PieceActionName,
                    pieceFrames[i].SourceFrameIndex,
                    pieceFrames[i].DelayOverrideMs);
            }

            return pieces;
        }

        private static ShadowPartnerActionPiece[] CreateIndexedPieces(
            params (string PieceActionName, int SourceFrameIndex, int DelayOverrideMs, bool Flip)[] pieceFrames)
        {
            if (pieceFrames == null || pieceFrames.Length == 0)
            {
                return Array.Empty<ShadowPartnerActionPiece>();
            }

            var pieces = new ShadowPartnerActionPiece[pieceFrames.Length];
            for (int i = 0; i < pieceFrames.Length; i++)
            {
                pieces[i] = new ShadowPartnerActionPiece(
                    i,
                    pieceFrames[i].PieceActionName,
                    pieceFrames[i].SourceFrameIndex,
                    pieceFrames[i].DelayOverrideMs,
                    pieceFrames[i].Flip);
            }

            return pieces;
        }

        private static ShadowPartnerActionPiece CreateIndexedPiece(
            int slotIndex,
            string pieceActionName,
            int sourceFrameIndex,
            int delayOverrideMs,
            bool flip = false,
            Point? move = null,
            int rotationDegrees = 0)
        {
            return new ShadowPartnerActionPiece(
                slotIndex,
                pieceActionName,
                sourceFrameIndex,
                delayOverrideMs,
                flip,
                move,
                rotationDegrees);
        }

        private static IEnumerable<string> EnumerateRangedAttackCandidates(string playerActionName, bool floating)
        {
            if (floating)
            {
                yield return "shootF";
            }

            if (string.Equals(playerActionName, "attack2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "shoot2";
                yield return "shoot1";
            }
            else
            {
                yield return "shoot1";
                yield return "shoot2";
            }

            if (!floating)
            {
                yield return "shootF";
            }
        }

        private static IEnumerable<string> EnumerateStabCandidates(bool preferTwoHandedFamily, bool floating)
        {
            foreach (string candidate in EnumerateAttackFamilyCandidates(
                         preferTwoHandedFamily ? "stabT" : "stabO",
                         preferTwoHandedFamily ? "stabO" : "stabT",
                         floating,
                         includeThirdGroundFrame: false))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateSwingCandidates(
            string primaryPrefix,
            string secondaryPrefix,
            bool floating)
        {
            foreach (string candidate in EnumerateAttackFamilyCandidates(
                         primaryPrefix,
                         secondaryPrefix,
                         floating,
                         includeThirdGroundFrame: true))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateAttackFamilyCandidates(
            string primaryPrefix,
            string secondaryPrefix,
            bool floating,
            bool includeThirdGroundFrame)
        {
            if (floating)
            {
                yield return primaryPrefix + "F";
            }

            yield return primaryPrefix + "1";
            yield return primaryPrefix + "2";

            if (includeThirdGroundFrame)
            {
                yield return primaryPrefix + "3";
            }

            if (!floating)
            {
                yield return primaryPrefix + "F";
            }

            if (floating)
            {
                yield return secondaryPrefix + "F";
            }

            yield return secondaryPrefix + "1";
            yield return secondaryPrefix + "2";

            if (includeThirdGroundFrame)
            {
                yield return secondaryPrefix + "3";
            }

            if (!floating)
            {
                yield return secondaryPrefix + "F";
            }
        }

        private static bool IsHeuristicAttackAction(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                return false;
            }

            return playerActionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
                   || ContainsAnyFragment(playerActionName, SwingHeuristicFragments)
                   || ContainsAnyFragment(playerActionName, StabHeuristicFragments)
                   || ContainsAnyFragment(playerActionName, RangedHeuristicFragments);
        }

        private static bool ContainsAnyFragment(string actionName, IEnumerable<string> fragments)
        {
            if (string.IsNullOrWhiteSpace(actionName) || fragments == null)
            {
                return false;
            }

            foreach (string fragment in fragments)
            {
                if (!string.IsNullOrWhiteSpace(fragment)
                    && actionName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRangedWeaponType(string weaponType)
        {
            return weaponType is "bow" or "crossbow" or "claw" or "gun" or "double bowgun" or "cannon";
        }

        private static bool IsPolearmWeaponType(string weaponType)
        {
            return weaponType is "spear" or "polearm";
        }

        private static bool IsTwoHandedMeleeWeaponType(string weaponType)
        {
            return weaponType is "2h sword" or "2h axe" or "2h blunt";
        }
    }
}
