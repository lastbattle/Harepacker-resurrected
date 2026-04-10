using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character.Skills;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace HaCreator.MapSimulator.Character
{
    public enum AvatarRenderLayer
    {
        UnderCharacter = 0,
        OverCharacter = 1,
        UnderFace = 2,
        Face = 3,
        OverFace = 4
    }

    /// <summary>
    /// Assembled frame ready for rendering - contains all parts positioned correctly
    /// </summary>
    public class AssembledFrame
    {
        public List<AssembledPart> Parts { get; set; } = new();
        public IReadOnlyList<AssembledPart>[] AvatarRenderLayers { get; set; } = CreateEmptyAvatarRenderLayers();
        public Rectangle Bounds { get; set; }
        public Point Origin { get; set; }
        public int Duration { get; set; }
        public Dictionary<string, Point> MapPoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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

        internal static IReadOnlyList<AssembledPart>[] CreateEmptyAvatarRenderLayers()
        {
            return new IReadOnlyList<AssembledPart>[5]
            {
                Array.Empty<AssembledPart>(),
                Array.Empty<AssembledPart>(),
                Array.Empty<AssembledPart>(),
                Array.Empty<AssembledPart>(),
                Array.Empty<AssembledPart>()
            };
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
        public PortableChairLayer SourcePortableChairLayer { get; set; }
        public AvatarRenderLayer RenderLayer { get; set; } = AvatarRenderLayer.OverCharacter;
    }

    /// <summary>
    /// Character Assembler - Composites character parts into renderable frames
    /// Equivalent to CAvatar in the MapleStory client
    /// </summary>
    public class CharacterAssembler
    {
        private const int MechanicTamingMobItemId = 1932016;
        private const int PortableChairBackZ = -100;
        private const int PortableChairFrontZ = 900;
        private static readonly HashSet<string> UnderCharacterZLayers = new(StringComparer.Ordinal)
        {
            "backHair",
            "backHairOverCape",
            "backWing",
            "cape",
            "shield",
            "shieldOverBody",
            "weaponBelowBody",
            "backBody"
        };

        private static readonly IReadOnlyDictionary<string, string[]> TamingMobActionAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["sit"] = new[] { "stand1", "stand2" },
                ["stand1"] = new[] { "stand2", "sit" },
                ["stand2"] = new[] { "stand1", "sit" },
                ["walk1"] = new[] { "walk2", "sit", "move" },
                ["walk2"] = new[] { "walk1", "sit", "move" },
                ["jump"] = new[] { "fly", "sit", "move" },
                ["prone"] = new[] { "sit", "stand1" },
                ["swim"] = new[] { "fly", "sit", "move" },
                ["fly"] = new[] { "walk1", "walk2", "sit", "move" },
                ["ladder"] = new[] { "ladder2", "rope2", "rope", "sit" },
                ["rope"] = new[] { "rope2", "ladder2", "ladder", "sit" },
                ["alert"] = new[] { "stand1", "stand2", "sit" },
                ["heal"] = new[] { "stand1", "stand2", "sit" },
                ["dead"] = new[] { "sit", "stand1" },
                ["ghost"] = new[] { "sit", "stand1" }
            };

        private readonly CharacterBuild _build;
        private readonly Dictionary<string, AssembledFrame[]> _cachedAnimations = new();
        private string _faceExpressionName = "default";
        private CharacterPart _overrideAvatarPart;
        private CharacterPart _overrideTamingMobPart;
        private int _preparedActionSpeedDegree = 6;
        private int _preparedWalkSpeed = 100;
        private bool _heldActionFrameDelay;
        private bool _currentFacingRight = true;

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

        public CharacterPart OverrideTamingMobPart
        {
            get => _overrideTamingMobPart;
            set
            {
                if (ReferenceEquals(_overrideTamingMobPart, value))
                {
                    return;
                }

                _overrideTamingMobPart = value;
                ClearCache();
            }
        }

        public CharacterPart OverrideAvatarPart
        {
            get => _overrideAvatarPart;
            set
            {
                if (ReferenceEquals(_overrideAvatarPart, value))
                {
                    return;
                }

                _overrideAvatarPart = value;
                ClearCache();
            }
        }

        internal int PreparedActionSpeedDegree
        {
            get => _preparedActionSpeedDegree;
            set => _preparedActionSpeedDegree = value;
        }

        internal int PreparedWalkSpeed
        {
            get => _preparedWalkSpeed;
            set => _preparedWalkSpeed = value;
        }

        internal bool HeldActionFrameDelay
        {
            get => _heldActionFrameDelay;
            set => _heldActionFrameDelay = value;
        }

        internal bool CurrentFacingRight
        {
            get => _currentFacingRight;
            set => _currentFacingRight = value;
        }

        public bool TryGetFaceLookDuration(string expressionName, out int durationMs)
        {
            durationMs = 0;

            FaceLookEntry faceLook = GetFaceLook(expressionName);
            if (faceLook == null || faceLook.TotalDuration <= 0)
            {
                return false;
            }

            durationMs = faceLook.TotalDuration;
            return true;
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
            string cacheKey = BuildAnimationCacheKey(actionName);
            if (_cachedAnimations.TryGetValue(cacheKey, out var cached))
                return cached;

            var frames = AssembleAnimation(actionName);
            _cachedAnimations[cacheKey] = frames;
            return frames;
        }

        internal int ResolveClientActionLayerDuration(string actionName)
        {
            int bodyDuration = ResolveClientCharacterActionLayerDuration(actionName);
            return TryResolveClientTamingMobActionLayerDuration(actionName, out int tamingMobDuration)
                ? Math.Max(bodyDuration, tamingMobDuration)
                : bodyDuration;
        }

        internal int ResolveClientCharacterActionLayerDuration(string actionName)
        {
            return ResolveAssembledAnimationDuration(GetAnimation(actionName));
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

            return ResolveDynamicPortableChairFrame(GetFrameAtTime(frames, timeMs), timeMs);
        }

        public AssembledFrame GetFrameAtTime(string actionName, int timeMs)
        {
            AssembledFrame mountedFrame = TryResolveMountedFrameAtTime(actionName, timeMs);
            if (mountedFrame != null)
            {
                return ResolveDynamicPortableChairFrame(mountedFrame, timeMs);
            }

            var frames = GetAnimation(actionName);
            if (frames == null || frames.Length == 0)
                return null;

            return ResolveDynamicPortableChairFrame(GetFrameAtTime(frames, timeMs), timeMs);
        }

        internal AssembledFrame GetMountedFrameAtTime(
            string bodyActionName,
            int bodyTimeMs,
            string tamingMobActionName,
            int tamingMobTimeMs)
        {
            AssembledFrame mountedFrame = TryResolveMountedFrameAtTime(
                bodyActionName,
                bodyTimeMs,
                tamingMobActionName,
                tamingMobTimeMs);
            return mountedFrame == null
                ? null
                : ResolveDynamicPortableChairFrame(mountedFrame, tamingMobTimeMs);
        }

        public int GetFrameIndexAtTime(string actionName, int timeMs)
        {
            var frames = GetAnimation(actionName);
            if (frames == null || frames.Length == 0)
            {
                return -1;
            }

            return GetFrameIndexAtTime(frames, timeMs);
        }

        public bool TryGetFrameTimingAtTime(string actionName, int timeMs, out int frameIndex, out int frameElapsedMs)
        {
            var frames = GetAnimation(actionName);
            return TryGetFrameTimingAtTime(frames, timeMs, out frameIndex, out frameElapsedMs);
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

        private static int ResolveAssembledAnimationDuration(AssembledFrame[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                return 0;
            }

            long duration = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                duration += Math.Max(0, frames[i]?.Duration ?? 0);
                if (duration >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }

            return (int)duration;
        }

        private static int GetFrameIndexAtTime(AssembledFrame[] frames, int timeMs)
        {
            if (frames == null || frames.Length == 0)
            {
                return -1;
            }

            if (frames.Length == 1)
            {
                return 0;
            }

            int forwardDuration = frames.Sum(f => f.Duration);
            if (forwardDuration <= 0)
            {
                return 0;
            }

            int backwardDuration = 0;
            for (int i = frames.Length - 2; i >= 1; i--)
            {
                backwardDuration += frames[i].Duration;
            }

            int cycleDuration = forwardDuration + backwardDuration;
            if (cycleDuration <= 0)
            {
                return 0;
            }

            int time = timeMs % cycleDuration;
            if (time < forwardDuration)
            {
                int elapsed = 0;
                for (int i = 0; i < frames.Length; i++)
                {
                    elapsed += frames[i].Duration;
                    if (time < elapsed)
                    {
                        return i;
                    }
                }

                return frames.Length - 1;
            }

            int backwardTime = time - forwardDuration;
            int backwardElapsed = 0;
            for (int i = frames.Length - 2; i >= 1; i--)
            {
                backwardElapsed += frames[i].Duration;
                if (backwardTime < backwardElapsed)
                {
                    return i;
                }
            }

            return Math.Min(1, frames.Length - 1);
        }

        private static bool TryGetFrameTimingAtTime(
            AssembledFrame[] frames,
            int timeMs,
            out int frameIndex,
            out int frameElapsedMs)
        {
            frameIndex = -1;
            frameElapsedMs = 0;

            if (frames == null || frames.Length == 0)
            {
                return false;
            }

            if (frames.Length == 1)
            {
                frameIndex = 0;
                frameElapsedMs = Math.Max(0, timeMs);
                return true;
            }

            int forwardDuration = frames.Sum(f => f.Duration);
            if (forwardDuration <= 0)
            {
                frameIndex = 0;
                return true;
            }

            int backwardDuration = 0;
            for (int i = frames.Length - 2; i >= 1; i--)
            {
                backwardDuration += frames[i].Duration;
            }

            int cycleDuration = forwardDuration + backwardDuration;
            if (cycleDuration <= 0)
            {
                frameIndex = 0;
                return true;
            }

            int time = timeMs % cycleDuration;
            if (time < forwardDuration)
            {
                int elapsed = 0;
                for (int i = 0; i < frames.Length; i++)
                {
                    int frameDuration = Math.Max(0, frames[i]?.Duration ?? 0);
                    if (time < elapsed + frameDuration || i == frames.Length - 1)
                    {
                        frameIndex = i;
                        frameElapsedMs = Math.Max(0, time - elapsed);
                        return true;
                    }

                    elapsed += frameDuration;
                }
            }
            else
            {
                int backwardTime = time - forwardDuration;
                int elapsed = 0;
                for (int i = frames.Length - 2; i >= 1; i--)
                {
                    int frameDuration = Math.Max(0, frames[i]?.Duration ?? 0);
                    if (backwardTime < elapsed + frameDuration || i == 1)
                    {
                        frameIndex = i;
                        frameElapsedMs = Math.Max(0, backwardTime - elapsed);
                        return true;
                    }

                    elapsed += frameDuration;
                }
            }

            frameIndex = 0;
            return true;
        }

        private AssembledFrame ResolveDynamicPortableChairFrame(AssembledFrame frame, int timeMs)
        {
            if (frame?.Parts == null || frame.Parts.Count == 0 || _build?.ActivePortableChair == null)
            {
                return frame;
            }

            bool hasPortableChairParts = false;
            for (int i = 0; i < frame.Parts.Count; i++)
            {
                if (frame.Parts[i].PartType == CharacterPartType.PortableChair
                    && frame.Parts[i].SourcePortableChairLayer?.Animation != null)
                {
                    hasPortableChairParts = true;
                    break;
                }
            }

            if (!hasPortableChairParts)
            {
                return frame;
            }

            var resolvedParts = new List<AssembledPart>(frame.Parts.Count);
            for (int i = 0; i < frame.Parts.Count; i++)
            {
                AssembledPart part = frame.Parts[i];
                if (part.PartType != CharacterPartType.PortableChair || part.SourcePortableChairLayer?.Animation == null)
                {
                    resolvedParts.Add(part);
                    continue;
                }

                CharacterFrame chairFrame = part.SourcePortableChairLayer.Animation.GetFrameAtTime(timeMs, out _);
                if (chairFrame?.Texture == null)
                {
                    continue;
                }

                resolvedParts.Add(new AssembledPart
                {
                    Texture = chairFrame.Texture,
                    OffsetX = -chairFrame.Origin.X,
                    OffsetY = -chairFrame.Origin.Y,
                    ZLayer = part.ZLayer,
                    ZIndex = part.ZIndex,
                    VisibilityTokens = part.VisibilityTokens,
                    VisibilityPriority = part.VisibilityPriority,
                    IsVisible = part.IsVisible,
                    SourcePart = part.SourcePart,
                    Tint = part.Tint,
                    PartType = part.PartType,
                    SourcePortableChairLayer = part.SourcePortableChairLayer,
                    RenderLayer = part.RenderLayer
                });
            }

            return new AssembledFrame
            {
                Parts = resolvedParts,
                AvatarRenderLayers = BuildAvatarRenderLayers(resolvedParts),
                Bounds = frame.Bounds,
                Origin = frame.Origin,
                Duration = frame.Duration,
                MapPoints = frame.MapPoints,
                FeetOffset = frame.FeetOffset
            };
        }

        private AssembledFrame TryResolveMountedFrameAtTime(string actionName, int timeMs)
        {
            return TryResolveMountedFrameAtTime(actionName, timeMs, actionName, timeMs);
        }

        private AssembledFrame TryResolveMountedFrameAtTime(
            string bodyActionName,
            int bodyTimeMs,
            string tamingMobActionName,
            int tamingMobTimeMs)
        {
            if (_overrideAvatarPart != null)
            {
                return null;
            }

            string preparedActionName = AvatarActionLayerCoordinator.ResolvePreparedActionName(bodyActionName, isMorphAvatar: false);
            CharacterLoader.CharacterActionMergeInput mergeInput =
                CharacterLoader.PrepareActionMergeInput(_build, preparedActionName, GetActiveTamingMobPart());
            string resolvedActionName = mergeInput?.ActionName ?? preparedActionName;
            CharacterPart activeTamingMob = mergeInput?.ActiveTamingMobPart;
            if (activeTamingMob?.Slot != EquipSlot.TamingMob
                || ShouldSuppressBaseAvatarForTamingMob(activeTamingMob, resolvedActionName))
            {
                return null;
            }

            CharacterAnimation bodyAnimation = _build.Body?.GetAnimation(CharacterPart.ParseActionString(resolvedActionName))
                                           ?? _build.Body?.GetAnimation(CharacterAction.Stand1);
            string preparedTamingMobActionName = AvatarActionLayerCoordinator.ResolvePreparedActionName(
                tamingMobActionName,
                isMorphAvatar: false);
            CharacterAnimation tamingMobAnimation = GetPartAnimation(activeTamingMob, preparedTamingMobActionName)
                                                ?? GetPartAnimation(activeTamingMob, resolvedActionName);
            if (bodyAnimation?.Frames?.Count <= 0 || tamingMobAnimation?.Frames?.Count <= 0)
            {
                return null;
            }

            CharacterFrame bodyFrame = bodyAnimation.GetFrameAtTime(Math.Max(0, bodyTimeMs), out int bodyFrameIndex);
            CharacterFrame tamingMobFrame = tamingMobAnimation.GetFrameAtTime(Math.Max(0, tamingMobTimeMs), out _);
            if (bodyFrame == null || tamingMobFrame == null)
            {
                return null;
            }

            return AssembleFrame(
                mergeInput,
                bodyFrameIndex,
                bodyFrame,
                tamingMobFrame);
        }

        internal bool TryResolveClientTamingMobActionLayerDuration(string actionName, out int durationMs)
        {
            durationMs = 0;
            if (_overrideAvatarPart != null)
            {
                return false;
            }

            string preparedActionName = AvatarActionLayerCoordinator.ResolvePreparedActionName(actionName, isMorphAvatar: false);
            CharacterLoader.CharacterActionMergeInput mergeInput =
                CharacterLoader.PrepareActionMergeInput(_build, preparedActionName, GetActiveTamingMobPart());
            string resolvedActionName = mergeInput?.ActionName ?? preparedActionName;
            CharacterPart activeTamingMob = mergeInput?.ActiveTamingMobPart;
            if (!IsTamingMobRenderOwnershipAction(activeTamingMob, resolvedActionName))
            {
                return false;
            }

            CharacterAnimation tamingMobAnimation = GetPartAnimation(activeTamingMob, resolvedActionName);
            durationMs = AvatarActionLayerCoordinator.ResolvePreparedAnimationDuration(
                tamingMobAnimation,
                resolvedActionName,
                PreparedActionSpeedDegree,
                PreparedWalkSpeed,
                HeldActionFrameDelay);
            return durationMs > 0;
        }

        #region Assembly

        private AssembledFrame[] AssembleAnimation(string actionName)
        {
            if (_overrideAvatarPart != null)
            {
                bool overrideIsMorphAvatar = _overrideAvatarPart.Type == CharacterPartType.Morph;
                string overridePreparedActionName = AvatarActionLayerCoordinator.ResolvePreparedActionName(actionName, overrideIsMorphAvatar);
                return AssembleStandaloneAnimation(_overrideAvatarPart, overridePreparedActionName);
            }

            var frames = new List<AssembledFrame>();
            bool isMorphAvatar = false;
            string preparedActionName = AvatarActionLayerCoordinator.ResolvePreparedActionName(actionName, isMorphAvatar);
            CharacterLoader.CharacterActionMergeInput mergeInput =
                CharacterLoader.PrepareActionMergeInput(_build, preparedActionName, GetActiveTamingMobPart());
            string resolvedActionName = mergeInput.ActionName;
            CharacterPart activeTamingMob = mergeInput.ActiveTamingMobPart;
            bool suppressBaseAvatar = ShouldSuppressBaseAvatarForTamingMob(activeTamingMob, resolvedActionName);

            // Get body animation as the base (determines frame count and timing)
            CharacterAnimation bodyAnim = suppressBaseAvatar
                ? GetPartAnimation(activeTamingMob, resolvedActionName)
                : _build.Body?.GetAnimation(CharacterPart.ParseActionString(resolvedActionName));
            if (bodyAnim == null || bodyAnim.Frames.Count == 0)
            {
                // Strict vehicle-owned requests should fail closed instead of inheriting locomotion frames.
                bodyAnim = suppressBaseAvatar && IsExactMechanicVehicleActionRequired(activeTamingMob, resolvedActionName)
                    ? null
                    : suppressBaseAvatar
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
                var assembled = AssembleFrame(mergeInput, i, bodyFrame);
                frames.Add(assembled);
            }

            return frames.ToArray();
        }

        private AssembledFrame[] AssembleStandaloneAnimation(CharacterPart part, string actionName)
        {
            bool requireExactMechanicVehicleAction = IsExactMechanicVehicleActionRequired(part, actionName);
            CharacterAnimation animation = GetPartAnimation(part, actionName);
            if (!requireExactMechanicVehicleAction)
            {
                animation ??= GetPartAnimation(part, CharacterPart.GetActionString(CharacterAction.Stand1))
                    ?? part?.Animations?.Values.FirstOrDefault(candidate => candidate?.Frames?.Count > 0);
            }

            if (animation == null || animation.Frames.Count == 0)
            {
                return new[] { new AssembledFrame { Duration = 100 } };
            }

            var frames = new AssembledFrame[animation.Frames.Count];
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                frames[i] = AssembleStandaloneFrame(part, actionName, animation.Frames[i], i);
            }

            return frames;
        }

        private AssembledFrame AssembleStandaloneFrame(CharacterPart part, string actionName, CharacterFrame frame, int frameIndex)
        {
            var assembled = new AssembledFrame
            {
                Duration = frame == null
                    ? 100
                    : AvatarActionLayerCoordinator.ResolvePreparedFrameDelay(
                        actionName,
                        frame.Delay,
                        PreparedActionSpeedDegree,
                        PreparedWalkSpeed,
                        HeldActionFrameDelay,
                        frameIndex,
                        isMorphAvatar: part?.Type == CharacterPartType.Morph,
                        isSuperManMorph: part?.IsSuperManMorph == true)
            };

            if (frame == null)
            {
                return assembled;
            }

            foreach (KeyValuePair<string, Point> mapPoint in frame.Map)
            {
                assembled.MapPoints[mapPoint.Key] = new Point(
                    mapPoint.Value.X - frame.Origin.X,
                    mapPoint.Value.Y - frame.Origin.Y);
            }

            var parts = new List<AssembledPart>();
            AddPart(parts, frame, Point.Zero, part.Type, part);
            parts.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
            ApplyVisibility(parts);
            assembled.Parts = parts;
            assembled.AvatarRenderLayers = BuildAvatarRenderLayers(parts);
            CalculateBounds(assembled);
            return assembled;
        }

        private AssembledFrame AssembleFrame(
            CharacterLoader.CharacterActionMergeInput mergeInput,
            int frameIndex,
            CharacterFrame bodyFrame,
            CharacterFrame tamingMobFrameOverride = null)
        {
            string actionName = mergeInput?.ActionName ?? CharacterPart.GetActionString(CharacterAction.Stand1);
            var assembled = new AssembledFrame
            {
                Duration = AvatarActionLayerCoordinator.ResolvePreparedFrameDelay(
                    actionName,
                    bodyFrame.Delay,
                    PreparedActionSpeedDegree,
                    PreparedWalkSpeed,
                    HeldActionFrameDelay,
                    frameIndex,
                    isMorphAvatar: false,
                    isSuperManMorph: false)
            };

            var parts = new List<AssembledPart>();
            CharacterPart activeTamingMob = mergeInput?.ActiveTamingMobPart;
            bool suppressBaseAvatar = ShouldSuppressBaseAvatarForTamingMob(activeTamingMob, actionName);

            // Body is the anchor point - navel at origin
            Point bodyNavel = bodyFrame.GetMapPoint(MAP_NAVEL);
            Point baseOffset = new Point(-bodyNavel.X, -bodyNavel.Y);
            foreach (KeyValuePair<string, Point> mapPoint in bodyFrame.Map)
            {
                assembled.MapPoints[mapPoint.Key] = new Point(
                    baseOffset.X + mapPoint.Value.X,
                    baseOffset.Y + mapPoint.Value.Y);
            }

            if (!suppressBaseAvatar)
            {
                // Add body
                AddPart(parts, bodyFrame, baseOffset, CharacterPartType.Body, _build.Body);
            }

            // Add head - connects to body's neck
            Point bodyNeck = bodyFrame.GetMapPoint(MAP_NECK);
            CharacterFrame headFrame = suppressBaseAvatar ? null : GetPartFrame(_build.Head, actionName, frameIndex);

            Point? headOffset = null;
            CharacterPart visibleFaceAccessory = !suppressBaseAvatar ? GetVisibleFaceAccessoryPart() : null;
            FaceLookEntry faceLook = !suppressBaseAvatar ? GetFaceLook(_faceExpressionName, visibleFaceAccessory) : null;
            if (!suppressBaseAvatar && headFrame != null)
            {
                Point headNeck = headFrame.GetMapPoint(MAP_NECK);
                headOffset = new Point(
                    baseOffset.X + bodyNeck.X - headNeck.X,
                    baseOffset.Y + bodyNeck.Y - headNeck.Y);

                AddPart(parts, headFrame, headOffset.Value, CharacterPartType.Head, _build.Head);
                foreach (KeyValuePair<string, Point> mapPoint in headFrame.Map)
                {
                    assembled.MapPoints[mapPoint.Key] = new Point(
                        headOffset.Value.X + mapPoint.Value.X,
                        headOffset.Value.Y + mapPoint.Value.Y);
                }

                // Add face - relative to head
                FaceLookFrame faceLookFrame = GetFaceLookFrame(faceLook, frameIndex);
                bool renderedCompositeFace = faceLookFrame?.CompositeFrame?.Texture != null;
                CharacterFrame renderedFaceFrame = renderedCompositeFace
                    ? faceLookFrame.CompositeFrame
                    : faceLookFrame?.FaceFrame;
                if (renderedFaceFrame != null)
                {
                    Point headBrow = headFrame.GetMapPoint(MAP_BROW);
                    Point faceBrow = renderedFaceFrame.GetMapPoint(MAP_BROW);
                    Point faceOffset = new Point(
                        headOffset.Value.X + headBrow.X - faceBrow.X,
                        headOffset.Value.Y + headBrow.Y - faceBrow.Y);

                    AddPart(parts, renderedFaceFrame, faceOffset, CharacterPartType.Face, _build.Face);
                }

                if (!renderedCompositeFace
                    && faceLookFrame?.AccessoryFrame != null
                    && visibleFaceAccessory != null)
                {
                    Point headBrow = headFrame.GetMapPoint(MAP_BROW);
                    Point accessoryBrow = faceLookFrame.AccessoryFrame.GetMapPoint(MAP_BROW);
                    Point accessoryOffset = new Point(
                        headOffset.Value.X + headBrow.X - accessoryBrow.X,
                        headOffset.Value.Y + headBrow.Y - accessoryBrow.Y);

                    AddPart(parts, faceLookFrame.AccessoryFrame, accessoryOffset, CharacterPartType.Face_Accessory, visibleFaceAccessory);
                }

                // Add hair - relative to head
                var hairFrame = GetPartFrame(_build.Hair, actionName, frameIndex);
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
            bool renderedWeaponLane = false;
            IReadOnlyDictionary<EquipSlot, CharacterPart> mergedEquipment = mergeInput?.Equipment ?? _build.Equipment;
            foreach (var kv in mergedEquipment)
            {
                if (suppressBaseAvatar && kv.Key != EquipSlot.TamingMob)
                {
                    continue;
                }

                if (kv.Key == EquipSlot.FaceAccessory && faceLook?.HasAccessory == true)
                {
                    continue;
                }

                CharacterPart renderPart = ResolveDisplayedEquipmentPart(
                    kv.Key,
                    kv.Value,
                    mergeInput?.WeaponSticker,
                    actionName,
                    frameIndex);
                CharacterFrame equipFrame = kv.Key == EquipSlot.TamingMob && tamingMobFrameOverride != null
                    ? tamingMobFrameOverride
                    : GetPartFrame(renderPart, actionName, frameIndex);
                if (equipFrame == null) continue;

                Point equipOffset = CalculateEquipOffset(equipFrame, bodyFrame, headFrame, baseOffset, headOffset, renderPart.Type);
                AddPart(parts, equipFrame, equipOffset, renderPart.Type, renderPart);
                if (kv.Key == EquipSlot.Weapon)
                {
                    renderedWeaponLane = true;
                }
            }

            if (!suppressBaseAvatar && !renderedWeaponLane && mergeInput?.WeaponSticker != null)
            {
                CharacterFrame stickerFrame = GetPartFrame(mergeInput.WeaponSticker, actionName, frameIndex);
                if (stickerFrame != null)
                {
                    Point stickerOffset = CalculateEquipOffset(
                        stickerFrame,
                        bodyFrame,
                        headFrame,
                        baseOffset,
                        headOffset,
                        mergeInput.WeaponSticker.Type);
                    AddPart(parts, stickerFrame, stickerOffset, mergeInput.WeaponSticker.Type, mergeInput.WeaponSticker);
                }
            }

            if (!suppressBaseAvatar && IsPortableChairAction(actionName))
            {
                AddPortableChairLayers(parts, frameIndex);
            }

            assembled.Parts = parts;

            if (!suppressBaseAvatar
                && activeTamingMob?.Slot == EquipSlot.TamingMob)
            {
                CharacterFrame tamingMobFrame = tamingMobFrameOverride ?? GetPartFrame(activeTamingMob, actionName, frameIndex);
                if (tamingMobFrame != null)
                {
                    AvatarActionLayerCoordinator.ApplyMountedOriginRelocation(
                        assembled,
                        actionName,
                        bodyFrame,
                        tamingMobFrame,
                        CurrentFacingRight);
                }
            }

            // Sort parts by z-index
            parts.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
            ApplyVisibility(parts);
            assembled.AvatarRenderLayers = BuildAvatarRenderLayers(parts);

            // Calculate bounds
            CalculateBounds(assembled);

            return assembled;
        }

        private string BuildAnimationCacheKey(string actionName)
        {
            return string.Concat(
                actionName ?? string.Empty,
                "|spd:", PreparedActionSpeedDegree.ToString(),
                "|walk:", PreparedWalkSpeed.ToString(),
                "|held:", HeldActionFrameDelay ? "1" : "0",
                "|dir:", CurrentFacingRight ? "R" : "L");
        }

        private CharacterPart GetActiveTamingMobPart()
        {
            if (_overrideTamingMobPart?.Slot == EquipSlot.TamingMob)
            {
                return _overrideTamingMobPart;
            }

            return _build?.Equipment != null
                && _build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart tamingMobPart)
                ? tamingMobPart
                : null;
        }

        private CharacterPart ResolveDisplayedEquipmentPart(
            EquipSlot slot,
            CharacterPart equippedPart,
            CharacterPart weaponSticker,
            string actionName,
            int frameIndex)
        {
            if (slot != EquipSlot.Weapon || weaponSticker == null)
            {
                return equippedPart;
            }

            CharacterFrame stickerFrame = GetPartFrame(weaponSticker, actionName, frameIndex);
            return stickerFrame != null ? weaponSticker : equippedPart;
        }

        private void AddPortableChairLayers(List<AssembledPart> parts, int frameIndex)
        {
            PortableChair chair = _build?.ActivePortableChair;
            if (chair == null)
            {
                return;
            }

            foreach (PortableChairLayer layer in EnumerateActivePortableChairLayers(chair))
            {
                CharacterFrame frame = GetPortableChairFrame(layer, frameIndex);
                if (frame == null)
                {
                    continue;
                }

                AddPart(
                    parts,
                    frame,
                    Point.Zero,
                    CharacterPartType.PortableChair,
                    sourcePart: null,
                    zOverride: GetPortableChairZIndex(layer.RelativeZ),
                    sourcePortableChairLayer: layer);
            }
        }

        private IEnumerable<PortableChairLayer> EnumerateActivePortableChairLayers(PortableChair chair)
        {
            if (chair?.Layers != null)
            {
                for (int i = 0; i < chair.Layers.Count; i++)
                {
                    PortableChairLayer layer = chair.Layers[i];
                    if (layer != null)
                    {
                        yield return layer;
                    }
                }
            }

            if (chair?.ExpressionLayers == null || chair.ExpressionLayers.Count == 0)
            {
                yield break;
            }

            if (!chair.ExpressionLayers.TryGetValue(_faceExpressionName, out List<PortableChairLayer> expressionLayers)
                || expressionLayers == null)
            {
                yield break;
            }

            for (int i = 0; i < expressionLayers.Count; i++)
            {
                PortableChairLayer layer = expressionLayers[i];
                if (layer != null)
                {
                    yield return layer;
                }
            }
        }

        private static CharacterFrame GetPortableChairFrame(PortableChairLayer layer, int frameIndex)
        {
            if (layer?.Animation?.Frames == null || layer.Animation.Frames.Count == 0)
            {
                return null;
            }

            int resolvedIndex = Math.Abs(frameIndex) % layer.Animation.Frames.Count;
            return layer.Animation.Frames[resolvedIndex];
        }

        private static int GetPortableChairZIndex(int relativeZ)
        {
            return relativeZ > 0
                ? PortableChairFrontZ + relativeZ
                : PortableChairBackZ + relativeZ;
        }

        private static bool IsPortableChairAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && actionName.StartsWith("sit", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldSuppressBaseAvatarForState(CharacterBuild build, string actionName)
        {
            if (build?.Equipment == null
                || !build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart tamingMobPart))
            {
                return false;
            }

            return ShouldSuppressBaseAvatarForTamingMob(tamingMobPart, actionName);
        }

        internal static bool SupportsTamingMobAction(CharacterPart tamingMobPart, string actionName)
        {
            if (tamingMobPart?.Type != CharacterPartType.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (tamingMobPart.TamingMobActionFrameOwner?.SupportsAction(tamingMobPart, actionName) == true)
            {
                return true;
            }

            if (TryLoadNamedTamingMobAnimation(tamingMobPart, actionName, out _))
            {
                return true;
            }

            foreach (string alias in GetPartActionAliases(tamingMobPart, actionName))
            {
                if (TryLoadNamedTamingMobAnimation(tamingMobPart, alias, out _))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsTamingMobRenderOwnershipAction(CharacterPart tamingMobPart, string actionName)
        {
            if (tamingMobPart?.Type != CharacterPartType.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return ShouldSuppressBaseAvatarForTamingMob(tamingMobPart, actionName);
        }

        private static bool ShouldSuppressBaseAvatarForTamingMob(CharacterPart tamingMobPart, string actionName)
        {
            return tamingMobPart?.Type == CharacterPartType.TamingMob
                   && tamingMobPart.ItemId == MechanicTamingMobItemId
                   && (IsMechanicMountedRenderOwnershipSignal(tamingMobPart, actionName)
                       || IsMechanicMountedSitFallbackAction(tamingMobPart, actionName));
        }

        private static bool IsExactMechanicVehicleActionRequired(CharacterPart tamingMobPart, string actionName)
        {
            return tamingMobPart?.Type == CharacterPartType.TamingMob
                   && tamingMobPart.ItemId == MechanicTamingMobItemId
                   && ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(
                       tamingMobPart.ItemId,
                       actionName);
        }

        private static bool IsMechanicMountedRenderOwnershipSignal(CharacterPart tamingMobPart, string actionName)
        {
            return SupportsTamingMobAction(tamingMobPart, actionName)
                   && (ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(
                           MechanicTamingMobItemId,
                           actionName)
                       || IsMechanicMountOnlyAction(tamingMobPart, actionName));
        }

        private static bool IsMechanicMountOnlyAction(CharacterPart tamingMobPart, string actionName)
        {
            if (!HasPublishedTamingMobAnimation(tamingMobPart, actionName))
            {
                return false;
            }

            return !string.Equals(actionName, "stand1", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "stand2", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "walk1", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "walk2", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "prone", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "fly", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "swim", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "sit", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "alert", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "heal", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "dead", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "ghost", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "tired", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPublishedTamingMobAnimation(CharacterPart tamingMobPart, string actionName)
        {
            if (tamingMobPart?.Type != CharacterPartType.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (tamingMobPart.Animations.TryGetValue(actionName, out CharacterAnimation animation))
            {
                return animation?.Frames?.Count > 0;
            }

            return tamingMobPart.AvailableAnimations != null
                   && tamingMobPart.AvailableAnimations.Count > 0
                   && tamingMobPart.AvailableAnimations.Contains(actionName);
        }

        private static bool TryLoadNamedTamingMobAnimation(
            CharacterPart tamingMobPart,
            string actionName,
            out CharacterAnimation animation)
        {
            animation = null;
            if (tamingMobPart?.Type != CharacterPartType.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (tamingMobPart.Animations.TryGetValue(actionName, out animation)
                && animation?.Frames?.Count > 0)
            {
                return true;
            }

            if (tamingMobPart.AvailableAnimations != null
                && tamingMobPart.AvailableAnimations.Count > 0
                && !tamingMobPart.AvailableAnimations.Contains(actionName))
            {
                animation = null;
                return false;
            }

            if (tamingMobPart.AnimationResolver == null)
            {
                animation = null;
                return false;
            }

            animation = tamingMobPart.AnimationResolver(actionName);
            if (animation?.Frames?.Count > 0)
            {
                tamingMobPart.Animations[actionName] = animation;
                return true;
            }

            animation = null;
            return false;
        }

        private static bool IsMechanicMountedSitFallbackAction(CharacterPart tamingMobPart, string actionName)
        {
            return tamingMobPart?.Type == CharacterPartType.TamingMob
                   && tamingMobPart.ItemId == MechanicTamingMobItemId
                   && string.Equals(actionName, "sit", StringComparison.OrdinalIgnoreCase)
                   && SupportsTamingMobAction(tamingMobPart, actionName);
        }

        private void AddPart(
            List<AssembledPart> parts,
            CharacterFrame frame,
            Point offset,
            CharacterPartType type,
            CharacterPart sourcePart,
            int? zOverride = null,
            PortableChairLayer sourcePortableChairLayer = null)
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
                        PartType = type,
                        SourcePortableChairLayer = sourcePortableChairLayer,
                        RenderLayer = ResolveAvatarRenderLayer(type, subPart.Z, zIndex)
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
                    PartType = type,
                    SourcePortableChairLayer = sourcePortableChairLayer,
                    RenderLayer = ResolveAvatarRenderLayer(type, frame.Z, zIndex)
                });
            }
        }

        private static AvatarRenderLayer ResolveAvatarRenderLayer(CharacterPartType partType, string zLayer, int zIndex)
        {
            if (TryResolveAvatarRenderLayerFromZLayer(zLayer, zIndex, out AvatarRenderLayer renderLayer))
            {
                return renderLayer;
            }

            return partType switch
            {
                CharacterPartType.HairBelowBody or CharacterPartType.Cape or CharacterPartType.Shield
                    => AvatarRenderLayer.UnderCharacter,
                CharacterPartType.Head or CharacterPartType.Ear or CharacterPartType.Earrings
                    => AvatarRenderLayer.UnderFace,
                CharacterPartType.Face
                    => AvatarRenderLayer.Face,
                CharacterPartType.Hair or CharacterPartType.HairOverHead
                    or CharacterPartType.Cap or CharacterPartType.CapOverHair or CharacterPartType.CapBelowAccessory
                    or CharacterPartType.Accessory or CharacterPartType.AccessoryOverHair
                    or CharacterPartType.Face_Accessory or CharacterPartType.Eye_Accessory
                    => AvatarRenderLayer.OverFace,
                _ => ResolveAvatarRenderLayerFromZIndex(zIndex, preferUnderCharacter: false)
            };
        }

        private static bool TryResolveAvatarRenderLayerFromZLayer(string zLayer, int zIndex, out AvatarRenderLayer renderLayer)
        {
            renderLayer = default;
            if (string.IsNullOrWhiteSpace(zLayer))
            {
                return false;
            }

            bool preferUnderCharacter = UnderCharacterZLayers.Contains(zLayer)
                || zLayer.StartsWith("back", StringComparison.Ordinal);
            renderLayer = ResolveAvatarRenderLayerFromZIndex(zIndex, preferUnderCharacter);
            return true;
        }

        private static AvatarRenderLayer ResolveAvatarRenderLayerFromZIndex(int zIndex, bool preferUnderCharacter)
        {
            int headZ = ZMapReference.GetZIndex("head");
            if (zIndex < headZ)
            {
                return preferUnderCharacter
                    ? AvatarRenderLayer.UnderCharacter
                    : AvatarRenderLayer.OverCharacter;
            }

            int faceZ = ZMapReference.GetZIndex("face");
            if (zIndex < faceZ)
            {
                return AvatarRenderLayer.UnderFace;
            }

            if (zIndex == faceZ)
            {
                return AvatarRenderLayer.Face;
            }

            return AvatarRenderLayer.OverFace;
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

        internal static CharacterAnimation GetPartAnimation(CharacterPart part, string actionName)
        {
            if (part == null)
            {
                return null;
            }

            if (part.Type == CharacterPartType.Morph)
            {
                if (part.MorphActionFrameOwner != null)
                {
                    return part.MorphActionFrameOwner.GetAnimation(part, actionName);
                }

                if (part.Animations.TryGetValue(actionName, out CharacterAnimation morphAnimation))
                {
                    return morphAnimation;
                }

                return part.GetAnimation(actionName);
            }

            if (part.Type == CharacterPartType.TamingMob)
            {
                if (part.TamingMobActionFrameOwner != null)
                {
                    return part.TamingMobActionFrameOwner.GetAnimation(part, actionName);
                }

                if (TryLoadNamedTamingMobAnimation(part, actionName, out CharacterAnimation mountAnimation))
                {
                    return mountAnimation;
                }

                foreach (string alias in GetPartActionAliases(part, actionName))
                {
                    if (TryLoadNamedTamingMobAnimation(part, alias, out mountAnimation))
                    {
                        return mountAnimation;
                    }
                }

                return null;
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

        private FaceLookEntry GetFaceLook(string expressionName, CharacterPart visibleFaceAccessory)
        {
            if (_build?.Face == null)
            {
                return null;
            }

            return _build.Face.GetLook(expressionName, _build.Skin, visibleFaceAccessory);
        }

        private FaceLookEntry GetFaceLook(string expressionName)
        {
            return GetFaceLook(expressionName, GetVisibleFaceAccessoryPart());
        }

        private CharacterPart GetVisibleFaceAccessoryPart()
        {
            return _build?.Equipment != null
                && _build.Equipment.TryGetValue(EquipSlot.FaceAccessory, out CharacterPart faceAccessoryPart)
                ? faceAccessoryPart
                : null;
        }

        private static FaceLookFrame GetFaceLookFrame(FaceLookEntry faceLook, int frameIndex)
        {
            if (faceLook?.Frames == null || faceLook.Frames.Count == 0)
            {
                return null;
            }

            return faceLook.Frames[frameIndex % faceLook.Frames.Count];
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

        private static IReadOnlyList<AssembledPart>[] BuildAvatarRenderLayers(List<AssembledPart> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return AssembledFrame.CreateEmptyAvatarRenderLayers();
            }

            var layeredParts = new List<AssembledPart>[5];
            for (int i = 0; i < parts.Count; i++)
            {
                AssembledPart part = parts[i];
                if (part?.Texture == null || !part.IsVisible)
                {
                    continue;
                }

                int layerIndex = (int)part.RenderLayer;
                layeredParts[layerIndex] ??= new List<AssembledPart>();
                layeredParts[layerIndex].Add(part);
            }

            var finalizedLayers = new IReadOnlyList<AssembledPart>[5];
            for (int i = 0; i < finalizedLayers.Length; i++)
            {
                finalizedLayers[i] = layeredParts[i] is { } layerParts
                    ? layerParts
                    : Array.Empty<AssembledPart>();
            }

            return finalizedLayers;
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
