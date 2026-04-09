using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character.Skills;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Character
{
    #region Enums

    /// <summary>
    /// Character animation action states (matching client action strings)
    /// </summary>
    public enum CharacterAction
    {
        // Basic states
        Stand1,
        Stand2,
        Walk1,
        Walk2,

        // Movement
        Jump,
        Sit,
        Prone,
        ProneStab,

        // Climbing
        Ladder,
        Rope,

        // Flying/Swimming
        Fly,
        Swim,

        // Combat - stab attacks
        StabO1,
        StabO2,
        StabOF,
        StabT1,
        StabT2,
        StabTF,

        // Combat - swing attacks
        SwingO1,
        SwingO2,
        SwingO3,
        SwingOF,
        SwingT1,
        SwingT2,
        SwingT3,
        SwingTF,
        SwingP1,
        SwingP2,
        SwingPF,

        // Combat - shoot attacks
        Shoot1,
        Shoot2,
        ShootF,

        // Status
        Alert,
        Heal,
        Dead,
        Ghost,

        // Custom/special
        Custom
    }

    /// <summary>
    /// Character part types for z-ordering
    /// </summary>
    public enum CharacterPartType
    {
        Body,
        Head,
        Face,
        Hair,
        HairOverHead,
        HairBelowBody,
        Cap,
        CapOverHair,
        CapBelowAccessory,
        Accessory,
        AccessoryOverHair,
        Face_Accessory,
        Eye_Accessory,
        Earrings,
        Coat,
        Longcoat,
        Pants,
        Shoes,
        Glove,
        Shield,
        Cape,
        Weapon,
        WeaponOverGlove,
        WeaponOverHand,
        WeaponOverBody,
        WeaponBelowArm,
        Arm,
        ArmOverHair,
        ArmOverHairBelowWeapon,
        Hand,
        HandBelowWeapon,
        HandOverHair,
        Ear,
        TamingMob,
        Morph,
        PortableChair
    }

    /// <summary>
    /// Equipment slot types
    /// </summary>
    public enum EquipSlot
    {
        None = 0,
        Cap = 1,
        FaceAccessory = 2,
        EyeAccessory = 3,
        Earrings = 4,
        Coat = 5,
        Longcoat = 6,
        Pants = 7,
        Shoes = 8,
        Glove = 9,
        Shield = 10,
        Cape = 20, // temporary
        Ring1 = 12,
        Ring2 = 13,
        Ring3 = 14,
        Ring4 = 15,
        Pendant = 17,
        TamingMob = 18,
        Saddle = 19,
        Medal = 49,
        Belt = 50,
        Shoulder = 51,
        Pocket = 52,
        Badge = 53,
        Pendant2 = 59,
        Weapon = 11,
        Android = 166,
        AndroidHeart = 167
    }

    /// <summary>
    /// Character gender
    /// </summary>
    public enum CharacterGender
    {
        Male = 0,
        Female = 1
    }

    /// <summary>
    /// Skin color type (determines body/head image ID)
    /// </summary>
    public enum SkinColor
    {
        Light = 0,      // 00002000, 00012000
        Tan = 1,        // 00002001, 00012001
        Dark = 2,       // 00002002, 00012002
        Pale = 3,       // 00002003, 00012003
        Blue = 4,       // 00002004, 00012004 (Mercedes)
        Green = 5,      // 00002005, 00012005
        White = 9,      // 00002009, 00012009
        Pink = 10,      // 00002010, 00012010
        Grey = 11       // 00002011, 00012011
    }

    #endregion

    #region Frame Data

    /// <summary>
    /// Sub-part of a character frame (body contains body, arm, lHand, rHand sub-parts)
    /// </summary>
    public class CharacterSubPart
    {
        public string Name { get; set; }            // Part name (body, arm, lHand, rHand)
        public IDXObject Texture { get; set; }
        public Point Origin { get; set; }
        public string Z { get; set; }               // Z-layer identifier
        public Dictionary<string, Point> Map { get; set; } = new(); // Map points
        public Point NavelOffset { get; set; }      // Offset from body navel for positioning

        /// <summary>
        /// Get a map point by name, with fallback to origin
        /// </summary>
        public Point GetMapPoint(string name)
        {
            if (Map.TryGetValue(name, out var point))
                return point;
            return Origin;
        }
    }

    /// <summary>
    /// Single frame of a character part animation
    /// </summary>
    public class CharacterFrame
    {
        public IDXObject Texture { get; set; }
        public Point Origin { get; set; }
        public int Delay { get; set; } = 100;
        public string Z { get; set; }               // Z-layer identifier
        public Dictionary<string, Point> Map { get; set; } = new(); // Map points (navel, hand, etc.)
        public bool Flip { get; set; }
        public Rectangle Bounds { get; set; }

        /// <summary>
        /// Sub-parts for body frames (body, arm, lHand, rHand)
        /// If this list is non-empty, render these instead of the main Texture
        /// </summary>
        public List<CharacterSubPart> SubParts { get; set; } = new();

        /// <summary>
        /// Get a map point by name, with fallback to origin
        /// </summary>
        public Point GetMapPoint(string name)
        {
            if (Map.TryGetValue(name, out var point))
                return point;
            return Origin;
        }

        /// <summary>
        /// Check if this frame has sub-parts that should be rendered separately
        /// </summary>
        public bool HasSubParts => SubParts != null && SubParts.Count > 0;
    }

    /// <summary>
    /// Animation sequence for a character part
    /// </summary>
    public class CharacterAnimation
    {
        public CharacterAction Action { get; set; }
        public string ActionName { get; set; }          // Original action string
        public List<CharacterFrame> Frames { get; set; } = new();
        public int TotalDuration { get; private set; }
        public bool Loop { get; set; } = true;

        public void CalculateTotalDuration()
        {
            TotalDuration = 0;
            foreach (var frame in Frames)
            {
                TotalDuration += frame.Delay;
            }
        }

        /// <summary>
        /// Get frame at a given time, with optional looping
        /// </summary>
        public CharacterFrame GetFrameAtTime(int timeMs, out int frameIndex)
        {
            frameIndex = 0;
            if (Frames.Count == 0)
                return null;

            if (TotalDuration == 0)
            {
                CalculateTotalDuration();
                if (TotalDuration == 0)
                {
                    return Frames[0];
                }
            }

            int time = Loop ? (timeMs % TotalDuration) : Math.Min(timeMs, TotalDuration);
            int elapsed = 0;

            for (int i = 0; i < Frames.Count; i++)
            {
                elapsed += Frames[i].Delay;
                if (time < elapsed)
                {
                    frameIndex = i;
                    return Frames[i];
                }
            }

            frameIndex = Frames.Count - 1;
            return Frames[^1];
        }
    }

    #endregion

    #region Part Data

    /// <summary>
    /// Character part data (body, hair, face, equipment item)
    /// </summary>
    public class CharacterPart
    {
        // Client melee raw action codes come from get_action_name_from_code, which
        // CActionMan::Init resolves against the ordered action roots in Character/00002000.img.
        private static readonly IReadOnlyDictionary<int, string> ClientRawActionCodeMap =
            new Dictionary<int, string>
            {
                [0] = "walk1",
                [1] = "walk2",
                [2] = "stand1",
                [3] = "stand2",
                [4] = "alert",
                [5] = "swingO1",
                [6] = "swingO2",
                [7] = "swingO3",
                [8] = "swingOF",
                [9] = "swingT1",
                [10] = "swingT2",
                [11] = "swingT3",
                [12] = "swingTF",
                [13] = "swingP1",
                [14] = "swingP2",
                [15] = "swingPF",
                [16] = "stabO1",
                // Client: get_action_name_from_code(17) feeds the same stabO2 afterimage slot
                // that 1221009 remaps onto before GetMeleeAttackRange/RegisterAfterimage.
                [17] = "stabO2",
                [18] = "stabOF",
                [19] = "stabT1",
                [20] = "stabT2",
                [21] = "stabTF",
                [22] = "shoot1",
                [23] = "shoot2",
                [24] = "shootF",
                [25] = "proneStab",
                [26] = "prone",
                [27] = "heal",
                [28] = "fly",
                [29] = "jump",
                [30] = "sit",
                [31] = "ladder",
                [32] = "rope",
                [33] = "dead",
                [34] = "savage",
                [35] = "alert2",
                [36] = "alert3",
                [37] = "alert4",
                [38] = "alert5",
                [39] = "alert6",
                [40] = "alert7",
                [41] = "rain",
                [42] = "paralyze",
                [43] = "ladder2",
                [44] = "rope2",
                [45] = "shoot6",
                [46] = "arrowRain",
                [47] = "arrowEruption",
                [48] = "magic1",
                [49] = "magic2",
                [50] = "magic3",
                [51] = "magic4",
                [52] = "magic5",
                [53] = "magic6",
                [54] = "explosion",
                [55] = "iceStrike",
                [56] = "burster1",
                [57] = "burster2",
                [58] = "avenger",
                [59] = "assaulter",
                [60] = "prone2",
                [61] = "assassination",
                [62] = "assassinationS",
                [63] = "rush",
                [64] = "rush2",
                [65] = "sanctuary",
                [66] = "meteor",
                [67] = "blizzard",
                [68] = "genesis",
                [69] = "brandish1",
                [70] = "brandish2",
                [71] = "ninjastorm",
                [72] = "chainlightning",
                [73] = "blast",
                [74] = "showdown",
                [75] = "smokeshell",
                [76] = "holyshield",
                [77] = "resurrection",
                [78] = "straight",
                [79] = "handgun",
                [80] = "somersault",
                [81] = "doublefire",
                [82] = "triplefire",
                [83] = "fake",
                [84] = "doubleupper",
                [85] = "eburster",
                [86] = "screw",
                [87] = "dash",
                [88] = "backspin",
                [89] = "eorb",
                [90] = "dragonstrike",
                [91] = "airstrike",
                [92] = "edrain",
                [93] = "octopus",
                [94] = "backstep",
                [95] = "timeleap",
                [96] = "shot",
                [97] = "recovery",
                [98] = "fist",
                [99] = "fireburner",
                [100] = "coolingeffect",
                [101] = "homing",
                [102] = "rapidfire",
                [103] = "ghostwalk",
                [104] = "ghoststand",
                [105] = "ghostjump",
                [106] = "ghostproneStab",
                [107] = "ghostladder",
                [108] = "ghostrope",
                [109] = "ghostfly",
                [110] = "ghostsit",
                [111] = "cannon",
                [112] = "torpedo",
                [113] = "darksight",
                [114] = "bamboo",
                [115] = "wave",
                [116] = "blade",
                [117] = "souldriver",
                [118] = "firestrike",
                [119] = "flamegear",
                [120] = "stormbreak",
                [121] = "shockwave",
                [122] = "demolition",
                [123] = "snatch",
                [124] = "windspear",
                [125] = "windshot",
                [126] = "vampire",
                [127] = "swingT2PoleArm",
                [128] = "swingP1PoleArm",
                [129] = "swingP2PoleArm",
                [130] = "combatStep",
                [131] = "doubleSwing",
                [132] = "tripleSwing",
                [133] = "finalCharge",
                [134] = "finalToss",
                [135] = "finalBlow",
                [136] = "comboSmash",
                [137] = "comboFenrir",
                [138] = "fullSwingDouble",
                [139] = "fullSwingTriple",
                [140] = "overSwingDouble",
                [141] = "overSwingTriple",
                [142] = "rollingSpin",
                [143] = "comboTempest",
                [144] = "comboJudgement",
                [145] = "float",
                [146] = "pyramid",
                [147] = "magicmissile",
                [148] = "fireCircle",
                [149] = "lightingBolt",
                [150] = "dragonBreathe",
                [151] = "breathe_prepare",
                [152] = "icebreathe_prepare",
                [153] = "blaze",
                [154] = "illusion",
                [155] = "dragonIceBreathe",
                [156] = "magicFlare",
                [157] = "elementalReset",
                [158] = "magicRegistance",
                [159] = "magicBooster",
                [160] = "magicShield",
                [161] = "killingWing",
                [162] = "recoveryAura",
                [163] = "OnixBlessing",
                [164] = "Earthquake",
                [165] = "soulStone",
                [166] = "dragonThrust",
                [167] = "darkFog",
                [168] = "ghostLettering",
                [169] = "slow",
                [170] = "flameWheel",
                [171] = "mapleHero",
                [172] = "OnixProtection",
                [173] = "OnixWill",
                [174] = "Awakening",
                [175] = "fly2",
                [176] = "fly2Move",
                [177] = "fly2Skill",
                [178] = "swingD1",
                [179] = "swingD2",
                [180] = "stabD1",
                [181] = "tripleStab",
                [182] = "flyingAssaulter",
                [183] = "tornadoDash",
                [184] = "tornadoRush",
                [185] = "tornadoDashStop",
                [186] = "fatalBlow",
                [187] = "slashStorm1",
                [188] = "slashStorm2",
                [189] = "bloodyStorm",
                [190] = "flashBang",
                [191] = "owlDead",
                [192] = "upperStab",
                [193] = "chainPull",
                [194] = "chainAttack",
                [195] = "monsterBombPrepare",
                [196] = "monsterBombThrow",
                [197] = "suddenRaid",
                [198] = "finalCutPrepare",
                [199] = "finalCut",
                [200] = "phantomBlow",
                [201] = "bladeFury",
                [202] = "revive",
                [203] = "darkChain",
                [204] = "superBody",
                [205] = "finishAttack",
                [206] = "finishAttack_link",
                [207] = "finishAttack_link2",
                [208] = "swingRes",
                [209] = "tripleBlow",
                [210] = "quadBlow",
                [211] = "deathBlow",
                [212] = "finishBlow",
                [213] = "darkLightning",
                [214] = "cyclone_pre",
                [215] = "cyclone",
                [216] = "cyclone_after",
                [217] = "lasergun",
                [218] = "siege_pre",
                [219] = "siege",
                [220] = "siege_stand",
                [221] = "siege_after",
                [222] = "tank_pre",
                [223] = "tank",
                [224] = "tank_walk",
                [225] = "tank_stand",
                [226] = "tank_prone",
                [227] = "tank_after",
                [228] = "tank_laser",
                [229] = "tank_siegepre",
                [230] = "tank_siegeattack",
                [231] = "tank_siegestand",
                [232] = "tank_siegeafter",
                [233] = "tank_msummon",
                [234] = "tank_rbooster_pre",
                [235] = "tank_rbooster_after",
                [236] = "tank_msummon2",
                [237] = "tank_mRush",
                [238] = "rbooster_pre",
                [239] = "rbooster",
                [240] = "rbooster_after",
                [241] = "gatlingshot2",
                [242] = "doubleJump",
                [243] = "knockback",
                [244] = "swallow_pre",
                [245] = "swallow_loop",
                [246] = "swallow",
                [247] = "swallow_attack",
                [248] = "drillrush",
                [249] = "giant",
                [250] = "mbooster",
                [251] = "crossRoad",
                [252] = "nemesis",
                [253] = "wildbeast",
                [254] = "sonicBoom",
                [255] = "earthslug",
                [256] = "rpunch",
                [257] = "msummon",
                [258] = "msummon2",
                [259] = "flashRain",
                [260] = "clawCut",
                [261] = "mine",
                [262] = "ride",
                [263] = "getoff",
                [264] = "capture",
                [265] = "proneStab_jaguar",
                [266] = "herbalism_jaguar",
                [267] = "mining_jaguar",
                [268] = "braveslash1",
                [269] = "braveslash2",
                [270] = "braveslash3",
                [271] = "braveslash4",
                [272] = "chargeBlow",
                [273] = "ride2",
                [274] = "getoff2",
                [275] = "flamethrower_pre2",
                [276] = "flamethrower2",
                [277] = "flamethrower_after2",
                [278] = "flamethrower_pre",
                [279] = "flamethrower",
                [280] = "flamethrower_after",
                [281] = "gatlingshot",
                [282] = "battlecharge",
                [283] = "mRush",
                [284] = "herbalism_mechanic",
                [285] = "mining_mechanic",
                [286] = "gather0",
                [287] = "gather1",
                [288] = "create2",
                [289] = "create2_s",
                [290] = "create2_f",
                [291] = "create3",
                [292] = "create3_s",
                [293] = "create3_f",
                [294] = "create4",
                [295] = "create4_s",
                [296] = "create4_f",
                [297] = "create0",
                [298] = "create1",
                [299] = "darkTornado_pre",
                [300] = "darkTornado",
                [301] = "darkTornado_after",
                [302] = "pvpko",
                [303] = "alert8",
                [304] = "swingT2Giant",
                [305] = "iceAttack1",
                [306] = "iceAttack2",
                [307] = "iceSmash",
                [308] = "iceTempest",
                [309] = "iceChop",
                [310] = "icePanic",
                [311] = "iceDoubleJump",
                [312] = "darkImpale",
                [313] = "glacialChain",
                [314] = "mistEruption",
                [315] = "archerDoubleJump",
                [316] = "piercing",
                [317] = "swingC1",
                [318] = "swingC2",
                [319] = "shotC1",
                [320] = "flamesplash",
                [321] = "cannonSmash",
                [322] = "giganticBackstep",
                [323] = "piratebless",
                [324] = "swiftShot",
                [325] = "cannonJump",
                [326] = "rushBoom",
                [327] = "pirateSpirit",
                [328] = "counterCannon",
                [329] = "cannonSlam",
                [330] = "noiseWave_pre",
                [331] = "noiseWave_ing",
                [332] = "noiseWave",
                [333] = "monkeyBoomboom",
                [334] = "superCannon",
                [335] = "magneticCannon",
                [336] = "bombExplosion",
                [337] = "cannonSpike",
                [338] = "cannonBooster",
                [339] = "immolation",
                [340] = "swingDb1",
                [341] = "swingDb2",
                [342] = "shootDb1",
                [343] = "slayerDoubleJump",
                [344] = "jShot",
                [345] = "demonSlasher",
                [346] = "ride3",
                [347] = "getoff3",
                [348] = "spiritJump",
                [349] = "speedDualShot",
                [350] = "bluntSmash",
                [351] = "crossPiercing",
                [352] = "strikeDual",
                [353] = "fastest",
                [354] = "elfTornado",
                [355] = "deathDraw",
                [356] = "elfrush",
                [357] = "elfrush2",
                [358] = "elfrush_final",
                [359] = "elfrush_final2",
                [360] = "demolitionElf",
                [361] = "windEffect",
                [362] = "multiSniping",
                [363] = "healingAttack",
                [364] = "dealingRush",
                [365] = "devilCry",
                [366] = "powerEndure",
                [367] = "maxForce0",
                [368] = "maxForce1",
                [369] = "maxForce2",
                [370] = "maxForce3",
                [371] = "demonTrace",
                [372] = "dualVulcanPrep",
                [373] = "dualVulcanLoop",
                [374] = "dualVulcanEnd",
                [375] = "darkSpin",
                [376] = "blessOfGaia",
                [377] = "movebind",
                [378] = "demonGravity",
                [379] = "darkThrust",
            };

        private static readonly IReadOnlyDictionary<string, int> ClientActionStringCodeMap =
            ClientRawActionCodeMap.ToDictionary(
                static pair => pair.Value,
                static pair => pair.Key,
                StringComparer.OrdinalIgnoreCase);

        private static readonly IReadOnlyDictionary<string, string[]> ActionFallbackMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["stand1"] = new[] { "stand", "stand2" },
                ["stand2"] = new[] { "stand1", "stand" },
                ["stand"] = new[] { "stand1", "stand2" },
                ["walk1"] = new[] { "walk", "move", "walk2" },
                ["walk2"] = new[] { "walk1", "walk", "move" },
                ["walk"] = new[] { "walk1", "walk2", "move" },
                ["move"] = new[] { "walk1", "walk2", "walk" },
                ["rope"] = new[] { "ladder" },
                ["ladder"] = new[] { "rope" },
                ["rope2"] = new[] { "rope", "ladder2", "ladder" },
                ["ladder2"] = new[] { "ladder", "rope2", "rope" },
                ["fly2"] = new[] { "fly", "swim" },
                ["fly2Move"] = new[] { "fly2", "fly", "jump" },
                ["fly2Skill"] = new[] { "fly2", "fly", "jump" },
                ["hit"] = new[] { "stand", "stand1" },
                ["heal"] = new[] { "stand1" },
                ["alert"] = new[] { "stand1" },
                ["ghost"] = new[] { "dead", "stand1" },
                ["dead"] = new[] { "stand1" },
                ["stabO2"] = new[] { "stabO1" },
                ["stabOF"] = new[] { "stabO1" },
                ["stabT2"] = new[] { "stabT1" },
                ["stabTF"] = new[] { "stabT1" },
                ["swingO2"] = new[] { "swingO1" },
                ["swingO3"] = new[] { "swingO1" },
                ["swingOF"] = new[] { "swingO1" },
                ["swingT2"] = new[] { "swingT1" },
                ["swingT3"] = new[] { "swingT1" },
                ["swingTF"] = new[] { "swingT1" },
                ["swingP2"] = new[] { "swingP1" },
                ["swingPF"] = new[] { "swingP1" },
                ["shoot2"] = new[] { "shoot1" },
                ["shootF"] = new[] { "shoot1" },
                ["proneStab"] = new[] { "stabO1" }
            };

        public int ItemId { get; set; }
        public string Name { get; set; }
        public CharacterPartType Type { get; set; }
        public EquipSlot Slot { get; set; }

        // Animations indexed by action name
        public Dictionary<string, CharacterAnimation> Animations { get; set; } = new();
        public HashSet<string> AvailableAnimations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        internal Func<string, CharacterAnimation> AnimationResolver { get; set; }
        internal TamingMobActionFrameOwner TamingMobActionFrameOwner { get; set; }

        // For equipment with visible slots
        public string VSlot { get; set; }           // Visible slot conflicts
        public string ISlot { get; set; }           // Item slot priority
        public bool IsCash { get; set; }            // Cash shop item (overrides defaults)
        public string Description { get; set; }
        public string ItemCategory { get; set; }
        public DateTime? ExpirationDateUtc { get; set; }
        public int? Durability { get; set; }
        public int? MaxDurability { get; set; }
        public int SellPrice { get; set; }
        public bool IsEpic { get; set; }
        public int RequiredJobMask { get; set; }
        public int RequiredFame { get; set; }
        public int RequiredLevel { get; set; }
        public int RequiredSTR { get; set; }
        public int RequiredDEX { get; set; }
        public int RequiredINT { get; set; }
        public int RequiredLUK { get; set; }
        public int BonusSTR { get; set; }
        public int BonusDEX { get; set; }
        public int BonusINT { get; set; }
        public int BonusLUK { get; set; }
        public int BonusHP { get; set; }
        public int BonusMP { get; set; }
        public int BonusWeaponAttack { get; set; }
        public int BonusMagicAttack { get; set; }
        public int BonusWeaponDefense { get; set; }
        public int BonusMagicDefense { get; set; }
        public int BonusAccuracy { get; set; }
        public int BonusAvoidability { get; set; }
        public int BonusHands { get; set; }
        public int BonusSpeed { get; set; }
        public int BonusJump { get; set; }
        public int UpgradeSlots { get; set; }
        public int? TotalUpgradeSlotCount { get; set; }
        public int? RemainingUpgradeSlotCount { get; set; }
        public int EnhancementStarCount { get; set; }
        public bool IsSuperManMorph { get; set; }
        public int KnockbackRate { get; set; }
        public int TradeAvailable { get; set; }
        public bool IsTradeBlocked { get; set; }
        public bool IsEquipTradeBlocked { get; set; }
        public bool IsOneOfAKind { get; set; }
        public bool IsUniqueEquipItem { get; set; }
        public bool IsNotForSale { get; set; }
        public bool IsAccountSharable { get; set; }
        public bool HasAccountShareTag { get; set; }
        public bool IsNoMoveToLocker { get; set; }
        public int? OwnerAccountId { get; set; }
        public int? OwnerCharacterId { get; set; }
        public bool IsCashOwnershipLocked { get; set; }
        public bool IsTimeLimited { get; set; }
        public string PotentialTierText { get; set; }
        public List<string> PotentialLines { get; set; } = new();
        public List<int> ItemOptionIds { get; set; } = new();
        public bool HasGrowthInfo { get; set; }
        public int GrowthLevel { get; set; }
        public int GrowthMaxLevel { get; set; }
        public int GrowthExpPercent { get; set; }

        // Icon for UI
        public IDXObject Icon { get; set; }
        public IDXObject IconRaw { get; set; }

        public virtual CharacterPart Clone()
        {
            return new CharacterPart
            {
                ItemId = ItemId,
                Name = Name,
                Type = Type,
                Slot = Slot,
                Animations = new Dictionary<string, CharacterAnimation>(Animations),
                AvailableAnimations = new HashSet<string>(AvailableAnimations ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase),
                VSlot = VSlot,
                ISlot = ISlot,
                IsCash = IsCash,
                Description = Description,
                ItemCategory = ItemCategory,
                ExpirationDateUtc = ExpirationDateUtc,
                Durability = Durability,
                MaxDurability = MaxDurability,
                SellPrice = SellPrice,
                IsEpic = IsEpic,
                RequiredJobMask = RequiredJobMask,
                RequiredFame = RequiredFame,
                RequiredLevel = RequiredLevel,
                RequiredSTR = RequiredSTR,
                RequiredDEX = RequiredDEX,
                RequiredINT = RequiredINT,
                RequiredLUK = RequiredLUK,
                BonusSTR = BonusSTR,
                BonusDEX = BonusDEX,
                BonusINT = BonusINT,
                BonusLUK = BonusLUK,
                BonusHP = BonusHP,
                BonusMP = BonusMP,
                BonusWeaponAttack = BonusWeaponAttack,
                BonusMagicAttack = BonusMagicAttack,
                BonusWeaponDefense = BonusWeaponDefense,
                BonusMagicDefense = BonusMagicDefense,
                BonusAccuracy = BonusAccuracy,
                BonusAvoidability = BonusAvoidability,
                BonusHands = BonusHands,
                BonusSpeed = BonusSpeed,
                BonusJump = BonusJump,
                UpgradeSlots = UpgradeSlots,
                TotalUpgradeSlotCount = TotalUpgradeSlotCount,
                RemainingUpgradeSlotCount = RemainingUpgradeSlotCount,
                EnhancementStarCount = EnhancementStarCount,
                IsSuperManMorph = IsSuperManMorph,
                KnockbackRate = KnockbackRate,
                TradeAvailable = TradeAvailable,
                IsTradeBlocked = IsTradeBlocked,
                IsEquipTradeBlocked = IsEquipTradeBlocked,
                IsOneOfAKind = IsOneOfAKind,
                IsUniqueEquipItem = IsUniqueEquipItem,
                IsNotForSale = IsNotForSale,
                IsAccountSharable = IsAccountSharable,
                HasAccountShareTag = HasAccountShareTag,
                IsNoMoveToLocker = IsNoMoveToLocker,
                OwnerAccountId = OwnerAccountId,
                OwnerCharacterId = OwnerCharacterId,
                IsCashOwnershipLocked = IsCashOwnershipLocked,
                IsTimeLimited = IsTimeLimited,
                PotentialTierText = PotentialTierText,
                PotentialLines = PotentialLines != null ? new List<string>(PotentialLines) : new List<string>(),
                ItemOptionIds = ItemOptionIds != null ? new List<int>(ItemOptionIds) : new List<int>(),
                HasGrowthInfo = HasGrowthInfo,
                GrowthLevel = GrowthLevel,
                GrowthMaxLevel = GrowthMaxLevel,
                GrowthExpPercent = GrowthExpPercent,
                Icon = Icon,
                IconRaw = IconRaw,
                AnimationResolver = AnimationResolver,
                TamingMobActionFrameOwner = TamingMobActionFrameOwner
            };
        }

        /// <summary>
        /// Get animation for an action, with fallback
        /// </summary>
        public CharacterAnimation GetAnimation(CharacterAction action)
        {
            return GetAnimation(GetActionString(action));
        }

        public CharacterAnimation GetAnimation(string actionName)
        {
            foreach (string candidate in GetActionLookupStrings(actionName))
            {
                if (TryGetAnimation(candidate, out CharacterAnimation animation))
                {
                    return animation;
                }
            }

            if (TryGetAnimation("stand1", out CharacterAnimation standAnimation))
            {
                return standAnimation;
            }

            foreach (CharacterAnimation animation in Animations.Values)
            {
                if (animation?.Frames?.Count > 0)
                {
                    return animation;
                }
            }

            return null;
        }

        private bool TryGetAnimation(string actionName, out CharacterAnimation animation)
        {
            animation = null;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (Animations.TryGetValue(actionName, out animation))
            {
                return animation?.Frames?.Count > 0;
            }

            if (AnimationResolver == null)
            {
                return false;
            }

            if (AvailableAnimations != null
                && AvailableAnimations.Count > 0
                && !AvailableAnimations.Contains(actionName))
            {
                return false;
            }

            animation = AnimationResolver(actionName);
            if (animation?.Frames?.Count > 0)
            {
                Animations[actionName] = animation;
                return true;
            }

            return false;
        }

        public static CharacterAnimation FindAnimation(IReadOnlyDictionary<string, CharacterAnimation> animations, string actionName)
        {
            if (animations == null)
            {
                return null;
            }

            foreach (string candidate in GetActionLookupStrings(actionName))
            {
                if (animations.TryGetValue(candidate, out var anim))
                {
                    return anim;
                }
            }

            if (animations.TryGetValue("stand1", out var standAnimation))
            {
                return standAnimation;
            }

            foreach (var kv in animations)
            {
                return kv.Value;
            }

            return null;
        }

        public static IEnumerable<string> GetActionLookupStrings(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            yield return actionName;

            if (ActionFallbackMap.TryGetValue(actionName, out string[] familyFallbacks))
            {
                foreach (string fallbackAction in familyFallbacks)
                {
                    yield return fallbackAction;
                }
            }

            if (actionName.StartsWith("sit", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(actionName, "sit", StringComparison.OrdinalIgnoreCase))
            {
                yield return "sit";
            }

            if (string.Equals(actionName, "swim", StringComparison.OrdinalIgnoreCase))
            {
                yield return "fly";
            }
            else if (string.Equals(actionName, "fly", StringComparison.OrdinalIgnoreCase))
            {
                yield return "swim";
            }
        }

        public static bool TryGetActionStringFromCode(int actionCode, out string actionName)
        {
            if (ClientRawActionCodeMap.TryGetValue(actionCode, out actionName))
            {
                return true;
            }

            actionName = null;
            return !string.IsNullOrWhiteSpace(actionName);
        }

        public static bool TryGetClientRawActionCode(string actionName, out int actionCode)
        {
            if (ClientActionStringCodeMap.TryGetValue(actionName ?? string.Empty, out actionCode))
            {
                return true;
            }

            actionCode = default;
            return false;
        }

        public static string GetActionString(CharacterAction action)
        {
            return action switch
            {
                CharacterAction.Stand1 => "stand1",
                CharacterAction.Stand2 => "stand2",
                CharacterAction.Walk1 => "walk1",
                CharacterAction.Walk2 => "walk2",
                CharacterAction.Jump => "jump",
                CharacterAction.Sit => "sit",
                CharacterAction.Prone => "prone",
                CharacterAction.ProneStab => "proneStab",
                CharacterAction.Ladder => "ladder",
                CharacterAction.Rope => "rope",
                CharacterAction.Fly => "fly",
                CharacterAction.Swim => "swim",
                CharacterAction.StabO1 => "stabO1",
                CharacterAction.StabO2 => "stabO2",
                CharacterAction.StabOF => "stabOF",
                CharacterAction.StabT1 => "stabT1",
                CharacterAction.StabT2 => "stabT2",
                CharacterAction.StabTF => "stabTF",
                CharacterAction.SwingO1 => "swingO1",
                CharacterAction.SwingO2 => "swingO2",
                CharacterAction.SwingO3 => "swingO3",
                CharacterAction.SwingOF => "swingOF",
                CharacterAction.SwingT1 => "swingT1",
                CharacterAction.SwingT2 => "swingT2",
                CharacterAction.SwingT3 => "swingT3",
                CharacterAction.SwingTF => "swingTF",
                CharacterAction.SwingP1 => "swingP1",
                CharacterAction.SwingP2 => "swingP2",
                CharacterAction.SwingPF => "swingPF",
                CharacterAction.Shoot1 => "shoot1",
                CharacterAction.Shoot2 => "shoot2",
                CharacterAction.ShootF => "shootF",
                CharacterAction.Alert => "alert",
                CharacterAction.Heal => "heal",
                CharacterAction.Dead => "dead",
                CharacterAction.Ghost => "ghost",
                _ => "stand1"
            };
        }

        public static CharacterAction ParseActionString(string action)
        {
            return action?.ToLowerInvariant() switch
            {
                "stand1" => CharacterAction.Stand1,
                "stand2" => CharacterAction.Stand2,
                "walk1" => CharacterAction.Walk1,
                "walk2" => CharacterAction.Walk2,
                "jump" => CharacterAction.Jump,
                "sit" => CharacterAction.Sit,
                "prone" => CharacterAction.Prone,
                "pronestab" => CharacterAction.ProneStab,
                "ladder" => CharacterAction.Ladder,
                "rope" => CharacterAction.Rope,
                "fly" => CharacterAction.Fly,
                "swim" => CharacterAction.Swim,
                "stabo1" => CharacterAction.StabO1,
                "stabo2" => CharacterAction.StabO2,
                "stabof" => CharacterAction.StabOF,
                "stabt1" => CharacterAction.StabT1,
                "stabt2" => CharacterAction.StabT2,
                "stabtf" => CharacterAction.StabTF,
                "swingo1" => CharacterAction.SwingO1,
                "swingo2" => CharacterAction.SwingO2,
                "swingo3" => CharacterAction.SwingO3,
                "swingof" => CharacterAction.SwingOF,
                "swingt1" => CharacterAction.SwingT1,
                "swingt2" => CharacterAction.SwingT2,
                "swingt3" => CharacterAction.SwingT3,
                "swingtf" => CharacterAction.SwingTF,
                "swingp1" => CharacterAction.SwingP1,
                "swingp2" => CharacterAction.SwingP2,
                "swingpf" => CharacterAction.SwingPF,
                "shoot1" => CharacterAction.Shoot1,
                "shoot2" => CharacterAction.Shoot2,
                "shootf" => CharacterAction.ShootF,
                "alert" => CharacterAction.Alert,
                "heal" => CharacterAction.Heal,
                "dead" => CharacterAction.Dead,
                "ghost" => CharacterAction.Ghost,
                _ => CharacterAction.Stand1
            };
        }

        public static IReadOnlyList<string> ParseSlotTokens(string slotValue)
        {
            if (string.IsNullOrWhiteSpace(slotValue))
            {
                return Array.Empty<string>();
            }

            var tokens = new List<string>(slotValue.Length / 2);
            for (int i = 0; i + 1 < slotValue.Length; i += 2)
            {
                string token = slotValue.Substring(i, 2);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    tokens.Add(token);
                }
            }

            return tokens;
        }
    }

    /// <summary>
    /// Body part with skin color
    /// </summary>
    public class BodyPart : CharacterPart
    {
        public SkinColor SkinColor { get; set; }
        public bool IsHead { get; set; }
    }

    /// <summary>
    /// Face part with expressions
    /// </summary>
    public sealed class FaceLookFrame
    {
        public CharacterFrame FaceFrame { get; init; }
        public CharacterFrame AccessoryFrame { get; init; }
        public int Delay { get; init; }
    }

    public sealed class FaceLookEntry
    {
        public string ExpressionName { get; init; }
        public SkinColor SkinColor { get; init; }
        public int FaceItemId { get; init; }
        public int FaceAccessoryItemId { get; init; }
        public List<FaceLookFrame> Frames { get; } = new();
        public int TotalDuration { get; set; }
        public bool HasAccessory => FaceAccessoryItemId > 0;
    }

    public class FacePart : CharacterPart
    {
        private readonly record struct FaceLookCacheKey(
            SkinColor SkinColor,
            string ExpressionName,
            int FaceAccessoryItemId);

        private readonly Dictionary<FaceLookCacheKey, FaceLookEntry> _lookCache = new();

        public Dictionary<string, CharacterAnimation> Expressions { get; set; } = new();

        public CharacterAnimation GetExpression(string expression)
        {
            if (Expressions.TryGetValue(expression, out var anim))
                return anim;
            if (Expressions.TryGetValue("default", out anim))
                return anim;
            if (Expressions.TryGetValue("blink", out anim))
                return anim;
            foreach (var kv in Expressions)
                return kv.Value;
            return null;
        }

        public FaceLookEntry GetLook(string expression, SkinColor skinColor, CharacterPart faceAccessoryPart)
        {
            string normalizedExpression = string.IsNullOrWhiteSpace(expression) ? "default" : expression.Trim();
            int faceAccessoryItemId = faceAccessoryPart?.ItemId ?? 0;
            FaceLookCacheKey cacheKey = new(skinColor, normalizedExpression, faceAccessoryItemId);
            if (_lookCache.TryGetValue(cacheKey, out FaceLookEntry cachedLook))
            {
                return cachedLook;
            }

            CharacterAnimation faceAnimation = GetExpression(normalizedExpression);
            CharacterAnimation accessoryAnimation = ResolveAccessoryExpression(faceAccessoryPart, normalizedExpression);
            int faceFrameCount = faceAnimation?.Frames?.Count ?? 0;
            int accessoryFrameCount = accessoryAnimation?.Frames?.Count ?? 0;
            int frameCount = Math.Max(faceFrameCount, accessoryFrameCount);
            if (frameCount == 0)
            {
                return null;
            }

            FaceLookEntry faceLook = new()
            {
                ExpressionName = normalizedExpression,
                SkinColor = skinColor,
                FaceItemId = ItemId,
                FaceAccessoryItemId = faceAccessoryItemId
            };

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                CharacterFrame faceFrame = GetAnimationFrame(faceAnimation, frameIndex);
                CharacterFrame accessoryFrame = GetAnimationFrame(accessoryAnimation, frameIndex);
                int delay = ResolveFaceLookDelay(faceFrame, accessoryFrame);

                faceLook.Frames.Add(new FaceLookFrame
                {
                    FaceFrame = faceFrame,
                    AccessoryFrame = accessoryFrame,
                    Delay = delay
                });

                faceLook.TotalDuration += delay;
            }

            _lookCache[cacheKey] = faceLook;
            return faceLook;
        }

        public bool TryGetLookDuration(string expression, SkinColor skinColor, CharacterPart faceAccessoryPart, out int durationMs)
        {
            durationMs = 0;
            FaceLookEntry faceLook = GetLook(expression, skinColor, faceAccessoryPart);
            if (faceLook == null || faceLook.TotalDuration <= 0)
            {
                return false;
            }

            durationMs = faceLook.TotalDuration;
            return true;
        }

        private static CharacterAnimation ResolveAccessoryExpression(CharacterPart faceAccessoryPart, string expression)
        {
            if (faceAccessoryPart?.Animations == null || faceAccessoryPart.Animations.Count == 0)
            {
                return null;
            }

            if (faceAccessoryPart.Animations.TryGetValue(expression, out CharacterAnimation animation))
            {
                return animation;
            }

            if (faceAccessoryPart.Animations.TryGetValue("default", out animation))
            {
                return animation;
            }

            if (faceAccessoryPart.Animations.TryGetValue("blink", out animation))
            {
                return animation;
            }

            foreach ((_, CharacterAnimation fallbackAnimation) in faceAccessoryPart.Animations)
            {
                return fallbackAnimation;
            }

            return null;
        }

        private static CharacterFrame GetAnimationFrame(CharacterAnimation animation, int frameIndex)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return null;
            }

            return animation.Frames[frameIndex % animation.Frames.Count];
        }

        private static int ResolveFaceLookDelay(CharacterFrame faceFrame, CharacterFrame accessoryFrame)
        {
            if (faceFrame?.Delay > 0)
            {
                return faceFrame.Delay;
            }

            if (accessoryFrame?.Delay > 0)
            {
                return accessoryFrame.Delay;
            }

            return 100;
        }
    }

    /// <summary>
    /// Hair part with front/back layers
    /// </summary>
    public class HairPart : CharacterPart
    {
        public Color HairColor { get; set; } = Color.White;
        public bool HasBackHair { get; set; }
        public Dictionary<string, CharacterAnimation> BackHairAnimations { get; set; } = new();
    }

    /// <summary>
    /// Weapon part with attack info
    /// </summary>
    public class WeaponPart : CharacterPart
    {
        public int AttackSpeed { get; set; } = 6;       // Attack speed modifier
        public int Attack { get; set; }                  // Weapon attack
        public string WeaponType { get; set; }           // "1h sword", "2h sword", "bow", etc.
        public string AfterImageType { get; set; }       // WZ info/afterImage family (e.g. swordOL, swordOS)
        public int Range { get; set; } = 100;            // Attack range in pixels
        public bool IsTwoHanded { get; set; }

        public override CharacterPart Clone()
        {
            return new WeaponPart
            {
                ItemId = ItemId,
                Name = Name,
                Type = Type,
                Slot = Slot,
                Animations = new Dictionary<string, CharacterAnimation>(Animations),
                VSlot = VSlot,
                ISlot = ISlot,
                IsCash = IsCash,
                Description = Description,
                ItemCategory = ItemCategory,
                ExpirationDateUtc = ExpirationDateUtc,
                Durability = Durability,
                MaxDurability = MaxDurability,
                SellPrice = SellPrice,
                IsEpic = IsEpic,
                RequiredJobMask = RequiredJobMask,
                RequiredFame = RequiredFame,
                RequiredLevel = RequiredLevel,
                RequiredSTR = RequiredSTR,
                RequiredDEX = RequiredDEX,
                RequiredINT = RequiredINT,
                RequiredLUK = RequiredLUK,
                BonusSTR = BonusSTR,
                BonusDEX = BonusDEX,
                BonusINT = BonusINT,
                BonusLUK = BonusLUK,
                BonusHP = BonusHP,
                BonusMP = BonusMP,
                BonusWeaponAttack = BonusWeaponAttack,
                BonusMagicAttack = BonusMagicAttack,
                BonusWeaponDefense = BonusWeaponDefense,
                BonusMagicDefense = BonusMagicDefense,
                BonusAccuracy = BonusAccuracy,
                BonusAvoidability = BonusAvoidability,
                BonusHands = BonusHands,
                BonusSpeed = BonusSpeed,
                BonusJump = BonusJump,
                UpgradeSlots = UpgradeSlots,
                TotalUpgradeSlotCount = TotalUpgradeSlotCount,
                RemainingUpgradeSlotCount = RemainingUpgradeSlotCount,
                EnhancementStarCount = EnhancementStarCount,
                KnockbackRate = KnockbackRate,
                TradeAvailable = TradeAvailable,
                IsTradeBlocked = IsTradeBlocked,
                IsEquipTradeBlocked = IsEquipTradeBlocked,
                IsOneOfAKind = IsOneOfAKind,
                IsNotForSale = IsNotForSale,
                IsAccountSharable = IsAccountSharable,
                HasAccountShareTag = HasAccountShareTag,
                IsNoMoveToLocker = IsNoMoveToLocker,
                OwnerAccountId = OwnerAccountId,
                OwnerCharacterId = OwnerCharacterId,
                IsCashOwnershipLocked = IsCashOwnershipLocked,
                IsTimeLimited = IsTimeLimited,
                PotentialTierText = PotentialTierText,
                PotentialLines = PotentialLines != null ? new List<string>(PotentialLines) : new List<string>(),
                ItemOptionIds = ItemOptionIds != null ? new List<int>(ItemOptionIds) : new List<int>(),
                HasGrowthInfo = HasGrowthInfo,
                GrowthLevel = GrowthLevel,
                GrowthMaxLevel = GrowthMaxLevel,
                GrowthExpPercent = GrowthExpPercent,
                Icon = Icon,
                IconRaw = IconRaw,
                AttackSpeed = AttackSpeed,
                Attack = Attack,
                WeaponType = WeaponType,
                AfterImageType = AfterImageType,
                Range = Range,
                IsTwoHanded = IsTwoHanded
            };
        }
    }

    public sealed class PortableChairLayer
    {
        public string Name { get; set; }
        public CharacterAnimation Animation { get; set; }
        public int RelativeZ { get; set; }
        public int PositionHint { get; set; }
    }

    public sealed class PortableChair
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int RecoveryHp { get; set; }
        public int RecoveryMp { get; set; }
        public int? SitActionId { get; set; }
        public int? TamingMobItemId { get; set; }
        public bool IsCoupleChair { get; set; }
        public int? CoupleDistanceX { get; set; }
        public int? CoupleDistanceY { get; set; }
        public int? CoupleMaxDiff { get; set; }
        public int? CoupleDirection { get; set; }
        public List<PortableChairLayer> Layers { get; set; } = new();
        public Dictionary<string, List<PortableChairLayer>> ExpressionLayers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<PortableChairLayer> CoupleSharedLayers { get; set; } = new();
        public List<PortableChairLayer> CoupleMidpointLayers { get; set; } = new();
    }

    public sealed class ItemEffectAnimationSet
    {
        public int ItemId { get; set; }
        public List<PortableChairLayer> OwnerLayers { get; set; } = new();
        public List<PortableChairLayer> SharedLayers { get; set; } = new();

        public int TotalDurationMs =>
            Math.Max(GetMaximumDuration(OwnerLayers), GetMaximumDuration(SharedLayers));

        private static int GetMaximumDuration(IEnumerable<PortableChairLayer> layers)
        {
            if (layers == null)
            {
                return 0;
            }

            int duration = 0;
            foreach (PortableChairLayer layer in layers)
            {
                duration = Math.Max(duration, layer?.Animation?.TotalDuration ?? 0);
            }

            return duration;
        }
    }

    public sealed class CarryItemEffectDefinition
    {
        public PortableChairLayer BundleLayer { get; set; }
        public PortableChairLayer SingleLayerA { get; set; }
        public PortableChairLayer SingleLayerB { get; set; }
        public bool IsReady => BundleLayer != null || SingleLayerA != null || SingleLayerB != null;
    }

    public sealed class RelationshipTextTagStyle
    {
        public Texture2D Left { get; set; }
        public Texture2D Middle { get; set; }
        public Texture2D Right { get; set; }
        public Color TextColor { get; set; } = Color.White;
        public bool IsReady => Left != null && Middle != null && Right != null;
        public int Height => Math.Max(Math.Max(Left?.Height ?? 0, Middle?.Height ?? 0), Right?.Height ?? 0);
    }

    #endregion

    #region Character Build

    /// <summary>
    /// Complete character build with all parts
    /// </summary>
    public class CharacterBuild
    {
        private readonly record struct AttackFormulaProfile(
            bool UsesMagicFormula,
            float WeaponMultiplier,
            int PrimaryStat,
            int SecondaryStat,
            float MasteryPrimaryScale);

        private enum JobArchetype
        {
            Beginner,
            Warrior,
            Magician,
            Bowman,
            Thief,
            Pirate
        }

        private enum AutoAssignStrategy
        {
            GenericBeginner,
            BeginnerWarriorLike,
            Warrior,
            Magician,
            BowmanBow,
            BowmanCrossbowLike,
            Thief,
            PirateBrawlerLike,
            PirateGunslingerLike
        }

        public const int AutoAssignClassBeginner = 0;
        public const int AutoAssignClassWarrior = 1;
        public const int AutoAssignClassMagician = 2;
        public const int AutoAssignClassBowman = 3;
        public const int AutoAssignClassThief = 4;
        public const int AutoAssignClassPirate = 5;

        public const int MaxPrimaryStat = 999;
        public const int MaxHpMpStat = 30000;
        private const int MinimumMasteryPercent = 10;
        private const int DefaultAttackValue = 10;
        private const int DefaultDefenseValue = 5;
        private const int DefaultMagicAttackValue = 5;
        private const int DefaultMagicDefenseValue = 5;
        private const float DefaultSpeedValue = 100f;
        private const float DefaultJumpValue = 100f;

        public int Id { get; set; }
        public string Name { get; set; }
        public CharacterGender Gender { get; set; }
        public SkinColor Skin { get; set; }

        // Core parts
        public BodyPart Body { get; set; }
        public BodyPart Head { get; set; }
        public FacePart Face { get; set; }
        public HairPart Hair { get; set; }
        public CharacterPart WeaponSticker { get; set; }
        public PortableChair ActivePortableChair { get; set; }
        public IReadOnlyList<int> RemotePetItemIds { get; set; } = Array.Empty<int>();
        public Func<int, CharacterPart> EquipmentPartLoader { get; set; }

        // Equipment slots
        public Dictionary<EquipSlot, CharacterPart> Equipment { get; set; } = new();
        public Dictionary<EquipSlot, CharacterPart> HiddenEquipment { get; set; } = new();

        // Stats
        public int Level { get; set; } = 1;
        public int MaxHP { get; set; } = 10000;
        public int MaxMP { get; set; } = 10000;
        public int HP { get; set; } = 10000;
        public int MP { get; set; } = 10000;

        // Primary Stats (Ability Points)
        public int STR { get; set; } = 4;
        public int DEX { get; set; } = 4;
        public int INT { get; set; } = 4;
        public int LUK { get; set; } = 4;
        public int AP { get; set; } = 0;    // Available ability points to distribute

        // Job info
        public int Job { get; set; } = 0;   // Job ID (0 = Beginner)
        public int SubJob { get; set; } = 0;
        public string JobName { get; set; } = "Beginner";
        public string GuildName { get; set; } = string.Empty;
        public string AllianceName { get; set; } = string.Empty;
        public int Fame { get; set; } = 0;
        public int CookieHousePoint { get; set; } = 0;
        public int WorldRank { get; set; }
        public int JobRank { get; set; }
        public bool HasMonsterRiding { get; set; }
        public int TraitCharisma { get; set; }
        public int TraitInsight { get; set; }
        public int TraitWill { get; set; }
        public int TraitCraft { get; set; }
        public int TraitSense { get; set; }
        public int TraitCharm { get; set; }
        public bool HasPendantSlotExtension { get; set; }
        public bool HasPocketSlot { get; set; }

        // Experience
        public long Exp { get; set; } = 0;
        public long ExpToNextLevel { get; set; } = 15;

        // Combat stats
        public int Attack { get; set; } = 10;
        public int Defense { get; set; } = 5;
        public int MagicAttack { get; set; } = 5;
        public int MagicDefense { get; set; } = 5;
        public int Accuracy { get; set; } = 0;
        public int Avoidability { get; set; } = 0;
        public int Hands { get; set; } = 0;
        public int CriticalRate { get; set; } = 0;
        public float Speed { get; set; } = 100;         // Movement speed %
        public float JumpPower { get; set; } = 100;     // Jump height %
        public Func<BuffStatType, int> SkillStatBonusProvider { get; set; }
        public Func<int> SkillMasteryProvider { get; set; }

        public string GuildDisplayText => string.IsNullOrWhiteSpace(GuildName) ? "-" : GuildName;
        public string AllianceDisplayText => string.IsNullOrWhiteSpace(AllianceName) ? "-" : AllianceName;
        public bool IsPendantSlotExtensionActive =>
            HasPendantSlotExtension || Equipment.ContainsKey(EquipSlot.Pendant2) || HiddenEquipment.ContainsKey(EquipSlot.Pendant2);
        public bool IsPocketSlotAvailable =>
            HasPocketSlot || TraitCharm >= 30 || Equipment.ContainsKey(EquipSlot.Pocket) || HiddenEquipment.ContainsKey(EquipSlot.Pocket);
        public int AutoAssignClass => ResolveAutoAssignClass(Job);

        public int ComputeEquipmentStateToken()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Id;
                hash = (hash * 31) + Job;
                hash = (hash * 31) + SubJob;
                hash = (hash * 31) + Level;
                hash = (hash * 31) + Fame;
                hash = (hash * 31) + STR;
                hash = (hash * 31) + DEX;
                hash = (hash * 31) + INT;
                hash = (hash * 31) + LUK;
                hash = (hash * 31) + TraitCharm;
                hash = (hash * 31) + (HasMonsterRiding ? 1 : 0);
                hash = (hash * 31) + (HasPendantSlotExtension ? 1 : 0);
                hash = (hash * 31) + (HasPocketSlot ? 1 : 0);
                hash = AppendEquipmentLayerToken(hash, Equipment, hiddenLayer: false);
                hash = AppendEquipmentLayerToken(hash, HiddenEquipment, hiddenLayer: true);
                return hash;
            }
        }

        public int ExpPercent
        {
            get
            {
                if (ExpToNextLevel <= 0)
                {
                    return 0;
                }

                long percent = (Exp * 100L) / ExpToNextLevel;
                return (int)Math.Clamp(percent, 0L, 100L);
            }
        }

        public string ExpDisplayText => $"{ExpPercent}%";

        public bool CanIncreasePrimaryStat(int currentValue)
        {
            return AP > 0 && currentValue < MaxPrimaryStat;
        }

        public bool IncreasePrimaryStat(BuffStatType stat)
        {
            if (AP <= 0)
            {
                return false;
            }

            switch (stat)
            {
                case BuffStatType.Strength when CanIncreasePrimaryStat(STR):
                    STR++;
                    break;
                case BuffStatType.Dexterity when CanIncreasePrimaryStat(DEX):
                    DEX++;
                    break;
                case BuffStatType.Intelligence when CanIncreasePrimaryStat(INT):
                    INT++;
                    break;
                case BuffStatType.Luck when CanIncreasePrimaryStat(LUK):
                    LUK++;
                    break;
                default:
                    return false;
            }

            AP--;
            return true;
        }

        public void AutoAssignAbilityPoints()
        {
            while (AP > 0)
            {
                bool assigned = ResolveAutoAssignStrategy() switch
                {
                    AutoAssignStrategy.BeginnerWarriorLike => TryAutoAssignTowardsTarget(
                        BuffStatType.Dexterity,
                        Math.Max(1, Level),
                        BuffStatType.Strength),
                    AutoAssignStrategy.Warrior => TryAutoAssignTowardsTarget(
                        BuffStatType.Dexterity,
                        GetWarriorStyleDexTarget(),
                        BuffStatType.Strength),
                    AutoAssignStrategy.Magician => TryAutoAssignTowardsTarget(
                        BuffStatType.Luck,
                        GetMagicianLukTarget(),
                        BuffStatType.Intelligence),
                    AutoAssignStrategy.BowmanBow => TryAutoAssignTowardsTarget(
                        BuffStatType.Strength,
                        GetBowmanStrengthTarget(additionalStrength: 5),
                        BuffStatType.Dexterity),
                    AutoAssignStrategy.BowmanCrossbowLike => TryAutoAssignTowardsTarget(
                        BuffStatType.Strength,
                        GetBowmanStrengthTarget(additionalStrength: 0),
                        BuffStatType.Dexterity),
                    AutoAssignStrategy.Thief => TryAutoAssignTowardsTarget(
                        BuffStatType.Dexterity,
                        GetThiefDexTarget(),
                        BuffStatType.Luck),
                    AutoAssignStrategy.PirateBrawlerLike => TryAutoAssignTowardsTarget(
                        BuffStatType.Dexterity,
                        GetBrawlerStyleDexTarget(),
                        BuffStatType.Strength),
                    AutoAssignStrategy.PirateGunslingerLike => TryAutoAssignTowardsTarget(
                        BuffStatType.Strength,
                        Math.Max(1, Level),
                        BuffStatType.Dexterity),
                    _ => TryAutoAssignBeginnerPoint()
                };

                if (!assigned)
                {
                    break;
                }
            }
        }

        public int TotalSTR => STR + SumEquipmentBonus(part => part.BonusSTR) + GetSkillStatBonus(BuffStatType.Strength);
        public int TotalDEX => DEX + SumEquipmentBonus(part => part.BonusDEX) + GetSkillStatBonus(BuffStatType.Dexterity);
        public int TotalINT => INT + SumEquipmentBonus(part => part.BonusINT) + GetSkillStatBonus(BuffStatType.Intelligence);
        public int TotalLUK => LUK + SumEquipmentBonus(part => part.BonusLUK) + GetSkillStatBonus(BuffStatType.Luck);
        public int TotalMaxHP => Math.Clamp(ApplyRateBonus(MaxHP + SumEquipmentBonus(part => part.BonusHP) + GetSkillStatBonus(BuffStatType.MaxHP), GetSkillStatBonus(BuffStatType.MaxHPPercent)), 1, MaxHpMpStat);
        public int TotalMaxMP => Math.Clamp(ApplyRateBonus(MaxMP + SumEquipmentBonus(part => part.BonusMP) + GetSkillStatBonus(BuffStatType.MaxMP), GetSkillStatBonus(BuffStatType.MaxMPPercent)), 0, MaxHpMpStat);
        public int TotalHP => Math.Clamp(HP + SumEquipmentBonus(part => part.BonusHP), 0, TotalMaxHP);
        public int TotalMP => Math.Clamp(MP + SumEquipmentBonus(part => part.BonusMP), 0, TotalMaxMP);
        public int TotalMastery => Math.Clamp(SkillMasteryProvider?.Invoke() ?? MinimumMasteryPercent, MinimumMasteryPercent, 100);
        public int TotalWeaponAttackStat => Math.Max(0, ApplyRateBonus(Math.Max(0, Attack - DefaultAttackValue) + SumEquipmentBonus(part => part.BonusWeaponAttack) + GetSkillStatBonus(BuffStatType.Attack), GetSkillStatBonus(BuffStatType.AttackPercent)));
        public int TotalWeaponDefenseStat => Math.Max(0, ApplyRateBonus(Math.Max(0, Defense - DefaultDefenseValue) + SumEquipmentBonus(part => part.BonusWeaponDefense) + GetSkillStatBonus(BuffStatType.Defense), GetSkillStatBonus(BuffStatType.DefensePercent)));
        public int TotalMagicAttackStat => Math.Max(0, ApplyRateBonus(Math.Max(0, MagicAttack - DefaultMagicAttackValue) + SumEquipmentBonus(part => part.BonusMagicAttack) + GetSkillStatBonus(BuffStatType.MagicAttack), GetSkillStatBonus(BuffStatType.MagicAttackPercent)));
        public int TotalMagicDefenseStat => Math.Max(0, ApplyRateBonus(Math.Max(0, MagicDefense - DefaultMagicDefenseValue) + SumEquipmentBonus(part => part.BonusMagicDefense) + GetSkillStatBonus(BuffStatType.MagicDefense), GetSkillStatBonus(BuffStatType.MagicDefensePercent)));
        public int TotalAttack => ComputeDisplayedPhysicalAttack();
        public int TotalDefense => ComputeDisplayedPhysicalDefense();
        public int TotalMagicAttack => ComputeDisplayedMagicAttack();
        public int TotalMagicDefense => ComputeDisplayedMagicDefense();

        public int TotalAccuracy => Math.Max(0, ApplyRateBonus(GetBaseAccuracy() + Accuracy + SumEquipmentBonus(part => part.BonusAccuracy) + GetSkillStatBonus(BuffStatType.Accuracy), GetSkillStatBonus(BuffStatType.AccuracyPercent)));
        public int TotalAvoidability => Math.Max(0, ApplyRateBonus(GetBaseAvoidability() + Avoidability + SumEquipmentBonus(part => part.BonusAvoidability) + GetSkillStatBonus(BuffStatType.Avoidability), GetSkillStatBonus(BuffStatType.AvoidabilityPercent)));
        public int TotalHands => Math.Max(0, Hands + TotalDEX + TotalINT + TotalLUK + SumEquipmentBonus(part => part.BonusHands));
        public int TotalCriticalRate => Math.Max(0, CriticalRate + GetSkillStatBonus(BuffStatType.CriticalRate));
        public float TotalSpeed => Math.Max(0f, ApplyRateBonus(Speed + SumEquipmentBonus(part => part.BonusSpeed) + GetSkillStatBonus(BuffStatType.Speed), GetSkillStatBonus(BuffStatType.SpeedPercent)));
        public float TotalJumpPower => Math.Max(0f, JumpPower + SumEquipmentBonus(part => part.BonusJump) + GetSkillStatBonus(BuffStatType.Jump));

        public bool CanIncreaseMaxHp()
        {
            return AP > 0 && MaxHP < MaxHpMpStat;
        }

        public bool CanIncreaseMaxMp()
        {
            return AP > 0 && MaxMP < MaxHpMpStat;
        }

        public bool IncreaseMaxHp(Func<int, int, int> rollInclusive = null)
        {
            if (!CanIncreaseMaxHp())
            {
                return false;
            }

            int amount = GetHpIncreaseAmount(rollInclusive ?? DefaultRollInclusive);
            MaxHP = Math.Clamp(MaxHP + amount, 1, MaxHpMpStat);
            HP = Math.Clamp(HP + amount, 0, MaxHP);
            AP--;
            return true;
        }

        public bool IncreaseMaxMp(Func<int, int, int> rollInclusive = null)
        {
            if (!CanIncreaseMaxMp())
            {
                return false;
            }

            int amount = GetMpIncreaseAmount(rollInclusive ?? DefaultRollInclusive);
            MaxMP = Math.Clamp(MaxMP + amount, 0, MaxHpMpStat);
            MP = Math.Clamp(MP + amount, 0, MaxMP);
            AP--;
            return true;
        }

        /// <summary>
        /// Get all parts in z-order for rendering
        /// </summary>
        public IEnumerable<CharacterPart> GetPartsInZOrder()
        {
            var parts = new List<(CharacterPart part, int zIndex)>();

            // Add body parts first
            if (Body != null) parts.Add((Body, GetZIndex(CharacterPartType.Body)));
            if (Head != null) parts.Add((Head, GetZIndex(CharacterPartType.Head)));
            if (Face != null) parts.Add((Face, GetZIndex(CharacterPartType.Face)));
            if (Hair != null) parts.Add((Hair, GetZIndex(CharacterPartType.Hair)));

            // Add equipment
            foreach (var kv in Equipment)
            {
                if (kv.Value != null)
                {
                    parts.Add((kv.Value, GetZIndex(kv.Value.Type)));
                }
            }

            // Sort by z-index
            parts.Sort((a, b) => a.zIndex.CompareTo(b.zIndex));

            foreach (var (part, _) in parts)
            {
                yield return part;
            }
        }

        private static int GetZIndex(CharacterPartType type)
        {
            // Z-order from back to front (higher = in front)
            return type switch
            {
                CharacterPartType.Cape => 0,
                CharacterPartType.HairBelowBody => 5,
                CharacterPartType.Shield => 10,
                CharacterPartType.Body => 20,
                CharacterPartType.Pants => 25,
                CharacterPartType.Shoes => 27,
                CharacterPartType.Coat => 30,
                CharacterPartType.Longcoat => 31,
                CharacterPartType.Arm => 35,
                CharacterPartType.HandBelowWeapon => 36,
                CharacterPartType.WeaponBelowArm => 37,
                CharacterPartType.Glove => 40,
                CharacterPartType.WeaponOverGlove => 42,
                CharacterPartType.Hand => 45,
                CharacterPartType.Weapon => 50,
                CharacterPartType.WeaponOverHand => 52,
                CharacterPartType.WeaponOverBody => 55,
                CharacterPartType.Head => 60,
                CharacterPartType.Ear => 62,
                CharacterPartType.Earrings => 63,
                CharacterPartType.Face => 65,
                CharacterPartType.Face_Accessory => 67,
                CharacterPartType.Eye_Accessory => 68,
                CharacterPartType.Hair => 70,
                CharacterPartType.HairOverHead => 72,
                CharacterPartType.ArmOverHair => 75,
                CharacterPartType.ArmOverHairBelowWeapon => 76,
                CharacterPartType.HandOverHair => 78,
                CharacterPartType.CapBelowAccessory => 80,
                CharacterPartType.Cap => 82,
                CharacterPartType.CapOverHair => 85,
                CharacterPartType.Accessory => 90,
                CharacterPartType.AccessoryOverHair => 92,
                CharacterPartType.TamingMob => 100,
                _ => 50
            };
        }

        /// <summary>
        /// Equip an item
        /// </summary>
        public void Equip(CharacterPart part)
        {
            if (part == null) return;
            Equipment[part.Slot] = part;
        }

        public void EquipHidden(CharacterPart part)
        {
            if (part == null) return;
            HiddenEquipment[part.Slot] = part;
        }

        public IReadOnlyList<CharacterPart> PlaceEquipment(CharacterPart part, EquipSlot targetSlot)
        {
            if (part == null)
            {
                return Array.Empty<CharacterPart>();
            }

            List<CharacterPart> displacedParts = new();
            part.Slot = targetSlot;
            Equipment.TryGetValue(targetSlot, out CharacterPart visiblePart);
            HiddenEquipment.TryGetValue(targetSlot, out CharacterPart hiddenPart);

            if (part.IsCash)
            {
                if (visiblePart?.IsCash == true)
                {
                    displacedParts.Add(visiblePart);
                }
                else if (visiblePart != null)
                {
                    HiddenEquipment[targetSlot] = visiblePart;
                }

                Equipment[targetSlot] = part;
                return displacedParts.Count == 0
                    ? Array.Empty<CharacterPart>()
                    : displacedParts.AsReadOnly();
            }

            if (visiblePart?.IsCash == true)
            {
                if (hiddenPart != null)
                {
                    displacedParts.Add(hiddenPart);
                }

                HiddenEquipment[targetSlot] = part;
                return displacedParts.Count == 0
                    ? Array.Empty<CharacterPart>()
                    : displacedParts.AsReadOnly();
            }

            if (visiblePart != null)
            {
                displacedParts.Add(visiblePart);
            }

            if (hiddenPart != null)
            {
                displacedParts.Add(hiddenPart);
                HiddenEquipment.Remove(targetSlot);
            }

            Equipment[targetSlot] = part;
            return displacedParts.Count == 0
                ? Array.Empty<CharacterPart>()
                : displacedParts.AsReadOnly();
        }

        /// <summary>
        /// Unequip an item
        /// </summary>
        public CharacterPart Unequip(EquipSlot slot)
        {
            if (Equipment.TryGetValue(slot, out var part))
            {
                Equipment.Remove(slot);
                if (part.IsCash && HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart))
                {
                    HiddenEquipment.Remove(slot);
                    Equipment[slot] = hiddenPart;
                }
                return part;
            }

            if (HiddenEquipment.TryGetValue(slot, out CharacterPart concealedPart))
            {
                HiddenEquipment.Remove(slot);
                return concealedPart;
            }

            return null;
        }

        public CharacterPart UnequipHidden(EquipSlot slot)
        {
            if (HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart))
            {
                HiddenEquipment.Remove(slot);
                return hiddenPart;
            }

            return null;
        }

        private static int AppendEquipmentLayerToken(int hash, Dictionary<EquipSlot, CharacterPart> equipment, bool hiddenLayer)
        {
            unchecked
            {
                hash = (hash * 31) + (hiddenLayer ? 1 : 0);
                if (equipment == null || equipment.Count == 0)
                {
                    return hash;
                }

                foreach (KeyValuePair<EquipSlot, CharacterPart> entry in equipment.OrderBy(entry => entry.Key))
                {
                    hash = (hash * 31) + (int)entry.Key;
                    hash = AppendPartToken(hash, entry.Value);
                }

                return hash;
            }
        }

        private static int AppendPartToken(int hash, CharacterPart part)
        {
            unchecked
            {
                if (part == null)
                {
                    return (hash * 31) - 1;
                }

                hash = (hash * 31) + part.ItemId;
                hash = (hash * 31) + (int)part.Slot;
                hash = (hash * 31) + (int)part.Type;
                hash = (hash * 31) + (part.IsCash ? 1 : 0);
                hash = (hash * 31) + part.RequiredLevel;
                hash = (hash * 31) + part.RequiredSTR;
                hash = (hash * 31) + part.RequiredDEX;
                hash = (hash * 31) + part.RequiredINT;
                hash = (hash * 31) + part.RequiredLUK;
                hash = (hash * 31) + part.RequiredFame;
                hash = (hash * 31) + part.RequiredJobMask;
                hash = (hash * 31) + part.BonusSTR;
                hash = (hash * 31) + part.BonusDEX;
                hash = (hash * 31) + part.BonusINT;
                hash = (hash * 31) + part.BonusLUK;
                hash = (hash * 31) + part.BonusHP;
                hash = (hash * 31) + part.BonusMP;
                hash = (hash * 31) + part.BonusWeaponAttack;
                hash = (hash * 31) + part.BonusMagicAttack;
                hash = (hash * 31) + part.BonusWeaponDefense;
                hash = (hash * 31) + part.BonusMagicDefense;
                hash = (hash * 31) + part.BonusAccuracy;
                hash = (hash * 31) + part.BonusAvoidability;
                hash = (hash * 31) + part.BonusHands;
                hash = (hash * 31) + part.BonusSpeed;
                hash = (hash * 31) + part.BonusJump;
                hash = (hash * 31) + part.UpgradeSlots;
                hash = (hash * 31) + (part.RemainingUpgradeSlotCount ?? int.MinValue);
                hash = (hash * 31) + (part.Durability ?? int.MinValue);
                hash = (hash * 31) + (part.MaxDurability ?? int.MinValue);
                hash = (hash * 31) + (part.IsTimeLimited ? 1 : 0);
                hash = (hash * 31) + part.TradeAvailable;
                hash = (hash * 31) + (part.IsTradeBlocked ? 1 : 0);
                hash = (hash * 31) + (part.IsEquipTradeBlocked ? 1 : 0);
                hash = (hash * 31) + (part.IsOneOfAKind ? 1 : 0);
                hash = (hash * 31) + (part.IsNotForSale ? 1 : 0);
                hash = (hash * 31) + (part.IsAccountSharable ? 1 : 0);
                hash = (hash * 31) + (part.HasAccountShareTag ? 1 : 0);
                hash = (hash * 31) + (part.ExpirationDateUtc?.ToBinary().GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>
        /// Get equipped weapon
        /// </summary>
        public WeaponPart GetWeapon()
        {
            if (Equipment.TryGetValue(EquipSlot.Weapon, out CharacterPart visibleWeapon) &&
                visibleWeapon is WeaponPart visibleWeaponPart &&
                !visibleWeaponPart.IsCash)
            {
                return visibleWeaponPart;
            }

            if (HiddenEquipment.TryGetValue(EquipSlot.Weapon, out CharacterPart hiddenWeapon))
            {
                return hiddenWeapon as WeaponPart;
            }

            if (Equipment.TryGetValue(EquipSlot.Weapon, out CharacterPart fallbackWeapon))
            {
                return fallbackWeapon as WeaponPart;
            }

            return null;
        }

        private static int DefaultRollInclusive(int min, int max)
        {
            return Random.Shared.Next(min, max + 1);
        }

        private AutoAssignStrategy ResolveAutoAssignStrategy()
        {
            int absoluteJobId = Math.Abs(Job);
            if (absoluteJobId == 0 || absoluteJobId == 2001)
            {
                return AutoAssignStrategy.BeginnerWarriorLike;
            }

            return ResolveAutoAssignClass(Job) switch
            {
                AutoAssignClassWarrior => AutoAssignStrategy.Warrior,
                AutoAssignClassMagician => AutoAssignStrategy.Magician,
                AutoAssignClassBowman => ResolveBowmanAutoAssignStrategy(absoluteJobId),
                AutoAssignClassThief => AutoAssignStrategy.Thief,
                AutoAssignClassPirate => ResolvePirateAutoAssignStrategy(absoluteJobId),
                _ => AutoAssignStrategy.GenericBeginner
            };
        }

        private AutoAssignStrategy ResolveBowmanAutoAssignStrategy(int absoluteJobId)
        {
            return GetWeaponCode(GetWeapon()?.ItemId ?? 0) switch
            {
                45 => AutoAssignStrategy.BowmanBow,
                46 or 52 => AutoAssignStrategy.BowmanCrossbowLike,
                _ => absoluteJobId switch
                {
                    300 or 310 or 311 or 312
                        or 1300 or 1310 or 1311 or 1312 => AutoAssignStrategy.BowmanBow,
                    320 or 321 or 322
                        or 3300 or 3310 or 3311 or 3312
                        or 2002 or 2300 or 2310 or 2311 or 2312 => AutoAssignStrategy.BowmanCrossbowLike,
                    _ => AutoAssignStrategy.BowmanCrossbowLike
                }
            };
        }

        private AutoAssignStrategy ResolvePirateAutoAssignStrategy(int absoluteJobId)
        {
            int equippedWeaponCode = GetWeaponCode(GetWeapon()?.ItemId ?? 0);
            if (equippedWeaponCode is 48 or 53)
            {
                return AutoAssignStrategy.PirateBrawlerLike;
            }

            if (IsDexDrivenPirateWeaponCode(equippedWeaponCode))
            {
                return AutoAssignStrategy.PirateGunslingerLike;
            }

            if (IsMechanicAutoAssignJob(absoluteJobId) || IsPirateGunslingerAutoAssignJob(absoluteJobId))
            {
                return AutoAssignStrategy.PirateGunslingerLike;
            }

            if (IsPirateBrawlerAutoAssignJob(absoluteJobId))
            {
                return AutoAssignStrategy.PirateBrawlerLike;
            }

            if (absoluteJobId == 500)
            {
                return InferBeginnerPirateAutoAssignStrategy();
            }

            return AutoAssignStrategy.PirateBrawlerLike;
        }

        private static bool IsMechanicAutoAssignJob(int absoluteJobId)
        {
            int jobFamily = absoluteJobId / 100;
            return jobFamily == 35;
        }

        private static bool IsPirateBrawlerAutoAssignJob(int absoluteJobId)
        {
            int jobBranch = absoluteJobId / 10;
            return jobBranch is 51 or 151 or 53;
        }

        private static bool IsPirateGunslingerAutoAssignJob(int absoluteJobId)
        {
            int jobBranch = absoluteJobId / 10;
            int jobFamily = absoluteJobId / 100;
            return jobBranch is 52 or 57 || jobFamily == 65;
        }

        private static bool IsCannoneerJob(int absoluteJobId)
        {
            return absoluteJobId / 10 == 53;
        }

        private static bool IsPolearmWarriorJob(int absoluteJobId)
        {
            int jobBranch = absoluteJobId / 10;
            int jobFamily = absoluteJobId / 100;
            return jobBranch == 13 || jobFamily == 21;
        }

        private AutoAssignStrategy InferBeginnerPirateAutoAssignStrategy()
        {
            return GetWeaponCode(GetWeapon()?.ItemId ?? 0) switch
            {
                48 or 53 => AutoAssignStrategy.PirateBrawlerLike,
                49 or 58 => AutoAssignStrategy.PirateGunslingerLike,
                _ => DEX > STR
                    ? AutoAssignStrategy.PirateGunslingerLike
                    : AutoAssignStrategy.PirateBrawlerLike
            };
        }

        private int GetWarriorStyleDexTarget()
        {
            int level = Math.Max(1, Level);
            return level > 30 ? level + 30 : 2 * level;
        }

        private int GetBrawlerStyleDexTarget()
        {
            int level = Math.Max(1, Level);
            return level > 20 ? level + 20 : 2 * level;
        }

        private int GetMagicianLukTarget()
        {
            return Math.Max(1, Level) + 3;
        }

        private int GetBowmanStrengthTarget(int additionalStrength)
        {
            return Math.Max(1, Level) + additionalStrength;
        }

        private int GetThiefDexTarget()
        {
            int level = Math.Max(1, Level);
            return level > 40 ? level + 40 : 2 * level;
        }

        private bool TryAutoAssignTowardsTarget(BuffStatType targetStat, int targetValue, BuffStatType fallbackStat)
        {
            return GetPrimaryStatValue(targetStat) < targetValue
                ? IncreasePrimaryStat(targetStat) || IncreasePrimaryStat(fallbackStat)
                : IncreasePrimaryStat(fallbackStat) || IncreasePrimaryStat(targetStat);
        }

        private int GetPrimaryStatValue(BuffStatType stat)
        {
            return stat switch
            {
                BuffStatType.Strength => STR,
                BuffStatType.Dexterity => DEX,
                BuffStatType.Intelligence => INT,
                BuffStatType.Luck => LUK,
                _ => 0
            };
        }

        private bool TryAutoAssignBeginnerPoint()
        {
            int minStat = Math.Min(Math.Min(STR, DEX), Math.Min(INT, LUK));
            if (STR == minStat)
            {
                return IncreasePrimaryStat(BuffStatType.Strength)
                       || IncreasePrimaryStat(BuffStatType.Dexterity)
                       || IncreasePrimaryStat(BuffStatType.Intelligence)
                       || IncreasePrimaryStat(BuffStatType.Luck);
            }

            if (DEX == minStat)
            {
                return IncreasePrimaryStat(BuffStatType.Dexterity)
                       || IncreasePrimaryStat(BuffStatType.Intelligence)
                       || IncreasePrimaryStat(BuffStatType.Luck)
                       || IncreasePrimaryStat(BuffStatType.Strength);
            }

            if (INT == minStat)
            {
                return IncreasePrimaryStat(BuffStatType.Intelligence)
                       || IncreasePrimaryStat(BuffStatType.Luck)
                       || IncreasePrimaryStat(BuffStatType.Strength)
                       || IncreasePrimaryStat(BuffStatType.Dexterity);
            }

            return IncreasePrimaryStat(BuffStatType.Luck)
                   || IncreasePrimaryStat(BuffStatType.Strength)
                   || IncreasePrimaryStat(BuffStatType.Dexterity)
                   || IncreasePrimaryStat(BuffStatType.Intelligence);
        }

        private static int ApplyRateBonus(int value, int percent)
        {
            if (percent == 0)
            {
                return value;
            }

            return (int)MathF.Floor(value * (100f + percent) / 100f);
        }

        private static float ApplyRateBonus(float value, int percent)
        {
            if (percent == 0)
            {
                return value;
            }

            return MathF.Floor(value * (100f + percent) / 100f);
        }

        private int SumEquipmentBonus(Func<CharacterPart, int> selector)
        {
            int total = 0;

            foreach (CharacterPart part in Equipment.Values)
            {
                if (part != null)
                {
                    total += selector(part);
                }
            }

            foreach (CharacterPart part in HiddenEquipment.Values)
            {
                if (part != null)
                {
                    total += selector(part);
                }
            }

            return total;
        }

        private int GetBaseAccuracy()
        {
            return ResolveJobArchetype() switch
            {
                JobArchetype.Warrior => (int)Math.Floor(TotalDEX * 0.8f + TotalLUK * 0.5f),
                JobArchetype.Magician => (int)Math.Floor(TotalDEX * 0.8f + TotalLUK * 0.5f),
                JobArchetype.Bowman => (int)Math.Floor(TotalDEX * 1.2f + TotalLUK),
                JobArchetype.Thief => (int)Math.Floor(TotalDEX * 0.6f + TotalLUK * 0.3f),
                JobArchetype.Pirate => (int)Math.Floor(TotalDEX * 0.8f + TotalSTR * 0.5f),
                _ => (int)Math.Floor(TotalDEX * 0.8f + TotalLUK * 0.5f)
            };
        }

        private int GetBaseAvoidability()
        {
            return ResolveJobArchetype() switch
            {
                JobArchetype.Thief => (int)Math.Floor(TotalDEX * 0.3f + TotalLUK * 0.6f),
                JobArchetype.Magician => (int)Math.Floor(TotalDEX * 0.2f + TotalLUK * 0.5f),
                _ => (int)Math.Floor(TotalDEX * 0.25f + TotalLUK * 0.5f)
            };
        }

        private int GetHpIncreaseAmount(Func<int, int, int> rollInclusive)
        {
            (int min, int max) range = ResolveJobArchetype() switch
            {
                JobArchetype.Warrior => (20, 24),
                JobArchetype.Magician => (6, 10),
                JobArchetype.Bowman => (16, 20),
                JobArchetype.Thief => (16, 20),
                JobArchetype.Pirate => (18, 22),
                _ => (8, 12)
            };

            return rollInclusive(range.min, range.max);
        }

        private int GetMpIncreaseAmount(Func<int, int, int> rollInclusive)
        {
            (int min, int max) range = ResolveJobArchetype() switch
            {
                JobArchetype.Warrior => (2, 4),
                JobArchetype.Magician => (18, 20),
                JobArchetype.Bowman => (10, 12),
                JobArchetype.Thief => (10, 12),
                JobArchetype.Pirate => (14, 16),
                _ => (6, 8)
            };

            return rollInclusive(range.min, range.max);
        }

        public static int ResolveAutoAssignClass(int jobId)
        {
            int absoluteJobId = Math.Abs(jobId);
            int jobBranch = absoluteJobId / 100;
            return jobBranch switch
            {
                1 or 11 or 21 or 31 or 51 or 61 => AutoAssignClassWarrior,
                2 or 12 or 22 or 27 or 32 => AutoAssignClassMagician,
                3 or 13 or 23 or 33 or 63 => AutoAssignClassBowman,
                4 or 14 or 24 or 64 => AutoAssignClassThief,
                5 or 15 or 25 or 35 or 65 or 155 => AutoAssignClassPirate,
                37 or 41 or 101 or 151 => AutoAssignClassWarrior,
                42 or 142 or 152 => AutoAssignClassMagician,
                // Hero branches use job-root starters before their first advancement.
                20 when absoluteJobId == 2000 => AutoAssignClassWarrior,
                20 when absoluteJobId == 2001 => AutoAssignClassMagician,
                20 when absoluteJobId == 2002 => AutoAssignClassBowman,
                20 when absoluteJobId == 2003 => AutoAssignClassThief,
                20 when absoluteJobId == 2004 => AutoAssignClassMagician,
                20 when absoluteJobId == 2005 => AutoAssignClassPirate,
                // Resistance uses a shared Citizen beginner plus the Demon beginner root.
                30 when absoluteJobId == 3001 => AutoAssignClassWarrior,
                30 when absoluteJobId == 3002 => AutoAssignClassBeginner,
                40 when absoluteJobId == 4001 => AutoAssignClassWarrior,
                40 when absoluteJobId == 4002 => AutoAssignClassMagician,
                // Post-Big Bang roots keep their own beginner ids before advancing into later job branches.
                50 when absoluteJobId == 5000 => AutoAssignClassWarrior,
                60 when absoluteJobId == 6000 => AutoAssignClassWarrior,
                60 when absoluteJobId == 6001 => AutoAssignClassPirate,
                60 when absoluteJobId == 6002 => AutoAssignClassThief,
                60 when absoluteJobId == 6003 => AutoAssignClassBowman,
                100 when absoluteJobId == 10000 => AutoAssignClassWarrior,
                150 when absoluteJobId == 15000 => AutoAssignClassMagician,
                150 when absoluteJobId == 15001 => AutoAssignClassPirate,
                150 when absoluteJobId == 15002 => AutoAssignClassWarrior,
                _ => AutoAssignClassBeginner
            };
        }

        private JobArchetype ResolveJobArchetype()
        {
            return ResolveAutoAssignClass(Job) switch
            {
                AutoAssignClassWarrior => JobArchetype.Warrior,
                AutoAssignClassMagician => JobArchetype.Magician,
                AutoAssignClassBowman => JobArchetype.Bowman,
                AutoAssignClassThief => JobArchetype.Thief,
                AutoAssignClassPirate => JobArchetype.Pirate,
                _ => JobArchetype.Beginner
            };
        }

        private int ComputeDisplayedPhysicalAttack()
        {
            AttackFormulaProfile profile = ResolveAttackFormulaProfile();
            if (profile.UsesMagicFormula)
            {
                return ComputeDisplayedMagicAttack();
            }

            float attackStat = Math.Max(1f, TotalWeaponAttackStat);
            float maxDamage = ((profile.PrimaryStat * profile.WeaponMultiplier) + profile.SecondaryStat) * attackStat / 100f;
            float minDamage = ((((profile.PrimaryStat * profile.WeaponMultiplier) * profile.MasteryPrimaryScale) * (TotalMastery / 100f)) + profile.SecondaryStat) * attackStat / 100f;
            return Math.Max(0, (int)MathF.Round((minDamage + maxDamage) * 0.5f));
        }

        private int ComputeDisplayedMagicAttack()
        {
            AttackFormulaProfile profile = ResolveAttackFormulaProfile();
            int primaryStat = profile.UsesMagicFormula ? profile.PrimaryStat : TotalINT;
            int secondaryStat = profile.UsesMagicFormula ? profile.SecondaryStat : TotalLUK;
            float magicAttackStat = Math.Max(1f, TotalMagicAttackStat);
            float maxDamage = ((primaryStat * 4f) + secondaryStat) * magicAttackStat / 100f;
            float minDamage = (((primaryStat * 4f) * (TotalMastery / 100f)) + secondaryStat) * magicAttackStat / 100f;
            return Math.Max(0, (int)MathF.Round((minDamage + maxDamage) * 0.5f));
        }

        private int ComputeDisplayedPhysicalDefense()
        {
            float statContribution = ResolveJobArchetype() switch
            {
                JobArchetype.Warrior => TotalSTR * 0.35f + TotalDEX * 0.20f,
                JobArchetype.Magician => TotalINT * 0.15f + TotalLUK * 0.10f,
                JobArchetype.Bowman => TotalDEX * 0.30f + TotalSTR * 0.15f,
                JobArchetype.Thief => TotalLUK * 0.30f + TotalDEX * 0.20f,
                JobArchetype.Pirate => UsesDexDrivenPirateWeapon() ? TotalDEX * 0.30f + TotalSTR * 0.15f : TotalSTR * 0.30f + TotalDEX * 0.20f,
                _ => TotalSTR * 0.20f + TotalDEX * 0.20f + TotalLUK * 0.10f
            };

            return Math.Max(0, TotalWeaponDefenseStat + (int)MathF.Floor(statContribution));
        }

        private int ComputeDisplayedMagicDefense()
        {
            float statContribution = ResolveJobArchetype() switch
            {
                JobArchetype.Warrior => TotalINT * 0.20f + TotalLUK * 0.10f,
                JobArchetype.Magician => TotalINT * 0.45f + TotalLUK * 0.20f,
                JobArchetype.Bowman => TotalINT * 0.25f + TotalLUK * 0.12f,
                JobArchetype.Thief => TotalINT * 0.25f + TotalLUK * 0.15f,
                JobArchetype.Pirate => TotalINT * 0.22f + TotalLUK * 0.12f,
                _ => TotalINT * 0.20f + TotalLUK * 0.10f
            };

            return Math.Max(0, TotalMagicDefenseStat + (int)MathF.Floor(statContribution));
        }

        private AttackFormulaProfile ResolveAttackFormulaProfile()
        {
            WeaponPart weapon = GetWeapon();
            int weaponCode = GetWeaponCode(weapon?.ItemId ?? 0);
            int thiefSecondaryStat = GetThiefSecondaryDamageStat();

            if (weaponCode <= 0)
            {
                return ResolveWeaponlessAttackFormulaProfile(thiefSecondaryStat);
            }

            return weaponCode switch
            {
                30 => new AttackFormulaProfile(false, 4.0f, TotalSTR, TotalDEX, 0.9f),
                31 => new AttackFormulaProfile(false, 4.4f, TotalSTR, TotalDEX, 0.9f),
                32 => new AttackFormulaProfile(false, 4.8f, TotalSTR, TotalDEX, 0.9f),
                33 => ResolveDaggerFormulaProfile(),
                34 => new AttackFormulaProfile(false, 3.6f, TotalLUK, thiefSecondaryStat, 0.9f),
                36 => new AttackFormulaProfile(false, 3.6f, TotalLUK, thiefSecondaryStat, 0.9f),
                37 => new AttackFormulaProfile(true, 1.0f, TotalINT, TotalLUK, 1.0f),
                38 => new AttackFormulaProfile(true, 1.0f, TotalINT, TotalLUK, 1.0f),
                40 => new AttackFormulaProfile(false, 4.6f, TotalSTR, TotalDEX, 0.9f),
                41 => new AttackFormulaProfile(false, 4.8f, TotalSTR, TotalDEX, 0.9f),
                42 => new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f),
                43 => new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f),
                44 => new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f),
                45 => new AttackFormulaProfile(false, 3.4f, TotalDEX, TotalSTR, 0.9f),
                46 => new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                47 => new AttackFormulaProfile(false, 3.6f, TotalLUK, thiefSecondaryStat, 0.9f),
                48 => new AttackFormulaProfile(false, 4.8f, TotalSTR, TotalDEX, 0.9f),
                49 => new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                52 => new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                53 => new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f),
                // WZ still carries later post-Big Bang weapon families beyond the original
                // v95-era stat surface. Treat Shining Rods as magic-weapon families and Soul
                // Shooters as dex-driven pirate weapons until their exact client coefficients are
                // recovered.
                56 => new AttackFormulaProfile(true, 1.0f, TotalINT, TotalLUK, 1.0f),
                58 => new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                _ when UsesMagicFormulaByJob() => new AttackFormulaProfile(true, 1.0f, TotalINT, TotalLUK, 1.0f),
                _ when UsesDexDrivenPirateWeapon() => new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                _ => ResolveWeaponlessAttackFormulaProfile(thiefSecondaryStat)
            };
        }

        private AttackFormulaProfile ResolveWeaponlessAttackFormulaProfile(int thiefSecondaryStat)
        {
            int absoluteJobId = Math.Abs(Job);
            if (UsesMagicFormulaByJob())
            {
                return new AttackFormulaProfile(true, 1.0f, TotalINT, TotalLUK, 1.0f);
            }

            return ResolveJobArchetype() switch
            {
                JobArchetype.Bowman => ResolveBowmanAutoAssignStrategy(absoluteJobId) == AutoAssignStrategy.BowmanBow
                    ? new AttackFormulaProfile(false, 3.4f, TotalDEX, TotalSTR, 0.9f)
                    : new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                JobArchetype.Pirate => ResolveWeaponlessPirateAttackFormulaProfile(absoluteJobId),
                JobArchetype.Thief => new AttackFormulaProfile(false, 3.6f, TotalLUK, thiefSecondaryStat, 0.9f),
                JobArchetype.Warrior when IsPolearmWarriorJob(absoluteJobId) => new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f),
                _ => new AttackFormulaProfile(false, 4.0f, TotalSTR, TotalDEX, 0.9f)
            };
        }

        private AttackFormulaProfile ResolveWeaponlessPirateAttackFormulaProfile(int absoluteJobId)
        {
            if (IsCannoneerJob(absoluteJobId))
            {
                return new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f);
            }

            return ResolvePirateAutoAssignStrategy(absoluteJobId) == AutoAssignStrategy.PirateGunslingerLike
                ? new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f)
                : new AttackFormulaProfile(false, 4.8f, TotalSTR, TotalDEX, 0.9f);
        }

        private AttackFormulaProfile ResolveDaggerFormulaProfile()
        {
            return ResolveJobArchetype() == JobArchetype.Thief
                ? new AttackFormulaProfile(false, 3.6f, TotalLUK, GetThiefSecondaryDamageStat(), 0.9f)
                : new AttackFormulaProfile(false, 4.0f, TotalSTR, TotalDEX, 0.9f);
        }

        private int GetThiefSecondaryDamageStat()
        {
            // Thief-family weapon formulas use the live STR+DEX secondary term rather than
            // dropping the STR contribution from claw, dagger, katara, or cane users.
            return TotalSTR + TotalDEX;
        }

        private bool UsesMagicFormulaByJob()
        {
            if (GetWeapon() is WeaponPart weapon)
            {
                int weaponCode = GetWeaponCode(weapon.ItemId);
                if (weaponCode is 37 or 38 or 56)
                {
                    return true;
                }
            }

            return ResolveJobArchetype() == JobArchetype.Magician;
        }

        private bool UsesDexDrivenPirateWeapon()
        {
            if (GetWeapon() is not WeaponPart weapon)
            {
                return false;
            }

            int weaponCode = GetWeaponCode(weapon.ItemId);
            return IsDexDrivenPirateWeaponCode(weaponCode);
        }

        private static bool IsDexDrivenPirateWeaponCode(int weaponCode)
        {
            return weaponCode is 49 or 58;
        }

        private static int GetWeaponCode(int itemId)
        {
            return itemId > 0 ? Math.Abs(itemId / 10000) % 100 : 0;
        }

        private int GetSkillStatBonus(BuffStatType stat)
        {
            return Math.Max(0, SkillStatBonusProvider?.Invoke(stat) ?? 0);
        }

        /// <summary>
        /// Clone this build
        /// </summary>
        public CharacterBuild Clone()
        {
            return new CharacterBuild
            {
                Id = Id,
                Name = Name,
                Gender = Gender,
                Skin = Skin,
                Body = Body,
                Head = Head,
                Face = Face,
                Hair = Hair,
                WeaponSticker = WeaponSticker,
                ActivePortableChair = ActivePortableChair,
                RemotePetItemIds = RemotePetItemIds != null ? new List<int>(RemotePetItemIds) : Array.Empty<int>(),
                EquipmentPartLoader = EquipmentPartLoader,
                Equipment = CloneEquipmentLayer(Equipment),
                HiddenEquipment = CloneEquipmentLayer(HiddenEquipment),
                Level = Level,
                MaxHP = MaxHP,
                MaxMP = MaxMP,
                HP = HP,
                MP = MP,
                STR = STR,
                DEX = DEX,
                INT = INT,
                LUK = LUK,
                AP = AP,
                Job = Job,
                SubJob = SubJob,
                JobName = JobName,
                GuildName = GuildName,
                AllianceName = AllianceName,
                Fame = Fame,
                CookieHousePoint = CookieHousePoint,
                WorldRank = WorldRank,
                JobRank = JobRank,
                HasMonsterRiding = HasMonsterRiding,
                TraitCharisma = TraitCharisma,
                TraitInsight = TraitInsight,
                TraitWill = TraitWill,
                TraitCraft = TraitCraft,
                TraitSense = TraitSense,
                TraitCharm = TraitCharm,
                HasPendantSlotExtension = HasPendantSlotExtension,
                HasPocketSlot = HasPocketSlot,
                Exp = Exp,
                ExpToNextLevel = ExpToNextLevel,
                Attack = Attack,
                Defense = Defense,
                MagicAttack = MagicAttack,
                MagicDefense = MagicDefense,
                Accuracy = Accuracy,
                Avoidability = Avoidability,
                Hands = Hands,
                CriticalRate = CriticalRate,
                Speed = Speed,
                JumpPower = JumpPower,
                SkillStatBonusProvider = SkillStatBonusProvider,
                SkillMasteryProvider = SkillMasteryProvider
            };
        }

        private static Dictionary<EquipSlot, CharacterPart> CloneEquipmentLayer(
            IReadOnlyDictionary<EquipSlot, CharacterPart> source)
        {
            Dictionary<EquipSlot, CharacterPart> clone = new();
            if (source == null)
            {
                return clone;
            }

            foreach (KeyValuePair<EquipSlot, CharacterPart> entry in source)
            {
                clone[entry.Key] = entry.Value?.Clone();
            }

            return clone;
        }
    }

    #endregion

    #region Z-Map Reference

    /// <summary>
    /// Z-layer mapping for proper part ordering (from zmap.img)
    /// </summary>
    public static class ZMapReference
    {
        private static readonly object MapperLock = new();

        // Standard z-layer strings - order matters! Lower index = rendered first (behind)
        private static readonly string[] DefaultZOrder = new[]
        {
            "backHair",
            "backHairOverCape",
            "backWing",
            "cape",
            "shield",
            "shieldOverBody",
            "weaponBelowBody",      // Weapon behind body (for some poses)
            "body",
            "backBody",
            "gloveWrist",
            "pants",
            "pantsOverShoes",
            "shoes",
            "coat",
            "coatBelowArmoverMail",
            "mail",
            "mailChest",
            "weaponBelowArm",       // Weapon behind arm (moved here from after weapon)
            "arm",
            "armOverHair",
            "armOverHairBelowWeapon",
            "weaponBelowHand",      // Weapon behind hand (for standing/holding poses)
            "hand",
            "handBelowWeapon",
            "handOverHair",
            "glove",
            "mailArm",
            "gloveOverBody",
            "gloveOverHair",
            "gloveWristOverHair",
            "weapon",               // Default weapon layer (in front of hand)
            "weaponOverGlove",
            "weaponOverArm",
            "weaponOverHand",
            "weaponOverBody",
            "head",
            "ear",
            "face",
            "faceOverHair",
            "hairShade",
            "hair",
            "hairOverHead",
            "cap",
            "capOverHair",
            "capAccessory",
            "capAccessoryBelowAccFace",
            "accessoryFace",
            "accessoryFaceOverFaceAcc",
            "accessoryFaceOverEar",
            "accessoryFaceBelowFace",
            "accessoryEyes",
            "accessoryEar",
            "accessoryEarBelowFace",
            "accessoryFaceOverFaceBelowCap",
            "accessoryFaceUpperOverCap",
            "shadow"
        };

        private static bool _mappersLoaded;
        private static string[] _zOrder = DefaultZOrder;
        private static Dictionary<string, int> _zIndices = BuildZIndexMap(DefaultZOrder);
        private static Dictionary<string, string[]> _slotMap = new(StringComparer.Ordinal);

        public static IReadOnlyList<string> ZOrder
        {
            get
            {
                EnsureLoaded();
                return _zOrder;
            }
        }

        public static int GetZIndex(string zLayer)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(zLayer) && _zIndices.TryGetValue(zLayer, out int index))
            {
                return index;
            }

            return _zOrder.Length > 0 ? _zOrder.Length / 2 : 50;
        }

        /// <summary>
        /// Check if z-layer exists in the order array
        /// </summary>
        public static bool HasZLayer(string zLayer)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(zLayer) && _zIndices.ContainsKey(zLayer);
        }

        public static IReadOnlyList<string> GetSlotTokens(string zLayer)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(zLayer) && _slotMap.TryGetValue(zLayer, out string[] tokens))
            {
                return tokens;
            }

            return Array.Empty<string>();
        }

        public static int GetSlotPriority(string slotValue)
        {
            EnsureLoaded();

            int maxPriority = int.MinValue;
            foreach (string token in CharacterPart.ParseSlotTokens(slotValue))
            {
                if (_zIndices.TryGetValue(token, out int priority))
                {
                    maxPriority = Math.Max(maxPriority, priority);
                }
            }

            return maxPriority;
        }

        public static int GetSlotPriority(IEnumerable<string> slotTokens)
        {
            EnsureLoaded();

            int maxPriority = int.MinValue;
            if (slotTokens == null)
            {
                return maxPriority;
            }

            foreach (string token in slotTokens)
            {
                if (!string.IsNullOrEmpty(token) && _zIndices.TryGetValue(token, out int priority))
                {
                    maxPriority = Math.Max(maxPriority, priority);
                }
            }

            return maxPriority;
        }

        public static void EnsureLoaded()
        {
            if (_mappersLoaded)
            {
                return;
            }

            lock (MapperLock)
            {
                if (_mappersLoaded)
                {
                    return;
                }

                TryLoadMappers();
                _mappersLoaded = true;
            }
        }

        private static void TryLoadMappers()
        {
            try
            {
                var zMapImage = global::HaCreator.Program.FindImage("base", "zmap.img");
                if (zMapImage != null)
                {
                    zMapImage.ParseImage();

                    var loadedOrder = new List<string>();
                    foreach (WzImageProperty property in zMapImage.WzProperties)
                    {
                        if (!string.IsNullOrWhiteSpace(property.Name))
                        {
                            loadedOrder.Add(property.Name);
                        }
                    }

                    if (loadedOrder.Count > 0)
                    {
                        // zmap.img is enumerated from front to back; drawing needs back to front.
                        loadedOrder.Reverse();
                        _zOrder = loadedOrder.ToArray();
                        _zIndices = BuildZIndexMap(_zOrder);
                    }
                }

                var sMapImage = global::HaCreator.Program.FindImage("base", "smap.img");
                if (sMapImage != null)
                {
                    sMapImage.ParseImage();

                    var loadedSlotMap = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (WzImageProperty property in sMapImage.WzProperties)
                    {
                        string value = property switch
                        {
                            WzStringProperty stringProperty => stringProperty.Value,
                            _ => null
                        };

                        loadedSlotMap[property.Name] = new List<string>(CharacterPart.ParseSlotTokens(value)).ToArray();
                    }

                    if (loadedSlotMap.Count > 0)
                    {
                        _slotMap = loadedSlotMap;
                    }
                }
            }
            catch
            {
                _zOrder = DefaultZOrder;
                _zIndices = BuildZIndexMap(DefaultZOrder);
                _slotMap = new Dictionary<string, string[]>(StringComparer.Ordinal);
            }
        }

        private static Dictionary<string, int> BuildZIndexMap(IReadOnlyList<string> zOrder)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            if (zOrder == null)
            {
                return map;
            }

            for (int i = 0; i < zOrder.Count; i++)
            {
                string entry = zOrder[i];
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    map[entry] = i;
                }
            }

            return map;
        }
    }

    #endregion
}
