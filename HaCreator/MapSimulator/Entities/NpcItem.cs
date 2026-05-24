using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Entities
{
    public class NpcItem : BaseDXDrawableItem, IInteractiveEntity
    {
        private readonly NpcInstance _npcInstance;
        public NpcInstance NpcInstance
        {
            get { return _npcInstance; }
            private set { }
        }

        private NameTooltipItem _nameTooltip = null;
        private NameTooltipItem _npcDescTooltip = null;

        // Animation system - using AnimationController for unified frame management
        private readonly NpcAnimationSet _animationSet;
        private readonly AnimationController _animationController;
        private readonly string[] _idleSpeechLines;
        private int _nextIdleSpeechIndex;
        private bool _hasRenderPositionOverride;
        private int _renderOverrideX;
        private int _renderOverrideY;

        // Action cycling (based on MapleNecrocer) - only when standing
        private int _actionCycleCounter = 0;
        private const int ACTION_CYCLE_INTERVAL = 1000; // Cycle to random action every ~1000 frames
        private static readonly Random _random = new Random();
        private string _temporaryAction;
        private int _temporaryActionRemainingMs;
        private string _lastLimitedEffectKey;
        private int _lastLimitedEffectStringPoolId = -1;
        private int _lastActionLayerResetTick = -1;
        private int _lastChatBalloonClearTick = -1;
        private string _imitatedName;
        private byte[] _imitatedAvatarLookPayload = Array.Empty<byte>();
        private CharacterBuild _imitatedBuild;
        private CharacterAssembler _imitatedAssembler;
        private Func<MapleTvVisualAssets> _mapleTvVisualAssetsProvider;
        private Func<MapleTvSnapshot> _mapleTvSnapshotProvider;
        private SpriteFont _mapleTvFont;
        private int _mapleTvMessageX;
        private int _mapleTvMessageY;
        private int _mapleTvAdX;
        private int _mapleTvAdY;
        private int _clientFloatVectorStartedAtTick = int.MinValue;
        private const int MapleTvNativeMessageLineHeight = 16;
        private const int MapleTvNativeSenderNameXDelta = -57;
        private const int MapleTvNativeReceiverNameXDelta = 146;
        private const int MapleTvNativeNameYDelta = 71;
        private const int MapleTvNativeReceiverGlyphWidth = 4;
        private const int ClientFloatVectorRadiusPx = 5;
        private const int ClientFloatVectorRotateMs = 2000;

        // Movement system
        public NpcMovementInfo MovementInfo { get; private set; }
        public bool MovementEnabled { get; set; } = true;
        public bool IdleActionCyclingEnabled { get; set; } = true;
        public bool Flip => flip;
        public int PacketObjectId { get; private set; } = -1;
        public int PacketFootholdId { get; private set; }
        public bool PacketControllerOwnedByLocalUser { get; private set; }
        public bool PacketEnabled { get; private set; } = true;
        public int LastPacketMoveAction { get; private set; }
        public int LastPacketChatIndex { get; private set; } = -1;
        public string LastSpecialAction { get; private set; }
        public int LastSpecialActionClientActionId { get; private set; } = -1;
        public string LastLimitedEffectKey => _lastLimitedEffectKey;
        public int LastLimitedEffectStringPoolId => _lastLimitedEffectStringPoolId;
        public int LastActionLayerResetTick => _lastActionLayerResetTick;
        public int LastChatBalloonClearTick => _lastChatBalloonClearTick;
        public string ImitatedName => _imitatedName;
        public IReadOnlyList<byte> ImitatedAvatarLookPayload => _imitatedAvatarLookPayload;
        internal CharacterBuild ImitatedBuild => _imitatedBuild;
        public bool HasImitatedLook => !string.IsNullOrWhiteSpace(_imitatedName) || _imitatedAvatarLookPayload.Length > 0;
        public bool HasMapleTvPresentation { get; private set; }
        public bool HasClientFloatPresentation { get; private set; }
        public int MapleTvMessageX => _mapleTvMessageX;
        public int MapleTvMessageY => _mapleTvMessageY;
        public int MapleTvAdX => _mapleTvAdX;
        public int MapleTvAdY => _mapleTvAdY;

        // Cached mirror boundary (optimization - avoid recalculating every frame)
        private readonly CachedBoundaryChecker _boundaryChecker = new CachedBoundaryChecker();

        /// <summary>
        /// Constructor with animation set
        /// </summary>
        /// <param name="_npcInstance"></param>
        /// <param name="animationSet"></param>
        /// <param name="_nameTooltip"></param>
        /// <param name="_npcDescTooltip"></param>
        public NpcItem(
            NpcInstance _npcInstance,
            NpcAnimationSet animationSet,
            NameTooltipItem _nameTooltip,
            NameTooltipItem _npcDescTooltip,
            IReadOnlyList<string> idleSpeechLines = null)
            : base(animationSet.GetFrames(AnimationKeys.Stand) ?? animationSet.GetFrames(null), _npcInstance.Flip)
        {
            this._npcInstance = _npcInstance;
            this._nameTooltip = _nameTooltip;
            this._npcDescTooltip = _npcDescTooltip;
            this._animationSet = animationSet;
            _idleSpeechLines = idleSpeechLines?
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<string>();
            _nextIdleSpeechIndex = _idleSpeechLines.Length > 0 ? _random.Next(_idleSpeechLines.Length) : 0;

            // Initialize animation controller
            _animationController = new AnimationController(animationSet, AnimationKeys.Stand);

            // Randomize initial counter so NPCs don't all change action at once
            _actionCycleCounter = _random.Next(ACTION_CYCLE_INTERVAL);

            // Initialize movement
            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a multi frame NPC (legacy support)
        /// </summary>
        /// <param name="_npcInstance"></param>
        /// <param name="frames"></param>
        /// <param name="_nameTooltip"></param>
        /// <param name="_npcDescTooltip"></param>
        public NpcItem(NpcInstance _npcInstance, List<IDXObject> frames, NameTooltipItem _nameTooltip, NameTooltipItem _npcDescTooltip)
            : base(frames, _npcInstance.Flip)
        {
            this._npcInstance = _npcInstance;
            this._nameTooltip = _nameTooltip;
            this._npcDescTooltip = _npcDescTooltip;

            // Create a simple animation set with all frames as "stand"
            _animationSet = new NpcAnimationSet();
            _animationSet.AddAnimation(AnimationKeys.Stand, frames);

            // Initialize animation controller
            _animationController = new AnimationController(_animationSet, AnimationKeys.Stand);

            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a single frame NPC (legacy support)
        /// </summary>
        /// <param name="_npcInstance"></param>
        /// <param name="frame0"></param>
        /// <param name="_nameTooltip"></param>
        /// <param name="_npcDescTooltip"></param>
        public NpcItem(NpcInstance _npcInstance, IDXObject frame0, NameTooltipItem _nameTooltip, NameTooltipItem _npcDescTooltip)
            : base(frame0, _npcInstance.Flip)
        {
            this._npcInstance = _npcInstance;
            this._nameTooltip = _nameTooltip;
            this._npcDescTooltip = _npcDescTooltip;

            // Create a simple animation set
            _animationSet = new NpcAnimationSet();
            _animationSet.AddAnimation(AnimationKeys.Stand, new List<IDXObject> { frame0 });

            // Initialize animation controller
            _animationController = new AnimationController(_animationSet, AnimationKeys.Stand);

            InitializeMovement();
        }

        /// <summary>
        /// Initialize movement info from NPC instance data
        /// </summary>
        private void InitializeMovement()
        {
            MovementInfo = new NpcMovementInfo();
            MovementInfo.Initialize(
                _npcInstance.X,
                _npcInstance.Y,
                _npcInstance.rx0Shift,
                _npcInstance.rx1Shift,
                _animationSet?.CanWalk ?? false
            );
        }

        /// <summary>
        /// Set the current animation action
        /// </summary>
        /// <param name="action">Action name (stand, speak, blink, etc.)</param>
        public void SetAction(string action)
        {
            _animationController?.SetAction(action);
        }

        /// <summary>
        /// Gets the current animation action
        /// </summary>
        public string CurrentAction => _animationController?.CurrentAction ?? AnimationKeys.Stand;

        public bool HasAction(string action)
        {
            return _animationSet != null && _animationSet.HasAnimation(action);
        }

        public IReadOnlyList<string> GetAvailableActions()
        {
            return _animationSet?.GetAvailableActionsList() ?? Array.Empty<string>();
        }

        public int GetActionTotalDurationMs(string action)
        {
            if (_animationSet == null)
            {
                return 0;
            }

            List<IDXObject> frames = _animationSet.GetFrames(action);
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int totalDelay = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDelay += Math.Max(frames[i]?.Delay ?? 100, 10);
            }

            return totalDelay;
        }

        public void SetRenderPositionOverride(int x, int y)
        {
            _hasRenderPositionOverride = true;
            _renderOverrideX = x;
            _renderOverrideY = y;
        }

        public void ApplyPacketInit(
            int objectId,
            int x,
            int y,
            int moveAction,
            int footholdId,
            int rx0,
            int rx1,
            bool enabled,
            bool localController)
        {
            PacketObjectId = objectId;
            PacketFootholdId = footholdId;
            PacketControllerOwnedByLocalUser = localController;
            LastPacketMoveAction = moveAction;
            PacketEnabled = enabled;
            MovementInfo?.ApplyPacketPosition(x, y, rx0, rx1, moveAction);
            SetRenderPositionOverride(x, y);
            ApplyPacketMoveAction(moveAction);
            ResetPacketActionLayer();
        }

        public void ApplyPacketMove(int oneTimeAction, int chatIndex)
        {
            ApplyPacketMove(oneTimeAction, chatIndex, null);
        }

        public void ApplyPacketMove(int oneTimeAction, int chatIndex, IReadOnlyList<MovePathElement> movePathElements)
        {
            LastPacketMoveAction = oneTimeAction;
            LastPacketChatIndex = chatIndex;
            ApplyPacketMoveAction(oneTimeAction);
            if (movePathElements?.Count > 0)
            {
                MovementInfo?.ApplyPacketMovePath(movePathElements);
                MovePathElement tail = movePathElements.Last();
                for (int i = movePathElements.Count - 1; i >= 0; i--)
                {
                    MovePathElement candidate = movePathElements[i];
                    if (candidate.X != 0 || candidate.Y != 0)
                    {
                        SetRenderPositionOverride(candidate.X, candidate.Y);
                        break;
                    }
                }

                if (tail.FootholdId != 0)
                {
                    PacketFootholdId = tail.FootholdId;
                }
            }

            if (oneTimeAction >= 0)
            {
                string actionName = NpcClientActionSetLoader.ResolveClientActionName(
                    oneTimeAction,
                    GetAvailableActions());
                if (HasAction(actionName))
                {
                    SetTemporaryAction(actionName, GetActionTotalDurationMs(actionName));
                }
            }
        }

        public void ApplyPacketLimitedInfo(bool enabled, int currentTick)
        {
            bool changed = PacketEnabled != enabled;
            PacketEnabled = enabled;
            _lastLimitedEffectStringPoolId = enabled ? 0x1154 : 0x1155;
            _lastLimitedEffectKey = $"StringPool:0x{_lastLimitedEffectStringPoolId:X4}";
            _lastChatBalloonClearTick = currentTick;
            ResetPacketActionLayer(currentTick);
            if (changed)
            {
                SetAction(AnimationKeys.Stand);
            }
        }

        public bool TryApplyPacketSpecialAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            IReadOnlyList<string> availableActions = GetAvailableActions();
            string resolvedAction = availableActions
                .FirstOrDefault(action => string.Equals(action, actionName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(resolvedAction))
            {
                return false;
            }

            LastSpecialAction = resolvedAction;
            List<string> templateActions = availableActions
                .Where(action => !string.Equals(action, AnimationKeys.Stand, StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(action, "default", StringComparison.OrdinalIgnoreCase))
                .ToList();
            int templateActionIndex = templateActions
                .TakeWhile(action => !string.Equals(action, resolvedAction, StringComparison.OrdinalIgnoreCase))
                .Count();
            LastSpecialActionClientActionId = templateActions.Any(action => string.Equals(action, resolvedAction, StringComparison.OrdinalIgnoreCase))
                ? templateActionIndex + 2
                : -1;
            SetTemporaryAction(resolvedAction, Math.Max(GetActionTotalDurationMs(resolvedAction), 180));
            return true;
        }

        public void ApplyImitatedLook(string name, byte[] avatarLookPayload, CharacterBuild avatarBuild = null)
        {
            _imitatedName = string.IsNullOrWhiteSpace(name) ? null : name;
            _imitatedAvatarLookPayload = avatarLookPayload?.ToArray() ?? Array.Empty<byte>();
            _imitatedBuild = avatarBuild;
            _imitatedAssembler = avatarBuild != null ? new CharacterAssembler(avatarBuild) : null;
            ResetPacketActionLayer();
        }

        public void MarkMapleTvPresentationAvailable(bool available, int messageX = 0, int messageY = 0, int adX = 0, int adY = 0)
        {
            HasMapleTvPresentation = available;
            _mapleTvMessageX = messageX;
            _mapleTvMessageY = messageY;
            _mapleTvAdX = adX;
            _mapleTvAdY = adY;
        }

        public void MarkClientFloatPresentationAvailable(bool available)
        {
            HasClientFloatPresentation = available;
            _clientFloatVectorStartedAtTick = int.MinValue;
        }

        internal void ConfigureMapleTvPresentation(
            Func<MapleTvVisualAssets> visualAssetsProvider,
            Func<MapleTvSnapshot> snapshotProvider,
            SpriteFont font)
        {
            _mapleTvVisualAssetsProvider = visualAssetsProvider;
            _mapleTvSnapshotProvider = snapshotProvider;
            _mapleTvFont = font;
        }

        public void ReplaceNameTooltip(NameTooltipItem tooltip)
        {
            _nameTooltip = tooltip;
        }

        public void ClearRenderPositionOverride()
        {
            _hasRenderPositionOverride = false;
        }

        /// <summary>
        /// Update NPC movement and action cycling
        /// </summary>
        /// <param name="deltaTimeMs">Time elapsed since last update in milliseconds</param>
        public void Update(int deltaTimeMs)
        {
            if (!PacketEnabled)
            {
                return;
            }

            UpdateTemporaryAction(deltaTimeMs);

            // Update movement
            if (MovementEnabled && MovementInfo != null && MovementInfo.CanMove)
            {
                MovementInfo.UpdateMovement(deltaTimeMs);

                // Update flip based on movement direction
                // NPC sprites typically face left by default, so invert the flip
                this.flip = !MovementInfo.FlipX;

                // Update animation based on movement state
                if (MovementInfo.IsMoving)
                {
                    // Use move/walk animation
                    string walkAction = _animationSet.HasAnimation(AnimationKeys.Move) ? AnimationKeys.Move : AnimationKeys.Walk;
                    SetAction(walkAction);
                }
                else
                {
                    if (HasTemporaryActionOverride)
                    {
                        return;
                    }

                    // Standing - cycle through idle actions
                    UpdateActionCycle();
                }
            }
            else
            {
                if (HasTemporaryActionOverride)
                {
                    return;
                }

                // No movement - just cycle through actions
                UpdateActionCycle();
            }
        }

        public void SetTemporaryAction(string action, int durationMs)
        {
            if (durationMs <= 0 || string.IsNullOrWhiteSpace(action) || _animationSet == null || !_animationSet.HasAnimation(action))
            {
                return;
            }

            _temporaryAction = action;
            _temporaryActionRemainingMs = durationMs;
            SetAction(action);
        }

        public bool HasIdleSpeech => _idleSpeechLines.Length > 0;

        public string GetNextIdleSpeechLine()
        {
            if (_idleSpeechLines.Length == 0)
            {
                return null;
            }

            string line = _idleSpeechLines[_nextIdleSpeechIndex];
            _nextIdleSpeechIndex = (_nextIdleSpeechIndex + 1) % _idleSpeechLines.Length;
            return line;
        }

        public IReadOnlyList<string> GetIdleSpeechLines()
        {
            return _idleSpeechLines;
        }

        /// <summary>
        /// Update action cycling - only when standing still
        /// Based on MapleNecrocer's NPC action cycling
        /// </summary>
        private void UpdateActionCycle()
        {
            if (!IdleActionCyclingEnabled || _animationSet == null || _animationSet.ActionCount <= 1)
                return;

            // Don't cycle if we're in move/walk action
            if (CurrentAction == AnimationKeys.Move || CurrentAction == AnimationKeys.Walk)
            {
                SetAction(AnimationKeys.Stand);
                return;
            }

            _actionCycleCounter++;
            if (_actionCycleCounter >= ACTION_CYCLE_INTERVAL)
            {
                _actionCycleCounter = 0;
                // Switch to a random action (excluding move/walk)
                string newAction;
                int attempts = 0;
                do
                {
                    newAction = _animationSet.GetRandomAction(_random);
                    attempts++;
                } while ((newAction == AnimationKeys.Move || newAction == AnimationKeys.Walk) && attempts < 5);

                if (newAction != AnimationKeys.Move && newAction != AnimationKeys.Walk)
                {
                    SetAction(newAction);
                }
            }
        }

        private bool HasTemporaryActionOverride =>
            _temporaryActionRemainingMs > 0 && !string.IsNullOrWhiteSpace(_temporaryAction);

        private void ResetPacketActionLayer(int currentTick = -1)
        {
            _temporaryAction = null;
            _temporaryActionRemainingMs = 0;
            _lastActionLayerResetTick = currentTick;
            SetAction(AnimationKeys.Stand);
        }

        private void UpdateTemporaryAction(int deltaTimeMs)
        {
            if (!HasTemporaryActionOverride)
            {
                return;
            }

            _temporaryActionRemainingMs = Math.Max(0, _temporaryActionRemainingMs - Math.Max(0, deltaTimeMs));
            if (_temporaryActionRemainingMs > 0)
            {
                return;
            }

            string actionToClear = _temporaryAction;
            _temporaryAction = null;
            if (string.Equals(CurrentAction, actionToClear, StringComparison.OrdinalIgnoreCase))
            {
                SetAction(AnimationKeys.Stand);
            }
        }

        /// <summary>
        /// Get the current animation frame based on time
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IDXObject GetCurrentAnimationFrame(int tickCount)
        {
            if (_animationController == null)
                return null;

            // Update the animation controller's frame
            _animationController.UpdateFrame(tickCount);

            return _animationController.GetCurrentFrame();
        }

        /// <summary>
        /// Gets the current frame for external use (e.g., size calculations)
        /// </summary>
        public IDXObject GetCurrentFrame()
        {
            return _animationController?.GetCurrentFrame();
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            // Calculate position offset from movement
            int positionOffsetX = 0;
            int positionOffsetY = 0;

            if (_hasRenderPositionOverride)
            {
                positionOffsetX = _renderOverrideX - _npcInstance.X;
                positionOffsetY = _renderOverrideY - _npcInstance.Y;
            }
            else if (MovementEnabled && MovementInfo != null && MovementInfo.CanMove)
            {
                positionOffsetX = (int)(MovementInfo.X - _npcInstance.X);
                positionOffsetY = (int)(MovementInfo.Y - _npcInstance.Y);
            }

            Point floatOffset = ResolveClientFloatVisualOffsetAtTick(TickCount);
            positionOffsetX += floatOffset.X;
            positionOffsetY += floatOffset.Y;

            int adjustedMapShiftX = mapShiftX - positionOffsetX;
            int adjustedMapShiftY = mapShiftY - positionOffsetY;

            // Get current frame from animation
            IDXObject drawFrame = GetCurrentAnimationFrame(TickCount);

            AssembledFrame imitatedFrame = _imitatedAssembler?.GetFrameAtTime(ResolveImitatedAvatarAction(), TickCount)
                                           ?? _imitatedAssembler?.GetFrameAtTime(ResolveFallbackImitatedAvatarAction(), TickCount);
            if (imitatedFrame != null)
            {
                int screenX = CurrentX + floatOffset.X - mapShiftX + centerX;
                int screenY = CurrentY + floatOffset.Y - mapShiftY + centerY;
                imitatedFrame.Draw(sprite, skeletonMeshRenderer, screenX, screenY, flip, Color.White);
            }
            else if (drawFrame != null)
            {
                int shiftCenteredX = adjustedMapShiftX - centerX;
                int shiftCenteredY = adjustedMapShiftY - centerY;

                if (IsFrameWithinView(drawFrame, shiftCenteredX, shiftCenteredY,
                    renderParameters.RenderWidth, renderParameters.RenderHeight))
                {
                    drawFrame.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                        shiftCenteredX, shiftCenteredY,
                        flip,
                        drawReflectionInfo);
                }
            }

            DrawMapleTvMessageLayer(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                drawReflectionInfo,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                TickCount);

            // Draw name tooltip
            if (PacketEnabled && _nameTooltip != null)
            {
                _nameTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
                    adjustedMapShiftX, adjustedMapShiftY, centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }

            // Draw description tooltip
            if (PacketEnabled && _npcDescTooltip != null)
            {
                _npcDescTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
                    adjustedMapShiftX, adjustedMapShiftY, centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }
        }

        /// <summary>
        /// Gets the current X position of the NPC (considering movement)
        /// </summary>
        public int CurrentX => MovementEnabled && MovementInfo != null && MovementInfo.CanMove
            ? (int)MovementInfo.X
            : _hasRenderPositionOverride
                ? _renderOverrideX
                : _npcInstance.X;

        /// <summary>
        /// Gets the current Y position of the NPC (considering movement)
        /// </summary>
        public int CurrentY => MovementEnabled && MovementInfo != null && MovementInfo.CanMove
            ? (int)MovementInfo.Y
            : _hasRenderPositionOverride
                ? _renderOverrideY
                : _npcInstance.Y;

        /// <summary>
        /// Gets the cached mirror boundary for this NPC
        /// </summary>
        public ReflectionDrawableBoundary CachedMirrorBoundary => _boundaryChecker.CachedBoundary;

        /// <summary>
        /// Check if a map point is within the NPC's bounds
        /// Uses the same calculation as the debug overlay for consistency
        /// </summary>
        /// <param name="mapX">Map X coordinate (mouse screen X converted to map coords)</param>
        /// <param name="mapY">Map Y coordinate (mouse screen Y converted to map coords)</param>
        /// <returns>True if the point is within the NPC's bounds</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsMapPoint(int mapX, int mapY)
        {
            if (!PacketEnabled)
            {
                return false;
            }

            int npcX = CurrentX;
            int npcY = CurrentY;

            // Use NpcInstance Width/Height like the debug overlay does
            // NPC position is at their feet, hitbox extends upward
            int width = Math.Max(100, _npcInstance.Width + 40);
            int height = Math.Max(120, _npcInstance.Height);

            // Left edge calculation matches debug overlay: instance.X - (instance.Width - 20)
            int left = npcX - (_npcInstance.Width - 20);
            int right = left + width;
            int top = npcY - _npcInstance.Height;
            int bottom = npcY;

            // Check if point is within bounds
            return mapX >= left && mapX <= right && mapY >= top && mapY <= bottom;
        }

        private void ApplyPacketMoveAction(int moveAction)
        {
            MovementInfo?.ApplyPacketMoveAction(moveAction);
            flip = (moveAction & 1) != 0;
        }

        private string ResolveImitatedAvatarAction()
        {
            return CharacterPart.TryGetActionStringFromCode(5, out string nativeInitAction)
                ? nativeInitAction
                : "swingO1";
        }

        private string ResolveFallbackImitatedAvatarAction()
        {
            return MovementInfo?.IsMoving == true
                ? "walk1"
                : "stand1";
        }

        private void DrawMapleTvMessageLayer(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount)
        {
            if (!HasMapleTvPresentation || _mapleTvVisualAssetsProvider == null)
            {
                return;
            }

            MapleTvVisualAssets visualAssets = _mapleTvVisualAssetsProvider();
            if (visualAssets == null)
            {
                return;
            }

            MapleTvSnapshot snapshot = _mapleTvSnapshotProvider?.Invoke();
            if (snapshot == null)
            {
                return;
            }

            Point floatOffset = ResolveClientFloatVisualOffsetAtTick(tickCount);
            if (!snapshot.IsShowingMessage)
            {
                IReadOnlyList<MapleTvAnimationFrame> idleFrames = ResolveActorLocalMapleTvIdleFrames(visualAssets, snapshot.QueueExists);
                MapleTvAnimationFrame idleFrame = SelectMapleTvFrame(idleFrames, ResolveActorLocalMapleTvAnimationTick(snapshot, tickCount));
                int idleOriginX = CurrentX + floatOffset.X + _mapleTvAdX - mapShiftX + centerX;
                int idleOriginY = CurrentY + floatOffset.Y + _mapleTvAdY - mapShiftY + centerY;
                DrawMapleTvFrame(
                    idleFrame,
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    drawReflectionInfo,
                    idleOriginX,
                    idleOriginY);
                return;
            }

            if (_mapleTvFont == null)
            {
                return;
            }

            IReadOnlyList<MapleTvAnimationFrame> mediaFrames = visualAssets.GetMediaFrames(snapshot.ResolvedMediaIndex);
            IReadOnlyList<MapleTvAnimationFrame> onFrames = visualAssets.OnFrames.Count > 0
                ? visualAssets.OnFrames
                : visualAssets.BasicFrames;
            int mapleTvAnimationTick = ResolveActorLocalMapleTvAnimationTick(snapshot, tickCount);
            MapleTvAnimationFrame mediaFrame = SelectMapleTvFrame(mediaFrames, mapleTvAnimationTick);
            MapleTvAnimationFrame onFrame = SelectMapleTvFrame(onFrames, mapleTvAnimationTick);
            IReadOnlyList<MapleTvAnimationFrame> chatFrames = ResolveActorLocalMapleTvChatFrames(visualAssets, snapshot);
            MapleTvAnimationFrame chatFrame = SelectMapleTvFrame(chatFrames, mapleTvAnimationTick);
            if (chatFrame == null)
            {
                return;
            }

            int adOriginX = CurrentX + floatOffset.X + _mapleTvAdX - mapShiftX + centerX;
            int adOriginY = CurrentY + floatOffset.Y + _mapleTvAdY - mapShiftY + centerY;
            DrawMapleTvFrame(
                mediaFrame,
                sprite,
                skeletonMeshRenderer,
                gameTime,
                drawReflectionInfo,
                adOriginX,
                adOriginY);
            DrawMapleTvFrame(
                onFrame,
                sprite,
                skeletonMeshRenderer,
                gameTime,
                drawReflectionInfo,
                adOriginX,
                adOriginY);

            int chatOriginX = CurrentX + floatOffset.X + _mapleTvMessageX - mapShiftX + centerX;
            int chatOriginY = CurrentY + floatOffset.Y + _mapleTvMessageY - mapShiftY + centerY;
            DrawMapleTvFrame(
                chatFrame,
                sprite,
                skeletonMeshRenderer,
                gameTime,
                drawReflectionInfo,
                chatOriginX,
                chatOriginY);

            Rectangle textBounds = ResolveActorLocalMapleTvTextBounds(visualAssets, snapshot);
            Point chatTopLeft = ResolveActorLocalMapleTvChatFamilyTopLeft(
                new Point(chatOriginX, chatOriginY),
                chatFrames);
            int drawY = chatTopLeft.Y + textBounds.Y;
            int lineX = chatTopLeft.X + textBounds.X;
            foreach (string line in (snapshot.DisplayLines ?? Array.Empty<string>()).Take(5))
            {
                string visibleLine = ResolveActorLocalMapleTvMessageLineText(
                    line,
                    ResolveMapleTvMaxChars(textBounds.Width, 0.36f));
                if (!string.IsNullOrWhiteSpace(visibleLine))
                {
                    ClientTextDrawing.DrawShadowed(
                        sprite,
                        visibleLine,
                        new Vector2(lineX, drawY),
                        Color.White,
                        _mapleTvFont,
                        0.36f);
                }

                drawY += MapleTvNativeMessageLineHeight;
            }

            DrawActorLocalMapleTvNames(sprite, snapshot, lineX, chatTopLeft.Y + textBounds.Y);
        }

        internal static string ResolveActorLocalMapleTvMessageLineText(string line, int maxChars)
        {
            string processedLine = line ?? string.Empty;
            if (!string.IsNullOrEmpty(processedLine)
                && ClientCurseProcessParity.TryProcessString(
                    processedLine,
                    ignoreNewLine: true,
                    out string filteredLine,
                    out _,
                    out _))
            {
                processedLine = filteredLine;
            }

            return TruncateMapleTvLine(processedLine, maxChars);
        }

        internal static IReadOnlyList<MapleTvAnimationFrame> ResolveActorLocalMapleTvIdleFrames(
            MapleTvVisualAssets visualAssets,
            bool queueExists)
        {
            if (visualAssets == null)
            {
                return Array.Empty<MapleTvAnimationFrame>();
            }

            if (queueExists)
            {
                return visualAssets.OffFrames.Count > 0
                    ? visualAssets.OffFrames
                    : visualAssets.BasicFrames;
            }

            return visualAssets.BasicFrames.Count > 0
                ? visualAssets.BasicFrames
                : visualAssets.OffFrames;
        }

        internal static IReadOnlyList<MapleTvAnimationFrame> ResolveActorLocalMapleTvChatFrames(
            MapleTvVisualAssets visualAssets,
            MapleTvSnapshot snapshot)
        {
            if (visualAssets == null)
            {
                return Array.Empty<MapleTvAnimationFrame>();
            }

            int variantKey = ResolveActorLocalMapleTvChatVariantKey(visualAssets, snapshot);
            if (visualAssets.ChatFrames.TryGetValue(variantKey, out IReadOnlyList<MapleTvAnimationFrame> frames)
                && frames.Count > 0)
            {
                return frames;
            }

            return visualAssets.GetChatFrames(snapshot?.ResolvedMediaIndex ?? visualAssets.DefaultMediaIndex);
        }

        internal static Rectangle ResolveActorLocalMapleTvTextBounds(
            MapleTvVisualAssets visualAssets,
            MapleTvSnapshot snapshot)
        {
            int variantKey = ResolveActorLocalMapleTvChatVariantKey(visualAssets, snapshot);
            return variantKey switch
            {
                0 => MapleTvWindow.StarChatTextBounds,
                2 => MapleTvWindow.HeartChatTextBounds,
                _ => MapleTvMediaIndexResolver.ResolveChatBounds(
                    snapshot?.ResolvedMediaIndex ?? visualAssets?.DefaultMediaIndex ?? 1,
                    visualAssets?.DefaultMediaIndex ?? 1,
                    visualAssets?.AvailableMediaIndices ?? Array.Empty<int>())
            };
        }

        internal static Point ResolveActorLocalMapleTvChatFamilyTopLeft(
            Point chatOrigin,
            IReadOnlyList<MapleTvAnimationFrame> chatFrames)
        {
            return MapleTvWindow.ResolveFamilyTopLeft(
                chatOrigin,
                MapleTvWindow.ResolveCompositeBounds(240, 90, chatFrames));
        }

        internal static Point ResolveActorLocalMapleTvSenderNameOffset(Rectangle textBounds)
        {
            return new Point(
                textBounds.X + MapleTvNativeSenderNameXDelta,
                textBounds.Y + MapleTvNativeNameYDelta);
        }

        internal static Point ResolveActorLocalMapleTvReceiverNameOffset(Rectangle textBounds, string receiverName)
        {
            int receiverLength = string.IsNullOrEmpty(receiverName) ? 0 : receiverName.Length;
            return new Point(
                textBounds.X + MapleTvNativeReceiverNameXDelta - (receiverLength * MapleTvNativeReceiverGlyphWidth),
                textBounds.Y + MapleTvNativeNameYDelta);
        }

        internal static int ResolveActorLocalMapleTvAnimationTick(MapleTvSnapshot snapshot, int fieldTickCount)
        {
            if (snapshot == null)
            {
                return fieldTickCount;
            }

            if (snapshot.PresentationAnimationTick > 0)
            {
                return snapshot.PresentationAnimationTick;
            }

            if (snapshot.IsShowingMessage)
            {
                return Math.Max(0, snapshot.MessageAnimationTick);
            }

            return snapshot.QueueExists ? 0 : fieldTickCount;
        }

        internal static Point ResolveClientFloatVisualOffset(bool hasFloatPresentation, int tickCount)
        {
            return ResolveClientFloatVisualOffset(hasFloatPresentation, tickCount, 0);
        }

        internal static Point ResolveClientFloatVisualOffset(bool hasFloatPresentation, int tickCount, int startedAtTick)
        {
            if (!hasFloatPresentation || ClientFloatVectorRotateMs <= 0)
            {
                return Point.Zero;
            }

            int elapsedTick = Math.Max(0, tickCount - startedAtTick);
            int normalizedTick = elapsedTick % ClientFloatVectorRotateMs;
            double angle = normalizedTick / (double)ClientFloatVectorRotateMs * Math.PI * 2.0;
            return new Point(
                (int)Math.Round(Math.Cos(angle) * ClientFloatVectorRadiusPx),
                (int)Math.Round(Math.Sin(angle) * ClientFloatVectorRadiusPx));
        }

        private Point ResolveClientFloatVisualOffsetAtTick(int tickCount)
        {
            if (!HasClientFloatPresentation)
            {
                return Point.Zero;
            }

            if (_clientFloatVectorStartedAtTick == int.MinValue)
            {
                _clientFloatVectorStartedAtTick = tickCount;
            }

            return ResolveClientFloatVisualOffset(true, tickCount, _clientFloatVectorStartedAtTick);
        }

        private static int ResolveActorLocalMapleTvChatVariantKey(
            MapleTvVisualAssets visualAssets,
            MapleTvSnapshot snapshot)
        {
            return MapleTvMediaIndexResolver.ResolveChatVariantKeyForMessageType(
                snapshot?.MessageType ?? 0,
                snapshot?.ResolvedMediaIndex ?? visualAssets?.DefaultMediaIndex ?? 1,
                visualAssets?.DefaultMediaIndex ?? 1,
                visualAssets?.AvailableMediaIndices ?? Array.Empty<int>());
        }

        private void DrawActorLocalMapleTvNames(
            SpriteBatch sprite,
            MapleTvSnapshot snapshot,
            int lineX,
            int lineY)
        {
            if (_mapleTvFont == null || snapshot == null)
            {
                return;
            }

            Point senderOffset = ResolveActorLocalMapleTvSenderNameOffset(new Rectangle(lineX, lineY, 1, 1));
            string senderName = TruncateMapleTvLine(snapshot.SenderName, ResolveMapleTvMaxChars(80, 0.36f));
            if (!string.IsNullOrWhiteSpace(senderName))
            {
                ClientTextDrawing.DrawShadowed(
                    sprite,
                    senderName,
                    new Vector2(senderOffset.X, senderOffset.Y),
                    Color.White,
                    _mapleTvFont,
                    0.36f);
            }

            if (!snapshot.UseReceiver)
            {
                return;
            }

            string receiverName = TruncateMapleTvLine(snapshot.ReceiverName, ResolveMapleTvMaxChars(80, 0.36f));
            if (string.IsNullOrWhiteSpace(receiverName))
            {
                return;
            }

            Point receiverOffset = ResolveActorLocalMapleTvReceiverNameOffset(
                new Rectangle(lineX, lineY, 1, 1),
                receiverName);
            ClientTextDrawing.DrawShadowed(
                sprite,
                receiverName,
                new Vector2(receiverOffset.X, receiverOffset.Y),
                Color.White,
                _mapleTvFont,
                0.36f);
        }

        private static void DrawMapleTvFrame(
            MapleTvAnimationFrame frame,
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            int originX,
            int originY)
        {
            frame?.Drawable?.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                originX + frame.Offset.X,
                originY + frame.Offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private static MapleTvAnimationFrame SelectMapleTvFrame(IReadOnlyList<MapleTvAnimationFrame> frames, int tickCount)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            int cycleDuration = frames.Sum(frame => Math.Max(1, frame.DelayMs));
            if (cycleDuration <= 0)
            {
                return frames[0];
            }

            int animationTime = Math.Abs(tickCount % cycleDuration);
            int elapsed = 0;
            foreach (MapleTvAnimationFrame frame in frames)
            {
                elapsed += Math.Max(1, frame.DelayMs);
                if (animationTime < elapsed)
                {
                    return frame;
                }
            }

            return frames[^1];
        }

        private int ResolveMapleTvMaxChars(int width, float scale)
        {
            if (_mapleTvFont == null || width <= 0)
            {
                return 24;
            }

            float glyphWidth = Math.Max(1f, ClientTextDrawing.Measure((GraphicsDevice)null, "W", scale, _mapleTvFont).X);
            return Math.Max(8, (int)(width / glyphWidth));
        }

        private static string TruncateMapleTvLine(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text ?? string.Empty;
            }

            return $"{text.Substring(0, Math.Max(0, maxChars - 3))}...";
        }

        /// <summary>
        /// Updates the cached mirror boundary if the NPC has moved significantly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateMirrorBoundary(Rectangle mirrorBottomRect, ReflectionDrawableBoundary mirrorBottomReflection,
            Func<int, int, ReflectionDrawableBoundary> checkMirrorFieldData)
        {
            _boundaryChecker.UpdateBoundary(CurrentX, CurrentY, mirrorBottomRect, mirrorBottomReflection, checkMirrorFieldData);
        }
    }
}
