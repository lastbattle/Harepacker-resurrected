using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Companions
{
    internal readonly struct PetPersistentState
    {
        public PetPersistentState(
            int skillMask,
            int commandLevel,
            int tameness,
            int questSpeed,
            int fullness,
            bool autoLootEnabled,
            bool autoConsumeHpEnabled,
            int autoConsumeHpItemId,
            InventoryType autoConsumeHpInventoryType)
        {
            SkillMask = skillMask;
            CommandLevel = commandLevel;
            Tameness = tameness;
            QuestSpeed = questSpeed;
            Fullness = fullness;
            AutoLootEnabled = autoLootEnabled;
            AutoConsumeHpEnabled = autoConsumeHpEnabled;
            AutoConsumeHpItemId = autoConsumeHpItemId;
            AutoConsumeHpInventoryType = autoConsumeHpInventoryType;
        }

        public int SkillMask { get; }
        public int CommandLevel { get; }
        public int Tameness { get; }
        public int QuestSpeed { get; }
        public int Fullness { get; }
        public bool AutoLootEnabled { get; }
        public bool AutoConsumeHpEnabled { get; }
        public int AutoConsumeHpItemId { get; }
        public InventoryType AutoConsumeHpInventoryType { get; }
    }

    public enum PetRenderPlane
    {
        BehindOwner = 0,
        UnderFace = 1,
        InFrontOfOwner = 2
    }

    public enum PetAutoSpeechEvent
    {
        LevelUp = 0,
        PreLevelUp = 1,
        Rest = 2,
        HpAlert = 3,
        NoHpPotion = 4,
        NoMpPotion = 5
    }

    public sealed class PetRuntime
    {
        internal const int AutoSpeakingSkillMask = 1 << (int)PetSkillFlag.Smart;
        internal const int FoodTamenessGain = 1;
        private const int MinFullness = 0;
        private const int MaxFullness = 100;
        private const int DefaultFullness = 60;
        private const int MinCommandLevel = 1;
        private const int MaxCommandLevel = 30;
        private const float FollowSpeed = 220f;
        private const float QuestSpeedStepMultiplier = 0.1f;
        private const float FollowSpacing = 28f;
        private const float MultiPetSpacing = 18f;
        private const float SnapDistance = 220f;
        private const float HangOnBackMoveSpeed = 280f;
        private const float BackHangPrimaryX = 18f;
        private const float BackHangPrimaryY = 24f;
        private const float BackHangMultiPrimaryX = 10f;
        private const float BackHangMultiPrimaryY = 18f;
        private const float BackHangSecondaryX = 24f;
        private const float BackHangSecondaryY = 30f;
        private const float BackHangTertiaryX = 36f;
        private const float BackHangTertiaryY = 40f;
        private const int AutoSpeechIntervalMs = 1800000;
        private const int IdleActionStartMs = 5000;
        private const int IdleSleepActionMs = 120000;
        private const int IdleActionRetryMs = 5000;
        private const int SpeechDurationMs = 5000;
        private const int TemporaryActionDurationMs = 2200;

        private static readonly Random SharedRandom = new();
        private static readonly int[] CommandLevelTamenessThresholds =
        {
            0,
            1,
            50,
            100,
            200,
            300,
            400,
            500,
            600,
            700,
            800,
            900,
            1000,
            1100,
            1200,
            1300,
            1400,
            1500,
            1600,
            1700,
            1800,
            1900,
            2000,
            2100,
            2200,
            2300,
            2400,
            2500,
            2600,
            2700
        };

        private readonly PetLoader _loader;
        private AnimationController _animation;
        private readonly IReadOnlyDictionary<PetAutoSpeechEvent, string[]> _eventSpeechLines;
        private readonly Dictionary<PetAutoSpeechEvent, int> _nextEventSpeechIndices = new();

        private int _nextAutoSpeechTick;
        private int _idleSinceTick = -1;
        private int _nextIdleActionTick;
        private string _temporaryActionName;
        private int _temporaryActionExpiresAt;
        private int _commandLevel = MinCommandLevel;
        private int _tameness;
        private int _questSpeed;
        private string _activeSpeechText;
        private int _activeSpeechExpiresAt;
        private bool _hangOnBack;
        private bool _useClientMultiPetHangAction;

        internal PetRuntime(int runtimeId, int slotIndex, PetDefinition definition, PetLoader loader, int initialFullness = DefaultFullness)
        {
            RuntimeId = runtimeId;
            SlotIndex = slotIndex;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _animation = new AnimationController(definition.Animations, "stand1");
            _eventSpeechLines = definition.EventSpeechLines ?? new Dictionary<PetAutoSpeechEvent, string[]>();
            Fullness = Math.Clamp(initialFullness, MinFullness, MaxFullness);
        }

        public int RuntimeId { get; }
        public int SlotIndex { get; internal set; }
        public PetDefinition Definition { get; }
        public bool AutoLootEnabled { get; set; } = true;
        public float X { get; private set; }
        public float Y { get; private set; }
        public bool FacingRight { get; private set; } = true;
        public string CurrentAction => _animation.CurrentAction;
        public int ItemId => Definition.ItemId;
        public int PetWearItemId { get; private set; }
        public string Name => Definition.Name;
        public int ChatBalloonStyle => Definition.ChatBalloonStyle;
        public int CommandLevel => _commandLevel;
        public int Tameness => _tameness;
        public int QuestSpeed => _questSpeed;
        public int SkillMask { get; private set; }
        public bool CanAutoSpeak => HasSkillMask(AutoSpeakingSkillMask);
        public int Fullness { get; private set; }
        public bool IsFull => Fullness >= MaxFullness;
        public bool HasIdleAutoSpeech => HasAutoSpeechEvent(PetAutoSpeechEvent.Rest);
        public bool HasActiveSpeech => !string.IsNullOrWhiteSpace(_activeSpeechText);
        public string ActiveSpeechText => _activeSpeechText;
        public int ActiveSpeechExpiresAt => _activeSpeechExpiresAt;
        public bool AutoConsumeHpEnabled { get; private set; } = true;
        public int AutoConsumeHpItemId { get; private set; }
        public InventoryType AutoConsumeHpInventoryType { get; private set; } = InventoryType.NONE;
        internal PetRenderPlane RenderPlane => _hangOnBack
            ? PetRenderPlane.UnderFace
            : PetRenderPlane.InFrontOfOwner;

        internal void SetPosition(float x, float y, bool facingRight)
        {
            X = x;
            Y = y;
            FacingRight = facingRight;
        }

        public bool HasAutoSpeechEvent(PetAutoSpeechEvent eventType)
        {
            return _eventSpeechLines.TryGetValue(eventType, out string[] lines) &&
                   lines != null &&
                   lines.Length > 0;
        }

        public bool HasSkillMask(int skillMask)
        {
            return skillMask > 0 && (SkillMask & skillMask) == skillMask;
        }

        public string GetNextAutoSpeechLine(PetAutoSpeechEvent eventType)
        {
            if (!HasAutoSpeechEvent(eventType))
            {
                return null;
            }

            string[] lines = _eventSpeechLines[eventType];
            _nextEventSpeechIndices.TryGetValue(eventType, out int nextIndex);
            string line = lines[nextIndex];
            _nextEventSpeechIndices[eventType] = (nextIndex + 1) % lines.Length;
            return line;
        }

        public IDXObject GetCurrentFrame()
        {
            return _animation.GetCurrentFrame();
        }

        internal void ApplyWearItem(int petWearItemId)
        {
            int normalizedWearId = Math.Max(0, petWearItemId);
            if (PetWearItemId == normalizedWearId)
            {
                return;
            }

            PetWearItemId = normalizedWearId;
            string requestedAction = string.IsNullOrWhiteSpace(_animation?.CurrentAction)
                ? "stand1"
                : _animation.CurrentAction;
            PetAnimationSet animations = _loader.LoadAnimationSet(Definition.ItemId, PetWearItemId);
            _animation = new AnimationController(animations, animations.HasAnimation(requestedAction) ? requestedAction : "stand1");
        }

        public bool TryTriggerAutoSpeechEvent(PetAutoSpeechEvent eventType, int currentTime)
        {
            if (!CanAutoSpeak)
            {
                return false;
            }

            string line = GetNextAutoSpeechLine(eventType);
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            SetSpeech(line, currentTime + SpeechDurationMs);
            if (_animation.AnimationSet.GetAvailableActions().Any(action =>
                    string.Equals(action, "chat", StringComparison.OrdinalIgnoreCase)))
            {
                SetTemporaryAction("chat", currentTime + TemporaryActionDurationMs);
            }

            return true;
        }

        internal bool AddSkillMask(int skillMask)
        {
            if (skillMask <= 0)
            {
                return false;
            }

            int updatedMask = SkillMask | skillMask;
            if (updatedMask == SkillMask)
            {
                return false;
            }

            SkillMask = updatedMask;
            return true;
        }

        internal PetPersistentState CapturePersistentState()
        {
            return new PetPersistentState(
                SkillMask,
                _commandLevel,
                _tameness,
                _questSpeed,
                Fullness,
                AutoLootEnabled,
                AutoConsumeHpEnabled,
                AutoConsumeHpItemId,
                AutoConsumeHpInventoryType);
        }

        internal void RestorePersistentState(PetPersistentState state)
        {
            SkillMask = Math.Max(0, state.SkillMask);
            if (state.Tameness > 0 || state.CommandLevel <= MinCommandLevel)
            {
                SetTameness(state.Tameness);
            }
            else
            {
                SetCommandLevel(state.CommandLevel);
            }

            SetQuestSpeed(state.QuestSpeed);
            Fullness = Math.Clamp(state.Fullness, MinFullness, MaxFullness);
            AutoLootEnabled = state.AutoLootEnabled;
            AutoConsumeHpEnabled = state.AutoConsumeHpEnabled;
            SetAutoConsumeHpItem(state.AutoConsumeHpItemId, state.AutoConsumeHpInventoryType);
        }

        internal void Update(PlayerCharacter owner, DropPool dropPool, int ownerId, bool pickupAllowed, int currentTime, float deltaTime, int activePetCount)
        {
            if (owner == null)
            {
                return;
            }

            if (_nextAutoSpeechTick == 0)
            {
                _nextAutoSpeechTick = currentTime + AutoSpeechIntervalMs;
            }

            if (HasActiveSpeech && currentTime >= _activeSpeechExpiresAt)
            {
                ClearSpeech();
            }

            if (!string.IsNullOrEmpty(_temporaryActionName) && currentTime >= _temporaryActionExpiresAt)
            {
                _temporaryActionName = null;
                _temporaryActionExpiresAt = 0;
            }

            FacingRight = owner.FacingRight;
            Vector2 followTarget = GetFollowTarget(owner, currentTime, activePetCount);
            Vector2 desiredTarget = followTarget;
            float moveSpeed = ResolveQuestAdjustedMoveSpeed(_hangOnBack ? HangOnBackMoveSpeed : FollowSpeed);
            bool chasingDrop = false;

            if (dropPool != null)
            {
                if (pickupAllowed && AutoLootEnabled && owner.State != PlayerState.Ladder && owner.State != PlayerState.Rope)
                {
                    PetDropTarget target = dropPool.UpdateChasingDropForPet(
                        RuntimeId,
                        X,
                        Y,
                        ownerId,
                        owner.X,
                        owner.Y,
                        currentTime,
                        deltaTime);

                    if (target != null)
                    {
                        desiredTarget = new Vector2(target.TargetX, target.TargetY);
                        moveSpeed = ResolveQuestAdjustedMoveSpeed(target.ChaseSpeed);
                        chasingDrop = target.IsChasing;

                        dropPool.TryPickUpDropByPet(RuntimeId, X, Y, ownerId, currentTime);
                    }
                }
                else
                {
                    dropPool.ClearPetTarget(RuntimeId);
                }
            }

            MoveTowards(desiredTarget, moveSpeed, deltaTime, followTarget);

            bool idleEligible = IsIdleEligible(owner, chasingDrop, followTarget, desiredTarget);
            UpdateIdleFeedback(currentTime, idleEligible);
            UpdateAutoSpeech(currentTime);
            UpdateAction(owner, chasingDrop, desiredTarget, idleEligible, activePetCount);
            _animation.UpdateFrame(currentTime);
        }

        public bool TryExecuteCommand(string message, int currentTime)
        {
            string normalizedMessage = NormalizeTrigger(message);
            if (string.IsNullOrEmpty(normalizedMessage) || Definition.Commands.Length == 0)
            {
                return false;
            }

            PetCommandDefinition command = Definition.Commands.FirstOrDefault(candidate =>
                IsCommandLevelEligible(candidate) &&
                candidate.Triggers.Any(trigger => string.Equals(NormalizeTrigger(trigger), normalizedMessage, StringComparison.OrdinalIgnoreCase)));
            if (command == null)
            {
                return false;
            }

            bool isSuccess = SharedRandom.Next(100) < command.SuccessProbability;
            ApplyReaction(isSuccess ? command.SuccessReaction : command.FailureReaction, currentTime);
            if (isSuccess && command.ClosenessDelta > 0)
            {
                AddTameness(command.ClosenessDelta);
            }

            return true;
        }

        public void SetCommandLevel(int level)
        {
            int boundedLevel = Math.Clamp(level, MinCommandLevel, MaxCommandLevel);
            SetTameness(ResolveMinimumTamenessForCommandLevel(boundedLevel));
        }

        public void SetAutoConsumeHpEnabled(bool enabled)
        {
            AutoConsumeHpEnabled = enabled;
        }

        public void SetAutoConsumeHpItem(int itemId, InventoryType inventoryType)
        {
            if (itemId <= 0 || inventoryType == InventoryType.NONE)
            {
                AutoConsumeHpItemId = 0;
                AutoConsumeHpInventoryType = InventoryType.NONE;
                return;
            }

            AutoConsumeHpItemId = itemId;
            AutoConsumeHpInventoryType = inventoryType;
        }

        public bool TryTriggerSlangFeedback(int currentTime)
        {
            return TryApplyDialogFeedback(Definition.SlangFeedback, success: true, currentTime: currentTime);
        }

        internal bool CanTriggerSlangFeedback()
        {
            return Definition?.SlangFeedback != null
                && ((Definition.SlangFeedback.SuccessLines?.Length ?? 0) > 0
                    || (Definition.SlangFeedback.FailureLines?.Length ?? 0) > 0);
        }

        public bool TryTriggerFoodFeedback(int variant, bool success, int currentTime)
        {
            if (variant < 1 || variant > 4 ||
                Definition.FoodFeedback == null ||
                !Definition.FoodFeedback.TryGetValue(variant, out PetDialogFeedbackDefinition feedback))
            {
                return false;
            }

            return TryApplyDialogFeedback(feedback, success, currentTime);
        }

        internal bool CanConsumeFood(int fullnessIncrease)
        {
            return fullnessIncrease > 0 && !IsFull;
        }

        internal bool TryAddQuestTameness(int amount)
        {
            if (amount == 0)
            {
                return false;
            }

            SetTameness(Math.Max(0, _tameness + amount));
            return true;
        }

        internal bool TryAddQuestSpeed(int amount)
        {
            if (amount == 0)
            {
                return false;
            }

            int updatedSpeed = Math.Max(0, _questSpeed + amount);
            if (updatedSpeed == _questSpeed)
            {
                return false;
            }

            SetQuestSpeed(updatedSpeed);
            return true;
        }

        internal float ResolveQuestAdjustedMoveSpeed(float baseSpeed)
        {
            return ApplyQuestSpeedToMoveSpeed(baseSpeed, _questSpeed);
        }

        internal bool TryFeed(int fullnessIncrease, int tamenessGain = FoodTamenessGain)
        {
            if (!CanConsumeFood(fullnessIncrease))
            {
                return false;
            }

            Fullness = Math.Clamp(Fullness + fullnessIncrease, MinFullness, MaxFullness);
            if (tamenessGain > 0)
            {
                AddTameness(tamenessGain);
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX, int mapShiftY, int centerX, int centerY, PetRenderPlane plane)
        {
            if (RenderPlane != plane)
            {
                return;
            }

            IDXObject frame = _animation.GetCurrentFrame();
            if (frame == null)
            {
                return;
            }

            int screenX = (int)X - mapShiftX + centerX;
            int screenY = (int)Y - mapShiftY + centerY;
            frame.DrawBackground(spriteBatch, skeletonRenderer, null, screenX, screenY, Color.White, !FacingRight, null);
        }

        private Vector2 GetFollowTarget(PlayerCharacter owner, int currentTime, int activePetCount)
        {
            Point? ownerBodyOrigin = owner.TryGetCurrentBodyOrigin(currentTime);
            return ResolveAnchorTarget(
                owner.X,
                owner.Y,
                ownerBodyOrigin,
                owner.GetHitbox(),
                owner.FacingRight,
                owner.State,
                SlotIndex,
                activePetCount,
                out _hangOnBack,
                out _useClientMultiPetHangAction);
        }

        internal static Vector2 ResolveAnchorTarget(
            float ownerX,
            float ownerY,
            Point? ownerBodyOrigin,
            Rectangle ownerHitbox,
            bool ownerFacingRight,
            PlayerState ownerState,
            int slotIndex,
            int activePetCount,
            out bool hangOnBack,
            out bool useMultiPetHangLayout)
        {
            hangOnBack = ownerState == PlayerState.Ladder || ownerState == PlayerState.Rope;
            useMultiPetHangLayout = hangOnBack && slotIndex == 0 && activePetCount > 1;

            float backDirection = ownerFacingRight ? -1f : 1f;
            if (!hangOnBack)
            {
                float offsetX = backDirection * (FollowSpacing + slotIndex * MultiPetSpacing);
                return new Vector2(ownerX + offsetX, ownerY);
            }

            float xOffset;
            float yOffset;
            switch (slotIndex)
            {
                case 0:
                    xOffset = useMultiPetHangLayout ? BackHangMultiPrimaryX : BackHangPrimaryX;
                    yOffset = useMultiPetHangLayout ? BackHangMultiPrimaryY : BackHangPrimaryY;
                    break;
                case 1:
                    xOffset = BackHangSecondaryX;
                    yOffset = BackHangSecondaryY;
                    break;
                default:
                    xOffset = BackHangTertiaryX;
                    yOffset = BackHangTertiaryY;
                    break;
            }

            float baseY = ownerHitbox.IsEmpty ? ownerY - 42f : ownerHitbox.Top + yOffset;
            if (ownerBodyOrigin.HasValue)
            {
                baseY = ownerBodyOrigin.Value.Y + yOffset;
            }

            return new Vector2(ownerX + (backDirection * xOffset), baseY);
        }

        private void MoveTowards(Vector2 desiredTarget, float moveSpeed, float deltaTime, Vector2 followTarget)
        {
            var current = new Vector2(X, Y);
            Vector2 toTarget = desiredTarget - current;
            float distance = toTarget.Length();

            if (distance > SnapDistance)
            {
                X = followTarget.X;
                Y = followTarget.Y;
                return;
            }

            if (distance <= 0.01f)
            {
                X = desiredTarget.X;
                Y = desiredTarget.Y;
                return;
            }

            float maxStep = moveSpeed * Math.Max(deltaTime, 0.001f);
            Vector2 step = distance <= maxStep
                ? toTarget
                : Vector2.Normalize(toTarget) * maxStep;

            X += step.X;
            Y += step.Y;
        }

        private bool IsIdleEligible(PlayerCharacter owner, bool chasingDrop, Vector2 followTarget, Vector2 desiredTarget)
        {
            if (owner.State != PlayerState.Standing || chasingDrop)
            {
                return false;
            }

            float deltaX = desiredTarget.X - X;
            float deltaY = desiredTarget.Y - Y;
            float followDeltaX = followTarget.X - X;
            float followDeltaY = followTarget.Y - Y;
            return Math.Abs(deltaX) <= 6f &&
                   Math.Abs(deltaY) <= 12f &&
                   Math.Abs(followDeltaX) <= 6f &&
                   Math.Abs(followDeltaY) <= 12f;
        }

        private void UpdateIdleFeedback(int currentTime, bool idleEligible)
        {
            if (!idleEligible)
            {
                _idleSinceTick = -1;
                _nextIdleActionTick = 0;
                _temporaryActionName = null;
                _temporaryActionExpiresAt = 0;
                return;
            }

            if (_idleSinceTick < 0)
            {
                _idleSinceTick = currentTime;
                _nextIdleActionTick = currentTime + IdleActionStartMs;
                return;
            }

            int idleDuration = currentTime - _idleSinceTick;
            if (idleDuration >= IdleSleepActionMs && Definition.Animations.GetAvailableActions().Any(action =>
                    string.Equals(action, "rest0", StringComparison.OrdinalIgnoreCase)))
            {
                SetTemporaryAction("rest0", currentTime + TemporaryActionDurationMs);
                return;
            }

            if (currentTime < _nextIdleActionTick || Definition.RandomIdleActions.Length == 0)
            {
                return;
            }

            _nextIdleActionTick = currentTime + IdleActionRetryMs;
            if ((SharedRandom.Next() & 1) == 0)
            {
                return;
            }

            string actionName = Definition.RandomIdleActions[SharedRandom.Next(Definition.RandomIdleActions.Length)];
            SetTemporaryAction(actionName, currentTime + TemporaryActionDurationMs);
        }

        private void UpdateAutoSpeech(int currentTime)
        {
            if (!CanAutoSpeak ||
                _nextAutoSpeechTick == 0 ||
                currentTime < _nextAutoSpeechTick ||
                !HasAutoSpeechEvent(PetAutoSpeechEvent.Rest))
            {
                return;
            }

            _nextAutoSpeechTick = currentTime + AutoSpeechIntervalMs;
            string line = GetNextAutoSpeechLine(PetAutoSpeechEvent.Rest);
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            SetSpeech(line, currentTime + SpeechDurationMs);
            if (_animation.AnimationSet.GetAvailableActions().Any(action =>
                    string.Equals(action, "chat", StringComparison.OrdinalIgnoreCase)))
            {
                SetTemporaryAction("chat", currentTime + TemporaryActionDurationMs);
            }
        }

        private void ApplyReaction(PetReactionDefinition reaction, int currentTime)
        {
            if (reaction == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(reaction.ActionName) &&
                _animation.AnimationSet.GetAvailableActions().Any(action =>
                    string.Equals(action, reaction.ActionName, StringComparison.OrdinalIgnoreCase)))
            {
                SetTemporaryAction(reaction.ActionName, currentTime + TemporaryActionDurationMs);
            }

            if (reaction.SpeechLines.Length > 0)
            {
                string line = reaction.SpeechLines[SharedRandom.Next(reaction.SpeechLines.Length)];
                TryShowSpeech(line, currentTime);
            }
        }

        private void UpdateAction(PlayerCharacter owner, bool chasingDrop, Vector2 desiredTarget, bool idleEligible, int activePetCount)
        {
            string action = "stand1";
            float deltaX = desiredTarget.X - X;
            float deltaY = desiredTarget.Y - Y;

            if (_hangOnBack)
            {
                action = ShouldUseClientMultiPetHangAction(activePetCount)
                    ? PetDefinition.ClientMultiPetHangActionName
                    : "hang";
            }
            else if (owner.State == PlayerState.Jumping || owner.State == PlayerState.Falling || owner.State == PlayerState.Flying || Math.Abs(deltaY) > 12f)
            {
                action = "fly";
            }
            else if (chasingDrop || Math.Abs(deltaX) > 6f)
            {
                action = "move";
            }
            else if (idleEligible && !string.IsNullOrWhiteSpace(_temporaryActionName))
            {
                action = _temporaryActionName;
            }

            _animation.SetAction(action);
        }

        private bool ShouldUseClientMultiPetHangAction(int activePetCount)
        {
            return _useClientMultiPetHangAction
                   && activePetCount > 1
                   && _animation.AnimationSet.HasAnimation(PetDefinition.ClientMultiPetHangActionName);
        }

        private void SetTemporaryAction(string actionName, int expiresAt)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            _temporaryActionName = actionName;
            _temporaryActionExpiresAt = expiresAt;
        }

        private void SetSpeech(string text, int expiresAt)
        {
            _activeSpeechText = text?.Trim();
            _activeSpeechExpiresAt = expiresAt;
        }

        private void ClearSpeech()
        {
            _activeSpeechText = null;
            _activeSpeechExpiresAt = 0;
        }

        private static string NormalizeTrigger(string trigger)
        {
            return string.IsNullOrWhiteSpace(trigger)
                ? string.Empty
                : trigger.Trim().Replace(" ", string.Empty);
        }

        private void AddTameness(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SetTameness(_tameness + amount);
        }

        private void SetTameness(int tameness)
        {
            _tameness = Math.Clamp(tameness, 0, CommandLevelTamenessThresholds[^1]);
            _commandLevel = ResolveCommandLevelForTameness(_tameness);
        }

        private void SetQuestSpeed(int questSpeed)
        {
            _questSpeed = Math.Max(0, questSpeed);
        }

        internal static float ApplyQuestSpeedToMoveSpeed(float baseSpeed, int questSpeed)
        {
            if (baseSpeed <= 0f || questSpeed <= 0)
            {
                return baseSpeed;
            }

            return baseSpeed * (1f + (Math.Max(0, questSpeed) * QuestSpeedStepMultiplier));
        }

        internal static int ResolveCommandLevelForTameness(int tameness)
        {
            int boundedTameness = Math.Max(0, tameness);
            for (int i = CommandLevelTamenessThresholds.Length - 1; i >= 0; i--)
            {
                if (boundedTameness >= CommandLevelTamenessThresholds[i])
                {
                    return i + 1;
                }
            }

            return MinCommandLevel;
        }

        internal static int ResolveMinimumTamenessForCommandLevel(int commandLevel)
        {
            int boundedLevel = Math.Clamp(commandLevel, MinCommandLevel, MaxCommandLevel);
            return CommandLevelTamenessThresholds[boundedLevel - 1];
        }

        private bool IsCommandLevelEligible(PetCommandDefinition command)
        {
            return command != null && _commandLevel >= command.LevelMin && _commandLevel <= command.LevelMax;
        }

        private bool TryApplyDialogFeedback(PetDialogFeedbackDefinition feedback, bool success, int currentTime)
        {
            if (feedback == null)
            {
                return false;
            }

            string[] lines = success ? feedback.SuccessLines : feedback.FailureLines;
            if ((lines == null || lines.Length == 0) && success)
            {
                lines = feedback.FailureLines;
            }
            else if ((lines == null || lines.Length == 0) && !success)
            {
                lines = feedback.SuccessLines;
            }

            if (lines == null || lines.Length == 0)
            {
                return false;
            }

            string line = lines[SharedRandom.Next(lines.Length)];
            return TryShowSpeech(line, currentTime);
        }

        private bool TryShowSpeech(string line, int currentTime)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            SetSpeech(line, currentTime + SpeechDurationMs);
            if (Definition.Animations.GetAvailableActions().Any(action =>
                    string.Equals(action, "chat", StringComparison.OrdinalIgnoreCase)))
            {
                SetTemporaryAction("chat", currentTime + TemporaryActionDurationMs);
            }

            return true;
        }
    }

    public sealed class PetController
    {
        internal enum PetFoodItemUseFailureReason
        {
            None = 0,
            NoActivePets = 1,
            NoCompatiblePets = 2,
            NoHungryCompatiblePets = 3
        }

        internal readonly struct PetFoodItemUsePlan
        {
            public int SlotIndex { get; init; }
            public int FullnessIncrease { get; init; }
            public bool ConsumeItem { get; init; }
        }

        private const int MaxPets = 3;
        private const int DefaultPetItemId = 5000000;
        private const int PickupForbiddenMapId = 209080000;
        private const int SpecialistSpeechCooldownMs = 4000;

        private readonly PetLoader _loader;
        private readonly List<PetRuntime> _activePets = new();
        private readonly Dictionary<int, Queue<PetPersistentState>> _persistedStateByItemId = new();
        private readonly Random _specialistSpeechRandom = new();
        private int _nextRuntimeId = 1;
        private int _lastSpecialistSpeechTick = int.MinValue;
        private Func<int> _currentMapIdProvider;
        private bool _fieldUsageBlocked;
        private string _fieldUsageRestrictionMessage;

        public PetController(GraphicsDevice device)
        {
            _loader = new PetLoader(device);
        }

        public IReadOnlyList<PetRuntime> ActivePets => _activePets;
        public bool IsFieldUsageBlocked => _fieldUsageBlocked;
        public string FieldUsageRestrictionMessage => _fieldUsageRestrictionMessage;

        public void SetFieldUsageRestriction(bool blocked, string message = null)
        {
            _fieldUsageBlocked = blocked;
            _fieldUsageRestrictionMessage = blocked ? message : null;
        }

        public void SetCurrentMapIdProvider(Func<int> currentMapIdProvider)
        {
            _currentMapIdProvider = currentMapIdProvider;
        }

        public void EnsureDefaultPetActive(PlayerCharacter owner)
        {
            if (_fieldUsageBlocked)
            {
                return;
            }

            if (_activePets.Count > 0)
            {
                if (owner != null)
                {
                    SyncPositionsToOwner(owner);
                }

                return;
            }

            SetActivePet(0, DefaultPetItemId, owner);
        }

        public bool SetActivePet(int slotIndex, int petItemId, PlayerCharacter owner = null)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            if (slotIndex < 0 || slotIndex >= MaxPets)
            {
                return false;
            }

            PetDefinition definition = _loader.Load(petItemId);
            if (definition == null)
            {
                return false;
            }

            int insertIndex = Math.Min(slotIndex, _activePets.Count);
            var pet = new PetRuntime(_nextRuntimeId++, insertIndex, definition, _loader);
            if (TryRestorePersistedState(petItemId, out PetPersistentState persistedState))
            {
                pet.RestorePersistentState(persistedState);
            }

            _activePets.Insert(insertIndex, pet);
            if (_activePets.Count > MaxPets)
            {
                PersistPetState(_activePets[^1]);
                _activePets.RemoveAt(_activePets.Count - 1);
            }

            ReindexPets(owner);
            return true;
        }

        public void RemovePetAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _activePets.Count)
            {
                return;
            }

            PersistPetState(_activePets[slotIndex]);
            _activePets.RemoveAt(slotIndex);
            ReindexPets();
        }

        public void SetAutoLootEnabled(int slotIndex, bool enabled)
        {
            if (_fieldUsageBlocked)
            {
                return;
            }

            if (slotIndex < 0 || slotIndex >= _activePets.Count)
            {
                return;
            }

            _activePets[slotIndex].AutoLootEnabled = enabled;
        }

        public bool TryExecuteCommand(string message, int currentTime)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            for (int i = 0; i < _activePets.Count; i++)
            {
                if (_activePets[i].TryExecuteCommand(message, currentTime))
                {
                    return true;
                }
            }

            return false;
        }

        public PetRuntime GetPetAt(int slotIndex)
        {
            return slotIndex < 0 || slotIndex >= _activePets.Count
                ? null
                : _activePets[slotIndex];
        }

        public bool TrySetCommandLevel(int slotIndex, int level)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            PetRuntime pet = GetPetAt(slotIndex);
            if (pet == null)
            {
                return false;
            }

            pet.SetCommandLevel(level);
            return true;
        }

        public bool TrySetAutoConsumeHpEnabled(int slotIndex, bool enabled)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            PetRuntime pet = GetPetAt(slotIndex);
            if (pet == null)
            {
                return false;
            }

            pet.SetAutoConsumeHpEnabled(enabled);
            return true;
        }

        public bool TrySetAutoConsumeHpItem(int slotIndex, int itemId, InventoryType inventoryType)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            PetRuntime pet = GetPetAt(slotIndex);
            if (pet == null)
            {
                return false;
            }

            pet.SetAutoConsumeHpItem(itemId, inventoryType);
            return true;
        }

        public bool TryTriggerSlangFeedback(int slotIndex, int currentTime)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            PetRuntime pet = GetPetAt(slotIndex);
            return pet != null && pet.TryTriggerSlangFeedback(currentTime);
        }

        public bool TryTriggerSpecialistChatFeedback(string message, int currentTime)
        {
            if (_fieldUsageBlocked ||
                string.IsNullOrWhiteSpace(message) ||
                _activePets.Count == 0)
            {
                return false;
            }

            if (_lastSpecialistSpeechTick != int.MinValue &&
                unchecked(currentTime - _lastSpecialistSpeechTick) < SpecialistSpeechCooldownMs)
            {
                return false;
            }

            var eligiblePets = new List<PetRuntime>();
            for (int i = 0; i < _activePets.Count; i++)
            {
                PetRuntime pet = _activePets[i];
                if (pet == null || pet.HasActiveSpeech || !pet.CanTriggerSlangFeedback())
                {
                    continue;
                }

                eligiblePets.Add(pet);
            }

            if (eligiblePets.Count == 0)
            {
                return false;
            }

            int startIndex = eligiblePets.Count > 1 ? _specialistSpeechRandom.Next(eligiblePets.Count) : 0;
            for (int offset = 0; offset < eligiblePets.Count; offset++)
            {
                PetRuntime selectedPet = eligiblePets[(startIndex + offset) % eligiblePets.Count];
                if (!selectedPet.TryTriggerSlangFeedback(currentTime))
                {
                    continue;
                }

                _lastSpecialistSpeechTick = currentTime;
                return true;
            }

            return false;
        }

        public bool TryTriggerSpeechEvent(PetAutoSpeechEvent eventType, int currentTime, int? slotIndex = null)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            if (slotIndex.HasValue)
            {
                PetRuntime pet = GetPetAt(slotIndex.Value);
                return pet != null && pet.TryTriggerAutoSpeechEvent(eventType, currentTime);
            }

            return TryPetAutoSpeaking(eventType, currentTime);
        }

        public bool TryPetAutoSpeaking(PetAutoSpeechEvent eventType, int currentTime)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            return TryDispatchPetAutoSpeakingByEvent(_activePets, eventType, currentTime) > 0;
        }

        internal static int TryDispatchPetAutoSpeakingByEvent(
            IReadOnlyList<PetRuntime> activePets,
            PetAutoSpeechEvent eventType,
            int currentTime)
        {
            if (activePets == null)
            {
                return 0;
            }

            int triggeredCount = 0;
            for (int i = 0; i < activePets.Count; i++)
            {
                PetRuntime pet = i < activePets.Count ? activePets[i] : null;
                if (pet == null)
                {
                    break;
                }

                if (pet.TryTriggerAutoSpeechEvent(eventType, currentTime))
                {
                    triggeredCount++;
                }
            }

            return triggeredCount;
        }

        public bool TryTriggerFoodFeedback(int slotIndex, int variant, bool success, int currentTime)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            PetRuntime pet = GetPetAt(slotIndex);
            return pet != null && pet.TryTriggerFoodFeedback(variant, success, currentTime);
        }

        internal bool TryGrantSkillMask(
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum,
            int skillMask,
            out int slotIndex)
        {
            if (_fieldUsageBlocked)
            {
                slotIndex = -1;
                return false;
            }

            return TryGrantSkillMask(_activePets, supportedPetItemIds, recallLimit, tamenessMinimum, tamenessMaximum, skillMask, out slotIndex);
        }

        internal bool TryGrantTameness(
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum,
            int amount,
            out int slotIndex)
        {
            if (_fieldUsageBlocked)
            {
                slotIndex = -1;
                return false;
            }

            return TryGrantTameness(_activePets, supportedPetItemIds, recallLimit, tamenessMinimum, tamenessMaximum, amount, out slotIndex);
        }

        internal bool TryGrantSpeed(
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum,
            int amount,
            out int slotIndex)
        {
            if (_fieldUsageBlocked)
            {
                slotIndex = -1;
                return false;
            }

            return TryGrantSpeed(_activePets, supportedPetItemIds, recallLimit, tamenessMinimum, tamenessMaximum, amount, out slotIndex);
        }

        internal bool TryPlanFoodItemUse(
            IReadOnlyCollection<int> supportedPetItemIds,
            int fullnessIncrease,
            out PetFoodItemUsePlan plan)
        {
            if (_fieldUsageBlocked)
            {
                plan = default;
                return false;
            }

            return TryPlanFoodItemUse(_activePets, supportedPetItemIds, fullnessIncrease, out plan, out _);
        }

        internal bool TryPlanFoodItemUse(
            IReadOnlyCollection<int> supportedPetItemIds,
            int fullnessIncrease,
            out PetFoodItemUsePlan plan,
            out PetFoodItemUseFailureReason failureReason)
        {
            if (_fieldUsageBlocked)
            {
                plan = default;
                failureReason = PetFoodItemUseFailureReason.None;
                return false;
            }

            return TryPlanFoodItemUse(_activePets, supportedPetItemIds, fullnessIncrease, out plan, out failureReason);
        }

        internal bool TryExecuteFoodItemUse(PetFoodItemUsePlan plan, int currentTime, out int fedSlotIndex)
        {
            if (_fieldUsageBlocked)
            {
                fedSlotIndex = -1;
                return false;
            }

            return TryExecuteFoodItemUse(_activePets, plan, currentTime, out fedSlotIndex);
        }

        public IEnumerable<PetRuntime> GetSpeakingPets(int currentTime)
        {
            if (_fieldUsageBlocked)
            {
                return Enumerable.Empty<PetRuntime>();
            }

            return _activePets.Where(pet => pet.HasActiveSpeech && pet.ActiveSpeechExpiresAt > currentTime);
        }

        internal bool HasCompatibleActivePet(
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum)
        {
            if (_fieldUsageBlocked)
            {
                return false;
            }

            return HasCompatibleActivePet(_activePets, supportedPetItemIds, recallLimit, tamenessMinimum, tamenessMaximum);
        }

        private PetRuntime SelectPetForFoodItem(IReadOnlyCollection<int> supportedPetItemIds, int fullnessIncrease)
        {
            return SelectPetForFoodItem(_activePets, supportedPetItemIds, fullnessIncrease);
        }

        private static PetRuntime SelectPetForFoodItem(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int fullnessIncrease)
        {
            return SelectPetForFoodItem(activePets, supportedPetItemIds, fullnessIncrease, out _);
        }

        internal static bool TryPlanFoodItemUse(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int fullnessIncrease,
            out PetFoodItemUsePlan plan,
            out PetFoodItemUseFailureReason failureReason)
        {
            plan = default;
            failureReason = PetFoodItemUseFailureReason.None;

            PetRuntime pet = SelectPetForFoodItem(activePets, supportedPetItemIds, fullnessIncrease, out failureReason);
            if (pet == null)
            {
                return false;
            }

            plan = new PetFoodItemUsePlan
            {
                SlotIndex = pet.SlotIndex,
                FullnessIncrease = Math.Max(0, fullnessIncrease),
                ConsumeItem = pet.CanConsumeFood(fullnessIncrease)
            };
            return true;
        }

        internal static bool TryExecuteFoodItemUse(
            IReadOnlyList<PetRuntime> activePets,
            PetFoodItemUsePlan plan,
            int currentTime,
            out int fedSlotIndex)
        {
            fedSlotIndex = -1;

            PetRuntime pet = activePets?.FirstOrDefault(candidate => candidate != null && candidate.SlotIndex == plan.SlotIndex);
            if (pet == null)
            {
                return false;
            }

            bool success = plan.ConsumeItem && pet.TryFeed(plan.FullnessIncrease);
            int variant = ResolveFoodFeedbackVariant(pet);
            bool handled = pet.TryTriggerFoodFeedback(variant, success, currentTime);
            fedSlotIndex = pet.SlotIndex;
            return handled || success || !plan.ConsumeItem;
        }

        internal static bool TryGrantSkillMask(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum,
            int skillMask,
            out int slotIndex)
        {
            slotIndex = -1;

            PetRuntime pet = SelectPetForSkillReward(activePets, supportedPetItemIds, recallLimit, tamenessMinimum, tamenessMaximum, skillMask);
            if (pet == null || !pet.AddSkillMask(skillMask))
            {
                return false;
            }

            slotIndex = pet.SlotIndex;
            return true;
        }

        internal static bool TryGrantTameness(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum,
            int amount,
            out int slotIndex)
        {
            slotIndex = -1;

            PetRuntime pet = SelectPetForQuestReward(activePets, supportedPetItemIds, recallLimit, tamenessMinimum, tamenessMaximum);
            if (pet == null || !pet.TryAddQuestTameness(amount))
            {
                return false;
            }

            slotIndex = pet.SlotIndex;
            return true;
        }

        internal static bool TryGrantSpeed(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum,
            int amount,
            out int slotIndex)
        {
            slotIndex = -1;

            PetRuntime pet = SelectPetForQuestReward(activePets, supportedPetItemIds, recallLimit, tamenessMinimum, tamenessMaximum);
            if (pet == null || !pet.TryAddQuestSpeed(amount))
            {
                return false;
            }

            slotIndex = pet.SlotIndex;
            return true;
        }

        internal static bool HasCompatibleActivePet(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum)
        {
            return SelectPetForQuestReward(activePets, supportedPetItemIds, recallLimit, tamenessMinimum, tamenessMaximum) != null;
        }

        private static PetRuntime SelectPetForFoodItem(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int fullnessIncrease,
            out PetFoodItemUseFailureReason failureReason)
        {
            if (activePets == null || activePets.Count == 0)
            {
                failureReason = PetFoodItemUseFailureReason.NoActivePets;
                return null;
            }

            IEnumerable<PetRuntime> compatiblePets = supportedPetItemIds == null || supportedPetItemIds.Count == 0
                ? activePets.Where(pet => pet != null)
                : activePets.Where(pet => pet != null && supportedPetItemIds.Contains(pet.ItemId));

            List<PetRuntime> compatiblePetList = compatiblePets.ToList();
            if (compatiblePetList.Count == 0)
            {
                failureReason = PetFoodItemUseFailureReason.NoCompatiblePets;
                return null;
            }

            PetRuntime hungryPet = compatiblePetList.FirstOrDefault(pet => pet.CanConsumeFood(fullnessIncrease));
            if (hungryPet != null)
            {
                failureReason = PetFoodItemUseFailureReason.None;
                return hungryPet;
            }

            failureReason = PetFoodItemUseFailureReason.NoHungryCompatiblePets;
            return null;
        }

        private static PetRuntime SelectPetForSkillReward(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum,
            int skillMask)
        {
            if (activePets == null || activePets.Count == 0)
            {
                return null;
            }

            if (recallLimit.HasValue && recallLimit.Value > 0 && activePets.Count > recallLimit.Value)
            {
                return null;
            }

            IEnumerable<PetRuntime> compatiblePets = EnumerateCompatibleQuestPets(
                activePets,
                supportedPetItemIds,
                tamenessMinimum,
                tamenessMaximum);

            return compatiblePets.FirstOrDefault(pet => !pet.HasSkillMask(skillMask))
                ?? compatiblePets.FirstOrDefault();
        }

        private static PetRuntime SelectPetForQuestReward(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum)
        {
            if (activePets == null || activePets.Count == 0)
            {
                return null;
            }

            if (recallLimit.HasValue && recallLimit.Value > 0 && activePets.Count > recallLimit.Value)
            {
                return null;
            }

            IEnumerable<PetRuntime> compatiblePets = EnumerateCompatibleQuestPets(
                activePets,
                supportedPetItemIds,
                tamenessMinimum,
                tamenessMaximum);

            return compatiblePets.FirstOrDefault();
        }

        private static IEnumerable<PetRuntime> EnumerateCompatibleQuestPets(
            IReadOnlyList<PetRuntime> activePets,
            IReadOnlyCollection<int> supportedPetItemIds,
            int? tamenessMinimum,
            int? tamenessMaximum)
        {
            return activePets.Where(pet =>
                pet != null &&
                (supportedPetItemIds == null || supportedPetItemIds.Count == 0 || supportedPetItemIds.Contains(pet.ItemId)) &&
                (!tamenessMinimum.HasValue || pet.Tameness >= tamenessMinimum.Value) &&
                (!tamenessMaximum.HasValue || pet.Tameness <= tamenessMaximum.Value));
        }

        private static int ResolveFoodFeedbackVariant(PetRuntime pet)
        {
            IReadOnlyDictionary<int, (int MinLevel, int MaxLevel)> ranges = pet?.Definition?.FoodFeedbackLevelRanges;
            IReadOnlyDictionary<int, PetDialogFeedbackDefinition> feedback = pet?.Definition?.FoodFeedback;
            if (feedback == null || feedback.Count == 0)
            {
                return pet?.CommandLevel switch
                {
                    <= 9 => 1,
                    <= 19 => 2,
                    <= 29 => 3,
                    _ => 4
                };
            }

            if (ranges != null)
            {
                foreach (KeyValuePair<int, (int MinLevel, int MaxLevel)> entry in ranges.OrderBy(pair => pair.Key))
                {
                    if (pet.CommandLevel >= entry.Value.MinLevel && pet.CommandLevel <= entry.Value.MaxLevel)
                    {
                        return feedback.ContainsKey(entry.Key)
                            ? entry.Key
                            : ResolveNearestAvailableFoodFeedbackVariant(feedback.Keys, entry.Key);
                    }
                }
            }

            int fallbackVariant = pet?.CommandLevel switch
            {
                <= 9 => 1,
                <= 19 => 2,
                <= 29 => 3,
                _ => 4
            };

            return feedback.ContainsKey(fallbackVariant)
                ? fallbackVariant
                : ResolveNearestAvailableFoodFeedbackVariant(feedback.Keys, fallbackVariant);
        }

        private static int ResolveNearestAvailableFoodFeedbackVariant(IEnumerable<int> availableVariants, int desiredVariant)
        {
            int[] orderedVariants = availableVariants?
                .Where(static variant => variant >= 1 && variant <= 4)
                .Distinct()
                .OrderBy(static variant => variant)
                .ToArray()
                ?? Array.Empty<int>();

            if (orderedVariants.Length == 0)
            {
                return Math.Clamp(desiredVariant, 1, 4);
            }

            int boundedDesiredVariant = Math.Clamp(desiredVariant, 1, 4);
            return orderedVariants
                .OrderBy(variant => Math.Abs(variant - boundedDesiredVariant))
                .ThenBy(variant => variant)
                .First();
        }

        public void Update(PlayerCharacter owner, DropPool dropPool, int currentTime, float deltaTime)
        {
            if (_fieldUsageBlocked || owner == null || !owner.IsAlive || _activePets.Count == 0)
            {
                return;
            }

            bool pickupAllowed = (_currentMapIdProvider?.Invoke() ?? -1) != PickupForbiddenMapId;
            int ownerId = owner.Build?.Id ?? 1;

            for (int i = 0; i < _activePets.Count; i++)
            {
                _activePets[i].Update(owner, dropPool, ownerId, pickupAllowed, currentTime, deltaTime, _activePets.Count);
            }
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX, int mapShiftY, int centerX, int centerY, PetRenderPlane plane)
        {
            if (_fieldUsageBlocked)
            {
                return;
            }

            for (int i = 0; i < _activePets.Count; i++)
            {
                _activePets[i].Draw(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, plane);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _activePets.Count; i++)
            {
                PersistPetState(_activePets[i]);
            }

            _activePets.Clear();
        }

        private void PersistPetState(PetRuntime pet)
        {
            if (pet == null || pet.ItemId <= 0)
            {
                return;
            }

            if (!_persistedStateByItemId.TryGetValue(pet.ItemId, out Queue<PetPersistentState> states))
            {
                states = new Queue<PetPersistentState>();
                _persistedStateByItemId[pet.ItemId] = states;
            }

            states.Enqueue(pet.CapturePersistentState());
        }

        private bool TryRestorePersistedState(int petItemId, out PetPersistentState state)
        {
            state = default;
            if (petItemId <= 0 ||
                !_persistedStateByItemId.TryGetValue(petItemId, out Queue<PetPersistentState> states) ||
                states.Count == 0)
            {
                return false;
            }

            state = states.Dequeue();
            if (states.Count == 0)
            {
                _persistedStateByItemId.Remove(petItemId);
            }

            return true;
        }

        private void ReindexPets(PlayerCharacter owner = null)
        {
            for (int i = 0; i < _activePets.Count; i++)
            {
                _activePets[i].SlotIndex = i;
            }

            if (owner != null)
            {
                SyncPositionsToOwner(owner);
            }
        }

        private void SyncPositionsToOwner(PlayerCharacter owner)
        {
            for (int i = 0; i < _activePets.Count; i++)
            {
                float direction = owner.FacingRight ? -1f : 1f;
                float offsetX = direction * (28f + i * 18f);
                _activePets[i].SetPosition(owner.X + offsetX, owner.Y, owner.FacingRight);
            }
        }
    }
}
