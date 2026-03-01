using System;
using System.Collections.Generic;
using HaSharedLibrary.Render.DX;
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
        TamingMob
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
        Cape = 11,
        Ring1 = 12,
        Ring2 = 13,
        Ring3 = 14,
        Ring4 = 15,
        Pendant = 17,
        TamingMob = 18,
        Saddle = 19,
        Medal = 49,
        Belt = 50,
        Weapon = 11
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
        public int ItemId { get; set; }
        public string Name { get; set; }
        public CharacterPartType Type { get; set; }
        public EquipSlot Slot { get; set; }

        // Animations indexed by action name
        public Dictionary<string, CharacterAnimation> Animations { get; set; } = new();

        // For equipment with visible slots
        public string VSlot { get; set; }           // Visible slot conflicts
        public bool IsCash { get; set; }            // Cash shop item (overrides defaults)

        // Icon for UI
        public IDXObject Icon { get; set; }
        public IDXObject IconRaw { get; set; }

        /// <summary>
        /// Get animation for an action, with fallback
        /// </summary>
        public CharacterAnimation GetAnimation(CharacterAction action)
        {
            string actionName = GetActionString(action);
            if (Animations.TryGetValue(actionName, out var anim))
                return anim;

            // Fallback to stand1
            if (Animations.TryGetValue("stand1", out anim))
                return anim;

            // Any animation
            foreach (var kv in Animations)
                return kv.Value;

            return null;
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
    }

    #endregion

    #region Character Build

    /// <summary>
    /// Complete character build with all parts
    /// </summary>
    public class CharacterBuild
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public CharacterGender Gender { get; set; }
        public SkinColor Skin { get; set; }

        // Core parts
        public BodyPart Body { get; set; }
        public BodyPart Head { get; set; }
        public FacePart Face { get; set; }
        public HairPart Hair { get; set; }

        // Equipment slots
        public Dictionary<EquipSlot, CharacterPart> Equipment { get; set; } = new();

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
        public string JobName { get; set; } = "Beginner";

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
        public float Speed { get; set; } = 100;         // Movement speed %
        public float JumpPower { get; set; } = 100;     // Jump height %

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

        /// <summary>
        /// Unequip an item
        /// </summary>
        public CharacterPart Unequip(EquipSlot slot)
        {
            if (Equipment.TryGetValue(slot, out var part))
            {
                Equipment.Remove(slot);
                return part;
            }
            return null;
        }

        /// <summary>
        /// Get equipped weapon
        /// </summary>
        public WeaponPart GetWeapon()
        {
            if (Equipment.TryGetValue(EquipSlot.Weapon, out var weapon))
                return weapon as WeaponPart;
            return null;
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
                Equipment = new Dictionary<EquipSlot, CharacterPart>(Equipment),
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
                JobName = JobName,
                Exp = Exp,
                ExpToNextLevel = ExpToNextLevel,
                Attack = Attack,
                Defense = Defense,
                MagicAttack = MagicAttack,
                MagicDefense = MagicDefense,
                Accuracy = Accuracy,
                Avoidability = Avoidability,
                Speed = Speed,
                JumpPower = JumpPower
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
        // Standard z-layer strings - order matters! Lower index = rendered first (behind)
        public static readonly string[] ZOrder = new[]
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
            "mailArm",
            "weaponBelowArm",       // Weapon behind arm (moved here from after weapon)
            "arm",
            "armOverHair",
            "armOverHairBelowWeapon",
            "weaponBelowHand",      // Weapon behind hand (for standing/holding poses)
            "hand",
            "handBelowWeapon",
            "handOverHair",
            "glove",
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

        public static int GetZIndex(string zLayer)
        {
            int index = Array.IndexOf(ZOrder, zLayer);
            return index >= 0 ? index : 50; // Default to middle
        }

        /// <summary>
        /// Check if z-layer exists in the order array
        /// </summary>
        public static bool HasZLayer(string zLayer)
        {
            return Array.IndexOf(ZOrder, zLayer) >= 0;
        }
    }

    #endregion
}
