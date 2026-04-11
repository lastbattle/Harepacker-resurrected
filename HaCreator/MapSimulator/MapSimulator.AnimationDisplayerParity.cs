using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private static readonly string AnimationDisplayerNewYearEffectUol =
            RelationshipOverlayClientStringPoolText.ResolveNewYearCardEffectPath();
        private const string AnimationDisplayerFireCrackerEffectUol = "Effect/OnUserEff.img/itemEffect/firework/5680024";
        private const string AnimationDisplayerGenericUserStateEffectUol = "Effect/OnUserEff.img/character";
        private const string AnimationDisplayerGenericUserStateSingleEffectUol = "Effect/OnUserEff.img/character/0";
        private const string AnimationDisplayerItemMakeSuccessEffectUol = "Effect/BasicEff.img/ItemMake/Success";
        private const string AnimationDisplayerItemMakeFailureEffectUol = "Effect/BasicEff.img/ItemMake/Failure";
        private const int AnimationDisplayerSkillBookSuccessFrontStringPoolId = 0x0FF1;
        private const int AnimationDisplayerSkillBookSuccessBackStringPoolId = 0x0FF2;
        private const int AnimationDisplayerSkillBookFailureFrontStringPoolId = 0x0FF3;
        private const int AnimationDisplayerSkillBookFailureBackStringPoolId = 0x0FF4;
        private static readonly string[] AnimationDisplayerFollowEffectUolCandidates =
        {
            "Effect/OnUserEff.img/eventEffect/flame/0",
            "Effect/OnUserEff.img/eventEffect/0"
        };
        private const string AnimationDisplayerQuestDeliveryEffectBaseUol = "Effect/OnUserEff.img/itemEffect/quest";
        private const int AnimationDisplayerQuestDeliveryFallbackEffectItemId = 2430071;
        private const int AnimationDisplayerNewYearSoundStringPoolId = 0x125C;
        private static readonly string[] AnimationDisplayerNewYearWeatherAliases = { "newyear", "happyNewyear" };
        private const int AnimationDisplayerUserStateOffsetY = -70;
        private const int AnimationDisplayerFollowRadius = 18;
        private const int AnimationDisplayerFollowPointCount = 8;
        private const int AnimationDisplayerFollowThetaDegrees = 20;
        private const int AnimationDisplayerFollowUpdateIntervalMs = 100;
        private const int AnimationDisplayerFollowDurationMs = 10000;
        private const int AnimationDisplayerFollowEmissionBoxSize = 25;
        private const int AnimationDisplayerFollowAbsoluteTravelOffsetY = -20;
        private const int AnimationDisplayerFollowEmissionVerticalBias = 10;
        private const int AnimationDisplayerPacketOwnedFollowRegistrationMask = unchecked((int)0xC0000000);
        private const int AnimationDisplayerTransientLayerWidth = 800;
        private const int AnimationDisplayerTransientLayerHeight = 600;
        private const int AnimationDisplayerTransientLayerDurationMs = 30000;
        private const int AnimationDisplayerNewYearUpdateIntervalMs = 2000;
        private const int AnimationDisplayerNewYearUpdateCount = 3;
        private const int AnimationDisplayerNewYearUpdateNextMs = 100;
        private const int AnimationDisplayerFireCrackerUpdateNextMs = 200;
        private static readonly string[] AnimationDisplayerNewYearSoundImageCandidates = { "Field", "Game", "MiniGame", "UI" };
        private static readonly (int UpdateIntervalMs, int UpdateCount)[] AnimationDisplayerFireCrackerBurstSchedule =
        {
            (200, 1),
            (1000, 5),
            (500, 3),
            (300, 2),
            (200, 1)
        };

        private readonly Dictionary<string, List<IDXObject>> _animationDisplayerEffectCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<List<IDXObject>>> _animationDisplayerSkillUseEffectCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<AnimationDisplayerSkillUseAvatarEffectVariant>> _animationDisplayerSkillUseAvatarEffectCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, AnimationDisplayerFollowEquipmentDefinition> _animationDisplayerFollowEquipmentCache = new();
        private readonly Dictionary<int, int> _animationDisplayerFollowAnimationIds = new();
        private readonly List<int> _packetOwnedAnimationDisplayerAreaAnimationIds = new();
        private int _animationDisplayerLocalQuestDeliveryItemId;
        private int _packetOwnedAnimationDisplayerFollowDriverId;

        internal enum AnimationDisplayerTransientEffectKind
        {
            None = 0,
            NewYear = 1,
            FireCracker = 2
        }

        private sealed class AnimationDisplayerSkillUseAvatarEffectVariant
        {
            public SkillAnimation OverlayAnimation { get; init; }
            public SkillAnimation UnderFaceAnimation { get; init; }

            public bool HasAvatarAnimations => OverlayAnimation != null || UnderFaceAnimation != null;
        }

        private sealed class AnimationDisplayerFollowEquipmentDefinition
        {
            public int ItemId { get; init; }
            public string EffectUol { get; init; }
            public IReadOnlyList<List<IDXObject>> EffectFrameVariants { get; init; }
            public IReadOnlyList<Vector2> GenerationPoints { get; init; }
            public Rectangle EmissionArea { get; init; }
            public int UpdateIntervalMs { get; init; }
            public int SpawnDurationMs { get; init; }
            public Point SpawnOffsetMin { get; init; }
            public Point SpawnOffsetMax { get; init; }
            public bool UsesRelativeEmission { get; init; }
            public bool? SpawnRelativeToTarget { get; init; }
            public bool SpawnOnlyOnOwnerMove { get; init; }
            public bool SuppressOwnerFlip { get; init; }
            public int ThetaDegrees { get; init; }
            public float Radius { get; init; }
            public int ZOrder { get; init; }
        }

        private readonly record struct AnimationDisplayerSkillUseBranchRequest(
            string BranchName,
            Point OriginOffset,
            bool FollowOwnerFacing = true,
            bool FollowOwnerPosition = true);

        private void RegisterAnimationDisplayerChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "socialanim",
                "Exercise the shared social or event animation-displayer runtime",
                "/socialanim <status|clear|newyear|firecracker|userstate|follow|questdelivery> [...]",
                HandleAnimationDisplayerChatCommand);
        }

        private ChatCommandHandler.CommandResult HandleAnimationDisplayerChatCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeAnimationDisplayerStatus());
            }

            if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
                ClearAnimationDisplayerState();
                return ChatCommandHandler.CommandResult.Ok(DescribeAnimationDisplayerStatus());
            }

            if (string.Equals(args[0], "newyear", StringComparison.OrdinalIgnoreCase))
            {
                Rectangle area = ResolveAnimationDisplayerArea(args, startIndex: 1);
                return TryRegisterAnimationDisplayerNewYearAnimation(area, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "firecracker", StringComparison.OrdinalIgnoreCase))
            {
                Rectangle area = ResolveAnimationDisplayerArea(args, startIndex: 1);
                return TryRegisterAnimationDisplayerFireCrackerAnimation(area, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "userstate", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim userstate <local|characterId> <on|off>");
                }

                if (!TryResolveAnimationDisplayerOwner(args[1], out int ownerCharacterId, out Func<Vector2> getPosition, out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error($"Could not resolve animation owner {args[1]}.");
                }

                if (string.Equals(args[2], "on", StringComparison.OrdinalIgnoreCase))
                {
                    bool registered = TryRegisterAnimationDisplayerUserState(ownerCharacterId, getPosition);
                    return registered
                        ? ChatCommandHandler.CommandResult.Ok($"Registered animation-displayer user-state layer for {ownerName}.")
                        : ChatCommandHandler.CommandResult.Error("User-state animation frames could not be loaded from Effect/OnUserEff.img/character.");
                }

                if (string.Equals(args[2], "off", StringComparison.OrdinalIgnoreCase))
                {
                    _animationEffects.RemoveUserState(ownerCharacterId, currTickCount);
                    return ChatCommandHandler.CommandResult.Ok($"Cleared animation-displayer user-state layer for {ownerName}.");
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /socialanim userstate <local|characterId> <on|off>");
            }

            if (string.Equals(args[0], "follow", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim follow <local|characterId> <on|off> [relative|absolute]");
                }

                if (!TryResolveAnimationDisplayerOwner(args[1], out int ownerCharacterId, out Func<Vector2> getPosition, out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error($"Could not resolve animation owner {args[1]}.");
                }

                if (string.Equals(args[2], "on", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryResolveAnimationDisplayerFollowRelativeEmission(args, startIndex: 3, out bool relativeEmission))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /socialanim follow <local|characterId> <on|off> [relative|absolute]");
                    }

                    bool registered = TryRegisterAnimationDisplayerFollow(ownerCharacterId, getPosition, relativeEmission);
                    return registered
                        ? ChatCommandHandler.CommandResult.Ok($"Registered animation-displayer follow layer for {ownerName} ({(relativeEmission ? "relative" : "absolute")} emission).")
                        : ChatCommandHandler.CommandResult.Error($"Follow animation frames could not be loaded from {AnimationDisplayerGenericUserStateEffectUol} or the authored eventEffect follow families.");
                }

                if (string.Equals(args[2], "off", StringComparison.OrdinalIgnoreCase))
                {
                    ClearAnimationDisplayerFollow(ownerCharacterId);
                    return ChatCommandHandler.CommandResult.Ok($"Cleared animation-displayer follow layer for {ownerName}.");
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /socialanim follow <local|characterId> <on|off> [relative|absolute]");
            }

            if (string.Equals(args[0], "questdelivery", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim questdelivery <itemId|clear>");
                }

                if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                {
                    ClearAnimationDisplayerLocalQuestDeliveryOwner();
                    return ChatCommandHandler.CommandResult.Ok("Cleared animation-displayer quest-delivery user-state layer for the local user.");
                }

                if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId) || itemId <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim questdelivery <itemId|clear>");
                }

                bool registered = TryRegisterAnimationDisplayerQuestDeliveryLocalUserState(itemId, out int resolvedItemId);
                return registered
                    ? ChatCommandHandler.CommandResult.Ok($"Registered animation-displayer quest-delivery layer for local user using item {resolvedItemId}.")
                    : ChatCommandHandler.CommandResult.Error($"Quest-delivery animation frames could not be loaded for item {itemId}.");
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /socialanim <status|clear|newyear|firecracker|userstate|follow|questdelivery> [...]");
        }

        private string DescribeAnimationDisplayerStatus()
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            bool localUserStateActive = localCharacterId > 0 && _animationEffects.HasUserState(localCharacterId);
            string localQuestDelivery = _animationDisplayerLocalQuestDeliveryItemId > 0
                ? _animationDisplayerLocalQuestDeliveryItemId.ToString(CultureInfo.InvariantCulture)
                : "idle";
            bool packetOwnedFollowActive = localCharacterId > 0
                && _animationDisplayerFollowAnimationIds.ContainsKey(BuildAnimationDisplayerPacketOwnedFollowRegistrationKey(localCharacterId));
            return $"Animation displayer parity: userStates={_animationEffects.UserStateCount}, followAnimations={_animationEffects.FollowAnimationCount}, areaAnimations={_animationEffects.AreaAnimationCount}, localUserState={(localUserStateActive ? "active" : "idle")}, localQuestDelivery={localQuestDelivery}, packetOwnedFollow={(packetOwnedFollowActive ? "active" : "idle")}, localFade={(_packetOwnedFieldFadeOverlay.IsActive ? "active" : "idle")}.";
        }

        private void ClearAnimationDisplayerState()
        {
            _animationEffects.ClearUserStates();
            _animationEffects.ClearAreaAnimations();
            ClearAnimationDisplayerFollowAnimations();
            _packetOwnedAnimationDisplayerAreaAnimationIds.Clear();
            _animationDisplayerLocalQuestDeliveryItemId = 0;
            _packetOwnedAnimationDisplayerFollowDriverId = 0;
            ResetAnimationDisplayerLocalFadeLayer();
        }

        private void ResetAnimationDisplayerLocalFadeLayer()
        {
            _packetOwnedFieldFadeOverlay.Clear();
        }

        private bool TryRegisterAnimationDisplayerNewYearAnimation(Rectangle area, out string message)
        {
            bool registered = TryRegisterAnimationDisplayerNewYearAnimation(area);
            message = registered
                ? $"Registered New Year animation-displayer area effect in ({area.X}, {area.Y}, {area.Width}, {area.Height})."
                : $"Could not resolve {AnimationDisplayerNewYearEffectUol}.";
            return registered;
        }

        private bool TryRegisterAnimationDisplayerFireCrackerAnimation(Rectangle area, out string message)
        {
            bool registered = TryRegisterAnimationDisplayerFireCrackerAnimation(area);
            message = registered
                ? $"Registered firecracker animation-displayer area effect in ({area.X}, {area.Y}, {area.Width}, {area.Height})."
                : $"Could not resolve {AnimationDisplayerFireCrackerEffectUol}.";
            return registered;
        }

        private bool TryRegisterAnimationDisplayerNewYearAnimation(Rectangle area)
        {
            return TryRegisterAnimationDisplayerAreaAnimation(
                cacheKey: "newyear",
                effectUol: AnimationDisplayerNewYearEffectUol,
                area,
                updateIntervalMs: AnimationDisplayerNewYearUpdateIntervalMs,
                updateCount: AnimationDisplayerNewYearUpdateCount,
                updateNextMs: AnimationDisplayerNewYearUpdateNextMs,
                durationMs: AnimationDisplayerTransientLayerDurationMs,
                onSpawn: () => TryPlayAnimationDisplayerNewYearTransientSound(weatherPath: null)) >= 0;
        }

        private bool TryRegisterAnimationDisplayerFireCrackerAnimation(Rectangle area)
        {
            bool anyRegistered = false;
            string[] effectUols = BuildAnimationDisplayerFireCrackerEffectUols(weatherPath: null);
            for (int i = 0; i < AnimationDisplayerFireCrackerBurstSchedule.Length; i++)
            {
                (int updateIntervalMs, int updateCount) = AnimationDisplayerFireCrackerBurstSchedule[i];
                string effectUol = i < effectUols.Length
                    ? effectUols[i]
                    : BuildAnimationDisplayerFireCrackerEffectUol(i, weatherPath: null);
                anyRegistered |= TryRegisterAnimationDisplayerAreaAnimation(
                    cacheKey: $"firecracker:{i}",
                    effectUol,
                    area,
                    updateIntervalMs,
                    updateCount,
                    AnimationDisplayerFireCrackerUpdateNextMs,
                    AnimationDisplayerTransientLayerDurationMs) >= 0;
            }

            return anyRegistered;
        }

        private int TryRegisterAnimationDisplayerAreaAnimation(
            string cacheKey,
            string effectUol,
            Rectangle area,
            int updateIntervalMs,
            int updateCount,
            int updateNextMs,
            int durationMs,
            Action onSpawn = null)
        {
            if (!TryGetAnimationDisplayerFrames(cacheKey, effectUol, out List<IDXObject> frames))
            {
                return -1;
            }

            return _animationEffects.RegisterAreaAnimation(
                frames,
                area,
                updateIntervalMs,
                updateCount,
                updateNextMs,
                durationMs,
                currTickCount,
                onSpawn);
        }

        private bool TryRegisterAnimationDisplayerUserState(int ownerCharacterId, Func<Vector2> getPosition)
        {
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                return false;
            }

            if (!TryResolveAnimationDisplayerUserStateFrames(
                    ownerCharacterId,
                    out List<IDXObject> startFrames,
                    out List<IDXObject> repeatFrames,
                    out List<IDXObject> endFrames))
            {
                return false;
            }

            return _animationEffects.RegisterUserState(
                       ownerCharacterId,
                       startFrames,
                       repeatFrames,
                       endFrames,
                       getPosition,
                       offsetX: 0f,
                       offsetY: AnimationDisplayerUserStateOffsetY,
                       currTickCount) >= 0;
        }

        private bool TryResolveAnimationDisplayerUserStateFrames(
            int ownerCharacterId,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames)
        {
            startFrames = null;
            repeatFrames = null;
            endFrames = null;

            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (ownerCharacterId > 0
                && ownerCharacterId == localCharacterId
                && TryResolveAnimationDisplayerSpecificUserStateFrames(
                    SkillManager.BuildAnimationDisplayerTemporaryStatAvatarEffectStatesForParity(
                        _playerManager?.Skills?.ActiveBuffs,
                        _playerManager?.Skills?.GetActiveDeferredAvatarEffectSkillIdForParity()),
                    out _,
                    out startFrames,
                    out repeatFrames,
                    out endFrames))
            {
                return true;
            }

            if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor actor) == true
                && actor != null
                && TryResolveAnimationDisplayerSpecificUserStateFrames(
                    BuildAnimationDisplayerSpecificUserStateCandidates(actor),
                    out _,
                    out startFrames,
                    out repeatFrames,
                    out endFrames))
            {
                return true;
            }

            return TryGetAnimationDisplayerFrames("userstate:generic", AnimationDisplayerGenericUserStateEffectUol, out repeatFrames);
        }

        internal static IReadOnlyList<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> BuildAnimationDisplayerSpecificUserStateCandidates(
            RemoteUserActor actor)
        {
            var candidates = new List<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState>(8);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatBarrierEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatBlessingArmorEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatRepeatEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatAuraEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatMagicShieldEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatSoulArrowEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatWeaponChargeEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatFinalCutEffect);
            return candidates;
        }

        internal static bool TryResolveAnimationDisplayerSpecificUserStateFrames(
            IReadOnlyList<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> candidateStates,
            out int resolvedSkillId,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames)
        {
            resolvedSkillId = 0;
            startFrames = null;
            repeatFrames = null;
            endFrames = null;

            if (candidateStates == null || candidateStates.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < candidateStates.Count; i++)
            {
                RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState state = candidateStates[i];
                if (!TryBuildAnimationDisplayerSpecificUserStateFrames(
                        state,
                        out List<IDXObject> candidateStartFrames,
                        out List<IDXObject> candidateRepeatFrames,
                        out List<IDXObject> candidateEndFrames))
                {
                    continue;
                }

                resolvedSkillId = state.SkillId;
                startFrames = candidateStartFrames;
                repeatFrames = candidateRepeatFrames;
                endFrames = candidateEndFrames;
                return true;
            }

            return false;
        }

        private static void AddAnimationDisplayerSpecificUserStateCandidate(
            List<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> candidates,
            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState candidate)
        {
            if (candidate != null)
            {
                candidates.Add(candidate);
            }
        }

        private static bool TryBuildAnimationDisplayerSpecificUserStateFrames(
            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState state,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames)
        {
            startFrames = BuildAnimationDisplayerFramesFromSkillAnimation(
                state?.Skill?.AffectedEffect
                ?? state?.Skill?.AffectedSecondaryEffect
                ?? state?.Skill?.Effect
                ?? state?.Skill?.EffectSecondary);
            repeatFrames = BuildAnimationDisplayerFramesFromSkillAnimation(
                state?.Skill?.RepeatEffect
                ?? state?.Skill?.RepeatSecondaryEffect
                ?? state?.OverlayAnimation
                ?? state?.OverlaySecondaryAnimation
                ?? state?.UnderFaceAnimation
                ?? state?.UnderFaceSecondaryAnimation);
            endFrames = null;

            if (!Animation.AnimationEffects.HasFrames(repeatFrames)
                && Animation.AnimationEffects.HasFrames(startFrames))
            {
                repeatFrames = startFrames;
            }

            return Animation.AnimationEffects.HasFrames(startFrames)
                || Animation.AnimationEffects.HasFrames(repeatFrames)
                || Animation.AnimationEffects.HasFrames(endFrames);
        }

        internal static List<IDXObject> BuildAnimationDisplayerFramesFromSkillAnimation(SkillAnimation animation)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return null;
            }

            var frames = new List<IDXObject>(animation.Frames.Count);
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                IDXObject texture = animation.Frames[i]?.Texture;
                if (texture != null)
                {
                    frames.Add(texture);
                }
            }

            return frames.Count > 0 ? frames : null;
        }

        private bool TryRegisterAnimationDisplayerFollow(int ownerCharacterId, Func<Vector2> getPosition, bool relativeEmission = true)
        {
            return TryRegisterAnimationDisplayerFollow(ownerCharacterId, ownerCharacterId, getPosition, relativeEmission);
        }

        private bool TryRegisterAnimationDisplayerFollow(int registrationKey, int ownerCharacterId, Func<Vector2> getPosition, bool relativeEmission = true)
        {
            if (registrationKey == 0 || ownerCharacterId <= 0 || getPosition == null)
            {
                return false;
            }

            AnimationDisplayerFollowEquipmentDefinition followDefinition = ResolveAnimationDisplayerFollowEquipmentDefinition(ownerCharacterId);
            List<IDXObject> frames = null;
            IReadOnlyList<List<IDXObject>> followFrameVariants = followDefinition?.EffectFrameVariants;
            if (!string.IsNullOrWhiteSpace(followDefinition?.EffectUol))
            {
                if (!Animation.AnimationEffects.HasFrameVariants(followFrameVariants))
                {
                    TryGetAnimationDisplayerFrames(
                        $"follow:equipment:{followDefinition.ItemId}",
                        followDefinition.EffectUol,
                        out frames);
                }
            }

            if (!Animation.AnimationEffects.HasFrames(frames))
            {
                TryGetAnimationDisplayerFrames("follow:generic", AnimationDisplayerGenericUserStateEffectUol, out frames);
            }

            if (!Animation.AnimationEffects.HasFrameVariants(followFrameVariants))
            {
                followFrameVariants = LoadAnimationDisplayerFollowFrameVariants();
            }

            if (!Animation.AnimationEffects.HasFrames(frames)
                && !Animation.AnimationEffects.HasFrameVariants(followFrameVariants))
            {
                return false;
            }

            IReadOnlyList<Vector2> generationPoints = followDefinition?.GenerationPoints;
            float resolvedRadius = followDefinition?.Radius > 0f
                ? followDefinition.Radius
                : AnimationDisplayerFollowRadius;
            Point spawnOffsetMin = BuildAnimationDisplayerFollowSpawnOffsetMin(relativeEmission, followDefinition);
            Point spawnOffsetMax = BuildAnimationDisplayerFollowSpawnOffsetMax(relativeEmission, followDefinition);
            Func<bool> getOwnerFlip = ResolveAnimationDisplayerFollowOwnerFlip(ownerCharacterId);
            bool spawnOnlyOnOwnerMove = followDefinition?.SpawnOnlyOnOwnerMove ?? false;

            ClearAnimationDisplayerFollowRegistration(registrationKey);
            int followId = _animationEffects.AddFollow(
                frames,
                getPosition,
                offsetX: 0f,
                offsetY: AnimationDisplayerUserStateOffsetY,
                durationMs: AnimationDisplayerFollowDurationMs,
                currentTimeMs: currTickCount,
                options: new Animation.AnimationEffects.FollowAnimationOptions
                {
                    GenerationPoints = generationPoints ?? BuildAnimationDisplayerFollowGenerationPoints(
                        AnimationDisplayerFollowRadius,
                        AnimationDisplayerFollowPointCount),
                    ThetaDegrees = followDefinition?.ThetaDegrees ?? AnimationDisplayerFollowThetaDegrees,
                    Radius = resolvedRadius,
                    RandomizeStartupAngle = true,
                    GetTargetFlip = getOwnerFlip,
                    UpdateIntervalMs = followDefinition?.UpdateIntervalMs > 0
                        ? followDefinition.UpdateIntervalMs
                        : AnimationDisplayerFollowUpdateIntervalMs,
                    SpawnFrameVariants = followFrameVariants,
                    SpawnRelativeToTarget = ResolveAnimationDisplayerFollowSpawnRelativeToTarget(
                        relativeEmission,
                        followDefinition?.SpawnRelativeToTarget),
                    SuppressTargetFlip = followDefinition?.SuppressOwnerFlip ?? false,
                    SpawnOnlyOnTargetMove = spawnOnlyOnOwnerMove,
                    IsTargetMoveAction = spawnOnlyOnOwnerMove
                        ? ResolveAnimationDisplayerFollowOwnerMoveAction(ownerCharacterId)
                        : null,
                    SpawnArea = followDefinition?.EmissionArea ?? BuildAnimationDisplayerFollowEmissionArea(),
                    SpawnUsesEmissionBox = !relativeEmission,
                    SpawnAppliesEmissionBias = relativeEmission && (followDefinition?.UsesRelativeEmission ?? true),
                    SpawnVerticalEmissionBias = AnimationDisplayerFollowEmissionVerticalBias,
                    SpawnDurationMs = followDefinition?.SpawnDurationMs ?? 0,
                    SpawnOffsetMin = spawnOffsetMin,
                    SpawnOffsetMax = spawnOffsetMax,
                    SpawnZOrder = followDefinition?.ZOrder ?? 0
                });
            if (followId < 0)
            {
                return false;
            }

            _animationDisplayerFollowAnimationIds[registrationKey] = followId;
            return true;
        }

        private bool TryRegisterAnimationDisplayerQuestDeliveryLocalUserState(int itemId)
        {
            return TryRegisterAnimationDisplayerQuestDeliveryLocalUserState(itemId, out _);
        }

        private bool TryRegisterAnimationDisplayerQuestDeliveryLocalUserState(int itemId, out int resolvedItemId)
        {
            resolvedItemId = 0;
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Build?.Id <= 0)
            {
                return false;
            }

            bool registered = TryRegisterAnimationDisplayerQuestDeliveryUserState(
                player.Build.Id,
                itemId,
                () => _playerManager?.Player?.Position ?? player.Position,
                out resolvedItemId);
            if (registered)
            {
                _animationDisplayerLocalQuestDeliveryItemId = resolvedItemId;
            }

            return registered;
        }

        private bool TryRegisterAnimationDisplayerQuestDeliveryUserState(
            int ownerCharacterId,
            int itemId,
            Func<Vector2> getPosition,
            out int resolvedItemId)
        {
            resolvedItemId = 0;
            if (ownerCharacterId <= 0 || getPosition == null || itemId <= 0)
            {
                return false;
            }

            if (!TryLoadAnimationDisplayerQuestDeliveryFrames(
                    itemId,
                    out List<IDXObject> startFrames,
                    out List<IDXObject> repeatFrames,
                    out List<IDXObject> endFrames,
                    out resolvedItemId))
            {
                return false;
            }

            int registrationKey = BuildAnimationDisplayerQuestDeliveryRegistrationKey(ownerCharacterId);
            return _animationEffects.RegisterUserState(
                       registrationKey,
                       ownerCharacterId,
                       startFrames,
                       repeatFrames,
                       endFrames,
                       getPosition,
                       offsetX: 0f,
                       offsetY: AnimationDisplayerUserStateOffsetY,
                       currTickCount) >= 0;
        }

        private bool TryLoadAnimationDisplayerQuestDeliveryFrames(
            int itemId,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames,
            out int resolvedItemId)
        {
            startFrames = null;
            repeatFrames = null;
            endFrames = null;
            resolvedItemId = 0;

            foreach (int candidateItemId in EnumerateAnimationDisplayerQuestDeliveryEffectItemIds(itemId))
            {
                if (candidateItemId <= 0)
                {
                    continue;
                }

                if (TryResolveAnimationDisplayerQuestDeliveryEffectUol(candidateItemId, out string effectUol, out _)
                    && TryLoadAnimationDisplayerQuestDeliveryPhaseFrames(
                        ResolveAnimationDisplayerProperty(effectUol),
                        out startFrames,
                        out repeatFrames,
                        out endFrames))
                {
                    resolvedItemId = candidateItemId;
                    return true;
                }
            }

            foreach (int candidateItemId in EnumerateAnimationDisplayerQuestDeliveryEffectItemIds(itemId))
            {
                if (candidateItemId <= 0)
                {
                    continue;
                }

                if (TryLoadAnimationDisplayerQuestDeliveryClientPhaseFrames(
                        candidateItemId,
                        out startFrames,
                        out repeatFrames,
                        out endFrames))
                {
                    resolvedItemId = candidateItemId;
                    return true;
                }
            }

            return false;
        }

        private bool TryLoadAnimationDisplayerQuestDeliveryClientPhaseFrames(
            int itemId,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames)
        {
            startFrames = LoadAnimationDisplayerFrames(AnimationDisplayerQuestDeliveryStringPoolText.ResolveArrivePath(itemId));
            repeatFrames = LoadAnimationDisplayerFrames(AnimationDisplayerQuestDeliveryStringPoolText.ResolveWaitPath(itemId));
            endFrames = LoadAnimationDisplayerFrames(AnimationDisplayerQuestDeliveryStringPoolText.ResolveLeavePath(itemId));
            return Animation.AnimationEffects.HasFrames(startFrames)
                || Animation.AnimationEffects.HasFrames(repeatFrames)
                || Animation.AnimationEffects.HasFrames(endFrames);
        }

        private bool TryResolveAnimationDisplayerQuestDeliveryEffectUol(int itemId, out string effectUol, out int resolvedItemId)
        {
            effectUol = null;
            resolvedItemId = 0;

            string[] candidateUols = EnumerateAnimationDisplayerQuestDeliveryEffectUols(itemId);
            for (int i = 0; i < candidateUols.Length; i++)
            {
                string candidate = candidateUols[i];
                if (string.IsNullOrWhiteSpace(candidate) || ResolveAnimationDisplayerProperty(candidate) == null)
                {
                    continue;
                }

                effectUol = candidate;
                string[] segments = candidate.Split('/');
                if (segments.Length > 0)
                {
                    int.TryParse(segments[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out resolvedItemId);
                }

                return true;
            }

            return false;
        }

        private bool TryRegisterCollisionVerticalJumpEffect(int currentTime)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Physics == null
                || !TryGetAnimationDisplayerFrames(
                    "portalcollision:verticaljump",
                    CollisionVerticalJumpEffectUol,
                    out List<IDXObject> frames))
            {
                return false;
            }

            Vector2 fallbackPosition = player.Physics.GetPosition();
            _animationEffects.AddOneTimeAttached(
                frames,
                () => _playerManager?.Player?.Physics?.GetPosition() ?? fallbackPosition,
                () => _playerManager?.Player?.FacingRight ?? player.FacingRight,
                fallbackPosition.X,
                fallbackPosition.Y,
                player.FacingRight,
                currentTime);
            return true;
        }

        private void HandleRemoteGenericUserStateEffect(RemoteUserActorPool.RemoteGenericUserStatePresentation presentation)
        {
            if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor actor) != true
                || actor == null
                || !actor.IsVisibleInWorld
                || !TryGetAnimationDisplayerFrames(
                    "userstate:generic:single",
                    AnimationDisplayerGenericUserStateSingleEffectUol,
                    out List<IDXObject> frames))
            {
                return;
            }

            Vector2 fallbackPosition = actor.Position;
            bool fallbackFacingRight = actor.FacingRight;
            _animationEffects.AddOneTimeAttached(
                frames,
                () =>
                {
                    if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor liveActor) == true
                        && liveActor != null)
                    {
                        return liveActor.Position;
                    }

                    return fallbackPosition;
                },
                () =>
                {
                    if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor liveActor) == true
                        && liveActor != null)
                    {
                        return liveActor.FacingRight;
                    }

                    return fallbackFacingRight;
                },
                fallbackPosition.X,
                fallbackPosition.Y,
                fallbackFacingRight,
                presentation.CurrentTime);
        }

        private void HandleRemoteItemMakeEffect(RemoteUserActorPool.RemoteItemMakePresentation presentation)
        {
            if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor actor) != true
                || actor == null
                || !actor.IsVisibleInWorld)
            {
                return;
            }

            string effectUol = presentation.Success
                ? AnimationDisplayerItemMakeSuccessEffectUol
                : AnimationDisplayerItemMakeFailureEffectUol;
            string cacheKey = presentation.Success
                ? "itemmake:success"
                : "itemmake:failure";
            if (!TryGetAnimationDisplayerFrames(cacheKey, effectUol, out List<IDXObject> frames))
            {
                return;
            }

            Vector2 fallbackPosition = actor.Position;
            bool fallbackFacingRight = actor.FacingRight;
            _animationEffects.AddOneTimeAttached(
                frames,
                () =>
                {
                    if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor liveActor) == true
                        && liveActor != null)
                    {
                        return liveActor.Position;
                    }

                    return fallbackPosition;
                },
                () =>
                {
                    if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor liveActor) == true
                        && liveActor != null)
                    {
                        return liveActor.FacingRight;
                    }

                    return fallbackFacingRight;
                },
                fallbackPosition.X,
                fallbackPosition.Y,
                fallbackFacingRight,
                presentation.CurrentTime);
        }

        private void HandleRemoteStringEffect(RemoteUserActorPool.RemoteStringEffectPresentation presentation)
        {
            if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor actor) != true
                || actor == null
                || !actor.IsVisibleInWorld)
            {
                return;
            }

            string effectUol = NormalizeRemotePacketOwnedStringEffectUol(presentation.EffectPath);
            if (string.IsNullOrWhiteSpace(effectUol)
                || !TryLoadRemotePacketOwnedStringEffectFrames(effectUol, out List<IDXObject> frames))
            {
                return;
            }

            Vector2 fallbackPosition = actor.Position;
            bool fallbackFacingRight = presentation.UseOwnerFacing && actor.FacingRight;
            _animationEffects.AddOneTimeAttached(
                frames,
                () =>
                {
                    if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor liveActor) == true
                        && liveActor != null)
                    {
                        return liveActor.Position;
                    }

                    return fallbackPosition;
                },
                () =>
                {
                    if (!presentation.UseOwnerFacing)
                    {
                        return false;
                    }

                    if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor liveActor) == true
                        && liveActor != null)
                    {
                        return liveActor.FacingRight;
                    }

                    return fallbackFacingRight;
                },
                fallbackPosition.X,
                fallbackPosition.Y,
                fallbackFacingRight,
                presentation.CurrentTime);
        }

        private void HandleRemoteMobAttackHitEffect(RemoteUserActorPool.RemoteMobAttackHitPresentation presentation)
        {
            if (_animationEffects == null
                || string.IsNullOrWhiteSpace(presentation.EffectPath)
                || !TryLoadRemotePacketOwnedStringEffectFrames(presentation.EffectPath, out List<IDXObject> frames))
            {
                return;
            }

            _animationEffects.AddOneTime(
                frames,
                presentation.Position.X,
                presentation.Position.Y,
                flip: !presentation.FacingRight,
                currentTimeMs: presentation.CurrentTime);
        }

        private void SyncAnimationDisplayerRemoteUserState(int characterId)
        {
            if (characterId <= 0)
            {
                return;
            }

            if (_remoteUserPool?.TryGetActor(characterId, out RemoteUserActor actor) != true
                || actor == null
                || !actor.IsVisibleInWorld
                || !actor.TemporaryStats.HasPayload)
            {
                _animationEffects.RemoveUserState(characterId, currTickCount);
                return;
            }

            TryRegisterAnimationDisplayerUserState(characterId, () =>
            {
                if (_remoteUserPool?.TryGetActor(characterId, out RemoteUserActor liveActor) == true && liveActor != null)
                {
                    return liveActor.Position;
                }

                return actor.Position;
            });
        }

        private void SyncAnimationDisplayerRemoteQuestDeliveryOwner(int characterId)
        {
            if (characterId <= 0)
            {
                return;
            }

            if (_remoteUserPool?.TryGetActor(characterId, out RemoteUserActor actor) != true
                || actor == null
                || !actor.IsVisibleInWorld
                || actor.PacketOwnedQuestDeliveryEffectItemId is not > 0)
            {
                ClearAnimationDisplayerRemoteQuestDeliveryOwner(characterId);
                return;
            }

            TryRegisterAnimationDisplayerQuestDeliveryUserState(
                characterId,
                actor.PacketOwnedQuestDeliveryEffectItemId.Value,
                () =>
                {
                    if (_remoteUserPool?.TryGetActor(characterId, out RemoteUserActor liveActor) == true && liveActor != null)
                    {
                        return liveActor.Position;
                    }

                    return actor.Position;
                },
                out _);
        }

        private void ClearAnimationDisplayerRemoteQuestDeliveryOwner(int characterId)
        {
            if (characterId <= 0)
            {
                return;
            }

            _animationEffects.RemoveUserStateByRegistrationKey(
                BuildAnimationDisplayerQuestDeliveryRegistrationKey(characterId),
                currTickCount);
        }

        private void ClearAnimationDisplayerRemotePresentationOwners(int characterId)
        {
            if (characterId <= 0)
            {
                return;
            }

            _animationEffects.RemoveUserState(characterId, currTickCount);
            ClearAnimationDisplayerRemoteQuestDeliveryOwner(characterId);
        }

        private bool TryGetAnimationDisplayerFrames(string cacheKey, string effectUol, out List<IDXObject> frames)
        {
            if (_animationDisplayerEffectCache.TryGetValue(cacheKey, out frames) && Animation.AnimationEffects.HasFrames(frames))
            {
                return true;
            }

            frames = LoadAnimationDisplayerFrames(effectUol);
            if (!Animation.AnimationEffects.HasFrames(frames))
            {
                frames = null;
                return false;
            }

            _animationDisplayerEffectCache[cacheKey] = frames;
            return true;
        }

        private bool TryRegisterAnimationDisplayerSkillUse(SkillCastInfo castInfo)
        {
            if (castInfo == null
                || _animationEffects == null
                || castInfo.SuppressEffectAnimation)
            {
                return false;
            }

            int skillId = castInfo.SkillId;
            if (skillId <= 0)
            {
                return false;
            }

            int delayRate = ResolveAnimationDisplayerSkillUseDelayRate(castInfo);
            TryResolveAnimationDisplayerSkillUseOwner(
                castInfo.CasterId,
                out Func<Vector2> getOwnerPosition,
                out Func<bool> getOwnerFacingRight);
            bool handledPrimary = castInfo.EffectAnimation == null;
            bool handledSecondary = castInfo.SecondaryEffectAnimation == null;
            bool registeredAny = false;

            foreach (AnimationDisplayerSkillUseBranchRequest branchRequest in EnumerateAnimationDisplayerSkillUseBranchRequests(castInfo))
            {
                if (!TryRegisterAnimationDisplayerSkillUseBranch(
                        castInfo.CasterId,
                        skillId,
                        branchRequest,
                        castInfo.CasterX,
                        castInfo.CasterY,
                        castInfo.FacingRight,
                        castInfo.CastTime,
                        delayRate,
                        getOwnerPosition,
                        getOwnerFacingRight))
                {
                    continue;
                }

                registeredAny = true;

                if (!handledPrimary
                    && string.Equals(castInfo.EffectAnimation?.Name, branchRequest.BranchName, StringComparison.OrdinalIgnoreCase))
                {
                    handledPrimary = true;
                }

                if (!handledSecondary
                    && string.Equals(castInfo.SecondaryEffectAnimation?.Name, branchRequest.BranchName, StringComparison.OrdinalIgnoreCase))
                {
                    handledSecondary = true;
                }
            }

            if (handledPrimary && handledSecondary)
            {
                castInfo.SuppressEffectAnimation = true;
            }

            return registeredAny;
        }

        private void HandleAnimationDisplayerBuffApplied(ActiveBuff buff)
        {
            if (buff?.SkillData == null)
            {
                return;
            }

            SkillCastInfo castInfo = BuildAnimationDisplayerLocalSkillUseRequest(
                buff.SkillData.SkillId,
                buff.SkillData,
                buff.StartTime,
                buff.SkillData.AffectedEffect,
                buff.SkillData.AffectedSecondaryEffect,
                requestedBranchNames: BuildAnimationDisplayerBuffAppliedRequestedBranchNames(buff.SkillData));
            TryRegisterAnimationDisplayerSkillUse(castInfo);
        }

        private void HandleAnimationDisplayerClientSkillEffectRequested(SkillUseEffectRequest request)
        {
            TryRegisterAnimationDisplayerLocalSkillUseRequest(request);
        }

        private void HandleAnimationDisplayerPreparedSkillStarted(PreparedSkill prepared)
        {
            if (prepared?.SkillData == null)
            {
                return;
            }

            SkillAnimation effectAnimation = prepared.IsHolding
                ? prepared.SkillData.RepeatEffect ?? prepared.SkillData.KeydownEffect
                : prepared.SkillData.PrepareEffect ?? prepared.SkillData.Effect;
            SkillAnimation secondaryEffectAnimation = prepared.IsHolding
                ? prepared.SkillData.RepeatSecondaryEffect ?? prepared.SkillData.KeydownSecondaryEffect
                : prepared.SkillData.PrepareSecondaryEffect;
            SkillCastInfo castInfo = BuildAnimationDisplayerLocalSkillUseRequest(
                prepared.SkillId,
                prepared.SkillData,
                prepared.StartTime,
                effectAnimation,
                secondaryEffectAnimation);
            TryRegisterAnimationDisplayerSkillUse(castInfo);
        }

        private void HandleAnimationDisplayerPreparedSkillReleased(PreparedSkill prepared)
        {
            if (prepared?.SkillData == null)
            {
                return;
            }

            SkillCastInfo castInfo = BuildAnimationDisplayerLocalSkillUseRequest(
                prepared.SkillId,
                prepared.SkillData,
                currTickCount,
                prepared.SkillData.KeydownEndEffect,
                prepared.SkillData.KeydownEndSecondaryEffect);
            TryRegisterAnimationDisplayerSkillUse(castInfo);
        }

        private bool TryRegisterAnimationDisplayerLocalSkillUseRequest(SkillUseEffectRequest request)
        {
            if (request?.EffectSkillId <= 0 || _animationEffects == null)
            {
                return false;
            }

            int effectSkillId = request.EffectSkillId;
            int sourceSkillId = request.SourceSkillId;
            SkillData effectSkill = _playerManager?.Skills?.GetSkillData(effectSkillId)
                                   ?? _playerManager?.Skills?.GetSkillData(sourceSkillId);
            PlayerCharacter localPlayer = _playerManager?.Player;
            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            Vector2 casterPosition = request.WorldOrigin ?? new Vector2(localPlayer?.X ?? 0f, localPlayer?.Y ?? 0f);
            bool facingRight = localPlayer?.FacingRight ?? true;
            SkillCastInfo castInfo = BuildAnimationDisplayerLocalSkillUseRequest(
                effectSkillId,
                effectSkill,
                request.RequestTime > 0 ? request.RequestTime : currTickCount,
                effectSkill?.Effect,
                effectSkill?.EffectSecondary,
                casterPosition,
                request.BranchNames,
                request.OriginOffset,
                request.FollowOwnerPosition,
                request.FollowOwnerFacing);
            castInfo.CasterId = localCharacterId;
            castInfo.FacingRight = facingRight;
            castInfo.DelayRateOverride = request.DelayRateOverride;
            return TryRegisterAnimationDisplayerSkillUse(castInfo);
        }

        private SkillCastInfo BuildAnimationDisplayerLocalSkillUseRequest(
            int skillId,
            SkillData skillData,
            int currentTime,
            SkillAnimation effectAnimation,
            SkillAnimation secondaryEffectAnimation,
            Vector2? casterPositionOverride = null,
            IReadOnlyList<string> requestedBranchNames = null,
            Point originOffset = default,
            bool followOwnerPosition = true,
            bool followOwnerFacing = true)
        {
            PlayerCharacter localPlayer = _playerManager?.Player;
            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            Vector2 casterPosition = casterPositionOverride ?? new Vector2(localPlayer?.X ?? 0f, localPlayer?.Y ?? 0f);
            bool facingRight = localPlayer?.FacingRight ?? true;

            return new SkillCastInfo
            {
                SkillId = skillId,
                SkillData = skillData,
                CastTime = currentTime,
                CasterId = localCharacterId,
                CasterX = casterPosition.X,
                CasterY = casterPosition.Y,
                FacingRight = facingRight,
                EffectAnimation = effectAnimation,
                SecondaryEffectAnimation = secondaryEffectAnimation,
                RequestedBranchNames = ResolveAnimationDisplayerRequestedBranchNames(
                    requestedBranchNames,
                    effectAnimation,
                    secondaryEffectAnimation),
                OriginOffset = originOffset,
                FollowOwnerPosition = followOwnerPosition,
                FollowOwnerFacing = followOwnerFacing
            };
        }

        private void HandleRemoteAnimationDisplayerSkillUse(RemoteUserActorPool.RemoteSkillUsePresentation presentation)
        {
            if (presentation.SkillId <= 0 || _animationEffects == null)
            {
                return;
            }

            TryResolveAnimationDisplayerSkillUseOwner(
                presentation.CharacterId,
                out Func<Vector2> getOwnerPosition,
                out Func<bool> getOwnerFacingRight);
            Vector2 fallbackPosition = presentation.WorldOrigin
                ?? getOwnerPosition?.Invoke()
                ?? Vector2.Zero;
            bool fallbackFacingRight = getOwnerFacingRight?.Invoke() ?? presentation.FacingRight;
            TryRememberRemoteTownPortalSkillCastObservation(presentation.CharacterId, presentation.SkillId, fallbackPosition);
            int delayRate = presentation.DelayRateOverride is > 0
                ? presentation.DelayRateOverride.Value
                : ResolveAnimationDisplayerSkillUseDelayRate(presentation.ActionSpeed);

            foreach (AnimationDisplayerSkillUseBranchRequest branchRequest in EnumerateAnimationDisplayerSkillUseBranchRequests(presentation))
            {
                TryRegisterAnimationDisplayerSkillUseBranch(
                    presentation.CharacterId,
                    presentation.SkillId,
                    branchRequest,
                    fallbackPosition.X,
                    fallbackPosition.Y,
                    fallbackFacingRight,
                    presentation.CurrentTime,
                    delayRate,
                    getOwnerPosition,
                    getOwnerFacingRight);
            }
        }

        private void TryRememberRemoteTownPortalSkillCastObservation(int characterId, int skillId, Vector2 position)
        {
            if (characterId <= 0 || !IsRemoteTownPortalSkillCastSkillId(skillId))
            {
                return;
            }

            RememberRemoteTownPortalOwnerFieldObservation(
                (uint)characterId,
                position,
                Fields.TemporaryPortalField.RemoteTownPortalObservationSource.SkillCast);
        }

        private static bool IsRemoteTownPortalSkillCastSkillId(int skillId)
        {
            return skillId == 2311002 || skillId == 8001;
        }

        private bool TryRegisterAnimationDisplayerSkillUseBranch(
            int ownerCharacterId,
            int skillId,
            AnimationDisplayerSkillUseBranchRequest branchRequest,
            float casterX,
            float casterY,
            bool facingRight,
            int currentTime,
            int delayRate,
            Func<Vector2> getOwnerPosition = null,
            Func<bool> getOwnerFacingRight = null)
        {
            if (skillId <= 0 || string.IsNullOrWhiteSpace(branchRequest.BranchName))
            {
                return false;
            }

            string branchName = branchRequest.BranchName;
            bool hasOriginOffset = branchRequest.OriginOffset != Point.Zero;
            float branchX = casterX + branchRequest.OriginOffset.X;
            float branchY = casterY + branchRequest.OriginOffset.Y;
            Func<Vector2> branchOwnerPosition = branchRequest.FollowOwnerPosition
                ? getOwnerPosition
                : null;
            if (hasOriginOffset && branchOwnerPosition != null)
            {
                Func<Vector2> ownerPositionProvider = branchOwnerPosition;
                branchOwnerPosition = () =>
                {
                    Vector2 ownerPosition = ownerPositionProvider();
                    ownerPosition.X += branchRequest.OriginOffset.X;
                    ownerPosition.Y += branchRequest.OriginOffset.Y;
                    return ownerPosition;
                };
            }

            Func<bool> branchOwnerFacingRight = branchRequest.FollowOwnerFacing
                ? getOwnerFacingRight
                : null;
            string effectUol = BuildAnimationDisplayerSkillUseBranchUol(skillId, branchName);
            if (TryGetAnimationDisplayerSkillUseAvatarEffectVariants(effectUol, out List<AnimationDisplayerSkillUseAvatarEffectVariant> avatarVariants))
            {
                bool registeredAvatarEffects = false;
                for (int i = 0; i < avatarVariants.Count; i++)
                {
                    if (TryRegisterAnimationDisplayerSkillUseAvatarEffectVariant(
                            ownerCharacterId,
                            skillId,
                            branchName,
                            i,
                            avatarVariants[i],
                            branchRequest,
                            delayRate,
                            currentTime))
                    {
                        registeredAvatarEffects = true;
                    }
                }

                if (registeredAvatarEffects)
                {
                    return true;
                }
            }

            if (!TryGetAnimationDisplayerSkillUseFrameVariants(effectUol, out List<List<IDXObject>> variants))
            {
                return false;
            }

            bool registered = false;
            for (int i = 0; i < variants.Count; i++)
            {
                List<IDXObject> adjustedFrames = ApplyAnimationDisplayerDelayRate(variants[i], delayRate);
                if (!Animation.AnimationEffects.HasFrames(adjustedFrames))
                {
                    continue;
                }

                if (branchOwnerPosition != null || branchOwnerFacingRight != null)
                {
                    _animationEffects.AddOneTimeAttached(
                        adjustedFrames,
                        branchOwnerPosition,
                        branchOwnerFacingRight,
                        branchX,
                        branchY,
                        facingRight,
                        currentTime);
                }
                else
                {
                    _animationEffects.AddOneTime(adjustedFrames, branchX, branchY, facingRight, currentTime);
                }

                registered = true;
            }

            return registered;
        }

        private bool TryRegisterAnimationDisplayerSkillUseAvatarEffectVariant(
            int ownerCharacterId,
            int skillId,
            string branchName,
            int variantIndex,
            AnimationDisplayerSkillUseAvatarEffectVariant variant,
            AnimationDisplayerSkillUseBranchRequest branchRequest,
            int delayRate,
            int currentTime)
        {
            if (variant?.HasAvatarAnimations != true)
            {
                return false;
            }

            SkillAnimation overlayAnimation = ApplyAnimationDisplayerOriginOffset(
                ApplyAnimationDisplayerDelayRate(variant.OverlayAnimation, delayRate),
                branchRequest.OriginOffset);
            SkillAnimation underFaceAnimation = ApplyAnimationDisplayerOriginOffset(
                ApplyAnimationDisplayerDelayRate(variant.UnderFaceAnimation, delayRate),
                branchRequest.OriginOffset);
            if (overlayAnimation == null && underFaceAnimation == null)
            {
                return false;
            }

            int registrationKey = BuildAnimationDisplayerSkillUseAvatarEffectRegistrationKey(
                skillId,
                branchName,
                variantIndex);
            PlayerCharacter localPlayer = _playerManager?.Player;
            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            if (localPlayer != null
                && (ownerCharacterId <= 0 || ownerCharacterId == localCharacterId))
            {
                return localPlayer.ApplyTransientSkillAvatarEffect(
                    registrationKey,
                    overlayAnimation,
                    underFaceAnimation,
                    currentTime);
            }

            return _remoteUserPool?.TryApplyTransientSkillUseAvatarEffect(
                ownerCharacterId,
                registrationKey,
                overlayAnimation,
                underFaceAnimation,
                currentTime,
                out _) == true;
        }

        private bool TryRegisterPacketOwnedRemoteSkillBookResultAvatarEffect(
            int ownerCharacterId,
            bool success,
            int currentTime,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0)
            {
                message = "Remote skill-book result owner is missing.";
                return false;
            }

            SkillAnimation frontAnimation = LoadAnimationDisplayerStringPoolAnimation(
                success ? AnimationDisplayerSkillBookSuccessFrontStringPoolId : AnimationDisplayerSkillBookFailureFrontStringPoolId);
            SkillAnimation backAnimation = LoadAnimationDisplayerStringPoolAnimation(
                success ? AnimationDisplayerSkillBookSuccessBackStringPoolId : AnimationDisplayerSkillBookFailureBackStringPoolId);
            if (frontAnimation == null && backAnimation == null)
            {
                message = "Remote skill-book result effect frames are unavailable.";
                return false;
            }

            int registrationKey = BuildAnimationDisplayerSkillUseAvatarEffectRegistrationKey(
                success ? AnimationDisplayerSkillBookSuccessFrontStringPoolId : AnimationDisplayerSkillBookFailureFrontStringPoolId,
                "Effect_SkillBookUsed",
                0);

            // Client evidence: `CWvsContext::OnSkillLearnItemResult` passes `CAvatar::GetLayerUnderFace`
            // as the overlay/anchor owner and `Effect_SkillBookUsed` loads the front UOL before the back UOL.
            // Simulator parity keeps that same owner/effect seam even if the remote actor is materialized later,
            // so queue through the existing transient-avatar-effect path instead of dropping to chat-only feedback.
            return _remoteUserPool?.TryQueueTransientSkillUseAvatarEffect(
                ownerCharacterId,
                registrationKey,
                frontAnimation,
                backAnimation,
                currentTime,
                out message) == true;
        }

        private SkillAnimation LoadAnimationDisplayerStringPoolAnimation(int stringPoolId)
        {
            if (!MapleStoryStringPool.TryGet(stringPoolId, out string effectUol)
                || string.IsNullOrWhiteSpace(effectUol))
            {
                return null;
            }

            WzImageProperty property = ResolveAnimationDisplayerProperty(effectUol)?.GetLinkedWzImageProperty();
            return LoadAnimationDisplayerSkillUseAnimation(property, effectUol);
        }

        private bool TryGetAnimationDisplayerSkillUseAvatarEffectVariants(
            string effectUol,
            out List<AnimationDisplayerSkillUseAvatarEffectVariant> variants)
        {
            if (_animationDisplayerSkillUseAvatarEffectCache.TryGetValue(effectUol, out variants)
                && variants?.Count > 0)
            {
                return true;
            }

            variants = LoadAnimationDisplayerSkillUseAvatarEffectVariants(effectUol);
            if (variants == null || variants.Count == 0)
            {
                variants = null;
                return false;
            }

            _animationDisplayerSkillUseAvatarEffectCache[effectUol] = variants;
            return true;
        }

        private List<AnimationDisplayerSkillUseAvatarEffectVariant> LoadAnimationDisplayerSkillUseAvatarEffectVariants(string effectUol)
        {
            WzImageProperty property = ResolveAnimationDisplayerProperty(effectUol)?.GetLinkedWzImageProperty();
            if (property == null)
            {
                return null;
            }

            var variants = new List<AnimationDisplayerSkillUseAvatarEffectVariant>();
            string defaultChildUol = $"{effectUol}/default";
            string indexedChildUol = $"{effectUol}/0";
            bool hasCompositeChildren = ResolveAnimationDisplayerProperty(defaultChildUol) != null
                || ResolveAnimationDisplayerProperty(indexedChildUol) != null;

            if (hasCompositeChildren)
            {
                TryAddAnimationDisplayerSkillUseAvatarEffectVariant(variants, defaultChildUol);
                for (int i = 0; ; i++)
                {
                    string candidateUol = $"{effectUol}/{i.ToString(CultureInfo.InvariantCulture)}";
                    if (ResolveAnimationDisplayerProperty(candidateUol) == null)
                    {
                        break;
                    }

                    TryAddAnimationDisplayerSkillUseAvatarEffectVariant(variants, candidateUol);
                }
            }
            else
            {
                TryAddAnimationDisplayerSkillUseAvatarEffectVariant(variants, effectUol);
            }

            return variants.Count > 0 ? variants : null;
        }

        private void TryAddAnimationDisplayerSkillUseAvatarEffectVariant(
            List<AnimationDisplayerSkillUseAvatarEffectVariant> variants,
            string effectUol)
        {
            if (TryLoadAnimationDisplayerSkillUseAvatarEffectVariant(effectUol, out AnimationDisplayerSkillUseAvatarEffectVariant variant))
            {
                variants.Add(variant);
            }
        }

        private bool TryLoadAnimationDisplayerSkillUseAvatarEffectVariant(
            string effectUol,
            out AnimationDisplayerSkillUseAvatarEffectVariant variant)
        {
            variant = null;
            WzImageProperty property = ResolveAnimationDisplayerProperty(effectUol)?.GetLinkedWzImageProperty();
            if (property == null || property is WzCanvasProperty)
            {
                return false;
            }

            SkillAnimation animation = LoadAnimationDisplayerSkillUseAnimation(property, effectUol);
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return false;
            }

            variant = ClientOwnedAvatarEffectParity.PrefersUnderFaceAvatarEffectPlane(animation)
                ? new AnimationDisplayerSkillUseAvatarEffectVariant { UnderFaceAnimation = animation }
                : new AnimationDisplayerSkillUseAvatarEffectVariant { OverlayAnimation = animation };
            return true;
        }

        private SkillAnimation LoadAnimationDisplayerSkillUseAnimation(WzImageProperty sourceProperty, string animationName)
        {
            sourceProperty = sourceProperty?.GetLinkedWzImageProperty() ?? sourceProperty;
            if (sourceProperty == null || sourceProperty is WzCanvasProperty || GraphicsDevice == null)
            {
                return null;
            }

            int sharedDelay = sourceProperty["delay"]?.GetInt() ?? 90;
            int zOrder = sourceProperty["z"]?.GetInt() ?? 0;
            int? positionCode = TryResolveAnimationDisplayerSkillUsePositionCode(sourceProperty["pos"]);
            var frames = new List<SkillFrame>();

            for (int i = 0; ; i++)
            {
                if (sourceProperty[i.ToString(CultureInfo.InvariantCulture)] is not WzCanvasProperty frameCanvas)
                {
                    break;
                }

                System.Drawing.Bitmap frameBitmap = frameCanvas.GetLinkedWzCanvasBitmap();
                if (frameBitmap == null)
                {
                    continue;
                }

                int delay = frameCanvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt()
                    ?? frameCanvas["delay"]?.GetInt()
                    ?? sharedDelay;
                System.Drawing.PointF originPoint = frameCanvas.GetCanvasOriginPosition();
                var origin = new System.Drawing.Point((int)originPoint.X, (int)originPoint.Y);
                using (frameBitmap)
                {
                    Texture2D texture = frameBitmap.ToTexture2D(GraphicsDevice);
                    frames.Add(new SkillFrame
                    {
                        Texture = new DXObject(-origin.X, -origin.Y, texture, delay),
                        Origin = new Point(origin.X, origin.Y),
                        Delay = delay,
                        Bounds = new Rectangle(-origin.X, -origin.Y, texture.Width, texture.Height),
                        Flip = false,
                        Z = zOrder
                    });
                }
            }

            if (frames.Count == 0)
            {
                return null;
            }

            SkillAnimation animation = new()
            {
                Name = animationName,
                Frames = frames,
                Loop = false,
                Origin = Point.Zero,
                ZOrder = zOrder,
                PositionCode = positionCode
            };
            animation.CalculateDuration();
            return animation;
        }

        private static SkillAnimation ApplyAnimationDisplayerOriginOffset(SkillAnimation animation, Point originOffset)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0 || originOffset == Point.Zero)
            {
                return animation;
            }

            var shiftedFrames = new List<SkillFrame>(animation.Frames.Count);
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                SkillFrame frame = animation.Frames[i];
                if (frame == null)
                {
                    continue;
                }

                var shiftedOrigin = new Point(
                    frame.Origin.X - originOffset.X,
                    frame.Origin.Y - originOffset.Y);
                shiftedFrames.Add(new SkillFrame
                {
                    Texture = frame.Texture,
                    Origin = shiftedOrigin,
                    Delay = frame.Delay,
                    Bounds = new Rectangle(
                        -shiftedOrigin.X,
                        -shiftedOrigin.Y,
                        frame.Bounds.Width,
                        frame.Bounds.Height),
                    Flip = frame.Flip,
                    Z = frame.Z,
                    AlphaStart = frame.AlphaStart,
                    AlphaEnd = frame.AlphaEnd,
                    ZoomStart = frame.ZoomStart,
                    ZoomEnd = frame.ZoomEnd
                });
            }

            if (shiftedFrames.Count == 0)
            {
                return null;
            }

            SkillAnimation shiftedAnimation = new()
            {
                Name = animation.Name,
                Frames = shiftedFrames,
                Loop = animation.Loop,
                Origin = new Point(animation.Origin.X - originOffset.X, animation.Origin.Y - originOffset.Y),
                ZOrder = animation.ZOrder,
                PositionCode = animation.PositionCode
            };
            shiftedAnimation.CalculateDuration();
            return shiftedAnimation;
        }

        private static int? TryResolveAnimationDisplayerSkillUsePositionCode(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            if (property is WzStringProperty stringProperty
                && int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedString))
            {
                return parsedString;
            }

            try
            {
                return property.GetInt();
            }
            catch
            {
                return null;
            }
        }

        private static SkillAnimation ApplyAnimationDisplayerDelayRate(SkillAnimation animation, int delayRate)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return null;
            }

            if (delayRate <= 0 || delayRate == 1000)
            {
                return animation;
            }

            var scaledFrames = new List<SkillFrame>(animation.Frames.Count);
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                SkillFrame frame = animation.Frames[i];
                if (frame == null)
                {
                    continue;
                }

                int scaledDelay = Math.Max(1, (int)Math.Round(frame.Delay * (delayRate / 1000d)));
                scaledFrames.Add(new SkillFrame
                {
                    Texture = frame.Texture,
                    Origin = frame.Origin,
                    Delay = scaledDelay,
                    Bounds = frame.Bounds,
                    Flip = frame.Flip,
                    Z = frame.Z,
                    AlphaStart = frame.AlphaStart,
                    AlphaEnd = frame.AlphaEnd,
                    ZoomStart = frame.ZoomStart,
                    ZoomEnd = frame.ZoomEnd
                });
            }

            if (scaledFrames.Count == 0)
            {
                return null;
            }

            SkillAnimation scaledAnimation = new()
            {
                Name = animation.Name,
                Frames = scaledFrames,
                Loop = animation.Loop,
                Origin = animation.Origin,
                ZOrder = animation.ZOrder,
                PositionCode = animation.PositionCode
            };
            scaledAnimation.CalculateDuration();
            return scaledAnimation;
        }

        private static int BuildAnimationDisplayerSkillUseAvatarEffectRegistrationKey(
            int skillId,
            string branchName,
            int variantIndex)
        {
            int hash = HashCode.Combine(
                skillId,
                StringComparer.OrdinalIgnoreCase.GetHashCode(branchName ?? string.Empty),
                variantIndex);
            if (hash == int.MinValue)
            {
                hash = int.MaxValue;
            }

            hash = Math.Abs(hash);
            return hash == 0 ? Math.Max(1, skillId) : hash;
        }

        private bool TryGetAnimationDisplayerSkillUseFrameVariants(string effectUol, out List<List<IDXObject>> variants)
        {
            if (_animationDisplayerSkillUseEffectCache.TryGetValue(effectUol, out variants) && variants?.Count > 0)
            {
                return true;
            }

            variants = LoadAnimationDisplayerSkillUseFrameVariants(effectUol);
            if (variants == null || variants.Count == 0)
            {
                variants = null;
                return false;
            }

            _animationDisplayerSkillUseEffectCache[effectUol] = variants;
            return true;
        }

        private List<List<IDXObject>> LoadAnimationDisplayerSkillUseFrameVariants(string effectUol)
        {
            var variants = new List<List<IDXObject>>();

            string defaultChildUol = $"{effectUol}/default";
            string indexedChildUol = $"{effectUol}/0";
            bool hasCompositeChildren = IsAnimationDisplayerStructuredLayer(defaultChildUol)
                || IsAnimationDisplayerStructuredLayer(indexedChildUol);

            if (hasCompositeChildren)
            {
                TryAddAnimationDisplayerVariantFrames(variants, defaultChildUol);

                for (int i = 0; ; i++)
                {
                    string candidateUol = $"{effectUol}/{i.ToString(CultureInfo.InvariantCulture)}";
                    if (!IsAnimationDisplayerStructuredLayer(candidateUol))
                    {
                        break;
                    }

                    TryAddAnimationDisplayerVariantFrames(variants, candidateUol);
                }
            }
            else
            {
                TryAddAnimationDisplayerVariantFrames(variants, effectUol);
            }

            return variants.Count > 0 ? variants : null;
        }

        private static List<IDXObject> ApplyAnimationDisplayerDelayRate(List<IDXObject> frames, int delayRate)
        {
            if (!Animation.AnimationEffects.HasFrames(frames) || delayRate <= 0 || delayRate == 1000)
            {
                return frames;
            }

            var adjusted = new List<IDXObject>(frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                IDXObject frame = frames[i];
                int scaledDelay = Math.Max(1, (int)Math.Round(frame.Delay * (delayRate / 1000d)));
                adjusted.Add(new DelayAdjustedDxObject(frame, scaledDelay));
            }

            return adjusted;
        }

        private void TryAddAnimationDisplayerVariantFrames(List<List<IDXObject>> variants, string effectUol)
        {
            List<IDXObject> frames = LoadAnimationDisplayerFrames(effectUol);
            if (Animation.AnimationEffects.HasFrames(frames))
            {
                variants.Add(frames);
            }
        }

        private bool IsAnimationDisplayerStructuredLayer(string effectUol)
        {
            WzImageProperty property = ResolveAnimationDisplayerProperty(effectUol)?.GetLinkedWzImageProperty();
            if (property == null || property is WzCanvasProperty)
            {
                return false;
            }

            List<IDXObject> frames = ExtractPacketOwnedFrameSprites(LoadPacketOwnedAnimationFrames(property));
            return Animation.AnimationEffects.HasFrames(frames);
        }

        private IEnumerable<string> EnumerateAnimationDisplayerSkillUseBranchNames(SkillCastInfo castInfo)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(castInfo?.EffectAnimation?.Name) && seen.Add(castInfo.EffectAnimation.Name))
            {
                yield return castInfo.EffectAnimation.Name;
            }

            if (!string.IsNullOrWhiteSpace(castInfo?.SecondaryEffectAnimation?.Name) && seen.Add(castInfo.SecondaryEffectAnimation.Name))
            {
                yield return castInfo.SecondaryEffectAnimation.Name;
            }

            int skillId = castInfo?.SkillId ?? 0;
            if (skillId <= 0)
            {
                yield break;
            }

            WzImage skillImage = Program.FindImage("Skill", $"{skillId / 10000}.img");
            skillImage?.ParseImage();
            if (skillImage?["skill"]?[skillId.ToString("D7", CultureInfo.InvariantCulture)] is not WzImageProperty skillNode)
            {
                yield break;
            }

            foreach (WzImageProperty child in skillNode.WzProperties)
            {
                if (!IsAnimationDisplayerSkillUseBranchName(child?.Name) || !seen.Add(child.Name))
                {
                    continue;
                }

                yield return child.Name;
            }
        }

        private IEnumerable<AnimationDisplayerSkillUseBranchRequest> EnumerateAnimationDisplayerSkillUseBranchRequests(
            SkillCastInfo castInfo)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (castInfo?.RequestedBranchNames != null)
            {
                for (int i = 0; i < castInfo.RequestedBranchNames.Count; i++)
                {
                    string branchName = castInfo.RequestedBranchNames[i];
                    if (string.IsNullOrWhiteSpace(branchName) || !seen.Add(branchName))
                    {
                        continue;
                    }

                    yield return new AnimationDisplayerSkillUseBranchRequest(
                        branchName,
                        castInfo.OriginOffset,
                        castInfo.FollowOwnerFacing,
                        castInfo.FollowOwnerPosition);
                }
            }

            if (seen.Count > 0)
            {
                yield break;
            }

            foreach (string branchName in EnumerateAnimationDisplayerSkillUseBranchNames(castInfo))
            {
                if (!seen.Add(branchName))
                {
                    continue;
                }

                yield return new AnimationDisplayerSkillUseBranchRequest(
                    branchName,
                    castInfo?.OriginOffset ?? Point.Zero,
                    castInfo?.FollowOwnerFacing ?? true,
                    castInfo?.FollowOwnerPosition ?? true);
            }
        }

        private IEnumerable<string> EnumerateAnimationDisplayerSkillUseBranchNames(int skillId)
        {
            if (skillId <= 0)
            {
                yield break;
            }

            WzImage skillImage = Program.FindImage("Skill", $"{skillId / 10000}.img");
            skillImage?.ParseImage();
            if (skillImage?["skill"]?[skillId.ToString("D7", CultureInfo.InvariantCulture)] is not WzImageProperty skillNode)
            {
                yield break;
            }

            foreach (WzImageProperty child in skillNode.WzProperties)
            {
                if (!IsAnimationDisplayerSkillUseBranchName(child?.Name))
                {
                    continue;
                }

                yield return child.Name;
            }
        }

        private IEnumerable<AnimationDisplayerSkillUseBranchRequest> EnumerateAnimationDisplayerSkillUseBranchRequests(
            RemoteUserActorPool.RemoteSkillUsePresentation presentation)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (presentation.BranchNames != null)
            {
                for (int i = 0; i < presentation.BranchNames.Count; i++)
                {
                    string branchName = presentation.BranchNames[i];
                    if (string.IsNullOrWhiteSpace(branchName) || !seen.Add(branchName))
                    {
                        continue;
                    }

                    yield return new AnimationDisplayerSkillUseBranchRequest(
                        branchName,
                        presentation.OriginOffset,
                        presentation.FollowOwnerFacing,
                        presentation.FollowOwnerPosition);
                }
            }

            if (seen.Count > 0)
            {
                yield break;
            }

            foreach (string branchName in EnumerateAnimationDisplayerSkillUseBranchNames(presentation.SkillId))
            {
                if (seen.Add(branchName))
                {
                    yield return new AnimationDisplayerSkillUseBranchRequest(
                        branchName,
                        presentation.OriginOffset,
                        presentation.FollowOwnerFacing,
                        presentation.FollowOwnerPosition);
                }
            }
        }

        private static IReadOnlyList<string> ResolveAnimationDisplayerRequestedBranchNames(
            IReadOnlyList<string> requestedBranchNames,
            SkillAnimation effectAnimation,
            SkillAnimation secondaryEffectAnimation)
        {
            if (requestedBranchNames != null && requestedBranchNames.Count > 0)
            {
                return requestedBranchNames;
            }

            List<string> resolvedBranchNames = null;
            TryAddAnimationDisplayerRequestedBranchName(effectAnimation?.Name, ref resolvedBranchNames);
            TryAddAnimationDisplayerRequestedBranchName(secondaryEffectAnimation?.Name, ref resolvedBranchNames);
            return resolvedBranchNames;
        }

        internal static IReadOnlyList<string> BuildAnimationDisplayerBuffAppliedRequestedBranchNames(SkillData skillData)
        {
            List<string> resolvedBranchNames = null;
            TryAddAnimationDisplayerRequestedBranchName(skillData?.AffectedEffect?.Name, ref resolvedBranchNames);
            TryAddAnimationDisplayerRequestedBranchName(skillData?.AffectedSecondaryEffect?.Name, ref resolvedBranchNames);
            TryAddAnimationDisplayerRequestedBranchName(skillData?.SpecialAffectedEffect?.Name, ref resolvedBranchNames);
            return resolvedBranchNames;
        }

        private static void TryAddAnimationDisplayerRequestedBranchName(
            string branchName,
            ref List<string> requestedBranchNames)
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return;
            }

            requestedBranchNames ??= new List<string>();
            for (int i = 0; i < requestedBranchNames.Count; i++)
            {
                if (string.Equals(requestedBranchNames[i], branchName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            requestedBranchNames.Add(branchName);
        }

        private static bool IsAnimationDisplayerSkillUseBranchName(string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName) || !branchName.StartsWith("effect", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (branchName.Length == "effect".Length)
            {
                return true;
            }

            for (int i = "effect".Length; i < branchName.Length; i++)
            {
                if (!char.IsDigit(branchName[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildAnimationDisplayerSkillUseBranchUol(int skillId, string branchName)
        {
            return $"Skill/{(skillId / 10000).ToString(CultureInfo.InvariantCulture)}.img/skill/{skillId.ToString("D7", CultureInfo.InvariantCulture)}/{branchName}";
        }

        private int ResolveAnimationDisplayerSkillUseDelayRate(SkillCastInfo castInfo)
        {
            if (castInfo?.DelayRateOverride is > 0)
            {
                return castInfo.DelayRateOverride.Value;
            }

            int delayRate = _playerManager?.Skills?.ResolveSharedSkillUseDelayRate(castInfo?.SkillId ?? 0) ?? 1000;
            return delayRate > 0 ? delayRate : 1000;
        }

        private static int ResolveAnimationDisplayerSkillUseDelayRate(int? actionSpeed)
        {
            if (!actionSpeed.HasValue)
            {
                return 1000;
            }

            int rate = (1000 * (actionSpeed.Value + 10)) / 16;
            return rate > 0 ? rate : 1000;
        }

        private void TryResolveAnimationDisplayerSkillUseOwner(
            int ownerCharacterId,
            out Func<Vector2> getPosition,
            out Func<bool> getFacingRight)
        {
            getPosition = null;
            getFacingRight = null;

            PlayerCharacter player = _playerManager?.Player;
            if (ownerCharacterId <= 0
                || (player?.Build?.Id > 0 && player.Build.Id == ownerCharacterId))
            {
                if (player != null)
                {
                    getPosition = () => _playerManager?.Player?.Position ?? player.Position;
                    getFacingRight = () => _playerManager?.Player?.FacingRight ?? player.FacingRight;
                }

                return;
            }

            if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor actor) != true || actor == null)
            {
                return;
            }

            getPosition = () =>
            {
                if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor liveActor) == true && liveActor != null)
                {
                    return liveActor.Position;
                }

                return actor.Position;
            };
            getFacingRight = () =>
            {
                if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor liveActor) == true && liveActor != null)
                {
                    return liveActor.FacingRight;
                }

                return actor.FacingRight;
            };
        }

        private bool TryLoadAnimationDisplayerQuestDeliveryPhaseFrames(
            WzImageProperty sourceProperty,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames)
        {
            startFrames = null;
            repeatFrames = null;
            endFrames = null;

            sourceProperty = sourceProperty?.GetLinkedWzImageProperty() ?? sourceProperty;
            if (sourceProperty == null)
            {
                return false;
            }

            List<Point> frameSizes = GetAnimationDisplayerFrameSizes(sourceProperty);
            ResolveAnimationDisplayerQuestDeliveryPhaseRanges(
                frameSizes,
                out int startIndex,
                out int startCount,
                out int repeatIndex,
                out int repeatCount,
                out int endIndex,
                out int endCount);

            startFrames = LoadAnimationDisplayerFrameRange(sourceProperty, startIndex, startCount);
            repeatFrames = LoadAnimationDisplayerFrameRange(sourceProperty, repeatIndex, repeatCount);
            endFrames = LoadAnimationDisplayerFrameRange(sourceProperty, endIndex, endCount);
            return Animation.AnimationEffects.HasFrames(startFrames)
                || Animation.AnimationEffects.HasFrames(repeatFrames)
                || Animation.AnimationEffects.HasFrames(endFrames);
        }

        private List<IDXObject> LoadAnimationDisplayerFrames(string effectUol)
        {
            WzImageProperty property = ResolveAnimationDisplayerProperty(effectUol);
            return ExtractPacketOwnedFrameSprites(LoadPacketOwnedAnimationFrames(property));
        }

        private bool TryLoadRemotePacketOwnedStringEffectFrames(string effectUol, out List<IDXObject> frames)
        {
            frames = LoadAnimationDisplayerFrames(effectUol);
            if (Animation.AnimationEffects.HasFrames(frames))
            {
                return true;
            }

            string indexedEffectUol = $"{effectUol}/0";
            frames = LoadAnimationDisplayerFrames(indexedEffectUol);
            return Animation.AnimationEffects.HasFrames(frames);
        }

        internal static string NormalizeRemotePacketOwnedStringEffectUol(string effectPath)
        {
            string normalized = effectPath?.Replace('\\', '/').Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (normalized.StartsWith("Effect/", StringComparison.OrdinalIgnoreCase))
            {
                return $"Effect/{normalized["Effect/".Length..]}";
            }

            string firstSegment = normalized.Split('/')[0];
            if (firstSegment.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                return $"Effect/{normalized}";
            }

            return normalized;
        }

        private WzImageProperty ResolveAnimationDisplayerProperty(string effectUol)
        {
            return ResolveAnimationDisplayerPropertyStatic(effectUol);
        }

        private static WzImageProperty ResolveAnimationDisplayerPropertyStatic(string effectUol)
        {
            if (string.IsNullOrWhiteSpace(effectUol))
            {
                return null;
            }

            string normalized = effectUol.Replace('\\', '/').Trim().Trim('/');
            string[] segments = normalized.Split('/');
            if (segments.Length < 3)
            {
                return null;
            }

            string category = segments[0];
            string imageName = segments[1];
            string propertyPath = string.Join("/", segments, 2, segments.Length - 2);
            WzImage image = Program.FindImage(category, imageName);
            return ResolvePacketOwnedPropertyPath(image, propertyPath);
        }

        private List<IDXObject> LoadAnimationDisplayerFrameRange(WzImageProperty sourceProperty, int startIndex, int frameCount)
        {
            if (sourceProperty == null || frameCount <= 0)
            {
                return null;
            }

            sourceProperty = sourceProperty.GetLinkedWzImageProperty() ?? sourceProperty;
            List<IDXObject> frames = new();
            int sharedDelay = sourceProperty["delay"]?.GetInt() ?? 90;

            for (int i = 0; i < frameCount; i++)
            {
                int frameIndex = startIndex + i;
                if (sourceProperty[frameIndex.ToString(CultureInfo.InvariantCulture)] is not WzCanvasProperty frameCanvas)
                {
                    break;
                }

                List<IDXObject> frame = ExtractPacketOwnedFrameSprites(LoadPacketOwnedCanvasFrame(frameCanvas, sharedDelay));
                if (!Animation.AnimationEffects.HasFrames(frame))
                {
                    continue;
                }

                frames.AddRange(frame);
            }

            return frames.Count > 0 ? frames : null;
        }

        private static List<Point> GetAnimationDisplayerFrameSizes(WzImageProperty sourceProperty)
        {
            var frameSizes = new List<Point>();
            if (sourceProperty == null)
            {
                return frameSizes;
            }

            sourceProperty = sourceProperty.GetLinkedWzImageProperty() ?? sourceProperty;
            for (int i = 0; ; i++)
            {
                if (sourceProperty[i.ToString(CultureInfo.InvariantCulture)] is not WzCanvasProperty frameCanvas)
                {
                    break;
                }

                frameSizes.Add(new Point(frameCanvas.PngProperty.Width, frameCanvas.PngProperty.Height));
            }

            return frameSizes;
        }

        internal static string BuildAnimationDisplayerQuestDeliveryEffectUol(int itemId)
        {
            return itemId > 0
                ? $"{AnimationDisplayerQuestDeliveryEffectBaseUol}/{itemId.ToString(CultureInfo.InvariantCulture)}"
                : null;
        }

        internal static int BuildAnimationDisplayerQuestDeliveryRegistrationKey(int ownerCharacterId)
        {
            return ownerCharacterId > 0
                ? unchecked(int.MinValue | ownerCharacterId)
                : 0;
        }

        internal static int BuildAnimationDisplayerPacketOwnedFollowRegistrationKey(int ownerCharacterId)
        {
            return ownerCharacterId > 0
                ? unchecked(AnimationDisplayerPacketOwnedFollowRegistrationMask | ownerCharacterId)
                : 0;
        }

        internal static IReadOnlyList<Vector2> BuildAnimationDisplayerFollowGenerationPoints(int radius, int pointCount)
        {
            int resolvedPointCount = Math.Max(1, pointCount);
            float resolvedRadius = Math.Max(0f, radius);
            var points = new List<Vector2>(resolvedPointCount);
            for (int i = 0; i < resolvedPointCount; i++)
            {
                int angleDegrees = (int)Math.Round((360d / resolvedPointCount) * i);
                points.Add(Animation.AnimationEffects.ResolvePolarFollowOffset(resolvedRadius, angleDegrees));
            }

            return points;
        }

        private AnimationDisplayerFollowEquipmentDefinition ResolveAnimationDisplayerFollowEquipmentDefinition(int ownerCharacterId)
        {
            CharacterBuild build = null;
            if (_playerManager?.Player?.Build?.Id == ownerCharacterId)
            {
                build = _playerManager.Player.Build;
            }
            else if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor actor) == true)
            {
                build = actor?.Build;
            }

            return ResolveAnimationDisplayerFollowEquipmentDefinition(build);
        }

        private AnimationDisplayerFollowEquipmentDefinition ResolveAnimationDisplayerFollowEquipmentDefinition(CharacterBuild build)
        {
            if (build == null)
            {
                return null;
            }

            foreach (int itemId in EnumerateAnimationDisplayerFollowEquipmentItemIds(build))
            {
                if (itemId <= 0)
                {
                    continue;
                }

                if (_animationDisplayerFollowEquipmentCache.TryGetValue(itemId, out AnimationDisplayerFollowEquipmentDefinition cachedDefinition))
                {
                    if (cachedDefinition != null)
                    {
                        return cachedDefinition;
                    }

                    continue;
                }

                AnimationDisplayerFollowEquipmentDefinition loadedDefinition = LoadAnimationDisplayerFollowEquipmentDefinition(itemId);
                _animationDisplayerFollowEquipmentCache[itemId] = loadedDefinition;
                if (loadedDefinition != null)
                {
                    return loadedDefinition;
                }
            }

            return null;
        }

        private static IEnumerable<int> EnumerateAnimationDisplayerFollowEquipmentItemIds(CharacterBuild build)
        {
            if (build == null)
            {
                yield break;
            }

            EquipSlot[] slots =
            {
                EquipSlot.Cap,
                EquipSlot.FaceAccessory,
                EquipSlot.EyeAccessory,
                EquipSlot.Earrings,
                EquipSlot.Coat,
                EquipSlot.Longcoat,
                EquipSlot.Pants,
                EquipSlot.Shoes,
                EquipSlot.Glove,
                EquipSlot.Shield,
                EquipSlot.Weapon,
                EquipSlot.Ring1,
                EquipSlot.Ring2,
                EquipSlot.Ring3,
                EquipSlot.Ring4,
                EquipSlot.Pendant,
                EquipSlot.Saddle,
                EquipSlot.Cape,
                EquipSlot.Medal,
                EquipSlot.Belt,
                EquipSlot.Shoulder,
                EquipSlot.Pocket,
                EquipSlot.Badge,
                EquipSlot.Pendant2,
                EquipSlot.TamingMobAccessory,
                EquipSlot.Android,
                EquipSlot.AndroidHeart,
                EquipSlot.TamingMob
            };
            var seenItemIds = new HashSet<int>();

            for (int i = 0; i < slots.Length; i++)
            {
                EquipSlot slot = slots[i];
                if (build.Equipment.TryGetValue(slot, out CharacterPart visiblePart) && visiblePart?.ItemId > 0 && seenItemIds.Add(visiblePart.ItemId))
                {
                    yield return visiblePart.ItemId;
                }

                if (build.HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart) && hiddenPart?.ItemId > 0 && seenItemIds.Add(hiddenPart.ItemId))
                {
                    yield return hiddenPart.ItemId;
                }
            }
        }

        private AnimationDisplayerFollowEquipmentDefinition LoadAnimationDisplayerFollowEquipmentDefinition(int itemId)
        {
            WzSubProperty effectProperty = ResolveAnimationDisplayerFollowEquipmentProperty(itemId);
            if (effectProperty == null)
            {
                return null;
            }

            string effectPath = NormalizeAnimationDisplayerPath(effectProperty["path"]?.GetString());
            string effectUol = BuildAnimationDisplayerFollowEquipmentEffectUol(effectPath);
            IReadOnlyList<List<IDXObject>> effectFrameVariants = LoadAnimationDisplayerFollowEquipmentFrameVariants(effectPath);
            if (!Animation.AnimationEffects.HasFrameVariants(effectFrameVariants)
                && !Animation.AnimationEffects.HasFrames(LoadAnimationDisplayerFrames(effectUol)))
            {
                return null;
            }

            Rectangle emissionArea = BuildAnimationDisplayerFollowEquipmentEmissionArea(effectProperty);
            int updateIntervalMs = GetAnimationDisplayerNumericValue(effectProperty, "interval")
                ?? AnimationDisplayerFollowUpdateIntervalMs;
            int spawnDurationMs = GetAnimationDisplayerNumericValue(effectProperty, "delay") ?? 0;
            int? authoredFollowFlag = GetAnimationDisplayerNumericValue(effectProperty, "follow");
            bool? authoredRelativePosition = ResolveAnimationDisplayerFollowAuthoredRelativePosition(authoredFollowFlag);
            Point spawnOffsetMin = BuildAnimationDisplayerFollowEquipmentSpawnOffsetMin(effectProperty, authoredRelativePosition);
            Point spawnOffsetMax = BuildAnimationDisplayerFollowEquipmentSpawnOffsetMax(effectProperty, spawnOffsetMin);
            IReadOnlyList<Vector2> generationPoints = LoadAnimationDisplayerFollowEquipmentGenerationPoints(effectProperty["genPoint"]);
            float radius = BuildAnimationDisplayerFollowEquipmentRadius(generationPoints, spawnOffsetMax);

            return new AnimationDisplayerFollowEquipmentDefinition
            {
                ItemId = itemId,
                EffectUol = effectUol,
                EffectFrameVariants = effectFrameVariants,
                GenerationPoints = generationPoints,
                EmissionArea = emissionArea,
                UpdateIntervalMs = updateIntervalMs,
                SpawnDurationMs = spawnDurationMs,
                SpawnOffsetMin = spawnOffsetMin,
                SpawnOffsetMax = spawnOffsetMax,
                UsesRelativeEmission = ResolveAnimationDisplayerFollowEquipmentEmission(
                    GetAnimationDisplayerNumericValue(effectProperty, "emission")),
                SpawnRelativeToTarget = authoredRelativePosition,
                SpawnOnlyOnOwnerMove = GetAnimationDisplayerNumericValue(effectProperty, "genOnMove")
                    == 1
                    || GetAnimationDisplayerNumericValue(effectProperty, "bGenOnMove") == 1,
                SuppressOwnerFlip = ResolveAnimationDisplayerFollowEquipmentNoFlip(
                    GetAnimationDisplayerNumericValue(effectProperty, "bNoFlip"),
                    GetAnimationDisplayerNumericValue(effectProperty, "noFlip")),
                ThetaDegrees = ResolveAnimationDisplayerFollowEquipmentThetaDegrees(
                    GetAnimationDisplayerNumericValue(effectProperty, "nTheta"),
                    GetAnimationDisplayerNumericValue(effectProperty, "theta")),
                Radius = radius,
                ZOrder = ResolveAnimationDisplayerFollowEquipmentZOrder(
                    GetAnimationDisplayerNumericValue(effectProperty, "z"))
            };
        }

        private static WzSubProperty ResolveAnimationDisplayerFollowEquipmentProperty(int itemId)
        {
            if (itemId <= 0)
            {
                return null;
            }

            string equipDataPath = ResolveAnimationDisplayerFollowEquipmentDataPath(itemId);
            if (string.IsNullOrWhiteSpace(equipDataPath))
            {
                return null;
            }

            WzImage itemImage = Program.FindImage("Character", equipDataPath);
            itemImage?.ParseImage();
            return itemImage?["info"]?["effect"] as WzSubProperty;
        }

        internal static string ResolveAnimationDisplayerFollowEquipmentDataPath(int itemId)
        {
            string folder = ResolveAnimationDisplayerFollowEquipmentFolder(itemId);
            return string.IsNullOrWhiteSpace(folder)
                ? null
                : $"{folder}/{itemId:D8}.img";
        }

        internal static string ResolveAnimationDisplayerFollowEquipmentFolder(int itemId)
        {
            int category = itemId / 10000;
            return category switch
            {
                100 => "Cap",
                101 or 102 or 103 => "Accessory",
                104 => "Coat",
                105 => "Longcoat",
                106 => "Pants",
                107 => "Shoes",
                108 => "Glove",
                109 => "Shield",
                110 => "Cape",
                111 => "Ring",
                112 or 113 or 114 or 115 or 116 or 118 => "Accessory",
                166 or 167 => "Android",
                170 => "Weapon",
                119 or 134 => "Weapon",
                >= 130 and < 170 => "Weapon",
                180 or 198 or 199 => "TamingMob",
                >= 190 and < 200 => "TamingMob",
                _ => null
            };
        }

        private static string BuildAnimationDisplayerFollowEquipmentEffectUol(string effectPath)
        {
            if (string.IsNullOrWhiteSpace(effectPath))
            {
                return null;
            }

            WzImageProperty property = ResolveAnimationDisplayerPropertyStatic(effectPath);
            if (property is WzCanvasProperty)
            {
                return effectPath;
            }

            if (ResolveAnimationDisplayerPropertyStatic($"{effectPath}/0") != null)
            {
                return $"{effectPath}/0";
            }

            return effectPath;
        }

        private IReadOnlyList<List<IDXObject>> LoadAnimationDisplayerFollowEquipmentFrameVariants(string effectPath)
        {
            string[] variantUols = EnumerateAnimationDisplayerFollowEquipmentEffectVariantUols(
                effectPath,
                ResolveAnimationDisplayerPropertyStatic(effectPath));
            if (variantUols.Length == 0)
            {
                return null;
            }

            var variants = new List<List<IDXObject>>(variantUols.Length);
            for (int i = 0; i < variantUols.Length; i++)
            {
                List<IDXObject> frames = LoadAnimationDisplayerFrames(variantUols[i]);
                if (Animation.AnimationEffects.HasFrames(frames))
                {
                    variants.Add(frames);
                }
            }

            return variants.Count > 0 ? variants : null;
        }

        internal static string[] EnumerateAnimationDisplayerFollowEquipmentEffectVariantUols(
            string effectPath,
            WzImageProperty effectRootProperty)
        {
            string normalizedEffectPath = NormalizeAnimationDisplayerPath(effectPath);
            WzImageProperty resolvedRoot = WzInfoTools.GetRealProperty(effectRootProperty);
            if (string.IsNullOrWhiteSpace(normalizedEffectPath) || resolvedRoot is not WzSubProperty rootSubProperty)
            {
                return Array.Empty<string>();
            }

            var variantUols = new List<string>();
            int childCount = rootSubProperty.WzProperties?.Count ?? 0;
            for (int index = 0; index < childCount; index++)
            {
                string childName = index.ToString(CultureInfo.InvariantCulture);
                WzImageProperty variantProperty = WzInfoTools.GetRealProperty(rootSubProperty[childName]);
                if (variantProperty == null)
                {
                    break;
                }

                variantUols.Add($"{normalizedEffectPath}/{childName}");
            }

            return variantUols.ToArray();
        }

        internal static int? GetAnimationDisplayerNumericValue(WzImageProperty parentProperty, string propertyName)
        {
            if (parentProperty == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            return GetAnimationDisplayerNumericValue(parentProperty[propertyName]);
        }

        internal static int? GetAnimationDisplayerNumericValue(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            int? directValue = property.GetInt();
            if (directValue.HasValue)
            {
                return directValue.Value;
            }

            string stringValue = property.GetString();
            return int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue)
                ? parsedValue
                : null;
        }

        internal static bool? ResolveAnimationDisplayerFollowAuthoredRelativePosition(int? authoredFollowFlag)
        {
            return authoredFollowFlag.HasValue
                ? authoredFollowFlag.Value != 0
                : null;
        }

        internal static int ResolveAnimationDisplayerFollowAuthoredDefaultOffsetY(bool? authoredRelativePosition)
        {
            return (authoredRelativePosition ?? true)
                ? 0
                : AnimationDisplayerFollowAbsoluteTravelOffsetY;
        }

        internal static bool ResolveAnimationDisplayerFollowEquipmentEmission(int? authoredEmission)
        {
            return authoredEmission.GetValueOrDefault() != 0;
        }

        internal static bool ResolveAnimationDisplayerFollowEquipmentNoFlip(int? authoredBNoFlip, int? authoredNoFlip)
        {
            return (authoredBNoFlip ?? authoredNoFlip ?? 1) != 0;
        }

        internal static int ResolveAnimationDisplayerFollowEquipmentThetaDegrees(int? authoredNTheta, int? authoredTheta)
        {
            return authoredNTheta ?? authoredTheta ?? 0;
        }

        internal static int ResolveAnimationDisplayerFollowEquipmentZOrder(int? authoredZ)
        {
            return authoredZ ?? 0;
        }

        private static Rectangle BuildAnimationDisplayerFollowEquipmentEmissionArea(WzImageProperty effectProperty)
        {
            int left = GetAnimationDisplayerNumericValue(effectProperty, "left") ?? 0;
            int top = GetAnimationDisplayerNumericValue(effectProperty, "top") ?? 0;
            int right = GetAnimationDisplayerNumericValue(effectProperty, "right") ?? AnimationDisplayerFollowEmissionBoxSize;
            int bottom = GetAnimationDisplayerNumericValue(effectProperty, "bottom") ?? AnimationDisplayerFollowEmissionBoxSize;
            int width = Math.Max(1, right - left);
            int height = Math.Max(1, bottom - top);
            return new Rectangle(left, top, width, height);
        }

        private static IReadOnlyList<Vector2> LoadAnimationDisplayerFollowEquipmentGenerationPoints(WzImageProperty generationPointProperty)
        {
            if (generationPointProperty == null)
            {
                return null;
            }

            generationPointProperty = generationPointProperty.GetLinkedWzImageProperty() ?? generationPointProperty;
            var points = new List<Vector2>();
            for (int index = 0; ; index++)
            {
                if (generationPointProperty[index.ToString()] is not WzVectorProperty point)
                {
                    break;
                }

                points.Add(new Vector2(point.X?.GetInt() ?? 0, point.Y?.GetInt() ?? 0));
            }

            return points.Count > 0 ? points : null;
        }

        private static Point BuildAnimationDisplayerFollowEquipmentSpawnOffsetMin(
            WzImageProperty effectProperty,
            bool? authoredRelativePosition)
        {
            return new Point(
                GetAnimationDisplayerNumericValue(effectProperty, "x0")
                    ?? GetAnimationDisplayerNumericValue(effectProperty, "rx0")
                    ?? 0,
                GetAnimationDisplayerNumericValue(effectProperty, "y0")
                    ?? GetAnimationDisplayerNumericValue(effectProperty, "ry0")
                    ?? ResolveAnimationDisplayerFollowAuthoredDefaultOffsetY(authoredRelativePosition));
        }

        private static Point BuildAnimationDisplayerFollowEquipmentSpawnOffsetMax(WzImageProperty effectProperty, Point spawnOffsetMin)
        {
            return new Point(
                GetAnimationDisplayerNumericValue(effectProperty, "x1")
                    ?? GetAnimationDisplayerNumericValue(effectProperty, "rx1")
                    ?? GetAnimationDisplayerNumericValue(effectProperty, "dx")
                    ?? spawnOffsetMin.X,
                GetAnimationDisplayerNumericValue(effectProperty, "y1")
                    ?? GetAnimationDisplayerNumericValue(effectProperty, "ry1")
                    ?? GetAnimationDisplayerNumericValue(effectProperty, "dy")
                    ?? GetAnimationDisplayerNumericValue(effectProperty, "dx")
                    ?? spawnOffsetMin.Y);
        }

        private static float BuildAnimationDisplayerFollowEquipmentRadius(IReadOnlyList<Vector2> generationPoints, Point spawnOffsetMax)
        {
            if (generationPoints != null && generationPoints.Count > 0)
            {
                float maxLengthSquared = 0f;
                for (int i = 0; i < generationPoints.Count; i++)
                {
                    maxLengthSquared = Math.Max(maxLengthSquared, generationPoints[i].LengthSquared());
                }

                if (maxLengthSquared > 0f)
                {
                    return (float)Math.Sqrt(maxLengthSquared);
                }
            }

            float offsetLength = new Vector2(spawnOffsetMax.X, spawnOffsetMax.Y).Length();
            return Math.Max(AnimationDisplayerFollowRadius, offsetLength);
        }

        internal static string[] EnumerateAnimationDisplayerFollowEffectUols()
        {
            return AnimationDisplayerFollowEffectUolCandidates;
        }

        internal static Rectangle BuildAnimationDisplayerFollowEmissionArea()
        {
            return new Rectangle(
                0,
                0,
                AnimationDisplayerFollowEmissionBoxSize,
                AnimationDisplayerFollowEmissionBoxSize);
        }

        internal static Point BuildAnimationDisplayerFollowSpawnOffsetMin(bool relativeEmission)
        {
            return relativeEmission
                ? Point.Zero
                : new Point(0, AnimationDisplayerFollowAbsoluteTravelOffsetY);
        }

        private static Point BuildAnimationDisplayerFollowSpawnOffsetMin(bool relativeEmission, AnimationDisplayerFollowEquipmentDefinition followDefinition)
        {
            if (!relativeEmission || followDefinition == null)
            {
                return BuildAnimationDisplayerFollowSpawnOffsetMin(relativeEmission);
            }

            return followDefinition.SpawnOffsetMin;
        }

        internal static Point BuildAnimationDisplayerFollowSpawnOffsetMax(bool relativeEmission)
        {
            return BuildAnimationDisplayerFollowSpawnOffsetMin(relativeEmission);
        }

        internal static bool ResolveAnimationDisplayerFollowSpawnRelativeToTarget(
            bool commandRelativeEmission,
            bool? authoredRelativePosition)
        {
            return commandRelativeEmission && (authoredRelativePosition ?? true);
        }

        private static Point BuildAnimationDisplayerFollowSpawnOffsetMax(bool relativeEmission, AnimationDisplayerFollowEquipmentDefinition followDefinition)
        {
            if (!relativeEmission || followDefinition == null)
            {
                return BuildAnimationDisplayerFollowSpawnOffsetMax(relativeEmission);
            }

            return followDefinition.SpawnOffsetMax;
        }

        private IReadOnlyList<List<IDXObject>> LoadAnimationDisplayerFollowFrameVariants()
        {
            var variants = new List<List<IDXObject>>();
            string[] candidateUols = EnumerateAnimationDisplayerFollowEffectUols();
            for (int i = 0; i < candidateUols.Length; i++)
            {
                string candidateUol = candidateUols[i];
                if (string.IsNullOrWhiteSpace(candidateUol))
                {
                    continue;
                }

                string cacheKey = $"follow:variant:{i}";
                if (TryGetAnimationDisplayerFrames(cacheKey, candidateUol, out List<IDXObject> frames))
                {
                    variants.Add(frames);
                }
            }

            return variants;
        }

        internal static string[] EnumerateAnimationDisplayerQuestDeliveryEffectUols(int itemId)
        {
            if (itemId <= 0)
            {
                return Array.Empty<string>();
            }

            string directUol = BuildAnimationDisplayerQuestDeliveryEffectUol(itemId);
            if (itemId == AnimationDisplayerQuestDeliveryFallbackEffectItemId)
            {
                return string.IsNullOrWhiteSpace(directUol)
                    ? Array.Empty<string>()
                    : new[] { directUol };
            }

            string fallbackUol = BuildAnimationDisplayerQuestDeliveryEffectUol(AnimationDisplayerQuestDeliveryFallbackEffectItemId);
            return string.IsNullOrWhiteSpace(directUol)
                ? new[] { fallbackUol }
                : new[] { directUol, fallbackUol };
        }

        internal static int[] EnumerateAnimationDisplayerQuestDeliveryEffectItemIds(int itemId)
        {
            if (itemId <= 0)
            {
                return Array.Empty<int>();
            }

            if (itemId == AnimationDisplayerQuestDeliveryFallbackEffectItemId)
            {
                return new[] { itemId };
            }

            return new[] { itemId, AnimationDisplayerQuestDeliveryFallbackEffectItemId };
        }

        internal static void ResolveAnimationDisplayerQuestDeliveryPhaseRanges(
            IReadOnlyList<Point> frameSizes,
            out int startIndex,
            out int startCount,
            out int repeatIndex,
            out int repeatCount,
            out int endIndex,
            out int endCount)
        {
            startIndex = 0;
            startCount = 0;
            repeatIndex = 0;
            repeatCount = 0;
            endIndex = 0;
            endCount = 0;

            if (frameSizes == null || frameSizes.Count == 0)
            {
                return;
            }

            int firstMaxIndex = 0;
            int lastMaxIndex = 0;
            long maxArea = long.MinValue;

            for (int i = 0; i < frameSizes.Count; i++)
            {
                Point size = frameSizes[i];
                long area = (long)Math.Max(0, size.X) * Math.Max(0, size.Y);
                if (area > maxArea)
                {
                    maxArea = area;
                    firstMaxIndex = i;
                    lastMaxIndex = i;
                }
                else if (area == maxArea)
                {
                    lastMaxIndex = i;
                }
            }

            startIndex = 0;
            startCount = firstMaxIndex;
            repeatIndex = firstMaxIndex;
            repeatCount = Math.Max(1, lastMaxIndex - firstMaxIndex + 1);
            endIndex = repeatIndex + repeatCount;
            endCount = Math.Max(0, frameSizes.Count - endIndex);
        }

        private Rectangle ResolveAnimationDisplayerArea(string[] args, int startIndex)
        {
            if (args.Length >= startIndex + 4
                && int.TryParse(args[startIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                && int.TryParse(args[startIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                && int.TryParse(args[startIndex + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width)
                && int.TryParse(args[startIndex + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
            {
                return new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
            }

            return ResolveAnimationDisplayerTransientLayerArea(_playerManager?.Player?.Position ?? Vector2.Zero);
        }

        private void ApplyPacketOwnedAnimationDisplayerTransientLayer(int itemId, string weatherPath)
        {
            ResetPacketOwnedAnimationDisplayerTransientLayers();

            Rectangle area = ResolveAnimationDisplayerTransientLayerArea(_playerManager?.Player?.Position ?? Vector2.Zero);
            switch (ResolveAnimationDisplayerTransientEffectKind(itemId, weatherPath))
            {
                case AnimationDisplayerTransientEffectKind.NewYear:
                    int newYearId = TryRegisterAnimationDisplayerAreaAnimation(
                        cacheKey: "newyear",
                        effectUol: AnimationDisplayerNewYearEffectUol,
                        area,
                        AnimationDisplayerNewYearUpdateIntervalMs,
                        AnimationDisplayerNewYearUpdateCount,
                        AnimationDisplayerNewYearUpdateNextMs,
                        AnimationDisplayerTransientLayerDurationMs,
                        onSpawn: () => TryPlayAnimationDisplayerNewYearTransientSound(weatherPath));
                    if (newYearId >= 0)
                    {
                        _packetOwnedAnimationDisplayerAreaAnimationIds.Add(newYearId);
                    }
                    break;

                case AnimationDisplayerTransientEffectKind.FireCracker:
                    string[] effectUols = BuildAnimationDisplayerFireCrackerEffectUols(weatherPath);
                    for (int i = 0; i < AnimationDisplayerFireCrackerBurstSchedule.Length; i++)
                    {
                        (int updateIntervalMs, int updateCount) = AnimationDisplayerFireCrackerBurstSchedule[i];
                        string effectUol = i < effectUols.Length
                            ? effectUols[i]
                            : BuildAnimationDisplayerFireCrackerEffectUol(i, weatherPath);
                        int areaAnimationId = TryRegisterAnimationDisplayerAreaAnimation(
                            cacheKey: $"firecracker:{i}",
                            effectUol,
                            area,
                            updateIntervalMs,
                            updateCount,
                            AnimationDisplayerFireCrackerUpdateNextMs,
                            AnimationDisplayerTransientLayerDurationMs);
                        if (areaAnimationId >= 0)
                        {
                            _packetOwnedAnimationDisplayerAreaAnimationIds.Add(areaAnimationId);
                        }
                    }
                    break;
            }
        }

        private void ResetPacketOwnedAnimationDisplayerTransientLayers()
        {
            for (int i = 0; i < _packetOwnedAnimationDisplayerAreaAnimationIds.Count; i++)
            {
                _animationEffects.RemoveAreaAnimation(_packetOwnedAnimationDisplayerAreaAnimationIds[i]);
            }

            _packetOwnedAnimationDisplayerAreaAnimationIds.Clear();
        }

        internal static Rectangle ResolveAnimationDisplayerTransientLayerArea(Vector2 centerPosition)
        {
            int width = AnimationDisplayerTransientLayerWidth;
            int height = AnimationDisplayerTransientLayerHeight;
            return new Rectangle(
                (int)Math.Round(centerPosition.X) - width / 2,
                (int)Math.Round(centerPosition.Y) - height / 2,
                width,
                height);
        }

        internal static AnimationDisplayerTransientEffectKind ResolveAnimationDisplayerTransientEffectKind(int itemId, string weatherPath)
        {
            if (itemId == 4300000)
            {
                return AnimationDisplayerTransientEffectKind.NewYear;
            }

            if (itemId == 5680024)
            {
                return AnimationDisplayerTransientEffectKind.FireCracker;
            }

            string normalizedPath = weatherPath?.Replace('\\', '/').Trim();
            if (string.Equals(normalizedPath, AnimationDisplayerNewYearEffectUol, StringComparison.OrdinalIgnoreCase)
                || IsAnimationDisplayerNewYearWeatherPath(normalizedPath))
            {
                return AnimationDisplayerTransientEffectKind.NewYear;
            }

            if (string.Equals(normalizedPath, AnimationDisplayerFireCrackerEffectUol, StringComparison.OrdinalIgnoreCase)
                || normalizedPath?.Contains("/firecracker", StringComparison.OrdinalIgnoreCase) == true
                || normalizedPath?.Contains("/firework", StringComparison.OrdinalIgnoreCase) == true)
            {
                return AnimationDisplayerTransientEffectKind.FireCracker;
            }

            return AnimationDisplayerTransientEffectKind.None;
        }

        private static bool IsAnimationDisplayerNewYearWeatherPath(string weatherPath)
        {
            string normalizedPath = NormalizeAnimationDisplayerPath(weatherPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            for (int i = 0; i < AnimationDisplayerNewYearWeatherAliases.Length; i++)
            {
                string alias = AnimationDisplayerNewYearWeatherAliases[i];
                if (normalizedPath.EndsWith($"/weather/{alias}", StringComparison.OrdinalIgnoreCase)
                    || normalizedPath.EndsWith($"/weather/{alias}.img", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedPath, alias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static IReadOnlyList<(int UpdateIntervalMs, int UpdateCount)> GetAnimationDisplayerFireCrackerBurstSchedule()
        {
            return AnimationDisplayerFireCrackerBurstSchedule;
        }

        internal static string BuildAnimationDisplayerFireCrackerEffectUol(int burstIndex, string weatherPath)
        {
            if (burstIndex < 0)
            {
                return null;
            }

            string baseUol = NormalizeAnimationDisplayerFireCrackerEffectBaseUol(weatherPath);
            return string.IsNullOrWhiteSpace(baseUol)
                ? null
                : $"{baseUol}/{burstIndex.ToString(CultureInfo.InvariantCulture)}";
        }

        internal static string[] BuildAnimationDisplayerFireCrackerEffectUols(string weatherPath)
        {
            string[] effectUols = new string[AnimationDisplayerFireCrackerBurstSchedule.Length];
            for (int i = 0; i < effectUols.Length; i++)
            {
                effectUols[i] = BuildAnimationDisplayerFireCrackerEffectUol(i, weatherPath);
            }

            return effectUols;
        }

        private static string NormalizeAnimationDisplayerFireCrackerEffectBaseUol(string weatherPath)
        {
            string normalizedPath = weatherPath?.Replace('\\', '/').Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return AnimationDisplayerFireCrackerEffectUol;
            }

            if (normalizedPath.StartsWith("Effect/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath["Effect/".Length..];
            }

            if (normalizedPath.StartsWith("OnUserEff.img/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath["OnUserEff.img/".Length..];
            }

            if (normalizedPath.EndsWith("/0", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith("/1", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith("/2", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith("/3", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith("/4", StringComparison.OrdinalIgnoreCase))
            {
                int separatorIndex = normalizedPath.LastIndexOf('/');
                normalizedPath = separatorIndex >= 0
                    ? normalizedPath[..separatorIndex]
                    : normalizedPath;
            }

            if (normalizedPath.Contains("itemEffect/firework/", StringComparison.OrdinalIgnoreCase))
            {
                return $"Effect/OnUserEff.img/{normalizedPath}";
            }

            return AnimationDisplayerFireCrackerEffectUol;
        }

        private void TryPlayAnimationDisplayerNewYearTransientSound(string weatherPath)
        {
            string[] candidates = BuildAnimationDisplayerNewYearSoundCandidates(weatherPath);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (TryPlayPacketOwnedWzSound(candidates[i], defaultImageName: "Field.img", out _, out _))
                {
                    return;
                }
            }
        }

        internal static string[] BuildAnimationDisplayerNewYearSoundCandidates(string weatherPath)
        {
            var candidates = new List<string>();
            foreach (string soundKey in EnumerateAnimationDisplayerNewYearSoundKeys(weatherPath))
            {
                AddAnimationDisplayerNewYearSoundCandidate(
                    candidates,
                    BuildAnimationDisplayerNewYearSoundDescriptor(soundKey));
            }

            AddAnimationDisplayerNewYearSoundCandidate(candidates, "Field/newyear");
            AddAnimationDisplayerNewYearSoundCandidate(candidates, "Field/weather/newyear");
            AddAnimationDisplayerNewYearSoundCandidate(candidates, "Game/newyear");

            string normalizedWeatherPath = NormalizeAnimationDisplayerPath(weatherPath);
            if (string.IsNullOrWhiteSpace(normalizedWeatherPath))
            {
                return candidates.ToArray();
            }

            string[] segments = normalizedWeatherPath.Split('/');
            string lastSegment = segments.Length > 0 ? segments[^1] : null;
            string secondLastSegment = segments.Length > 1 ? segments[^2] : null;
            string imageSegment = segments.Length > 1 ? NormalizeAnimationDisplayerImageNameSegment(segments[1]) : null;

            if (!string.IsNullOrWhiteSpace(lastSegment))
            {
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/weather/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Game/{lastSegment}");
            }

            if (!string.IsNullOrWhiteSpace(secondLastSegment) && !string.IsNullOrWhiteSpace(lastSegment))
            {
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/{secondLastSegment}/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Game/{secondLastSegment}/{lastSegment}");
            }

            if (!string.IsNullOrWhiteSpace(imageSegment) && !string.IsNullOrWhiteSpace(lastSegment))
            {
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/{imageSegment}/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/{imageSegment}/weather/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Game/{imageSegment}/{lastSegment}");
            }

            for (int i = 0; i < AnimationDisplayerNewYearSoundImageCandidates.Length; i++)
            {
                string imageName = AnimationDisplayerNewYearSoundImageCandidates[i];
                if (!string.IsNullOrWhiteSpace(lastSegment))
                {
                    AddAnimationDisplayerNewYearSoundCandidate(candidates, $"{imageName}/{lastSegment}");
                    AddAnimationDisplayerNewYearSoundCandidate(candidates, $"{imageName}/weather/{lastSegment}");
                }
            }

            return candidates.ToArray();
        }

        internal static string BuildAnimationDisplayerNewYearSoundDescriptor(string soundKey)
        {
            if (string.IsNullOrWhiteSpace(soundKey))
            {
                return null;
            }

            string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                AnimationDisplayerNewYearSoundStringPoolId,
                "Sound/Field.img/{0}",
                maxPlaceholderCount: 1,
                out _);
            string formatted = string.Format(CultureInfo.InvariantCulture, compositeFormat, soundKey.Trim());
            return NormalizeAnimationDisplayerSoundDescriptor(formatted);
        }

        internal static string NormalizeAnimationDisplayerSoundDescriptor(string descriptor)
        {
            string normalized = NormalizeAnimationDisplayerPath(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (normalized.StartsWith("Sound/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["Sound/".Length..];
            }

            string[] segments = normalized.Split('/');
            if (segments.Length == 0)
            {
                return null;
            }

            if (segments[0].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                segments[0] = NormalizeAnimationDisplayerImageNameSegment(segments[0]);
            }

            return NormalizeAnimationDisplayerPath(string.Join("/", segments));
        }

        private static string NormalizeAnimationDisplayerPath(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim().Replace('\\', '/').Trim('/');
        }

        private static IEnumerable<string> EnumerateAnimationDisplayerNewYearSoundKeys(string weatherPath)
        {
            string normalizedWeatherPath = NormalizeAnimationDisplayerPath(weatherPath);
            if (string.IsNullOrWhiteSpace(normalizedWeatherPath))
            {
                yield return "newyear";
                yield break;
            }

            string normalizedSoundDescriptor = NormalizeAnimationDisplayerSoundDescriptor(normalizedWeatherPath);
            if (!string.IsNullOrWhiteSpace(normalizedSoundDescriptor))
            {
                string[] directSegments = normalizedSoundDescriptor.Split('/');
                if (directSegments.Length >= 2)
                {
                    yield return string.Join("/", directSegments, 1, directSegments.Length - 1);
                }
            }

            string[] segments = normalizedWeatherPath.Split('/');
            int weatherSegmentIndex = Array.FindIndex(
                segments,
                segment => string.Equals(segment, "weather", StringComparison.OrdinalIgnoreCase));
            if (weatherSegmentIndex >= 0 && weatherSegmentIndex < segments.Length - 1)
            {
                yield return string.Join("/", segments, weatherSegmentIndex + 1, segments.Length - weatherSegmentIndex - 1);
            }

            if (segments.Length > 0)
            {
                yield return segments[^1];
            }
        }

        private static string NormalizeAnimationDisplayerImageNameSegment(string value)
        {
            string normalized = NormalizeAnimationDisplayerPath(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            int dotIndex = normalized.IndexOf(".img", StringComparison.OrdinalIgnoreCase);
            return dotIndex > 0 ? normalized[..dotIndex] : normalized;
        }

        private static void AddAnimationDisplayerNewYearSoundCandidate(List<string> candidates, string descriptor)
        {
            string normalized = NormalizeAnimationDisplayerPath(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(normalized);
        }

        private static bool TryResolveAnimationDisplayerFollowRelativeEmission(string[] args, int startIndex, out bool relativeEmission)
        {
            relativeEmission = true;
            if (args == null || startIndex >= args.Length)
            {
                return true;
            }

            string token = args[startIndex]?.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }

            if (string.Equals(token, "relative", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "rel", StringComparison.OrdinalIgnoreCase))
            {
                relativeEmission = true;
                return true;
            }

            if (string.Equals(token, "absolute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "abs", StringComparison.OrdinalIgnoreCase))
            {
                relativeEmission = false;
                return true;
            }

            return false;
        }

        private void ClearAnimationDisplayerFollowAnimations()
        {
            foreach (KeyValuePair<int, int> entry in _animationDisplayerFollowAnimationIds)
            {
                _animationEffects.RemoveFollow(entry.Value);
            }

            _animationDisplayerFollowAnimationIds.Clear();
            _packetOwnedAnimationDisplayerFollowDriverId = 0;
        }

        private void ClearAnimationDisplayerFollow(int ownerCharacterId)
        {
            ClearAnimationDisplayerFollowRegistration(ownerCharacterId);
        }

        private void ClearAnimationDisplayerFollowRegistration(int registrationKey)
        {
            if (registrationKey == 0)
            {
                return;
            }

            if (_animationDisplayerFollowAnimationIds.TryGetValue(registrationKey, out int followId))
            {
                _animationEffects.RemoveFollow(followId);
                _animationDisplayerFollowAnimationIds.Remove(registrationKey);
            }

            if (registrationKey < 0
                && (registrationKey & AnimationDisplayerPacketOwnedFollowRegistrationMask) == AnimationDisplayerPacketOwnedFollowRegistrationMask)
            {
                _packetOwnedAnimationDisplayerFollowDriverId = 0;
            }
        }

        private void SyncPacketOwnedAnimationDisplayerFollow()
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            int registrationKey = BuildAnimationDisplayerPacketOwnedFollowRegistrationKey(localCharacterId);
            if (localCharacterId <= 0 || registrationKey == 0)
            {
                ClearAnimationDisplayerFollowRegistration(registrationKey);
                _packetOwnedAnimationDisplayerFollowDriverId = 0;
                return;
            }

            PlayerCharacter player = _playerManager?.Player;
            if (_localFollowRuntime.AttachedDriverId <= 0 || player == null)
            {
                ClearAnimationDisplayerFollowRegistration(registrationKey);
                _packetOwnedAnimationDisplayerFollowDriverId = 0;
                return;
            }

            int attachedDriverId = _localFollowRuntime.AttachedDriverId;
            if (_packetOwnedAnimationDisplayerFollowDriverId == attachedDriverId
                && _animationDisplayerFollowAnimationIds.ContainsKey(registrationKey))
            {
                return;
            }

            if (TryRegisterAnimationDisplayerFollow(
                registrationKey,
                localCharacterId,
                () => _playerManager?.Player?.Position ?? player.Position,
                relativeEmission: true))
            {
                _packetOwnedAnimationDisplayerFollowDriverId = attachedDriverId;
            }
        }

        private void ClearAnimationDisplayerLocalQuestDeliveryOwner()
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (localCharacterId > 0 && _animationDisplayerLocalQuestDeliveryItemId > 0)
            {
                _animationEffects.RemoveUserStateByRegistrationKey(
                    BuildAnimationDisplayerQuestDeliveryRegistrationKey(localCharacterId),
                    currTickCount);
            }

            _animationDisplayerLocalQuestDeliveryItemId = 0;
        }

        private void HandleAnimationDisplayerOverlayClose(NpcInteractionOverlayCloseKind closeKind)
        {
            if (closeKind != NpcInteractionOverlayCloseKind.None)
            {
                ClearAnimationDisplayerLocalQuestDeliveryOwner();
            }
        }

        private bool TryResolveAnimationDisplayerOwner(
            string ownerToken,
            out int ownerCharacterId,
            out Func<Vector2> getPosition,
            out string ownerName)
        {
            ownerCharacterId = 0;
            getPosition = null;
            ownerName = "unknown";

            if (string.Equals(ownerToken, "local", StringComparison.OrdinalIgnoreCase))
            {
                PlayerCharacter player = _playerManager?.Player;
                if (player?.Build?.Id > 0)
                {
                    ownerCharacterId = player.Build.Id;
                    ownerName = player.Build.Name ?? $"Character {ownerCharacterId}";
                    getPosition = () => _playerManager?.Player?.Position ?? player.Position;
                    return true;
                }

                return false;
            }

            if (!int.TryParse(ownerToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int characterId)
                || _remoteUserPool?.TryGetActor(characterId, out RemoteUserActor actor) != true
                || actor == null)
            {
                return false;
            }

            ownerCharacterId = characterId;
            ownerName = string.IsNullOrWhiteSpace(actor.Name) ? $"Character {characterId}" : actor.Name.Trim();
            getPosition = () =>
            {
                if (_remoteUserPool?.TryGetActor(characterId, out RemoteUserActor liveActor) == true && liveActor != null)
                {
                    return liveActor.Position;
                }

                return actor.Position;
            };
            return true;
        }

        private Func<bool> ResolveAnimationDisplayerFollowOwnerFlip(int ownerCharacterId)
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (ownerCharacterId > 0 && ownerCharacterId == localCharacterId)
            {
                return () => !(_playerManager?.Player?.FacingRight ?? true);
            }

            if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor actor) == true && actor != null)
            {
                return () =>
                {
                    if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor liveActor) == true && liveActor != null)
                    {
                        return !liveActor.FacingRight;
                    }

                    return !actor.FacingRight;
                };
            }

            return null;
        }

        private Func<bool> ResolveAnimationDisplayerFollowOwnerMoveAction(int ownerCharacterId)
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (ownerCharacterId > 0 && ownerCharacterId == localCharacterId)
            {
                return () => IsAnimationDisplayerFollowGenOnMoveActionName(_playerManager?.Player?.CurrentActionName);
            }

            if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor actor) == true && actor != null)
            {
                return () =>
                {
                    if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor liveActor) == true && liveActor != null)
                    {
                        return IsAnimationDisplayerFollowGenOnMoveActionName(liveActor.ActionName);
                    }

                    return IsAnimationDisplayerFollowGenOnMoveActionName(actor.ActionName);
                };
            }

            return null;
        }

        internal static bool IsAnimationDisplayerFollowGenOnMoveActionName(string actionName)
        {
            return CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                && IsAnimationDisplayerFollowGenOnMoveRawActionCode(rawActionCode);
        }

        internal static bool IsAnimationDisplayerFollowGenOnMoveRawActionCode(int rawActionCode)
        {
            return rawActionCode is 0 or 1 or 43 or 44;
        }

        private sealed class DelayAdjustedDxObject : IDXObject
        {
            private readonly IDXObject _inner;
            private readonly int _delay;

            public DelayAdjustedDxObject(IDXObject inner, int delay)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _delay = delay;
            }

            public void DrawObject(
                Microsoft.Xna.Framework.Graphics.SpriteBatch sprite,
                Spine.SkeletonMeshRenderer meshRenderer,
                Microsoft.Xna.Framework.GameTime gameTime,
                int mapShiftX,
                int mapShiftY,
                bool flip,
                ReflectionDrawableBoundary drawReflectionInfo)
            {
                _inner.DrawObject(sprite, meshRenderer, gameTime, mapShiftX, mapShiftY, flip, drawReflectionInfo);
            }

            public void DrawBackground(
                Microsoft.Xna.Framework.Graphics.SpriteBatch sprite,
                Spine.SkeletonMeshRenderer meshRenderer,
                Microsoft.Xna.Framework.GameTime gameTime,
                int x,
                int y,
                Microsoft.Xna.Framework.Color color,
                bool flip,
                ReflectionDrawableBoundary drawReflectionInfo)
            {
                _inner.DrawBackground(sprite, meshRenderer, gameTime, x, y, color, flip, drawReflectionInfo);
            }

            public int Delay => _delay;
            public int X => _inner.X;
            public int Y => _inner.Y;
            public int Width => _inner.Width;
            public int Height => _inner.Height;
            public object Tag { get => _inner.Tag; set => _inner.Tag = value; }
            public Microsoft.Xna.Framework.Graphics.Texture2D Texture => _inner.Texture;
        }
    }
}
