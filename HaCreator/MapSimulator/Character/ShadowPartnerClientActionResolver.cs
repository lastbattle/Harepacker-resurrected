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
            int RotationDegrees = 0,
            bool IsSyntheticMirroredTailPiece = false,
            bool IsClientActionManInitPiece = false,
            int? EventDelayOverrideMs = null,
            IReadOnlyList<string> InlineCanvasChildNames = null);

        private static readonly string[] SwingHeuristicFragments =
        {
            "swing",
            "slash",
            "blow",
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
            "cut",
            "impale",
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
                ["holyshield"] = new[] { "alert", "stand1", "stand2" },
                ["resurrection"] = new[] { "alert", "stand1", "stand2" },
                ["dash"] = new[] { "walk1", "walk2", "stand1" },
                ["octopus"] = new[] { "alert", "stand1", "stand2" },
                ["darksight"] = new[] { "alert", "stand1", "stand2" },
                // Mounted helper rows currently recover concrete piece plans for these
                // actions in v95, but keep explicit alias fallback so other data sets
                // still resolve onto authored `special/*` branches when those rows are absent.
                ["rain"] = new[] { "shoot1", "shoot2", "shootF" },
                ["paralyze"] = new[] { "shoot1", "shoot2", "shootF" },
                ["shoot6"] = new[] { "shoot1", "shoot2", "shootF" },
                ["arrowRain"] = new[] { "shoot1", "shoot2", "shootF" },
                ["arrowEruption"] = new[] { "shoot1", "shoot2", "shootF" },
                ["chargeBlow"] = new[] { "stabO1", "stabO2", "stabOF" },
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
                    ("alert", 1, -450)),
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
                // Mounted Character/00002000 rows below carry full piece-authored
                // frame+delay metadata; keep built-in parity plans so fallback stays
                // on client-init timing even when mounted rows are unavailable.
                ["rain"] = CreateIndexedPieces(
                    ("stand1", 1, -60),
                    ("swingT1", 1, -90),
                    ("swingT1", 0, -390),
                    ("swingT1", 2, 120),
                    ("stand1", 1, 60)),
                ["paralyze"] = CreateIndexedPieces(
                    ("swingO3", 0, -350),
                    ("stabO2", 1, 450)),
                ["shoot6"] = CreateIndexedPieces(
                    ("shootF", 0, -480),
                    ("shootF", 1, -160),
                    ("shootF", 2, 260)),
                ["arrowRain"] = CreateIndexedPieces(
                    ("swingT1", 1, -120),
                    ("swingT1", 0, -510),
                    ("swingT1", 2, 330),
                    ("alert", 0, 0)),
                ["arrowEruption"] = CreateIndexedPieces(
                    ("swingT1", 1, -120),
                    ("swingT1", 0, -510),
                    ("swingT1", 2, 330),
                    ("alert", 0, 0)),
                ["chargeBlow"] = CreateIndexedPieces(
                    ("stabO1", 0, -300),
                    ("stabO1", 1, 420),
                    ("alert", 0, 90)),
                // Legacy helper create rows remain authored in Character/00002000 and are
                // still used as fallback when create2/3/4 branches are unavailable.
                // WZ-backed rows:
                // - create0/{0,1}: shoot2/2/450, shoot2/1/450
                // - create1/{0,1,2,3}: shoot2/2/180, shoot2/0/270, shoot2/2/180, shoot2/0/270
                ["create0"] = CreateIndexedPieces(
                    ("shoot2", 2, 450),
                    ("shoot2", 1, 450)),
                ["create1"] = CreateIndexedPieces(
                    ("shoot2", 2, 180),
                    ("shoot2", 0, 270),
                    ("shoot2", 2, 180),
                    ("shoot2", 0, 270)),
                // Additional mounted client-init action rows from Character/00002000.
                // Keep these as built-in fallback piece plans so loader-owned event-delay
                // shaping remains intact when mounted rows are missing from a dataset.
                ["rush"] = CreateIndexedPieces(
                    ("stabT1", 0, 100),
                    ("stabT1", 2, 500)),
                ["rush2"] = CreateIndexedPieces(
                    ("stabOF", 0, 100),
                    ("stabOF", 2, 500)),
                ["magic1"] = CreateIndexedPieces(
                    ("shootF", 1, -900),
                    ("swingO3", 1, 200),
                    ("swingO3", 2, 200)),
                ["magic2"] = CreateIndexedPieces(
                    ("alert", 0, -350),
                    ("alert", 1, 350),
                    ("alert", 2, 350)),
                ["magic3"] = CreateIndexedPieces(
                    ("stand1", 0, -300),
                    ("stand1", 1, 300),
                    ("stand1", 2, 300)),
                ["magic4"] = CreateIndexedPieces(
                    ("swingO3", 0, -800),
                    ("swingO3", 1, 300),
                    ("swingO3", 2, 300)),
                ["magic5"] = CreateIndexedPieces(
                    ("alert", 0, -720),
                    ("alert", 1, 240),
                    ("alert", 2, 240)),
                ["magic6"] = CreateIndexedPieces(
                    ("alert", 0, -300),
                    ("alert", 1, 420)),
                ["explosion"] = CreateIndexedPieces(
                    ("alert", 0, 210),
                    ("alert", 1, 210)),
                ["iceStrike"] = CreateIndexedPieces(
                    ("alert", 0, -390),
                    ("alert", 1, 450),
                    ("alert", 2, 450)),
                ["burster1"] = CreateIndexedPieces(
                    ("stabT1", 0, -300),
                    ("stabT1", 1, -300),
                    ("stabT1", 2, 150),
                    ("stabTF", 1, 150)),
                ["burster2"] = CreateIndexedPieces(
                    ("stabT1", 0, -300),
                    ("stabT1", 1, -300),
                    ("stabT1", 2, 150),
                    ("stabTF", 1, 150),
                    ("stabT2", 2, 150)),
                ["sanctuary"] = new[]
                {
                    CreateIndexedPiece(0, "stand2", 1, -480),
                    CreateIndexedPiece(1, "alert", 0, -60),
                    CreateIndexedPiece(2, "swingPF", 2, -120, move: new Point(0, -14)),
                    CreateIndexedPiece(3, "swingPF", 2, -120, move: new Point(0, -58)),
                    CreateIndexedPiece(4, "swingPF", 2, -120, move: new Point(0, -60)),
                    CreateIndexedPiece(5, "swingPF", 2, -120, move: new Point(0, -61)),
                    CreateIndexedPiece(6, "swingPF", 2, -120, move: new Point(0, -62)),
                    CreateIndexedPiece(7, "swingPF", 2, -120, move: new Point(0, -63)),
                    CreateIndexedPiece(8, "swingPF", 2, -240, move: new Point(0, -64)),
                    CreateIndexedPiece(9, "swingP1", 2, 840, move: new Point(-7, 0)),
                    CreateIndexedPiece(10, "alert", 0, 120),
                    CreateIndexedPiece(11, "stand2", 1, 60)
                },
                ["meteor"] = CreateIndexedPieces(
                    ("alert", 0, -360),
                    ("alert", 1, -1800),
                    ("alert", 2, 1320)),
                ["blizzard"] = CreateIndexedPieces(
                    ("alert", 0, -360),
                    ("alert", 1, -1800),
                    ("alert", 2, 1320)),
                ["genesis"] = CreateIndexedPieces(
                    ("alert", 0, -900),
                    ("alert", 1, -900),
                    ("alert", 2, 900)),
                ["brandish1"] = CreateIndexedPieces(
                    ("swingOF", 0, -120),
                    ("swingOF", 1, -120),
                    ("swingOF", 2, -120),
                    ("swingOF", 3, 120),
                    ("stabOF", 0, 120),
                    ("stabOF", 1, 120),
                    ("stabOF", 2, 120)),
                ["brandish2"] = CreateIndexedPieces(
                    ("swingT3", 0, -120),
                    ("swingT3", 1, -120),
                    ("swingT3", 2, 120),
                    ("swingTF", 0, 120),
                    ("swingTF", 1, 120),
                    ("swingTF", 2, 120),
                    ("swingTF", 3, 120)),
                ["chainlightning"] = CreateIndexedPieces(
                    ("swingO3", 0, -540),
                    ("stabO2", 1, 240)),
                ["blast"] = CreateIndexedPieces(
                    ("stabO2", 0, -540),
                    ("stabO2", 1, 300)),
                ["straight"] = CreateIndexedPieces(
                    ("stabO1", 0, -240),
                    ("stabO1", 1, 360)),
                ["handgun"] = new[]
                {
                    CreateIndexedPiece(0, "shoot2", 0, -240),
                    CreateIndexedPiece(1, "stabO1", 0, 540, move: new Point(2, 0)),
                    CreateIndexedPiece(2, "shoot2", 0, 0)
                },
                ["somersault"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, 0),
                    CreateIndexedPiece(1, "swingPF", 3, 120, move: new Point(35, 0)),
                    CreateIndexedPiece(2, "swingT2", 0, 120, move: new Point(-10, -8)),
                    CreateIndexedPiece(3, "swingP2", 1, 120, move: new Point(-30, -112), rotationDegrees: 180),
                    CreateIndexedPiece(4, "swingP2", 0, 120, move: new Point(-13, -115), rotationDegrees: 180),
                    CreateIndexedPiece(5, "swingP2", 0, 120, move: new Point(-7, -111), rotationDegrees: 180),
                    CreateIndexedPiece(6, "stabT2", 1, 120, move: new Point(35, -84), rotationDegrees: 270),
                    CreateIndexedPiece(7, "swingPF", 3, 120, move: new Point(35, 0)),
                    CreateIndexedPiece(8, "alert", 0, 0)
                },
                // Additional mounted Character/00002000 piece rows recovered from the
                // same client-init helper table surface. Keep these in built-in fallback
                // so unavailable mounted rows still preserve piece-order timing/move/flip.
                ["doublefire"] = new[]
                {
                    CreateIndexedPiece(0, "shoot2", 0, 90),
                    CreateIndexedPiece(1, "stabO1", 0, 360, move: new Point(2, 0)),
                    CreateIndexedPiece(2, "shoot2", 0, 0)
                },
                ["triplefire"] = new[]
                {
                    CreateIndexedPiece(0, "shoot2", 0, 90),
                    CreateIndexedPiece(1, "stabO1", 0, 720, move: new Point(2, 0)),
                    CreateIndexedPiece(2, "shoot2", 0, 0)
                },
                ["fake"] = new[]
                {
                    CreateIndexedPiece(0, "shoot2", 0, 90),
                    CreateIndexedPiece(1, "stabO1", 0, 720, move: new Point(2, 0)),
                    CreateIndexedPiece(2, "shoot2", 0, 0)
                },
                ["doubleupper"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -240),
                    CreateIndexedPiece(1, "swingOF", 0, -90, move: new Point(5, 0)),
                    CreateIndexedPiece(2, "swingP2", 1, 90, move: new Point(4, 0)),
                    CreateIndexedPiece(3, "swingP2", 0, 90, move: new Point(-3, 0)),
                    CreateIndexedPiece(4, "swingO3", 2, 90, move: new Point(17, 0)),
                    CreateIndexedPiece(5, "stabO2", 0, 180, move: new Point(-4, 0)),
                    CreateIndexedPiece(6, "shoot2", 3, 90, move: new Point(-4, 0))
                },
                ["eburster"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, 180),
                    CreateIndexedPiece(1, "stabO1", 1, 90),
                    CreateIndexedPiece(2, "stabO1", 1, 90, move: new Point(-2, 0)),
                    CreateIndexedPiece(3, "stabO1", 1, 90, move: new Point(-1, 0)),
                    CreateIndexedPiece(4, "stabO1", 1, 90, move: new Point(-1, 0)),
                    CreateIndexedPiece(5, "stabO1", 1, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(6, "swingO2", 1, 90),
                    CreateIndexedPiece(7, "alert", 0, 0)
                },
                ["screw"] = CreateIndexedPieces(
                    ("stabO2", 1, 600),
                    ("swingP2", 1, 240)),
                ["backspin"] = new[]
                {
                    CreateIndexedPiece(0, "swingP2", 1, -120),
                    CreateIndexedPiece(1, "swingO2", 1, -120),
                    CreateIndexedPiece(2, "swingO2", 0, -120),
                    CreateIndexedPiece(3, "swingO2", 1, -120, move: new Point(16, 0)),
                    CreateIndexedPiece(4, "shoot2", 3, 480)
                },
                ["eorb"] = new[]
                {
                    CreateIndexedPiece(0, "swingOF", 0, -630),
                    CreateIndexedPiece(1, "swingOF", 1, -90, move: new Point(1, 6)),
                    CreateIndexedPiece(2, "swingOF", 1, -90, move: new Point(1, 3)),
                    CreateIndexedPiece(3, "swingOF", 2, 90, move: new Point(11, 4)),
                    CreateIndexedPiece(4, "swingOF", 3, 180, move: new Point(28, 0)),
                    CreateIndexedPiece(5, "stabT2", 0, 90, move: new Point(-7, 0)),
                    CreateIndexedPiece(6, "alert", 1, 0)
                },
                ["dragonstrike"] = new[]
                {
                    CreateIndexedPiece(0, "swingP2", 1, -90, move: new Point(6, -14)),
                    CreateIndexedPiece(1, "swingP2", 0, -90, move: new Point(1, -20)),
                    CreateIndexedPiece(2, "swingOF", 1, -90, move: new Point(6, -19)),
                    CreateIndexedPiece(3, "swingT2", 0, -90, flip: true, move: new Point(-7, -35)),
                    CreateIndexedPiece(4, "swingTF", 1, -90, move: new Point(8, -34)),
                    CreateIndexedPiece(5, "swingTF", 1, -90, move: new Point(8, -35)),
                    CreateIndexedPiece(6, "swingTF", 1, -90, move: new Point(8, -34)),
                    CreateIndexedPiece(7, "swingTF", 1, -90, move: new Point(8, -28)),
                    CreateIndexedPiece(8, "swingOF", 3, -90, move: new Point(43, 0)),
                    CreateIndexedPiece(9, "swingOF", 3, -90, move: new Point(43, 1)),
                    CreateIndexedPiece(10, "swingOF", 3, -90, move: new Point(43, 2)),
                    CreateIndexedPiece(11, "swingOF", 3, 90, move: new Point(43, 3))
                },
                ["airstrike"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -240),
                    CreateIndexedPiece(1, "alert", 1, -240),
                    CreateIndexedPiece(2, "alert", 2, -240),
                    CreateIndexedPiece(3, "swingT2", 1, -120, move: new Point(9, -12)),
                    CreateIndexedPiece(4, "swingT1", 0, -120, move: new Point(0, -15)),
                    CreateIndexedPiece(5, "swingT1", 0, -120, move: new Point(0, -16)),
                    CreateIndexedPiece(6, "swingT1", 0, -120, move: new Point(0, -15)),
                    CreateIndexedPiece(7, "swingT1", 0, -120, move: new Point(0, -13)),
                    CreateIndexedPiece(8, "swingT2", 2, -120, move: new Point(15, 1)),
                    CreateIndexedPiece(9, "swingT2", 2, -120, move: new Point(15, 2)),
                    CreateIndexedPiece(10, "swingT2", 2, -120, move: new Point(15, 3)),
                    CreateIndexedPiece(11, "swingT3", 1, -120, move: new Point(2, 0)),
                    CreateIndexedPiece(12, "alert", 0, -180),
                    CreateIndexedPiece(13, "alert", 1, 450),
                    CreateIndexedPiece(14, "alert", 2, 450)
                },
                ["edrain"] = CreateIndexedPieces(
                    ("alert", 0, 600)),
                ["shot"] = new[]
                {
                    CreateIndexedPiece(0, "shoot2", 0, -240),
                    CreateIndexedPiece(1, "stabO1", 0, 540, move: new Point(2, 0)),
                    CreateIndexedPiece(2, "shoot2", 0, 0)
                },
                ["flashBang"] = CreateIndexedPieces(
                    ("swingO3", 0, 90),
                    ("swingO3", 1, 90),
                    ("swingO3", 2, 330)),
                // `Character/00002000.img` still exposes high-count helper-piece rows for
                // `fist` and `bamboo`. Keep built-in parity plans so loader-owned fallback
                // retains the same client-init piece ordering and signed-delay timing.
                ["fist"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -90),
                    CreateIndexedPiece(1, "alert", 1, -90),
                    CreateIndexedPiece(2, "alert", 0, -270),
                    CreateIndexedPiece(3, "stabOF", 1, 90),
                    CreateIndexedPiece(4, "stabOF", 1, 90),
                    CreateIndexedPiece(5, "stabO2", 0, 90),
                    CreateIndexedPiece(6, "stabO1", 1, 90),
                    CreateIndexedPiece(7, "stabO1", 1, 90),
                    CreateIndexedPiece(8, "swingTF", 1, 90),
                    CreateIndexedPiece(9, "stabOF", 1, 90),
                    CreateIndexedPiece(10, "stabOF", 1, 90),
                    CreateIndexedPiece(11, "swingT2", 0, 90),
                    CreateIndexedPiece(12, "stabO1", 1, 90),
                    CreateIndexedPiece(13, "stabO1", 1, 90),
                    CreateIndexedPiece(14, "stabTF", 2, 90),
                    CreateIndexedPiece(15, "swingOF", 1, 90),
                    CreateIndexedPiece(16, "swingOF", 2, 90),
                    CreateIndexedPiece(17, "swingT1", 2, 90),
                    CreateIndexedPiece(18, "swingT1", 2, 90),
                    CreateIndexedPiece(19, "stabOF", 0, 90),
                    CreateIndexedPiece(20, "swingP2", 0, 90),
                    CreateIndexedPiece(21, "swingOF", 1, 90),
                    CreateIndexedPiece(22, "swingOF", 1, 90),
                    CreateIndexedPiece(23, "swingOF", 2, 90, flip: true),
                    CreateIndexedPiece(24, "swingPF", 3, 90)
                },
                ["fireburner"] = new[]
                {
                    CreateIndexedPiece(0, "shoot2", 0, -240),
                    CreateIndexedPiece(1, "stabO1", 0, -270, move: new Point(2, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, 90, move: new Point(2, 0)),
                    CreateIndexedPiece(3, "shoot2", 0, 0)
                },
                ["coolingeffect"] = new[]
                {
                    CreateIndexedPiece(0, "shoot2", 0, -240),
                    CreateIndexedPiece(1, "stabO1", 0, -90, move: new Point(2, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, 270, move: new Point(2, 0)),
                    CreateIndexedPiece(3, "shoot2", 0, 0)
                },
                ["rapidfire"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 0, 60, move: new Point(7, 0))
                },
                ["homing"] = CreateIndexedPieces(
                    ("swingO3", 0, -720),
                    ("swingO3", 1, 120),
                    ("swingO3", 2, 120)),
                ["backstep"] = CreateIndexedPieces(
                    ("stabO1", 0, 300)),
                ["timeleap"] = CreateIndexedPieces(
                    ("alert", 0, -450),
                    ("alert", 1, -540),
                    ("alert", 2, 450)),
                ["recovery"] = CreateIndexedPieces(
                    ("alert", 0, 30)),
                ["owlDead"] = CreateIndexedPieces(
                    ("stabO1", 0, -120),
                    ("stabO1", 0, 840),
                    ("swingO1", 0, 450)),
                ["cannon"] = CreateIndexedPieces(
                    ("alert", 0, 780)),
                ["torpedo"] = CreateIndexedPieces(
                    ("alert", 0, 990)),
                ["bamboo"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, -720),
                    CreateIndexedPiece(1, "swingPF", 1, -120),
                    CreateIndexedPiece(2, "swingP2", 1, -120),
                    CreateIndexedPiece(3, "swingP2", 0, -120),
                    CreateIndexedPiece(4, "swingOF", 1, -120),
                    CreateIndexedPiece(5, "swingT2", 0, -120, flip: true),
                    CreateIndexedPiece(6, "swingTF", 1, -120),
                    CreateIndexedPiece(7, "swingO1", 2, -120),
                    CreateIndexedPiece(8, "swingO3", 1, -120),
                    CreateIndexedPiece(9, "swingO2", 2, -120),
                    CreateIndexedPiece(10, "swingO2", 1, -120),
                    CreateIndexedPiece(11, "swingTF", 1, -120),
                    CreateIndexedPiece(12, "swingO1", 2, -120),
                    CreateIndexedPiece(13, "swingO2", 1, -120),
                    CreateIndexedPiece(14, "swingTF", 1, -120),
                    CreateIndexedPiece(15, "swingTF", 1, -120),
                    CreateIndexedPiece(16, "swingTF", 1, -120),
                    CreateIndexedPiece(17, "stabO1", 1, -120),
                    CreateIndexedPiece(18, "swingTF", 1, -120),
                    CreateIndexedPiece(19, "swingTF", 1, -120),
                    CreateIndexedPiece(20, "swingTF", 1, -120),
                    CreateIndexedPiece(21, "swingTF", 1, -120),
                    CreateIndexedPiece(22, "swingTF", 1, -120),
                    CreateIndexedPiece(23, "swingTF", 1, 120),
                    CreateIndexedPiece(24, "swingTF", 1, 120),
                    CreateIndexedPiece(25, "swingTF", 1, 120),
                    CreateIndexedPiece(26, "swingOF", 3, 120),
                    CreateIndexedPiece(27, "swingOF", 3, 120),
                    CreateIndexedPiece(28, "swingOF", 3, 120),
                    CreateIndexedPiece(29, "swingOF", 3, 120),
                    CreateIndexedPiece(30, "alert", 1, 240)
                },
                ["wave"] = new[]
                {
                    CreateIndexedPiece(0, "swingTF", 1, -60, move: new Point(14, 0)),
                    CreateIndexedPiece(1, "swingTF", 1, -420, move: new Point(15, 0)),
                    CreateIndexedPiece(2, "stabO1", 1, -60, move: new Point(-29, 0)),
                    CreateIndexedPiece(3, "stabO1", 1, 450, move: new Point(-30, 0)),
                    CreateIndexedPiece(4, "alert", 0, 60)
                },
                ["blade"] = new[]
                {
                    CreateIndexedPiece(0, "swingTF", 0, -90, move: new Point(-1, -4)),
                    CreateIndexedPiece(1, "swingTF", 1, -90, move: new Point(6, -13)),
                    CreateIndexedPiece(2, "swingTF", 2, -90, move: new Point(11, -27)),
                    CreateIndexedPiece(3, "swingTF", 2, -90, move: new Point(11, -32)),
                    CreateIndexedPiece(4, "swingTF", 2, -90, move: new Point(11, -33)),
                    CreateIndexedPiece(5, "swingTF", 2, -120, move: new Point(11, -34)),
                    CreateIndexedPiece(6, "swingTF", 2, 60, move: new Point(11, -34)),
                    CreateIndexedPiece(7, "swingTF", 3, 360, move: new Point(16, 0))
                },
                ["souldriver"] = CreateIndexedPieces(
                    ("alert", 0, -800),
                    ("alert", 1, -800),
                    ("alert", 2, 1)),
                ["firestrike"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 0, -60, move: new Point(6, 0)),
                    CreateIndexedPiece(1, "stabO1", 0, -480, move: new Point(7, 0)),
                    CreateIndexedPiece(2, "stabO1", 1, 60, move: new Point(-24, 0)),
                    CreateIndexedPiece(3, "stabO1", 1, 60, move: new Point(-36, 0)),
                    CreateIndexedPiece(4, "stabO1", 1, 60, move: new Point(-39, 0)),
                    CreateIndexedPiece(5, "stabO1", 1, 120, move: new Point(-41, 0))
                },
                ["flamegear"] = CreateIndexedPieces(
                    ("alert", 0, -630),
                    ("alert", 1, -630),
                    ("alert", 2, 180)),
                ["stormbreak"] = new[]
                {
                    CreateIndexedPiece(0, "swingT3", 0, -90, move: new Point(-9, 0)),
                    CreateIndexedPiece(1, "swingT3", 1, -90, move: new Point(-20, 0)),
                    CreateIndexedPiece(2, "swingT3", 2, 90, move: new Point(-15, 0)),
                    CreateIndexedPiece(3, "swingT3", 2, 180, move: new Point(-15, 0))
                },
                ["shockwave"] = CreateIndexedPieces(
                    ("alert", 0, -870),
                    ("alert", 1, 360)),
                ["demolition"] = CreateIndexedPieces(
                    ("alert", 0, -450),
                    ("alert", 1, 2670)),
                ["snatch"] = CreateIndexedPieces(
                    ("alert", 0, -630),
                    ("alert", 1, 180)),
                // CActionMan::Init switches raw actions 124..131 to child row "1".
                // Keep fallback plans on that same row even when mounted rows are absent.
                ["windspear"] = CreateIndexedPieces(
                    ("alert", 1, 540)),
                ["windshot"] = CreateIndexedPieces(
                    ("alert", 1, 660)),
                ["swingT2PoleArm"] = CreateIndexedPieces(
                    ("swingT2", 1, -60)),
                ["swingP1PoleArm"] = CreateIndexedPieces(
                    ("swingP1", 1, -60)),
                ["swingP2PoleArm"] = CreateIndexedPieces(
                    ("swingP2", 1, -60)),
                ["combatStep"] = CreateIndexedPieces(
                    ("walk2", 1, 30)),
                ["finalCharge"] = new[]
                {
                    CreateIndexedPiece(0, "stabTF", 2, -120, move: new Point(13, 14)),
                    CreateIndexedPiece(1, "stabTF", 2, 120, move: new Point(14, 13)),
                    CreateIndexedPiece(2, "stabT2", 2, 120),
                    CreateIndexedPiece(3, "stabT2", 2, 120, flip: true, move: new Point(-56, 0)),
                    CreateIndexedPiece(4, "stabT2", 2, 120),
                    CreateIndexedPiece(5, "stabT2", 2, 90, flip: true, move: new Point(-56, 0)),
                    CreateIndexedPiece(6, "stabT2", 2, 90),
                    CreateIndexedPiece(7, "stabT2", 2, 90, flip: true, move: new Point(-56, 0)),
                    CreateIndexedPiece(8, "stabT2", 2, 90),
                    CreateIndexedPiece(9, "stabT2", 2, 60, move: new Point(-2, 0)),
                    CreateIndexedPiece(10, "stabT2", 2, 60, move: new Point(-1, 0))
                },
                ["finalToss"] = new[]
                {
                    CreateIndexedPiece(0, "swingPF", 3, -90, move: new Point(47, 0)),
                    CreateIndexedPiece(1, "swingPF", 2, 90, move: new Point(14, -54)),
                    CreateIndexedPiece(2, "swingPF", 2, 90, move: new Point(14, -60)),
                    CreateIndexedPiece(3, "swingPF", 2, 90, move: new Point(14, -63)),
                    CreateIndexedPiece(4, "swingPF", 2, 30, move: new Point(14, -64)),
                    CreateIndexedPiece(5, "swingP2", 0, 30, move: new Point(0, -74))
                },
                ["finalBlow"] = new[]
                {
                    CreateIndexedPiece(0, "swingT2", 2, -90, move: new Point(4, 0)),
                    CreateIndexedPiece(1, "swingPF", 2, -90, move: new Point(-2, -12)),
                    CreateIndexedPiece(2, "swingPF", 2, -90, move: new Point(-2, -33)),
                    CreateIndexedPiece(3, "swingPF", 2, -90, move: new Point(-2, -40)),
                    CreateIndexedPiece(4, "swingPF", 2, -90, move: new Point(-2, -41)),
                    CreateIndexedPiece(5, "swingPF", 2, -90, move: new Point(-2, -40)),
                    CreateIndexedPiece(6, "swingPF", 3, 90, move: new Point(23, 6)),
                    CreateIndexedPiece(7, "swingPF", 3, 90, move: new Point(23, 7)),
                    CreateIndexedPiece(8, "swingPF", 3, 90, move: new Point(23, 2))
                },
                ["comboSmash"] = new[]
                {
                    CreateIndexedPiece(0, "swingOF", 2, -120, move: new Point(39, -12)),
                    CreateIndexedPiece(1, "swingOF", 1, -120, move: new Point(34, -18)),
                    CreateIndexedPiece(2, "swingOF", 2, -120, flip: true, move: new Point(4, -14)),
                    CreateIndexedPiece(3, "stabT2", 2, 120, move: new Point(38, 0)),
                    CreateIndexedPiece(4, "stabT2", 2, 120),
                    CreateIndexedPiece(5, "stabT2", 2, 600, move: new Point(-2, 0))
                },
                ["comboFenrir"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -90),
                    CreateIndexedPiece(1, "swingT2", 0, -90, move: new Point(3, 0)),
                    CreateIndexedPiece(2, "swingT2", 1, -90, move: new Point(3, 0)),
                    CreateIndexedPiece(3, "swingPF", 3, -90, move: new Point(45, 0)),
                    CreateIndexedPiece(4, "swingP2", 2, -90, move: new Point(33, 0)),
                    CreateIndexedPiece(5, "stabTF", 2, -90, move: new Point(60, 16)),
                    CreateIndexedPiece(6, "stabTF", 2, -360, move: new Point(61, 16)),
                    CreateIndexedPiece(7, "stabT2", 2, 90, move: new Point(-82, 0)),
                    CreateIndexedPiece(8, "stabT2", 2, 540, move: new Point(-87, 0))
                },
                ["fullSwingDouble"] = new[]
                {
                    CreateIndexedPiece(0, "stabT2", 2, -90, move: new Point(-9, 0)),
                    CreateIndexedPiece(1, "stabT2", 2, 60, move: new Point(-9, 0)),
                    CreateIndexedPiece(2, "stabT1", 0, 120, move: new Point(1, 0)),
                    CreateIndexedPiece(3, "stabT1", 2, 150, move: new Point(-25, 0))
                },
                ["fullSwingTriple"] = new[]
                {
                    CreateIndexedPiece(0, "swingT2", 0, -60, move: new Point(-84, -85), rotationDegrees: 90),
                    CreateIndexedPiece(1, "swingT2", 0, -60, move: new Point(-57, -152), rotationDegrees: 180),
                    CreateIndexedPiece(2, "swingPF", 2, -60, move: new Point(-42, -51)),
                    CreateIndexedPiece(3, "swingPF", 2, -60, move: new Point(-42, -55)),
                    CreateIndexedPiece(4, "swingPF", 2, -90, move: new Point(-42, -56)),
                    CreateIndexedPiece(5, "swingPF", 3, 120, move: new Point(-34, 0)),
                    CreateIndexedPiece(6, "swingPF", 3, 120, move: new Point(-34, 1)),
                    CreateIndexedPiece(7, "swingPF", 3, 90, move: new Point(-34, 2))
                },
                ["overSwingDouble"] = new[]
                {
                    CreateIndexedPiece(0, "swingPF", 3, -90, flip: true, move: new Point(-41, 0)),
                    CreateIndexedPiece(1, "stabT1", 2, 90, move: new Point(22, 0)),
                    CreateIndexedPiece(2, "swingPF", 3, 120, flip: true, move: new Point(-57, 0)),
                    CreateIndexedPiece(3, "stabT1", 2, 120)
                },
                ["overSwingTriple"] = new[]
                {
                    CreateIndexedPiece(0, "stabTF", 2, -30, flip: true, move: new Point(-67, -18)),
                    CreateIndexedPiece(1, "stabTF", 2, -60, move: new Point(-29, -38)),
                    CreateIndexedPiece(2, "swingPF", 2, -60, move: new Point(-29, -54)),
                    CreateIndexedPiece(3, "swingPF", 2, -60, move: new Point(-29, -57)),
                    CreateIndexedPiece(4, "swingPF", 2, -60, move: new Point(-29, -59)),
                    CreateIndexedPiece(5, "swingPF", 2, -60, move: new Point(-29, -60)),
                    CreateIndexedPiece(6, "swingPF", 3, 120, move: new Point(-33, 0)),
                    CreateIndexedPiece(7, "swingPF", 3, 120, move: new Point(-33, 1)),
                    CreateIndexedPiece(8, "swingPF", 3, 90, move: new Point(-33, 2))
                },
                ["rollingSpin"] = new[]
                {
                    CreateIndexedPiece(0, "stabT1", 2, 120, flip: true, move: new Point(-33, -21)),
                    CreateIndexedPiece(1, "stabT1", 2, 120, move: new Point(27, -29)),
                    CreateIndexedPiece(2, "stabT1", 2, 120, flip: true, move: new Point(-33, -35)),
                    CreateIndexedPiece(3, "stabT1", 2, 120, move: new Point(27, -39)),
                    CreateIndexedPiece(4, "stabTF", 2, 120, move: new Point(13, -17)),
                    CreateIndexedPiece(5, "stabTF", 2, 120, move: new Point(13, -22)),
                    CreateIndexedPiece(6, "stabTF", 2, 120, move: new Point(13, -7))
                },
                ["comboTempest"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -1200),
                    CreateIndexedPiece(1, "alert", 1, -1200),
                    CreateIndexedPiece(2, "alert", 2, 600)
                },
                ["comboJudgement"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -60),
                    CreateIndexedPiece(1, "alert", 1, -60),
                    CreateIndexedPiece(2, "alert", 0, -60),
                    CreateIndexedPiece(3, "swingP2", 0, -90, move: new Point(4, 0)),
                    CreateIndexedPiece(4, "swingPF", 2, -90, move: new Point(-2, -12)),
                    CreateIndexedPiece(5, "swingPF", 2, -90, move: new Point(-2, -33)),
                    CreateIndexedPiece(6, "swingPF", 2, -90, move: new Point(-2, -40)),
                    CreateIndexedPiece(7, "swingPF", 2, -90, move: new Point(-2, -41)),
                    CreateIndexedPiece(8, "swingPF", 2, -120, move: new Point(-2, -40)),
                    CreateIndexedPiece(9, "swingPF", 2, -120, move: new Point(-2, -40)),
                    CreateIndexedPiece(10, "swingPF", 2, -120, move: new Point(23, 6)),
                    CreateIndexedPiece(11, "swingPF", 3, 150, move: new Point(23, 7)),
                    CreateIndexedPiece(12, "swingPF", 3, 150, move: new Point(23, 2)),
                    CreateIndexedPiece(13, "alert", 2, 120)
                },
                ["float"] = CreateIndexedPieces(
                    ("alert", 0, 1320)),
                ["pyramid"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, -120),
                    CreateIndexedPiece(1, "swingPF", 1, -120),
                    CreateIndexedPiece(2, "swingOF", 1, -120, move: new Point(3, -34)),
                    CreateIndexedPiece(3, "swingP2", 0, -120, move: new Point(6, -68)),
                    CreateIndexedPiece(4, "swingOF", 1, -120, move: new Point(9, -102)),
                    CreateIndexedPiece(5, "swingTF", 1, -120, move: new Point(14, -127)),
                    CreateIndexedPiece(6, "swingTF", 1, -120, move: new Point(14, -132)),
                    CreateIndexedPiece(7, "swingTF", 1, -120, move: new Point(14, -137)),
                    CreateIndexedPiece(8, "swingOF", 3, 120, move: new Point(35, -96)),
                    CreateIndexedPiece(9, "swingOF", 3, 120, move: new Point(35, -47)),
                    CreateIndexedPiece(10, "swingOF", 3, 120, move: new Point(35, 0)),
                    CreateIndexedPiece(11, "alert", 1, 120)
                },
                ["magicmissile"] = new[]
                {
                    CreateIndexedPiece(0, "walk1", 1, -90, move: new Point(1, 0)),
                    CreateIndexedPiece(1, "stabO1", 0, -90, move: new Point(10, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, -90, move: new Point(11, 0)),
                    CreateIndexedPiece(3, "swingO3", 1, -90, move: new Point(11, 0)),
                    CreateIndexedPiece(4, "swingO3", 2, 90, move: new Point(11, 0)),
                    CreateIndexedPiece(5, "swingO3", 2, 150, move: new Point(10, 0))
                },
                ["fireCircle"] = new[]
                {
                    CreateIndexedPiece(0, "walk1", 1, -90, move: new Point(1, 0)),
                    CreateIndexedPiece(1, "stabO1", 0, -90, move: new Point(10, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, 180, move: new Point(11, 0)),
                    CreateIndexedPiece(3, "swingO3", 1, 90, move: new Point(-9, 0)),
                    CreateIndexedPiece(4, "swingO3", 2, 90, move: new Point(-8, 0)),
                    CreateIndexedPiece(5, "swingO3", 2, 90, move: new Point(-9, 0)),
                    CreateIndexedPiece(6, "alert", 0, 90)
                },
                ["lightingBolt"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -180),
                    CreateIndexedPiece(1, "stabO1", 0, -90, move: new Point(9, 0)),
                    CreateIndexedPiece(2, "swingO2", 2, 90, move: new Point(4, 0)),
                    CreateIndexedPiece(3, "swingO2", 1, 90, move: new Point(2, 0)),
                    CreateIndexedPiece(4, "swingO2", 0, 90),
                    CreateIndexedPiece(5, "swingO2", 0, 90),
                    CreateIndexedPiece(6, "swingO2", 0, 90),
                    CreateIndexedPiece(7, "alert", 0, 180)
                },
                ["dragonBreathe"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 1, 90, move: new Point(-23, 0)),
                    CreateIndexedPiece(1, "stabO1", 1, 90, move: new Point(-35, 0)),
                    CreateIndexedPiece(2, "stabO1", 1, 90, move: new Point(-40, 0)),
                    CreateIndexedPiece(3, "stabO1", 1, 90, move: new Point(-42, 0)),
                    CreateIndexedPiece(4, "stabO1", 1, 90, move: new Point(-43, 0)),
                    CreateIndexedPiece(5, "stabO1", 1, 360, move: new Point(-44, 0)),
                    CreateIndexedPiece(6, "stabO1", 0, 90, move: new Point(-10, 0)),
                    CreateIndexedPiece(7, "alert", 0, 90)
                },
                ["breathe_prepare"] = CreateIndexedPieces(
                    ("walk1", 0, 1)),
                ["icebreathe_prepare"] = CreateIndexedPieces(
                    ("walk1", 0, 1)),
                ["blaze"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, -90),
                    CreateIndexedPiece(1, "stabO1", 0, -90, move: new Point(4, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, -90, move: new Point(6, 0)),
                    CreateIndexedPiece(3, "stabO1", 0, -90, move: new Point(7, 0)),
                    CreateIndexedPiece(4, "stabO1", 1, 90, move: new Point(-2, 0)),
                    CreateIndexedPiece(5, "stabO1", 1, 90, move: new Point(-6, 0)),
                    CreateIndexedPiece(6, "stabO1", 1, 90, move: new Point(-8, 0)),
                    CreateIndexedPiece(7, "stabO1", 1, 90, move: new Point(-9, 0)),
                    CreateIndexedPiece(8, "stabO1", 1, 90, move: new Point(-9, 0)),
                    CreateIndexedPiece(9, "alert", 0, 90)
                },
                ["illusion"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -90),
                    CreateIndexedPiece(1, "stabO1", 0, -60, move: new Point(15, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, -120, move: new Point(16, 0)),
                    CreateIndexedPiece(3, "swingO3", 0, 90, move: new Point(8, 0)),
                    CreateIndexedPiece(4, "stabO2", 1, 90, move: new Point(-13, 0)),
                    CreateIndexedPiece(5, "stabO2", 1, 120, move: new Point(-19, 0)),
                    CreateIndexedPiece(6, "stabO2", 1, 120, move: new Point(-21, 0)),
                    CreateIndexedPiece(7, "stabO2", 1, 120, move: new Point(-22, 0)),
                    CreateIndexedPiece(8, "alert", 1, 90),
                    CreateIndexedPiece(9, "alert", 0, 60)
                },
                ["dragonIceBreathe"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 1, 90, move: new Point(-23, 0)),
                    CreateIndexedPiece(1, "stabO1", 1, 90, move: new Point(-35, 0)),
                    CreateIndexedPiece(2, "stabO1", 1, 90, move: new Point(-40, 0)),
                    CreateIndexedPiece(3, "stabO1", 1, 90, move: new Point(-42, 0)),
                    CreateIndexedPiece(4, "stabO1", 1, 270, move: new Point(-43, 0)),
                    CreateIndexedPiece(5, "stabO1", 0, 90, move: new Point(-10, 0)),
                    CreateIndexedPiece(6, "alert", 0, 90)
                },
                ["magicFlare"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -30),
                    CreateIndexedPiece(1, "stabO1", 0, -60, move: new Point(9, 0)),
                    CreateIndexedPiece(2, "swingO2", 2, -60, move: new Point(4, 0)),
                    CreateIndexedPiece(3, "swingO2", 1, -120, move: new Point(2, 0)),
                    CreateIndexedPiece(4, "swingO2", 0, -200),
                    CreateIndexedPiece(5, "alert", 0, 270)
                },
                ["elementalReset"] = new[]
                {
                    CreateIndexedPiece(0, "stand1", 0, 90),
                    CreateIndexedPiece(1, "alert", 2, 60),
                    CreateIndexedPiece(2, "alert", 1, 60),
                    CreateIndexedPiece(3, "alert", 0, 600),
                    CreateIndexedPiece(4, "swingO2", 1, 90, move: new Point(-4, 0)),
                    CreateIndexedPiece(5, "swingO2", 0, 120, move: new Point(-9, 0)),
                    CreateIndexedPiece(6, "swingO2", 1, 90, move: new Point(-4, 0)),
                    CreateIndexedPiece(7, "alert", 2, 90),
                    CreateIndexedPiece(8, "stand1", 0, 240)
                },
                ["magicRegistance"] = new[]
                {
                    CreateIndexedPiece(0, "stand1", 0, 90),
                    CreateIndexedPiece(1, "alert", 2, 60, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 1, 60, move: new Point(1, 0)),
                    CreateIndexedPiece(3, "alert", 0, 600, move: new Point(1, 0)),
                    CreateIndexedPiece(4, "swingO2", 1, 90, move: new Point(3, 0)),
                    CreateIndexedPiece(5, "swingO1", 2, 120, move: new Point(7, 0)),
                    CreateIndexedPiece(6, "swingO1", 2, 90, move: new Point(4, 0)),
                    CreateIndexedPiece(7, "swingO1", 2, 90, move: new Point(3, 0)),
                    CreateIndexedPiece(8, "alert", 2, 240)
                },
                ["magicBooster"] = new[]
                {
                    CreateIndexedPiece(0, "stand1", 0, 90),
                    CreateIndexedPiece(1, "alert", 2, 60),
                    CreateIndexedPiece(2, "alert", 1, 60),
                    CreateIndexedPiece(3, "alert", 0, 600),
                    CreateIndexedPiece(4, "swingO2", 1, 90, move: new Point(-4, 0)),
                    CreateIndexedPiece(5, "swingO2", 0, 120, move: new Point(-9, 0)),
                    CreateIndexedPiece(6, "swingO2", 1, 90, move: new Point(-4, 0)),
                    CreateIndexedPiece(7, "alert", 2, 90),
                    CreateIndexedPiece(8, "stand1", 0, 240)
                },
                ["magicShield"] = new[]
                {
                    CreateIndexedPiece(0, "stand1", 0, 120),
                    CreateIndexedPiece(1, "alert", 2, 330, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 1, 120),
                    CreateIndexedPiece(3, "alert", 0, 120),
                    CreateIndexedPiece(4, "stand1", 0, 930)
                },
                ["killingWing"] = new[]
                {
                    CreateIndexedPiece(0, "stand1", 0, -90),
                    CreateIndexedPiece(1, "swingO3", 1, -120, move: new Point(10, 0)),
                    CreateIndexedPiece(2, "stabO1", 1, -120, move: new Point(-11, 0)),
                    CreateIndexedPiece(3, "stabO1", 1, -120, move: new Point(-13, 0)),
                    CreateIndexedPiece(4, "stabO1", 1, -120, move: new Point(-13, 0)),
                    CreateIndexedPiece(5, "swingO1", 2, 90, move: new Point(22, 0)),
                    CreateIndexedPiece(6, "swingO1", 2, 90, move: new Point(28, 0)),
                    CreateIndexedPiece(7, "swingO1", 2, 90, move: new Point(29, 0)),
                    CreateIndexedPiece(8, "swingO2", 2, 270, move: new Point(12, 0)),
                    CreateIndexedPiece(9, "alert", 0, 90)
                },
                ["Earthquake"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, -60, move: new Point(-3, 0)),
                    CreateIndexedPiece(1, "alert", 0, -60, move: new Point(-3, 0)),
                    CreateIndexedPiece(2, "swingO2", 1, -90, move: new Point(-1, 0)),
                    CreateIndexedPiece(3, "swingO2", 0, -240, move: new Point(-4, 0)),
                    CreateIndexedPiece(4, "swingO2", 2, -90, move: new Point(-12, 0)),
                    CreateIndexedPiece(5, "swingO2", 2, 90, move: new Point(-15, 0)),
                    CreateIndexedPiece(6, "swingO2", 2, 120, move: new Point(-16, 0)),
                    CreateIndexedPiece(7, "swingO2", 2, 90, move: new Point(-12, 0)),
                    CreateIndexedPiece(8, "alert", 0, 330, move: new Point(-7, 0))
                },
                ["recoveryAura"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -90, move: new Point(1, 0)),
                    CreateIndexedPiece(1, "alert", 1, -90, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 0, -270, move: new Point(1, 0)),
                    CreateIndexedPiece(3, "jump", 0, -90, move: new Point(1, -42)),
                    CreateIndexedPiece(4, "jump", 0, -90, move: new Point(1, -44)),
                    CreateIndexedPiece(5, "swingO3", 1, -90, move: new Point(22, -38)),
                    CreateIndexedPiece(6, "swingO2", 2, -90, move: new Point(8, 0)),
                    CreateIndexedPiece(7, "alert", 2, 540, move: new Point(1, 0))
                },
                ["OnixBlessing"] = new[]
                {
                    CreateIndexedPiece(0, "stand1", 0, 120),
                    CreateIndexedPiece(1, "alert", 2, 420, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 1, 120),
                    CreateIndexedPiece(3, "alert", 0, 240),
                    CreateIndexedPiece(4, "stand1", 0, 720)
                },
                ["soulStone"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(1, "alert", 1, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 1, 450, move: new Point(1, 0)),
                    CreateIndexedPiece(3, "swingO1", 2, 120, move: new Point(1, 0)),
                    CreateIndexedPiece(4, "swingO1", 2, 120, move: new Point(-2, 0)),
                    CreateIndexedPiece(5, "swingO1", 2, 150, move: new Point(-3, 0)),
                    CreateIndexedPiece(6, "alert", 2, 240)
                },
                ["dragonThrust"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -60),
                    CreateIndexedPiece(1, "alert", 1, -60),
                    CreateIndexedPiece(2, "alert", 0, -60),
                    CreateIndexedPiece(3, "stabO1", 1, -90, move: new Point(-23, 0)),
                    CreateIndexedPiece(4, "stabO1", 1, 90, move: new Point(-40, 0)),
                    CreateIndexedPiece(5, "stabO1", 1, 90, move: new Point(-43, 0)),
                    CreateIndexedPiece(6, "stabO1", 1, 90, move: new Point(-44, 0)),
                    CreateIndexedPiece(7, "stabO1", 1, 90, move: new Point(-45, 0)),
                    CreateIndexedPiece(8, "stabO1", 0, 90, move: new Point(-11, 0)),
                    CreateIndexedPiece(9, "alert", 2, 60)
                },
                ["darkFog"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, -360, move: new Point(8, 0)),
                    CreateIndexedPiece(1, "alert", 2, -90, move: new Point(8, 0)),
                    CreateIndexedPiece(2, "swingO2", 0, -90, move: new Point(10, 0)),
                    CreateIndexedPiece(3, "swingO2", 0, -1800, move: new Point(11, 0)),
                    CreateIndexedPiece(4, "swingO2", 1, 90, move: new Point(9, 0)),
                    CreateIndexedPiece(5, "swingO2", 2, 630, move: new Point(10, 0))
                },
                ["ghostLettering"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -240, move: new Point(1, 0)),
                    CreateIndexedPiece(1, "swingO1", 2, 120, move: new Point(4, 0)),
                    CreateIndexedPiece(2, "swingO1", 2, 120, move: new Point(-2, 0)),
                    CreateIndexedPiece(3, "swingO1", 2, 120, move: new Point(-3, 0)),
                    CreateIndexedPiece(4, "swingO1", 2, 120, move: new Point(-4, 0)),
                    CreateIndexedPiece(5, "alert", 2, 120, move: new Point(1, 0))
                },
                ["slow"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, 120, move: new Point(1, 0)),
                    CreateIndexedPiece(1, "alert", 1, 120, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 0, 480, move: new Point(1, 0)),
                    CreateIndexedPiece(3, "swingO2", 1, 120, move: new Point(3, 0)),
                    CreateIndexedPiece(4, "swingO1", 2, 120, move: new Point(6, 0)),
                    CreateIndexedPiece(5, "swingO1", 2, 120, move: new Point(3, 0)),
                    CreateIndexedPiece(6, "swingO1", 2, 120, move: new Point(2, 0)),
                    CreateIndexedPiece(7, "alert", 2, 150, move: new Point(1, 0))
                },
                ["mapleHero"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(1, "alert", 1, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 0, 990, move: new Point(1, 0)),
                    CreateIndexedPiece(3, "alert", 2, 1080, move: new Point(1, 0))
                },
                ["OnixProtection"] = new[]
                {
                    CreateIndexedPiece(0, "stand1", 1, 60),
                    CreateIndexedPiece(1, "alert", 2, 120, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 1, 120),
                    CreateIndexedPiece(3, "alert", 0, 510),
                    CreateIndexedPiece(4, "swingO2", 1, 90),
                    CreateIndexedPiece(5, "swingO1", 2, 90),
                    CreateIndexedPiece(6, "swingO1", 2, 90),
                    CreateIndexedPiece(7, "swingO1", 2, 360)
                },
                ["OnixWill"] = new[]
                {
                    CreateIndexedPiece(0, "stand1", 0, 120),
                    CreateIndexedPiece(1, "alert", 2, 120, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 1, 120),
                    CreateIndexedPiece(3, "alert", 0, 540),
                    CreateIndexedPiece(4, "swingO1", 1, 90),
                    CreateIndexedPiece(5, "swingO2", 2, 540)
                },
                ["Awakening"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(1, "alert", 1, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "alert", 0, 120, move: new Point(1, 0)),
                    CreateIndexedPiece(3, "alert", 2, 480, move: new Point(1, 0))
                },
                ["flameWheel"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -60, move: new Point(8, 0)),
                    CreateIndexedPiece(1, "stabO1", 0, -60, move: new Point(25, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, -60, move: new Point(27, 0)),
                    CreateIndexedPiece(3, "stabO1", 0, -90, move: new Point(28, 0)),
                    CreateIndexedPiece(4, "stabO1", 0, -90, move: new Point(29, 0)),
                    CreateIndexedPiece(5, "swingO3", 0, -90, move: new Point(16, 0)),
                    CreateIndexedPiece(6, "stabO2", 1, -90, move: new Point(-5, 0)),
                    CreateIndexedPiece(7, "stabO2", 1, 90, move: new Point(-11, 0)),
                    CreateIndexedPiece(8, "stabO2", 1, 90, move: new Point(-13, 0)),
                    CreateIndexedPiece(9, "stabO2", 1, 270, move: new Point(-14, 0)),
                    CreateIndexedPiece(10, "alert", 2, 360)
                },
                ["tripleStab"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, -120),
                    CreateIndexedPiece(1, "swingO1", 1, 120),
                    CreateIndexedPiece(2, "swingO1", 2, 120),
                    CreateIndexedPiece(3, "swingO3", 0, 120),
                    CreateIndexedPiece(4, "swingO3", 1, 90),
                    CreateIndexedPiece(5, "swingO3", 2, 150),
                    CreateIndexedPiece(6, "alert", 1, 150)
                },
                ["tornadoDash"] = new[]
                {
                    CreateIndexedPiece(0, "swingO3", 2, 90, move: new Point(8, 0)),
                    CreateIndexedPiece(1, "swingO3", 2, 90, move: new Point(8, -2))
                },
                ["tornadoRush"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, 0),
                    CreateIndexedPiece(1, "swingOF", 2, 30, move: new Point(-15, 0), rotationDegrees: 90),
                    CreateIndexedPiece(2, "swingOF", 2, 60, move: new Point(-15, 0), rotationDegrees: 90),
                    CreateIndexedPiece(3, "swingOF", 2, 60, flip: true, move: new Point(15, -44), rotationDegrees: 270),
                    CreateIndexedPiece(4, "swingOF", 1, 60, flip: true, move: new Point(18, -30), rotationDegrees: 270),
                    CreateIndexedPiece(5, "swingOF", 2, 60, move: new Point(-15, 0), rotationDegrees: 90),
                    CreateIndexedPiece(6, "swingOF", 2, 60, flip: true, move: new Point(15, -44), rotationDegrees: 270),
                    CreateIndexedPiece(7, "swingOF", 1, 60, flip: true, move: new Point(18, -25), rotationDegrees: 270),
                    CreateIndexedPiece(8, "swingOF", 2, 90, move: new Point(-15, 0), rotationDegrees: 90),
                    CreateIndexedPiece(9, "swingOF", 3, 150, move: new Point(32, 0)),
                    CreateIndexedPiece(10, "swingOF", 3, 330, move: new Point(25, 0))
                },
                ["tornadoDashStop"] = new[]
                {
                    CreateIndexedPiece(0, "swingOF", 0, 400)
                },
                ["fatalBlow"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -142),
                    CreateIndexedPiece(1, "stabO1", 1, 270, move: new Point(-90, 0)),
                    CreateIndexedPiece(2, "swingO1", 0, 120, move: new Point(-75, 0)),
                    CreateIndexedPiece(3, "swingO1", 1, 120),
                    CreateIndexedPiece(4, "swingO1", 2, 120, move: new Point(21, 0)),
                    CreateIndexedPiece(5, "swingOF", 2, 120, flip: true, move: new Point(0, -65)),
                    CreateIndexedPiece(6, "swingOF", 1, 90, move: new Point(0, -65)),
                    CreateIndexedPiece(7, "swingOF", 2, 90, move: new Point(0, -65)),
                    CreateIndexedPiece(8, "swingOF", 3, 300),
                    CreateIndexedPiece(9, "swingO1", 0, 120),
                    CreateIndexedPiece(10, "alert", 0, 90)
                },
                ["slashStorm1"] = new[]
                {
                    CreateIndexedPiece(0, "swingOF", 3, -120, move: new Point(20, 0)),
                    CreateIndexedPiece(1, "stabO1", 1, -120, flip: true, move: new Point(-39, 0)),
                    CreateIndexedPiece(2, "stabOF", 2, 120),
                    CreateIndexedPiece(3, "swingO3", 2, 120),
                    CreateIndexedPiece(4, "swingO3", 2, 120)
                },
                ["slashStorm2"] = new[]
                {
                    CreateIndexedPiece(0, "swingOF", 3, -120, move: new Point(20, 0)),
                    CreateIndexedPiece(1, "stabO1", 1, 120, flip: true, move: new Point(-39, 0)),
                    CreateIndexedPiece(2, "stabOF", 2, 120),
                    CreateIndexedPiece(3, "swingO3", 2, 120),
                    CreateIndexedPiece(4, "swingOF", 1, 60, move: new Point(0, -47)),
                    CreateIndexedPiece(5, "swingOF", 2, 90, move: new Point(0, -85)),
                    CreateIndexedPiece(6, "swingOF", 3, 150, move: new Point(20, 0))
                },
                ["bloodyStorm"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, 0),
                    CreateIndexedPiece(1, "swingO3", 0, -90, flip: true, move: new Point(0, -101), rotationDegrees: 180),
                    CreateIndexedPiece(2, "swingOF", 2, -90, flip: true, move: new Point(47, -85), rotationDegrees: 270),
                    CreateIndexedPiece(3, "swingO3", 0, -60, flip: true, move: new Point(-15, 0)),
                    CreateIndexedPiece(4, "swingOF", 3, -60, move: new Point(20, 0)),
                    CreateIndexedPiece(5, "stabO1", 1, -60, flip: true, move: new Point(-39, 0)),
                    CreateIndexedPiece(6, "stabOF", 2, 120),
                    CreateIndexedPiece(7, "swingO3", 2, 120),
                    CreateIndexedPiece(8, "swingOF", 1, 60, move: new Point(0, -47)),
                    CreateIndexedPiece(9, "swingOF", 2, 60, move: new Point(0, -85)),
                    CreateIndexedPiece(10, "swingOF", 3, 150, move: new Point(20, 0))
                },
                ["upperStab"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, -120),
                    CreateIndexedPiece(1, "swingPF", 3, -120),
                    CreateIndexedPiece(2, "swingPF", 2, 120, move: new Point(0, -20)),
                    CreateIndexedPiece(3, "swingPF", 2, 120)
                },
                ["chainPull"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 1, 330),
                    CreateIndexedPiece(1, "swingO3", 1, 180),
                    CreateIndexedPiece(2, "stabO1", 0, 150)
                },
                ["chainAttack"] = new[]
                {
                    CreateIndexedPiece(0, "swingO1", 1, 60),
                    CreateIndexedPiece(1, "swingO1", 2, 60),
                    CreateIndexedPiece(2, "swingOF", 1, 90, move: new Point(-35, -15)),
                    CreateIndexedPiece(3, "swingO2", 1, 90, flip: true, move: new Point(-35, -15)),
                    CreateIndexedPiece(4, "swingOF", 3, 90, flip: true, move: new Point(-75, 0)),
                    CreateIndexedPiece(5, "swingO3", 1, 90, move: new Point(-31, 0)),
                    CreateIndexedPiece(6, "swingOF", 1, 90, move: new Point(-55, -65)),
                    CreateIndexedPiece(7, "swingOF", 3, 90, flip: true, move: new Point(-125, -95)),
                    CreateIndexedPiece(8, "swingO3", 1, 90, move: new Point(-55, -115)),
                    CreateIndexedPiece(9, "swingPF", 2, 240, move: new Point(-55, -130)),
                    CreateIndexedPiece(10, "swingOF", 2, 60, move: new Point(-55, -145)),
                    CreateIndexedPiece(11, "swingO1", 2, 270, move: new Point(-55, -170)),
                    CreateIndexedPiece(12, "swingOF", 2, 60, move: new Point(-55, -145)),
                    CreateIndexedPiece(13, "swingOF", 2, 60, move: new Point(-55, -24)),
                    CreateIndexedPiece(14, "swingPF", 3, 60, move: new Point(-55, 0)),
                    CreateIndexedPiece(15, "swingOF", 1, 60, move: new Point(-30, -45)),
                    CreateIndexedPiece(16, "swingOF", 2, 60, move: new Point(-20, -45)),
                    CreateIndexedPiece(17, "swingOF", 1, 60, move: new Point(-10, -20)),
                    CreateIndexedPiece(18, "swingOF", 2, 60)
                },
                ["monsterBombPrepare"] = new[]
                {
                    CreateIndexedPiece(0, "swingPF", 1, 1200)
                },
                ["monsterBombThrow"] = new[]
                {
                    CreateIndexedPiece(0, "swingOF", 1, 150, move: new Point(0, -58)),
                    CreateIndexedPiece(1, "swingOF", 2, 150, move: new Point(0, -115)),
                    CreateIndexedPiece(2, "swingO1", 2, 150, move: new Point(0, -120)),
                    CreateIndexedPiece(3, "swingO1", 2, 150, move: new Point(0, -115)),
                    CreateIndexedPiece(4, "swingOF", 2, 150, move: new Point(0, -58)),
                    CreateIndexedPiece(5, "swingOF", 3, 150, move: new Point(20, 0)),
                    CreateIndexedPiece(6, "swingPF", 1, 150),
                    CreateIndexedPiece(7, "alert", 1, 150)
                },
                ["suddenRaid"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -90),
                    CreateIndexedPiece(1, "alert", 1, -90),
                    CreateIndexedPiece(2, "alert", 0, -90),
                    CreateIndexedPiece(3, "swingOF", 0, -90),
                    CreateIndexedPiece(4, "swingOF", 1, -90),
                    CreateIndexedPiece(5, "swingOF", 2, -360, move: new Point(20, -50)),
                    CreateIndexedPiece(6, "swingOF", 3, -330, move: new Point(20, 0)),
                    CreateIndexedPiece(7, "alert", 1, -210),
                    CreateIndexedPiece(8, "alert", 0, 1350)
                },
                ["finalCutPrepare"] = new[]
                {
                    CreateIndexedPiece(0, "swingOF", 0, 1200)
                },
                ["finalCut"] = new[]
                {
                    CreateIndexedPiece(0, "swingO1", 2, 180),
                    CreateIndexedPiece(1, "swingO1", 2, 780)
                },
                ["phantomBlow"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 1, 90, move: new Point(5, 0)),
                    CreateIndexedPiece(1, "stabT1", 2, 90, move: new Point(15, 0)),
                    CreateIndexedPiece(2, "stabO2", 1, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(3, "swingPF", 1, 90, move: new Point(-2, 0)),
                    CreateIndexedPiece(4, "stabOF", 2, 90, move: new Point(21, 0)),
                    CreateIndexedPiece(5, "swingO1", 2, 90, move: new Point(10, 0)),
                    CreateIndexedPiece(6, "stabT1", 2, 90, move: new Point(-9, 0)),
                    CreateIndexedPiece(7, "swingO1", 0, 90, move: new Point(38, 0)),
                    CreateIndexedPiece(8, "swingO1", 0, 90, move: new Point(42, 0))
                },
                ["bladeFury"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -60),
                    CreateIndexedPiece(1, "swingOF", 3, -90, move: new Point(40, 0)),
                    CreateIndexedPiece(2, "swingOF", 3, 90, move: new Point(-42, -32), rotationDegrees: 90),
                    CreateIndexedPiece(3, "swingOF", 3, 90, move: new Point(-57, -131), rotationDegrees: 180),
                    CreateIndexedPiece(4, "swingOF", 3, 90, move: new Point(33, -152), rotationDegrees: 270),
                    CreateIndexedPiece(5, "swingOF", 3, 90, move: new Point(46, -68)),
                    CreateIndexedPiece(6, "swingOF", 3, 90, move: new Point(-43, -52), rotationDegrees: 90),
                    CreateIndexedPiece(7, "swingOF", 3, 90, move: new Point(-57, -126), rotationDegrees: 180),
                    CreateIndexedPiece(8, "swingOF", 3, 90, move: new Point(30, -111), rotationDegrees: 270),
                    CreateIndexedPiece(9, "alert", 0, 30)
                },
                ["revive"] = CreateIndexedPieces(
                    ("alert", 2, 200),
                    ("alert", 1, 200),
                    ("alert", 0, 200),
                    ("alert", 0, 200),
                    ("alert", 0, 200),
                    ("alert", 0, 200)),
                ["darkChain"] = new[]
                {
                    CreateIndexedPiece(0, "swingO3", 0, -70, move: new Point(4, 0)),
                    CreateIndexedPiece(1, "swingO2", 2, -70, move: new Point(11, 0)),
                    CreateIndexedPiece(2, "swingO2", 1, -70, move: new Point(9, 0)),
                    CreateIndexedPiece(3, "swingO2", 0, -70, move: new Point(5, 0)),
                    CreateIndexedPiece(4, "swingO2", 0, -350, move: new Point(5, 0)),
                    CreateIndexedPiece(5, "stabO1", 0, 420, move: new Point(13, 0))
                },
                ["superBody"] = CreateIndexedPieces(
                    ("alert", 1, -60),
                    ("alert", 0, -600),
                    ("alert", 0, 300),
                    ("alert", 1, 60),
                    ("alert", 2, 420)),
                // Raw action 208 (`swingRes`) is still walked by CActionMan::Init
                // and mounted Character/00002000 publishes it as a direct single
                // frame linked to `swingO2/2` with delay 350.
                ["swingRes"] = CreateIndexedPieces(
                    ("swingO2", 2, 350)),
                ["finishAttack"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 0, -90),
                    CreateIndexedPiece(1, "stabO1", 0, -90, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, -270, move: new Point(3, 0)),
                    CreateIndexedPiece(3, "stabO2", 1, 90, move: new Point(-29, 0)),
                    CreateIndexedPiece(4, "stabO2", 1, 90, move: new Point(-34, 0)),
                    CreateIndexedPiece(5, "stabO2", 1, 90, move: new Point(-35, 0)),
                    CreateIndexedPiece(6, "stabO2", 1, 180, move: new Point(-36, 0)),
                    CreateIndexedPiece(7, "swingO1", 0, 90, move: new Point(-45, 0))
                },
                ["finishAttack_link"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 0, -30),
                    CreateIndexedPiece(1, "stabO1", 0, -60, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, -60, move: new Point(3, 0)),
                    CreateIndexedPiece(3, "stabO1", 0, -60, move: new Point(3, 0)),
                    CreateIndexedPiece(4, "stabO1", 0, -60, move: new Point(3, 0)),
                    CreateIndexedPiece(5, "stabO2", 1, 60, move: new Point(-29, 0)),
                    CreateIndexedPiece(6, "stabO2", 1, 60, move: new Point(-34, 0)),
                    CreateIndexedPiece(7, "stabO2", 1, 60, move: new Point(-35, 0)),
                    CreateIndexedPiece(8, "stabO2", 1, 60, move: new Point(-36, 0)),
                    CreateIndexedPiece(9, "swingO1", 0, 60, move: new Point(-45, 0))
                },
                ["finishAttack_link2"] = new[]
                {
                    CreateIndexedPiece(0, "stabO1", 0, -30),
                    CreateIndexedPiece(1, "stabO1", 0, -30, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, -30, move: new Point(3, 0)),
                    CreateIndexedPiece(3, "stabO1", 0, -60, move: new Point(4, 0)),
                    CreateIndexedPiece(4, "stabO1", 0, -60, move: new Point(4, 0)),
                    CreateIndexedPiece(5, "stabO1", 0, -60, move: new Point(4, 0)),
                    CreateIndexedPiece(6, "stabO2", 1, 90, move: new Point(-29, 0)),
                    CreateIndexedPiece(7, "stabO2", 1, 60, move: new Point(-34, 0)),
                    CreateIndexedPiece(8, "stabO2", 1, 60, move: new Point(-35, 0)),
                    CreateIndexedPiece(9, "stabO2", 1, 60, move: new Point(-37, 0)),
                    CreateIndexedPiece(10, "stabO2", 1, 60, move: new Point(-37, 0)),
                    CreateIndexedPiece(11, "swingOF", 0, 90, move: new Point(-38, 0)),
                    CreateIndexedPiece(12, "alert", 1, 60, move: new Point(-44, 0))
                },
                ["tripleBlow"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, -30),
                    CreateIndexedPiece(1, "swingO1", 0, 60, move: new Point(-5, 0)),
                    CreateIndexedPiece(2, "swingO1", 1, 60, move: new Point(-5, 0)),
                    CreateIndexedPiece(3, "swingO1", 2, 120, move: new Point(7, 0)),
                    CreateIndexedPiece(4, "swingO1", 2, 60, move: new Point(7, 0)),
                    CreateIndexedPiece(5, "swingO3", 0, 90, move: new Point(-21, 0)),
                    CreateIndexedPiece(6, "swingO3", 1, 120, move: new Point(-6, 0)),
                    CreateIndexedPiece(7, "swingO1", 1, 90, move: new Point(-26, 0)),
                    CreateIndexedPiece(8, "stabO1", 1, 210, move: new Point(-17, 0)),
                    CreateIndexedPiece(9, "alert", 1, 60, move: new Point(-22, 0))
                },
                ["quadBlow"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, -30),
                    CreateIndexedPiece(1, "swingO1", 0, 60, move: new Point(-5, 0)),
                    CreateIndexedPiece(2, "swingO3", 0, 90, move: new Point(-21, 0)),
                    CreateIndexedPiece(3, "swingO3", 1, 120, move: new Point(-6, 0)),
                    CreateIndexedPiece(4, "swingO1", 1, 90, move: new Point(-26, 0)),
                    CreateIndexedPiece(5, "stabO2", 1, 120, move: new Point(-17, 0))
                },

                ["deathBlow"] = new[]
                {
                    CreateIndexedPiece(0, "swingPF", 1, 60, move: new Point(9, 0)),
                    CreateIndexedPiece(1, "stabO2", 1, 120, move: new Point(-21, 0)),
                    CreateIndexedPiece(2, "swingO1", 2, 90, move: new Point(-24, 0)),
                    CreateIndexedPiece(3, "swingO2", 1, 60, move: new Point(30, 0)),
                    CreateIndexedPiece(4, "swingO2", 2, 120, move: new Point(17, 0)),
                    CreateIndexedPiece(5, "swingOF", 0, 60, move: new Point(6, 0)),
                    CreateIndexedPiece(6, "stabT2", 2, 120, move: new Point(7, 0)),
                    CreateIndexedPiece(7, "stabT2", 2, 90, flip: true, move: new Point(-57, 0)),
                    CreateIndexedPiece(8, "stabT1", 2, 120, move: new Point(-37, 0)),
                    CreateIndexedPiece(9, "swingOF", 0, 60, move: new Point(-65, 0)),
                    CreateIndexedPiece(10, "alert", 1, 30, move: new Point(-74, 0))
                },
                ["finishBlow"] = new[]
                {
                    CreateIndexedPiece(0, "swingPF", 1, 60, move: new Point(9, 0)),
                    CreateIndexedPiece(1, "stabO2", 1, 120, move: new Point(-3, 0)),
                    CreateIndexedPiece(2, "swingO1", 2, 60, move: new Point(7, 0)),
                    CreateIndexedPiece(3, "swingO2", 1, 60),
                    CreateIndexedPiece(4, "swingO2", 2, 120, move: new Point(-13, 0)),
                    CreateIndexedPiece(5, "swingOF", 0, 60),
                    CreateIndexedPiece(6, "stabT2", 2, 120, move: new Point(-14, 0)),
                    CreateIndexedPiece(7, "swingOF", 1, 90, flip: true, move: new Point(0, -41)),
                    CreateIndexedPiece(8, "swingPF", 2, 120, move: new Point(21, -29)),
                    CreateIndexedPiece(9, "swingPF", 3, 120, move: new Point(30, 1)),
                    CreateIndexedPiece(10, "swingPF", 3, 60, move: new Point(30, 0))
                },
                ["darkLightning"] = new[]
                {
                    CreateIndexedPiece(0, "swingO2", 0, -60, move: new Point(6, 0)),
                    CreateIndexedPiece(1, "swingO2", 1, -60, move: new Point(10, 0)),
                    CreateIndexedPiece(2, "stabO1", 0, -60, move: new Point(14, 0)),
                    CreateIndexedPiece(3, "stabO1", 0, -60, move: new Point(17, 0)),
                    CreateIndexedPiece(4, "stabO1", 0, -60, move: new Point(19, 0)),
                    CreateIndexedPiece(5, "stabO1", 0, -60, move: new Point(20, 0)),
                    CreateIndexedPiece(6, "stabO1", 0, -60, move: new Point(20, 0)),
                    CreateIndexedPiece(7, "stabO2", 1, 90, move: new Point(13, 0)),
                    CreateIndexedPiece(8, "stabO2", 1, 810, move: new Point(12, 0))
                },
                ["cyclone_pre"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 1, 450),
                    CreateIndexedPiece(1, "alert", 0, 180),
                    CreateIndexedPiece(2, "stabO1", 0, 90, move: new Point(10, 0)),
                    CreateIndexedPiece(3, "stabO1", 0, 90, move: new Point(11, 0)),
                    CreateIndexedPiece(4, "stabO1", 0, 90, move: new Point(13, 0)),
                    CreateIndexedPiece(5, "swingO2", 0, 90, move: new Point(1, -6)),
                    CreateIndexedPiece(6, "swingTF", 0, 90, move: new Point(1, -8)),
                    CreateIndexedPiece(7, "swingTF", 0, 90, move: new Point(1, -12)),
                    CreateIndexedPiece(8, "swingTF", 0, 90, move: new Point(1, -12)),
                    CreateIndexedPiece(9, "swingO2", 0, 90, flip: true, move: new Point(-6, -14)),
                    CreateIndexedPiece(10, "swingO2", 0, 90, flip: true, move: new Point(-6, -15)),
                    CreateIndexedPiece(11, "swingO2", 0, 90, move: new Point(1, -15)),
                    CreateIndexedPiece(12, "swingTF", 0, 90, move: new Point(1, -13)),
                    CreateIndexedPiece(13, "swingO2", 0, 90, flip: true, move: new Point(-6, -11))
                },
                ["cyclone"] = new[]
                {
                    CreateIndexedPiece(0, "swingO2", 0, 90, move: new Point(1, -13)),
                    CreateIndexedPiece(1, "swingTF", 0, 90, move: new Point(1, -16)),
                    CreateIndexedPiece(2, "swingO2", 0, 90, flip: true, move: new Point(-6, -18)),
                    CreateIndexedPiece(3, "swingO2", 0, 90, move: new Point(1, -19)),
                    CreateIndexedPiece(4, "swingTF", 0, 90, move: new Point(1, -20)),
                    CreateIndexedPiece(5, "swingO2", 0, 90, flip: true, move: new Point(-6, -20)),
                    CreateIndexedPiece(6, "swingO2", 0, 90, move: new Point(1, -19)),
                    CreateIndexedPiece(7, "swingTF", 0, 90, move: new Point(1, -18)),
                    CreateIndexedPiece(8, "swingO2", 0, 90, flip: true, move: new Point(-6, -16)),
                    CreateIndexedPiece(9, "swingO2", 0, 90, move: new Point(1, -13)),
                    CreateIndexedPiece(10, "swingTF", 0, 90, move: new Point(1, -11)),
                    CreateIndexedPiece(11, "swingO2", 0, 90, flip: true, move: new Point(-6, -11))
                },
                ["cyclone_after"] = new[]
                {
                    CreateIndexedPiece(0, "swingO2", 0, 90, move: new Point(1, -13)),
                    CreateIndexedPiece(1, "swingTF", 0, 90, move: new Point(1, -12)),
                    CreateIndexedPiece(2, "swingO2", 0, 90, flip: true, move: new Point(-6, -11)),
                    CreateIndexedPiece(3, "swingO2", 0, 90, move: new Point(1, -9)),
                    CreateIndexedPiece(4, "swingTF", 0, 90, move: new Point(1, -5)),
                    CreateIndexedPiece(5, "alert", 0, 90, flip: true, move: new Point(-5, 1)),
                    CreateIndexedPiece(6, "alert", 0, 90, move: new Point(-1, 2)),
                    CreateIndexedPiece(7, "alert", 1, 90, move: new Point(0, 1)),
                    CreateIndexedPiece(8, "alert", 2, 90)
                },
                ["lasergun"] = new[]
                {
                    CreateIndexedPiece(0, "shoot2", 0, 90),
                    CreateIndexedPiece(1, "stabO1", 0, 360, move: new Point(2, 0)),
                    CreateIndexedPiece(2, "shoot2", 0, 0)
                },
                ["siege_pre"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, 60),
                    CreateIndexedPiece(1, "sit", 0, 60),
                    CreateIndexedPiece(2, "sit", 0, 60),
                    CreateIndexedPiece(3, "sit", 0, 60),
                    CreateIndexedPiece(4, "sit", 0, 60),
                    CreateIndexedPiece(5, "sit", 0, 60),
                    CreateIndexedPiece(6, "sit", 0, 60),
                    CreateIndexedPiece(7, "sit", 0, 60),
                    CreateIndexedPiece(8, "sit", 0, 60),
                    CreateIndexedPiece(9, "sit", 0, 60),
                    CreateIndexedPiece(10, "sit", 0, 60),
                    CreateIndexedPiece(11, "sit", 0, 60)
                },
                ["siege"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, 30),
                    CreateIndexedPiece(1, "sit", 0, 60),
                    CreateIndexedPiece(2, "sit", 0, 30),
                    CreateIndexedPiece(3, "sit", 0, 60)
                },
                ["siege_stand"] = CreateIndexedPieces(
                    ("sit", 0, 150),
                    ("sit", 0, 150)),
                ["siege_after"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, 60),
                    CreateIndexedPiece(1, "sit", 0, 60),
                    CreateIndexedPiece(2, "sit", 0, 60),
                    CreateIndexedPiece(3, "sit", 0, 60),
                    CreateIndexedPiece(4, "sit", 0, 60),
                    CreateIndexedPiece(5, "sit", 0, 60),
                    CreateIndexedPiece(6, "sit", 0, 60),
                    CreateIndexedPiece(7, "sit", 0, 60),
                    CreateIndexedPiece(8, "sit", 0, 60),
                    CreateIndexedPiece(9, "sit", 0, 60),
                    CreateIndexedPiece(10, "sit", 0, 60),
                    CreateIndexedPiece(11, "sit", 0, 60)
                },
                ["tank_pre"] = CreateIndexedPieces(
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60)),
                ["tank"] = CreateIndexedPieces(
                    ("sit", 0, -30),
                    ("sit", 0, -30),
                    ("sit", 0, -30),
                    ("sit", 0, -30),
                    ("sit", 0, -30),
                    ("sit", 0, -30),
                    ("sit", 0, 30),
                    ("sit", 0, 30),
                    ("sit", 0, 30),
                    ("sit", 0, 30),
                    ("sit", 0, 30),
                    ("sit", 0, 30),
                    ("sit", 0, 30),
                    ("sit", 0, 30)),
                ["tank_walk"] = CreateIndexedPieces(
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 120)),
                ["tank_stand"] = CreateIndexedPieces(
                    ("sit", 0, 150),
                    ("sit", 0, 150)),
                ["tank_prone"] = CreateIndexedPieces(
                    ("sit", 0, 300)),
                ["tank_after"] = CreateIndexedPieces(
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60)),
                ["tank_laser"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, -120),
                    CreateIndexedPiece(1, "sit", 0, -90),
                    CreateIndexedPiece(2, "sit", 0, -90),
                    CreateIndexedPiece(3, "sit", 0, -90),
                    CreateIndexedPiece(4, "sit", 0, -90),
                    CreateIndexedPiece(5, "sit", 0, -90),
                    CreateIndexedPiece(6, "sit", 0, -90),
                    CreateIndexedPiece(7, "sit", 0, 90),
                    CreateIndexedPiece(8, "sit", 0, 90),
                    CreateIndexedPiece(9, "sit", 0, 90),
                    CreateIndexedPiece(10, "sit", 0, 90),
                    CreateIndexedPiece(11, "sit", 0, 90),
                    CreateIndexedPiece(12, "sit", 0, 90),
                    CreateIndexedPiece(13, "sit", 0, 90),
                    CreateIndexedPiece(14, "sit", 0, 90),
                    CreateIndexedPiece(15, "sit", 0, 90)
                },
                ["tank_siegepre"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, 60),
                    CreateIndexedPiece(1, "sit", 0, 60),
                    CreateIndexedPiece(2, "sit", 0, 60),
                    CreateIndexedPiece(3, "sit", 0, 60),
                    CreateIndexedPiece(4, "sit", 0, 60),
                    CreateIndexedPiece(5, "sit", 0, 60),
                    CreateIndexedPiece(6, "sit", 0, 60),
                    CreateIndexedPiece(7, "sit", 0, 60),
                    CreateIndexedPiece(8, "sit", 0, 60)
                },
                ["tank_siegeattack"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, 30),
                    CreateIndexedPiece(1, "sit", 0, 60),
                    CreateIndexedPiece(2, "sit", 0, 30),
                    CreateIndexedPiece(3, "sit", 0, 60)
                },
                ["tank_siegeafter"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, 60),
                    CreateIndexedPiece(1, "sit", 0, 60),
                    CreateIndexedPiece(2, "sit", 0, 60),
                    CreateIndexedPiece(3, "sit", 0, 60),
                    CreateIndexedPiece(4, "sit", 0, 60)
                },
                ["tank_siegestand"] = CreateIndexedPieces(
                    ("sit", 0, 150),
                    ("sit", 0, 150)),
                ["tank_msummon"] = CreateIndexedPieces(
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 270),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90)),
                ["tank_rbooster_pre"] = CreateIndexedPieces(
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60)),
                ["tank_rbooster_after"] = CreateIndexedPieces(
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60)),
                ["tank_msummon2"] = CreateIndexedPieces(
                    ("sit", 0, 540),
                    ("sit", 0, 4140),
                    ("sit", 0, 960)),
                ["rbooster_pre"] = CreateIndexedPieces(
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60)),
                ["rbooster"] = CreateIndexedPieces(
                    ("alert", 0, 60),
                    ("alert", 0, 60)),
                ["rbooster_after"] = CreateIndexedPieces(
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60)),
                ["gatlingshot2"] = CreateIndexedPieces(
                    ("sit", 0, 810)),
                ["doubleJump"] = CreateIndexedPieces(
                    ("sit", 0, 720)),
                ["knockback"] = CreateIndexedPieces(
                    ("sit", 0, -450),
                    ("sit", 0, 600)),
                ["swallow_pre"] = CreateIndexedPieces(
                    ("sit", 0, -810)),
                ["swallow_loop"] = CreateIndexedPieces(
                    ("sit", 0, 270)),
                ["swallow"] = CreateIndexedPieces(
                    ("sit", 0, -810),
                    ("sit", 0, 450)),
                ["swallow_attack"] = CreateIndexedPieces(
                    ("sit", 0, -120),
                    ("sit", 0, 600)),
                ["tank_mRush"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, 90),
                    CreateIndexedPiece(1, "sit", 0, 90)
                },
                ["drillrush"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, -90),
                    CreateIndexedPiece(1, "sit", 0, 810)
                },
                ["giant"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, 100),
                    CreateIndexedPiece(1, "sit", 0, 100)
                },
                ["mbooster"] = CreateIndexedPieces(
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100),
                    ("sit", 0, 100)),
                ["crossRoad"] = CreateIndexedPieces(
                    ("sit", 0, -480),
                    ("sit", 0, 840)),
                ["nemesis"] = CreateIndexedPieces(
                    ("alert", 0, -840),
                    ("alert", 1, -840),
                    ("alert", 2, 1560)),
                ["wildbeast"] = CreateIndexedPieces(
                    ("sit", 0, 1530)),
                ["sonicBoom"] = CreateIndexedPieces(
                    ("sit", 0, -1260),
                    ("sit", 0, 720)),
                ["earthslug"] = CreateIndexedPieces(
                    ("sit", 0, -660),
                    ("sit", 0, 570)),
                ["rpunch"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, -450),
                    CreateIndexedPiece(1, "sit", 0, 660)
                },
                ["flashRain"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, -1320),
                    CreateIndexedPiece(1, "alert", 0, 1320)
                },
                ["clawCut"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, -450),
                    CreateIndexedPiece(1, "sit", 0, 1080)
                },
                ["mine"] = new[]
                {
                    CreateIndexedPiece(0, "sit", 0, 1350)
                },
                ["msummon"] = CreateIndexedPieces(
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60),
                    ("sit", 0, 60)),
                ["msummon2"] = CreateIndexedPieces(
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 90),
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 120),
                    ("sit", 0, 120)),
                ["ride"] = CreateIndexedPieces(
                    ("stand1", 0, 120),
                    ("alert", 1, 120),
                    ("alert", 0, 120),
                    ("jump", 0, 120),
                    ("jump", 0, 120),
                    ("sit", 0, 720)),
                ["getoff"] = CreateIndexedPieces(
                    ("sit", 0, 120),
                    ("jump", 0, 360),
                    ("swingPF", 3, 240),
                    ("alert", 1, 120),
                    ("stand2", 0, 240)),
                ["capture"] = new[]
                {
                    CreateIndexedPiece(0, "shoot2", 1, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(1, "shoot2", 2, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(2, "shoot2", 3, 270, move: new Point(1, 0)),
                    CreateIndexedPiece(3, "shoot2", 4, 90, move: new Point(2, 0)),
                    CreateIndexedPiece(4, "shoot2", 4, 90, move: new Point(1, 0)),
                    CreateIndexedPiece(5, "shoot2", 3, 360, move: new Point(1, 0))
                },
                ["proneStab_jaguar"] = CreateIndexedPieces(
                    ("sit", 0, -200),
                    ("sit", 0, 400)),
                ["herbalism_jaguar"] = CreateIndexedPieces(
                    ("sit", 0, 180)),
                ["mining_jaguar"] = CreateIndexedPieces(
                    ("sit", 0, 180)),
                ["braveslash1"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(1, "swingO2", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(2, "swingO2", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(3, "swingO2", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(4, "swingO2", 2, 90, move: new Point(-7, 0)),
                    CreateIndexedPiece(5, "swingO2", 2, 120, move: new Point(-9, 0)),
                    CreateIndexedPiece(6, "swingOF", 0, 60, move: new Point(-12, 0)),
                    CreateIndexedPiece(7, "swingOF", 1, 90, move: new Point(-12, -6)),
                    CreateIndexedPiece(8, "swingOF", 2, 120, move: new Point(-7, -4)),
                    CreateIndexedPiece(9, "stabO1", 1, 60, move: new Point(-22, 0)),
                    CreateIndexedPiece(10, "stabO1", 1, 90, move: new Point(-23, 0)),
                    CreateIndexedPiece(11, "stabO1", 1, 90, move: new Point(-23, 0))
                },
                ["braveslash2"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(1, "swingO2", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(2, "swingO2", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(3, "swingO2", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(4, "swingO2", 2, 90, move: new Point(-7, 0)),
                    CreateIndexedPiece(5, "swingO2", 2, 120, move: new Point(-9, 0)),
                    CreateIndexedPiece(6, "swingOF", 0, 60, move: new Point(-12, 0)),
                    CreateIndexedPiece(7, "swingOF", 1, 90, move: new Point(-12, -6)),
                    CreateIndexedPiece(8, "swingOF", 2, 120, move: new Point(-7, -4)),
                    CreateIndexedPiece(9, "stabO1", 1, 60, move: new Point(-22, 0)),
                    CreateIndexedPiece(10, "stabO1", 1, 90, move: new Point(-23, 0)),
                    CreateIndexedPiece(11, "stabO1", 1, 90, move: new Point(-23, 0))
                },
                ["braveslash3"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(1, "swingT1", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(2, "swingT1", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(3, "swingT1", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(4, "swingT1", 2, 90, move: new Point(-7, 0)),
                    CreateIndexedPiece(5, "swingT1", 2, 120, move: new Point(-9, 0)),
                    CreateIndexedPiece(6, "swingT3", 1, 60, move: new Point(-18, 0)),
                    CreateIndexedPiece(7, "swingTF", 0, 90, move: new Point(-27, 0)),
                    CreateIndexedPiece(8, "swingTF", 1, 120, move: new Point(-27, 0)),
                    CreateIndexedPiece(9, "stabO1", 1, 60, move: new Point(-22, 0)),
                    CreateIndexedPiece(10, "stabO1", 1, 90, move: new Point(-23, 0)),
                    CreateIndexedPiece(11, "stabO1", 1, 90, move: new Point(-23, 0))
                },
                ["braveslash4"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 2, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(1, "swingT1", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(2, "swingT1", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(3, "swingT1", 0, -30, move: new Point(0, 0)),
                    CreateIndexedPiece(4, "swingT1", 2, 90, move: new Point(-7, 0)),
                    CreateIndexedPiece(5, "swingT1", 2, 120, move: new Point(-9, 0)),
                    CreateIndexedPiece(6, "swingT3", 1, 60, move: new Point(-18, 0)),
                    CreateIndexedPiece(7, "swingTF", 0, 90, move: new Point(-27, 0)),
                    CreateIndexedPiece(8, "swingTF", 1, 120, move: new Point(-27, 0)),
                    CreateIndexedPiece(9, "stabO1", 1, 60, move: new Point(-22, 0)),
                    CreateIndexedPiece(10, "stabO1", 1, 90, move: new Point(-23, 0)),
                    CreateIndexedPiece(11, "stabO1", 1, 90, move: new Point(-23, 0))
                },

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
                // Additional non-attack client-init rows from Character/00002000 still
                // publish concrete helper-piece timing and transform metadata.
                ["holyshield"] = CreateIndexedPieces(
                    ("stabO1", 0, -300),
                    ("stabO1", 1, 840)),
                ["resurrection"] = CreateIndexedPieces(
                    ("alert", 0, -840),
                    ("alert", 1, -840),
                    ("alert", 2, 840)),
                ["dash"] = CreateIndexedPieces(
                    ("walk1", 0, 1)),
                ["octopus"] = new[]
                {
                    CreateIndexedPiece(0, "alert", 0, 0),
                    CreateIndexedPiece(1, "swingPF", 3, 90, move: new Point(37, 0)),
                    CreateIndexedPiece(2, "stabT2", 1, 90, move: new Point(36, -83), rotationDegrees: 270),
                    CreateIndexedPiece(3, "swingT2", 0, 90, move: new Point(-8, -108), rotationDegrees: 180),
                    CreateIndexedPiece(4, "swingP2", 0, 90, move: new Point(-11, -114), rotationDegrees: 180),
                    CreateIndexedPiece(5, "swingP2", 1, 90, move: new Point(-24, -111), rotationDegrees: 180),
                    CreateIndexedPiece(6, "swingT2", 0, 90, move: new Point(-9, -14)),
                    CreateIndexedPiece(7, "swingOF", 3, 270, move: new Point(32, 0)),
                    CreateIndexedPiece(8, "alert", 0, 0)
                },
                ["darksight"] = CreateIndexedPieces(
                    ("alert", 0, 100)),
                // Mounted Character/00002000 ghost helper rows are still published on
                // child `1/*` as concrete fallback frame tables with authored delays.
                // Keep row-shaped plans in fallback so loader-owned timing/event delay
                // stays aligned when mounted rows are unavailable.
                ["ghostwalk"] = CreateIndexedPieces(
                    ("walk1", 0, 180),
                    ("walk1", 1, 180),
                    ("walk1", 0, 180),
                    ("walk1", 1, 180)),
                ["ghoststand"] = CreateIndexedPieces(
                    ("stand1", 0, 500),
                    ("stand1", 1, 500),
                    ("stand1", 2, 500)),
                ["ghostjump"] = CreateIndexedPieces(
                    ("jump", 0, 200)),
                ["ghostproneStab"] = CreateIndexedPieces(
                    ("proneStab", 0, 300),
                    ("proneStab", 1, 300)),
                ["ghostladder"] = CreateIndexedPieces(
                    ("ladder", 0, 250),
                    ("ladder", 1, 250)),
                ["ghostrope"] = CreateIndexedPieces(
                    ("rope", 0, 250),
                    ("rope", 1, 250)),
                ["ghostfly"] = CreateIndexedPieces(
                    ("fly", 0, 300),
                    ("fly", 1, 300)),
                ["ghostsit"] = CreateIndexedPieces(
                    ("sit", 0, 100)),
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
                    CreateIndexedPiece(1, "swingPF", 0, 90, flip: true, move: new Point(-2, 0))
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
            "chargeBlow",
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
            "rain",
            "paralyze",
            "shoot6",
            "arrowRain",
            "arrowEruption",
            "chargeBlow"
        };

        private static readonly HashSet<string> ClientInitializedFallbackOnlyActionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // Mounted Character/00002000 helper rows are still loader-owned fallback
            // inputs for these client-init raw actions even when they are not treated
            // as attack-identity actions by the runtime state machine.
            "ladder2",
            "rope2",
            "smokeshell",
            "fake",
            "flashBang",
            "timeleap",
            "owlDead",
            "homing",
            "recovery",
            "backstep",
            "holyshield",
            "resurrection",
            "dash",
            "octopus",
            "darksight",
            // Character/00002000 still publishes client-init `ghost*` helper rows under
            // the same action-table surface (raw 103..110). Keep these admitted on the
            // fallback seam so missing mounted rows still resolve through loader-owned
            // alias remap instead of dropping out of helper fallback coverage.
            "ghostwalk",
            "ghoststand",
            "ghostjump",
            "ghostproneStab",
            "ghostladder",
            "ghostrope",
            "ghostfly",
            "ghostsit",
            // Mounted raw rows 145..174 also publish concrete helper-piece tables
            // that are not runtime attack identities. Admit them only through the
            // loader fallback surface so missing mounted rows can still use the
            // recovered client-init timing and transform metadata.
            "float",
            "pyramid",
            "breathe_prepare",
            "icebreathe_prepare",
            "elementalReset",
            "magicRegistance",
            "magicBooster",
            "magicShield",
            "recoveryAura",
            "OnixBlessing",
            "soulStone",
            "ghostLettering",
            "slow",
            "mapleHero",
            "OnixProtection",
            "OnixWill",
            "Awakening"
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

        private static readonly HashSet<string> MountedCreateActionFrameNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "create2",
            "create2_s",
            "create2_f",
            "create3",
            "create3_s",
            "create3_f",
            "create4",
            "create4_s",
            "create4_f"
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
                ["dash"] = "dash",
                ["darksight"] = "darksight",
                ["alert2"] = "alert2",
                ["swingO2"] = "swingO2",
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

            bool playerActionFamilyUnsupported = IsFamilyUnsupportedClientRawActionName(playerActionName, supportedRawActionNames);
            if (!string.IsNullOrWhiteSpace(playerActionName))
            {
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

        internal static bool IsMountedCreateActionFrameName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && MountedCreateActionFrameNames.Contains(actionName);
        }

        internal static bool ShouldSynthesizeMountedCharacterActionName(
            string actionName,
            IReadOnlySet<string> supportedRawActionNames)
        {
            if (!string.IsNullOrWhiteSpace(actionName)
                && SupportedRawActionCanonicalNames.ContainsKey(actionName))
            {
                return IsSupportedRawActionName(actionName, supportedRawActionNames);
            }

            if (IsGenericHelperSurfaceActionName(actionName))
            {
                return true;
            }

            if (IsClientInitializedShadowPartnerRawActionName(actionName))
            {
                return true;
            }

            return false;
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

        internal static IEnumerable<string> EnumerateClientInitializedFallbackActionNames()
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string actionName in EnumerateClientInitializedShadowPartnerRawActionNames())
            {
                if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in ClientInitializedFallbackOnlyActionNames)
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
        }

        internal static bool IsClientInitializedShadowPartnerRawActionName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                   && rawActionCode < ClientInitializedShadowPartnerActionCodeLimitExclusive
                   && !ClientActionManInitSkippedRawActionCodes.Contains(rawActionCode);
        }

        internal static bool IsClientActionManInitSkippedActionName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                   && ClientActionManInitSkippedRawActionCodes.Contains(rawActionCode);
        }

        internal static WzImageProperty ResolveClientActionManInitPieceOwnerNode(
            string actionName,
            WzImageProperty actionNode)
        {
            if (actionNode == null)
            {
                return actionNode;
            }

            bool actionNodeHasPieceChildren = ContainsClientActionPieceChildren(actionNode);
            if (!actionNodeHasPieceChildren
                && TryResolveClientActionManInitNumericPieceOwnerNode(actionNode, actionName, out WzImageProperty numericPieceOwnerNode))
            {
                return numericPieceOwnerNode;
            }

            WzImageProperty variantNode = actionNode["1"];
            if (variantNode?.WzProperties == null || variantNode.WzProperties.Count == 0)
            {
                return actionNode;
            }

            if (string.IsNullOrWhiteSpace(actionName)
                || !CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                || rawActionCode < ClientActionManInitVariantRowStartRawActionCode
                || rawActionCode > ClientActionManInitVariantRowEndRawActionCode)
            {
                return actionNode;
            }

            // CActionMan::Init switches raw actions 124..131 to child row "1" before
            // reading action-piece metadata. In the mounted export, row "1" can itself
            // be a single piece row; the parser handles that shape directly.
            return variantNode;
        }

        private static bool TryResolveClientActionManInitNumericPieceOwnerNode(
            WzImageProperty actionNode,
            string actionName,
            out WzImageProperty pieceOwnerNode)
        {
            pieceOwnerNode = null;
            if (actionNode?.WzProperties == null || actionNode.WzProperties.Count == 0)
            {
                return false;
            }

            bool prefersVariantOne = !string.IsNullOrWhiteSpace(actionName)
                                     && CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                                     && rawActionCode >= ClientActionManInitVariantRowStartRawActionCode
                                     && rawActionCode <= ClientActionManInitVariantRowEndRawActionCode;

            var candidateRows = new List<(int Index, WzImageProperty Node)>();
            foreach (WzImageProperty child in actionNode.WzProperties)
            {
                if (child == null
                    || !int.TryParse(child.Name, out int numericIndex)
                    || !ContainsClientActionPieceChildren(child)
                    || IsClientActionPieceNode(child))
                {
                    continue;
                }

                candidateRows.Add((numericIndex, child));
            }

            if (candidateRows.Count == 0)
            {
                return false;
            }

            if (prefersVariantOne)
            {
                WzImageProperty preferredRow = candidateRows
                    .Where(static entry => entry.Index == 1)
                    .Select(static entry => entry.Node)
                    .FirstOrDefault();
                if (preferredRow != null)
                {
                    pieceOwnerNode = preferredRow;
                    return true;
                }
            }

            pieceOwnerNode = candidateRows
                .OrderBy(static entry => entry.Index)
                .Select(static entry => entry.Node)
                .First();
            return true;
        }

        internal static bool ContainsClientActionPieceChildren(WzImageProperty node)
        {
            return node?.WzProperties != null
                   && node.WzProperties.Any(static child => IsClientActionPieceNode(child));
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
                || IsClientActionManInitSkippedActionName(actionName)
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
                if (!TryResolvePieceAnimation(
                        actionAnimations,
                        piece.PieceActionName,
                        supportedRawActionNames,
                        out SkillAnimation pieceAnimation))
                {
                    return null;
                }

                firstPieceAnimation ??= pieceAnimation;
                if (piece.SourceFrameIndex.HasValue)
                {
                    int frameIndex = piece.SourceFrameIndex.Value;
                    if (frameIndex < 0 || frameIndex >= pieceAnimation.Frames.Count)
                    {
                        return null;
                    }

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
                Loop = ShouldLoopShadowPartnerAction(actionName),
                Origin = firstPieceAnimation?.Origin ?? Point.Zero,
                ZOrder = firstPieceAnimation?.ZOrder ?? 0,
                PositionCode = firstPieceAnimation?.PositionCode,
                ClientEventDelayMs = ResolveClientActionManInitEventDelayMs(piecePlan)
            };
            piecedAnimation.CalculateDuration();
            return piecedAnimation;
        }

        private static bool TryResolvePieceAnimation(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string pieceActionName,
            IReadOnlySet<string> supportedRawActionNames,
            out SkillAnimation pieceAnimation)
        {
            pieceAnimation = null;
            if (actionAnimations == null
                || actionAnimations.Count == 0
                || string.IsNullOrWhiteSpace(pieceActionName))
            {
                return false;
            }

            if (actionAnimations.TryGetValue(pieceActionName, out pieceAnimation)
                && pieceAnimation?.Frames != null
                && pieceAnimation.Frames.Count > 0)
            {
                return true;
            }

            foreach (string remappedCandidate in EnumerateLoaderRemappedActionCandidates(pieceActionName))
            {
                if (string.IsNullOrWhiteSpace(remappedCandidate)
                    || !actionAnimations.TryGetValue(remappedCandidate, out pieceAnimation)
                    || pieceAnimation?.Frames == null
                    || pieceAnimation.Frames.Count == 0)
                {
                    continue;
                }

                return true;
            }

            pieceAnimation = ResolvePlaybackAnimation(
                actionAnimations,
                pieceActionName,
                pieceActionName,
                rawActionName: pieceActionName,
                supportedRawActionNames: supportedRawActionNames);
            return pieceAnimation?.Frames != null && pieceAnimation.Frames.Count > 0;
        }

        internal static int ResolveClientActionManInitEventDelayMs(
            IReadOnlyList<ShadowPartnerActionPiece> piecePlan)
        {
            if (piecePlan == null || piecePlan.Count == 0)
            {
                return 0;
            }

            ShadowPartnerActionPiece[] orderedPieces = piecePlan
                .OrderBy(static piece => piece.SlotIndex)
                .ToArray();

            bool hasClientActionManInitPieces = false;
            bool hasSyntheticMirroredTail = false;
            foreach (ShadowPartnerActionPiece piece in orderedPieces)
            {
                if (!piece.IsSyntheticMirroredTailPiece && piece.EventDelayOverrideMs.HasValue)
                {
                    return Math.Max(0, piece.EventDelayOverrideMs.Value);
                }
            }

            int eventDelayMs = 0;
            foreach (ShadowPartnerActionPiece piece in orderedPieces)
            {
                hasClientActionManInitPieces |= piece.IsClientActionManInitPiece;
                hasSyntheticMirroredTail |= piece.IsSyntheticMirroredTailPiece;
                if (piece.IsSyntheticMirroredTailPiece
                    || !piece.DelayOverrideMs.HasValue)
                {
                    continue;
                }

                if (piece.DelayOverrideMs.Value < 0)
                {
                    eventDelayMs += Math.Abs(piece.DelayOverrideMs.Value);
                }
            }

            if (hasSyntheticMirroredTail)
            {
                return 0;
            }

            if (eventDelayMs > 0)
            {
                return eventDelayMs;
            }

            if (!hasClientActionManInitPieces || hasSyntheticMirroredTail)
            {
                return 0;
            }

            int totalDelayMs = 0;
            int lastFrameDelayMs = 0;
            bool hasResolvedFrameDelay = false;
            foreach (ShadowPartnerActionPiece piece in orderedPieces)
            {
                if (piece.IsSyntheticMirroredTailPiece || !piece.DelayOverrideMs.HasValue)
                {
                    continue;
                }

                int resolvedDelay = ResolvePlaybackFrameDurationMs(piece.DelayOverrideMs.Value);
                totalDelayMs += resolvedDelay;
                lastFrameDelayMs = resolvedDelay;
                hasResolvedFrameDelay = true;
            }

            if (!hasResolvedFrameDelay)
            {
                return 0;
            }

            return Math.Max(0, totalDelayMs - lastFrameDelayMs);
        }

        internal static bool ShouldLoopShadowPartnerAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (actionName.StartsWith("create", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "dead", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
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

            foreach (string resolvedActionName in EnumerateLoaderRemappedActionCandidates(actionName))
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

        private static IEnumerable<string> EnumerateLoaderRemappedActionCandidates(string actionName)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in EnumerateActionSpecificAliasCandidates(actionName))
            {
                if (!string.IsNullOrWhiteSpace(candidate) && yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (string candidate in EnumerateHeuristicAttackAliases(actionName, PlayerState.Attacking, weaponType: null))
            {
                if (!string.IsNullOrWhiteSpace(candidate) && yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
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
            bool hasMorphTransform,
            bool hasGhostTransform = false)
        {
            if (IsClientBackAction(observedPlayerActionName, state, rawActionCode, hasMorphTransform, hasGhostTransform))
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

            int elapsedTime = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(
                currentTime,
                transitionStartTime);
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

            if (rawActionCode.HasValue
                && (rawActionCode.Value >= ClientInitializedShadowPartnerActionCodeLimitExclusive
                    || ClientActionManInitSkippedRawActionCodes.Contains(rawActionCode.Value)))
            {
                return false;
            }

            // CActionMan::Init seeds helper rows only for raw actions 0..272
            // (skipping 55). CUser::PrepareShadowPartnerActionLayer still keeps
            // raw action 47 on the helper path; in v95 this resolves through
            // Character/00002000.img/arrowEruption.
            return true;
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
                if (animation.ClientEventDelayMs.HasValue)
                {
                    return Math.Max(0, animation.ClientEventDelayMs.Value);
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

        public static bool ShouldHoldBlockingAction(
            string actionName,
            SkillAnimation playbackAnimation,
            int elapsedTimeMs)
        {
            return ShouldHoldBlockingAction(
                actionName,
                playbackAnimation,
                actionAnimations: null,
                elapsedTimeMs);
        }

        public static bool ShouldHoldBlockingAction(
            string actionName,
            SkillAnimation playbackAnimation,
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            int elapsedTimeMs)
        {
            if (!IsBlockingAction(actionName))
            {
                return false;
            }

            SkillAnimation resolvedPlaybackAnimation = playbackAnimation;
            if ((resolvedPlaybackAnimation?.Frames == null || resolvedPlaybackAnimation.Frames.Count == 0)
                && !string.IsNullOrWhiteSpace(actionName)
                && actionAnimations != null)
            {
                actionAnimations.TryGetValue(actionName, out resolvedPlaybackAnimation);
            }

            return resolvedPlaybackAnimation?.Frames != null
                   && resolvedPlaybackAnimation.Frames.Count > 0
                   && !IsPlaybackComplete(resolvedPlaybackAnimation, elapsedTimeMs);
        }

        public static int ResolvePlaybackFrameDurationMs(int frameDelayMs)
        {
            return Math.Max(1, Math.Abs(frameDelayMs));
        }

        public static float ResolveFrameAlpha(SkillFrame frame, int frameElapsedMs)
        {
            if (frame == null)
            {
                return 1f;
            }

            int startAlpha = Math.Clamp(frame.AlphaStart, 0, 255);
            int endAlpha = Math.Clamp(frame.AlphaEnd, 0, 255);
            int delay = ResolvePlaybackFrameDurationMs(frame.Delay);
            if (startAlpha == endAlpha || delay <= 1)
            {
                return startAlpha / 255f;
            }

            float progress = MathHelper.Clamp(frameElapsedMs / (float)delay, 0f, 1f);
            return MathHelper.Lerp(startAlpha, endAlpha, progress) / 255f;
        }

        public static float ResolveFrameAlphaForPlayback(
            SkillAnimation playbackAnimation,
            SkillFrame frame,
            int frameElapsedMs,
            int actionElapsedMs)
        {
            SkillFrame alphaFrame = frame;
            int alphaFrameElapsedMs = frameElapsedMs;
            if (ShouldClampLoopedPlaybackAlphaEnvelope(playbackAnimation, actionElapsedMs)
                && TryResolvePlaybackTerminalFrameEndState(
                    playbackAnimation,
                    out SkillFrame clampedFrame,
                    out int clampedFrameElapsedMs))
            {
                alphaFrame = clampedFrame;
                alphaFrameElapsedMs = clampedFrameElapsedMs;
            }
            else if (ShouldClampCompletedOneShotPlaybackAlphaEnvelope(playbackAnimation, actionElapsedMs)
                     && TryResolvePlaybackTerminalFrameEndState(
                         playbackAnimation,
                         out SkillFrame terminalFrame,
                         out int terminalFrameElapsedMs))
            {
                alphaFrame = terminalFrame;
                alphaFrameElapsedMs = terminalFrameElapsedMs;
            }

            return ResolveFrameAlpha(alphaFrame, alphaFrameElapsedMs);
        }

        public static bool ShouldPreserveOneShotAlphaLifetimeOnFacingChange(
            SkillAnimation playbackAnimation,
            int actionElapsedMs)
        {
            if (playbackAnimation?.Loop != false
                || playbackAnimation.Frames == null
                || playbackAnimation.Frames.Count == 0)
            {
                return false;
            }

            int totalDurationMs = ResolvePlaybackTotalDurationMs(playbackAnimation);
            return totalDurationMs > 0
                   && actionElapsedMs >= totalDurationMs
                   && HasAuthoredFrameAlphaEnvelope(playbackAnimation);
        }

        private static bool ShouldClampLoopedPlaybackAlphaEnvelope(
            SkillAnimation playbackAnimation,
            int actionElapsedMs)
        {
            if (playbackAnimation?.Loop != true
                || playbackAnimation.Frames == null
                || playbackAnimation.Frames.Count == 0)
            {
                return false;
            }

            int totalDurationMs = ResolvePlaybackTotalDurationMs(playbackAnimation);
            return totalDurationMs > 0
                   && actionElapsedMs >= totalDurationMs
                   && HasAuthoredFrameAlphaEnvelope(playbackAnimation);
        }

        private static bool ShouldClampCompletedOneShotPlaybackAlphaEnvelope(
            SkillAnimation playbackAnimation,
            int actionElapsedMs)
        {
            if (playbackAnimation?.Loop != false
                || playbackAnimation.Frames == null
                || playbackAnimation.Frames.Count == 0)
            {
                return false;
            }

            int totalDurationMs = ResolvePlaybackTotalDurationMs(playbackAnimation);
            return totalDurationMs > 0
                   && actionElapsedMs >= totalDurationMs
                   && HasAuthoredFrameAlphaEnvelope(playbackAnimation);
        }

        private static bool TryResolvePlaybackTerminalFrameEndState(
            SkillAnimation playbackAnimation,
            out SkillFrame frame,
            out int frameElapsedMs)
        {
            frame = null;
            frameElapsedMs = 0;
            if (playbackAnimation?.Frames == null || playbackAnimation.Frames.Count == 0)
            {
                return false;
            }

            frame = playbackAnimation.Frames[^1];
            frameElapsedMs = ResolvePlaybackFrameDurationMs(frame?.Delay ?? 0);
            return true;
        }

        private static bool HasAuthoredFrameAlphaEnvelope(SkillAnimation playbackAnimation)
        {
            if (playbackAnimation?.Frames == null)
            {
                return false;
            }

            foreach (SkillFrame frame in playbackAnimation.Frames)
            {
                if (frame == null)
                {
                    continue;
                }

                if (frame.HasAlphaStart
                    || frame.HasAlphaEnd
                    || frame.AlphaStart != 255
                    || frame.AlphaEnd != 255)
                {
                    return true;
                }
            }

            return false;
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

            // Preserve `create2/3/4` priority from authored `special/*` rows, then
            // fall back to mounted legacy helper rows (`create1`, `create0`).
            foreach (string candidate in new[] { "create2", "create3", "create4", "create1", "create0" })
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

        internal static bool ShouldSynthesizeClientInitializedFallbackAction(string actionName)
        {
            return IsAttackAction(actionName)
                   || IsGenericHelperSurfaceActionName(actionName)
                   || IsClientInitializedBuiltInPieceActionName(actionName)
                   || (!string.IsNullOrWhiteSpace(actionName)
                       && ClientInitializedFallbackOnlyActionNames.Contains(actionName));
        }

        private static bool IsClientInitializedBuiltInPieceActionName(string actionName)
        {
            return IsPiecedShadowPartnerActionName(actionName)
                   && IsClientInitializedShadowPartnerRawActionName(actionName);
        }

        public static bool ShouldUseAttackIdentityForObservation(string observedPlayerActionName, PlayerState state)
        {
            return state == PlayerState.Attacking || IsAttackAction(observedPlayerActionName);
        }

        public static bool ShouldRetryAttackResolutionAfterCreate(
            string currentActionName,
            string pendingActionName,
            string queuedActionName,
            bool currentActionBlockingHoldActive)
        {
            if (string.IsNullOrWhiteSpace(currentActionName)
                || !currentActionName.StartsWith("create", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (currentActionBlockingHoldActive)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(pendingActionName))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(queuedActionName);
        }

        public static bool ShouldForceReplayForAttackTrigger(
            int observedActionTriggerTime,
            int previousReplayTriggerTime)
        {
            return observedActionTriggerTime != int.MinValue
                   && observedActionTriggerTime != previousReplayTriggerTime;
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
            bool hasMorphTransform,
            bool hasGhostTransform = false)
        {
            if (state is PlayerState.Ladder or PlayerState.Rope)
            {
                return true;
            }

            if (rawActionCode.HasValue && IsClientBackRawAction(rawActionCode.Value, hasMorphTransform || hasGhostTransform))
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

        internal static bool IsClientGhostActionName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && (string.Equals(actionName, "ghost", StringComparison.OrdinalIgnoreCase)
                       || actionName.StartsWith("ghost", StringComparison.OrdinalIgnoreCase));
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

        public static bool TryResolveAttackIdentityActionName(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string playerActionName,
            PlayerState state,
            out string resolvedActionName,
            string weaponType = null,
            int? rawActionCode = null,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            resolvedActionName = null;
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return false;
            }

            foreach (string candidate in EnumerateHelperIdentityCandidates(
                         playerActionName,
                         state,
                         weaponType,
                         rawActionCode,
                         supportedRawActionNames))
            {
                if (string.IsNullOrWhiteSpace(candidate)
                    || !IsAttackAction(candidate, rawActionCode)
                    || !actionAnimations.TryGetValue(candidate, out SkillAnimation animation)
                    || animation?.Frames == null
                    || animation.Frames.Count == 0)
                {
                    continue;
                }

                resolvedActionName = candidate;
                return true;
            }

            return false;
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
                    pieceFrames[i].SourceFrameIndex,
                    IsClientActionManInitPiece: true);
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
                    pieceFrames[i].DelayOverrideMs,
                    IsClientActionManInitPiece: true);
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
                    pieceFrames[i].Flip,
                    IsClientActionManInitPiece: true);
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
                rotationDegrees,
                IsClientActionManInitPiece: true);
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
