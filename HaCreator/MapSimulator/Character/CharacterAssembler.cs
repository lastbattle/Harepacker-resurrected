using System;
using System.Collections.Generic;
using System.Linq;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Assembled frame ready for rendering - contains all parts positioned correctly
    /// </summary>
    public class AssembledFrame
    {
        public List<AssembledPart> Parts { get; set; } = new();
        public Rectangle Bounds { get; set; }
        public Point Origin { get; set; }
        public int Duration { get; set; }

        /// <summary>
        /// Offset from navel (anchor point) to feet (bottom of character).
        /// In MapleStory, the player's Y position represents their feet position (on the foothold),
        /// but the character is assembled with navel at origin. This offset is used to adjust
        /// rendering so the character's feet are at the specified Y position instead of the navel.
        /// </summary>
        public int FeetOffset { get; set; }

        /// <summary>
        /// Draw all parts in order
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer,
            int screenX, int screenY, bool flip, Color tint, float scale = 1f, float rotation = 0f)
        {
            // Apply feet offset: move character up so feet are at screenY instead of navel
            // In local coords, feet are at FeetOffset below navel (positive = below)
            // To place feet at screenY, we subtract FeetOffset from the render Y
            int adjustedY = screenY - FeetOffset;

            foreach (var part in Parts)
            {
                if (part.Texture == null || !part.IsVisible) continue;

                int partX, partY;
                if (flip)
                {
                    // When flipped, we need to mirror the X offset and account for texture width
                    // The offset was calculated as: offset.X - origin.X
                    // When flipped, the visual position needs to be mirrored around the character center
                    partX = screenX - part.OffsetX - part.Texture.Width;
                    partY = adjustedY + part.OffsetY;
                }
                else
                {
                    partX = screenX + part.OffsetX;
                    partY = adjustedY + part.OffsetY;
                }

                // Use DrawBackground for proper rendering
                Color partColor = part.Tint != Color.White ? part.Tint : tint;
                part.Texture.DrawBackground(spriteBatch, skeletonRenderer, null,
                    partX, partY, partColor, flip, null);
            }
        }
    }

    /// <summary>
    /// Single part in an assembled frame
    /// </summary>
    public class AssembledPart
    {
        public IDXObject Texture { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public string ZLayer { get; set; }
        public int ZIndex { get; set; }
        public IReadOnlyList<string> VisibilityTokens { get; set; } = Array.Empty<string>();
        public int VisibilityPriority { get; set; }
        public bool IsVisible { get; set; } = true;
        public CharacterPart SourcePart { get; set; }
        public Color Tint { get; set; } = Color.White;
        public CharacterPartType PartType { get; set; }
    }

    /// <summary>
    /// Character Assembler - Composites character parts into renderable frames
    /// Equivalent to CAvatar in the MapleStory client
    /// </summary>
    public class CharacterAssembler
    {
        private const int MechanicTamingMobItemId = 1932016;

        private static readonly IReadOnlyDictionary<string, string[]> TamingMobActionAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["stand1"] = new[] { "stand2", "sit" },
                ["stand2"] = new[] { "stand1", "sit" },
                ["walk1"] = new[] { "walk2", "sit", "move" },
                ["walk2"] = new[] { "walk1", "sit", "move" },
                ["jump"] = new[] { "fly", "sit", "move" },
                ["prone"] = new[] { "sit", "stand1" },
                ["swim"] = new[] { "fly", "sit", "move" },
                ["fly"] = new[] { "walk1", "walk2", "sit", "move" },
                ["ladder"] = new[] { "rope", "sit" },
                ["rope"] = new[] { "ladder", "sit" },
                ["alert"] = new[] { "stand1", "stand2", "sit" },
                ["heal"] = new[] { "stand1", "stand2", "sit" },
                ["dead"] = new[] { "sit", "stand1" },
                ["ghost"] = new[] { "sit", "stand1" }
            };

        private readonly CharacterBuild _build;
        private readonly Dictionary<string, AssembledFrame[]> _cachedAnimations = new();
        private string _faceExpressionName = "default";

        // Map point names for alignment
        private const string MAP_NAVEL = "navel";
        private const string MAP_NECK = "neck";
        private const string MAP_HAND = "hand";
        private const string MAP_HAND_MOVE = "handMove";
        private const string MAP_BROW = "brow";
        private const string MAP_EAR = "ear";

        public CharacterAssembler(CharacterBuild build)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
        }

        public string FaceExpressionName
        {
            get => _faceExpressionName;
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value) ? "default" : value;
                if (string.Equals(_faceExpressionName, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _faceExpressionName = normalized;
                ClearCache();
            }
        }

        /// <summary>
        /// Get assembled frames for an action
        /// </summary>
        public AssembledFrame[] GetAnimation(CharacterAction action)
        {
            string actionName = CharacterPart.GetActionString(action);
            return GetAnimation(actionName);
        }

        /// <summary>
        /// Get assembled frames for an action by name
        /// </summary>
        public AssembledFrame[] GetAnimation(string actionName)
        {
            if (_cachedAnimations.TryGetValue(actionName, out var cached))
                return cached;

            var frames = AssembleAnimation(actionName);
            _cachedAnimations[actionName] = frames;
            return frames;
        }

        /// <summary>
        /// Get a single frame at a specific time using ping-pong animation.
        /// Animation plays forward (0,1,2,3) then backward (2,1) and repeats.
        /// This matches MapleStory's character animation behavior.
        /// </summary>
        public AssembledFrame GetFrameAtTime(CharacterAction action, int timeMs)
        {
            var frames = GetAnimation(action);
            if (frames == null || frames.Length == 0)
                return null;

            return GetFrameAtTime(frames, timeMs);
        }

        public AssembledFrame GetFrameAtTime(string actionName, int timeMs)
        {
            var frames = GetAnimation(actionName);
            if (frames == null || frames.Length == 0)
                return null;

            return GetFrameAtTime(frames, timeMs);
        }

        private static AssembledFrame GetFrameAtTime(AssembledFrame[] frames, int timeMs)
        {
            if (frames == null || frames.Length == 0)
                return null;

            // Single frame - no animation needed
            if (frames.Length == 1)
                return frames[0];

            // Calculate forward duration (all frames)
            int forwardDuration = frames.Sum(f => f.Duration);
            if (forwardDuration == 0)
                return frames[0];

            // For ping-pong: 0 1 2 3 2 1 0 1 2 3 2 1 ...
            // Forward phase plays frames 0 to N-1
            // Backward phase plays frames N-2 to 1 (skipping first and last)
            // Calculate backward duration (frames 1 to N-2, i.e., middle frames only)
            int backwardDuration = 0;
            for (int i = frames.Length - 2; i >= 1; i--)
            {
                backwardDuration += frames[i].Duration;
            }

            int pingPongCycleDuration = forwardDuration + backwardDuration;
            if (pingPongCycleDuration == 0)
                return frames[0];

            int time = timeMs % pingPongCycleDuration;

            // Determine if we're in forward or backward phase
            if (time < forwardDuration)
            {
                // Forward phase: find frame normally
                int elapsed = 0;
                foreach (var frame in frames)
                {
                    elapsed += frame.Duration;
                    if (time < elapsed)
                        return frame;
                }
                return frames[^1];
            }
            else
            {
                // Backward phase: find frame in reverse (excluding first and last frame)
                int backwardTime = time - forwardDuration;
                int elapsed = 0;

                // Iterate from frame N-2 down to frame 1
                for (int i = frames.Length - 2; i >= 1; i--)
                {
                    elapsed += frames[i].Duration;
                    if (backwardTime < elapsed)
                        return frames[i];
                }
                return frames[1]; // Fallback to frame 1
            }
        }

        #region Assembly

        private AssembledFrame[] AssembleAnimation(string actionName)
        {
            var frames = new List<AssembledFrame>();
            CharacterPart activeTamingMob = GetActiveTamingMobPart();
            bool suppressBaseAvatar = ShouldSuppressBaseAvatarForTamingMob(activeTamingMob, actionName);

            // Get body animation as the base (determines frame count and timing)
            CharacterAnimation bodyAnim = suppressBaseAvatar
                ? GetPartAnimation(activeTamingMob, actionName)
                : _build.Body?.GetAnimation(CharacterPart.ParseActionString(actionName));
            if (bodyAnim == null || bodyAnim.Frames.Count == 0)
            {
                // Try stand1 as fallback
                bodyAnim = suppressBaseAvatar
                    ? GetPartAnimation(activeTamingMob, CharacterPart.GetActionString(CharacterAction.Stand1))
                    : _build.Body?.GetAnimation(CharacterAction.Stand1);
            }

            if (bodyAnim == null || bodyAnim.Frames.Count == 0)
            {
                // Create empty frame
                frames.Add(new AssembledFrame { Duration = 100 });
                return frames.ToArray();
            }

            // Assemble each frame
            for (int i = 0; i < bodyAnim.Frames.Count; i++)
            {
                var bodyFrame = bodyAnim.Frames[i];
                var assembled = AssembleFrame(actionName, i, bodyFrame);
                frames.Add(assembled);
            }

            return frames.ToArray();
        }

        private AssembledFrame AssembleFrame(string actionName, int frameIndex, CharacterFrame bodyFrame)
        {
            var assembled = new AssembledFrame
            {
                Duration = bodyFrame.Delay
            };

            var parts = new List<AssembledPart>();
            CharacterPart activeTamingMob = GetActiveTamingMobPart();
            bool suppressBaseAvatar = ShouldSuppressBaseAvatarForTamingMob(activeTamingMob, actionName);

            // Body is the anchor point - navel at origin
            Point bodyNavel = bodyFrame.GetMapPoint(MAP_NAVEL);
            Point baseOffset = new Point(-bodyNavel.X, -bodyNavel.Y);

            if (!suppressBaseAvatar)
            {
                // Add body
                AddPart(parts, bodyFrame, baseOffset, CharacterPartType.Body, _build.Body);
            }

            // Add head - connects to body's neck
            Point bodyNeck = bodyFrame.GetMapPoint(MAP_NECK);
            CharacterFrame headFrame = suppressBaseAvatar ? null : GetPartFrame(_build.Head, actionName, frameIndex);

            // Debug: log what we're getting
            if (frameIndex == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Assembler] action={actionName}, frame={frameIndex}");
                System.Diagnostics.Debug.WriteLine($"[Assembler] Body has texture: {bodyFrame.Texture != null}");
                System.Diagnostics.Debug.WriteLine($"[Assembler] Head part: {_build.Head != null}, headFrame: {headFrame != null}");
                System.Diagnostics.Debug.WriteLine($"[Assembler] Face part: {_build.Face != null}");
                System.Diagnostics.Debug.WriteLine($"[Assembler] Hair part: {_build.Hair != null}");
            }

            Point? headOffset = null;
            if (!suppressBaseAvatar && headFrame != null)
            {
                Point headNeck = headFrame.GetMapPoint(MAP_NECK);
                headOffset = new Point(
                    baseOffset.X + bodyNeck.X - headNeck.X,
                    baseOffset.Y + bodyNeck.Y - headNeck.Y);

                AddPart(parts, headFrame, headOffset.Value, CharacterPartType.Head, _build.Head);

                // Add face - relative to head
                var faceFrame = GetFaceFrame(_build.Face, _faceExpressionName, frameIndex);
                if (frameIndex == 0)
                    System.Diagnostics.Debug.WriteLine($"[Assembler] faceFrame: {faceFrame != null}");

                if (faceFrame != null)
                {
                    Point headBrow = headFrame.GetMapPoint(MAP_BROW);
                    Point faceBrow = faceFrame.GetMapPoint(MAP_BROW);
                    Point faceOffset = new Point(
                        headOffset.Value.X + headBrow.X - faceBrow.X,
                        headOffset.Value.Y + headBrow.Y - faceBrow.Y);

                    AddPart(parts, faceFrame, faceOffset, CharacterPartType.Face, _build.Face);
                }

                // Add hair - relative to head
                if (frameIndex == 0 && _build.Hair != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Assembler] Hair has {_build.Hair.Animations.Count} animations:");
                    foreach (var kv in _build.Hair.Animations)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {kv.Key}: {kv.Value.Frames.Count} frames");
                    }
                }
                var hairFrame = GetPartFrame(_build.Hair, actionName, frameIndex);
                if (frameIndex == 0)
                    System.Diagnostics.Debug.WriteLine($"[Assembler] hairFrame: {hairFrame != null}");

                if (hairFrame != null)
                {
                    Point headBrow = headFrame.GetMapPoint(MAP_BROW);
                    Point hairBrow = hairFrame.GetMapPoint(MAP_BROW);
                    Point hairOffset = new Point(
                        headOffset.Value.X + headBrow.X - hairBrow.X,
                        headOffset.Value.Y + headBrow.Y - hairBrow.Y);

                    AddPart(parts, hairFrame, hairOffset, CharacterPartType.Hair, _build.Hair);
                }

                // Add back hair if exists
                if (_build.Hair is HairPart hairPart && hairPart.HasBackHair)
                {
                    var backHairAnim = CharacterPart.FindAnimation(hairPart.BackHairAnimations, actionName);
                    if (backHairAnim != null && frameIndex < backHairAnim.Frames.Count)
                    {
                        var backHairFrame = backHairAnim.Frames[frameIndex];
                        Point headBrow = headFrame.GetMapPoint(MAP_BROW);
                        Point bhBrow = backHairFrame.GetMapPoint(MAP_BROW);
                        Point bhOffset = new Point(
                            headOffset.Value.X + headBrow.X - bhBrow.X,
                            headOffset.Value.Y + headBrow.Y - bhBrow.Y);

                        AddPart(parts, backHairFrame, bhOffset, CharacterPartType.HairBelowBody, _build.Hair, zOverride: 0);
                    }
                }
            }

            // Add equipment
            foreach (var kv in _build.Equipment)
            {
                if (suppressBaseAvatar && kv.Key != EquipSlot.TamingMob)
                {
                    continue;
                }

                var equipFrame = GetPartFrame(kv.Value, actionName, frameIndex);
                if (equipFrame == null) continue;

                Point equipOffset = CalculateEquipOffset(equipFrame, bodyFrame, headFrame, baseOffset, headOffset, kv.Value.Type);
                AddPart(parts, equipFrame, equipOffset, kv.Value.Type, kv.Value);
            }

            // Sort parts by z-index
            parts.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
            ApplyVisibility(parts);
            assembled.Parts = parts;

            // Calculate bounds
            CalculateBounds(assembled);

            return assembled;
        }

        private CharacterPart GetActiveTamingMobPart()
        {
            return _build?.Equipment != null
                && _build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart tamingMobPart)
                ? tamingMobPart
                : null;
        }

        private static bool ShouldSuppressBaseAvatarForTamingMob(CharacterPart tamingMobPart, string actionName)
        {
            return tamingMobPart?.Type == CharacterPartType.TamingMob
                   && tamingMobPart.ItemId == MechanicTamingMobItemId
                   && IsMechanicVehicleAction(actionName);
        }

        private static bool IsMechanicVehicleAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.StartsWith("tank_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("siege_", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("flamethrower", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("rbooster", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("gatlingshot", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("drillrush", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("earthslug", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("rpunch", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("mbooster", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("msummon", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("mRush", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "herbalism_mechanic", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "mining_mechanic", StringComparison.OrdinalIgnoreCase);
        }

        private void AddPart(
            List<AssembledPart> parts,
            CharacterFrame frame,
            Point offset,
            CharacterPartType type,
            CharacterPart sourcePart,
            int? zOverride = null)
        {
            if (frame == null) return;

            // If frame has sub-parts (body, arm, lHand, rHand), render each sub-part separately
            if (frame.HasSubParts)
            {
                foreach (var subPart in frame.SubParts)
                {
                    if (subPart?.Texture == null) continue;

                    // Calculate sub-part offset: base offset + navel offset - origin
                    int subOffsetX = offset.X + subPart.NavelOffset.X - subPart.Origin.X;
                    int subOffsetY = offset.Y + subPart.NavelOffset.Y - subPart.Origin.Y;

                    int zIndex = zOverride ?? ZMapReference.GetZIndex(subPart.Z);
                    parts.Add(new AssembledPart
                    {
                        Texture = subPart.Texture,
                        OffsetX = subOffsetX,
                        OffsetY = subOffsetY,
                        ZLayer = subPart.Z,
                        ZIndex = zIndex,
                        VisibilityTokens = GetVisibilityTokens(sourcePart, subPart.Z),
                        VisibilityPriority = GetVisibilityPriority(sourcePart, subPart.Z, zIndex),
                        SourcePart = sourcePart,
                        PartType = type
                    });
                }
            }
            else if (frame.Texture != null)
            {
                // Single texture (head, face, hair, equipment)
                int zIndex = zOverride ?? ZMapReference.GetZIndex(frame.Z);
                parts.Add(new AssembledPart
                {
                    Texture = frame.Texture,
                    OffsetX = offset.X - frame.Origin.X,
                    OffsetY = offset.Y - frame.Origin.Y,
                    ZLayer = frame.Z,
                    ZIndex = zIndex,
                    VisibilityTokens = GetVisibilityTokens(sourcePart, frame.Z),
                    VisibilityPriority = GetVisibilityPriority(sourcePart, frame.Z, zIndex),
                    SourcePart = sourcePart,
                    PartType = type
                });
            }
        }

        private CharacterFrame GetPartFrame(CharacterPart part, string actionName, int frameIndex)
        {
            if (part == null) return null;

            // Special handling for weapons on ladder/rope - hide weapon if no specific animation
            bool isClimbingAction = actionName == "ladder" || actionName == "rope";
            if (isClimbingAction && part.Type == CharacterPartType.Weapon)
            {
                // Only show weapon if it specifically has a ladder/rope animation
                var climbAnim = part.Animations.GetValueOrDefault(actionName);
                if (climbAnim == null || climbAnim.Frames.Count == 0)
                {
                    return null; // Hide weapon while climbing
                }
            }

            var anim = GetPartAnimation(part, actionName);

            if (anim == null || anim.Frames.Count == 0)
                return null;

            // Wrap frame index
            int idx = frameIndex % anim.Frames.Count;
            return anim.Frames[idx];
        }

        private CharacterAnimation GetPartAnimation(CharacterPart part, string actionName)
        {
            if (part == null)
            {
                return null;
            }

            if (part.Type == CharacterPartType.TamingMob)
            {
                if (part.Animations.TryGetValue(actionName, out CharacterAnimation mountAnimation))
                {
                    return mountAnimation;
                }

                foreach (string alias in GetPartActionAliases(part, actionName))
                {
                    if (part.Animations.TryGetValue(alias, out mountAnimation))
                    {
                        return mountAnimation;
                    }
                }

                return part.GetAnimation(actionName);
            }

            CharacterAnimation animation = part.GetAnimation(actionName);
            if (animation != null)
            {
                return animation;
            }

            foreach (string alias in GetPartActionAliases(part, actionName))
            {
                animation = part.GetAnimation(alias);
                if (animation != null)
                {
                    return animation;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetPartActionAliases(CharacterPart part, string actionName)
        {
            if (part == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (part.Type == CharacterPartType.TamingMob)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (TamingMobActionAliases.TryGetValue(actionName, out string[] aliases))
                {
                    foreach (string alias in aliases)
                    {
                        if (!string.IsNullOrWhiteSpace(alias) && seen.Add(alias))
                        {
                            yield return alias;
                        }
                    }
                }

                if (actionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                    || actionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                    || actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                    || actionName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase))
                {
                    if (seen.Add("walk1"))
                    {
                        yield return "walk1";
                    }

                    if (seen.Add("walk2"))
                    {
                        yield return "walk2";
                    }

                    if (seen.Add("stand1"))
                    {
                        yield return "stand1";
                    }

                    if (seen.Add("move"))
                    {
                        yield return "move";
                    }

                    if (seen.Add("sit"))
                    {
                        yield return "sit";
                    }
                }
            }
        }

        private CharacterFrame GetFaceFrame(FacePart face, string expressionName, int frameIndex)
        {
            if (face == null) return null;

            // Debug: show available expressions
            if (frameIndex == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Assembler] Face has {face.Expressions.Count} expressions:");
                foreach (var kv in face.Expressions)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {kv.Key}: {kv.Value.Frames.Count} frames");
                }
            }

            var expr = face.GetExpression(expressionName) ?? face.GetExpression("default") ?? face.GetExpression("blink");
            if (expr == null || expr.Frames.Count == 0)
            {
                if (frameIndex == 0)
                    System.Diagnostics.Debug.WriteLine($"[Assembler] No valid face expression found!");
                return null;
            }

            int idx = frameIndex % expr.Frames.Count;
            return expr.Frames[idx];
        }

        private Point CalculateEquipOffset(
            CharacterFrame equipFrame,
            CharacterFrame bodyFrame,
            CharacterFrame headFrame,
            Point baseOffset,
            Point? headOffset,
            CharacterPartType type)
        {
            if (TryCalculateHeadEquipOffset(equipFrame, headFrame, headOffset, type, out Point headBasedOffset))
            {
                return headBasedOffset;
            }

            string[] anchorCandidates = type switch
            {
                CharacterPartType.Weapon or CharacterPartType.WeaponOverGlove or CharacterPartType.WeaponOverHand
                    => new[] { MAP_HAND, MAP_HAND_MOVE, MAP_NAVEL },
                CharacterPartType.Glove
                    => new[] { MAP_HAND, MAP_HAND_MOVE, MAP_NAVEL },
                CharacterPartType.Coat or CharacterPartType.Longcoat or CharacterPartType.Pants or CharacterPartType.Shoes or CharacterPartType.Cape
                    => new[] { MAP_NAVEL, MAP_NECK },
                _ => new[] { MAP_NAVEL, MAP_NECK }
            };

            if (TryResolveAnchorOffset(bodyFrame, equipFrame, baseOffset, anchorCandidates, out Point anchorOffset, out string resolvedAnchor))
            {
                if (type == CharacterPartType.Weapon || type == CharacterPartType.WeaponOverGlove || type == CharacterPartType.WeaponOverHand)
                {
                    System.Diagnostics.Debug.WriteLine($"[Assembler] Weapon positioning anchor={resolvedAnchor}, z={equipFrame.Z}");
                }

                return anchorOffset;
            }

            Point bodyAnchor = bodyFrame?.Origin ?? Point.Zero;
            Point equipAnchor = equipFrame?.Origin ?? Point.Zero;
            return new Point(
                baseOffset.X + bodyAnchor.X - equipAnchor.X,
                baseOffset.Y + bodyAnchor.Y - equipAnchor.Y);
        }

        private bool TryCalculateHeadEquipOffset(
            CharacterFrame equipFrame,
            CharacterFrame headFrame,
            Point? headOffset,
            CharacterPartType type,
            out Point offset)
        {
            offset = Point.Zero;

            if (equipFrame == null || headFrame == null || !headOffset.HasValue)
            {
                return false;
            }

            string[] anchorCandidates = type switch
            {
                CharacterPartType.Cap or CharacterPartType.CapOverHair or CharacterPartType.CapBelowAccessory
                    => new[] { MAP_BROW, MAP_EAR, MAP_NECK },
                CharacterPartType.Accessory or CharacterPartType.AccessoryOverHair or CharacterPartType.Face_Accessory
                    => new[] { MAP_BROW, MAP_EAR, MAP_NECK },
                CharacterPartType.Eye_Accessory
                    => new[] { MAP_BROW, MAP_EAR, MAP_NECK },
                CharacterPartType.Earrings
                    => new[] { MAP_EAR, MAP_BROW, MAP_NECK },
                _ => null
            };

            if (anchorCandidates == null || anchorCandidates.Length == 0)
            {
                return false;
            }

            if (!TryResolveAnchorOffset(headFrame, equipFrame, headOffset.Value, anchorCandidates, out offset, out _))
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveAnchorOffset(
            CharacterFrame sourceFrame,
            CharacterFrame targetFrame,
            Point baseOffset,
            IReadOnlyList<string> anchorCandidates,
            out Point offset,
            out string resolvedAnchor)
        {
            offset = Point.Zero;
            resolvedAnchor = null;

            if (sourceFrame == null || targetFrame == null || anchorCandidates == null)
            {
                return false;
            }

            foreach (string anchorName in anchorCandidates)
            {
                if (string.IsNullOrWhiteSpace(anchorName))
                {
                    continue;
                }

                if (!sourceFrame.Map.TryGetValue(anchorName, out Point sourceAnchor) ||
                    !targetFrame.Map.TryGetValue(anchorName, out Point targetAnchor))
                {
                    continue;
                }

                offset = new Point(
                    baseOffset.X + sourceAnchor.X - targetAnchor.X,
                    baseOffset.Y + sourceAnchor.Y - targetAnchor.Y);
                resolvedAnchor = anchorName;
                return true;
            }

            return false;
        }

        private static IReadOnlyList<string> GetVisibilityTokens(CharacterPart sourcePart, string zLayer)
        {
            IReadOnlyList<string> layerTokens = ZMapReference.GetSlotTokens(zLayer);
            IReadOnlyList<string> partTokens = CharacterPart.ParseSlotTokens(sourcePart?.VSlot);

            if (layerTokens.Count == 0)
            {
                return partTokens;
            }

            if (partTokens.Count == 0)
            {
                return layerTokens;
            }

            var intersection = new List<string>();
            foreach (string token in layerTokens)
            {
                if (partTokens.Contains(token))
                {
                    intersection.Add(token);
                }
            }

            return intersection.Count > 0 ? intersection : partTokens;
        }

        private static int GetVisibilityPriority(CharacterPart sourcePart, string zLayer, int zIndex)
        {
            int priority = ZMapReference.GetSlotPriority(sourcePart?.ISlot);
            if (priority != int.MinValue)
            {
                return priority;
            }

            priority = ZMapReference.GetSlotPriority(GetVisibilityTokens(sourcePart, zLayer));
            return priority != int.MinValue ? priority : zIndex;
        }

        private static void ApplyVisibility(List<AssembledPart> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return;
            }

            var owners = new Dictionary<string, AssembledPart>(StringComparer.Ordinal);
            foreach (AssembledPart part in parts)
            {
                part.IsVisible = true;
                if (part.VisibilityTokens == null || part.VisibilityTokens.Count == 0)
                {
                    continue;
                }

                var conflictingOwners = new HashSet<AssembledPart>();
                foreach (string token in part.VisibilityTokens)
                {
                    if (owners.TryGetValue(token, out AssembledPart owner)
                        && owner != null
                        && owner.IsVisible
                        && owner != part
                        && owner.SourcePart != part.SourcePart)
                    {
                        conflictingOwners.Add(owner);
                    }
                }

                if (conflictingOwners.Count == 0)
                {
                    ClaimVisibilityTokens(owners, part);
                    continue;
                }

                bool currentWins = true;
                foreach (AssembledPart owner in conflictingOwners)
                {
                    if (owner.VisibilityPriority >= part.VisibilityPriority)
                    {
                        currentWins = false;
                        break;
                    }
                }

                if (!currentWins)
                {
                    part.IsVisible = false;
                    continue;
                }

                foreach (AssembledPart owner in conflictingOwners)
                {
                    owner.IsVisible = false;
                    ReleaseVisibilityTokens(owners, owner);
                }

                ClaimVisibilityTokens(owners, part);
            }
        }

        private static void ClaimVisibilityTokens(Dictionary<string, AssembledPart> owners, AssembledPart part)
        {
            foreach (string token in part.VisibilityTokens)
            {
                owners[token] = part;
            }
        }

        private static void ReleaseVisibilityTokens(Dictionary<string, AssembledPart> owners, AssembledPart part)
        {
            if (part.VisibilityTokens == null)
            {
                return;
            }

            foreach (string token in part.VisibilityTokens)
            {
                if (owners.TryGetValue(token, out AssembledPart owner) && owner == part)
                {
                    owners.Remove(token);
                }
            }
        }

        private void CalculateBounds(AssembledFrame frame)
        {
            if (frame.Parts.Count == 0)
            {
                frame.Bounds = Rectangle.Empty;
                frame.FeetOffset = 0;
                return;
            }

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var part in frame.Parts)
            {
                if (part.Texture == null || !part.IsVisible) continue;

                var tex = part.Texture;
                int left = part.OffsetX;
                int top = part.OffsetY;
                int right = left + tex.Width;
                int bottom = top + tex.Height;

                minX = Math.Min(minX, left);
                minY = Math.Min(minY, top);
                maxX = Math.Max(maxX, right);
                maxY = Math.Max(maxY, bottom);
            }

            if (minX == int.MaxValue)
            {
                frame.Bounds = Rectangle.Empty;
                frame.FeetOffset = 0;
                return;
            }

            frame.Bounds = new Rectangle(minX, minY, maxX - minX, maxY - minY);
            frame.Origin = new Point(-minX, -minY);

            // Calculate feet offset: distance from navel (at origin 0,0) to feet (bottom of bounds)
            // maxY is the Y coordinate of the feet in local space (relative to navel)
            // This is used to offset rendering so character's feet are at the position instead of navel
            frame.FeetOffset = maxY;
        }

        #endregion

        #region Cache Management

        public void ClearCache()
        {
            _cachedAnimations.Clear();
        }

        public void PreloadAnimation(CharacterAction action)
        {
            GetAnimation(action);
        }

        public void PreloadStandardAnimations()
        {
            PreloadAnimation(CharacterAction.Stand1);
            PreloadAnimation(CharacterAction.Walk1);
            PreloadAnimation(CharacterAction.Jump);
            PreloadAnimation(CharacterAction.Ladder);
            PreloadAnimation(CharacterAction.Rope);
            PreloadAnimation(CharacterAction.Swim);
            PreloadAnimation(CharacterAction.Fly);
            PreloadAnimation(CharacterAction.Prone);
            PreloadAnimation(CharacterAction.Alert);

            // Preload attack animations
            PreloadAnimation(CharacterAction.SwingO1);
            PreloadAnimation(CharacterAction.SwingO2);
            PreloadAnimation(CharacterAction.SwingO3);
            PreloadAnimation(CharacterAction.StabO1);
            PreloadAnimation(CharacterAction.StabO2);
            PreloadAnimation(CharacterAction.Shoot1);
            PreloadAnimation(CharacterAction.ProneStab);
        }

        #endregion

        #region Utility

        /// <summary>
        /// Get attack hitbox for current weapon
        /// </summary>
        public Rectangle GetAttackHitbox(CharacterAction attackAction, int frameIndex, bool facingRight)
        {
            var weapon = _build.GetWeapon();
            if (weapon == null)
            {
                // Default unarmed hitbox
                return new Rectangle(facingRight ? 0 : -50, -30, 50, 60);
            }

            int range = weapon.Range;
            int width = range;
            int height = 60;

            return new Rectangle(
                facingRight ? 10 : -10 - width,
                -40,
                width,
                height);
        }

        /// <summary>
        /// Get character render size for UI purposes
        /// </summary>
        public Point GetRenderSize()
        {
            var frame = GetFrameAtTime(CharacterAction.Stand1, 0);
            if (frame == null)
                return new Point(50, 80);

            return new Point(frame.Bounds.Width, frame.Bounds.Height);
        }

        #endregion
    }
}
