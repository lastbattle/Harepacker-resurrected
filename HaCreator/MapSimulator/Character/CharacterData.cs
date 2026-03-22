using System;
using System.Collections.Generic;
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

        // For equipment with visible slots
        public string VSlot { get; set; }           // Visible slot conflicts
        public string ISlot { get; set; }           // Item slot priority
        public bool IsCash { get; set; }            // Cash shop item (overrides defaults)
        public string Description { get; set; }
        public string ItemCategory { get; set; }
        public DateTime? ExpirationDateUtc { get; set; }
        public int? Durability { get; set; }
        public int? MaxDurability { get; set; }
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
        public int BonusSpeed { get; set; }
        public int BonusJump { get; set; }
        public int UpgradeSlots { get; set; }

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
                VSlot = VSlot,
                ISlot = ISlot,
                IsCash = IsCash,
                Description = Description,
                ItemCategory = ItemCategory,
                ExpirationDateUtc = ExpirationDateUtc,
                Durability = Durability,
                MaxDurability = MaxDurability,
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
                BonusSpeed = BonusSpeed,
                BonusJump = BonusJump,
                UpgradeSlots = UpgradeSlots,
                Icon = Icon,
                IconRaw = IconRaw
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
            return FindAnimation(Animations, actionName);
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
    public class FacePart : CharacterPart
    {
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
                BonusSpeed = BonusSpeed,
                BonusJump = BonusJump,
                UpgradeSlots = UpgradeSlots,
                Icon = Icon,
                IconRaw = IconRaw,
                AttackSpeed = AttackSpeed,
                Attack = Attack,
                WeaponType = WeaponType,
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
        public List<PortableChairLayer> CoupleMidpointLayers { get; set; } = new();
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

        public int TotalSTR => STR + SumEquipmentBonus(part => part.BonusSTR);
        public int TotalDEX => DEX + SumEquipmentBonus(part => part.BonusDEX);
        public int TotalINT => INT + SumEquipmentBonus(part => part.BonusINT);
        public int TotalLUK => LUK + SumEquipmentBonus(part => part.BonusLUK);
        public int TotalMaxHP => Math.Clamp(MaxHP + SumEquipmentBonus(part => part.BonusHP) + GetSkillStatBonus(BuffStatType.MaxHP), 1, MaxHpMpStat);
        public int TotalMaxMP => Math.Clamp(MaxMP + SumEquipmentBonus(part => part.BonusMP) + GetSkillStatBonus(BuffStatType.MaxMP), 0, MaxHpMpStat);
        public int TotalHP => Math.Clamp(HP + SumEquipmentBonus(part => part.BonusHP), 0, TotalMaxHP);
        public int TotalMP => Math.Clamp(MP + SumEquipmentBonus(part => part.BonusMP), 0, TotalMaxMP);
        public int TotalMastery => Math.Clamp(SkillMasteryProvider?.Invoke() ?? MinimumMasteryPercent, MinimumMasteryPercent, 100);
        public int TotalWeaponAttackStat => Math.Max(0, Math.Max(0, Attack - DefaultAttackValue) + SumEquipmentBonus(part => part.BonusWeaponAttack) + GetSkillStatBonus(BuffStatType.Attack));
        public int TotalWeaponDefenseStat => Math.Max(0, Math.Max(0, Defense - DefaultDefenseValue) + SumEquipmentBonus(part => part.BonusWeaponDefense) + GetSkillStatBonus(BuffStatType.Defense));
        public int TotalMagicAttackStat => Math.Max(0, Math.Max(0, MagicAttack - DefaultMagicAttackValue) + SumEquipmentBonus(part => part.BonusMagicAttack) + GetSkillStatBonus(BuffStatType.MagicAttack));
        public int TotalMagicDefenseStat => Math.Max(0, Math.Max(0, MagicDefense - DefaultMagicDefenseValue) + SumEquipmentBonus(part => part.BonusMagicDefense) + GetSkillStatBonus(BuffStatType.MagicDefense));
        public int TotalAttack => ComputeDisplayedPhysicalAttack();
        public int TotalDefense => ComputeDisplayedPhysicalDefense();
        public int TotalMagicAttack => ComputeDisplayedMagicAttack();
        public int TotalMagicDefense => ComputeDisplayedMagicDefense();

        public int TotalAccuracy => Math.Max(0, GetBaseAccuracy() + Accuracy + SumEquipmentBonus(part => part.BonusAccuracy) + GetSkillStatBonus(BuffStatType.Accuracy));
        public int TotalAvoidability => Math.Max(0, GetBaseAvoidability() + Avoidability + SumEquipmentBonus(part => part.BonusAvoidability) + GetSkillStatBonus(BuffStatType.Avoidability));
        public int TotalHands => Math.Max(0, Hands + TotalDEX + TotalINT + TotalLUK);
        public int TotalCriticalRate => Math.Max(0, CriticalRate + GetSkillStatBonus(BuffStatType.CriticalRate));
        public float TotalSpeed => Math.Max(0f, Speed + SumEquipmentBonus(part => part.BonusSpeed) + GetSkillStatBonus(BuffStatType.Speed));
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

        private JobArchetype ResolveJobArchetype()
        {
            int jobBranch = Math.Abs(Job) / 100;
            return jobBranch switch
            {
                1 or 11 or 21 or 31 => JobArchetype.Warrior,
                2 or 12 or 22 or 32 => JobArchetype.Magician,
                3 or 13 or 23 or 33 => JobArchetype.Bowman,
                4 or 14 or 24 => JobArchetype.Thief,
                5 or 15 or 35 => JobArchetype.Pirate,
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

            return weaponCode switch
            {
                30 => new AttackFormulaProfile(false, 4.0f, TotalSTR, TotalDEX, 0.9f),
                31 => new AttackFormulaProfile(false, 4.4f, TotalSTR, TotalDEX, 0.9f),
                32 => new AttackFormulaProfile(false, 4.8f, TotalSTR, TotalDEX, 0.9f),
                33 => ResolveDaggerFormulaProfile(),
                34 => new AttackFormulaProfile(false, 3.6f, TotalLUK, TotalDEX, 0.9f),
                36 => new AttackFormulaProfile(false, 3.6f, TotalLUK, TotalDEX, 0.9f),
                37 => new AttackFormulaProfile(true, 1.0f, TotalINT, TotalLUK, 1.0f),
                38 => new AttackFormulaProfile(true, 1.0f, TotalINT, TotalLUK, 1.0f),
                40 => new AttackFormulaProfile(false, 4.6f, TotalSTR, TotalDEX, 0.9f),
                41 => new AttackFormulaProfile(false, 4.8f, TotalSTR, TotalDEX, 0.9f),
                42 => new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f),
                43 => new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f),
                44 => new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f),
                45 => new AttackFormulaProfile(false, 3.4f, TotalDEX, TotalSTR, 0.9f),
                46 => new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                47 => new AttackFormulaProfile(false, 3.6f, TotalLUK, TotalDEX, 0.9f),
                48 => new AttackFormulaProfile(false, 4.8f, TotalSTR, TotalDEX, 0.9f),
                49 => new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                52 => new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                53 => new AttackFormulaProfile(false, 5.0f, TotalSTR, TotalDEX, 0.9f),
                _ when UsesMagicFormulaByJob() => new AttackFormulaProfile(true, 1.0f, TotalINT, TotalLUK, 1.0f),
                _ when UsesDexDrivenPirateWeapon() => new AttackFormulaProfile(false, 3.6f, TotalDEX, TotalSTR, 0.9f),
                _ when ResolveJobArchetype() == JobArchetype.Bowman => new AttackFormulaProfile(false, 3.4f, TotalDEX, TotalSTR, 0.9f),
                _ when ResolveJobArchetype() == JobArchetype.Thief => new AttackFormulaProfile(false, 3.6f, TotalLUK, TotalDEX, 0.9f),
                _ => new AttackFormulaProfile(false, 4.0f, TotalSTR, TotalDEX, 0.9f)
            };
        }

        private AttackFormulaProfile ResolveDaggerFormulaProfile()
        {
            return ResolveJobArchetype() == JobArchetype.Thief
                ? new AttackFormulaProfile(false, 3.6f, TotalLUK, TotalDEX, 0.9f)
                : new AttackFormulaProfile(false, 4.0f, TotalSTR, TotalDEX, 0.9f);
        }

        private bool UsesMagicFormulaByJob()
        {
            if (GetWeapon() is WeaponPart weapon)
            {
                int weaponCode = GetWeaponCode(weapon.ItemId);
                if (weaponCode is 37 or 38)
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
            return weaponCode == 49;
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
                Equipment = new Dictionary<EquipSlot, CharacterPart>(Equipment),
                HiddenEquipment = new Dictionary<EquipSlot, CharacterPart>(HiddenEquipment),
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
