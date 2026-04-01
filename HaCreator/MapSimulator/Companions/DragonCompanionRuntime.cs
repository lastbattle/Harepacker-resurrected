using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Physics;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace HaCreator.MapSimulator.Companions
{
    internal sealed class DragonCompanionRuntime
    {
        [Flags]
        private enum FollowUpdateFlags
        {
            None = 0,
            SnappedToTarget = 1 << 0,
            PassiveSettleEffect = 1 << 1
        }

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

        private enum DragonQuestInfoState
        {
            PreStart = 0,
            RewardReady = 1,
            Active = 2,
            Hidden = 6
        }

        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, DragonAnimationSet> _animationCache = new();
        private readonly Dictionary<int, SkillAnimation> _questInfoAnimationCache = new();
        private SkillAnimation _dragonFuryAnimation;
        private SkillAnimation _dragonBlinkAnimation;
        private SkillAnimation _questInfoAnimation;
        private static readonly HashSet<int> HiddenDragonMountIds = new()
        {
            1902040,
            1902041,
            1902042
        };
        private Func<MapInfo> _currentMapInfoProvider;
        private DragonAnimationSet _currentSet;
        private string _currentActionName;
        private int _currentActionStartTime;
        private string _observedOwnerActionName;
        private bool _facingRight = true;
        private Vector2 _worldAnchor;
        private Vector2 _visualAnchor;
        private Vector2 _followVelocity;
        private float _alpha;
        private float _dragonFuryAlpha;
        private float _questInfoAlpha;
        private int _lastUpdateTime = int.MinValue;
        private int _lastBlinkStartTime = int.MinValue;
        private bool _isSuppressed;
        private bool _isFollowActive;
        private DragonQuestInfoState _questInfoPreviewState = DragonQuestInfoState.Hidden;

        private const float SnapDistance = 140f;
        private const float AlphaFadeRate = 5.5f;
        private const float GroundSideOffset = 42f;
        private const float GroundVerticalOffset = -12f;
        private const float LadderSideOffset = 34f;
        private const float LadderVerticalOffset = 18f;
        private const float DragonKeyDownBarHalfWidth = 36f;
        private const float DragonKeyDownBarVerticalGap = 30f;
        private const float FollowMinSpeed = 18f;
        private const float FollowMaxHorizontalSpeed = 210f;
        private const float FollowMaxVerticalSpeed = 180f;
        private const float FollowHorizontalResponse = 7.5f;
        private const float FollowVerticalResponse = 6.5f;
        private const float FollowHorizontalForceScale = 0.65f;
        private const float FollowVerticalForceScale = 0.55f;
        private const float FollowBrakeScale = 1.35f;
        private const float FollowArrivalDistance = 1.5f;
        private const float ActiveFollowEngageDistance = 36f;
        private const float ActiveFollowReleaseDistance = 14f;
        private const float PassiveHorizontalResponse = 3.2f;
        private const float PassiveVerticalResponse = 3.8f;
        private const float PassiveHorizontalForceScale = 0.3f;
        private const float PassiveVerticalForceScale = 0.34f;
        private const float PassiveMaxHorizontalSpeed = 92f;
        private const float PassiveMaxVerticalSpeed = 108f;
        private const float PassiveArrivalDistance = 4f;
        private const float PassiveHoldDistance = 7f;
        private const float PassiveVerticalHoldDistance = 5f;
        private const float QuestInfoHorizontalOffset = 20f;
        private const float QuestInfoVerticalGap = 15f;

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
            _isSuppressed = ShouldSuppress(owner);

            float deltaSeconds = GetDeltaSeconds(currentTime);
            UpdateFollowState(owner);
            FollowUpdateFlags followUpdate = UpdateVisualAnchor(deltaSeconds);
            if ((followUpdate & (FollowUpdateFlags.SnappedToTarget | FollowUpdateFlags.PassiveSettleEffect)) != 0)
            {
                TriggerBlink(currentTime);
            }

            bool explicitActionSelected = false;
            string ownerActionName = owner.CurrentActionName;
            string explicitActionName = ResolveExplicitActionName(ownerActionName, animationSet);
            if (!string.Equals(_observedOwnerActionName, ownerActionName, StringComparison.OrdinalIgnoreCase))
            {
                _observedOwnerActionName = ownerActionName;
                if (!string.IsNullOrWhiteSpace(explicitActionName))
                {
                    SetCurrentAction(explicitActionName, currentTime);
                    explicitActionSelected = true;
                }
            }

            bool shouldLoopExplicitAction = false;
            if (_currentSet.TryGetAnimation(_currentActionName, out SkillAnimation currentAnimation)
                && IsExplicitDragonAction(_currentActionName)
                && ShouldLoopExplicitAction(_currentActionName)
                && string.Equals(explicitActionName, _currentActionName, StringComparison.OrdinalIgnoreCase))
            {
                int elapsed = Math.Max(0, currentTime - _currentActionStartTime);
                if (currentAnimation.IsComplete(elapsed))
                {
                    _currentActionStartTime = currentTime;
                }

                shouldLoopExplicitAction = true;
            }

            string baseActionName = ResolveBaseActionName(owner, animationSet);
            if (!explicitActionSelected
                && (string.IsNullOrWhiteSpace(_currentActionName)
                || string.Equals(_currentActionName, "stand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_currentActionName, "move", StringComparison.OrdinalIgnoreCase)
                || !animationSet.TryGetAnimation(_currentActionName, out SkillAnimation activeAnimation)
                || !IsExplicitDragonAction(_currentActionName)
                || (shouldLoopExplicitAction
                    && string.Equals(explicitActionName, _currentActionName, StringComparison.OrdinalIgnoreCase))
                || (activeAnimation.IsComplete(Math.Max(0, currentTime - _currentActionStartTime))
                    && !string.Equals(ownerActionName, _currentActionName, StringComparison.OrdinalIgnoreCase))))
            {
                SetCurrentAction(baseActionName, currentTime, preserveStartTimeWhenUnchanged: true);
            }

            _alpha = Approach(_alpha, _isSuppressed ? 0f : 1f, deltaSeconds * AlphaFadeRate);
            UpdateAuxiliaryLayers(owner, currentTime, deltaSeconds);
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
            DrawAuxiliaryLayers(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, currentTime);

            if (!TryResolveCurrentFrame(currentTime, out SkillFrame frame, out float frameAlpha)
                || frame?.Texture == null
                || frameAlpha <= 0.01f)
            {
                return;
            }

            int screenX = (int)_visualAnchor.X - mapShiftX + centerX;
            int screenY = (int)_visualAnchor.Y - mapShiftY + centerY;
            frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, screenX, screenY, Color.White * frameAlpha, !_facingRight, null);
        }

        public bool TryGetCurrentFrameTop(int currentTime, out Vector2 top)
        {
            top = Vector2.Zero;

            if (!TryResolveCurrentFrame(currentTime, out SkillFrame frame, out _))
            {
                return false;
            }

            Rectangle bounds = GetRelativeBounds(frame);
            float centerOffsetX = (bounds.Left + bounds.Right) * 0.5f;
            float topOffsetY = bounds.Top;
            float topX = _facingRight
                ? _visualAnchor.X + centerOffsetX
                : _visualAnchor.X - centerOffsetX;
            top = new Vector2(topX, _visualAnchor.Y + topOffsetY);
            return true;
        }

        public bool TryGetCurrentKeyDownBarAnchor(int currentTime, out Vector2 anchor)
        {
            anchor = Vector2.Zero;

            if (!TryResolveCurrentFrame(currentTime, out SkillFrame frame, out _))
            {
                return false;
            }

            anchor = new Vector2(
                _visualAnchor.X - DragonKeyDownBarHalfWidth,
                _visualAnchor.Y - frame.Bounds.Height - DragonKeyDownBarVerticalGap);
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
            _followVelocity = Vector2.Zero;
            _alpha = 0f;
            _dragonFuryAlpha = 0f;
            _questInfoAlpha = 0f;
            _lastUpdateTime = int.MinValue;
            _lastBlinkStartTime = int.MinValue;
            _isSuppressed = false;
            _isFollowActive = false;
        }

        public void SetQuestInfoPreviewState(int? state)
        {
            _questInfoPreviewState = state switch
            {
                0 => DragonQuestInfoState.PreStart,
                1 => DragonQuestInfoState.RewardReady,
                2 => DragonQuestInfoState.Active,
                _ => DragonQuestInfoState.Hidden
            };
            _questInfoAnimation = null;
            _questInfoAlpha = 0f;
        }

        public string DescribeDebugStatus(PlayerCharacter owner)
        {
            string ownerName = owner?.Build?.Name;
            string questInfoLabel = _questInfoPreviewState switch
            {
                DragonQuestInfoState.PreStart => "state 0 (prestart)",
                DragonQuestInfoState.RewardReady => "state 1 (reward-ready)",
                DragonQuestInfoState.Active => "state 2 (active)",
                _ => "hidden"
            };

            return $"Dragon action: {_currentActionName ?? "none"}, follow: {(_isFollowActive ? "active" : "passive")}, suppressed: {_isSuppressed}, quest info: {questInfoLabel}, owner: {ownerName ?? "Unknown"}";
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
                    Bounds = ResolveFrameBounds(canvas, texture),
                    AlphaStart = Math.Clamp(GetIntValue(canvas["a0"]) ?? 255, 0, 255),
                    AlphaEnd = Math.Clamp(GetIntValue(canvas["a1"]) ?? 255, 0, 255)
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

        private static bool ShouldLoopExplicitAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && actionName.EndsWith("_prepare", StringComparison.OrdinalIgnoreCase);
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
            return ShouldSuppressForMapInfo(mapInfo);
        }

        private static bool ShouldSuppressForMapInfo(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            return mapInfo.vanishDragon == true
                || mapInfo.fieldType == FieldType.FIELDTYPE_NODRAGON;
        }

        private bool ShouldSuppress(PlayerCharacter owner)
        {
            return ShouldSuppressForCurrentMap() || ShouldSuppressForCurrentMount(owner);
        }

        private static bool ShouldSuppressForCurrentMount(PlayerCharacter owner)
        {
            if (owner?.Build?.Equipment == null
                || !owner.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart)
                || mountPart?.ItemId is not int mountItemId)
            {
                return false;
            }

            return HiddenDragonMountIds.Contains(mountItemId);
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

        private void UpdateFollowState(PlayerCharacter owner)
        {
            float horizontalDelta = Math.Abs(_worldAnchor.X - _visualAnchor.X);
            float verticalDelta = Math.Abs(_worldAnchor.Y - _visualAnchor.Y);
            bool ownerInMotion = owner?.State is PlayerState.Walking
                or PlayerState.Jumping
                or PlayerState.Falling
                or PlayerState.Ladder
                or PlayerState.Rope
                or PlayerState.Swimming
                or PlayerState.Flying;
            bool ownerHasMomentum = owner?.Physics != null
                && (Math.Abs(owner.Physics.VelocityX) > FollowMinSpeed || Math.Abs(owner.Physics.VelocityY) > FollowMinSpeed);

            if (_isFollowActive)
            {
                _isFollowActive = ownerInMotion
                    || ownerHasMomentum
                    || horizontalDelta > ActiveFollowReleaseDistance
                    || verticalDelta > ActiveFollowReleaseDistance;
            }
            else
            {
                _isFollowActive = ownerInMotion
                    || ownerHasMomentum
                    || horizontalDelta > ActiveFollowEngageDistance
                    || verticalDelta > ActiveFollowEngageDistance;
            }
        }

        private FollowUpdateFlags UpdateVisualAnchor(float deltaSeconds)
        {
            if (_visualAnchor == Vector2.Zero)
            {
                _visualAnchor = _worldAnchor;
                _followVelocity = Vector2.Zero;
                return FollowUpdateFlags.None;
            }

            float distance = Vector2.Distance(_visualAnchor, _worldAnchor);
            if (distance >= SnapDistance)
            {
                _visualAnchor = _worldAnchor;
                _followVelocity = Vector2.Zero;
                return FollowUpdateFlags.SnappedToTarget;
            }

            double velocityX = _followVelocity.X;
            double velocityY = _followVelocity.Y;
            FollowUpdateFlags result = FollowUpdateFlags.None;

            if (_isFollowActive)
            {
                UpdateFollowAxis(
                    ref _visualAnchor.X,
                    _worldAnchor.X,
                    ref velocityX,
                    deltaSeconds,
                    FollowHorizontalResponse,
                    FollowMaxHorizontalSpeed,
                    CVecCtrl.WalkAcceleration * FollowHorizontalForceScale,
                    FollowArrivalDistance);

                UpdateFollowAxis(
                    ref _visualAnchor.Y,
                    _worldAnchor.Y,
                    ref velocityY,
                    deltaSeconds,
                    FollowVerticalResponse,
                    FollowMaxVerticalSpeed,
                    CVecCtrl.AirDragDeceleration * FollowVerticalForceScale,
                    FollowArrivalDistance);
            }
            else
            {
                bool hadPassiveTravel = HasPassiveTravel(
                    _visualAnchor,
                    _worldAnchor,
                    _followVelocity);

                UpdatePassiveFollowAxis(
                    ref _visualAnchor.X,
                    _worldAnchor.X,
                    ref velocityX,
                    deltaSeconds,
                    PassiveHorizontalResponse,
                    PassiveMaxHorizontalSpeed,
                    CVecCtrl.WalkAcceleration * PassiveHorizontalForceScale,
                    PassiveHoldDistance,
                    PassiveArrivalDistance);

                UpdatePassiveFollowAxis(
                    ref _visualAnchor.Y,
                    _worldAnchor.Y,
                    ref velocityY,
                    deltaSeconds,
                    PassiveVerticalResponse,
                    PassiveMaxVerticalSpeed,
                    CVecCtrl.AirDragDeceleration * PassiveVerticalForceScale,
                    PassiveVerticalHoldDistance,
                    PassiveArrivalDistance);

                if (hadPassiveTravel
                    && IsPassiveSettled(_visualAnchor, _worldAnchor, velocityX, velocityY))
                {
                    result |= FollowUpdateFlags.PassiveSettleEffect;
                }
            }

            _followVelocity = new Vector2((float)velocityX, (float)velocityY);
            return result;
        }

        private static bool HasPassiveTravel(Vector2 visualAnchor, Vector2 worldAnchor, Vector2 velocity)
        {
            return Math.Abs(worldAnchor.X - visualAnchor.X) > PassiveHoldDistance
                   || Math.Abs(worldAnchor.Y - visualAnchor.Y) > PassiveVerticalHoldDistance
                   || Math.Abs(velocity.X) > FollowMinSpeed
                   || Math.Abs(velocity.Y) > FollowMinSpeed;
        }

        private static bool IsPassiveSettled(Vector2 visualAnchor, Vector2 worldAnchor, double velocityX, double velocityY)
        {
            return Math.Abs(worldAnchor.X - visualAnchor.X) <= PassiveArrivalDistance
                   && Math.Abs(worldAnchor.Y - visualAnchor.Y) <= PassiveArrivalDistance
                   && Math.Abs(velocityX) <= FollowMinSpeed
                   && Math.Abs(velocityY) <= FollowMinSpeed;
        }

        private static void UpdateFollowAxis(
            ref float position,
            float target,
            ref double velocity,
            float deltaSeconds,
            float responseScale,
            float maxSpeed,
            double force,
            float arrivalDistance)
        {
            float delta = target - position;
            if (Math.Abs(delta) <= arrivalDistance)
            {
                position = target;
                velocity = 0d;
                return;
            }

            double desiredSpeed = Math.Clamp(
                Math.Abs(delta) * responseScale,
                FollowMinSpeed,
                maxSpeed);
            double brake = Math.Max(force, CVecCtrl.WalkDeceleration * FollowBrakeScale);

            if (delta > 0f)
            {
                if (velocity < 0d)
                {
                    CVecCtrl.DecSpeed(ref velocity, brake, PhysicsConstants.Instance.DefaultMass, 0d, deltaSeconds);
                }

                CVecCtrl.AccSpeed(ref velocity, force, PhysicsConstants.Instance.DefaultMass, desiredSpeed, deltaSeconds);
            }
            else
            {
                if (velocity > 0d)
                {
                    CVecCtrl.DecSpeed(ref velocity, brake, PhysicsConstants.Instance.DefaultMass, 0d, deltaSeconds);
                }

                CVecCtrl.AccSpeed(ref velocity, -force, PhysicsConstants.Instance.DefaultMass, desiredSpeed, deltaSeconds);
            }

            float nextPosition = position + (float)(velocity * deltaSeconds);
            if ((delta > 0f && nextPosition >= target) || (delta < 0f && nextPosition <= target))
            {
                position = target;
                velocity = 0d;
                return;
            }

            position = nextPosition;
        }

        private static void UpdatePassiveFollowAxis(
            ref float position,
            float target,
            ref double velocity,
            float deltaSeconds,
            float responseScale,
            float maxSpeed,
            double force,
            float holdDistance,
            float arrivalDistance)
        {
            float delta = target - position;
            if (Math.Abs(delta) <= arrivalDistance)
            {
                position = target;
                velocity = 0d;
                return;
            }

            if (Math.Abs(delta) <= holdDistance)
            {
                CVecCtrl.DecSpeed(ref velocity, Math.Max(force, CVecCtrl.WalkDeceleration), PhysicsConstants.Instance.DefaultMass, 0d, deltaSeconds);
                position += (float)(velocity * deltaSeconds);
                return;
            }

            UpdateFollowAxis(ref position, target, ref velocity, deltaSeconds, responseScale, maxSpeed, force, arrivalDistance);
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

        private static Rectangle ResolveFrameBounds(WzCanvasProperty canvas, IDXObject texture)
        {
            WzVectorProperty lt = canvas["lt"] as WzVectorProperty;
            WzVectorProperty rb = canvas["rb"] as WzVectorProperty;
            if (lt != null && rb != null)
            {
                int left = lt.X.Value;
                int top = lt.Y.Value;
                int width = Math.Max(1, rb.X.Value - left);
                int height = Math.Max(1, rb.Y.Value - top);
                return new Rectangle(left, top, width, height);
            }

            WzVectorProperty origin = canvas["origin"] as WzVectorProperty;
            int originX = origin?.X.Value ?? 0;
            int originY = origin?.Y.Value ?? 0;
            return new Rectangle(-originX, -originY, texture.Width, texture.Height);
        }

        private static Rectangle GetRelativeBounds(SkillFrame frame)
        {
            if (frame == null)
            {
                return Rectangle.Empty;
            }

            if (!frame.Bounds.IsEmpty)
            {
                return frame.Bounds;
            }

            return new Rectangle(-frame.Origin.X, -frame.Origin.Y, 0, 0);
        }

        private bool TryResolveCurrentFrame(int currentTime, out SkillFrame frame, out float frameAlpha)
        {
            frame = null;
            frameAlpha = 0f;

            if (_currentSet == null
                || string.IsNullOrWhiteSpace(_currentActionName)
                || _alpha <= 0.01f
                || !_currentSet.TryGetAnimation(_currentActionName, out SkillAnimation animation)
                || !animation.TryGetFrameAtTime(Math.Max(0, currentTime - _currentActionStartTime), out frame, out int frameElapsedMs)
                || frame == null)
            {
                return false;
            }

            frameAlpha = _alpha * ResolveFrameAlpha(frame, frameElapsedMs);
            return frameAlpha > 0.01f;
        }

        private static float ResolveFrameAlpha(SkillFrame frame, int frameElapsedMs)
        {
            if (frame == null)
            {
                return 0f;
            }

            int startAlpha = Math.Clamp(frame.AlphaStart, 0, 255);
            int endAlpha = Math.Clamp(frame.AlphaEnd, 0, 255);
            float progress = MathHelper.Clamp(frameElapsedMs / (float)Math.Max(1, frame.Delay), 0f, 1f);
            return MathHelper.Lerp(startAlpha, endAlpha, progress) / 255f;
        }

        private void UpdateAuxiliaryLayers(PlayerCharacter owner, int currentTime, float deltaSeconds)
        {
            EnsureAuxiliaryAnimationsLoaded();

            bool shouldShowFury = ShouldShowDragonFury(owner);
            if (!shouldShowFury)
            {
                _dragonFuryAlpha = 0f;
                return;
            }

            _dragonFuryAlpha = Approach(_dragonFuryAlpha, _alpha, deltaSeconds * AlphaFadeRate * 1.5f);
            UpdateQuestInfoLayer(owner);
        }

        private void DrawAuxiliaryLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            DrawAnimationLayer(_dragonFuryAnimation, _dragonFuryAlpha, spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            DrawQuestInfoLayer(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, currentTime);

            if (_lastBlinkStartTime != int.MinValue)
            {
                DrawAnimationLayer(_dragonBlinkAnimation, _alpha, spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, currentTime, _lastBlinkStartTime);
            }
        }

        private void DrawAnimationLayer(
            SkillAnimation animation,
            float alpha,
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime,
            int? startTime = null,
            Vector2? anchorOverride = null)
        {
            if (animation == null || alpha <= 0.01f)
            {
                return;
            }

            int animationStartTime = startTime ?? _currentActionStartTime;
            if (!animation.TryGetFrameAtTime(Math.Max(0, currentTime - animationStartTime), out SkillFrame frame, out int frameElapsedMs)
                || frame?.Texture == null)
            {
                return;
            }

            if (!animation.Loop && animation.IsComplete(Math.Max(0, currentTime - animationStartTime)))
            {
                if (ReferenceEquals(animation, _dragonBlinkAnimation))
                {
                    _lastBlinkStartTime = int.MinValue;
                }

                return;
            }

            float frameAlpha = alpha * ResolveFrameAlpha(frame, frameElapsedMs);
            if (frameAlpha <= 0.01f)
            {
                return;
            }

            Vector2 anchor = anchorOverride ?? _visualAnchor;
            int screenX = (int)anchor.X - mapShiftX + centerX;
            int screenY = (int)anchor.Y - mapShiftY + centerY;
            frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, screenX, screenY, Color.White * frameAlpha, !_facingRight, null);
        }

        private void EnsureAuxiliaryAnimationsLoaded()
        {
            _dragonFuryAnimation ??= LoadEffectAnimation("BasicEff.img", "dragonFury", loop: true);
            _dragonBlinkAnimation ??= LoadEffectAnimation("BasicEff.img", "dragonBlink", loop: false);
        }

        private void UpdateQuestInfoLayer(PlayerCharacter owner)
        {
            if (!TryResolveQuestInfoAnimation(out SkillAnimation animation)
                || !ShouldShowQuestInfo(owner))
            {
                _questInfoAlpha = 0f;
                return;
            }

            _questInfoAnimation = animation;
            _questInfoAlpha = 1f;
        }

        private void DrawQuestInfoLayer(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (_questInfoAnimation == null
                || _questInfoAlpha <= 0.01f
                || !TryResolveQuestInfoAnchor(out Vector2 anchor))
            {
                return;
            }

            DrawAnimationLayer(
                _questInfoAnimation,
                _questInfoAlpha,
                spriteBatch,
                skeletonRenderer,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                currentTime,
                anchorOverride: anchor);
        }

        private SkillAnimation LoadEffectAnimation(string imageName, string propertyPath, bool loop)
        {
            WzImage image = global::HaCreator.Program.FindImage("Effect", imageName);
            if (image == null)
            {
                return null;
            }

            WzImageProperty property = image[propertyPath];
            if (property is not WzSubProperty subProperty)
            {
                return null;
            }

            SkillAnimation animation = LoadAnimation(subProperty);
            if (animation == null)
            {
                return null;
            }

            animation.Loop = loop;
            animation.CalculateDuration();
            return animation;
        }

        private bool ShouldShowDragonFury(PlayerCharacter owner)
        {
            if (_dragonFuryAnimation == null || _isSuppressed || _alpha <= 0.01f)
            {
                return false;
            }

            if (ShouldSuppressForCurrentMount(owner))
            {
                return false;
            }

            return !IsExplicitDragonAction(_currentActionName);
        }

        private bool TryResolveQuestInfoAnimation(out SkillAnimation animation)
        {
            animation = null;
            if (_questInfoPreviewState == DragonQuestInfoState.Hidden)
            {
                return false;
            }

            int questState = (int)_questInfoPreviewState;
            if (_questInfoAnimationCache.TryGetValue(questState, out animation))
            {
                return animation != null;
            }

            string effectPath = questState switch
            {
                (int)DragonQuestInfoState.PreStart => "QuestAlert",
                (int)DragonQuestInfoState.RewardReady => "QuestAlert2",
                (int)DragonQuestInfoState.Active => "QuestAlert3",
                _ => null
            };
            if (string.IsNullOrWhiteSpace(effectPath))
            {
                return false;
            }

            animation = LoadEffectAnimation("BasicEff.img", effectPath, loop: true);
            _questInfoAnimationCache[questState] = animation;
            return animation != null;
        }

        private bool TryResolveQuestInfoAnchor(out Vector2 anchor)
        {
            anchor = Vector2.Zero;

            if (!TryResolveCurrentFrame(Environment.TickCount, out SkillFrame frame, out _)
                || frame == null)
            {
                return false;
            }

            Rectangle bounds = GetRelativeBounds(frame);
            float dragonHeight = bounds.Height > 0 ? bounds.Height : frame.Texture?.Height ?? 0;
            anchor = new Vector2(
                _visualAnchor.X + QuestInfoHorizontalOffset,
                _visualAnchor.Y - dragonHeight - QuestInfoVerticalGap);
            return true;
        }

        private bool ShouldShowQuestInfo(PlayerCharacter owner)
        {
            if (_questInfoPreviewState == DragonQuestInfoState.Hidden
                || _isSuppressed
                || _alpha <= 0.01f)
            {
                return false;
            }

            if (ShouldSuppressForCurrentMount(owner))
            {
                return false;
            }

            return !IsExplicitDragonAction(_currentActionName);
        }

        private void TriggerBlink(int currentTime)
        {
            if (_isSuppressed)
            {
                return;
            }

            EnsureAuxiliaryAnimationsLoaded();
            if (_dragonBlinkAnimation == null)
            {
                return;
            }

            _lastBlinkStartTime = currentTime;
        }
    }
}
