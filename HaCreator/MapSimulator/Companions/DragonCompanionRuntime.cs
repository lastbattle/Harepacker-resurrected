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

        internal readonly struct OwnerPhaseContext
        {
            public OwnerPhaseContext(bool hasLocalUser, bool ownerMatchesLocalPhase, int phaseAlpha)
            {
                HasLocalUser = hasLocalUser;
                OwnerMatchesLocalPhase = ownerMatchesLocalPhase;
                PhaseAlpha = Math.Clamp(phaseAlpha, 0, 255);
            }

            public bool HasLocalUser { get; }
            public bool OwnerMatchesLocalPhase { get; }
            public int PhaseAlpha { get; }

            public static OwnerPhaseContext NoLocalUser { get; } = new(false, false, 255);
        }

        internal readonly struct ClientDragonFlushTail
        {
            public ClientDragonFlushTail(byte[] keyPadStates, short left, short top, short right, short bottom)
            {
                KeyPadStates = keyPadStates ?? Array.Empty<byte>();
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public byte[] KeyPadStates { get; }
            public short Left { get; }
            public short Top { get; }
            public short Right { get; }
            public short Bottom { get; }
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
        private Func<OwnerPhaseContext> _ownerPhaseContextProvider;
        private Func<Vector2, int?> _actionLayerOwnerZProvider;
        private Func<byte?> _clientKeyPadStateProvider;
        private bool? _wrapperOwnedNoDragonSuppression;
        private DragonAnimationSet _currentSet;
        private string _currentActionName;
        private int _currentActionStartTime;
        private int _currentActionSpeed = 6;
        private string _observedOwnerActionName;
        private bool _facingRight = true;
        private Vector2 _worldAnchor;
        private Vector2 _visualAnchor;
        private Vector2 _followVelocity;
        private float _alpha;
        private float _ownerPhaseActionAlpha = 1f;
        private Color _actionLayerColor = Color.White;
        private int _actionLayerZ = -1;
        private int? _vecCtrlOwnedActionLayerOwnerZ;
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
        private int _vecCtrlWorkStepCarryMilliseconds;
        private int _vecCtrlEndUpdateActiveFlushCarryMilliseconds;
        private readonly Queue<byte[]> _pendingVecCtrlEndUpdateActiveFlushPayloads = new();
        private readonly List<MovePathElement> _clientVecCtrlMovePathBuffer = new();
        private readonly List<byte> _clientVecCtrlMovePathKeyPadStates = new();
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
        private const int ClientVecCtrlDragonMovePacketOpcode = 214;
        private const int ClientVecCtrlDragonFlushThresholdMilliseconds = 1000;
        private const int MaxPendingClientVecCtrlFlushPackets = 128;
        private const int MaxClientVecCtrlMovePathElements = 128;
        private const float QuestInfoHorizontalOffset = 20f;
        private const float QuestInfoVerticalGap = 15f;
        private const int DragonBlinkEffectStringPoolId = 0x0B6B;
        private const int DragonFuryEffectStringPoolId = 0x15DA;
        private const int DragonQuestInfoEffectStringPoolId = 0x19BC;
        private const int ClientVecCtrlLayerZBaseOffset = unchecked((int)0xC0007526);
        private const int ClientVecCtrlLayerZPageScale = 3000;
        private const int ClientVecCtrlLayerZStride = 10;
        private const int ClientVecCtrlLayerZOffsetGround = 2;
        private const int ClientVecCtrlLayerZOffsetLadder = 7;
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

        public void SetWrapperOwnedNoDragonSuppression(bool? suppressDragonPresentation)
        {
            _wrapperOwnedNoDragonSuppression = suppressDragonPresentation;
        }

        public void SetQuestInfoStateProvider(Func<int?> questInfoStateProvider)
        {
            _questInfoStateProvider = questInfoStateProvider;
        }

        public void SetDragonFuryVisibleProvider(Func<bool> dragonFuryVisibleProvider)
        {
            _dragonFuryVisibleProvider = dragonFuryVisibleProvider;
        }

        public void SetActionLayerOwnerZProvider(Func<Vector2, int?> actionLayerOwnerZProvider)
        {
            _actionLayerOwnerZProvider = actionLayerOwnerZProvider;
        }

        internal void SetClientKeyPadStateProvider(Func<byte?> clientKeyPadStateProvider)
        {
            _clientKeyPadStateProvider = clientKeyPadStateProvider;
        }

        public void SetOwnerPhaseActionAlphaProvider(Func<int?> ownerPhaseActionAlphaProvider)
        {
            _ownerPhaseContextProvider = ownerPhaseActionAlphaProvider == null
                ? null
                : () => ResolveLegacyOwnerPhaseContext(ownerPhaseActionAlphaProvider());
        }

        internal void SetOwnerPhaseContextProvider(Func<OwnerPhaseContext> ownerPhaseContextProvider)
        {
            _ownerPhaseContextProvider = ownerPhaseContextProvider;
        }

        internal static OwnerPhaseContext ResolveLegacyOwnerPhaseContext(int? ownerPhaseAlpha)
        {
            // Legacy callers only provided alpha, not local-user phase identity. Treat that as
            // unknown local-user context so we do not incorrectly force the mismatch clamp path.
            return ownerPhaseAlpha.HasValue
                ? new OwnerPhaseContext(hasLocalUser: false, ownerMatchesLocalPhase: false, ownerPhaseAlpha.Value)
                : OwnerPhaseContext.NoLocalUser;
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
            _currentActionSpeed = owner.Build.GetEffectiveWeaponAttackSpeed();
            _worldAnchor = ResolveAnchor(owner, animationSet, currentTime);
            bool suppressedForMap = ShouldSuppressForCurrentMap();
            bool suppressedForMount = ShouldSuppressForCurrentMount(owner);
            bool ownerUpdated = owner?.IsAlive == true;
            bool ownerUpdateVisible = ResolveClientOwnerUpdateVisibility(ownerUpdated, suppressedForMap);
            _isSuppressed = suppressedForMap || suppressedForMount;
            ApplyQuestInfoState(_questInfoStateProvider?.Invoke());

            float deltaSeconds = GetDeltaSeconds(currentTime);
            FollowUpdateFlags followUpdate = UpdateFollowStateAndVisualAnchor(owner, deltaSeconds);
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
                if (IsClientActionComplete(currentAnimation, _currentActionName, elapsed, _currentActionSpeed))
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
                || (IsClientActionComplete(activeAnimation, _currentActionName, Math.Max(0, currentTime - _currentActionStartTime), _currentActionSpeed)
                    && !string.Equals(ownerActionName, _currentActionName, StringComparison.OrdinalIgnoreCase))))
            {
                SetCurrentAction(baseActionName, currentTime, preserveStartTimeWhenUnchanged: true);
            }

            int ownerLayerZ = owner.GetCurrentLayerZ(currentTime);
            int resolvedOwnerLayerZ = ResolveClientDragonActionLayerOwnerZWithPersistedVecCtrl(
                ownerLayerZ,
                _actionLayerOwnerZProvider?.Invoke(_visualAnchor),
                ref _vecCtrlOwnedActionLayerOwnerZ);
            _actionLayerZ = ResolveClientDragonActionLayerZ(resolvedOwnerLayerZ);
            _alpha = ResolveClientLayerAlpha(!_isSuppressed);
            OwnerPhaseContext ownerPhaseContext = _ownerPhaseContextProvider?.Invoke() ?? OwnerPhaseContext.NoLocalUser;
            _actionLayerColor = ResolveClientActionLayerColorAfterOwnerUpdate(
                ResolveClientActionLayerColor(Color.White, _alpha, null),
                ownerUpdateVisible: ownerUpdateVisible,
                hasLocalUser: ownerPhaseContext.HasLocalUser,
                ownerMatchesLocalPhase: ownerPhaseContext.OwnerMatchesLocalPhase,
                ownerPhaseAlpha: ownerPhaseContext.PhaseAlpha,
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

            anchor = ResolveClientDragonKeyDownBarAnchor(_visualAnchor, frame);
            return true;
        }

        public void Clear()
        {
            _currentSet = null;
            _currentActionName = null;
            _currentActionStartTime = 0;
            _currentActionSpeed = 6;
            _observedOwnerActionName = null;
            _worldAnchor = Vector2.Zero;
            _visualAnchor = Vector2.Zero;
            _followVelocity = Vector2.Zero;
            _alpha = 0f;
            _ownerPhaseActionAlpha = 1f;
            _actionLayerColor = Color.White;
            _actionLayerZ = -1;
            _vecCtrlOwnedActionLayerOwnerZ = null;
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
            _vecCtrlWorkStepCarryMilliseconds = 0;
            _vecCtrlEndUpdateActiveFlushCarryMilliseconds = 0;
            _pendingVecCtrlEndUpdateActiveFlushPayloads.Clear();
            _clientVecCtrlMovePathBuffer.Clear();
            _clientVecCtrlMovePathKeyPadStates.Clear();
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

            return $"Dragon action: {_currentActionName ?? "none"}, follow: {(_isFollowActive ? "active" : "passive")}, fury: {IsDragonFuryVisible()}, suppressed: {_isSuppressed}, action alpha: {Math.Round(_ownerPhaseActionAlpha * 255f)}, quest info: {questInfoLabel}, pending vecctrl flush: {_pendingVecCtrlEndUpdateActiveFlushPayloads.Count}, owner: {ownerName ?? "Unknown"}";
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
            return !IsClientActionComplete(animation, _currentActionName, elapsed, _currentActionSpeed);
        }

        private bool ShouldSuppressForCurrentMap()
        {
            return ResolveNoDragonSuppression(_wrapperOwnedNoDragonSuppression);
        }

        internal static bool ResolveNoDragonSuppression(bool? wrapperOwnedSuppression)
        {
            return wrapperOwnedSuppression == true;
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

        private FollowUpdateFlags UpdateFollowStateAndVisualAnchor(PlayerCharacter owner, float frameDeltaSeconds)
        {
            if (_visualAnchor == Vector2.Zero)
            {
                _visualAnchor = _worldAnchor;
                _followVelocity = Vector2.Zero;
            }

            int workStepCount = ResolveClientVecCtrlWorkStepCount(frameDeltaSeconds, ref _vecCtrlWorkStepCarryMilliseconds);
            FollowUpdateFlags followUpdate = FollowUpdateFlags.None;
            EnsureClientVecCtrlMovePathSeed(owner);
            for (int step = 0; step < workStepCount; step++)
            {
                UpdateFollowState(owner);
                Vector2 previousAnchor = _visualAnchor;
                followUpdate |= UpdateVisualAnchor();
                RecordClientVecCtrlMovePathStep(owner, previousAnchor);
            }

            QueueClientVecCtrlEndUpdateActiveFlushPackets(workStepCount);
            return followUpdate;
        }

        private void QueueClientVecCtrlEndUpdateActiveFlushPackets(int workStepCount)
        {
            int flushPacketCount = ResolveClientDragonEndUpdateActiveFlushPacketCount(
                workStepCount,
                ref _vecCtrlEndUpdateActiveFlushCarryMilliseconds,
                ClientVecCtrlPassiveStepMilliseconds,
                ClientVecCtrlDragonFlushThresholdMilliseconds);
            if (flushPacketCount <= 0)
            {
                return;
            }

            if (!CanClientDragonEndUpdateActiveFlushNonShortNonFlyPath(_clientVecCtrlMovePathBuffer))
            {
                _vecCtrlEndUpdateActiveFlushCarryMilliseconds = ClientVecCtrlDragonFlushThresholdMilliseconds;
                return;
            }

            for (int i = 0; i < flushPacketCount; i++)
            {
                EnqueueClientVecCtrlEndUpdateActiveFlushPacketPayload(
                    BuildClientVecCtrlEndUpdateActiveFlushPacketPayload());
            }
        }

        private void EnsureClientVecCtrlMovePathSeed(PlayerCharacter? owner)
        {
            if (_clientVecCtrlMovePathBuffer.Count > 0)
            {
                return;
            }

            MovePathElement seed = CreateClientVecCtrlMovePathElement(owner, _visualAnchor, _followVelocity);
            _clientVecCtrlMovePathBuffer.Add(seed);
            _clientVecCtrlMovePathKeyPadStates.Add(CreateClientVecCtrlPassiveKeyPadState(owner, seed, _clientKeyPadStateProvider));
        }

        private void RecordClientVecCtrlMovePathStep(PlayerCharacter owner, Vector2 previousAnchor)
        {
            if (_clientVecCtrlMovePathBuffer.Count <= 0)
            {
                MovePathElement seed = CreateClientVecCtrlMovePathElement(owner, previousAnchor, _followVelocity);
                _clientVecCtrlMovePathBuffer.Add(seed);
                _clientVecCtrlMovePathKeyPadStates.Add(CreateClientVecCtrlPassiveKeyPadState(owner, seed, _clientKeyPadStateProvider));
            }

            int lastIndex = _clientVecCtrlMovePathBuffer.Count - 1;
            MovePathElement tail = _clientVecCtrlMovePathBuffer[lastIndex];
            tail.Duration = (short)ClientVecCtrlPassiveStepMilliseconds;
            _clientVecCtrlMovePathBuffer[lastIndex] = tail;
            if (_clientVecCtrlMovePathKeyPadStates.Count <= lastIndex)
            {
                _clientVecCtrlMovePathKeyPadStates.Add(CreateClientVecCtrlPassiveKeyPadState(owner, tail, _clientKeyPadStateProvider));
            }
            else
            {
                _clientVecCtrlMovePathKeyPadStates[lastIndex] = CreateClientVecCtrlPassiveKeyPadState(owner, tail, _clientKeyPadStateProvider);
            }

            MovePathElement next = CreateClientVecCtrlMovePathElement(owner, _visualAnchor, _followVelocity);
            _clientVecCtrlMovePathBuffer.Add(next);
            _clientVecCtrlMovePathKeyPadStates.Add(CreateClientVecCtrlPassiveKeyPadState(owner, next, _clientKeyPadStateProvider));

            while (_clientVecCtrlMovePathBuffer.Count > MaxClientVecCtrlMovePathElements)
            {
                _clientVecCtrlMovePathBuffer.RemoveAt(0);
                if (_clientVecCtrlMovePathKeyPadStates.Count > 0)
                {
                    _clientVecCtrlMovePathKeyPadStates.RemoveAt(0);
                }
            }
        }

        private MovePathElement CreateClientVecCtrlMovePathElement(PlayerCharacter? owner, Vector2 anchor, Vector2 velocity)
        {
            short clampedX = (short)Math.Clamp((int)MathF.Round(anchor.X), short.MinValue, short.MaxValue);
            short clampedY = (short)Math.Clamp((int)MathF.Round(anchor.Y), short.MinValue, short.MaxValue);
            short clampedVelocityX = (short)Math.Clamp((int)MathF.Round(velocity.X), short.MinValue, short.MaxValue);
            short clampedVelocityY = (short)Math.Clamp((int)MathF.Round(velocity.Y), short.MinValue, short.MaxValue);
            short footholdId = (short)Math.Clamp(owner?.Physics?.CurrentFoothold?.num ?? 0, short.MinValue, short.MaxValue);
            short fallStartFootholdId = (short)Math.Clamp(owner?.Physics?.FallStartFoothold?.num ?? 0, short.MinValue, short.MaxValue);
            MoveAction action = Math.Abs(clampedVelocityX) > 0 || Math.Abs(clampedVelocityY) > 0
                ? MoveAction.Walk
                : MoveAction.Stand;
            return new MovePathElement
            {
                X = clampedX,
                Y = clampedY,
                VelocityX = clampedVelocityX,
                VelocityY = clampedVelocityY,
                Action = action,
                FootholdId = footholdId,
                FallStartFootholdId = fallStartFootholdId,
                Duration = 0,
                FacingRight = _facingRight,
                MovePathAttribute = 0,
                XOffset = 0,
                YOffset = 0,
                RandomCount = 0,
                ActualRandomCount = 0,
                StatChanged = false
            };
        }

        private static byte CreateClientVecCtrlPassiveKeyPadState(
            PlayerCharacter? owner,
            MovePathElement element,
            Func<byte?> clientKeyPadStateProvider)
        {
            byte? clientKeyPadState = clientKeyPadStateProvider?.Invoke();
            return clientKeyPadState.HasValue
                ? NormalizeClientVecCtrlPassiveKeyPadState(clientKeyPadState.Value)
                : CreateClientVecCtrlPassiveKeyPadState(owner, element);
        }

        internal static byte CreateClientVecCtrlPassiveKeyPadState(PlayerCharacter? owner, MovePathElement element)
        {
            const byte Up = 1 << 0;
            const byte Down = 1 << 1;
            const byte Left = 1 << 2;
            const byte Right = 1 << 3;

            byte state = 0;
            if (element.VelocityX < 0)
            {
                state |= Left;
            }
            else if (element.VelocityX > 0)
            {
                state |= Right;
            }

            if (element.VelocityY < 0)
            {
                state |= Up;
            }
            else if (element.VelocityY > 0)
            {
                state |= Down;
            }

            if (state == 0
                && owner?.State is PlayerState.Ladder or PlayerState.Rope
                && element.VelocityY < 0)
            {
                state = Up;
            }

            return NormalizeClientVecCtrlPassiveKeyPadState(state);
        }

        internal static byte ResolveClientVecCtrlPassiveKeyPadStateFromInput(InputState inputState)
        {
            const byte Up = 1 << 0;
            const byte Down = 1 << 1;
            const byte Left = 1 << 2;
            const byte Right = 1 << 3;

            byte state = 0;
            if (inputState.Up)
            {
                state |= Up;
            }

            if (inputState.Down)
            {
                state |= Down;
            }

            if (inputState.Left)
            {
                state |= Left;
            }

            if (inputState.Right)
            {
                state |= Right;
            }

            return NormalizeClientVecCtrlPassiveKeyPadState(state);
        }

        internal static byte NormalizeClientVecCtrlPassiveKeyPadState(byte keyPadState)
        {
            return (byte)(keyPadState & 0x0F);
        }

        private int ApplyClientVecCtrlPostFlushRetainedElements(IReadOnlyList<MovePathElement> sourcePath)
        {
            List<MovePathElement> sourceElements = sourcePath?.ToList() ?? new List<MovePathElement>();
            int retainedStartIndex = ResolveClientDragonFlushRetainedStartIndex(sourceElements);
            _clientVecCtrlMovePathBuffer.Clear();
            _clientVecCtrlMovePathKeyPadStates.Clear();
            if (retainedStartIndex < 0)
            {
                return 0;
            }

            for (int i = retainedStartIndex; i < sourceElements.Count; i++)
            {
                MovePathElement element = sourceElements[i];
                _clientVecCtrlMovePathBuffer.Add(element);
            }

            return ResolveClientDragonFlushRetainedGatherDuration(sourceElements);
        }

        private byte[] BuildClientVecCtrlEndUpdateActiveFlushPacketPayload()
        {
            EnsureClientVecCtrlMovePathSeed(owner: null);
            IReadOnlyList<MovePathElement> movePath = _clientVecCtrlMovePathBuffer;
            if (!TryEncodeClientDragonEndUpdateActiveFlushMovePathPayload(
                    movePath,
                    _clientVecCtrlMovePathKeyPadStates,
                    out byte[] payload,
                    out _))
            {
                payload = Array.Empty<byte>();
            }

            _vecCtrlEndUpdateActiveFlushCarryMilliseconds = ApplyClientVecCtrlPostFlushRetainedElements(movePath);

            return payload ?? Array.Empty<byte>();
        }

        private void EnqueueClientVecCtrlEndUpdateActiveFlushPacketPayload(byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            while (_pendingVecCtrlEndUpdateActiveFlushPayloads.Count >= MaxPendingClientVecCtrlFlushPackets)
            {
                _pendingVecCtrlEndUpdateActiveFlushPayloads.Dequeue();
            }

            _pendingVecCtrlEndUpdateActiveFlushPayloads.Enqueue(payload);
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

        private FollowUpdateFlags UpdateVisualAnchor()
        {
            double velocityX = _followVelocity.X;
            double velocityY = _followVelocity.Y;
            FollowUpdateFlags result = FollowUpdateFlags.None;

            if (_isFollowActive)
            {
                result |= UpdateActiveVisualAnchor(ref velocityX, ref velocityY);
            }
            else
            {
                float passiveDeltaSeconds = ResolveClientPassiveFollowStepSeconds(0f);
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

        internal static int ResolveClientVecCtrlWorkStepCount(float frameDeltaSeconds, ref int carriedMilliseconds)
        {
            int elapsedMilliseconds = ResolveClientElapsedMillisecondsFromFrameDelta(frameDeltaSeconds);
            return ResolveClientVecCtrlWorkStepCountFromElapsedMilliseconds(
                elapsedMilliseconds,
                ref carriedMilliseconds,
                ClientVecCtrlPassiveStepMilliseconds);
        }

        internal static int ResolveClientElapsedMillisecondsFromFrameDelta(float frameDeltaSeconds)
        {
            return (int)Math.Round(Math.Max(0f, frameDeltaSeconds) * 1000f);
        }

        internal static int ResolveClientVecCtrlWorkStepCountFromElapsedMilliseconds(
            int elapsedMilliseconds,
            ref int carriedMilliseconds,
            int stepMilliseconds)
        {
            if (stepMilliseconds <= 0)
            {
                carriedMilliseconds = 0;
                return 0;
            }

            long totalMilliseconds = Math.Max(0, elapsedMilliseconds) + Math.Max(0, carriedMilliseconds);
            int workStepCount = (int)(totalMilliseconds / stepMilliseconds);
            carriedMilliseconds = (int)(totalMilliseconds - (long)workStepCount * stepMilliseconds);
            return workStepCount;
        }

        internal static int ResolveClientDragonEndUpdateActiveFlushPacketCount(
            int workStepCount,
            ref int accumulatedFlushMilliseconds,
            int workStepMilliseconds,
            int flushThresholdMilliseconds)
        {
            if (workStepMilliseconds <= 0 || flushThresholdMilliseconds <= 0)
            {
                accumulatedFlushMilliseconds = 0;
                return 0;
            }

            if (workStepCount <= 0)
            {
                return 0;
            }

            int carry = Math.Max(0, accumulatedFlushMilliseconds);
            int flushPacketCount = 0;
            for (int step = 0; step < workStepCount; step++)
            {
                carry += workStepMilliseconds;
                if (carry >= flushThresholdMilliseconds)
                {
                    flushPacketCount++;
                    carry = 0;
                }
            }

            accumulatedFlushMilliseconds = carry;
            return flushPacketCount;
        }

        internal static int ResolveClientDragonFlushRetainedStartIndex(IReadOnlyList<MovePathElement> movePath)
        {
            if (movePath == null || movePath.Count <= 0)
            {
                return -1;
            }

            int tailIndex = movePath.Count - 1;
            while (tailIndex >= 0 && movePath[tailIndex].FootholdId <= 0)
            {
                tailIndex--;
            }

            if (tailIndex < 0)
            {
                return -1;
            }

            int retainedStartIndex = tailIndex + 1;
            return retainedStartIndex < movePath.Count
                ? retainedStartIndex
                : -1;
        }

        internal static int ResolveClientDragonFlushRetainedGatherDuration(IReadOnlyList<MovePathElement> movePath)
        {
            int retainedStartIndex = ResolveClientDragonFlushRetainedStartIndex(movePath);
            if (retainedStartIndex < 0)
            {
                return 0;
            }

            int gatherDuration = 0;
            for (int i = retainedStartIndex; i < movePath.Count; i++)
            {
                gatherDuration += Math.Max((short) 0, movePath[i].Duration);
            }

            return gatherDuration;
        }

        internal static bool CanClientDragonEndUpdateActiveFlushNonShortNonFlyPath(IReadOnlyList<MovePathElement> movePath)
        {
            if (movePath == null || movePath.Count <= 0)
            {
                return false;
            }

            for (int i = movePath.Count - 1; i >= 0; i--)
            {
                if (movePath[i].FootholdId > 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryEncodeClientDragonEndUpdateActiveFlushMovePathPayload(
            IReadOnlyList<MovePathElement> movePath,
            IReadOnlyList<byte> passiveKeyPadStates,
            out byte[] payload,
            out string error)
        {
            IReadOnlyList<MovePathElement> normalizedPath =
                CMovePathClientPacketCodec.NormalizeForPortalOwnedClientMakeMovePath(movePath);
            return CMovePathClientPacketCodec.TryEncode(
                normalizedPath,
                out payload,
                out error,
                includeClientRandomCounts: false,
                includeClientFlushTail: true,
                passiveKeyPadStates: passiveKeyPadStates);
        }

        internal static bool TryDecodeClientDragonEndUpdateActiveFlushTail(
            IReadOnlyList<byte> payload,
            out ClientDragonFlushTail tail,
            out string error)
        {
            tail = default;
            error = null;

            if (payload == null || payload.Count < 9)
            {
                error = "Dragon move-path payload is too short to contain a client flush tail.";
                return false;
            }

            int offset = sizeof(short) * 4;
            if (!TryReadByte(payload, ref offset, out byte movePathCount))
            {
                error = "Dragon move-path payload is missing its move-element count.";
                return false;
            }

            for (int i = 0; i < movePathCount; i++)
            {
                if (!TrySkipClientMovePathElement(payload, ref offset))
                {
                    error = $"Dragon move-path payload ended inside move element {i}.";
                    return false;
                }
            }

            if (!TryReadByte(payload, ref offset, out byte stateCountByte))
            {
                error = "Dragon move-path payload is missing its keypad-state count.";
                return false;
            }

            int stateCount = stateCountByte;
            int packedStateByteCount = (stateCount + 1) / 2;
            if (payload.Count - offset != packedStateByteCount + sizeof(short) * 4)
            {
                error = "Dragon move-path payload tail length does not match its keypad-state count.";
                return false;
            }

            byte[] states = new byte[stateCount];
            for (int i = 0; i < stateCount; i++)
            {
                byte packed = payload[offset + i / 2];
                states[i] = (byte)(((i & 1) == 0 ? packed : packed >> 4) & 0x0F);
            }

            int boundsOffset = offset + packedStateByteCount;
            tail = new ClientDragonFlushTail(
                states,
                ReadInt16LittleEndian(payload, boundsOffset),
                ReadInt16LittleEndian(payload, boundsOffset + sizeof(short)),
                ReadInt16LittleEndian(payload, boundsOffset + sizeof(short) * 2),
                ReadInt16LittleEndian(payload, boundsOffset + sizeof(short) * 3));
            return true;
        }

        internal static bool TryDecodeClientDragonEndUpdateActiveFlushTailFromRawPacket(
            IReadOnlyList<byte> rawPacket,
            out ClientDragonFlushTail tail,
            out string error)
        {
            tail = default;
            error = null;

            if (rawPacket == null || rawPacket.Count < sizeof(ushort))
            {
                error = "Dragon move raw packet is too short to contain an opcode.";
                return false;
            }

            int opcode = ReadUInt16LittleEndian(rawPacket, 0);
            if (opcode != ClientVecCtrlDragonMovePacketOpcode)
            {
                error = $"Dragon move raw packet opcode {opcode} does not match client opcode {ClientVecCtrlDragonMovePacketOpcode}.";
                return false;
            }

            byte[] payload = new byte[rawPacket.Count - sizeof(ushort)];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = rawPacket[sizeof(ushort) + i];
            }

            return TryDecodeClientDragonEndUpdateActiveFlushTail(payload, out tail, out error);
        }

        internal bool TryConsumeClientVecCtrlEndUpdateActiveFlushPacket(out int packetOpcode, out byte[] payload)
        {
            if (_pendingVecCtrlEndUpdateActiveFlushPayloads.Count <= 0)
            {
                packetOpcode = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            payload = _pendingVecCtrlEndUpdateActiveFlushPayloads.Dequeue() ?? Array.Empty<byte>();
            packetOpcode = ClientVecCtrlDragonMovePacketOpcode;
            return true;
        }

        private static short ReadInt16LittleEndian(IReadOnlyList<byte> buffer, int offset)
        {
            return (short)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        private static ushort ReadUInt16LittleEndian(IReadOnlyList<byte> buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        private static bool TryReadByte(IReadOnlyList<byte> buffer, ref int offset, out byte value)
        {
            value = 0;
            if (buffer == null || offset < 0 || offset >= buffer.Count)
            {
                return false;
            }

            value = buffer[offset++];
            return true;
        }

        private static bool TrySkipClientMovePathElement(IReadOnlyList<byte> buffer, ref int offset)
        {
            if (!TryReadByte(buffer, ref offset, out byte attribute))
            {
                return false;
            }

            int bodyLength = attribute switch
            {
                0 or 5 or 14 or 35 or 36 => 14,
                12 => 16,
                1 or 2 or 13 or 16 or 18 or 31 or 32 or 33 or 34 => 4,
                3 or 4 or 6 or 7 or 8 or 10 => 6,
                9 => 1,
                11 => 6,
                17 => 8,
                _ => 0
            };
            int suffixLength = attribute == 9 ? 0 : sizeof(byte) + sizeof(short);
            int nextOffset = offset + bodyLength + suffixLength;
            if (nextOffset > buffer.Count)
            {
                return false;
            }

            offset = nextOffset;
            return true;
        }

        internal static float ResolveClientPassiveFollowStepSeconds(float frameDeltaSeconds)
        {
            _ = frameDeltaSeconds;
            return ClientVecCtrlPassiveStepMilliseconds / 1000f;
        }

        private FollowUpdateFlags UpdateActiveVisualAnchor(ref double velocityX, ref double velocityY)
        {
            if (ShouldSnapActiveFollowToTarget(_visualAnchor, _worldAnchor))
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

        internal static bool ShouldSnapActiveFollowToTarget(Vector2 currentAnchor, Vector2 targetAnchor)
        {
            float deltaX = targetAnchor.X - currentAnchor.X;
            float deltaY = targetAnchor.Y - currentAnchor.Y;
            return deltaX < -ActiveFollowSnapWidth
                   || deltaX >= ActiveFollowSnapWidth
                   || deltaY < -ActiveFollowSnapHeight
                   || deltaY >= ActiveFollowSnapHeight;
        }

        internal static float ResolveClientActiveFollowHorizontalStep(float currentX, float targetX, out double velocityX)
        {
            int currentXInt = ToClientWorldInt(currentX);
            int targetXInt = ToClientWorldInt(targetX);
            if (targetXInt > currentXInt + ActiveFollowDistanceX)
            {
                velocityX = 1d;
                int nextX = Math.Min(targetXInt - (int)ActiveFollowDistanceX, currentXInt + (int)ActiveFollowStepX);
                return nextX;
            }

            if (targetXInt < currentXInt - ActiveFollowDistanceX)
            {
                velocityX = -1d;
                int nextX = Math.Max(targetXInt + (int)ActiveFollowDistanceX, currentXInt - (int)ActiveFollowStepX);
                return nextX;
            }

            velocityX = 0d;
            return currentXInt;
        }

        internal static float ResolveClientActiveFollowVerticalStep(
            float currentY,
            float targetY,
            ref int followingState,
            ref int checkCount,
            out double velocityY)
        {
            int currentYInt = ToClientWorldInt(currentY);
            int targetYInt = ToClientWorldInt(targetY);
            int deltaY = targetYInt - currentYInt;
            int absoluteDeltaY = Math.Abs(deltaY);
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

            if (followingState != 0)
            {
                checkCount = 0;
            }

            bool shouldMoveVertically = followingState != 0
                || ShouldUseImmediateActiveVerticalFollow(deltaY);
            if (!shouldMoveVertically)
            {
                velocityY = 0d;
                return currentYInt;
            }

            if (followingState < 0 && currentYInt == targetYInt)
            {
                followingState = 0;
                velocityY = 0d;
                return currentYInt;
            }

            int verticalStep = Math.Max(1, (int)(MathF.Min(ActiveFollowVerticalStepCap, absoluteDeltaY / ActiveFollowVerticalStepDivisor) + 1f));
            float nextY = deltaY >= 0f
                ? Math.Min(targetYInt, currentYInt + verticalStep)
                : Math.Max(targetYInt, currentYInt - verticalStep);
            followingState = nextY == targetY ? -1 : 1;
            velocityY = deltaY >= 0f ? 1d : -1d;
            return nextY;
        }

        private static int ToClientWorldInt(float value)
        {
            return (int)value;
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

        internal static Vector2 ResolveClientDragonKeyDownBarAnchor(Vector2 visualAnchor, SkillFrame frame)
        {
            Rectangle bounds = GetRelativeBounds(frame);
            float dragonHeight = bounds.Height > 0 ? bounds.Height : frame?.Texture?.Height ?? 0;
            return new Vector2(
                visualAnchor.X - DragonKeyDownBarHalfWidth,
                visualAnchor.Y - dragonHeight - DragonKeyDownBarVerticalGap);
        }

        private bool TryResolveCurrentFrame(int currentTime, out SkillFrame frame, out float frameAlpha)
        {
            frame = null;
            frameAlpha = 0f;

            if (_currentSet == null
                || string.IsNullOrWhiteSpace(_currentActionName)
                || _alpha <= 0.01f
                || !_currentSet.TryGetAnimation(_currentActionName, out SkillAnimation animation)
                || !TryGetClientActionFrameAtTime(
                    animation,
                    _currentActionName,
                    Math.Max(0, currentTime - _currentActionStartTime),
                    _currentActionSpeed,
                    out frame,
                    out int frameElapsedMs,
                    out int frameDelayMs)
                || frame == null)
            {
                return false;
            }

            frameAlpha = ResolveFrameAlpha(frame, frameElapsedMs, frameDelayMs);
            return frameAlpha > 0.01f;
        }

        private static float ResolveFrameAlpha(SkillFrame frame, int frameElapsedMs)
        {
            return ResolveFrameAlpha(frame, frameElapsedMs, frame?.Delay ?? 0);
        }

        private static float ResolveFrameAlpha(SkillFrame frame, int frameElapsedMs, int frameDelayMs)
        {
            if (frame == null)
            {
                return 0f;
            }

            int startAlpha = Math.Clamp(frame.AlphaStart, 0, 255);
            int endAlpha = Math.Clamp(frame.AlphaEnd, 0, 255);
            float progress = MathHelper.Clamp(frameElapsedMs / (float)Math.Max(1, frameDelayMs), 0f, 1f);
            return MathHelper.Lerp(startAlpha, endAlpha, progress) / 255f;
        }

        internal static int ResolveClientDragonActionFrameDelay(string actionName, int authoredDelay, int actionSpeed)
        {
            int delay = Math.Max(1, authoredDelay);
            if (!IsExplicitDragonAction(actionName))
            {
                return delay;
            }

            int clampedActionSpeed = Math.Clamp(actionSpeed, 2, 10);
            return Math.Max(1, delay * (clampedActionSpeed + 10) / 16);
        }

        internal static int ResolveClientDragonActionDuration(SkillAnimation animation, string actionName, int actionSpeed)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return 0;
            }

            int duration = 0;
            foreach (SkillFrame frame in animation.Frames)
            {
                duration += ResolveClientDragonActionFrameDelay(actionName, frame?.Delay ?? 0, actionSpeed);
            }

            return duration;
        }

        private static bool IsClientActionComplete(SkillAnimation animation, string actionName, int elapsedMs, int actionSpeed)
        {
            if (animation == null || animation.Loop)
            {
                return false;
            }

            return elapsedMs >= ResolveClientDragonActionDuration(animation, actionName, actionSpeed);
        }

        private static bool TryGetClientActionFrameAtTime(
            SkillAnimation animation,
            string actionName,
            int timeMs,
            int actionSpeed,
            out SkillFrame frame,
            out int frameElapsedMs,
            out int frameDelayMs)
        {
            frame = null;
            frameElapsedMs = 0;
            frameDelayMs = 0;

            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return false;
            }

            if (animation.Loop || !IsExplicitDragonAction(actionName))
            {
                bool resolved = animation.TryGetFrameAtTime(timeMs, out frame, out frameElapsedMs);
                frameDelayMs = Math.Max(1, frame?.Delay ?? 0);
                return resolved;
            }

            int totalDuration = ResolveClientDragonActionDuration(animation, actionName, actionSpeed);
            if (totalDuration <= 0)
            {
                frame = animation.Frames[0];
                frameDelayMs = Math.Max(1, frame.Delay);
                return true;
            }

            int clampedTime = Math.Min(Math.Max(0, timeMs), totalDuration - 1);
            int elapsed = 0;
            foreach (SkillFrame currentFrame in animation.Frames)
            {
                int currentDelay = ResolveClientDragonActionFrameDelay(actionName, currentFrame?.Delay ?? 0, actionSpeed);
                elapsed += currentDelay;
                if (clampedTime < elapsed)
                {
                    frame = currentFrame;
                    frameElapsedMs = clampedTime - (elapsed - currentDelay);
                    frameDelayMs = currentDelay;
                    return true;
                }
            }

            frame = animation.Frames[^1];
            frameDelayMs = ResolveClientDragonActionFrameDelay(actionName, frame?.Delay ?? 0, actionSpeed);
            frameElapsedMs = Math.Max(0, Math.Min(clampedTime, totalDuration - 1));
            return true;
        }

        internal static float ResolveClientLayerAlpha(bool shouldShow)
        {
            return shouldShow ? 1f : 0f;
        }

        internal static int ResolveClientDragonActionLayerZ(int ownerLayerZ)
        {
            return ownerLayerZ - 1;
        }

        internal static int ResolveClientDragonActionLayerOwnerZ(int fallbackOwnerLayerZ, int? vecCtrlOwnerLayerZ)
        {
            return vecCtrlOwnerLayerZ ?? fallbackOwnerLayerZ;
        }

        internal static int ResolveClientDragonActionLayerOwnerZWithPersistedVecCtrl(
            int fallbackOwnerLayerZ,
            int? sampledVecCtrlOwnerLayerZ,
            ref int? persistedVecCtrlOwnerLayerZ)
        {
            if (sampledVecCtrlOwnerLayerZ.HasValue)
            {
                persistedVecCtrlOwnerLayerZ = sampledVecCtrlOwnerLayerZ.Value;
            }

            return ResolveClientDragonActionLayerOwnerZ(fallbackOwnerLayerZ, persistedVecCtrlOwnerLayerZ);
        }

        internal static int? ResolveClientOwnerLayerZFromVecCtrlContext(int? layerPage, int? layerZMass, bool onLadderOrRope)
        {
            if (!layerPage.HasValue || !layerZMass.HasValue)
            {
                return null;
            }

            long pageTerm = (long)ClientVecCtrlLayerZPageScale * layerPage.Value - layerZMass.Value;
            long ladderTerm = onLadderOrRope ? ClientVecCtrlLayerZOffsetLadder : ClientVecCtrlLayerZOffsetGround;
            long layerZ = ClientVecCtrlLayerZStride * pageTerm + ladderTerm + ClientVecCtrlLayerZBaseOffset;
            return (int)Math.Clamp(layerZ, int.MinValue, int.MaxValue);
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

        internal static bool ResolveClientOwnerUpdateVisibility(bool ownerUpdated, bool suppressedForMap)
        {
            _ = suppressedForMap;
            return ownerUpdated;
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
            if (canvas == null)
            {
                return null;
            }

            try
            {
                return canvas.GetLinkedWzImageProperty() as WzCanvasProperty ?? canvas;
            }
            catch
            {
                return canvas;
            }
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
