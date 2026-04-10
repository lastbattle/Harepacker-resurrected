using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
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
            PassiveSettleEffect = 1 << 1,
            SnapEffect = 1 << 2
        }

        private sealed class DragonAnimationSet
        {
            private readonly DragonActionLoader _loader;

            public DragonAnimationSet(int jobId, DragonActionLoader loader, Dictionary<string, SkillAnimation> animations)
            {
                JobId = jobId;
                _loader = loader ?? throw new ArgumentNullException(nameof(loader));
                Animations = animations ?? throw new ArgumentNullException(nameof(animations));
            }

            public int JobId { get; }
            public Dictionary<string, SkillAnimation> Animations { get; }
            public SkillAnimation StandAnimation => GetAnimation("stand");
            public SkillAnimation MoveAnimation => GetAnimation("move");

            public bool TryGetAnimation(string actionName, out SkillAnimation animation)
            {
                animation = null;
                string resolvedActionName = _loader.ResolveClientActionName(actionName);
                if (string.IsNullOrWhiteSpace(resolvedActionName))
                {
                    return false;
                }

                if (!Animations.TryGetValue(resolvedActionName, out animation) || animation == null)
                {
                    animation = _loader.GetOrLoadAnimation(JobId, resolvedActionName);
                    if (animation != null)
                    {
                        Animations[resolvedActionName] = animation;
                    }
                }

                return animation != null;
            }

            public SkillAnimation GetAnimation(string actionName)
            {
                TryGetAnimation(actionName, out SkillAnimation animation);
                return animation;
            }
        }

        private sealed class LayerAnimationSequence
        {
            public SkillAnimation AppearAnimation { get; init; }
            public SkillAnimation DefaultAnimation { get; init; }

            public bool HasFrames => (AppearAnimation?.Frames?.Count ?? 0) > 0
                || (DefaultAnimation?.Frames?.Count ?? 0) > 0;
        }

        private enum DragonQuestInfoState
        {
            PreStart = 0,
            RewardReady = 1,
            Active = 2,
            Hidden = 6
        }

        private readonly GraphicsDevice _device;
        private readonly DragonActionLoader _actionLoader;
        private readonly Dictionary<int, DragonAnimationSet> _animationCache = new();
        private readonly Dictionary<int, LayerAnimationSequence> _questInfoAnimationCache = new();
        private SkillAnimation _dragonFuryAnimation;
        private SkillAnimation _dragonBlinkAnimation;
        private LayerAnimationSequence _questInfoAnimation;
        private static readonly HashSet<int> HiddenDragonMountIds = new()
        {
            1902040,
            1902041,
            1902042
        };
        private Func<MapInfo> _currentMapInfoProvider;
        private Func<int?> _questInfoStateProvider;
        private Func<bool> _dragonFuryVisibleProvider;
        private Func<int?> _ownerPhaseActionAlphaProvider;
        private DragonAnimationSet _currentSet;
        private string _currentActionName;
        private int _currentActionStartTime;
        private string _observedOwnerActionName;
        private bool _facingRight = true;
        private Vector2 _worldAnchor;
        private Vector2 _visualAnchor;
        private Vector2 _followVelocity;
        private float _alpha;
        private float _ownerPhaseActionAlpha = 1f;
        private Color _actionLayerColor = Color.White;
        private float _dragonFuryAlpha;
        private float _questInfoAlpha;
        private int _lastUpdateTime = int.MinValue;
        private int _lastBlinkStartTime = int.MinValue;
        private int _questInfoAnimationStartTime = int.MinValue;
        private bool _isSuppressed;
        private bool _hasActiveOneTimeAction;
        private bool _isFollowActive;
        private int _activeFollowReleaseStableFrames;
        private int _activeVerticalFollowState;
        private int _activeVerticalCheckCount;
        private DragonQuestInfoState _questInfoPreviewState = DragonQuestInfoState.Hidden;

        private const float GroundSideOffset = 42f;
        private const float GroundVerticalOffset = -12f;
        private const float LadderSideOffset = 34f;
        private const float LadderVerticalOffset = 18f;
        private const float DragonKeyDownBarHalfWidth = 36f;
        private const float DragonKeyDownBarVerticalGap = 30f;
        private const float FollowMinSpeed = 18f;
        private const float ActiveFollowSnapWidth = 300f;
        private const float ActiveFollowSnapHeight = 200f;
        private const float ActiveFollowDistanceX = 5f;
        private const float ActiveFollowStepX = 7f;
        private const float ActiveFollowVerticalCheckDistance = 30f;
        private const float ActiveFollowImmediateVerticalDistance = 100f;
        private const float ActiveFollowVerticalStepDivisor = 10f;
        private const float ActiveFollowVerticalStepCap = 17f;
        private const int ActiveFollowVerticalCheckFrames = 5;
        private const int ActiveFollowReleaseStableFrameCount = 6;
        private const float ActiveFollowEngageDistance = ActiveFollowDistanceX + ActiveFollowStepX;
        private const float ActiveFollowReleaseDistance = ActiveFollowDistanceX;
        private const float PassiveHorizontalResponse = 3.2f;
        private const float PassiveVerticalResponse = 3.8f;
        private const float PassiveHorizontalForceScale = 0.3f;
        private const float PassiveVerticalForceScale = 0.34f;
        private const float PassiveMaxHorizontalSpeed = 92f;
        private const float PassiveMaxVerticalSpeed = 108f;
        private const float PassiveArrivalDistance = 4f;
        private const float PassiveHoldDistance = ActiveFollowDistanceX;
        private const float PassiveVerticalHoldDistance = 5f;
        private const int ClientVecCtrlPassiveStepMilliseconds = 30;
        private const float QuestInfoHorizontalOffset = 20f;
        private const float QuestInfoVerticalGap = 15f;
        private const int DragonBlinkEffectStringPoolId = 0x0B6B;
        private const int DragonFuryEffectStringPoolId = 0x15DA;
        private const int DragonQuestInfoEffectStringPoolId = 0x19BC;
        private static readonly string[] ExactClientQuestInfoFormatTokens =
        {
            "%d",
            "%i",
            "%u",
            "%ld",
            "%li",
            "%lu",
            "%hd",
            "%hi",
            "%hu"
        };
        private static readonly string[] CompatibilityQuestInfoFormatTokens =
        {
            "{0}",
            "%d",
            "%i",
            "%u",
            "%ld",
            "%li",
            "%lu",
            "%hd",
            "%hi",
            "%hu",
            "%s"
        };

        public DragonCompanionRuntime(GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _actionLoader = new DragonActionLoader(device);
        }

        public void SetCurrentMapInfoProvider(Func<MapInfo> currentMapInfoProvider)
        {
            _currentMapInfoProvider = currentMapInfoProvider;
        }

        public void SetQuestInfoStateProvider(Func<int?> questInfoStateProvider)
        {
            _questInfoStateProvider = questInfoStateProvider;
        }

        public void SetDragonFuryVisibleProvider(Func<bool> dragonFuryVisibleProvider)
        {
            _dragonFuryVisibleProvider = dragonFuryVisibleProvider;
        }

        public void SetOwnerPhaseActionAlphaProvider(Func<int?> ownerPhaseActionAlphaProvider)
        {
            _ownerPhaseActionAlphaProvider = ownerPhaseActionAlphaProvider;
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
            bool suppressedForMap = ShouldSuppressForCurrentMap();
            bool suppressedForMount = ShouldSuppressForCurrentMount(owner);
            _isSuppressed = suppressedForMap || suppressedForMount;
            ApplyQuestInfoState(_questInfoStateProvider?.Invoke());

            float deltaSeconds = GetDeltaSeconds(currentTime);
            UpdateFollowState(owner);
            FollowUpdateFlags followUpdate = UpdateVisualAnchor(deltaSeconds);
            if ((followUpdate & FollowUpdateFlags.PassiveSettleEffect) != 0)
            {
                TriggerBlink(owner, currentTime);
            }
            else if ((followUpdate & FollowUpdateFlags.SnapEffect) != 0)
            {
                TriggerBlink(owner, currentTime);
            }

            bool explicitActionSelected = false;
            string ownerActionName = owner.CurrentActionName;
            string explicitActionName = ResolveExplicitActionName(owner, ownerActionName, animationSet);
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

            _alpha = ResolveClientLayerAlpha(!_isSuppressed);
            int? ownerPhaseAlpha = _ownerPhaseActionAlphaProvider?.Invoke();
            _actionLayerColor = ResolveClientActionLayerColorAfterOwnerUpdate(
                ResolveClientActionLayerColor(Color.White, _alpha, null),
                ownerUpdateVisible: !suppressedForMap,
                hasLocalUser: ownerPhaseAlpha.HasValue,
                ownerMatchesLocalPhase: !ownerPhaseAlpha.HasValue,
                ownerPhaseAlpha: ownerPhaseAlpha ?? 255,
                hasSpecialDragonRidingMount: suppressedForMount);
            _ownerPhaseActionAlpha = _actionLayerColor.A / 255f;
            _hasActiveOneTimeAction = HasClientOwnedOneTimeAction(explicitActionName, currentTime);
            UpdateAuxiliaryLayers(owner, currentTime);
        }

        internal bool CanOwnSkillCast(PlayerCharacter owner)
        {
            if (owner?.Build == null || !TryResolveDragonJob(owner.Build.Job, out int dragonJob))
            {
                return false;
            }

            if (ShouldSuppress(owner))
            {
                return false;
            }

            return GetOrLoadAnimationSet(dragonJob) != null;
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
            frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, screenX, screenY, _actionLayerColor * frameAlpha, !_facingRight, null);
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
            _ownerPhaseActionAlpha = 1f;
            _actionLayerColor = Color.White;
            _dragonFuryAlpha = 0f;
            _questInfoAlpha = 0f;
            _lastUpdateTime = int.MinValue;
            _lastBlinkStartTime = int.MinValue;
            _questInfoAnimationStartTime = int.MinValue;
            _isSuppressed = false;
            _hasActiveOneTimeAction = false;
            _isFollowActive = false;
            _activeFollowReleaseStableFrames = 0;
            _activeVerticalFollowState = 0;
            _activeVerticalCheckCount = 0;
        }

        internal bool ClearClientOwnedOneTimeActionOnSkillCancel(PlayerCharacter owner, int currentTime)
        {
            if (owner == null || _currentSet == null)
            {
                return false;
            }

            _facingRight = owner.FacingRight;
            _hasActiveOneTimeAction = false;
            _observedOwnerActionName = owner.CurrentActionName;

            if (!IsExplicitDragonAction(_currentActionName))
            {
                return false;
            }

            string baseActionName = ResolveBaseActionName(owner, _currentSet);
            SetCurrentAction(baseActionName, currentTime, preserveStartTimeWhenUnchanged: true);
            return true;
        }

        public void SetQuestInfoPreviewState(int? state)
        {
            ApplyQuestInfoState(state);
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

            return $"Dragon action: {_currentActionName ?? "none"}, follow: {(_isFollowActive ? "active" : "passive")}, fury: {IsDragonFuryVisible()}, suppressed: {_isSuppressed}, action alpha: {Math.Round(_ownerPhaseActionAlpha * 255f)}, quest info: {questInfoLabel}, owner: {ownerName ?? "Unknown"}";
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
            var animations = new Dictionary<string, SkillAnimation>(StringComparer.OrdinalIgnoreCase);
            foreach (string baseActionName in new[] { "stand", "move" })
            {
                SkillAnimation animation = _actionLoader.GetOrLoadAnimation(dragonJob, baseActionName);
                if (animation != null)
                {
                    animations[animation.Name] = animation;
                }
            }

            if (animations.Count == 0)
            {
                foreach (string actionName in _actionLoader.EnumerateKnownActionNames(dragonJob))
                {
                    SkillAnimation animation = _actionLoader.GetOrLoadAnimation(dragonJob, actionName);
                    if (animation != null)
                    {
                        animations[animation.Name] = animation;
                    }
                }
            }

            return animations.Count == 0
                ? null
                : new DragonAnimationSet(dragonJob, _actionLoader, animations);
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

        private static string ResolveExplicitActionName(PlayerCharacter owner, string ownerActionName, DragonAnimationSet animationSet)
        {
            if (owner?.TryGetCurrentClientRawActionCode(out int rawActionCode) == true
                && DragonActionLoader.TryGetClientActionNameFromRawActionCode(rawActionCode, out string rawActionName)
                && animationSet.TryGetAnimation(rawActionName, out _)
                && IsExplicitDragonAction(rawActionName))
            {
                return rawActionName;
            }

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

        private string ResolveBaseActionName(PlayerCharacter owner, DragonAnimationSet animationSet)
        {
            bool useMove = ShouldUseMoveAction(owner);

            if (useMove && animationSet.MoveAnimation != null)
            {
                return "move";
            }

            return animationSet.StandAnimation != null ? "stand" : animationSet.Animations.Keys.FirstOrDefault();
        }

        private bool ShouldUseMoveAction(PlayerCharacter owner)
        {
            if (owner?.State is PlayerState.Walking
                or PlayerState.Jumping
                or PlayerState.Falling
                or PlayerState.Ladder
                or PlayerState.Rope
                or PlayerState.Swimming
                or PlayerState.Flying)
            {
                return true;
            }

            if (_isFollowActive)
            {
                return true;
            }

            if (Math.Abs(_followVelocity.X) > FollowMinSpeed
                || Math.Abs(_followVelocity.Y) > FollowMinSpeed)
            {
                return true;
            }

            return Math.Abs(_worldAnchor.X - _visualAnchor.X) > PassiveHoldDistance
                   || Math.Abs(_worldAnchor.Y - _visualAnchor.Y) > PassiveVerticalHoldDistance;
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

        private static bool ShouldLoopExplicitAction(string actionName)
        {
            return DragonActionLoader.IsClientHeldActionName(actionName);
        }

        private bool HasClientOwnedOneTimeAction(string explicitActionName, int currentTime)
        {
            if (!IsExplicitDragonAction(_currentActionName))
            {
                return false;
            }

            if (string.Equals(explicitActionName, _currentActionName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!_currentSet.TryGetAnimation(_currentActionName, out SkillAnimation animation) || animation == null)
            {
                return false;
            }

            int elapsed = Math.Max(0, currentTime - _currentActionStartTime);
            return !animation.IsComplete(elapsed);
        }

        private bool ShouldSuppressForCurrentMap()
        {
            MapInfo mapInfo = _currentMapInfoProvider?.Invoke();
            return ShouldSuppressForMapInfo(mapInfo);
        }

        private static bool ShouldSuppressForMapInfo(MapInfo mapInfo)
        {
            return MapSimulator.SuppressesDragonPresentation(mapInfo);
        }

        private bool ShouldSuppress(PlayerCharacter owner)
        {
            return ShouldSuppressForCurrentMap() || ShouldSuppressForCurrentMount(owner);
        }

        private static bool ShouldSuppressForCurrentMount(PlayerCharacter owner)
        {
            if (!TryGetCurrentMountItemId(owner, out int mountItemId))
            {
                return false;
            }

            return ShouldHideDragonActionLayerForMountItemId(mountItemId);
        }

        internal static bool ShouldHideDragonActionLayerForMountItemId(int mountItemId)
        {
            return HiddenDragonMountIds.Contains(mountItemId);
        }

        private static bool HasMountedVehicle(PlayerCharacter owner)
        {
            return TryGetCurrentMountItemId(owner, out _);
        }

        private static bool TryGetCurrentMountItemId(PlayerCharacter owner, out int mountItemId)
        {
            mountItemId = 0;

            if (owner?.Build?.Equipment == null
                || !owner.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart)
                || mountPart?.ItemId is not int resolvedMountItemId
                || resolvedMountItemId <= 0)
            {
                return false;
            }

            mountItemId = resolvedMountItemId;
            return true;
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
            bool shouldEngageActiveFollow = ownerInMotion
                || ownerHasMomentum
                || horizontalDelta > ActiveFollowEngageDistance
                || verticalDelta > ActiveFollowVerticalCheckDistance;
            bool shouldHoldActiveFollow = ownerInMotion
                || ownerHasMomentum
                || horizontalDelta > ActiveFollowReleaseDistance
                || verticalDelta > ActiveFollowVerticalCheckDistance;

            if (_isFollowActive)
            {
                if (shouldHoldActiveFollow)
                {
                    _activeFollowReleaseStableFrames = 0;
                    return;
                }

                _activeFollowReleaseStableFrames++;
                _isFollowActive = _activeFollowReleaseStableFrames < ActiveFollowReleaseStableFrameCount;
            }
            else
            {
                _activeFollowReleaseStableFrames = 0;
                _isFollowActive = shouldEngageActiveFollow;
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

            double velocityX = _followVelocity.X;
            double velocityY = _followVelocity.Y;
            FollowUpdateFlags result = FollowUpdateFlags.None;

            if (_isFollowActive)
            {
                result |= UpdateActiveVisualAnchor(ref velocityX, ref velocityY);
            }
            else
            {
                float passiveDeltaSeconds = ResolveClientPassiveFollowStepSeconds(deltaSeconds);
                bool hadPassiveTravel = HasPassiveTravel(
                    _visualAnchor,
                    _worldAnchor,
                    _followVelocity);

                UpdatePassiveFollowAxis(
                    ref _visualAnchor.X,
                    _worldAnchor.X,
                    ref velocityX,
                    passiveDeltaSeconds,
                    PassiveHorizontalResponse,
                    PassiveMaxHorizontalSpeed,
                    CVecCtrl.WalkAcceleration * PassiveHorizontalForceScale,
                    PassiveHoldDistance,
                    PassiveArrivalDistance);

                UpdatePassiveFollowAxis(
                    ref _visualAnchor.Y,
                    _worldAnchor.Y,
                    ref velocityY,
                    passiveDeltaSeconds,
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

        internal static float ResolveClientPassiveFollowStepSeconds(float frameDeltaSeconds)
        {
            _ = frameDeltaSeconds;
            return ClientVecCtrlPassiveStepMilliseconds / 1000f;
        }

        private FollowUpdateFlags UpdateActiveVisualAnchor(ref double velocityX, ref double velocityY)
        {
            float deltaX = _worldAnchor.X - _visualAnchor.X;
            float deltaY = _worldAnchor.Y - _visualAnchor.Y;
            if (Math.Abs(deltaX) > ActiveFollowSnapWidth || Math.Abs(deltaY) > ActiveFollowSnapHeight)
            {
                _visualAnchor = _worldAnchor;
                _activeVerticalFollowState = 0;
                _activeVerticalCheckCount = 0;
                velocityX = 0d;
                velocityY = 0d;
                return FollowUpdateFlags.SnappedToTarget | FollowUpdateFlags.SnapEffect;
            }

            float nextX = ResolveClientActiveFollowHorizontalStep(
                _visualAnchor.X,
                _worldAnchor.X,
                out velocityX);
            float nextY = ResolveClientActiveFollowVerticalStep(
                _visualAnchor.Y,
                _worldAnchor.Y,
                ref _activeVerticalFollowState,
                ref _activeVerticalCheckCount,
                out velocityY);

            _visualAnchor = new Vector2(nextX, nextY);
            return FollowUpdateFlags.None;
        }

        internal static float ResolveClientActiveFollowHorizontalStep(float currentX, float targetX, out double velocityX)
        {
            if (targetX > currentX + ActiveFollowDistanceX)
            {
                velocityX = 1d;
                return Math.Min(targetX - ActiveFollowDistanceX, currentX + ActiveFollowStepX);
            }

            if (targetX < currentX - ActiveFollowDistanceX)
            {
                velocityX = -1d;
                return Math.Max(targetX + ActiveFollowDistanceX, currentX - ActiveFollowStepX);
            }

            velocityX = 0d;
            return currentX;
        }

        internal static float ResolveClientActiveFollowVerticalStep(
            float currentY,
            float targetY,
            ref int followingState,
            ref int checkCount,
            out double velocityY)
        {
            float deltaY = targetY - currentY;
            float absoluteDeltaY = Math.Abs(deltaY);
            if (followingState == 0)
            {
                if (absoluteDeltaY > ActiveFollowVerticalCheckDistance)
                {
                    checkCount++;
                    if (checkCount >= ActiveFollowVerticalCheckFrames)
                    {
                        followingState = 1;
                    }
                }
                else
                {
                    checkCount = 0;
                }
            }
            else
            {
                checkCount = 0;
            }

            bool shouldMoveVertically = followingState != 0
                || ShouldUseImmediateActiveVerticalFollow(deltaY);
            if (!shouldMoveVertically)
            {
                velocityY = 0d;
                return currentY;
            }

            if (followingState < 0 && absoluteDeltaY <= PassiveArrivalDistance)
            {
                followingState = 0;
                velocityY = deltaY >= 0f ? 1d : -1d;
                return currentY;
            }

            float verticalStep = MathF.Min(ActiveFollowVerticalStepCap, absoluteDeltaY / ActiveFollowVerticalStepDivisor) + 1f;
            float nextY = deltaY >= 0f
                ? Math.Min(targetY, currentY + verticalStep)
                : Math.Max(targetY, currentY - verticalStep);
            followingState = Math.Abs(targetY - nextY) <= PassiveArrivalDistance ? -1 : 1;
            velocityY = deltaY >= 0f ? 1d : -1d;
            return nextY;
        }

        internal static bool ShouldUseImmediateActiveVerticalFollow(float deltaY)
        {
            return Math.Abs(deltaY) > ActiveFollowImmediateVerticalDistance;
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

            double directedForce = Math.Max(force * Math.Max(0.1f, responseScale), CVecCtrl.WalkAcceleration);
            double directedMaxSpeed = Math.Max(arrivalDistance, maxSpeed * Math.Max(0.1f, responseScale));
            bool movingTowardTarget = Math.Sign(delta) == Math.Sign(velocity) || Math.Abs(velocity) <= FollowMinSpeed;
            if (!movingTowardTarget)
            {
                CVecCtrl.DecSpeed(ref velocity, Math.Max(directedForce, CVecCtrl.WalkDeceleration), PhysicsConstants.Instance.DefaultMass, 0d, deltaSeconds);
            }
            else
            {
                double targetVelocity = Math.Sign(delta) * directedMaxSpeed;
                CVecCtrl.AccSpeed(ref velocity, Math.Abs(directedForce), PhysicsConstants.Instance.DefaultMass, Math.Abs(targetVelocity), deltaSeconds);
                velocity = Math.Sign(delta) * Math.Abs(velocity);
            }

            position += (float)(velocity * deltaSeconds);
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

            frameAlpha = ResolveFrameAlpha(frame, frameElapsedMs);
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

        internal static float ResolveClientLayerAlpha(bool shouldShow)
        {
            return shouldShow ? 1f : 0f;
        }

        internal static float ResolveOwnerPhaseClampedActionLayerAlpha(float actionLayerAlpha, int? ownerPhaseAlpha)
        {
            return ResolveClientActionLayerColor(Color.White, actionLayerAlpha, ownerPhaseAlpha).A / 255f;
        }

        internal static Color ResolveClientActionLayerColor(Color baseColor, float actionLayerAlpha, int? ownerPhaseAlpha)
        {
            byte actionAlpha = (byte)Math.Round(MathHelper.Clamp(actionLayerAlpha, 0f, 1f) * 255f);
            return ResolveClientActionLayerColorCap(new Color(baseColor.R, baseColor.G, baseColor.B, actionAlpha), ownerPhaseAlpha);
        }

        internal static Color ResolveClientActionLayerColorCap(Color currentColor, int? ownerPhaseAlpha)
        {
            if (!ownerPhaseAlpha.HasValue)
            {
                return currentColor;
            }

            byte cappedAlpha = (byte)Math.Clamp(ownerPhaseAlpha.Value, 0, 255);
            return currentColor.A > cappedAlpha
                ? new Color(currentColor.R, currentColor.G, currentColor.B, cappedAlpha)
                : currentColor;
        }

        internal static Color ResolveClientActionLayerColorAfterOwnerUpdate(
            Color currentColor,
            bool ownerUpdateVisible,
            bool hasLocalUser,
            bool ownerMatchesLocalPhase,
            int ownerPhaseAlpha,
            bool hasSpecialDragonRidingMount)
        {
            if (!ownerUpdateVisible)
            {
                return currentColor;
            }

            if (!hasLocalUser || ownerMatchesLocalPhase)
            {
                return hasSpecialDragonRidingMount
                    ? currentColor
                    : new Color(currentColor.R, currentColor.G, currentColor.B, byte.MaxValue);
            }

            return ResolveClientActionLayerColorCap(currentColor, ownerPhaseAlpha);
        }

        private void UpdateAuxiliaryLayers(PlayerCharacter owner, int currentTime)
        {
            EnsureAuxiliaryAnimationsLoaded();

            bool shouldShowFury = ShouldShowDragonFury(owner);
            _dragonFuryAlpha = ResolveClientLayerAlpha(shouldShowFury);
            UpdateQuestInfoLayer(owner, currentTime);
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
            _dragonFuryAnimation ??= LoadEffectAnimationFromStringPool(
                DragonFuryEffectStringPoolId,
                fallbackEffectUol: "Effect/BasicEff.img/dragonFury",
                loop: true);
            _dragonBlinkAnimation ??= LoadEffectAnimationFromStringPool(
                DragonBlinkEffectStringPoolId,
                fallbackEffectUol: "Effect/BasicEff.img/dragonBlink",
                loop: false);
        }

        private void UpdateQuestInfoLayer(PlayerCharacter owner, int currentTime)
        {
            if (!TryResolveQuestInfoAnimation(out LayerAnimationSequence animation)
                || !ShouldShowQuestInfo(owner))
            {
                _questInfoAlpha = ResolveClientLayerAlpha(false);
                return;
            }

            if (!ReferenceEquals(_questInfoAnimation, animation))
            {
                _questInfoAnimation = animation;
                _questInfoAnimationStartTime = currentTime;
            }

            _questInfoAlpha = ResolveClientLayerAlpha(true);
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
                || !TryResolveLayerAnimationFrame(_questInfoAnimation, _questInfoAnimationStartTime, currentTime, out _, out _)
                || !TryResolveQuestInfoAnchor(currentTime, out Vector2 anchor))
            {
                return;
            }

            DrawLayerAnimationSequence(
                _questInfoAnimation,
                _questInfoAnimationStartTime,
                _questInfoAlpha,
                spriteBatch,
                skeletonRenderer,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                currentTime,
                anchor);
        }

        private SkillAnimation LoadEffectAnimationFromStringPool(int stringPoolId, string fallbackEffectUol, bool loop)
        {
            string effectUol = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackEffectUol);
            return LoadEffectAnimation(effectUol, loop);
        }

        private SkillAnimation LoadEffectAnimation(string effectUol, bool loop)
        {
            if (!TryResolveWzAssetUol(effectUol, defaultCategory: "Effect", out string category, out string imageName, out string propertyPath))
            {
                return null;
            }

            WzImage image = global::HaCreator.Program.FindImage(category, imageName);
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

        private SkillAnimation LoadAnimation(WzSubProperty actionNode)
        {
            if (actionNode == null)
            {
                return null;
            }

            List<SkillFrame> frames = new();
            foreach (WzCanvasProperty canvas in actionNode.WzProperties.OfType<WzCanvasProperty>().OrderBy(frame => ParseFrameIndex(frame.Name)))
            {
                WzCanvasProperty metadataCanvas = ResolveMetadataCanvas(canvas);
                IDXObject texture = LoadAnimationTexture(metadataCanvas);
                if (texture == null)
                {
                    continue;
                }

                WzVectorProperty origin = metadataCanvas["origin"] as WzVectorProperty;
                frames.Add(new SkillFrame
                {
                    Texture = texture,
                    Origin = new Point(origin?.X.Value ?? 0, origin?.Y.Value ?? 0),
                    Delay = Math.Max(1, GetIntValue(metadataCanvas["delay"]) ?? 100),
                    Bounds = ResolveFrameBounds(metadataCanvas, texture),
                    AlphaStart = Math.Clamp(GetIntValue(metadataCanvas["a0"]) ?? 255, 0, 255),
                    AlphaEnd = Math.Clamp(GetIntValue(metadataCanvas["a1"]) ?? 255, 0, 255)
                });
            }

            if (frames.Count == 0)
            {
                return null;
            }

            SkillAnimation animation = new()
            {
                Name = actionNode.Name,
                PositionCode = GetIntValue(actionNode["pos"])
            };
            animation.Frames.AddRange(frames);
            animation.CalculateDuration();
            return animation;
        }

        private IDXObject LoadAnimationTexture(WzCanvasProperty canvas)
        {
            if (canvas?.PngProperty == null)
            {
                return null;
            }

            try
            {
                System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
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

        private static WzCanvasProperty ResolveMetadataCanvas(WzCanvasProperty canvas)
        {
            return canvas;
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

        internal static bool TryResolveWzAssetUol(
            string uol,
            string defaultCategory,
            out string category,
            out string imageName,
            out string propertyPath)
        {
            category = defaultCategory;
            imageName = null;
            propertyPath = null;

            if (string.IsNullOrWhiteSpace(uol))
            {
                return false;
            }

            string normalized = uol.Replace('\\', '/').Trim('/');
            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            int imageIndex = Array.FindIndex(segments, segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
            if (imageIndex < 0)
            {
                return false;
            }

            if (imageIndex > 0)
            {
                category = segments[0];
            }

            imageName = segments[imageIndex];
            propertyPath = string.Join("/", segments.Skip(imageIndex + 1));
            return !string.IsNullOrWhiteSpace(imageName) && !string.IsNullOrWhiteSpace(propertyPath);
        }

        internal static bool ShouldHideAuxiliaryLayer(bool hasActiveOneTimeAction)
        {
            return hasActiveOneTimeAction;
        }

        internal static bool ShouldShowAuxiliaryLayer(bool isSuppressed, float actionLayerAlpha, bool hasMountedVehicle, bool hasActiveOneTimeAction)
        {
            if (isSuppressed || actionLayerAlpha <= 0.01f || hasMountedVehicle)
            {
                return false;
            }

            return !ShouldHideAuxiliaryLayer(hasActiveOneTimeAction);
        }

        private bool ShouldShowDragonFury(PlayerCharacter owner)
        {
            if (_dragonFuryAnimation == null
                || !IsDragonFuryVisible()
                || !ShouldShowAuxiliaryLayer(_isSuppressed, _alpha, HasMountedVehicle(owner), _hasActiveOneTimeAction))
            {
                return false;
            }

            return true;
        }

        private bool IsDragonFuryVisible()
        {
            return _dragonFuryVisibleProvider?.Invoke() == true;
        }

        private void DrawLayerAnimationSequence(
            LayerAnimationSequence animationSequence,
            int animationStartTime,
            float alpha,
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime,
            Vector2 anchor)
        {
            if (!TryResolveLayerAnimationFrame(animationSequence, animationStartTime, currentTime, out SkillFrame frame, out float frameAlpha)
                || frame?.Texture == null)
            {
                return;
            }

            float resolvedAlpha = alpha * frameAlpha;
            if (resolvedAlpha <= 0.01f)
            {
                return;
            }

            int screenX = (int)anchor.X - mapShiftX + centerX;
            int screenY = (int)anchor.Y - mapShiftY + centerY;
            frame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, screenX, screenY, Color.White * resolvedAlpha, !_facingRight, null);
        }

        private static bool TryResolveLayerAnimationFrame(
            LayerAnimationSequence animationSequence,
            int animationStartTime,
            int currentTime,
            out SkillFrame frame,
            out float frameAlpha)
        {
            frame = null;
            frameAlpha = 0f;

            if (animationSequence == null)
            {
                return false;
            }

            int elapsed = Math.Max(0, currentTime - Math.Max(0, animationStartTime));
            SkillAnimation appearAnimation = animationSequence.AppearAnimation;
            if (appearAnimation?.Frames?.Count > 0)
            {
                int appearDuration = Math.Max(1, appearAnimation.TotalDuration);
                if (elapsed < appearDuration
                    && appearAnimation.TryGetFrameAtTime(elapsed, out frame, out int appearFrameElapsedMs))
                {
                    frameAlpha = ResolveFrameAlpha(frame, appearFrameElapsedMs);
                    return frameAlpha > 0.01f;
                }

                elapsed = Math.Max(0, elapsed - appearDuration);
            }

            SkillAnimation defaultAnimation = animationSequence.DefaultAnimation;
            if (defaultAnimation?.Frames?.Count > 0
                && defaultAnimation.TryGetFrameAtTime(elapsed, out frame, out int defaultFrameElapsedMs))
            {
                frameAlpha = ResolveFrameAlpha(frame, defaultFrameElapsedMs);
                return frameAlpha > 0.01f;
            }

            if (appearAnimation?.Frames?.Count > 0
                && appearAnimation.TryGetFrameAtTime(Math.Max(0, appearAnimation.TotalDuration - 1), out frame, out int finalFrameElapsedMs))
            {
                frameAlpha = ResolveFrameAlpha(frame, finalFrameElapsedMs);
                return frameAlpha > 0.01f;
            }

            return false;
        }

        private bool TryResolveQuestInfoAnimation(out LayerAnimationSequence animation)
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

            string effectUol = ResolveQuestInfoEffectUol((DragonQuestInfoState)questState);
            if (string.IsNullOrWhiteSpace(effectUol))
            {
                return false;
            }

            animation = LoadLayerAnimationSequence(effectUol);
            _questInfoAnimationCache[questState] = animation;
            return animation?.HasFrames == true;
        }

        private static string ResolveQuestInfoEffectUol(DragonQuestInfoState questState)
        {
            if (TryResolveQuestInfoEffectUolFromStringPool((int)questState, out string effectUol))
            {
                return effectUol;
            }

            return questState switch
            {
                DragonQuestInfoState.PreStart => "Effect/BasicEff.img/QuestAlert",
                DragonQuestInfoState.RewardReady => "Effect/BasicEff.img/QuestAlert2",
                DragonQuestInfoState.Active => "Effect/BasicEff.img/QuestAlert3",
                _ => null
            };
        }

        internal static bool TryResolveQuestInfoEffectUolFromStringPool(int questState, out string effectUol)
        {
            effectUol = null;

            if (!MapleStoryStringPool.TryGet(DragonQuestInfoEffectStringPoolId, out string rawFormat)
                || string.IsNullOrWhiteSpace(rawFormat))
            {
                return false;
            }

            if (TryFormatQuestInfoEffectUolExactClient(rawFormat, questState, out string directEffectUol)
                && DoesWzEffectAssetExist(directEffectUol))
            {
                effectUol = directEffectUol;
                return true;
            }

            if (TryFormatQuestInfoEffectUolCompatibility(rawFormat, questState, out string compatibilityEffectUol)
                && DoesWzEffectAssetExist(compatibilityEffectUol))
            {
                effectUol = compatibilityEffectUol;
                return true;
            }

            return false;
        }

        internal static bool TryFormatQuestInfoEffectUolExactClient(string rawFormat, int questState, out string effectUol)
        {
            effectUol = null;

            if (questState is < 0 or > 2 || string.IsNullOrWhiteSpace(rawFormat))
            {
                return false;
            }

            if (!TryFormatQuestInfoEffectUolTemplate(rawFormat, questState.ToString(), ExactClientQuestInfoFormatTokens, out string formatted)
                || !LooksLikeQuestInfoEffectUol(formatted))
            {
                return false;
            }

            effectUol = formatted;
            return true;
        }

        internal static bool TryFormatQuestInfoEffectUolCompatibility(string rawFormat, int questState, out string effectUol)
        {
            effectUol = null;

            string suffix = questState switch
            {
                0 => string.Empty,
                1 => "2",
                2 => "3",
                _ => null
            };
            if (suffix == null || string.IsNullOrWhiteSpace(rawFormat))
            {
                return false;
            }

            string normalized = rawFormat.Trim().Replace('\\', '/');
            if (!TryFormatQuestInfoEffectUolTemplate(normalized, suffix, CompatibilityQuestInfoFormatTokens, out string formatted))
            {
                if (questState == 0 && LooksLikeQuestInfoEffectUol(normalized))
                {
                    formatted = normalized;
                }
                else
                {
                    return false;
                }
            }

            if (!LooksLikeQuestInfoEffectUol(formatted))
            {
                return false;
            }

            effectUol = formatted;
            return true;
        }

        internal static bool TryFormatQuestInfoEffectUolTemplate(string rawFormat, string replacement, out string formatted)
        {
            return TryFormatQuestInfoEffectUolTemplate(rawFormat, replacement, CompatibilityQuestInfoFormatTokens, out formatted);
        }

        private static bool TryFormatQuestInfoEffectUolTemplate(
            string rawFormat,
            string replacement,
            IReadOnlyList<string> supportedTokens,
            out string formatted)
        {
            formatted = null;
            if (string.IsNullOrWhiteSpace(rawFormat))
            {
                return false;
            }

            string normalized = rawFormat.Trim().Replace('\\', '/');
            foreach (string token in supportedTokens ?? Array.Empty<string>())
            {
                int tokenIndex = normalized.IndexOf(token, StringComparison.Ordinal);
                if (tokenIndex < 0)
                {
                    continue;
                }

                formatted = normalized.Remove(tokenIndex, token.Length).Insert(tokenIndex, replacement ?? string.Empty);
                return true;
            }

            return false;
        }

        private static bool DoesWzEffectAssetExist(string effectUol)
        {
            if (!TryResolveWzAssetUol(effectUol, defaultCategory: "Effect", out string category, out string imageName, out string propertyPath))
            {
                return false;
            }

            WzImage image = global::HaCreator.Program.FindImage(category, imageName);
            if (image == null)
            {
                return false;
            }

            return image[propertyPath] is WzSubProperty;
        }

        private static bool LooksLikeQuestInfoEffectUol(string effectUol)
        {
            if (string.IsNullOrWhiteSpace(effectUol))
            {
                return false;
            }

            string normalized = effectUol.Trim().Replace('\\', '/');
            return normalized.StartsWith("Effect/", StringComparison.OrdinalIgnoreCase)
                   && normalized.Contains(".img/", StringComparison.OrdinalIgnoreCase)
                   && normalized.Contains("QuestAlert", StringComparison.OrdinalIgnoreCase);
        }

        private LayerAnimationSequence LoadLayerAnimationSequence(string effectUol)
        {
            if (!TryResolveWzAssetUol(effectUol, defaultCategory: "Effect", out string category, out string imageName, out string propertyPath))
            {
                return null;
            }

            WzImage image = global::HaCreator.Program.FindImage(category, imageName);
            if (image == null)
            {
                return null;
            }

            WzImageProperty property = image[propertyPath];
            if (property is not WzSubProperty subProperty)
            {
                return null;
            }

            SkillAnimation appearAnimation = subProperty["Appear"] is WzSubProperty appearProperty
                ? LoadAnimation(appearProperty)
                : null;
            SkillAnimation defaultAnimation = subProperty["Default"] is WzSubProperty defaultProperty
                ? LoadAnimation(defaultProperty)
                : null;

            if (appearAnimation != null)
            {
                appearAnimation.Loop = false;
                appearAnimation.CalculateDuration();
            }

            if (defaultAnimation != null)
            {
                defaultAnimation.Loop = true;
                defaultAnimation.CalculateDuration();
            }

            if ((appearAnimation?.Frames?.Count ?? 0) == 0 && (defaultAnimation?.Frames?.Count ?? 0) == 0)
            {
                SkillAnimation fallbackAnimation = LoadAnimation(subProperty);
                if (fallbackAnimation?.Frames?.Count > 0)
                {
                    fallbackAnimation.Loop = true;
                    fallbackAnimation.CalculateDuration();
                    defaultAnimation = fallbackAnimation;
                }
            }

            LayerAnimationSequence sequence = new()
            {
                AppearAnimation = appearAnimation?.Frames?.Count > 0 ? appearAnimation : null,
                DefaultAnimation = defaultAnimation?.Frames?.Count > 0 ? defaultAnimation : null
            };

            return sequence.HasFrames ? sequence : null;
        }

        private bool TryResolveQuestInfoAnchor(int currentTime, out Vector2 anchor)
        {
            anchor = Vector2.Zero;

            if (!TryResolveCurrentFrame(currentTime, out SkillFrame frame, out _)
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

            MapInfo mapInfo = _currentMapInfoProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(FieldInteractionRestrictionEvaluator.GetQuestAlertRestrictionMessage(mapInfo?.fieldLimit ?? 0)))
            {
                return false;
            }

            return ShouldShowAuxiliaryLayer(_isSuppressed, _alpha, HasMountedVehicle(owner), _hasActiveOneTimeAction);
        }

        private void TriggerBlink(PlayerCharacter owner, int currentTime)
        {
            if (_isSuppressed || HasMountedVehicle(owner))
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

        private void ApplyQuestInfoState(int? state)
        {
            DragonQuestInfoState nextState = state switch
            {
                0 => DragonQuestInfoState.PreStart,
                1 => DragonQuestInfoState.RewardReady,
                2 => DragonQuestInfoState.Active,
                _ => DragonQuestInfoState.Hidden
            };

            if (_questInfoPreviewState == nextState)
            {
                return;
            }

            _questInfoPreviewState = nextState;
            _questInfoAnimation = null;
            _questInfoAlpha = 0f;
            _questInfoAnimationStartTime = int.MinValue;
        }
    }
}
