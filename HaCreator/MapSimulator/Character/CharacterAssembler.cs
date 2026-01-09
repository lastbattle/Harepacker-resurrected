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
                if (part.Texture == null) continue;

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
        public Color Tint { get; set; } = Color.White;
        public CharacterPartType PartType { get; set; }
    }

    /// <summary>
    /// Character Assembler - Composites character parts into renderable frames
    /// Equivalent to CAvatar in the MapleStory client
    /// </summary>
    public class CharacterAssembler
    {
        private readonly CharacterBuild _build;
        private readonly Dictionary<string, AssembledFrame[]> _cachedAnimations = new();

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
        /// Get a single frame at a specific time
        /// </summary>
        public AssembledFrame GetFrameAtTime(CharacterAction action, int timeMs)
        {
            var frames = GetAnimation(action);
            if (frames == null || frames.Length == 0)
                return null;

            // Calculate which frame based on time
            int totalDuration = frames.Sum(f => f.Duration);
            if (totalDuration == 0)
                return frames[0];

            int time = timeMs % totalDuration;
            int elapsed = 0;

            foreach (var frame in frames)
            {
                elapsed += frame.Duration;
                if (time < elapsed)
                    return frame;
            }

            return frames[^1];
        }

        #region Assembly

        private AssembledFrame[] AssembleAnimation(string actionName)
        {
            var frames = new List<AssembledFrame>();

            // Get body animation as the base (determines frame count and timing)
            var bodyAnim = _build.Body?.GetAnimation(CharacterPart.ParseActionString(actionName));
            if (bodyAnim == null || bodyAnim.Frames.Count == 0)
            {
                // Try stand1 as fallback
                bodyAnim = _build.Body?.GetAnimation(CharacterAction.Stand1);
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

            // Body is the anchor point - navel at origin
            Point bodyNavel = bodyFrame.GetMapPoint(MAP_NAVEL);
            Point baseOffset = new Point(-bodyNavel.X, -bodyNavel.Y);

            // Add body
            AddPart(parts, bodyFrame, baseOffset, CharacterPartType.Body);

            // Add head - connects to body's neck
            Point bodyNeck = bodyFrame.GetMapPoint(MAP_NECK);
            var headFrame = GetPartFrame(_build.Head, actionName, frameIndex);

            // Debug: log what we're getting
            if (frameIndex == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Assembler] action={actionName}, frame={frameIndex}");
                System.Diagnostics.Debug.WriteLine($"[Assembler] Body has texture: {bodyFrame.Texture != null}");
                System.Diagnostics.Debug.WriteLine($"[Assembler] Head part: {_build.Head != null}, headFrame: {headFrame != null}");
                System.Diagnostics.Debug.WriteLine($"[Assembler] Face part: {_build.Face != null}");
                System.Diagnostics.Debug.WriteLine($"[Assembler] Hair part: {_build.Hair != null}");
            }

            if (headFrame != null)
            {
                Point headNeck = headFrame.GetMapPoint(MAP_NECK);
                Point headOffset = new Point(
                    baseOffset.X + bodyNeck.X - headNeck.X,
                    baseOffset.Y + bodyNeck.Y - headNeck.Y);

                AddPart(parts, headFrame, headOffset, CharacterPartType.Head);

                // Add face - relative to head
                var faceFrame = GetFaceFrame(_build.Face, frameIndex);
                if (frameIndex == 0)
                    System.Diagnostics.Debug.WriteLine($"[Assembler] faceFrame: {faceFrame != null}");

                if (faceFrame != null)
                {
                    Point headBrow = headFrame.GetMapPoint(MAP_BROW);
                    Point faceBrow = faceFrame.GetMapPoint(MAP_BROW);
                    Point faceOffset = new Point(
                        headOffset.X + headBrow.X - faceBrow.X,
                        headOffset.Y + headBrow.Y - faceBrow.Y);

                    AddPart(parts, faceFrame, faceOffset, CharacterPartType.Face);
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
                        headOffset.X + headBrow.X - hairBrow.X,
                        headOffset.Y + headBrow.Y - hairBrow.Y);

                    AddPart(parts, hairFrame, hairOffset, CharacterPartType.Hair);
                }

                // Add back hair if exists
                if (_build.Hair is HairPart hairPart && hairPart.HasBackHair)
                {
                    var backHairAnim = hairPart.BackHairAnimations.GetValueOrDefault(actionName)
                                      ?? hairPart.BackHairAnimations.GetValueOrDefault("stand1");
                    if (backHairAnim != null && frameIndex < backHairAnim.Frames.Count)
                    {
                        var backHairFrame = backHairAnim.Frames[frameIndex];
                        Point headBrow = headFrame.GetMapPoint(MAP_BROW);
                        Point bhBrow = backHairFrame.GetMapPoint(MAP_BROW);
                        Point bhOffset = new Point(
                            headOffset.X + headBrow.X - bhBrow.X,
                            headOffset.Y + headBrow.Y - bhBrow.Y);

                        AddPart(parts, backHairFrame, bhOffset, CharacterPartType.HairBelowBody, zOverride: 0);
                    }
                }
            }

            // Add equipment
            foreach (var kv in _build.Equipment)
            {
                var equipFrame = GetPartFrame(kv.Value, actionName, frameIndex);
                if (equipFrame == null) continue;

                Point equipOffset = CalculateEquipOffset(equipFrame, bodyFrame, baseOffset, kv.Value.Type);
                AddPart(parts, equipFrame, equipOffset, kv.Value.Type);
            }

            // Sort parts by z-index
            parts.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
            assembled.Parts = parts;

            // Calculate bounds
            CalculateBounds(assembled);

            return assembled;
        }

        private void AddPart(List<AssembledPart> parts, CharacterFrame frame, Point offset, CharacterPartType type, int? zOverride = null)
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

                    parts.Add(new AssembledPart
                    {
                        Texture = subPart.Texture,
                        OffsetX = subOffsetX,
                        OffsetY = subOffsetY,
                        ZLayer = subPart.Z,
                        ZIndex = zOverride ?? ZMapReference.GetZIndex(subPart.Z),
                        PartType = type
                    });
                }
            }
            else if (frame.Texture != null)
            {
                // Single texture (head, face, hair, equipment)
                parts.Add(new AssembledPart
                {
                    Texture = frame.Texture,
                    OffsetX = offset.X - frame.Origin.X,
                    OffsetY = offset.Y - frame.Origin.Y,
                    ZLayer = frame.Z,
                    ZIndex = zOverride ?? ZMapReference.GetZIndex(frame.Z),
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

            var anim = part.Animations.GetValueOrDefault(actionName)
                      ?? part.Animations.GetValueOrDefault("stand1");

            if (anim == null || anim.Frames.Count == 0)
                return null;

            // Wrap frame index
            int idx = frameIndex % anim.Frames.Count;
            return anim.Frames[idx];
        }

        private CharacterFrame GetFaceFrame(FacePart face, int frameIndex)
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

            var expr = face.GetExpression("default") ?? face.GetExpression("blink");
            if (expr == null || expr.Frames.Count == 0)
            {
                if (frameIndex == 0)
                    System.Diagnostics.Debug.WriteLine($"[Assembler] No valid face expression found!");
                return null;
            }

            int idx = frameIndex % expr.Frames.Count;
            return expr.Frames[idx];
        }

        private Point CalculateEquipOffset(CharacterFrame equipFrame, CharacterFrame bodyFrame, Point baseOffset, CharacterPartType type)
        {
            // Different equipment types connect at different map points
            string mapPoint = type switch
            {
                CharacterPartType.Weapon or CharacterPartType.WeaponOverGlove or CharacterPartType.WeaponOverHand
                    => MAP_HAND,
                CharacterPartType.Glove => MAP_HAND,
                CharacterPartType.Coat or CharacterPartType.Longcoat or CharacterPartType.Pants or CharacterPartType.Shoes
                    => MAP_NAVEL,
                CharacterPartType.Cape => MAP_NAVEL,
                _ => MAP_NAVEL
            };

            Point bodyAnchor;
            Point equipAnchor;
            string usedBodyAnchor = "";
            string usedEquipAnchor = "";

            // For weapons, try multiple map points in order of preference
            if (type == CharacterPartType.Weapon || type == CharacterPartType.WeaponOverGlove || type == CharacterPartType.WeaponOverHand)
            {
                // Try "hand" first, then "handMove", then "navel"
                if (bodyFrame.Map.ContainsKey(MAP_HAND))
                {
                    bodyAnchor = bodyFrame.Map[MAP_HAND];
                    usedBodyAnchor = "hand";
                }
                else if (bodyFrame.Map.ContainsKey(MAP_HAND_MOVE))
                {
                    bodyAnchor = bodyFrame.Map[MAP_HAND_MOVE];
                    usedBodyAnchor = "handMove";
                }
                else if (bodyFrame.Map.ContainsKey(MAP_NAVEL))
                {
                    // Use navel as fallback but offset for approximate hand position
                    var navel = bodyFrame.Map[MAP_NAVEL];
                    bodyAnchor = new Point(navel.X + 10, navel.Y - 15);
                    usedBodyAnchor = "navel+offset";
                }
                else
                {
                    bodyAnchor = bodyFrame.Origin;
                    usedBodyAnchor = "origin";
                }

                // For weapon anchor, use "hand" or "handMove"
                if (equipFrame.Map.ContainsKey(MAP_HAND))
                {
                    equipAnchor = equipFrame.Map[MAP_HAND];
                    usedEquipAnchor = "hand";
                }
                else if (equipFrame.Map.ContainsKey(MAP_HAND_MOVE))
                {
                    equipAnchor = equipFrame.Map[MAP_HAND_MOVE];
                    usedEquipAnchor = "handMove";
                }
                else
                {
                    equipAnchor = equipFrame.Origin;
                    usedEquipAnchor = "origin";
                }

                // Debug: Log weapon positioning info (once per animation load)
                System.Diagnostics.Debug.WriteLine($"[Assembler] Weapon positioning: body.{usedBodyAnchor}={bodyAnchor}, weapon.{usedEquipAnchor}={equipAnchor}, z={equipFrame.Z}");
            }
            else
            {
                bodyAnchor = bodyFrame.GetMapPoint(mapPoint);
                equipAnchor = equipFrame.GetMapPoint(mapPoint);
            }

            return new Point(
                baseOffset.X + bodyAnchor.X - equipAnchor.X,
                baseOffset.Y + bodyAnchor.Y - equipAnchor.Y);
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
                if (part.Texture == null) continue;

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
