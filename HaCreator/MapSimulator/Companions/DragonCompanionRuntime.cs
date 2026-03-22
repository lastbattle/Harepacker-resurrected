using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace HaCreator.MapSimulator.Companions
{
    internal sealed class DragonCompanionRuntime
    {
        private sealed class DragonAnimationSet
        {
            public DragonAnimationSet(int jobId, IReadOnlyDictionary<string, SkillAnimation> animations)
            {
                JobId = jobId;
                Animations = animations ?? throw new ArgumentNullException(nameof(animations));
                StandAnimation = GetAnimation("stand");
                MoveAnimation = GetAnimation("move");
            }

            public int JobId { get; }
            public IReadOnlyDictionary<string, SkillAnimation> Animations { get; }
            public SkillAnimation StandAnimation { get; }
            public SkillAnimation MoveAnimation { get; }

            public bool TryGetAnimation(string actionName, out SkillAnimation animation)
            {
                animation = null;
                return !string.IsNullOrWhiteSpace(actionName)
                       && Animations.TryGetValue(actionName, out animation)
                       && animation != null;
            }

            public SkillAnimation GetAnimation(string actionName)
            {
                TryGetAnimation(actionName, out SkillAnimation animation);
                return animation;
            }
        }

        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, DragonAnimationSet> _animationCache = new();
        private Func<MapInfo> _currentMapInfoProvider;
        private DragonAnimationSet _currentSet;
        private string _currentActionName;
        private int _currentActionStartTime;
        private string _observedOwnerActionName;
        private bool _facingRight = true;
        private Vector2 _worldAnchor;
        private Vector2 _visualAnchor;
        private float _alpha;
        private int _lastUpdateTime = int.MinValue;
        private bool _isSuppressed;

        private const float SnapDistance = 140f;
        private const float FollowLerpRate = 10f;
        private const float AlphaFadeRate = 5.5f;
        private const float GroundSideOffset = 42f;
        private const float GroundVerticalOffset = -12f;
        private const float LadderSideOffset = 34f;
        private const float LadderVerticalOffset = 18f;
        private const int ExplicitActionFadeLeadMs = 120;

        public DragonCompanionRuntime(GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public void SetCurrentMapInfoProvider(Func<MapInfo> currentMapInfoProvider)
        {
            _currentMapInfoProvider = currentMapInfoProvider;
        }

        public void Update(PlayerCharacter owner, int currentTime)
        {
            if (owner?.Build == null || !TryResolveDragonJob(owner.Build.Job, out int dragonJob))
            {
                Clear();
                return;
            }

            DragonAnimationSet animationSet = GetOrLoadAnimationSet(dragonJob);
            if (animationSet == null)
            {
                Clear();
                return;
            }

            _currentSet = animationSet;
            _facingRight = owner.FacingRight;
            _worldAnchor = ResolveAnchor(owner, animationSet, currentTime);
            _isSuppressed = ShouldSuppressForCurrentMap();

            float deltaSeconds = GetDeltaSeconds(currentTime);
            UpdateVisualAnchor(deltaSeconds);

            bool explicitActionSelected = false;
            string ownerActionName = owner.CurrentActionName;
            if (!string.Equals(_observedOwnerActionName, ownerActionName, StringComparison.OrdinalIgnoreCase))
            {
                _observedOwnerActionName = ownerActionName;
                string explicitActionName = ResolveExplicitActionName(ownerActionName, animationSet);
                if (!string.IsNullOrWhiteSpace(explicitActionName))
                {
                    SetCurrentAction(explicitActionName, currentTime);
                    explicitActionSelected = true;
                }
            }

            string baseActionName = ResolveBaseActionName(owner, animationSet);
            if (!explicitActionSelected
                && (string.IsNullOrWhiteSpace(_currentActionName)
                || string.Equals(_currentActionName, "stand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_currentActionName, "move", StringComparison.OrdinalIgnoreCase)
                || !animationSet.TryGetAnimation(_currentActionName, out SkillAnimation activeAnimation)
                || !IsExplicitDragonAction(_currentActionName)
                || (activeAnimation.IsComplete(Math.Max(0, currentTime - _currentActionStartTime))
                    && !string.Equals(ownerActionName, _currentActionName, StringComparison.OrdinalIgnoreCase))))
            {
                SetCurrentAction(baseActionName, currentTime, preserveStartTimeWhenUnchanged: true);
            }

            float alphaTarget = _isSuppressed ? 0f : 1f;
            if (!_isSuppressed
                && _currentSet.TryGetAnimation(_currentActionName, out SkillAnimation activeAction)
                && IsExplicitDragonAction(_currentActionName))
            {
                int elapsed = Math.Max(0, currentTime - _currentActionStartTime);
                int fadeStart = Math.Max(0, activeAction.TotalDuration - ExplicitActionFadeLeadMs);
                if (activeAction.IsComplete(elapsed))
                {
                    alphaTarget = 0f;
                }
                else if (elapsed >= fadeStart && activeAction.TotalDuration > fadeStart)
                {
                    float fadeProgress = (elapsed - fadeStart) / (float)(activeAction.TotalDuration - fadeStart);
                    alphaTarget = MathHelper.Clamp(1f - fadeProgress, 0f, 1f);
                }
            }

            _alpha = Approach(_alpha, alphaTarget, deltaSeconds * AlphaFadeRate);
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (_currentSet == null
                || string.IsNullOrWhiteSpace(_currentActionName)
                || _alpha <= 0.01f
                || !_currentSet.TryGetAnimation(_currentActionName, out SkillAnimation animation))
            {
                return;
            }

            SkillFrame frame = animation.GetFrameAtTime(Math.Max(0, currentTime - _currentActionStartTime));
            if (frame?.Texture == null)
            {
                return;
            }

            int screenX = (int)_visualAnchor.X - mapShiftX + centerX;
            int screenY = (int)_visualAnchor.Y - mapShiftY + centerY;
            frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, screenX, screenY, Color.White * _alpha, !_facingRight, null);
        }

        public bool TryGetCurrentFrameTop(int currentTime, out Vector2 top)
        {
            top = Vector2.Zero;

            if (_currentSet == null
                || string.IsNullOrWhiteSpace(_currentActionName)
                || _alpha <= 0.01f
                || !_currentSet.TryGetAnimation(_currentActionName, out SkillAnimation animation))
            {
                return false;
            }

            SkillFrame frame = animation.GetFrameAtTime(Math.Max(0, currentTime - _currentActionStartTime));
            if (frame == null)
            {
                return false;
            }

            float halfWidth = frame.Bounds.Width * 0.5f;
            float left = _facingRight
                ? _visualAnchor.X - frame.Origin.X
                : _visualAnchor.X - (frame.Bounds.Width - frame.Origin.X);
            top = new Vector2(left + halfWidth, _visualAnchor.Y - frame.Origin.Y);
            return true;
        }

        public void Clear()
        {
            _currentSet = null;
            _currentActionName = null;
            _currentActionStartTime = 0;
            _observedOwnerActionName = null;
            _worldAnchor = Vector2.Zero;
            _visualAnchor = Vector2.Zero;
            _alpha = 0f;
            _lastUpdateTime = int.MinValue;
            _isSuppressed = false;
        }

        private DragonAnimationSet GetOrLoadAnimationSet(int dragonJob)
        {
            if (_animationCache.TryGetValue(dragonJob, out DragonAnimationSet cached))
            {
                return cached;
            }

            DragonAnimationSet loaded = LoadAnimationSet(dragonJob);
            if (loaded != null)
            {
                _animationCache[dragonJob] = loaded;
            }

            return loaded;
        }

        private DragonAnimationSet LoadAnimationSet(int dragonJob)
        {
            WzImage image = global::HaCreator.Program.FindImage("Skill", $"Dragon/{dragonJob}.img");
            if (image == null)
            {
                return null;
            }

            var animations = new Dictionary<string, SkillAnimation>(StringComparer.OrdinalIgnoreCase);
            foreach (WzImageProperty property in image.WzProperties)
            {
                if (property is not WzSubProperty actionNode || string.Equals(actionNode.Name, "info", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SkillAnimation animation = LoadAnimation(actionNode);
                if (animation?.Frames?.Count > 0)
                {
                    animations[actionNode.Name] = animation;
                }
            }

            return animations.Count == 0
                ? null
                : new DragonAnimationSet(dragonJob, animations);
        }

        private SkillAnimation LoadAnimation(WzSubProperty actionNode)
        {
            var animation = new SkillAnimation
            {
                Name = actionNode.Name,
                Loop = IsLoopingAction(actionNode.Name)
            };

            IEnumerable<WzCanvasProperty> frames = actionNode.WzProperties
                .OfType<WzCanvasProperty>()
                .OrderBy(frame => ParseFrameIndex(frame.Name));

            foreach (WzCanvasProperty canvas in frames)
            {
                IDXObject texture = LoadTexture(canvas);
                if (texture == null)
                {
                    continue;
                }

                WzVectorProperty origin = canvas["origin"] as WzVectorProperty;
                animation.Frames.Add(new SkillFrame
                {
                    Texture = texture,
                    Origin = new Point(origin?.X.Value ?? 0, origin?.Y.Value ?? 0),
                    Delay = Math.Max(1, GetIntValue(canvas["delay"]) ?? 100),
                    Bounds = new Rectangle(0, 0, texture.Width, texture.Height)
                });
            }

            animation.CalculateDuration();
            return animation;
        }

        private IDXObject LoadTexture(WzCanvasProperty canvas)
        {
            if (canvas?.PngProperty == null)
            {
                return null;
            }

            try
            {
                var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null)
                {
                    return null;
                }

                Texture2D texture = bitmap.ToTexture2DAndDispose(_device);
                return texture == null
                    ? null
                    : new DXObject(0, 0, texture, Math.Max(1, GetIntValue(canvas["delay"]) ?? 100))
                    {
                        Tag = canvas
                    };
            }
            catch
            {
                return null;
            }
        }

        private static Vector2 ResolveAnchor(PlayerCharacter owner, DragonAnimationSet animationSet, int currentTime)
        {
            if (owner.State == PlayerState.Ladder || owner.State == PlayerState.Rope)
            {
                Point? bodyOrigin = owner.TryGetCurrentBodyOrigin(currentTime);
                if (bodyOrigin.HasValue)
                {
                    return ResolveLadderAnchor(owner, animationSet, bodyOrigin.Value);
                }
            }

            return ResolveGroundAnchor(owner, animationSet, currentTime);
        }

        private static bool TryResolveDragonJob(int jobId, out int dragonJob)
        {
            dragonJob = jobId switch
            {
                >= 2200 and <= 2218 => jobId,
                _ => 0
            };

            return dragonJob != 0;
        }

        private static string ResolveExplicitActionName(string ownerActionName, DragonAnimationSet animationSet)
        {
            if (string.IsNullOrWhiteSpace(ownerActionName))
            {
                return null;
            }

            foreach (string candidate in EnumerateActionCandidates(ownerActionName))
            {
                if (animationSet.TryGetAnimation(candidate, out _) && IsExplicitDragonAction(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string ResolveBaseActionName(PlayerCharacter owner, DragonAnimationSet animationSet)
        {
            bool useMove = owner.State is PlayerState.Walking
                or PlayerState.Jumping
                or PlayerState.Falling
                or PlayerState.Ladder
                or PlayerState.Rope
                or PlayerState.Swimming
                or PlayerState.Flying;

            if (useMove && animationSet.MoveAnimation != null)
            {
                return "move";
            }

            return animationSet.StandAnimation != null ? "stand" : animationSet.Animations.Keys.FirstOrDefault();
        }

        private void SetCurrentAction(string actionName, int currentTime, bool preserveStartTimeWhenUnchanged = false)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            if (preserveStartTimeWhenUnchanged
                && string.Equals(_currentActionName, actionName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentActionName = actionName;
            _currentActionStartTime = currentTime;
            if (!_isSuppressed)
            {
                _alpha = Math.Max(_alpha, 0.15f);
            }
        }

        private static IEnumerable<string> EnumerateActionCandidates(string ownerActionName)
        {
            yield return ownerActionName;

            if (string.Equals(ownerActionName, "stand1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerActionName, "stand2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerActionName, "alert", StringComparison.OrdinalIgnoreCase))
            {
                yield return "stand";
                yield break;
            }

            if (string.Equals(ownerActionName, "walk1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerActionName, "walk2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerActionName, "jump", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerActionName, "fly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerActionName, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ownerActionName, "rope", StringComparison.OrdinalIgnoreCase))
            {
                yield return "move";
            }
        }

        private static bool IsExplicitDragonAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && !string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLoopingAction(string actionName)
        {
            return string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseFrameIndex(string value)
        {
            return int.TryParse(value, out int parsed) ? parsed : int.MaxValue;
        }

        private static int? GetIntValue(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)longProperty.Value,
                _ => null
            };
        }

        private bool ShouldSuppressForCurrentMap()
        {
            MapInfo mapInfo = _currentMapInfoProvider?.Invoke();
            return mapInfo?.vanishDragon == true;
        }

        private float GetDeltaSeconds(int currentTime)
        {
            if (_lastUpdateTime == int.MinValue)
            {
                _lastUpdateTime = currentTime;
                return 1f / 60f;
            }

            int elapsedMs = Math.Max(0, currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;
            return MathHelper.Clamp(elapsedMs / 1000f, 1f / 240f, 0.1f);
        }

        private void UpdateVisualAnchor(float deltaSeconds)
        {
            if (_visualAnchor == Vector2.Zero)
            {
                _visualAnchor = _worldAnchor;
                return;
            }

            float distance = Vector2.Distance(_visualAnchor, _worldAnchor);
            if (distance >= SnapDistance)
            {
                _visualAnchor = _worldAnchor;
                return;
            }

            float amount = MathHelper.Clamp(deltaSeconds * FollowLerpRate, 0f, 1f);
            _visualAnchor = Vector2.Lerp(_visualAnchor, _worldAnchor, amount);
        }

        private static Vector2 ResolveGroundAnchor(PlayerCharacter owner, DragonAnimationSet animationSet, int currentTime)
        {
            SkillFrame standFrame = animationSet.StandAnimation?.Frames?.Count > 0
                ? animationSet.StandAnimation.Frames[0]
                : null;

            float side = owner.FacingRight ? -1f : 1f;
            float horizontalOffset = Math.Max(GroundSideOffset, (standFrame?.Origin.X ?? 79) * 0.55f);
            Point? bodyOrigin = owner.TryGetCurrentBodyOrigin(currentTime);
            if (bodyOrigin.HasValue)
            {
                return new Vector2(bodyOrigin.Value.X + side * horizontalOffset, bodyOrigin.Value.Y + GroundVerticalOffset);
            }

            Rectangle? ownerBounds = owner.TryGetCurrentFrameBounds(currentTime);
            if (ownerBounds.HasValue && !ownerBounds.Value.IsEmpty)
            {
                Rectangle bounds = ownerBounds.Value;
                float anchorX = owner.FacingRight
                    ? bounds.Left + side * horizontalOffset
                    : bounds.Right + side * horizontalOffset;
                return new Vector2(anchorX, owner.Y + GroundVerticalOffset);
            }

            return new Vector2(owner.X + side * horizontalOffset, owner.Y + GroundVerticalOffset);
        }

        private static Vector2 ResolveLadderAnchor(PlayerCharacter owner, DragonAnimationSet animationSet, Point bodyOrigin)
        {
            SkillFrame moveFrame = animationSet.MoveAnimation?.Frames?.Count > 0
                ? animationSet.MoveAnimation.Frames[0]
                : animationSet.StandAnimation?.Frames?.Count > 0
                    ? animationSet.StandAnimation.Frames[0]
                    : null;

            float side = owner.FacingRight ? -1f : 1f;
            float horizontalOffset = Math.Max(LadderSideOffset, (moveFrame?.Origin.X ?? 79) * 0.45f);
            return new Vector2(bodyOrigin.X + side * horizontalOffset, bodyOrigin.Y + LadderVerticalOffset);
        }

        private static float Approach(float current, float target, float maxStep)
        {
            if (current < target)
            {
                return Math.Min(current + maxStep, target);
            }

            return Math.Max(current - maxStep, target);
        }
    }
}
