using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Combat;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
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
using System.Linq;
using MapleLib.WzLib.WzStructure.Data;

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
        private const string AnimationDisplayerMonsterBookCardGetEffectUol = "Effect/BasicEff.img/MonsterBook/cardGet";
        private const string AnimationDisplayerBuffItemUseFallbackEffectUol = "Effect/BasicEff.img/Buff";
        private const string AnimationDisplayerItemUnreleaseBaseEffectUol = "Effect/BasicEff.img/Enchant/Success";
        private const string AnimationDisplayerRedCombatFeedbackEffectBaseUol = "Effect/BasicEff.img/NoRed0";
        private const string AnimationDisplayerVioletCombatFeedbackEffectBaseUol = "Effect/BasicEff.img/NoViolet0";
        private const string AnimationDisplayerCatchEffectBaseUol = "Effect/BasicEff.img/Catch";
        private const int AnimationDisplayerCatchMobHeadVerticalOffset = 15;
        private const int AnimationDisplayerMobSwallowTargetVerticalOffset = 40;
        private const int AnimationDisplayerMobSwallowTargetHorizontalOffset = 70;
        private const string AnimationDisplayerMobSwallowPrimaryAction = "attack1";
        private const int AnimationDisplayerCoolEffectStringPoolId = 0x14E3;
        private const string AnimationDisplayerCoolEffectFallbackUol = "Effect/BasicEff.img/CoolHit/cool";
        private const string AnimationDisplayerSquibEffectUol = "Effect/BasicEff.img/Flame/SquibEffect";
        private const string AnimationDisplayerSquibEffect2Uol = "Effect/BasicEff.img/Flame/SquibEffect2";
        private const string AnimationDisplayerTransformEffectUol = "Effect/BasicEff.img/Transform";
        private const string AnimationDisplayerTransformOnLadderEffectUol = "Effect/BasicEff.img/TransformOnLadder";
        private const int AnimationDisplayerSessionValueCoolKeyStringPoolId = 0x14F1;
        private const string AnimationDisplayerSessionValueCoolFallbackKey = "massacre_cool";
        private const int AnimationDisplayerReservedRemoteUtilityActionRestoreFallbackDurationMs = 1200;
        private const int AnimationDisplayerFallingFallbackDurationMs = 1000;
        private const int AnimationDisplayerExplosionFallbackIntervalMs = 100;
        private const int AnimationDisplayerExplosionFallbackCount = 1;
        private const int AnimationDisplayerExplosionFallbackDurationMs = 1000;
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
        private const int AnimationDisplayerFollowDefaultEmissionLeft = -25;
        private const int AnimationDisplayerFollowDefaultEmissionTop = -25;
        private const int AnimationDisplayerFollowDefaultEmissionRight = 25;
        private const int AnimationDisplayerFollowDefaultEmissionBottom = 25;
        private const int AnimationDisplayerFollowAbsoluteTravelOffsetY = -20;
        private const int AnimationDisplayerFollowEmissionVerticalBias = 10;
        private const int AnimationDisplayerLadderRawActionCode = 31;
        private const int AnimationDisplayerRopeRawActionCode = 32;
        private const int AnimationDisplayerLadder2RawActionCode = 43;
        private const int AnimationDisplayerRope2RawActionCode = 44;
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
        private static readonly EquipSlot[] AnimationDisplayerFollowCandidateEquipSlots =
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
            EquipSlot.TamingMob
        };

        private readonly Dictionary<string, List<IDXObject>> _animationDisplayerEffectCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<List<IDXObject>>> _animationDisplayerSkillUseEffectCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<AnimationDisplayerSkillUseAvatarEffectVariant>> _animationDisplayerSkillUseAvatarEffectCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(int ItemId, EquipSlot Slot, int ClientEquipIndex), AnimationDisplayerFollowEquipmentDefinition> _animationDisplayerFollowEquipmentCache = new();
        private readonly Dictionary<int, List<AnimationDisplayerFollowRegistrationEntry>> _animationDisplayerFollowAnimationIds = new();
        private readonly Dictionary<int, int> _animationDisplayerFollowRegistrationSignatures = new();
        private readonly Dictionary<int, AnimationDisplayerRemoteGenericUserStateOwnerState> _animationDisplayerRemoteGenericUserStateOwnerStates = new();
        private readonly Dictionary<int, AnimationDisplayerRemoteMakerSkillOwnerState> _animationDisplayerRemoteMakerSkillOwnerStates = new();
        private readonly Dictionary<int, AnimationDisplayerRemoteItemMakeOwnerState> _animationDisplayerRemoteItemMakeOwnerStates = new();
        private readonly Dictionary<int, AnimationDisplayerRemoteUpgradeTombOwnerState> _animationDisplayerRemoteUpgradeTombOwnerStates = new();
        private readonly Dictionary<int, Dictionary<string, AnimationDisplayerRemotePacketOwnedStringEffectOwnerState>> _animationDisplayerRemotePacketOwnedStringEffectOwnerStates = new();
        private readonly Dictionary<int, AnimationDisplayerRemoteMobAttackHitOwnerState> _animationDisplayerRemoteMobAttackHitOwnerStates = new();
        private readonly Dictionary<int, AnimationDisplayerReservedRemoteUtilityActionOwnerState> _animationDisplayerReservedRemoteUtilityActionOwnerStates = new();
        private readonly Dictionary<int, Dictionary<string, AnimationDisplayerRemoteHookingChainOwnerState>> _animationDisplayerRemoteHookingChainOwnerStates = new();
        private readonly Dictionary<int, Dictionary<string, AnimationDisplayerRemoteSkillUseOwnerState>> _animationDisplayerRemoteSkillUseOwnerStates = new();
        private readonly Dictionary<int, Dictionary<int, AnimationDisplayerPacketOwnedMonsterBookCardGetOwnerState>> _animationDisplayerPacketOwnedMonsterBookCardGetOwnerStates = new();
        private readonly Dictionary<int, Dictionary<string, AnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerState>> _animationDisplayerLocalPacketOwnedBasicOneTimeOwnerStates = new();
        private readonly Dictionary<string, AnimationDisplayerPacketOwnedMobOneTimeOwnerState> _animationDisplayerPacketOwnedMobOneTimeOwnerStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> _packetOwnedAnimationDisplayerAreaAnimationIds = new();
        private readonly List<AnimationDisplayerPendingReservedOwnerEffect> _animationDisplayerPendingReservedOwnerEffects = new();
        private readonly List<AnimationDisplayerRemoteGrenadeActor> _animationDisplayerRemoteGrenadeActors = new();
        private int _animationDisplayerLocalQuestDeliveryItemId;
        private int _animationDisplayerSessionValueCoolRank;
        private int _packetOwnedAnimationDisplayerFollowDriverId;
        private int _packetOwnedAnimationDisplayerFollowRegistrationKey;

        internal enum AnimationDisplayerTransientEffectKind
        {
            None = 0,
            NewYear = 1,
            FireCracker = 2
        }

        internal enum AnimationDisplayerReservedType5SoundOwnerKind
        {
            None = 0,
            BgmOverride = 1,
            SoundEffect = 2
        }

        private sealed class AnimationDisplayerSkillUseAvatarEffectVariant
        {
            public SkillAnimation OverlayAnimation { get; init; }
            public SkillAnimation UnderFaceAnimation { get; init; }

            public bool HasAvatarAnimations => OverlayAnimation != null || UnderFaceAnimation != null;
        }

        private sealed class AnimationDisplayerRemoteGrenadeActor
        {
            public int CharacterId { get; init; }
            public int SkillId { get; init; }
            public int StartTime { get; init; }
            public int FlightStartTime { get; init; }
            public int ExplosionTime { get; init; }
            public Vector2 Origin { get; init; }
            public Vector2 FlightOrigin { get; init; }
            public Vector2 Impact { get; init; }
            public Vector2 FlightRenderOffset { get; init; }
            public Vector2 ExplosionRenderOffset { get; init; }
            public int DragX { get; init; }
            public int DragY { get; init; }
            public bool GravityFree { get; init; }
            public bool FacingRight { get; init; }
            public SkillAnimation BallAnimation { get; init; }
            public SkillAnimation ExplosionAnimation { get; init; }
            public int ExpireTime { get; init; }

            public bool IsExploding(int currentTime) => ExplosionAnimation != null && currentTime >= ExplosionTime;
            public bool IsExpired(int currentTime) => currentTime >= ExpireTime;
        }

        private sealed class AnimationDisplayerRemotePacketOwnedStringEffectOwnerState
        {
            public byte EffectType { get; init; }
            public string EffectUol { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerRemoteMobAttackHitOwnerState
        {
            public int MobTemplateId { get; init; }
            public sbyte AttackIndex { get; init; }
            public string EffectUol { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public bool AttachToOwner { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerRemoteItemMakeOwnerState
        {
            public bool Success { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerRemoteMakerSkillOwnerState
        {
            public string EffectUol { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerRemoteUpgradeTombOwnerState
        {
            public int ItemId { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerRemoteGenericUserStateOwnerState
        {
            public string EffectUol { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerReservedRemoteUtilityActionOwnerState
        {
            public string ReservedActionName { get; init; }
            public string PreviousActionName { get; init; }
            public bool? PreviousFacingRight { get; init; }
            public int RestoreAtTime { get; init; }
        }

        private sealed class AnimationDisplayerRemoteHookingChainOwnerState
        {
            public int SkillId { get; init; }
            public int MobObjectId { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerRemoteSkillUseOwnerState
        {
            public int SkillId { get; init; }
            public string BranchName { get; init; }
            public int VariantIndex { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerPacketOwnedMonsterBookCardGetOwnerState
        {
            public int ItemId { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerState
        {
            public string EffectUol { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerPacketOwnedMobOneTimeOwnerState
        {
            public string OwnerKind { get; init; }
            public int MobTemplateId { get; init; }
            public string AttackAction { get; init; }
            public string EffectUol { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int AnimationStartTime { get; init; }
            public int DurationMs { get; init; }
        }

        private sealed class AnimationDisplayerPendingReservedOwnerEffect
        {
            public RemoteUserActorPool.RemoteStringEffectPresentation Presentation { get; init; }
            public Func<Vector2> GetPosition { get; init; }
            public string SourceEffectUol { get; init; }
            public AnimationDisplayerReservedEffectMetadata Metadata { get; init; }
            public int RegisterTime { get; init; }
            public AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext OwnerContext { get; init; }
        }

        private readonly record struct AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext(
            int CharacterId,
            byte EffectType,
            string EffectUol,
            string OwnerActionName,
            bool OwnerFacingRight,
            int CurrentTime);

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
            public EquipSlot SourceEquipSlot { get; init; }
            public int ClientEquipIndex { get; init; }
            public IReadOnlyList<int> EffectVariantIndices { get; init; }
        }

        private sealed class AnimationDisplayerFollowRegistrationEntry
        {
            public int DefinitionKey { get; init; }
            public int FollowId { get; init; }
        }

        internal readonly record struct AnimationDisplayerFollowEquipmentCandidate(
            int ItemId,
            EquipSlot Slot,
            int ClientEquipIndex,
            bool IsHidden);

        private readonly record struct AnimationDisplayerResolvedFollowEquipmentEntry(
            AnimationDisplayerFollowEquipmentDefinition Definition,
            int CandidateIdentity);

        private readonly record struct AnimationDisplayerSkillUseBranchRequest(
            string BranchName,
            Point OriginOffset,
            bool FollowOwnerFacing = true,
            bool FollowOwnerPosition = true,
            bool? FacingRightOverride = null);

        internal readonly record struct AnimationDisplayerFallingRegistrationProfile(
            Rectangle StartArea,
            int OffsetX,
            int OffsetY,
            int Alpha,
            int FallDistance,
            int UpdateIntervalMs,
            int UpdateCount,
            int UpdateNextMs,
            int DurationMs,
            int FrameCount)
        {
            public int EffectiveFallDistance => Math.Max(1, FallDistance);
            public int EffectiveUpdateCount => Math.Max(1, UpdateCount > 0 ? UpdateCount : FrameCount);
            public int EffectiveDurationMs => Math.Max(120, DurationMs > 0 ? DurationMs : AnimationDisplayerFallingFallbackDurationMs);
            public float EffectiveFallSpeed => EffectiveFallDistance * 1000f / EffectiveDurationMs;
            public byte EffectiveAlpha => (byte)Math.Clamp(Alpha > 0 ? Alpha : byte.MaxValue, byte.MinValue, byte.MaxValue);
        }

        internal readonly record struct AnimationDisplayerAreaRegistrationProfile(
            Rectangle Area,
            int OffsetX,
            int OffsetY,
            int UpdateIntervalMs,
            int UpdateCount,
            int UpdateNextMs,
            int DurationMs,
            int? LayerZ)
        {
            public Rectangle EffectiveArea => new(
                Area.X + OffsetX,
                Area.Y + OffsetY,
                Area.Width,
                Area.Height);
            public int EffectiveUpdateIntervalMs => Math.Max(1, UpdateIntervalMs > 0 ? UpdateIntervalMs : AnimationDisplayerExplosionFallbackIntervalMs);
            public int EffectiveUpdateCount => Math.Max(1, UpdateCount > 0 ? UpdateCount : AnimationDisplayerExplosionFallbackCount);
            public int EffectiveUpdateNextMs => Math.Max(0, UpdateNextMs);
            public int EffectiveDurationMs => Math.Max(1, DurationMs > 0 ? DurationMs : AnimationDisplayerExplosionFallbackDurationMs);
            public int EffectiveLayerZ => LayerZ ?? 1;
        }

        internal readonly record struct AnimationDisplayerReservedEffectMetadata(
            int Type,
            int StartDelayMs,
            string VisualEffectUol,
            string SoundEffectDescriptor,
            int FieldId,
            string ActionName,
            int EmotionId,
            int? PositionX,
            int? PositionY,
            int OffsetX,
            int OffsetY,
            int RelativeOffsetX,
            int RelativeOffsetY,
            int Width,
            int Height,
            int DurationMs,
            float Probability,
            int LayerZ,
            IReadOnlyList<int> EquippedItemIds);

        private void RegisterAnimationDisplayerChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "socialanim",
                "Exercise the shared social or event animation-displayer runtime",
                "/socialanim <status|clear|newyear|firecracker|guard|miss|catch|cool|squib|transformed|buffitem|itemunrelease|falling|explosion|userstate|follow|questdelivery> [...]",
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

            if (string.Equals(args[0], "guard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "miss", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "shot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "counter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "resist", StringComparison.OrdinalIgnoreCase))
            {
                string specialTextName = DamageNumberRenderer.ResolveSpecialTextName(args[0]);
                if (!TryResolveAnimationDisplayerOwner(
                        args.Length > 1 ? args[1] : "local",
                        out int ownerCharacterId,
                        out Func<Vector2> getPosition,
                        out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error($"Could not resolve {specialTextName} animation owner.");
                }

                if (!TryResolveAnimationDisplayerCombatFeedbackColor(
                        args.Length > 2 ? args[2] : null,
                        out DamageColorType colorType))
                {
                    return ChatCommandHandler.CommandResult.Error($"Usage: /socialanim {args[0]} [local|characterId] [red|blue|violet|0|1|2]");
                }

                return TryRegisterAnimationDisplayerCombatFeedback(
                    specialTextName,
                    ownerCharacterId,
                    getPosition,
                    currTickCount,
                    colorType,
                    out string message)
                    ? ChatCommandHandler.CommandResult.Ok($"{message} Owner={ownerName}.")
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "catch", StringComparison.OrdinalIgnoreCase))
            {
                bool success = true;
                if (args.Length > 1)
                {
                    if (string.Equals(args[1], "success", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(args[1], "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        success = true;
                    }
                    else if (string.Equals(args[1], "fail", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(args[1], "failure", StringComparison.OrdinalIgnoreCase))
                    {
                        success = false;
                    }
                    else
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /socialanim catch <success|fail> [local|characterId]");
                    }
                }

                if (!TryResolveAnimationDisplayerOwner(
                        args.Length > 2 ? args[2] : "local",
                        out int ownerCharacterId,
                        out Func<Vector2> getPosition,
                        out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error("Could not resolve catch animation owner.");
                }

                return TryRegisterAnimationDisplayerCatch(
                    success,
                    ownerCharacterId,
                    getPosition,
                    currTickCount,
                    requestedEffectUol: null,
                    packetOwnedOwnerContext: null,
                    out string message)
                    ? ChatCommandHandler.CommandResult.Ok($"{message} Owner={ownerName}.")
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "cool", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryResolveAnimationDisplayerOwner(
                        args.Length > 1 ? args[1] : "local",
                        out int ownerCharacterId,
                        out Func<Vector2> getPosition,
                        out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error("Could not resolve cooldown animation owner.");
                }

                return TryRegisterAnimationDisplayerCool(
                    ownerCharacterId,
                    getPosition,
                    currTickCount,
                    out string message)
                    ? ChatCommandHandler.CommandResult.Ok($"{message} Owner={ownerName}.")
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "squib", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryResolveAnimationDisplayerOwner(
                        args.Length > 1 ? args[1] : "local",
                        out int ownerCharacterId,
                        out Func<Vector2> getPosition,
                        out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error("Could not resolve squib animation owner.");
                }

                int variant = 1;
                if (args.Length > 2)
                {
                    if (!int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out variant)
                        || variant < 1
                        || variant > 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /socialanim squib [local|characterId] [1|2]");
                    }
                }

                return TryRegisterAnimationDisplayerSquib(
                    ownerCharacterId,
                    getPosition,
                    variant,
                    currTickCount,
                    packetOwnedOwnerContext: null,
                    out string message)
                    ? ChatCommandHandler.CommandResult.Ok($"{message} Owner={ownerName}.")
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "transformed", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryResolveAnimationDisplayerOwner(
                        args.Length > 1 ? args[1] : "local",
                        out int ownerCharacterId,
                        out Func<Vector2> getPosition,
                        out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error("Could not resolve transformed animation owner.");
                }

                bool onLadder = args.Length > 2
                    && string.Equals(args[2], "ladder", StringComparison.OrdinalIgnoreCase);
                if (args.Length > 2
                    && !onLadder
                    && !string.Equals(args[2], "normal", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim transformed [local|characterId] [normal|ladder]");
                }

                return TryRegisterAnimationDisplayerTransformed(
                    ownerCharacterId,
                    getPosition,
                    onLadder,
                    currTickCount,
                    packetOwnedOwnerContext: null,
                    out string message)
                    ? ChatCommandHandler.CommandResult.Ok($"{message} Owner={ownerName}.")
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "buffitem", StringComparison.OrdinalIgnoreCase))
            {
                string requestedEffectUol = args.Length > 1 ? args[1] : null;
                if (!TryResolveAnimationDisplayerOwner(
                        args.Length > 2 ? args[2] : "local",
                        out int ownerCharacterId,
                        out Func<Vector2> getPosition,
                        out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error("Could not resolve buff-item-use animation owner.");
                }

                return TryRegisterAnimationDisplayerBuffItemUse(
                    requestedEffectUol,
                    ownerCharacterId,
                    getPosition,
                    out string message)
                    ? ChatCommandHandler.CommandResult.Ok($"{message} Owner={ownerName}.")
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "itemunrelease", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryResolveAnimationDisplayerOwner(
                        args.Length > 1 ? args[1] : "local",
                        out int ownerCharacterId,
                        out Func<Vector2> getPosition,
                        out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error("Could not resolve item-unrelease animation owner.");
                }

                return TryRegisterAnimationDisplayerItemUnrelease(
                    ownerCharacterId,
                    getPosition,
                    out string message)
                    ? ChatCommandHandler.CommandResult.Ok($"{message} Owner={ownerName}.")
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "falling", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim falling <effectUol> [x y width height]");
                }

                Rectangle area = ResolveAnimationDisplayerArea(args, startIndex: 2);
                return TryRegisterAnimationDisplayerFallingAnimation(args[1], area, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "explosion", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim explosion <effectUol> [x y width height]");
                }

                Rectangle area = ResolveAnimationDisplayerArea(args, startIndex: 2);
                return TryRegisterAnimationDisplayerExplosionAnimation(args[1], area, out string message)
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

            return ChatCommandHandler.CommandResult.Error("Usage: /socialanim <status|clear|newyear|firecracker|guard|miss|catch|cool|squib|transformed|buffitem|itemunrelease|falling|explosion|userstate|follow|questdelivery> [...]");
        }

        private string DescribeAnimationDisplayerStatus()
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            bool localUserStateActive = localCharacterId > 0 && _animationEffects.HasUserState(localCharacterId);
            string localQuestDelivery = _animationDisplayerLocalQuestDeliveryItemId > 0
                ? _animationDisplayerLocalQuestDeliveryItemId.ToString(CultureInfo.InvariantCulture)
                : "idle";
            bool packetOwnedFollowActive = localCharacterId > 0
                && HasAnimationDisplayerFollowRegistration(BuildAnimationDisplayerPacketOwnedFollowRegistrationKey(localCharacterId));
            return $"Animation displayer parity: userStates={_animationEffects.UserStateCount}, followAnimations={_animationEffects.FollowAnimationCount}, fallingAnimations={_animationEffects.FallingAnimationCount}, areaAnimations={_animationEffects.AreaAnimationCount}, secondaryOwners={_animationEffects.SecondarySkillAnimationOwnerCount}, localUserState={(localUserStateActive ? "active" : "idle")}, localQuestDelivery={localQuestDelivery}, packetOwnedFollow={(packetOwnedFollowActive ? "active" : "idle")}, localFade={(_packetOwnedFieldFadeOverlay.IsActive ? "active" : "idle")}.";
        }

        private void ClearAnimationDisplayerState()
        {
            _animationEffects.ClearUserStates();
            _animationEffects.ClearAreaAnimations();
            _animationEffects.ClearSecondarySkillAnimationOwners();
            ClearAnimationDisplayerFollowAnimations();
            _animationDisplayerRemoteGenericUserStateOwnerStates.Clear();
            _animationDisplayerRemoteMakerSkillOwnerStates.Clear();
            _animationDisplayerRemoteItemMakeOwnerStates.Clear();
            _animationDisplayerRemoteUpgradeTombOwnerStates.Clear();
            _animationDisplayerRemotePacketOwnedStringEffectOwnerStates.Clear();
            _animationDisplayerRemoteMobAttackHitOwnerStates.Clear();
            _animationDisplayerReservedRemoteUtilityActionOwnerStates.Clear();
            _animationDisplayerRemoteHookingChainOwnerStates.Clear();
            _animationDisplayerRemoteSkillUseOwnerStates.Clear();
            _animationDisplayerPacketOwnedMonsterBookCardGetOwnerStates.Clear();
            _animationDisplayerLocalPacketOwnedBasicOneTimeOwnerStates.Clear();
            _animationDisplayerPacketOwnedMobOneTimeOwnerStates.Clear();
            _packetOwnedAnimationDisplayerAreaAnimationIds.Clear();
            _animationDisplayerPendingReservedOwnerEffects.Clear();
            _animationDisplayerRemoteGrenadeActors.Clear();
            _animationDisplayerLocalQuestDeliveryItemId = 0;
            _animationDisplayerSessionValueCoolRank = 0;
            _packetOwnedAnimationDisplayerFollowDriverId = 0;
            _packetOwnedAnimationDisplayerFollowRegistrationKey = 0;
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

        private bool TryRegisterAnimationDisplayerBuffItemUse(
            string requestedEffectUol,
            int ownerCharacterId,
            Func<Vector2> getPosition,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                message = "Buff-item-use animation owner is missing.";
                return false;
            }

            string resolvedEffectUol = ResolveAnimationDisplayerBuffItemUseEffectUol(
                requestedEffectUol,
                effectUol => ResolveAnimationDisplayerProperty(effectUol) != null);
            if (!TryGetAnimationDisplayerFrames($"buffitem:{resolvedEffectUol}", resolvedEffectUol, out List<IDXObject> frames))
            {
                message = $"Buff-item-use animation frames could not be loaded from {resolvedEffectUol}.";
                return false;
            }

            Vector2 fallbackPosition = getPosition();
            string ownerActionName = ResolveAnimationDisplayerLocalPacketOwnedActionName(ownerCharacterId);
            bool ownerFacingRight = ResolveAnimationDisplayerLocalPacketOwnedFacingRight(ownerCharacterId);
            int initialElapsedMs = ResolveAnimationDisplayerLocalPacketOwnedBasicOneTimeInitialElapsed(
                ownerCharacterId,
                BuildAnimationDisplayerLocalPacketOwnedBuffItemUseOwnerSlotKey(resolvedEffectUol),
                resolvedEffectUol,
                ownerActionName,
                ownerFacingRight,
                currTickCount,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            _animationEffects.AddPacketOwnedBuffItemUse(
                frames,
                resolvedEffectUol,
                getPosition,
                fallbackPosition.X,
                fallbackPosition.Y,
                currTickCount,
                initialElapsedMs: initialElapsedMs);
            message = $"Registered buff-item-use animation-displayer layer from {resolvedEffectUol}.";
            return true;
        }

        private bool TryRegisterAnimationDisplayerMonsterBookCardPickup(
            int ownerCharacterId,
            Func<Vector2> getPosition,
            int itemId,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                message = "Monster Book card-get animation owner is missing.";
                return false;
            }

            if (!TryGetAnimationDisplayerFrames(
                    "monsterbook:cardget",
                    AnimationDisplayerMonsterBookCardGetEffectUol,
                    out List<IDXObject> frames))
            {
                message = $"Monster Book card-get animation frames could not be loaded from {AnimationDisplayerMonsterBookCardGetEffectUol}.";
                return false;
            }

            Vector2 fallbackPosition = getPosition();
            string ownerActionName = ResolveAnimationDisplayerLocalPacketOwnedActionName(ownerCharacterId);
            bool ownerFacingRight = ResolveAnimationDisplayerLocalPacketOwnedFacingRight(ownerCharacterId);
            int initialElapsedMs = ResolveAnimationDisplayerPacketOwnedMonsterBookCardGetInitialElapsed(
                ownerCharacterId,
                itemId,
                ownerActionName,
                ownerFacingRight,
                currTickCount,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            _animationEffects.AddPacketOwnedMonsterBookCardGet(
                frames,
                AnimationDisplayerMonsterBookCardGetEffectUol,
                getPosition,
                fallbackPosition.X,
                fallbackPosition.Y,
                currTickCount,
                initialElapsedMs: initialElapsedMs);
            message = $"Registered Monster Book card-get animation-displayer layer from {AnimationDisplayerMonsterBookCardGetEffectUol}.";
            return true;
        }

        private bool TryRegisterAnimationDisplayerItemUnrelease(
            int ownerCharacterId,
            Func<Vector2> getPosition,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                message = "Item-unrelease animation owner is missing.";
                return false;
            }

            bool registered = false;
            Vector2 fallbackPosition = getPosition();
            string ownerActionName = ResolveAnimationDisplayerLocalPacketOwnedActionName(ownerCharacterId);
            bool ownerFacingRight = ResolveAnimationDisplayerLocalPacketOwnedFacingRight(ownerCharacterId);
            foreach (string effectUol in EnumerateAnimationDisplayerItemUnreleaseEffectUols(AnimationDisplayerItemUnreleaseBaseEffectUol))
            {
                if (!TryGetAnimationDisplayerFrames($"itemunrelease:{effectUol}", effectUol, out List<IDXObject> frames))
                {
                    continue;
                }

                int initialElapsedMs = ResolveAnimationDisplayerLocalPacketOwnedBasicOneTimeInitialElapsed(
                    ownerCharacterId,
                    BuildAnimationDisplayerLocalPacketOwnedItemUnreleaseOwnerSlotKey(effectUol),
                    effectUol,
                    ownerActionName,
                    ownerFacingRight,
                    currTickCount,
                    ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
                _animationEffects.AddPacketOwnedItemUnrelease(
                    frames,
                    effectUol,
                    getPosition,
                    fallbackPosition.X,
                    fallbackPosition.Y,
                    currTickCount,
                    initialElapsedMs: initialElapsedMs);
                registered = true;
            }

            message = registered
                ? $"Registered item-unrelease animation-displayer layers from {AnimationDisplayerItemUnreleaseBaseEffectUol}."
                : $"Item-unrelease animation frames could not be loaded from {AnimationDisplayerItemUnreleaseBaseEffectUol}.";
            return registered;
        }

        private bool TryRegisterAnimationDisplayerFallingAnimation(string effectUol, Rectangle area, out string message)
        {
            message = null;
            WzImageProperty property = ResolveAnimationDisplayerProperty(effectUol)?.GetLinkedWzImageProperty();
            if (property == null)
            {
                message = $"Falling animation property could not be loaded from {effectUol}.";
                return false;
            }

            List<IDXObject> frames = LoadAnimationDisplayerFrames(effectUol);
            if (!Animation.AnimationEffects.HasFrames(frames))
            {
                message = $"Falling animation frames could not be loaded from {effectUol}.";
                return false;
            }

            AnimationDisplayerFallingRegistrationProfile profile = BuildAnimationDisplayerFallingRegistrationProfile(
                area,
                ReadAnimationDisplayerIntProperty(property, "x"),
                ReadAnimationDisplayerIntProperty(property, "y"),
                ReadAnimationDisplayerIntProperty(property, "a"),
                ReadAnimationDisplayerIntProperty(property, "fall"),
                ReadAnimationDisplayerIntProperty(property, "interval"),
                ReadAnimationDisplayerIntProperty(property, "count"),
                ReadAnimationDisplayerIntProperty(property, "delay"),
                ReadAnimationDisplayerIntProperty(property, "end"),
                frames.Count);
            float centerX = profile.StartArea.Left + (profile.StartArea.Width / 2f) + profile.OffsetX;
            float endY = profile.StartArea.Bottom + profile.OffsetY;
            float startY = endY - profile.EffectiveFallDistance;
            _animationEffects.AddPacketOwnedFallingBurst(
                frames,
                effectUol,
                centerX,
                startY,
                endY,
                Math.Max(1f, profile.StartArea.Width / 2f),
                profile.EffectiveUpdateCount,
                Math.Max(120f, profile.EffectiveFallSpeed),
                currTickCount + profile.UpdateNextMs,
                profile.EffectiveAlpha);
            message = $"Registered falling animation-displayer effect from {effectUol} ({profile.EffectiveUpdateCount} drops).";
            return true;
        }

        private bool TryRegisterAnimationDisplayerExplosionAnimation(string effectUol, Rectangle area, out string message)
        {
            message = null;
            WzImageProperty property = ResolveAnimationDisplayerProperty(effectUol)?.GetLinkedWzImageProperty();
            if (property == null)
            {
                message = $"Explosion animation property could not be loaded from {effectUol}.";
                return false;
            }

            List<IDXObject> frames = LoadAnimationDisplayerFrames(effectUol);
            if (!Animation.AnimationEffects.HasFrames(frames))
            {
                message = $"Explosion animation frames could not be loaded from {effectUol}.";
                return false;
            }

            AnimationDisplayerAreaRegistrationProfile profile = BuildAnimationDisplayerAreaRegistrationProfile(
                area,
                ReadAnimationDisplayerIntProperty(property, "x"),
                ReadAnimationDisplayerIntProperty(property, "y"),
                ReadAnimationDisplayerIntProperty(property, "interval"),
                ReadAnimationDisplayerIntProperty(property, "count"),
                ReadAnimationDisplayerIntProperty(property, "delay"),
                ReadAnimationDisplayerIntProperty(property, "end"),
                ReadAnimationDisplayerNullableIntProperty(property, "z"));
            int registrationId = _animationEffects.RegisterPacketOwnedAreaAnimation(
                frames,
                effectUol,
                profile.EffectiveArea,
                profile.EffectiveUpdateIntervalMs,
                profile.EffectiveUpdateCount,
                profile.EffectiveUpdateNextMs,
                profile.EffectiveDurationMs,
                currTickCount,
                zOrder: profile.EffectiveLayerZ);
            if (registrationId < 0)
            {
                message = $"Explosion animation-displayer effect from {effectUol} could not be registered.";
                return false;
            }

            _packetOwnedAnimationDisplayerAreaAnimationIds.Add(registrationId);
            message = $"Registered explosion animation-displayer area effect from {effectUol}.";
            return true;
        }

        private void HandleAnimationDisplayerCombatFeedbackRequested(
            string specialTextName,
            float x,
            float y,
            int currentTime,
            DamageColorType colorType)
        {
            if (_animationEffects == null)
            {
                return;
            }

            if (!DamageNumberRenderer.IsSupportedColorType(colorType))
            {
                return;
            }

            string resolvedSpecialTextName = DamageNumberRenderer.ResolveSpecialTextName(specialTextName);
            if (!DamageNumberRenderer.IsSupportedSpecialTextName(resolvedSpecialTextName))
            {
                return;
            }

            TryRegisterAnimationDisplayerCombatFeedback(
                resolvedSpecialTextName,
                _playerManager?.Player?.Build?.Id ?? 1,
                () => new Vector2(x, y),
                currentTime,
                colorType,
                out _);
        }

        private bool TryRegisterAnimationDisplayerCombatFeedback(
            string specialTextName,
            int ownerCharacterId,
            Func<Vector2> getPosition,
            int currentTime,
            DamageColorType colorType,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                message = "Combat-feedback animation owner is missing.";
                return false;
            }

            if (!DamageNumberRenderer.IsSupportedColorType(colorType))
            {
                message = $"Unsupported combat-feedback color type {(int)colorType}.";
                return false;
            }

            string resolvedSpecialTextName = DamageNumberRenderer.ResolveSpecialTextName(specialTextName);
            string effectUol = ResolveAnimationDisplayerCombatFeedbackEffectUol(resolvedSpecialTextName, colorType);
            string cacheKey = ResolveAnimationDisplayerCombatFeedbackFrameCacheKey(resolvedSpecialTextName, colorType);
            if (string.IsNullOrWhiteSpace(effectUol))
            {
                message = $"Unsupported combat-feedback color type {(int)colorType}.";
                return false;
            }

            if (!TryGetAnimationDisplayerFrames(
                    cacheKey,
                    effectUol,
                    out List<IDXObject> frames))
            {
                message = $"Combat-feedback animation frames could not be loaded from {effectUol}.";
                return false;
            }

            Vector2 fallbackPosition = getPosition();
            string ownerActionName = ResolveAnimationDisplayerLocalPacketOwnedActionName(ownerCharacterId);
            bool ownerFacingRight = ResolveAnimationDisplayerLocalPacketOwnedFacingRight(ownerCharacterId);
            int initialElapsedMs = ResolveAnimationDisplayerLocalPacketOwnedBasicOneTimeInitialElapsed(
                ownerCharacterId,
                BuildAnimationDisplayerLocalPacketOwnedCombatFeedbackOwnerSlotKey(effectUol),
                effectUol,
                ownerActionName,
                ownerFacingRight,
                currentTime,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            _animationEffects.AddPacketOwnedCombatFeedback(
                frames,
                effectUol,
                getPosition,
                fallbackPosition.X,
                fallbackPosition.Y,
                currentTime,
                initialElapsedMs: initialElapsedMs);
            message = $"Registered combat-feedback animation-displayer layer from {effectUol}.";
            return true;
        }

        private bool TryRegisterAnimationDisplayerCatch(
            bool success,
            int ownerCharacterId,
            Func<Vector2> getPosition,
            int currentTime,
            string requestedEffectUol,
            AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext? packetOwnedOwnerContext,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                message = "Catch animation owner is missing.";
                return false;
            }

            List<string> candidateEffectUols = EnumerateAnimationDisplayerCatchEffectUolCandidates(
                requestedEffectUol,
                success);
            string effectUol = null;
            List<IDXObject> frames = null;
            for (int i = 0; i < candidateEffectUols.Count; i++)
            {
                string candidateEffectUol = candidateEffectUols[i];
                if (!TryGetAnimationDisplayerFrames($"catch:{candidateEffectUol}", candidateEffectUol, out List<IDXObject> candidateFrames))
                {
                    continue;
                }

                effectUol = candidateEffectUol;
                frames = candidateFrames;
                break;
            }

            if (!Animation.AnimationEffects.HasFrames(frames))
            {
                message = $"Catch animation frames could not be loaded from {ResolveAnimationDisplayerCatchEffectUol(success)}.";
                return false;
            }

            int initialElapsedMs = ResolveAnimationDisplayerRemotePacketOwnedStringEffectInitialElapsed(
                packetOwnedOwnerContext,
                frames);
            Vector2 fallbackPosition = getPosition();
            _animationEffects.AddPacketOwnedCatch(
                frames,
                effectUol,
                getPosition,
                fallbackPosition.X,
                fallbackPosition.Y,
                currentTime,
                initialElapsedMs: initialElapsedMs);
            message = $"Registered catch animation-displayer layer from {effectUol}.";
            return true;
        }

        private bool TryRegisterAnimationDisplayerCool(
            int ownerCharacterId,
            Func<Vector2> getPosition,
            int currentTime,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                message = "Cooldown animation owner is missing.";
                return false;
            }

            string effectUol = ResolveAnimationDisplayerCoolEffectUol();
            if (!TryGetAnimationDisplayerFrames($"cool:{effectUol}", effectUol, out List<IDXObject> frames))
            {
                message = $"Cooldown animation frames could not be loaded from {effectUol}.";
                return false;
            }

            Vector2 fallbackPosition = getPosition();
            string ownerActionName = ResolveAnimationDisplayerLocalPacketOwnedActionName(ownerCharacterId);
            bool ownerFacingRight = ResolveAnimationDisplayerLocalPacketOwnedFacingRight(ownerCharacterId);
            int initialElapsedMs = ResolveAnimationDisplayerLocalPacketOwnedBasicOneTimeInitialElapsed(
                ownerCharacterId,
                BuildAnimationDisplayerLocalPacketOwnedCoolOwnerSlotKey(effectUol),
                effectUol,
                ownerActionName,
                ownerFacingRight,
                currentTime,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            _animationEffects.AddPacketOwnedCool(
                frames,
                effectUol,
                getPosition,
                fallbackPosition.X,
                fallbackPosition.Y,
                currentTime,
                initialElapsedMs: initialElapsedMs);
            message = $"Registered cooldown animation-displayer layer from {effectUol}.";
            return true;
        }

        private bool TryRegisterAnimationDisplayerSquib(
            int ownerCharacterId,
            Func<Vector2> getPosition,
            int variant,
            int currentTime,
            AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext? packetOwnedOwnerContext,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                message = "Squib animation owner is missing.";
                return false;
            }

            string effectUol = ResolveAnimationDisplayerSquibEffectUol(variant);
            string visualEffectUol = ResolveAnimationDisplayerSquibVisualEffectUol(
                effectUol,
                ResolveAnimationDisplayerProperty);
            if (!TryGetAnimationDisplayerFrames(
                    $"squib:{effectUol}:{visualEffectUol}",
                    visualEffectUol,
                    out List<IDXObject> frames))
            {
                message = $"Squib animation frames could not be loaded from {visualEffectUol}.";
                return false;
            }

            int initialElapsedMs = ResolveAnimationDisplayerRemotePacketOwnedStringEffectInitialElapsed(
                packetOwnedOwnerContext,
                frames);
            Vector2 fallbackPosition = getPosition();
            _animationEffects.AddPacketOwnedSquib(
                frames,
                visualEffectUol,
                getPosition,
                fallbackPosition.X,
                fallbackPosition.Y,
                currentTime,
                initialElapsedMs: initialElapsedMs);
            message = $"Registered squib animation-displayer layer from {visualEffectUol}.";
            return true;
        }

        private bool TryRegisterAnimationDisplayerTransformed(
            int ownerCharacterId,
            Func<Vector2> getPosition,
            bool onLadder,
            int currentTime,
            AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext? packetOwnedOwnerContext,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                message = "Transformed animation owner is missing.";
                return false;
            }

            string effectUol = ResolveAnimationDisplayerTransformedEffectUol(onLadder);
            if (!TryGetAnimationDisplayerFrames($"transformed:{effectUol}", effectUol, out List<IDXObject> frames))
            {
                message = $"Transformed animation frames could not be loaded from {effectUol}.";
                return false;
            }

            int initialElapsedMs = ResolveAnimationDisplayerRemotePacketOwnedStringEffectInitialElapsed(
                packetOwnedOwnerContext,
                frames);
            Vector2 fallbackPosition = getPosition();
            _animationEffects.AddPacketOwnedTransformed(
                frames,
                effectUol,
                getPosition,
                fallbackPosition.X,
                fallbackPosition.Y,
                currentTime,
                initialElapsedMs: initialElapsedMs);
            message = $"Registered transformed animation-displayer layer from {effectUol}.";
            return true;
        }

        internal static string ResolveAnimationDisplayerBuffItemUseEffectUol(
            string requestedEffectUol,
            Func<string, bool> effectExists)
        {
            string normalized = NormalizeRemotePacketOwnedStringEffectUol(requestedEffectUol);
            if (!string.IsNullOrWhiteSpace(normalized)
                && (effectExists == null || effectExists(normalized)))
            {
                return normalized;
            }

            return AnimationDisplayerBuffItemUseFallbackEffectUol;
        }

        internal static IReadOnlyList<string> EnumerateAnimationDisplayerItemUnreleaseEffectUols(string baseEffectUol)
        {
            string normalizedBase = NormalizeRemotePacketOwnedStringEffectUol(baseEffectUol)
                                    ?? AnimationDisplayerItemUnreleaseBaseEffectUol;
            return new[]
            {
                CombineAnimationDisplayerEffectUol(normalizedBase, "default"),
                CombineAnimationDisplayerEffectUol(normalizedBase, "0"),
                normalizedBase
            };
        }

        internal static string ResolveAnimationDisplayerCombatFeedbackEffectUol(string specialTextName, DamageColorType colorType)
        {
            if (!DamageNumberRenderer.IsSupportedColorType(colorType))
            {
                return null;
            }

            string resolvedSpecialTextName = DamageNumberRenderer.ResolveSpecialTextName(specialTextName);
            return CombineAnimationDisplayerEffectUol(
                AnimationDisplayerRedCombatFeedbackEffectBaseUol,
                resolvedSpecialTextName);
        }

        internal static string ResolveAnimationDisplayerCombatFeedbackFrameCacheKey(
            string specialTextName,
            DamageColorType colorType)
        {
            string effectUol = ResolveAnimationDisplayerCombatFeedbackEffectUol(specialTextName, colorType);
            return string.IsNullOrWhiteSpace(effectUol)
                ? null
                : $"combatfeedback:{effectUol}";
        }

        internal static string ResolveAnimationDisplayerCatchEffectUol(bool success)
        {
            return CombineAnimationDisplayerEffectUol(
                AnimationDisplayerCatchEffectBaseUol,
                success ? "Success" : "Fail");
        }

        internal static List<string> EnumerateAnimationDisplayerCatchEffectUolCandidates(
            string requestedEffectUol,
            bool success)
        {
            var candidates = new List<string>(capacity: 2);
            string normalizedRequested = NormalizeRemotePacketOwnedStringEffectUol(requestedEffectUol);
            if (!string.IsNullOrWhiteSpace(normalizedRequested))
            {
                candidates.Add(normalizedRequested);
            }

            string fallbackEffectUol = ResolveAnimationDisplayerCatchEffectUol(success);
            if (!string.Equals(normalizedRequested, fallbackEffectUol, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(fallbackEffectUol);
            }

            return candidates;
        }

        internal static bool TryResolveAnimationDisplayerCatchSuccessFromEffectUol(
            string effectUol,
            int? successHint,
            out bool success)
        {
            success = false;
            string normalizedEffectUol = NormalizeRemotePacketOwnedStringEffectUol(effectUol);
            if (string.IsNullOrWhiteSpace(normalizedEffectUol))
            {
                return false;
            }

            int catchIndex = normalizedEffectUol.IndexOf("/Catch", StringComparison.OrdinalIgnoreCase);
            if (catchIndex < 0)
            {
                return false;
            }

            // WZ-backed shape:
            // - Effect/BasicEff.img/Catch/Success/<0..N> animated branch
            // - Effect/BasicEff.img/Catch/Fail single canvas branch
            // Treat bare Catch and numeric Catch children as success-family paths.
            // Client evidence: Effect_Catch takes an explicit bSuccess argument.
            string catchSuffix = normalizedEffectUol[(catchIndex + "/Catch".Length)..].Trim('/');
            bool? resolvedHint = ResolveAnimationDisplayerCatchSuccessHint(successHint);
            if (string.IsNullOrWhiteSpace(catchSuffix))
            {
                success = resolvedHint ?? true;
                return true;
            }

            if (catchSuffix.StartsWith("Success", StringComparison.OrdinalIgnoreCase))
            {
                success = true;
                return true;
            }

            if (catchSuffix.StartsWith("Fail", StringComparison.OrdinalIgnoreCase))
            {
                success = false;
                return true;
            }

            string[] segments = catchSuffix.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0
                && int.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                success = resolvedHint ?? true;
                return true;
            }

            return false;
        }

        internal static bool TryResolveAnimationDisplayerCatchSuccessFromEffectUol(
            string effectUol,
            out bool success)
        {
            return TryResolveAnimationDisplayerCatchSuccessFromEffectUol(effectUol, successHint: null, out success);
        }

        internal static bool? ResolveAnimationDisplayerCatchSuccessHint(int? successHint)
        {
            return successHint switch
            {
                0 => false,
                1 => true,
                _ => null
            };
        }

        internal static int? ResolveAnimationDisplayerCatchSuccessHintFromPacketEffect(
            RemoteUserActorPool.RemoteStringEffectPresentation presentation)
        {
            int? secondaryHint = presentation.SecondaryInt32Value;
            if (ResolveAnimationDisplayerCatchSuccessHint(secondaryHint).HasValue)
            {
                return secondaryHint;
            }

            int[] trailingValues = presentation.TrailingInt32Values;
            if (trailingValues == null || trailingValues.Length == 0)
            {
                return secondaryHint;
            }

            for (int i = 0; i < trailingValues.Length; i++)
            {
                int candidateHint = trailingValues[i];
                if (ResolveAnimationDisplayerCatchSuccessHint(candidateHint).HasValue)
                {
                    return candidateHint;
                }
            }

            byte[] branchPrefixBytes = presentation.StringBranchPrefixBytes;
            if (branchPrefixBytes != null && branchPrefixBytes.Length > 0)
            {
                for (int i = branchPrefixBytes.Length - 1; i >= 0; i--)
                {
                    int candidateHint = branchPrefixBytes[i];
                    if (ResolveAnimationDisplayerCatchSuccessHint(candidateHint).HasValue)
                    {
                        return candidateHint;
                    }
                }
            }

            return secondaryHint;
        }

        internal static Vector2 ResolveAnimationDisplayerMobCatchAnchor(Vector2 mobHeadAnchor)
        {
            return new Vector2(
                mobHeadAnchor.X,
                mobHeadAnchor.Y - AnimationDisplayerCatchMobHeadVerticalOffset);
        }

        internal static string ResolveAnimationDisplayerCoolEffectUol(
            Func<int, string, string> stringPoolResolver = null)
        {
            Func<int, string, string> resolver = stringPoolResolver
                ?? ((stringPoolId, fallbackText) => MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackText));
            string resolvedEffectUol = NormalizeRemotePacketOwnedStringEffectUol(
                resolver(AnimationDisplayerCoolEffectStringPoolId, AnimationDisplayerCoolEffectFallbackUol));
            return string.IsNullOrWhiteSpace(resolvedEffectUol)
                ? AnimationDisplayerCoolEffectFallbackUol
                : resolvedEffectUol;
        }

        internal static string ResolveAnimationDisplayerSquibEffectUol(int variant)
        {
            return variant == 2
                ? AnimationDisplayerSquibEffect2Uol
                : AnimationDisplayerSquibEffectUol;
        }

        internal static string ResolveAnimationDisplayerSquibVisualEffectUol(
            string effectUol,
            Func<string, WzImageProperty> propertyResolver)
        {
            string normalizedEffectUol = NormalizeRemotePacketOwnedStringEffectUol(effectUol);
            if (string.IsNullOrWhiteSpace(normalizedEffectUol))
            {
                return null;
            }

            Func<string, WzImageProperty> resolver = propertyResolver ?? ResolveAnimationDisplayerPropertyStatic;
            WzImageProperty rootProperty = WzInfoTools.GetRealProperty(resolver(normalizedEffectUol));
            if (rootProperty == null || rootProperty is WzCanvasProperty)
            {
                return normalizedEffectUol;
            }

            if (rootProperty is not WzSubProperty rootSubProperty)
            {
                return normalizedEffectUol;
            }

            int childCount = rootSubProperty.WzProperties?.Count ?? 0;
            for (int index = 0; index < childCount; index++)
            {
                WzImageProperty child = WzInfoTools.GetRealProperty(
                    rootSubProperty[index.ToString(CultureInfo.InvariantCulture)]);
                string visualPath = NormalizeRemotePacketOwnedStringEffectUol(child?["visual"]?.GetString());
                if (!string.IsNullOrWhiteSpace(visualPath))
                {
                    return visualPath;
                }
            }

            if (resolver($"{normalizedEffectUol}/0") != null)
            {
                return $"{normalizedEffectUol}/0";
            }

            return normalizedEffectUol;
        }

        internal static bool TryResolveAnimationDisplayerSquibVariantFromEffectUol(string effectUol, out int variant)
        {
            variant = 0;
            string normalizedEffectUol = NormalizeRemotePacketOwnedStringEffectUol(effectUol);
            if (string.IsNullOrWhiteSpace(normalizedEffectUol))
            {
                return false;
            }

            if (normalizedEffectUol.IndexOf("/Flame/SquibEffect2", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                variant = 2;
                return true;
            }

            if (normalizedEffectUol.IndexOf("/Flame/SquibEffect", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                variant = 1;
                return true;
            }

            return false;
        }

        internal static bool TryResolveAnimationDisplayerTransformedOnLadderFromEffectUol(
            string effectUol,
            out bool onLadder)
        {
            onLadder = false;
            string normalizedEffectUol = NormalizeRemotePacketOwnedStringEffectUol(effectUol);
            if (string.IsNullOrWhiteSpace(normalizedEffectUol))
            {
                return false;
            }

            if (normalizedEffectUol.IndexOf("/TransformOnLadder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                onLadder = true;
                return true;
            }

            if (normalizedEffectUol.EndsWith("/Transform", StringComparison.OrdinalIgnoreCase)
                || normalizedEffectUol.IndexOf("/Transform/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        internal static string ResolveAnimationDisplayerTransformedEffectUol(bool onLadder)
        {
            return onLadder
                ? AnimationDisplayerTransformOnLadderEffectUol
                : AnimationDisplayerTransformEffectUol;
        }

        internal static bool TryResolveAnimationDisplayerReservedEffectMetadata(
            string effectUol,
            Func<string, WzImageProperty> propertyResolver,
            out AnimationDisplayerReservedEffectMetadata metadata)
        {
            metadata = default;
            IReadOnlyList<AnimationDisplayerReservedEffectMetadata> metadataEntries =
                EnumerateAnimationDisplayerReservedEffectMetadata(effectUol, propertyResolver);
            if (metadataEntries.Count <= 0)
            {
                return false;
            }

            metadata = metadataEntries[0];
            return true;
        }

        internal static IReadOnlyList<AnimationDisplayerReservedEffectMetadata> EnumerateAnimationDisplayerReservedEffectMetadata(
            string effectUol,
            Func<string, WzImageProperty> propertyResolver)
        {
            string normalizedEffectUol = NormalizeRemotePacketOwnedStringEffectUol(effectUol);
            if (string.IsNullOrWhiteSpace(normalizedEffectUol))
            {
                return Array.Empty<AnimationDisplayerReservedEffectMetadata>();
            }

            Func<string, WzImageProperty> resolver = propertyResolver ?? ResolveAnimationDisplayerPropertyStatic;
            WzImageProperty rootProperty = WzInfoTools.GetRealProperty(resolver(normalizedEffectUol));
            if (rootProperty == null)
            {
                return Array.Empty<AnimationDisplayerReservedEffectMetadata>();
            }

            var entries = new List<AnimationDisplayerReservedEffectMetadata>();
            if (rootProperty is WzSubProperty rootSubProperty)
            {
                if (TryGetAnimationDisplayerReservedNumericRow(rootSubProperty, 0, out WzImageProperty firstNumericRow))
                {
                    int rowIndex = 0;
                    WzImageProperty numericRow = firstNumericRow;
                    while (numericRow != null)
                    {
                        if (!TryBuildAnimationDisplayerReservedEffectMetadata(
                                numericRow,
                                out AnimationDisplayerReservedEffectMetadata childMetadata))
                        {
                            break;
                        }

                        entries.Add(childMetadata);
                        rowIndex++;
                        if (!TryGetAnimationDisplayerReservedNumericRow(rootSubProperty, rowIndex, out numericRow))
                        {
                            break;
                        }
                    }
                }
                else
                {
                    var children = rootSubProperty.WzProperties;
                    if (children != null)
                    {
                        for (int index = 0; index < children.Count; index++)
                        {
                            WzImageProperty child = WzInfoTools.GetRealProperty(children[index]);
                            if (child == null)
                            {
                                continue;
                            }

                            if (!TryBuildAnimationDisplayerReservedEffectMetadata(child, out AnimationDisplayerReservedEffectMetadata childMetadata))
                            {
                                continue;
                            }

                            entries.Add(childMetadata);
                        }
                    }
                }
            }

            if (entries.Count > 0)
            {
                return entries;
            }

            return TryBuildAnimationDisplayerReservedEffectMetadata(rootProperty, out AnimationDisplayerReservedEffectMetadata rootMetadata)
                ? new[] { rootMetadata }
                : Array.Empty<AnimationDisplayerReservedEffectMetadata>();
        }

        private static bool TryGetAnimationDisplayerReservedNumericRow(
            WzSubProperty rootProperty,
            int rowIndex,
            out WzImageProperty row)
        {
            row = null;
            if (rootProperty == null || rowIndex < 0)
            {
                return false;
            }

            row = WzInfoTools.GetRealProperty(rootProperty[rowIndex.ToString(CultureInfo.InvariantCulture)]);
            return row != null;
        }

        private static bool TryBuildAnimationDisplayerReservedEffectMetadata(
            WzImageProperty property,
            out AnimationDisplayerReservedEffectMetadata metadata)
        {
            metadata = default;
            if (property == null)
            {
                return false;
            }

            int type = property["type"]?.GetInt() ?? 0;
            int startDelayMs = Math.Max(0, property["start"]?.GetInt() ?? 0);
            string visualPath = NormalizeRemotePacketOwnedStringEffectUol(property["visual"]?.GetString());
            string soundDescriptor = NormalizeAnimationDisplayerPath(property["sound"]?.GetString());
            int fieldId = Math.Max(0, property["field"]?.GetInt() ?? 0);
            int emotionId = property["x"]?.GetInt() ?? -1;
            string actionName = property["action"]?.GetString()?.Trim();
            int? positionX = property["x"] != null
                ? property["x"]?.GetInt() ?? 0
                : null;
            int? positionY = property["y"] != null
                ? property["y"]?.GetInt() ?? 0
                : null;
            int offsetX = property["dx"]?.GetInt() ?? 0;
            int offsetY = property["dy"]?.GetInt() ?? 0;
            int relativeOffsetX = property["x1"]?.GetInt() ?? 0;
            int relativeOffsetY = property["y1"]?.GetInt() ?? 0;
            int width = Math.Max(0, property["width"]?.GetInt() ?? 0);
            int height = Math.Max(0, property["height"]?.GetInt() ?? 0);
            int durationMs = Math.Max(0, property["duration"]?.GetInt() ?? 0);
            float probability = Math.Max(0f, property["probability"]?.GetFloat() ?? 0f);
            int layerZ = property["z"]?.GetInt() ?? 0;
            IReadOnlyList<int> equippedItemIds = ResolveAnimationDisplayerReservedEquippedItemIds(property);

            bool hasEntryData = !string.IsNullOrWhiteSpace(visualPath)
                || !string.IsNullOrWhiteSpace(soundDescriptor)
                || fieldId > 0
                || !string.IsNullOrWhiteSpace(actionName)
                || emotionId >= 0
                || startDelayMs > 0
                || type != 0
                || positionX.HasValue
                || positionY.HasValue
                || offsetX != 0
                || offsetY != 0
                || relativeOffsetX != 0
                || relativeOffsetY != 0
                || width > 0
                || height > 0
                || durationMs > 0
                || probability > 0f
                || layerZ != 0
                || equippedItemIds.Count > 0;
            if (!hasEntryData)
            {
                return false;
            }

            metadata = new AnimationDisplayerReservedEffectMetadata(
                Type: type,
                StartDelayMs: startDelayMs,
                VisualEffectUol: visualPath,
                SoundEffectDescriptor: soundDescriptor,
                FieldId: fieldId,
                ActionName: actionName,
                EmotionId: emotionId,
                PositionX: positionX,
                PositionY: positionY,
                OffsetX: offsetX,
                OffsetY: offsetY,
                RelativeOffsetX: relativeOffsetX,
                RelativeOffsetY: relativeOffsetY,
                Width: width,
                Height: height,
                DurationMs: durationMs,
                Probability: probability,
                LayerZ: layerZ,
                EquippedItemIds: equippedItemIds);
            return true;
        }

        private static IReadOnlyList<int> ResolveAnimationDisplayerReservedEquippedItemIds(WzImageProperty property)
        {
            if (property is not WzSubProperty metadataProperty || metadataProperty.WzProperties == null)
            {
                return Array.Empty<int>();
            }

            var equippedItemIdsByIndex = new SortedDictionary<int, int>();
            foreach (WzImageProperty child in metadataProperty.WzProperties)
            {
                if (child == null
                    || !int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int equipIndex)
                    || equipIndex <= 0)
                {
                    continue;
                }

                WzImageProperty resolvedChild = WzInfoTools.GetRealProperty(child);
                if (resolvedChild?.GetInt() is not int itemId || itemId <= 0)
                {
                    continue;
                }

                equippedItemIdsByIndex[equipIndex] = itemId;
            }

            if (equippedItemIdsByIndex.Count <= 0)
            {
                return Array.Empty<int>();
            }

            var equippedItemIds = new List<int>(equippedItemIdsByIndex.Count);
            foreach (KeyValuePair<int, int> entry in equippedItemIdsByIndex)
            {
                equippedItemIds.Add(entry.Value);
            }

            return equippedItemIds;
        }

        internal static bool ShouldApplyAnimationDisplayerReservedFieldScopedVisual(int reservedFieldId, int currentMapId)
        {
            return reservedFieldId <= 0
                || currentMapId <= 0
                || reservedFieldId == currentMapId;
        }

        internal static int ResolveAnimationDisplayerReservedAreaBurstCount(int durationMs, float probability)
        {
            int clampedDurationMs = Math.Max(1, durationMs);
            float clampedProbability = Math.Max(0f, probability);
            int bursts = (int)Math.Round(
                (clampedDurationMs / 100f) * clampedProbability,
                MidpointRounding.AwayFromZero);
            return Math.Max(1, bursts);
        }

        internal static int ResolveAnimationDisplayerReservedAreaBurstAttemptCount(int durationMs, int updateIntervalMs)
        {
            int clampedDurationMs = Math.Max(1, durationMs);
            int clampedUpdateIntervalMs = Math.Max(1, updateIntervalMs);
            return Math.Max(1, (int)Math.Ceiling(clampedDurationMs / (double)clampedUpdateIntervalMs));
        }

        internal static float ResolveAnimationDisplayerReservedAreaSpawnProbability(float probability)
        {
            if (float.IsNaN(probability) || probability <= 0f)
            {
                return 0f;
            }

            return Math.Min(1f, probability);
        }

        internal static int ResolveAnimationDisplayerReservedVisualVariantIndex(
            int registrationTime,
            int variantCount)
        {
            if (variantCount <= 0)
            {
                return 0;
            }

            uint unsignedTime = unchecked((uint)registrationTime);
            return (int)(unsignedTime % (uint)variantCount);
        }

        internal static int ResolveAnimationDisplayerReservedEmotionDurationMs()
        {
            // Client evidence (`RESERVEDINFO::Update`, type 6):
            // `CAvatar::SetEmotion(..., -1)` uses the indefinite sentinel.
            return -1;
        }

        internal static int ResolveAnimationDisplayerReservedStartRegistrationTime(int currentTime, int startDelayMs)
        {
            if (startDelayMs <= 0)
            {
                return currentTime;
            }

            return unchecked(currentTime + startDelayMs);
        }

        internal static bool ShouldConsumeAnimationDisplayerReservedTypeWithoutVisualFallback(int type)
        {
            return type >= 2 && type <= 6;
        }

        internal static bool ShouldApplyAnimationDisplayerReservedTransferFieldRequest(int targetFieldId, int currentMapId)
        {
            return targetFieldId > 0
                && targetFieldId != MapConstants.MaxMap
                && targetFieldId != currentMapId;
        }

        internal static bool ShouldApplyAnimationDisplayerReservedLocalOnlyOwnerEffect(
            int effectCharacterId,
            int localCharacterId)
        {
            return effectCharacterId > 0
                && localCharacterId > 0
                && effectCharacterId == localCharacterId;
        }

        internal static int ResolveAnimationDisplayerReservedRemoteUtilityActionRestoreDelayMs(
            int metadataDurationMs,
            int actionDurationMs = 0)
        {
            int clampedMetadataDurationMs = Math.Max(0, metadataDurationMs);
            int clampedActionDurationMs = Math.Max(0, actionDurationMs);
            if (clampedMetadataDurationMs > 0 || clampedActionDurationMs > 0)
            {
                // Client evidence (`RESERVEDINFO::Update`, type 4):
                // `CAvatar::SetOneTimeAction` owns completion timing. Keep simulator restore
                // from ending earlier than either the metadata window or authored action window.
                return Math.Max(1, Math.Max(clampedMetadataDurationMs, clampedActionDurationMs));
            }

            return Math.Max(
                1,
                AnimationDisplayerReservedRemoteUtilityActionRestoreFallbackDurationMs);
        }

        internal static (string PreviousActionName, bool? PreviousFacingRight) ResolveAnimationDisplayerReservedRemoteUtilityActionRestoreTarget(
            string actorActionName,
            bool actorFacingRight,
            string activeReservedActionName,
            string activePreviousActionName,
            bool? activePreviousFacingRight)
        {
            if (!string.IsNullOrWhiteSpace(actorActionName)
                && !string.IsNullOrWhiteSpace(activeReservedActionName)
                && !string.IsNullOrWhiteSpace(activePreviousActionName)
                && string.Equals(actorActionName, activeReservedActionName, StringComparison.OrdinalIgnoreCase))
            {
                return (activePreviousActionName, activePreviousFacingRight);
            }

            return (actorActionName, actorFacingRight);
        }

        internal static Vector2 ResolveAnimationDisplayerReservedVisualPosition(
            Vector2 ownerPosition,
            AnimationDisplayerReservedEffectMetadata metadata,
            int registerTime,
            int currentTime)
        {
            Vector2 basePosition = metadata.PositionX.HasValue && metadata.PositionY.HasValue
                ? new Vector2(metadata.PositionX.Value, metadata.PositionY.Value)
                : ownerPosition;
            basePosition = new Vector2(
                basePosition.X + metadata.OffsetX,
                basePosition.Y + metadata.OffsetY);

            if (metadata.Type != 0
                || (metadata.RelativeOffsetX == 0 && metadata.RelativeOffsetY == 0))
            {
                return new Vector2(
                    basePosition.X + metadata.RelativeOffsetX,
                    basePosition.Y + metadata.RelativeOffsetY);
            }

            int durationMs = Math.Max(1, metadata.DurationMs);
            int elapsedMs = ResolveAnimationDisplayerTickElapsedMs(currentTime, registerTime, durationMs);
            float t = elapsedMs / (float)durationMs;
            return new Vector2(
                basePosition.X + (metadata.RelativeOffsetX * t),
                basePosition.Y + (metadata.RelativeOffsetY * t));
        }

        private bool TryRegisterAnimationDisplayerLocalCooldownReady(int currentTime)
        {
            if (!TryResolveAnimationDisplayerOwner(
                    "local",
                    out int ownerCharacterId,
                    out Func<Vector2> getPosition,
                    out _))
            {
                return false;
            }

            return TryRegisterAnimationDisplayerCool(
                ownerCharacterId,
                getPosition,
                currentTime,
                out _);
        }

        private void HandleAnimationDisplayerCatchRegistrationRequested(
            SkillManager.AnimationDisplayerCatchRegistrationRequest request)
        {
            if (_animationEffects == null || request.TargetMobId <= 0)
            {
                return;
            }

            MobItem target = _mobPool?.GetMob(request.TargetMobId);
            if (target == null)
            {
                return;
            }

            _ = TryRegisterAnimationDisplayerCatch(
                request.Success,
                request.TargetMobId,
                () => ResolveAnimationDisplayerMobCatchAnchor(
                    target.GetDamageNumberAnchor(verticalPadding: 0)),
                request.RequestedAt,
                requestedEffectUol: null,
                packetOwnedOwnerContext: null,
                out _);
        }

        private void HandleAnimationDisplayerSwallowAbsorbRequested(
            SkillManager.SwallowAbsorbRequest request)
        {
            if (_animationEffects == null || request.TargetMobId <= 0)
            {
                return;
            }

            MobItem target = _mobPool?.GetMob(request.TargetMobId);
            if (target == null)
            {
                return;
            }

            _ = TryRegisterAnimationDisplayerMobSwallowAbsorbOwner(request, target, out _);
        }

        private bool TryRegisterAnimationDisplayerMobSwallowAbsorbOwner(
            SkillManager.SwallowAbsorbRequest request,
            MobItem target,
            out string message)
        {
            message = null;
            if (target == null || target.MobId <= 0)
            {
                message = "Mob swallow animation-displayer owner is missing.";
                return false;
            }

            PlayerCharacter localPlayer = _playerManager?.Player;
            if (localPlayer == null)
            {
                message = "Local user owner is missing for swallow animation-displayer parity.";
                return false;
            }

            if (!TryResolveAnimationDisplayerMobSwallowFrames(
                    target.MobId,
                    out string resolvedEffectUol,
                    out List<IDXObject> frames))
            {
                message = $"Mob swallow animation frames could not be loaded for {target.MobId:D7}.";
                return false;
            }

            Vector2 startPosition = target.GetDamageNumberAnchor(verticalPadding: 0);
            int registerTime = request.RequestedAt > 0 ? request.RequestedAt : currTickCount;
            Vector2 fallbackTargetPosition = _playerManager?.Player?.Physics?.GetPosition() ?? localPlayer.Position;
            bool fallbackFacingRight = _playerManager?.Player?.FacingRight ?? localPlayer.FacingRight;
            Vector2 fallbackEndPosition = ResolveAnimationDisplayerMobSwallowTargetAnchor(
                fallbackTargetPosition,
                fallbackFacingRight);
            int travelDurationMs = ResolveAnimationDisplayerMobSwallowTravelDurationMs(
                startPosition,
                fallbackEndPosition);
            Func<Vector2> getPosition = () =>
            {
                Vector2 liveTargetPosition = _playerManager?.Player?.Physics?.GetPosition() ?? fallbackTargetPosition;
                bool liveFacingRight = _playerManager?.Player?.FacingRight ?? fallbackFacingRight;
                Vector2 liveEndPosition = ResolveAnimationDisplayerMobSwallowTargetAnchor(
                    liveTargetPosition,
                    liveFacingRight);
                int elapsedMs = ResolveAnimationDisplayerTickElapsedMs(currTickCount, registerTime, travelDurationMs);
                float t = travelDurationMs <= 0 ? 1f : elapsedMs / (float)travelDurationMs;
                return Vector2.Lerp(startPosition, liveEndPosition, t);
            };

            Vector2 fallbackPosition = getPosition();
            int initialElapsedMs = ResolveAnimationDisplayerPacketOwnedMobOneTimeInitialElapsed(
                "mobSwallow",
                target.MobId,
                AnimationDisplayerMobSwallowPrimaryAction,
                resolvedEffectUol,
                ownerFacingRight: fallbackFacingRight,
                registerTime,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            _animationEffects.AddPacketOwnedMobSwallow(
                frames,
                resolvedEffectUol,
                getPosition,
                getFlip: null,
                fallbackPosition.X,
                fallbackPosition.Y,
                fallbackFlip: false,
                registerTime,
                initialElapsedMs: initialElapsedMs);
            message =
                $"Registered mob-swallow animation-displayer owner frames from {resolvedEffectUol} (targetMob={request.TargetMobId}).";
            return true;
        }

        private bool TryApplyAnimationDisplayerSessionValueCoolOwner(
            string key,
            string value,
            int currentTime,
            out string message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string coolKey = MapleStoryStringPool.GetOrFallback(
                AnimationDisplayerSessionValueCoolKeyStringPoolId,
                AnimationDisplayerSessionValueCoolFallbackKey);
            int previousCoolRank = _animationDisplayerSessionValueCoolRank;
            if (string.Equals(key.Trim(), coolKey.Trim(), StringComparison.Ordinal)
                && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int observedCoolRank))
            {
                _animationDisplayerSessionValueCoolRank = observedCoolRank;
            }

            if (!ShouldRegisterAnimationDisplayerSessionValueCool(
                    key,
                    value,
                    previousCoolRank,
                    coolKey,
                    out int currentCoolRank))
            {
                return false;
            }

            if (!TryResolveAnimationDisplayerOwner(
                    "local",
                    out int ownerCharacterId,
                    out Func<Vector2> getPosition,
                    out _))
            {
                return false;
            }

            bool registered = TryRegisterAnimationDisplayerCool(
                ownerCharacterId,
                getPosition,
                currentTime,
                out string registrationMessage);
            if (!registered)
            {
                return false;
            }

            message = $"{registrationMessage} session={key} rank={currentCoolRank}";
            return true;
        }

        internal static bool ShouldRegisterAnimationDisplayerSessionValueCool(
            string key,
            string value,
            int previousCoolRank,
            string expectedKey,
            out int currentCoolRank)
        {
            currentCoolRank = previousCoolRank;
            if (string.IsNullOrWhiteSpace(key)
                || string.IsNullOrWhiteSpace(value)
                || string.IsNullOrWhiteSpace(expectedKey))
            {
                return false;
            }

            if (!string.Equals(key.Trim(), expectedKey.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out currentCoolRank))
            {
                return false;
            }

            return currentCoolRank > previousCoolRank;
        }

        private void HandleAnimationDisplayerMobProjectileRegistration(
            MobAttackSystem.AnimationDisplayerProjectileRegistrationRequest request)
        {
            if (_animationEffects == null)
            {
                return;
            }

            TryRegisterAnimationDisplayerMobProjectile(request, out _);
        }

        private bool TryRegisterAnimationDisplayerMobProjectile(
            MobAttackSystem.AnimationDisplayerProjectileRegistrationRequest request,
            out string message)
        {
            message = null;
            if (request.MobId <= 0
                || string.IsNullOrWhiteSpace(request.AttackAction)
                || request.GetPosition == null)
            {
                message = "Mob projectile animation-displayer owner is missing.";
                return false;
            }

            if (!TryResolveAnimationDisplayerMobProjectileFrames(
                    request.MobId,
                    request.AttackAction,
                    request.BallUol,
                    out string resolvedEffectUol,
                    out List<IDXObject> frames))
            {
                message = $"Mob projectile animation frames could not be loaded for {request.MobId}/{request.AttackAction}.";
                return false;
            }

            bool isMobBulletOwner = MobAttackSystem.ShouldRegisterAnimationDisplayerMobBullet(
                request.HasClientMobActionFrames,
                resolvedEffectUol);
            bool hasResolvedDisplayerProjectileFrames = Animation.AnimationEffects.HasFrames(frames);
            bool isMobSwallowOwner = !isMobBulletOwner
                && MobAttackSystem.ShouldRegisterAnimationDisplayerMobSwallowBullet(
                    request.HasCanvasFrames,
                    hasResolvedDisplayerProjectileFrames,
                    request.HasClientMobActionFrames,
                    request.AttackType,
                    request.IsRangedAttack);
            if (!isMobBulletOwner && !isMobSwallowOwner)
            {
                message = $"Mob projectile owner was skipped for {request.MobId}/{request.AttackAction}.";
                return false;
            }

            string ownerName = isMobSwallowOwner ? "mob-swallow" : "mob-bullet";
            Vector2 fallbackPosition = request.GetPosition();
            Func<bool> getFlip = request.GetFacingRight == null
                ? null
                : () => !request.GetFacingRight();
            bool fallbackFlip = request.GetFacingRight != null && !request.GetFacingRight();
            bool ownerFacingRight = request.GetFacingRight?.Invoke() ?? !fallbackFlip;
            int registerTime = ResolveAnimationDisplayerMobProjectileRegistrationTime(
                request.CurrentTime,
                request.AttackAfterMs);
            string ownerKind = isMobSwallowOwner ? "mobSwallowBullet" : "mobBullet";
            int initialElapsedMs = ResolveAnimationDisplayerPacketOwnedMobOneTimeInitialElapsed(
                ownerKind,
                request.MobId,
                request.AttackAction,
                resolvedEffectUol,
                ownerFacingRight,
                registerTime,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            if (isMobSwallowOwner)
            {
                _animationEffects.AddPacketOwnedMobSwallow(
                    frames,
                    resolvedEffectUol,
                    request.GetPosition,
                    getFlip,
                    fallbackPosition.X,
                    fallbackPosition.Y,
                    fallbackFlip,
                    registerTime,
                    initialElapsedMs: initialElapsedMs);
            }
            else
            {
                _animationEffects.AddPacketOwnedMobBullet(
                    frames,
                    resolvedEffectUol,
                    request.GetPosition,
                    getFlip,
                    fallbackPosition.X,
                    fallbackPosition.Y,
                    fallbackFlip,
                    registerTime,
                    initialElapsedMs: initialElapsedMs);
            }

            message = $"Registered {ownerName} animation-displayer owner frames from {resolvedEffectUol}.";
            return true;
        }

        private bool TryResolveAnimationDisplayerMobProjectileFrames(
            int mobId,
            string attackAction,
            string requestedBallUol,
            out string resolvedEffectUol,
            out List<IDXObject> frames)
        {
            resolvedEffectUol = null;
            frames = null;

            var candidates = new List<string>();
            AddAnimationDisplayerMobProjectileCandidate(candidates, requestedBallUol);
            IReadOnlyList<string> fallbackCandidates =
                MobAttackSystem.EnumerateAnimationDisplayerMobBulletEffectUolCandidates(mobId, attackAction);
            for (int i = 0; i < fallbackCandidates.Count; i++)
            {
                AddAnimationDisplayerMobProjectileCandidate(candidates, fallbackCandidates[i]);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (TryGetAnimationDisplayerFrames(
                        $"mobbullet:{mobId}:{attackAction}:{i}",
                        candidate,
                        out List<IDXObject> candidateFrames))
                {
                    resolvedEffectUol = candidate;
                    frames = candidateFrames;
                    return true;
                }
            }

            return false;
        }

        private static void AddAnimationDisplayerMobProjectileCandidate(List<string> candidates, string candidateUol)
        {
            string normalized = NormalizeRemotePacketOwnedStringEffectUol(candidateUol);
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

        private bool TryResolveAnimationDisplayerMobSwallowFrames(
            int mobId,
            out string resolvedEffectUol,
            out List<IDXObject> frames)
        {
            resolvedEffectUol = null;
            frames = null;

            IReadOnlyList<string> candidates = EnumerateAnimationDisplayerMobSwallowEffectUolCandidates(mobId);
            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (TryGetAnimationDisplayerFrames(
                        $"mobswallow:{mobId}:{i}",
                        candidate,
                        out List<IDXObject> candidateFrames))
                {
                    resolvedEffectUol = candidate;
                    frames = candidateFrames;
                    return true;
                }
            }

            return false;
        }

        internal static int ResolveAnimationDisplayerMobProjectileRegistrationTime(
            int currentTime,
            int attackAfterMs)
        {
            return unchecked(currentTime + Math.Max(0, attackAfterMs));
        }

        internal static IReadOnlyList<string> EnumerateAnimationDisplayerMobSwallowEffectUolCandidates(int mobId)
        {
            if (mobId <= 0)
            {
                return Array.Empty<string>();
            }

            string imageName = mobId.ToString("D7", CultureInfo.InvariantCulture);
            return new[]
            {
                $"Mob/{imageName}.img/{AnimationDisplayerMobSwallowPrimaryAction}/0",
                $"Mob/{imageName}.img/stand/0"
            };
        }

        internal static Vector2 ResolveAnimationDisplayerMobSwallowTargetAnchor(
            Vector2 userPosition,
            bool facingRight)
        {
            return new Vector2(
                userPosition.X + (facingRight ? AnimationDisplayerMobSwallowTargetHorizontalOffset : -AnimationDisplayerMobSwallowTargetHorizontalOffset),
                userPosition.Y - AnimationDisplayerMobSwallowTargetVerticalOffset);
        }

        internal static int ResolveAnimationDisplayerMobSwallowTravelDurationMs(
            Vector2 sourcePosition,
            Vector2 targetPosition)
        {
            float distance = Vector2.Distance(sourcePosition, targetPosition);
            int durationMs = (int)MathF.Round(distance * 1.5f, MidpointRounding.AwayFromZero);
            return Math.Max(1, durationMs);
        }

        internal static bool TryResolveAnimationDisplayerCombatFeedbackColor(
            string token,
            out DamageColorType colorType)
        {
            colorType = DamageColorType.Red;
            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }

            string normalized = token.Trim();
            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int encodedColorType))
            {
                if (encodedColorType == 0)
                {
                    colorType = DamageColorType.Red;
                    return true;
                }

                if (encodedColorType == 1)
                {
                    colorType = DamageColorType.Blue;
                    return true;
                }

                if (encodedColorType == 2)
                {
                    colorType = DamageColorType.Violet;
                    return true;
                }

                return false;
            }

            if (string.Equals(normalized, "red", StringComparison.OrdinalIgnoreCase))
            {
                colorType = DamageColorType.Red;
                return true;
            }

            if (string.Equals(normalized, "violet", StringComparison.OrdinalIgnoreCase))
            {
                colorType = DamageColorType.Violet;
                return true;
            }

            if (string.Equals(normalized, "blue", StringComparison.OrdinalIgnoreCase))
            {
                colorType = DamageColorType.Blue;
                return true;
            }

            return false;
        }

        internal static AnimationDisplayerFallingRegistrationProfile BuildAnimationDisplayerFallingRegistrationProfile(
            Rectangle startArea,
            int offsetX,
            int offsetY,
            int alpha,
            int fallDistance,
            int updateIntervalMs,
            int updateCount,
            int updateNextMs,
            int durationMs,
            int frameCount)
        {
            return new AnimationDisplayerFallingRegistrationProfile(
                startArea,
                offsetX,
                offsetY,
                alpha,
                Math.Max(1, fallDistance),
                Math.Max(1, updateIntervalMs),
                Math.Max(0, updateCount),
                Math.Max(0, updateNextMs),
                Math.Max(0, durationMs),
                Math.Max(0, frameCount));
        }

        internal static AnimationDisplayerAreaRegistrationProfile BuildAnimationDisplayerAreaRegistrationProfile(
            Rectangle area,
            int offsetX,
            int offsetY,
            int updateIntervalMs,
            int updateCount,
            int updateNextMs,
            int durationMs,
            int? layerZ = null)
        {
            return new AnimationDisplayerAreaRegistrationProfile(
                area,
                offsetX,
                offsetY,
                updateIntervalMs,
                updateCount,
                updateNextMs,
                durationMs,
                layerZ);
        }

        private static string CombineAnimationDisplayerEffectUol(string baseEffectUol, string childName)
        {
            string normalizedBase = baseEffectUol?.Replace('\\', '/').Trim().TrimEnd('/');
            string normalizedChild = childName?.Replace('\\', '/').Trim().TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedBase))
            {
                return normalizedChild;
            }

            if (string.IsNullOrWhiteSpace(normalizedChild))
            {
                return normalizedBase;
            }

            return $"{normalizedBase}/{normalizedChild}";
        }

        private static int ReadAnimationDisplayerIntProperty(WzImageProperty property, string name)
        {
            return property?[name]?.GetInt() ?? 0;
        }

        private static int? ReadAnimationDisplayerNullableIntProperty(WzImageProperty property, string name)
        {
            WzImageProperty value = property?[name];
            return value == null ? null : value.GetInt();
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
                    out int initialElapsedMs,
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
                       currTickCount,
                       initialElapsedMs) >= 0;
        }

        private bool TryResolveAnimationDisplayerUserStateFrames(
            int ownerCharacterId,
            out int initialElapsedMs,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames)
        {
            initialElapsedMs = 0;
            startFrames = null;
            repeatFrames = null;
            endFrames = null;

            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (ownerCharacterId > 0
                && ownerCharacterId == localCharacterId
                && TryResolveAnimationDisplayerSpecificUserStateFramesForElapsed(
                    SkillManager.BuildAnimationDisplayerTemporaryStatAvatarEffectStatesForParity(
                        _playerManager?.Skills?.ActiveBuffs,
                        _playerManager?.Skills?.GetActiveDeferredAvatarEffectSkillIdForParity()),
                    currTickCount,
                    _playerManager?.Player?.CurrentActionName,
                    out initialElapsedMs,
                    out _,
                    out startFrames,
                    out repeatFrames,
                    out endFrames))
            {
                return true;
            }

            if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor actor) == true
                && actor != null
                && TryResolveAnimationDisplayerSpecificUserStateFramesForElapsed(
                    BuildAnimationDisplayerSpecificUserStateCandidates(actor, currTickCount),
                    currTickCount,
                    actor.ActionName,
                    out initialElapsedMs,
                    out _,
                    out startFrames,
                    out repeatFrames,
                    out endFrames))
            {
                return true;
            }

            return TryGetAnimationDisplayerFrames("userstate:generic", AnimationDisplayerGenericUserStateEffectUol, out repeatFrames);
        }

        private static bool TryResolveAnimationDisplayerSpecificUserStateFramesForElapsed(
            IReadOnlyList<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> candidateStates,
            int currentTime,
            string ownerActionName,
            out int initialElapsedMs,
            out int resolvedSkillId,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames)
        {
            initialElapsedMs = 0;
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
                        ownerActionName,
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
                initialElapsedMs = ResolveAnimationDisplayerSpecificUserStateInitialElapsed(state, currentTime);
                return true;
            }

            return false;
        }

        internal static int ResolveAnimationDisplayerSpecificUserStateInitialElapsed(
            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState state,
            int currentTime)
        {
            if (state == null
                || state.AnimationStartTime == int.MinValue)
            {
                return 0;
            }

            return ResolveAnimationDisplayerTickElapsedMs(currentTime, state.AnimationStartTime);
        }

        internal static IReadOnlyList<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> BuildAnimationDisplayerSpecificUserStateCandidates(
            RemoteUserActor actor)
        {
            return BuildAnimationDisplayerSpecificUserStateCandidates(actor, Environment.TickCount);
        }

        internal static IReadOnlyList<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> BuildAnimationDisplayerSpecificUserStateCandidatesForTesting(
            RemoteUserActor actor,
            int currentTime)
        {
            return BuildAnimationDisplayerSpecificUserStateCandidates(actor, currentTime);
        }

        private static IReadOnlyList<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> BuildAnimationDisplayerSpecificUserStateCandidates(
            RemoteUserActor actor,
            int currentTime)
        {
            var candidates = new List<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState>(11);
            // Client owner-family chain from `CUser::Update` / `CUser::UpdateMoreWildEffect`:
            // SoulArrow -> WeaponCharge -> Aura tails + active Aura -> MoreWild -> Barrier -> BlessingArmor -> Repeat -> MagicShield -> FinalCut.
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatSoulArrowEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatWeaponChargeEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(
                candidates,
                ResolveLatestAnimationDisplayerAuraTailState(actor?.TemporaryStatAuraEffectTails, currentTime));
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatAuraEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatMoreWildEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatBarrierEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatBlessingArmorEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatRepeatEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatMagicShieldEffect);
            AddAnimationDisplayerSpecificUserStateCandidate(candidates, actor?.TemporaryStatFinalCutEffect);
            return candidates;
        }

        private static RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState ResolveLatestAnimationDisplayerAuraTailState(
            IReadOnlyList<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> auraTailStates,
            int currentTime)
        {
            if (auraTailStates == null || auraTailStates.Count == 0)
            {
                return null;
            }

            for (int i = auraTailStates.Count - 1; i >= 0; i--)
            {
                RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState candidate = auraTailStates[i];
                if (candidate != null
                    && !IsAnimationDisplayerSpecificUserStateTailTransitionExpired(candidate, currentTime))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsAnimationDisplayerSpecificUserStateTailTransitionExpired(
            RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState state,
            int currentTime)
        {
            if (state == null
                || currentTime == int.MinValue
                || state.TransitionStartTime == int.MinValue
                || state.TransitionDurationMs <= 0)
            {
                return false;
            }

            return ResolveAnimationDisplayerTickElapsedMs(currentTime, state.TransitionStartTime) >= state.TransitionDurationMs
                   && state.TransitionEndAlpha <= 0f;
        }

        internal static int ResolveAnimationDisplayerTickElapsedMs(int currentTime, int startTime)
        {
            if (currentTime == int.MinValue || startTime == int.MinValue)
            {
                return 0;
            }

            long elapsed = unchecked((uint)(currentTime - startTime));
            return elapsed >= int.MaxValue
                ? int.MaxValue
                : (int)elapsed;
        }

        internal static int ResolveAnimationDisplayerTickElapsedMs(int currentTime, int startTime, int maxElapsedMs)
        {
            if (maxElapsedMs <= 0)
            {
                return 0;
            }

            return Math.Min(ResolveAnimationDisplayerTickElapsedMs(currentTime, startTime), maxElapsedMs);
        }

        internal static bool TryResolveAnimationDisplayerSpecificUserStateFrames(
            IReadOnlyList<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> candidateStates,
            string ownerActionName,
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
                        ownerActionName,
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
            string ownerActionName,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames)
        {
            bool isLadderOrRopeAction = IsAnimationDisplayerSpecificUserStateLadderActionName(ownerActionName);

            startFrames = BuildAnimationDisplayerFramesFromSkillAnimation(
                state?.Skill?.AffectedEffect
                ?? state?.Skill?.AffectedSecondaryEffect
                ?? state?.Skill?.Effect
                ?? state?.Skill?.EffectSecondary);
            repeatFrames = BuildAnimationDisplayerFramesFromSkillAnimation(
                isLadderOrRopeAction
                    ? state?.Skill?.AvatarLadderEffect
                      ?? state?.Skill?.RepeatEffect
                      ?? state?.Skill?.RepeatSecondaryEffect
                      ?? state?.OverlayAnimation
                      ?? state?.OverlaySecondaryAnimation
                      ?? state?.UnderFaceAnimation
                      ?? state?.UnderFaceSecondaryAnimation
                    : state?.Skill?.RepeatEffect
                      ?? state?.Skill?.RepeatSecondaryEffect
                      ?? state?.OverlayAnimation
                      ?? state?.OverlaySecondaryAnimation
                      ?? state?.UnderFaceAnimation
                      ?? state?.UnderFaceSecondaryAnimation);
            endFrames = isLadderOrRopeAction
                ? BuildAnimationDisplayerFramesFromSkillAnimations(
                    state?.Skill?.AvatarLadderFinishEffect,
                    state?.Skill?.AvatarOverlayFinishEffect,
                    state?.Skill?.AvatarUnderFaceFinishEffect,
                    state?.Skill?.KeydownEndEffect,
                    state?.Skill?.KeydownEndSecondaryEffect,
                    state?.Skill?.StopEffect,
                    state?.Skill?.StopSecondaryEffect)
                : BuildAnimationDisplayerFramesFromSkillAnimations(
                    state?.Skill?.AvatarOverlayFinishEffect,
                    state?.Skill?.AvatarUnderFaceFinishEffect,
                    state?.Skill?.AvatarLadderFinishEffect,
                    state?.Skill?.KeydownEndEffect,
                    state?.Skill?.KeydownEndSecondaryEffect,
                    state?.Skill?.StopEffect,
                    state?.Skill?.StopSecondaryEffect);

            if (!Animation.AnimationEffects.HasFrames(repeatFrames)
                && Animation.AnimationEffects.HasFrames(startFrames))
            {
                repeatFrames = startFrames;
            }

            return Animation.AnimationEffects.HasFrames(startFrames)
                || Animation.AnimationEffects.HasFrames(repeatFrames)
                || Animation.AnimationEffects.HasFrames(endFrames);
        }

        internal static bool IsAnimationDisplayerSpecificUserStateLadderActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
            {
                return rawActionCode == AnimationDisplayerLadderRawActionCode
                    || rawActionCode == AnimationDisplayerRopeRawActionCode
                    || rawActionCode == AnimationDisplayerLadder2RawActionCode
                    || rawActionCode == AnimationDisplayerRope2RawActionCode;
            }

            return string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase);
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
                SkillFrame skillFrame = animation.Frames[i];
                IDXObject texture = skillFrame?.Texture;
                if (texture == null)
                {
                    continue;
                }

                if (texture.Texture != null)
                {
                    frames.Add(new DXObject(
                        -skillFrame.Origin.X,
                        -skillFrame.Origin.Y,
                        texture.Texture,
                        skillFrame.Delay)
                    {
                        Tag = texture.Tag
                    });
                    continue;
                }

                frames.Add(texture);
            }

            return frames.Count > 0 ? frames : null;
        }

        internal static List<IDXObject> BuildAnimationDisplayerFramesFromSkillAnimations(
            params SkillAnimation[] animations)
        {
            if (animations == null || animations.Length == 0)
            {
                return null;
            }

            List<IDXObject> mergedFrames = null;
            for (int i = 0; i < animations.Length; i++)
            {
                List<IDXObject> branchFrames = BuildAnimationDisplayerFramesFromSkillAnimation(animations[i]);
                if (!Animation.AnimationEffects.HasFrames(branchFrames))
                {
                    continue;
                }

                mergedFrames ??= new List<IDXObject>(branchFrames.Count);
                mergedFrames.AddRange(branchFrames);
            }

            return Animation.AnimationEffects.HasFrames(mergedFrames)
                ? mergedFrames
                : null;
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

            IReadOnlyList<AnimationDisplayerResolvedFollowEquipmentEntry> followDefinitions =
                ResolveAnimationDisplayerFollowEquipmentDefinitions(ownerCharacterId);
            bool allowGenericFallback = ShouldAllowAnimationDisplayerGenericFollowFallback(registrationKey);
            var definitionsToRegister = new List<AnimationDisplayerResolvedFollowEquipmentEntry>(followDefinitions);
            if (definitionsToRegister.Count == 0 && allowGenericFallback)
            {
                definitionsToRegister.Add(default);
            }
            else if (definitionsToRegister.Count == 0)
            {
                ClearAnimationDisplayerFollowRegistration(registrationKey);
                return false;
            }

            Func<bool> getOwnerFlip = ResolveAnimationDisplayerFollowOwnerFlip(ownerCharacterId);
            Func<bool> getOwnerMoveAction = ResolveAnimationDisplayerFollowOwnerMoveAction(ownerCharacterId);
            Dictionary<int, Queue<AnimationDisplayerFollowRegistrationEntry>> existingEntriesByDefinitionKey =
                BuildAnimationDisplayerFollowExistingEntriesByDefinitionKey(registrationKey);
            var resolvedEntries = new List<AnimationDisplayerFollowRegistrationEntry>(definitionsToRegister.Count);
            for (int definitionIndex = 0; definitionIndex < definitionsToRegister.Count; definitionIndex++)
            {
                AnimationDisplayerResolvedFollowEquipmentEntry resolvedFollowDefinition = definitionsToRegister[definitionIndex];
                AnimationDisplayerFollowEquipmentDefinition followDefinition = resolvedFollowDefinition.Definition;
                int definitionKey = BuildAnimationDisplayerFollowRegistrationDefinitionKey(
                    followDefinition,
                    relativeEmission,
                    resolvedFollowDefinition.CandidateIdentity);

                if (TryDequeueAnimationDisplayerFollowRegistrationEntry(
                        existingEntriesByDefinitionKey,
                        definitionKey,
                        out AnimationDisplayerFollowRegistrationEntry existingEntry))
                {
                    resolvedEntries.Add(existingEntry);
                    continue;
                }

                if (!TryRegisterAnimationDisplayerFollowDefinition(
                        ownerCharacterId,
                        getPosition,
                        relativeEmission,
                        followDefinition,
                        getOwnerFlip,
                        getOwnerMoveAction,
                        resolvedFollowDefinition.CandidateIdentity,
                        out int followId))
                {
                    continue;
                }

                resolvedEntries.Add(new AnimationDisplayerFollowRegistrationEntry
                {
                    DefinitionKey = definitionKey,
                    FollowId = followId
                });
            }

            RemoveAnimationDisplayerFollowStaleEntries(existingEntriesByDefinitionKey);
            if (resolvedEntries.Count == 0)
            {
                _animationDisplayerFollowAnimationIds.Remove(registrationKey);
                _animationDisplayerFollowRegistrationSignatures.Remove(registrationKey);
                return false;
            }

            _animationDisplayerFollowAnimationIds[registrationKey] = resolvedEntries;
            _animationDisplayerFollowRegistrationSignatures[registrationKey] =
                ResolveAnimationDisplayerFollowCandidateSignature(ownerCharacterId);
            return true;
        }

        private bool TryRegisterAnimationDisplayerFollowDefinition(
            int ownerCharacterId,
            Func<Vector2> getPosition,
            bool relativeEmission,
            AnimationDisplayerFollowEquipmentDefinition followDefinition,
            Func<bool> getOwnerFlip,
            Func<bool> getOwnerMoveAction,
            int candidateIdentity,
            out int followId)
        {
            followId = -1;
            List<IDXObject> frames = null;
            IReadOnlyList<List<IDXObject>> followFrameVariants = followDefinition?.EffectFrameVariants;
            if (!string.IsNullOrWhiteSpace(followDefinition?.EffectUol)
                && !Animation.AnimationEffects.HasFrameVariants(followFrameVariants))
            {
                TryGetAnimationDisplayerFrames(
                    $"follow:equipment:{followDefinition.ItemId}",
                    followDefinition.EffectUol,
                    out frames);
            }

            if (followDefinition == null && !Animation.AnimationEffects.HasFrames(frames))
            {
                TryGetAnimationDisplayerFrames("follow:generic", AnimationDisplayerGenericUserStateEffectUol, out frames);
            }

            if (!Animation.AnimationEffects.HasFrameVariants(followFrameVariants))
            {
                followFrameVariants = followDefinition == null
                    ? LoadAnimationDisplayerFollowFrameVariants()
                    : followDefinition?.EffectFrameVariants;
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
            bool spawnOnlyOnOwnerMove = followDefinition?.SpawnOnlyOnOwnerMove ?? false;
            Func<Vector2> getFollowTargetPosition = ResolveAnimationDisplayerFollowTargetPosition(
                ownerCharacterId,
                getPosition,
                followDefinition);
            Point spawnOffsetMin = BuildAnimationDisplayerFollowSpawnOffsetMin(relativeEmission, followDefinition);
            Point spawnOffsetMax = BuildAnimationDisplayerFollowSpawnOffsetMax(relativeEmission, followDefinition);
            bool usesEquipmentEmission = followDefinition?.UsesRelativeEmission ?? false;
            bool spawnUsesEmissionBox = ResolveAnimationDisplayerFollowSpawnUsesEmissionBox(
                relativeEmission,
                followDefinition != null,
                usesEquipmentEmission);
            followId = _animationEffects.AddFollow(
                frames,
                getFollowTargetPosition,
                offsetX: 0f,
                offsetY: followDefinition == null ? AnimationDisplayerUserStateOffsetY : 0f,
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
                    IsTargetMoveAction = spawnOnlyOnOwnerMove ? getOwnerMoveAction : null,
                    SpawnArea = followDefinition?.EmissionArea ?? BuildAnimationDisplayerFollowEmissionArea(),
                    SpawnUsesEmissionBox = spawnUsesEmissionBox,
                    SpawnAppliesEmissionBias = followDefinition == null
                        ? relativeEmission
                        : usesEquipmentEmission,
                    SpawnUsesEmissionTravelDistance = usesEquipmentEmission,
                    SpawnVerticalEmissionBias = AnimationDisplayerFollowEmissionVerticalBias,
                    SpawnDurationMs = followDefinition?.SpawnDurationMs ?? 0,
                    SpawnOffsetMin = spawnOffsetMin,
                    SpawnOffsetMax = spawnOffsetMax,
                    SpawnZOrder = followDefinition?.ZOrder ?? 0,
                    SourceItemId = followDefinition?.ItemId ?? 0,
                    SourceClientEquipIndex = followDefinition?.ClientEquipIndex ?? -1,
                    SourceCandidateIdentity = candidateIdentity,
                    SourceVariantCount = followFrameVariants?.Count ?? 0,
                    SourceVariantIndices = followDefinition?.EffectVariantIndices
                });

            return followId >= 0;
        }

        private Dictionary<int, Queue<AnimationDisplayerFollowRegistrationEntry>> BuildAnimationDisplayerFollowExistingEntriesByDefinitionKey(
            int registrationKey)
        {
            var existingEntriesByDefinitionKey = new Dictionary<int, Queue<AnimationDisplayerFollowRegistrationEntry>>();
            if (registrationKey == 0
                || !_animationDisplayerFollowAnimationIds.TryGetValue(registrationKey, out List<AnimationDisplayerFollowRegistrationEntry> existingEntries)
                || existingEntries == null)
            {
                return existingEntriesByDefinitionKey;
            }

            for (int i = 0; i < existingEntries.Count; i++)
            {
                AnimationDisplayerFollowRegistrationEntry existingEntry = existingEntries[i];
                if (existingEntry == null || existingEntry.FollowId < 0)
                {
                    continue;
                }

                if (!existingEntriesByDefinitionKey.TryGetValue(existingEntry.DefinitionKey, out Queue<AnimationDisplayerFollowRegistrationEntry> queue))
                {
                    queue = new Queue<AnimationDisplayerFollowRegistrationEntry>();
                    existingEntriesByDefinitionKey[existingEntry.DefinitionKey] = queue;
                }

                queue.Enqueue(existingEntry);
            }

            return existingEntriesByDefinitionKey;
        }

        private static bool TryDequeueAnimationDisplayerFollowRegistrationEntry(
            Dictionary<int, Queue<AnimationDisplayerFollowRegistrationEntry>> existingEntriesByDefinitionKey,
            int definitionKey,
            out AnimationDisplayerFollowRegistrationEntry entry)
        {
            entry = null;
            if (existingEntriesByDefinitionKey != null
                && existingEntriesByDefinitionKey.TryGetValue(definitionKey, out Queue<AnimationDisplayerFollowRegistrationEntry> queue)
                && queue != null
                && queue.Count > 0)
            {
                entry = queue.Dequeue();
                return entry != null && entry.FollowId >= 0;
            }

            return false;
        }

        private void RemoveAnimationDisplayerFollowStaleEntries(
            Dictionary<int, Queue<AnimationDisplayerFollowRegistrationEntry>> existingEntriesByDefinitionKey)
        {
            if (existingEntriesByDefinitionKey == null)
            {
                return;
            }

            foreach (Queue<AnimationDisplayerFollowRegistrationEntry> queue in existingEntriesByDefinitionKey.Values)
            {
                if (queue == null)
                {
                    continue;
                }

                while (queue.Count > 0)
                {
                    AnimationDisplayerFollowRegistrationEntry staleEntry = queue.Dequeue();
                    if (staleEntry?.FollowId >= 0)
                    {
                        _animationEffects.RemoveFollow(staleEntry.FollowId);
                    }
                }
            }
        }

        private static int BuildAnimationDisplayerFollowRegistrationDefinitionKey(
            AnimationDisplayerFollowEquipmentDefinition followDefinition,
            bool relativeEmission,
            int candidateIdentity)
        {
            unchecked
            {
                int key = 17;
                key = (key * 31) + (followDefinition?.ItemId ?? 0);
                key = (key * 31) + (int)(followDefinition?.SourceEquipSlot ?? 0);
                key = (key * 31) + (followDefinition?.ClientEquipIndex ?? -1);
                key = (key * 31) + (relativeEmission ? 1 : 0);
                key = (key * 31) + candidateIdentity;
                return key;
            }
        }

        internal static int BuildAnimationDisplayerFollowRegistrationDefinitionKeyForTesting(
            int itemId,
            EquipSlot slot,
            int clientEquipIndex,
            bool relativeEmission,
            int registrationOrder,
            int candidateIdentity)
        {
            _ = registrationOrder;
            var followDefinition = new AnimationDisplayerFollowEquipmentDefinition
            {
                ItemId = itemId,
                SourceEquipSlot = slot,
                ClientEquipIndex = clientEquipIndex
            };
            return BuildAnimationDisplayerFollowRegistrationDefinitionKey(
                followDefinition,
                relativeEmission,
                candidateIdentity);
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
                initialElapsedMs: 0,
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
            int initialElapsedMs,
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
                       currTickCount,
                       initialElapsedMs) >= 0;
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

        private bool TryRegisterCollisionCustomImpactEffect(int currentTime, double velocityX)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Physics == null
                || !TryGetAnimationDisplayerFrames(
                    "portalcollision:customimpact",
                    CollisionVerticalJumpEffectUol,
                    out List<IDXObject> frames))
            {
                return false;
            }

            Vector2 fallbackPosition = player.Physics.GetPosition();
            bool clientFlip = velocityX != 0d;
            _animationEffects.AddOneTimeAttached(
                frames,
                () => _playerManager?.Player?.Physics?.GetPosition() ?? fallbackPosition,
                () => velocityX != 0d,
                fallbackPosition.X,
                fallbackPosition.Y,
                clientFlip,
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

            string ownerActionName = ResolveAnimationDisplayerRemotePacketOwnedActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            int initialElapsedMs = ResolveAnimationDisplayerRemoteGenericUserStateInitialElapsed(
                presentation.CharacterId,
                AnimationDisplayerGenericUserStateSingleEffectUol,
                ownerActionName,
                ownerFacingRight,
                presentation.CurrentTime,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            Vector2 fallbackPosition = actor.Position;
            bool fallbackFacingRight = ownerFacingRight;
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
                presentation.CurrentTime,
                initialElapsedMs: initialElapsedMs);
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

            string ownerActionName = ResolveAnimationDisplayerRemotePacketOwnedActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            int initialElapsedMs = ResolveAnimationDisplayerRemoteItemMakeInitialElapsed(
                presentation.CharacterId,
                presentation.Success,
                ownerActionName,
                ownerFacingRight,
                presentation.CurrentTime,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            Vector2 fallbackPosition = actor.Position;
            bool fallbackFacingRight = ownerFacingRight;
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
                presentation.CurrentTime,
                initialElapsedMs: initialElapsedMs);
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
            if (string.IsNullOrWhiteSpace(effectUol))
            {
                return;
            }

            string ownerActionName = ResolveAnimationDisplayerRemotePacketOwnedActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            var ownerContext = new AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext(
                presentation.CharacterId,
                presentation.EffectType,
                effectUol,
                ownerActionName,
                ownerFacingRight,
                presentation.CurrentTime);

            if (TryRegisterAnimationDisplayerRemoteUtilityOwnerEffect(presentation, actor, effectUol, ownerContext))
            {
                return;
            }

            if (!TryLoadRemotePacketOwnedStringEffectFrames(effectUol, out List<IDXObject> frames))
            {
                return;
            }

            int initialElapsedMs = ResolveAnimationDisplayerRemotePacketOwnedStringEffectInitialElapsed(ownerContext, frames);
            if (presentation.EffectType == (byte)RemoteUserEffectSubtype.MakerSkill)
            {
                initialElapsedMs = ResolveAnimationDisplayerRemoteMakerSkillInitialElapsed(
                    presentation.CharacterId,
                    effectUol,
                    ownerActionName,
                    ownerFacingRight,
                    presentation.CurrentTime,
                    ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
            }
            Vector2 packetAnchorPosition = presentation.WorldOrigin ?? actor.Position;
            bool fallbackFacingRight = presentation.UseOwnerFacing && actor.FacingRight;
            _animationEffects.AddOneTimeAttached(
                frames,
                () =>
                {
                    if (!presentation.AttachToOwner)
                    {
                        return packetAnchorPosition;
                    }

                    if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor liveActor) == true
                        && liveActor != null)
                    {
                        return liveActor.Position;
                    }

                    return packetAnchorPosition;
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
                packetAnchorPosition.X,
                packetAnchorPosition.Y,
                fallbackFacingRight,
                presentation.CurrentTime,
                initialElapsedMs: initialElapsedMs);
        }

        private bool TryRegisterAnimationDisplayerRemoteUtilityOwnerEffect(
            RemoteUserActorPool.RemoteStringEffectPresentation presentation,
            RemoteUserActor actor,
            string effectUol,
            AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext ownerContext)
        {
            Vector2 packetAnchorPosition = presentation.WorldOrigin ?? actor.Position;
            Func<Vector2> getPosition = () =>
            {
                if (!presentation.AttachToOwner)
                {
                    return packetAnchorPosition;
                }

                if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor liveActor) == true
                    && liveActor != null)
                {
                    return liveActor.Position;
                }

                return packetAnchorPosition;
            };

            IReadOnlyList<AnimationDisplayerReservedEffectMetadata> reservedMetadataEntries =
                EnumerateAnimationDisplayerReservedEffectMetadata(effectUol, ResolveAnimationDisplayerProperty);
            bool consumedReservedEntry = false;
            for (int entryIndex = 0; entryIndex < reservedMetadataEntries.Count; entryIndex++)
            {
                AnimationDisplayerReservedEffectMetadata reservedMetadata = reservedMetadataEntries[entryIndex];
                int registerTime = ResolveAnimationDisplayerReservedStartRegistrationTime(
                    presentation.CurrentTime,
                    reservedMetadata.StartDelayMs);

                if (ShouldDeferAnimationDisplayerReservedOwnerEffect(
                        presentation.CurrentTime,
                        registerTime,
                        reservedMetadata.StartDelayMs))
                {
                    QueueAnimationDisplayerPendingReservedOwnerEffect(
                        presentation,
                        getPosition,
                        effectUol,
                        reservedMetadata,
                        registerTime,
                        ownerContext);
                    consumedReservedEntry = true;
                    continue;
                }

                if (TryApplyAnimationDisplayerReservedRemoteUtilityOwnerEffect(
                        presentation,
                        getPosition,
                        effectUol,
                        reservedMetadata,
                        registerTime,
                        ownerContext))
                {
                    consumedReservedEntry = true;
                }
            }
            if (consumedReservedEntry)
            {
                return true;
            }

            if (TryResolveAnimationDisplayerCatchSuccessFromEffectUol(
                    effectUol,
                    ResolveAnimationDisplayerCatchSuccessHintFromPacketEffect(presentation),
                    out bool catchSuccess))
            {
                return TryRegisterAnimationDisplayerCatch(
                    catchSuccess,
                    presentation.CharacterId,
                    getPosition,
                    presentation.CurrentTime,
                    effectUol,
                    ownerContext,
                    out _);
            }

            if (TryResolveAnimationDisplayerSquibVariantFromEffectUol(effectUol, out int squibVariant))
            {
                return TryRegisterAnimationDisplayerSquib(
                    presentation.CharacterId,
                    getPosition,
                    squibVariant,
                    presentation.CurrentTime,
                    ownerContext,
                    out _);
            }

            if (TryResolveAnimationDisplayerTransformedOnLadderFromEffectUol(effectUol, out bool transformedOnLadder))
            {
                return TryRegisterAnimationDisplayerTransformed(
                    presentation.CharacterId,
                    getPosition,
                    transformedOnLadder,
                    presentation.CurrentTime,
                    ownerContext,
                    out _);
            }

            return false;
        }

        private void QueueAnimationDisplayerPendingReservedOwnerEffect(
            RemoteUserActorPool.RemoteStringEffectPresentation presentation,
            Func<Vector2> getPosition,
            string sourceEffectUol,
            AnimationDisplayerReservedEffectMetadata metadata,
            int registerTime,
            AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext ownerContext)
        {
            if (getPosition == null)
            {
                return;
            }

            _animationDisplayerPendingReservedOwnerEffects.Add(
                new AnimationDisplayerPendingReservedOwnerEffect
                {
                    Presentation = presentation,
                    GetPosition = getPosition,
                    SourceEffectUol = sourceEffectUol,
                    Metadata = metadata,
                    RegisterTime = registerTime,
                    OwnerContext = ownerContext
                });
        }

        internal static bool ShouldDeferAnimationDisplayerReservedOwnerEffect(
            int currentTime,
            int registerTime,
            int startDelayMs)
        {
            return startDelayMs > 0
                && !HasAnimationDisplayerReservedUpdateTimePassed(currentTime, registerTime);
        }

        internal static bool ShouldApplyAnimationDisplayerPendingReservedOwnerEffect(
            int currentTime,
            int registerTime)
        {
            return HasAnimationDisplayerReservedUpdateTimePassed(currentTime, registerTime);
        }

        internal static bool HasAnimationDisplayerReservedUpdateTimeReached(
            int currentTime,
            int targetTime)
        {
            return currentTime == targetTime
                || unchecked((uint)(currentTime - targetTime)) < int.MaxValue;
        }

        internal static bool HasAnimationDisplayerReservedUpdateTimePassed(
            int currentTime,
            int targetTime)
        {
            return currentTime != targetTime
                && unchecked((uint)(currentTime - targetTime)) < int.MaxValue;
        }

        private static AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext ResolveAnimationDisplayerDelayedReservedOwnerContext(
            AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext ownerContext,
            int registerTime)
        {
            return new AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext(
                ownerContext.CharacterId,
                ownerContext.EffectType,
                ownerContext.EffectUol,
                ownerContext.OwnerActionName,
                ownerContext.OwnerFacingRight,
                registerTime);
        }

        private bool TryApplyAnimationDisplayerReservedRemoteUtilityOwnerEffect(
            RemoteUserActorPool.RemoteStringEffectPresentation presentation,
            Func<Vector2> getPosition,
            string sourceEffectUol,
            AnimationDisplayerReservedEffectMetadata metadata,
            int registerTime,
            AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext ownerContext)
        {
            bool consumed = false;
            bool consumeWithoutVisualFallback = ShouldConsumeAnimationDisplayerReservedTypeWithoutVisualFallback(metadata.Type);
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (metadata.Type == 2)
            {
                consumed = true;
                // Client evidence (`RESERVEDINFO::Update`, case 2):
                // transfer requests are CUserLocal-owned side effects.
                if (ShouldApplyAnimationDisplayerReservedLocalTransferFieldOwnerEffect(
                        metadata.Type,
                        presentation.CharacterId,
                        localCharacterId,
                        metadata.FieldId))
                {
                    _ = TryApplyAnimationDisplayerReservedTransferFieldOwnerEffect(metadata.FieldId, registerTime);
                }
            }

            if (metadata.Type == 6)
            {
                consumed = true;
                if (ShouldApplyAnimationDisplayerReservedLocalEmotionOwnerEffect(
                        metadata.Type,
                        presentation.CharacterId,
                        localCharacterId,
                        metadata.EmotionId))
                {
                    _ = _playerManager?.Player?.TryApplyPacketOwnedEmotion(
                        metadata.EmotionId,
                        ResolveAnimationDisplayerReservedEmotionDurationMs(),
                        byItemOption: false,
                        registerTime,
                        out _);
                }
            }

            if (metadata.Type == 4)
            {
                consumed = true;
                // Client evidence (`RESERVEDINFO::Update`, case 4):
                // `CAvatar::SetOneTimeAction` is applied on CUserLocal.
                if (ShouldApplyAnimationDisplayerReservedLocalOneTimeActionOwnerEffect(
                        metadata.Type,
                        presentation.CharacterId,
                        localCharacterId,
                        metadata.ActionName))
                {
                    int authoredActionDurationMs = ResolveAnimationDisplayerReservedLocalUtilityActionDurationMs(
                        metadata.ActionName);
                    int minimumDurationMs = ResolveAnimationDisplayerReservedLocalOneTimeActionMinimumDurationMs(
                        metadata.DurationMs,
                        authoredActionDurationMs);
                    _ = TryApplyAnimationDisplayerReservedLocalUtilityActionOwnerEffect(
                        metadata.ActionName,
                        minimumDurationMs,
                        registerTime);
                }
            }

            if (metadata.Type == 3)
            {
                consumed = true;
                // Client evidence (`RESERVEDINFO::Update`, case 3):
                // AvatarLook is rebuilt for CUserLocal before CAvatar::SetAvatarLook.
                if (ShouldApplyAnimationDisplayerReservedLocalOnlyOwnerEffect(
                        presentation.CharacterId,
                        localCharacterId))
                {
                    _ = TryApplyAnimationDisplayerReservedLocalUtilityEquipOwnerEffect(
                        metadata.EquippedItemIds);
                }
            }

            if (metadata.Type == 5)
            {
                consumed = true;
                // Client evidence (`RESERVEDINFO::Update`, case 5):
                // sound/BGM handoff is local-user owned. WZ direction rows use
                // Effect/*.img sound descriptors here, while Bgm* descriptors remain BGM overrides.
                if (ShouldApplyAnimationDisplayerReservedLocalOnlyOwnerEffect(
                        presentation.CharacterId,
                        localCharacterId))
                {
                    switch (ResolveAnimationDisplayerReservedType5SoundOwnerKind(metadata.Type, metadata.SoundEffectDescriptor))
                    {
                        case AnimationDisplayerReservedType5SoundOwnerKind.BgmOverride:
                            _ = TryApplyAnimationDisplayerReservedBgmOwnerEffect(metadata.SoundEffectDescriptor);
                            break;
                        case AnimationDisplayerReservedType5SoundOwnerKind.SoundEffect:
                            _ = TryPlayAnimationDisplayerReservedSoundEffect(metadata.SoundEffectDescriptor);
                            break;
                    }
                }
            }

            if (consumeWithoutVisualFallback)
            {
                return consumed;
            }

            int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
            if (!ShouldApplyAnimationDisplayerReservedFieldScopedVisual(metadata.FieldId, currentMapId))
            {
                return consumed;
            }

            if (string.IsNullOrWhiteSpace(metadata.VisualEffectUol))
            {
                return consumed;
            }

            string resolvedVisualEffectUol = metadata.VisualEffectUol;
            WzImageProperty visualRootProperty = ResolveAnimationDisplayerPropertyStatic(metadata.VisualEffectUol);
            string[] visualVariantUols = EnumerateAnimationDisplayerFollowEquipmentEffectVariantUols(
                metadata.VisualEffectUol,
                visualRootProperty,
                out _);
            if (visualVariantUols.Length > 0)
            {
                int variantIndex = ResolveAnimationDisplayerReservedVisualVariantIndex(
                    registerTime,
                    visualVariantUols.Length);
                resolvedVisualEffectUol = visualVariantUols[variantIndex];
            }

            if (TryResolveAnimationDisplayerSquibVariantFromEffectUol(
                    resolvedVisualEffectUol,
                    out int reservedSquibVariant))
            {
                bool registered = TryRegisterAnimationDisplayerSquib(
                    presentation.CharacterId,
                    getPosition,
                    reservedSquibVariant,
                    registerTime,
                    ownerContext,
                    out _);
                if (registered)
                {
                    _ = TryPlayAnimationDisplayerReservedSoundEffect(metadata.SoundEffectDescriptor);
                }

                return consumed || registered;
            }

            if (TryResolveAnimationDisplayerTransformedOnLadderFromEffectUol(
                    resolvedVisualEffectUol,
                    out bool reservedTransformedOnLadder))
            {
                bool registered = TryRegisterAnimationDisplayerTransformed(
                    presentation.CharacterId,
                    getPosition,
                    reservedTransformedOnLadder,
                    registerTime,
                    ownerContext,
                    out _);
                if (registered)
                {
                    _ = TryPlayAnimationDisplayerReservedSoundEffect(metadata.SoundEffectDescriptor);
                }

                return consumed || registered;
            }

            if (TryGetAnimationDisplayerFrames(
                    $"reserved:{sourceEffectUol}:{metadata.Type}:{resolvedVisualEffectUol}",
                    resolvedVisualEffectUol,
                    out List<IDXObject> reservedFrames))
            {
                Vector2 anchor = ResolveAnimationDisplayerReservedVisualAnchor(getPosition, metadata);
                if (metadata.Type == 1 && metadata.Width > 0 && metadata.Height > 0)
                {
                    Rectangle area = new(
                        (int)MathF.Round(anchor.X - (metadata.Width / 2f)),
                        (int)MathF.Round(anchor.Y - (metadata.Height / 2f)),
                        Math.Max(1, metadata.Width),
                        Math.Max(1, metadata.Height));
                    int durationMs = Math.Max(1, metadata.DurationMs > 0 ? metadata.DurationMs : 1000);
                    int updateIntervalMs = 100;
                    int updateCount = ResolveAnimationDisplayerReservedAreaBurstAttemptCount(
                        durationMs,
                        updateIntervalMs);
                    int registrationId = _animationEffects.RegisterAreaAnimation(
                        reservedFrames,
                        area,
                        updateIntervalMs,
                        updateCount,
                        updateNextMs: 0,
                        durationMs,
                        registerTime,
                        onSpawn: () => TryPlayAnimationDisplayerReservedSoundEffect(metadata.SoundEffectDescriptor),
                        zOrder: metadata.LayerZ,
                        spawnProbability: ResolveAnimationDisplayerReservedAreaSpawnProbability(metadata.Probability));
                    if (registrationId >= 0)
                    {
                        _packetOwnedAnimationDisplayerAreaAnimationIds.Add(registrationId);
                        return true;
                    }
                }

                if (metadata.PositionX.HasValue && metadata.PositionY.HasValue)
                {
                    Func<Vector2> getFixedPosition = () => ResolveAnimationDisplayerReservedVisualPosition(
                        getPosition(),
                        metadata,
                        registerTime,
                        currTickCount);
                    Vector2 fallbackFixedPosition = getFixedPosition();
                    int initialElapsedMs = ResolveAnimationDisplayerRemotePacketOwnedStringEffectInitialElapsed(
                        ownerContext,
                        reservedFrames);
                    _animationEffects.AddOneTimeAttached(
                        reservedFrames,
                        getFixedPosition,
                        getFlip: null,
                        fallbackFixedPosition.X,
                        fallbackFixedPosition.Y,
                        fallbackFlip: false,
                        registerTime,
                        metadata.LayerZ,
                        initialElapsedMs: initialElapsedMs);
                    _ = TryPlayAnimationDisplayerReservedSoundEffect(metadata.SoundEffectDescriptor);
                    return true;
                }

                Func<Vector2> getAttachedPosition = () =>
                {
                    Vector2 ownerPosition = getPosition();
                    return ResolveAnimationDisplayerReservedVisualPosition(
                        ownerPosition,
                        metadata,
                        registerTime,
                        currTickCount);
                };
                Vector2 fallbackPosition = getAttachedPosition();
                int attachedInitialElapsedMs = ResolveAnimationDisplayerRemotePacketOwnedStringEffectInitialElapsed(
                    ownerContext,
                    reservedFrames);
                _animationEffects.AddOneTimeAttached(
                    reservedFrames,
                    getAttachedPosition,
                    getFlip: null,
                    fallbackPosition.X,
                    fallbackPosition.Y,
                    fallbackFlip: false,
                    registerTime,
                    metadata.LayerZ,
                    initialElapsedMs: attachedInitialElapsedMs);
                _ = TryPlayAnimationDisplayerReservedSoundEffect(metadata.SoundEffectDescriptor);
                return true;
            }

            return consumed;
        }

        private bool TryApplyAnimationDisplayerReservedRemoteUtilityActionOwnerEffect(
            int characterId,
            string actionName,
            int durationMs,
            int currentTime)
        {
            if (characterId <= 0
                || string.IsNullOrWhiteSpace(actionName)
                || _remoteUserPool?.TryGetActor(characterId, out RemoteUserActor actor) != true
                || actor == null)
            {
                return false;
            }

            _animationDisplayerReservedRemoteUtilityActionOwnerStates.TryGetValue(
                characterId,
                out AnimationDisplayerReservedRemoteUtilityActionOwnerState activeState);
            (string previousActionName, bool? previousFacingRight) =
                ResolveAnimationDisplayerReservedRemoteUtilityActionRestoreTarget(
                    actor.ActionName,
                    actor.FacingRight,
                    activeState?.ReservedActionName,
                    activeState?.PreviousActionName,
                    activeState?.PreviousFacingRight);
            if (_remoteUserPool?.TrySetAction(characterId, actionName, null, out _) != true)
            {
                return false;
            }

            int actionDurationMs = ResolveAnimationDisplayerReservedRemoteUtilityActionDurationMs(actor, actionName);
            int restoreDelayMs = ResolveAnimationDisplayerReservedRemoteUtilityActionRestoreDelayMs(
                durationMs,
                actionDurationMs);
            int restoreAtTime = ResolveAnimationDisplayerReservedStartRegistrationTime(currentTime, restoreDelayMs);
            _animationDisplayerReservedRemoteUtilityActionOwnerStates[characterId] =
                new AnimationDisplayerReservedRemoteUtilityActionOwnerState
                {
                    ReservedActionName = actionName,
                    PreviousActionName = previousActionName,
                    PreviousFacingRight = previousFacingRight,
                    RestoreAtTime = restoreAtTime
                };
            return true;
        }

        internal static bool ShouldApplyAnimationDisplayerReservedLocalOneTimeActionOwnerEffect(
            int metadataType,
            int effectCharacterId,
            int localCharacterId,
            string actionName)
        {
            return metadataType == 4
                && ShouldApplyAnimationDisplayerReservedLocalOnlyOwnerEffect(effectCharacterId, localCharacterId)
                && !string.IsNullOrWhiteSpace(actionName);
        }

        internal static bool ShouldApplyAnimationDisplayerReservedLocalTransferFieldOwnerEffect(
            int metadataType,
            int effectCharacterId,
            int localCharacterId,
            int targetFieldId)
        {
            return metadataType == 2
                && targetFieldId > 0
                && ShouldApplyAnimationDisplayerReservedLocalOnlyOwnerEffect(effectCharacterId, localCharacterId);
        }

        internal static bool ShouldApplyAnimationDisplayerReservedLocalEmotionOwnerEffect(
            int metadataType,
            int effectCharacterId,
            int localCharacterId,
            int emotionId)
        {
            return metadataType == 6
                && emotionId >= 0
                && ShouldApplyAnimationDisplayerReservedLocalOnlyOwnerEffect(effectCharacterId, localCharacterId);
        }

        private bool TryApplyAnimationDisplayerReservedLocalUtilityActionOwnerEffect(
            string actionName,
            int minimumDurationMs,
            int currentTime)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            player.TriggerSkillAnimation(
                actionName.Trim(),
                skillId: 0,
                currentTime: currentTime,
                playEffectiveWeaponSfx: false,
                minimumDurationMs: minimumDurationMs);
            return true;
        }

        private int ResolveAnimationDisplayerReservedLocalUtilityActionDurationMs(string actionName)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Assembler == null || string.IsNullOrWhiteSpace(actionName))
            {
                return 0;
            }

            return ResolveAnimationDisplayerReservedUtilityActionDurationMs(
                player.Assembler.GetAnimation(actionName.Trim()));
        }

        private static int ResolveAnimationDisplayerReservedRemoteUtilityActionDurationMs(
            RemoteUserActor actor,
            string actionName)
        {
            if (actor?.Assembler == null || string.IsNullOrWhiteSpace(actionName))
            {
                return 0;
            }

            return ResolveAnimationDisplayerReservedUtilityActionDurationMs(
                actor.Assembler.GetAnimation(actionName));
        }

        private static int ResolveAnimationDisplayerReservedUtilityActionDurationMs(AssembledFrame[] animation)
        {
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            int totalDurationMs = 0;
            for (int index = 0; index < animation.Length; index++)
            {
                totalDurationMs += Math.Max(0, animation[index]?.Duration ?? 0);
            }

            return Math.Max(0, totalDurationMs);
        }

        internal static int ResolveAnimationDisplayerReservedLocalOneTimeActionMinimumDurationMs(
            int metadataDurationMs,
            int actionDurationMs)
        {
            return ResolveAnimationDisplayerReservedRemoteUtilityActionRestoreDelayMs(
                metadataDurationMs,
                actionDurationMs);
        }

        private bool TryApplyAnimationDisplayerReservedTransferFieldOwnerEffect(int targetFieldId, int currentTime)
        {
            int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
            if (!ShouldApplyAnimationDisplayerReservedTransferFieldRequest(targetFieldId, currentMapId))
            {
                return false;
            }

            if (!CanSendTransferFieldRequest(currentTime)
                || !CanQueueKnownCrossMapTransfer(targetFieldId, showFailureMessage: false))
            {
                return false;
            }

            SetTransferFieldExclusiveRequestSent(currentTime);
            return QueueMapTransfer(targetFieldId, null);
        }

        private bool TryApplyAnimationDisplayerReservedBgmOwnerEffect(string descriptor)
        {
            string bgmOverrideName = ResolveAnimationDisplayerReservedBgmOverrideName(descriptor);
            if (string.IsNullOrWhiteSpace(bgmOverrideName))
            {
                return false;
            }

            RequestSpecialFieldBgmOverride(bgmOverrideName);
            return true;
        }

        internal static string ResolveAnimationDisplayerReservedBgmOverrideName(string descriptor)
        {
            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (!TrySplitPacketOwnedClientSoundDescriptor(normalized, out string imageName, out string propertyPath))
            {
                return string.Empty;
            }

            string imageBaseName = imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase)
                ? imageName[..^4]
                : imageName;
            if (!imageBaseName.StartsWith("Bgm", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(propertyPath)
                ? imageBaseName
                : $"{imageBaseName}/{propertyPath}";
        }

        internal static AnimationDisplayerReservedType5SoundOwnerKind ResolveAnimationDisplayerReservedType5SoundOwnerKind(
            int metadataType,
            string descriptor)
        {
            if (metadataType != 5 || string.IsNullOrWhiteSpace(descriptor))
            {
                return AnimationDisplayerReservedType5SoundOwnerKind.None;
            }

            return string.IsNullOrWhiteSpace(ResolveAnimationDisplayerReservedBgmOverrideName(descriptor))
                ? AnimationDisplayerReservedType5SoundOwnerKind.SoundEffect
                : AnimationDisplayerReservedType5SoundOwnerKind.BgmOverride;
        }

        private bool TryApplyAnimationDisplayerReservedLocalUtilityEquipOwnerEffect(
            IReadOnlyList<int> equippedItemIds)
        {
            PlayerCharacter player = _playerManager?.Player;
            CharacterLoader loader = _playerManager?.Loader;
            CharacterBuild build = player?.Build;
            if (equippedItemIds == null
                || equippedItemIds.Count <= 0
                || loader == null
                || build == null)
            {
                return false;
            }

            bool appliedAnyEquipment = false;
            for (int index = 0; index < equippedItemIds.Count; index++)
            {
                int itemId = equippedItemIds[index];
                if (itemId <= 0)
                {
                    continue;
                }

                CharacterPart part = loader.LoadEquipment(itemId);
                if (part?.ItemId <= 0)
                {
                    continue;
                }

                build.Equip(part);
                appliedAnyEquipment = true;
            }

            if (!appliedAnyEquipment)
            {
                return false;
            }

            player.Assembler?.ClearCache();
            return true;
        }

        private static Vector2 ResolveAnimationDisplayerReservedVisualAnchor(
            Func<Vector2> getPosition,
            AnimationDisplayerReservedEffectMetadata metadata)
        {
            if (metadata.PositionX.HasValue && metadata.PositionY.HasValue)
            {
                return new Vector2(
                    metadata.PositionX.Value + metadata.OffsetX + metadata.RelativeOffsetX,
                    metadata.PositionY.Value + metadata.OffsetY + metadata.RelativeOffsetY);
            }

            Vector2 ownerPosition = getPosition();
            return new Vector2(
                ownerPosition.X + metadata.OffsetX + metadata.RelativeOffsetX,
                ownerPosition.Y + metadata.OffsetY + metadata.RelativeOffsetY);
        }

        private bool TryPlayAnimationDisplayerReservedSoundEffect(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return false;
            }

            if (TryResolveAnimationDisplayerReservedEmbeddedEffectSoundUol(
                    descriptor,
                    out string embeddedEffectSoundUol)
                && _soundManager != null)
            {
                WzImageProperty embeddedSound = ResolveAnimationDisplayerPropertyStatic(embeddedEffectSoundUol);
                if (WzInfoTools.GetRealProperty(embeddedSound) is WzBinaryProperty binaryProperty)
                {
                    string soundKey = $"AnimationDisplayerReservedSound:{embeddedEffectSoundUol}";
                    _soundManager.RegisterSound(soundKey, binaryProperty);
                    _soundManager.PlaySound(soundKey);
                    return true;
                }
            }

            string[] defaultImages = { "Game.img", "Field.img", "Bgm.img", "UI.img" };
            for (int i = 0; i < defaultImages.Length; i++)
            {
                if (TryPlayPacketOwnedWzSound(
                        descriptor,
                        defaultImages[i],
                        out _,
                        out _))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryResolveAnimationDisplayerReservedEmbeddedEffectSoundUol(
            string descriptor,
            out string effectSoundUol)
        {
            effectSoundUol = null;
            string normalized = NormalizePacketOwnedClientSoundDescriptor(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            const string effectPrefix = "Effect/";
            if (!normalized.StartsWith(effectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relative = normalized[effectPrefix.Length..];
            string[] segments = relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                return false;
            }

            string imageName = segments[0].EndsWith(".img", StringComparison.OrdinalIgnoreCase)
                ? segments[0]
                : $"{segments[0]}.img";
            string propertyPath = string.Join("/", segments.Skip(1));
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return false;
            }

            effectSoundUol = $"Effect/{imageName}/{propertyPath}";
            return true;
        }

        private void HandleRemoteMobAttackHitEffect(RemoteUserActorPool.RemoteMobAttackHitPresentation presentation)
        {
            if (_animationEffects == null
                || string.IsNullOrWhiteSpace(presentation.EffectPath))
            {
                return;
            }

            string effectUol = NormalizeRemotePacketOwnedStringEffectUol(presentation.EffectPath);
            if (string.IsNullOrWhiteSpace(effectUol)
                || !TryLoadRemotePacketOwnedStringEffectFrames(effectUol, out List<IDXObject> frames))
            {
                return;
            }

            bool ownerFacingRight = presentation.FacingRight;
            string ownerActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
            if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor actor) == true
                && actor != null)
            {
                ownerFacingRight = actor.FacingRight;
                ownerActionName = ResolveAnimationDisplayerRemotePacketOwnedActionName(actor);
            }

            int initialElapsedMs = ResolveAnimationDisplayerRemoteMobAttackHitInitialElapsed(
                presentation.CharacterId,
                presentation.MobTemplateId,
                presentation.AttackIndex,
                effectUol,
                ownerActionName,
                ownerFacingRight,
                presentation.AttachToOwner,
                presentation.CurrentTime,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));

            if (presentation.AttachToOwner)
            {
                Vector2 fallbackPosition = presentation.Position;
                bool fallbackFacingRight = presentation.FacingRight;
                Vector2 attachedOffset = Vector2.Zero;
                if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor initialActor) == true
                    && initialActor != null)
                {
                    attachedOffset = fallbackPosition - initialActor.Position;
                }
                _animationEffects.AddOneTimeAttached(
                    frames,
                    () =>
                    {
                        if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor liveActor) == true
                            && liveActor != null)
                        {
                            return liveActor.Position + attachedOffset;
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
                    presentation.CurrentTime,
                    initialElapsedMs: initialElapsedMs);
                return;
            }

            bool fallbackFlip = !presentation.FacingRight;
            _animationEffects.AddOneTimeAttached(
                frames,
                () => presentation.Position,
                () => !presentation.FacingRight,
                presentation.Position.X,
                presentation.Position.Y,
                fallbackFlip,
                presentation.CurrentTime,
                initialElapsedMs: initialElapsedMs);
        }

        private void HandleRemoteGrenadeEffect(RemoteUserActorPool.RemoteGrenadePresentation presentation)
        {
            if (_playerManager?.SkillLoader == null)
            {
                return;
            }

            SkillData skill = _playerManager.SkillLoader.LoadSkill(presentation.SkillId);
            ProjectileData projectile = skill?.Projectile;
            int ownerLevel = Math.Max(1, presentation.OwnerLevel);
            SkillAnimation ballAnimation = projectile?.ResolveGetBallLikeAnimation(
                Math.Max(1, presentation.SkillLevel),
                ownerLevel,
                flip: presentation.Impact.X < 0,
                skill?.MaxLevel ?? 0);
            if (ballAnimation?.Frames?.Count <= 0)
            {
                return;
            }

            SkillAnimation explosionAnimation = ResolveRemoteGrenadeExplosionAnimation(skill, projectile);
            int flightStartTime = RemoteUserActorPool.ResolveRemoteGrenadeFlightStartTimeForParity(presentation);
            int explosionTime = RemoteUserActorPool.ResolveRemoteGrenadeExplosionTimeForParity(presentation, ballAnimation);
            int expireTime = RemoteUserActorPool.ResolveRemoteGrenadeExpireTimeForParity(
                presentation,
                ballAnimation,
                explosionAnimation);

            // CUser::ThrowGrenade appends each spawned CGrenade into m_lpGrenade without
            // replacing prior active grenades from the same user/skill.
            _animationDisplayerRemoteGrenadeActors.Add(new AnimationDisplayerRemoteGrenadeActor
            {
                CharacterId = presentation.CharacterId,
                SkillId = presentation.SkillId,
                StartTime = presentation.CurrentTime,
                FlightStartTime = flightStartTime,
                ExplosionTime = explosionTime,
                Origin = new Vector2(presentation.Target.X, presentation.Target.Y),
                FlightOrigin = RemoteUserActorPool.ResolveRemoteGrenadeFlightOriginForParity(presentation),
                Impact = presentation.Impact,
                FlightRenderOffset = RemoteUserActorPool.ResolveRemoteGrenadeRenderOffsetForParity(
                    presentation,
                    includeRotateLayerCollisionCompensation: true),
                ExplosionRenderOffset = RemoteUserActorPool.ResolveRemoteGrenadeRenderOffsetForParity(
                    presentation,
                    includeRotateLayerCollisionCompensation: false),
                DragX = presentation.DragX,
                DragY = presentation.DragY,
                GravityFree = presentation.GravityFree,
                FacingRight = RemoteUserActorPool.ResolveRemoteGrenadeFacingForParity(presentation),
                BallAnimation = ballAnimation,
                ExplosionAnimation = explosionAnimation,
                ExpireTime = expireTime
            });
        }

        private static SkillAnimation ResolveRemoteGrenadeExplosionAnimation(SkillData skill, ProjectileData projectile)
        {
            if (skill?.SkillId == 4341003)
            {
                return skill.Effect?.Frames?.Count > 0
                    ? skill.Effect
                    : skill.AvatarOverlayEffect?.Frames?.Count > 0
                        ? skill.AvatarOverlayEffect
                        : projectile?.HitAnimation;
            }

            return projectile?.HitAnimation?.Frames?.Count > 0
                ? projectile.HitAnimation
                : projectile?.ExplosionAnimation;
        }

        private void UpdateAnimationDisplayerRemoteGrenades(int currentTime)
        {
            UpdateAnimationDisplayerPendingReservedOwnerEffects(currentTime);
            UpdateAnimationDisplayerReservedRemoteUtilityActionOwners(currentTime);
            if (_animationDisplayerRemoteGrenadeActors.Count == 0)
            {
                return;
            }

            // CGrenade lifetime is packet-owned once spawned. Keep it alive until
            // its own animation/expiry window ends instead of owner visibility state.
            _animationDisplayerRemoteGrenadeActors.RemoveAll(actor =>
                actor == null
                || actor.IsExpired(currentTime));
        }

        private void UpdateAnimationDisplayerPendingReservedOwnerEffects(int currentTime)
        {
            if (_animationDisplayerPendingReservedOwnerEffects.Count <= 0)
            {
                return;
            }

            for (int i = _animationDisplayerPendingReservedOwnerEffects.Count - 1; i >= 0; i--)
            {
                AnimationDisplayerPendingReservedOwnerEffect pending = _animationDisplayerPendingReservedOwnerEffects[i];
                if (pending == null)
                {
                    _animationDisplayerPendingReservedOwnerEffects.RemoveAt(i);
                    continue;
                }

                if (!ShouldApplyAnimationDisplayerPendingReservedOwnerEffect(currentTime, pending.RegisterTime))
                {
                    continue;
                }

                TryApplyAnimationDisplayerReservedRemoteUtilityOwnerEffect(
                    pending.Presentation,
                    pending.GetPosition,
                    pending.SourceEffectUol,
                    pending.Metadata,
                    currentTime,
                    ResolveAnimationDisplayerDelayedReservedOwnerContext(pending.OwnerContext, currentTime));
                _animationDisplayerPendingReservedOwnerEffects.RemoveAt(i);
            }
        }

        private void UpdateAnimationDisplayerReservedRemoteUtilityActionOwners(int currentTime)
        {
            if (_animationDisplayerReservedRemoteUtilityActionOwnerStates.Count <= 0 || _remoteUserPool == null)
            {
                return;
            }

            var resolvedOwners = new List<int>();
            foreach (KeyValuePair<int, AnimationDisplayerReservedRemoteUtilityActionOwnerState> entry in _animationDisplayerReservedRemoteUtilityActionOwnerStates)
            {
                int characterId = entry.Key;
                AnimationDisplayerReservedRemoteUtilityActionOwnerState state = entry.Value;
                if (state == null
                    || !HasAnimationDisplayerReservedUpdateTimeReached(currentTime, state.RestoreAtTime))
                {
                    continue;
                }

                resolvedOwners.Add(characterId);
                if (_remoteUserPool.TryGetActor(characterId, out RemoteUserActor actor) != true
                    || actor == null
                    || !string.Equals(actor.ActionName, state.ReservedActionName, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(state.PreviousActionName))
                {
                    continue;
                }

                _ = _remoteUserPool.TrySetAction(
                    characterId,
                    state.PreviousActionName,
                    state.PreviousFacingRight,
                    out _);
            }

            for (int i = 0; i < resolvedOwners.Count; i++)
            {
                _animationDisplayerReservedRemoteUtilityActionOwnerStates.Remove(resolvedOwners[i]);
            }
        }

        private void DrawAnimationDisplayerRemoteGrenades(
            SpriteBatch spriteBatch,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (spriteBatch == null || _animationDisplayerRemoteGrenadeActors.Count == 0)
            {
                return;
            }

            foreach (AnimationDisplayerRemoteGrenadeActor actor in _animationDisplayerRemoteGrenadeActors)
            {
                DrawAnimationDisplayerRemoteGrenade(
                    spriteBatch,
                    actor,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    currentTime);
            }
        }

        private static void DrawAnimationDisplayerRemoteGrenade(
            SpriteBatch spriteBatch,
            AnimationDisplayerRemoteGrenadeActor actor,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (actor == null)
            {
                return;
            }

            bool isExploding = actor.IsExploding(currentTime);
            bool isPreFlight = !isExploding && currentTime < actor.FlightStartTime;
            SkillAnimation animation = isExploding
                ? actor.ExplosionAnimation
                : actor.BallAnimation;
            int animationTime = isExploding
                ? currentTime - actor.ExplosionTime
                : isPreFlight
                    ? currentTime - actor.StartTime
                    : currentTime - actor.FlightStartTime;
            SkillFrame frame = animation?.GetFrameAtTime(Math.Max(0, animationTime));
            if (frame?.Texture == null)
            {
                return;
            }

            Vector2 position;
            if (isExploding)
            {
                position = RemoteUserActorPool.ResolveRemoteGrenadePositionForParity(
                    actor.FlightOrigin,
                    actor.Impact,
                    actor.ExplosionTime - actor.FlightStartTime,
                    actor.ExplosionTime - actor.FlightStartTime,
                    actor.DragX,
                    actor.DragY,
                    actor.GravityFree);
            }
            else if (isPreFlight)
            {
                // CGrenade exists during init delay before vec-control flight starts.
                position = actor.Origin;
            }
            else
            {
                position = RemoteUserActorPool.ResolveRemoteGrenadePositionForParity(
                    actor.FlightOrigin,
                    actor.Impact,
                    currentTime - actor.FlightStartTime,
                    actor.ExplosionTime - actor.FlightStartTime,
                    actor.DragX,
                    actor.DragY,
                    actor.GravityFree);
            }
            bool shouldFlip = actor.FacingRight ^ frame.Flip;
            position += isExploding
                ? actor.ExplosionRenderOffset
                : actor.FlightRenderOffset;
            int screenX = (int)MathF.Round(position.X) - mapShiftX + centerX;
            int screenY = (int)MathF.Round(position.Y) - mapShiftY + centerY;

            frame.Texture.DrawBackground(
                spriteBatch,
                null,
                null,
                shouldFlip ? screenX + frame.Origin.X - frame.Bounds.Width : screenX - frame.Origin.X,
                screenY - frame.Origin.Y,
                Color.White,
                shouldFlip,
                null);
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
                initialElapsedMs: actor.PacketOwnedQuestDeliveryEffectAppliedTime != int.MinValue
                    ? ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currTickCount, actor.PacketOwnedQuestDeliveryEffectAppliedTime)
                    : 0,
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

            _animationDisplayerRemoteGenericUserStateOwnerStates.Remove(characterId);
            _animationDisplayerRemoteMakerSkillOwnerStates.Remove(characterId);
            _animationDisplayerRemoteItemMakeOwnerStates.Remove(characterId);
            _animationDisplayerRemoteUpgradeTombOwnerStates.Remove(characterId);
            _animationDisplayerRemotePacketOwnedStringEffectOwnerStates.Remove(characterId);
            _animationDisplayerRemoteMobAttackHitOwnerStates.Remove(characterId);
            _animationDisplayerRemoteHookingChainOwnerStates.Remove(characterId);
            _animationDisplayerRemoteSkillUseOwnerStates.Remove(characterId);
            _animationEffects.RemoveUserState(characterId, currTickCount);
            ClearAnimationDisplayerRemoteQuestDeliveryOwner(characterId);
        }

        private static string ResolveAnimationDisplayerRemotePacketOwnedActionName(RemoteUserActor actor)
        {
            if (!string.IsNullOrWhiteSpace(actor?.ActionName))
            {
                return actor.ActionName;
            }

            return CharacterPart.GetActionString(CharacterAction.Stand1);
        }

        private string ResolveAnimationDisplayerLocalPacketOwnedActionName(int characterId)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (characterId > 0
                && player?.Build?.Id == characterId
                && !string.IsNullOrWhiteSpace(player.CurrentActionName))
            {
                return player.CurrentActionName;
            }

            return CharacterPart.GetActionString(CharacterAction.Stand1);
        }

        private bool ResolveAnimationDisplayerLocalPacketOwnedFacingRight(int characterId)
        {
            PlayerCharacter player = _playerManager?.Player;
            return characterId > 0 && player?.Build?.Id == characterId
                ? player.FacingRight
                : true;
        }

        private int ResolveAnimationDisplayerRemoteGenericUserStateInitialElapsed(
            int characterId,
            string effectUol,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0
                || string.IsNullOrWhiteSpace(effectUol)
                || durationMs <= 0)
            {
                return 0;
            }

            int initialElapsedMs = 0;
            if (_animationDisplayerRemoteGenericUserStateOwnerStates.TryGetValue(
                    characterId,
                    out AnimationDisplayerRemoteGenericUserStateOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerRemoteGenericUserStateRestoreElapsedCore(
                    existingState.EffectUol,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    effectUol,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            _animationDisplayerRemoteGenericUserStateOwnerStates[characterId] =
                new AnimationDisplayerRemoteGenericUserStateOwnerState
                {
                    EffectUol = effectUol,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };

            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerRemoteItemMakeInitialElapsed(
            int characterId,
            bool success,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0 || durationMs <= 0)
            {
                return 0;
            }

            int initialElapsedMs = 0;
            if (_animationDisplayerRemoteItemMakeOwnerStates.TryGetValue(
                    characterId,
                    out AnimationDisplayerRemoteItemMakeOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerRemoteItemMakeRestoreElapsedCore(
                    existingState.Success,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    success,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            _animationDisplayerRemoteItemMakeOwnerStates[characterId] =
                new AnimationDisplayerRemoteItemMakeOwnerState
                {
                    Success = success,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };

            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerRemoteMakerSkillInitialElapsed(
            int characterId,
            string effectUol,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0
                || string.IsNullOrWhiteSpace(effectUol)
                || durationMs <= 0)
            {
                return 0;
            }

            int initialElapsedMs = 0;
            if (_animationDisplayerRemoteMakerSkillOwnerStates.TryGetValue(
                    characterId,
                    out AnimationDisplayerRemoteMakerSkillOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerRemoteMakerSkillRestoreElapsedCore(
                    existingState.EffectUol,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    effectUol,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            _animationDisplayerRemoteMakerSkillOwnerStates[characterId] =
                new AnimationDisplayerRemoteMakerSkillOwnerState
                {
                    EffectUol = effectUol,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };

            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerRemoteUpgradeTombInitialElapsed(
            int characterId,
            int itemId,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0 || itemId <= 0 || durationMs <= 0)
            {
                return 0;
            }

            int initialElapsedMs = 0;
            if (_animationDisplayerRemoteUpgradeTombOwnerStates.TryGetValue(
                    characterId,
                    out AnimationDisplayerRemoteUpgradeTombOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerRemoteUpgradeTombRestoreElapsedCore(
                    existingState.ItemId,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    itemId,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            _animationDisplayerRemoteUpgradeTombOwnerStates[characterId] =
                new AnimationDisplayerRemoteUpgradeTombOwnerState
                {
                    ItemId = itemId,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };

            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerRemoteMobAttackHitInitialElapsed(
            int characterId,
            int mobTemplateId,
            sbyte attackIndex,
            string effectUol,
            string ownerActionName,
            bool ownerFacingRight,
            bool attachToOwner,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0
                || mobTemplateId <= 0
                || string.IsNullOrWhiteSpace(effectUol)
                || durationMs <= 0)
            {
                return 0;
            }

            int initialElapsedMs = 0;
            if (_animationDisplayerRemoteMobAttackHitOwnerStates.TryGetValue(
                    characterId,
                    out AnimationDisplayerRemoteMobAttackHitOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerRemoteMobAttackHitRestoreElapsedCore(
                    existingState.MobTemplateId,
                    existingState.AttackIndex,
                    existingState.EffectUol,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AttachToOwner,
                    existingState.AnimationStartTime,
                    mobTemplateId,
                    attackIndex,
                    effectUol,
                    ownerActionName,
                    ownerFacingRight,
                    attachToOwner,
                    currentTime,
                    durationMs);
            }

            _animationDisplayerRemoteMobAttackHitOwnerStates[characterId] =
                new AnimationDisplayerRemoteMobAttackHitOwnerState
                {
                    MobTemplateId = mobTemplateId,
                    AttackIndex = attackIndex,
                    EffectUol = effectUol,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AttachToOwner = attachToOwner,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };

            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerRemotePacketOwnedStringEffectInitialElapsed(
            AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext? ownerContext,
            IReadOnlyList<IDXObject> frames)
        {
            if (!ownerContext.HasValue)
            {
                return 0;
            }

            AnimationDisplayerRemotePacketOwnedStringEffectOwnerContext context = ownerContext.Value;
            return ResolveAnimationDisplayerRemotePacketOwnedStringEffectRestoreElapsed(
                context.CharacterId,
                context.EffectType,
                context.EffectUol,
                context.OwnerActionName,
                context.OwnerFacingRight,
                context.CurrentTime,
                ResolveAnimationDisplayerOneTimeFrameDurationMs(frames));
        }

        private int ResolveAnimationDisplayerRemoteHookingChainInitialElapsed(
            int characterId,
            int skillId,
            int mobObjectId,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0
                || skillId <= 0
                || mobObjectId <= 0
                || durationMs <= 0)
            {
                return 0;
            }

            string ownerSlotKey = BuildAnimationDisplayerRemoteHookingChainOwnerSlotKey(skillId, mobObjectId);
            if (string.IsNullOrWhiteSpace(ownerSlotKey))
            {
                return 0;
            }

            if (!_animationDisplayerRemoteHookingChainOwnerStates.TryGetValue(
                    characterId,
                    out Dictionary<string, AnimationDisplayerRemoteHookingChainOwnerState> ownerStates)
                || ownerStates == null)
            {
                ownerStates = new Dictionary<string, AnimationDisplayerRemoteHookingChainOwnerState>(
                    StringComparer.OrdinalIgnoreCase);
                _animationDisplayerRemoteHookingChainOwnerStates[characterId] = ownerStates;
            }

            int initialElapsedMs = 0;
            if (ownerStates.TryGetValue(
                    ownerSlotKey,
                    out AnimationDisplayerRemoteHookingChainOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerRemoteHookingChainRestoreElapsedCore(
                    existingState.SkillId,
                    existingState.MobObjectId,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    skillId,
                    mobObjectId,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            ownerStates[ownerSlotKey] =
                new AnimationDisplayerRemoteHookingChainOwnerState
                {
                    SkillId = skillId,
                    MobObjectId = mobObjectId,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };
            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerRemoteSkillUseInitialElapsed(
            int characterId,
            int skillId,
            string branchName,
            int variantIndex,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0
                || skillId <= 0
                || string.IsNullOrWhiteSpace(branchName)
                || variantIndex < 0
                || durationMs <= 0)
            {
                return 0;
            }

            string ownerSlotKey = BuildAnimationDisplayerRemoteSkillUseOwnerSlotKey(
                skillId,
                branchName,
                variantIndex);
            if (string.IsNullOrWhiteSpace(ownerSlotKey))
            {
                return 0;
            }

            if (!_animationDisplayerRemoteSkillUseOwnerStates.TryGetValue(
                    characterId,
                    out Dictionary<string, AnimationDisplayerRemoteSkillUseOwnerState> ownerStates)
                || ownerStates == null)
            {
                ownerStates = new Dictionary<string, AnimationDisplayerRemoteSkillUseOwnerState>(
                    StringComparer.OrdinalIgnoreCase);
                _animationDisplayerRemoteSkillUseOwnerStates[characterId] = ownerStates;
            }

            int initialElapsedMs = 0;
            if (ownerStates.TryGetValue(
                    ownerSlotKey,
                    out AnimationDisplayerRemoteSkillUseOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerRemoteSkillUseRestoreElapsedCore(
                    existingState.SkillId,
                    existingState.BranchName,
                    existingState.VariantIndex,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    skillId,
                    branchName,
                    variantIndex,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            ownerStates[ownerSlotKey] =
                new AnimationDisplayerRemoteSkillUseOwnerState
                {
                    SkillId = skillId,
                    BranchName = branchName,
                    VariantIndex = variantIndex,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };
            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerPacketOwnedMonsterBookCardGetInitialElapsed(
            int characterId,
            int itemId,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0 || itemId <= 0 || durationMs <= 0)
            {
                return 0;
            }

            if (!_animationDisplayerPacketOwnedMonsterBookCardGetOwnerStates.TryGetValue(
                    characterId,
                    out Dictionary<int, AnimationDisplayerPacketOwnedMonsterBookCardGetOwnerState> ownerStates)
                || ownerStates == null)
            {
                ownerStates = new Dictionary<int, AnimationDisplayerPacketOwnedMonsterBookCardGetOwnerState>();
                _animationDisplayerPacketOwnedMonsterBookCardGetOwnerStates[characterId] = ownerStates;
            }

            int initialElapsedMs = 0;
            if (ownerStates.TryGetValue(itemId, out AnimationDisplayerPacketOwnedMonsterBookCardGetOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerPacketOwnedMonsterBookCardGetRestoreElapsedCore(
                    existingState.ItemId,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    itemId,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            ownerStates[itemId] =
                new AnimationDisplayerPacketOwnedMonsterBookCardGetOwnerState
                {
                    ItemId = itemId,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };
            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerLocalPacketOwnedBasicOneTimeInitialElapsed(
            int characterId,
            string ownerSlotKey,
            string effectUol,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0
                || string.IsNullOrWhiteSpace(ownerSlotKey)
                || string.IsNullOrWhiteSpace(effectUol)
                || durationMs <= 0)
            {
                return 0;
            }

            if (!_animationDisplayerLocalPacketOwnedBasicOneTimeOwnerStates.TryGetValue(
                    characterId,
                    out Dictionary<string, AnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerState> ownerStates)
                || ownerStates == null)
            {
                ownerStates = new Dictionary<string, AnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerState>(
                    StringComparer.OrdinalIgnoreCase);
                _animationDisplayerLocalPacketOwnedBasicOneTimeOwnerStates[characterId] = ownerStates;
            }

            int initialElapsedMs = 0;
            if (ownerStates.TryGetValue(
                    ownerSlotKey,
                    out AnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerLocalPacketOwnedBasicOneTimeRestoreElapsedCore(
                    existingState.EffectUol,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    effectUol,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            ownerStates[ownerSlotKey] =
                new AnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerState
                {
                    EffectUol = effectUol,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };
            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerPacketOwnedMobOneTimeInitialElapsed(
            string ownerKind,
            int mobTemplateId,
            string attackAction,
            string effectUol,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (string.IsNullOrWhiteSpace(ownerKind)
                || mobTemplateId <= 0
                || string.IsNullOrWhiteSpace(attackAction)
                || string.IsNullOrWhiteSpace(effectUol)
                || durationMs <= 0)
            {
                return 0;
            }

            string ownerSlotKey = BuildAnimationDisplayerPacketOwnedMobOneTimeOwnerSlotKey(
                ownerKind,
                mobTemplateId,
                attackAction,
                effectUol);
            if (string.IsNullOrWhiteSpace(ownerSlotKey))
            {
                return 0;
            }

            int initialElapsedMs = 0;
            if (_animationDisplayerPacketOwnedMobOneTimeOwnerStates.TryGetValue(
                    ownerSlotKey,
                    out AnimationDisplayerPacketOwnedMobOneTimeOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerPacketOwnedMobOneTimeRestoreElapsedCore(
                    existingState.OwnerKind,
                    existingState.MobTemplateId,
                    existingState.AttackAction,
                    existingState.EffectUol,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    ownerKind,
                    mobTemplateId,
                    attackAction,
                    effectUol,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            _animationDisplayerPacketOwnedMobOneTimeOwnerStates[ownerSlotKey] =
                new AnimationDisplayerPacketOwnedMobOneTimeOwnerState
                {
                    OwnerKind = ownerKind,
                    MobTemplateId = mobTemplateId,
                    AttackAction = attackAction,
                    EffectUol = effectUol,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };
            return initialElapsedMs;
        }

        private int ResolveAnimationDisplayerRemotePacketOwnedStringEffectRestoreElapsed(
            int characterId,
            byte effectType,
            string effectUol,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            int durationMs)
        {
            if (characterId <= 0
                || string.IsNullOrWhiteSpace(effectUol)
                || durationMs <= 0)
            {
                return 0;
            }

            string ownerSlotKey = BuildAnimationDisplayerRemotePacketOwnedStringEffectOwnerSlotKey(
                effectType,
                effectUol);
            if (string.IsNullOrWhiteSpace(ownerSlotKey))
            {
                return 0;
            }

            if (!_animationDisplayerRemotePacketOwnedStringEffectOwnerStates.TryGetValue(
                    characterId,
                    out Dictionary<string, AnimationDisplayerRemotePacketOwnedStringEffectOwnerState> ownerStates)
                || ownerStates == null)
            {
                ownerStates = new Dictionary<string, AnimationDisplayerRemotePacketOwnedStringEffectOwnerState>(
                    StringComparer.OrdinalIgnoreCase);
                _animationDisplayerRemotePacketOwnedStringEffectOwnerStates[characterId] = ownerStates;
            }

            int initialElapsedMs = 0;
            if (ownerStates.TryGetValue(
                    ownerSlotKey,
                    out AnimationDisplayerRemotePacketOwnedStringEffectOwnerState existingState)
                && existingState != null)
            {
                initialElapsedMs = ResolveAnimationDisplayerRemotePacketOwnedStringEffectRestoreElapsedCore(
                    existingState.EffectType,
                    existingState.EffectUol,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    existingState.AnimationStartTime,
                    effectType,
                    effectUol,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    durationMs);
            }

            ownerStates[ownerSlotKey] =
                new AnimationDisplayerRemotePacketOwnedStringEffectOwnerState
                {
                    EffectType = effectType,
                    EffectUol = effectUol,
                    OwnerActionName = ownerActionName,
                    OwnerFacingRight = ownerFacingRight,
                    AnimationStartTime = unchecked(currentTime - initialElapsedMs),
                    DurationMs = durationMs
                };
            return initialElapsedMs;
        }

        private static string BuildAnimationDisplayerRemotePacketOwnedStringEffectOwnerSlotKey(
            byte effectType,
            string effectUol)
        {
            return string.IsNullOrWhiteSpace(effectUol)
                ? string.Empty
                : $"{effectType}:{effectUol.Trim()}";
        }

        private static string BuildAnimationDisplayerRemoteHookingChainOwnerSlotKey(
            int skillId,
            int mobObjectId)
        {
            return skillId <= 0 || mobObjectId <= 0
                ? string.Empty
                : $"{skillId}:{mobObjectId}";
        }

        private static string BuildAnimationDisplayerRemoteSkillUseOwnerSlotKey(
            int skillId,
            string branchName,
            int variantIndex)
        {
            return skillId <= 0 || string.IsNullOrWhiteSpace(branchName) || variantIndex < 0
                ? string.Empty
                : $"{skillId}:{branchName.Trim()}:{variantIndex}";
        }

        private static string BuildAnimationDisplayerPacketOwnedMonsterBookCardGetOwnerSlotKey(int itemId)
        {
            return itemId <= 0
                ? string.Empty
                : itemId.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildAnimationDisplayerLocalPacketOwnedBuffItemUseOwnerSlotKey(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerSlotKey(
                "aux.packetOwnedBuffItemUse.oneTime",
                effectUol);
        }

        private static string BuildAnimationDisplayerLocalPacketOwnedItemUnreleaseOwnerSlotKey(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerSlotKey(
                "aux.packetOwnedItemUnrelease.oneTime",
                effectUol);
        }

        private static string BuildAnimationDisplayerLocalPacketOwnedCombatFeedbackOwnerSlotKey(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerSlotKey(
                "aux.packetOwnedCombatFeedback.oneTime",
                effectUol);
        }

        private static string BuildAnimationDisplayerLocalPacketOwnedCoolOwnerSlotKey(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerSlotKey(
                "aux.packetOwnedCool.oneTime",
                effectUol);
        }

        private static string BuildAnimationDisplayerPacketOwnedFallingOwnerSlotKey(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerSlotKey(
                "aux.packetOwnedFalling.oneTime",
                effectUol);
        }

        private static string BuildAnimationDisplayerPacketOwnedExplosionOwnerSlotKey(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerSlotKey(
                "aux.packetOwnedExplosion.oneTime",
                effectUol);
        }

        private static string BuildAnimationDisplayerLocalPacketOwnedBasicOneTimeOwnerSlotKey(
            string ownerFamily,
            string effectUol)
        {
            return string.IsNullOrWhiteSpace(ownerFamily) || string.IsNullOrWhiteSpace(effectUol)
                ? string.Empty
                : $"{ownerFamily.Trim()}:{effectUol.Trim()}";
        }

        private static string BuildAnimationDisplayerPacketOwnedMobOneTimeOwnerSlotKey(
            string ownerKind,
            int mobTemplateId,
            string attackAction,
            string effectUol)
        {
            return string.IsNullOrWhiteSpace(ownerKind)
                   || mobTemplateId <= 0
                   || string.IsNullOrWhiteSpace(attackAction)
                   || string.IsNullOrWhiteSpace(effectUol)
                ? string.Empty
                : $"{ownerKind.Trim()}:{mobTemplateId.ToString(CultureInfo.InvariantCulture)}:{attackAction.Trim()}:{effectUol.Trim()}";
        }

        internal static bool ShouldRegisterAnimationDisplayerPacketOwnedMonsterBookCardGetForChangedPickup(
            ISet<int> registeredItemIds,
            int itemId)
        {
            if (registeredItemIds == null || itemId <= 0)
            {
                return false;
            }

            return registeredItemIds.Add(itemId);
        }

        internal static int ResolveAnimationDisplayerRemoteHookingChainDurationMsForTesting(int attackWindowMs)
        {
            return ResolveAnimationDisplayerRemoteHookingChainDurationMs(attackWindowMs);
        }

        private static int ResolveAnimationDisplayerRemoteHookingChainDurationMs(int attackWindowMs)
        {
            return Math.Max(0, attackWindowMs - 100) + 1000;
        }

        private static int ResolveAnimationDisplayerOneTimeFrameDurationMs(IReadOnlyList<IDXObject> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int durationMs = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                durationMs += Math.Max(1, frames[i]?.Delay ?? 0);
            }

            return Math.Max(0, durationMs);
        }

        private static int ResolveAnimationDisplayerRemoteItemMakeRestoreElapsedCore(
            bool previousSuccess,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            bool currentSuccess,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || previousSuccess != currentSuccess
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerRemoteGenericUserStateRestoreElapsedCore(
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || !string.Equals(previousEffectUol, currentEffectUol, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerRemoteMakerSkillRestoreElapsedCore(
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || !string.Equals(previousEffectUol, currentEffectUol, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerRemoteUpgradeTombRestoreElapsedCore(
            int previousItemId,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            int currentItemId,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || previousItemId != currentItemId
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerRemoteMobAttackHitRestoreElapsedCore(
            int previousMobTemplateId,
            sbyte previousAttackIndex,
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            bool previousAttachToOwner,
            int previousAnimationStartTime,
            int currentMobTemplateId,
            sbyte currentAttackIndex,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            bool currentAttachToOwner,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || previousMobTemplateId != currentMobTemplateId
                || previousAttackIndex != currentAttackIndex
                || !string.Equals(previousEffectUol, currentEffectUol, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight
                || previousAttachToOwner != currentAttachToOwner)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerRemotePacketOwnedStringEffectRestoreElapsedCore(
            byte previousEffectType,
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            byte currentEffectType,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || previousEffectType != currentEffectType
                || !string.Equals(previousEffectUol, currentEffectUol, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerRemoteHookingChainRestoreElapsedCore(
            int previousSkillId,
            int previousMobObjectId,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            int currentSkillId,
            int currentMobObjectId,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || previousSkillId != currentSkillId
                || previousMobObjectId != currentMobObjectId
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerRemoteSkillUseRestoreElapsedCore(
            int previousSkillId,
            string previousBranchName,
            int previousVariantIndex,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            int currentSkillId,
            string currentBranchName,
            int currentVariantIndex,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || previousSkillId != currentSkillId
                || previousVariantIndex != currentVariantIndex
                || !string.Equals(previousBranchName, currentBranchName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerPacketOwnedMonsterBookCardGetRestoreElapsedCore(
            int previousItemId,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            int currentItemId,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || previousItemId != currentItemId
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerLocalPacketOwnedBasicOneTimeRestoreElapsedCore(
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || !string.Equals(previousEffectUol, currentEffectUol, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousActionName, currentActionName, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        private static int ResolveAnimationDisplayerPacketOwnedMobOneTimeRestoreElapsedCore(
            string previousOwnerKind,
            int previousMobTemplateId,
            string previousAttackAction,
            string previousEffectUol,
            bool previousFacingRight,
            int previousAnimationStartTime,
            string currentOwnerKind,
            int currentMobTemplateId,
            string currentAttackAction,
            string currentEffectUol,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            if (durationMs <= 0
                || previousAnimationStartTime == int.MinValue
                || previousMobTemplateId != currentMobTemplateId
                || !string.Equals(previousOwnerKind, currentOwnerKind, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousAttackAction, currentAttackAction, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousEffectUol, currentEffectUol, StringComparison.OrdinalIgnoreCase)
                || previousFacingRight != currentFacingRight)
            {
                return 0;
            }

            int elapsedMs = ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, previousAnimationStartTime);
            return elapsedMs < durationMs ? elapsedMs : 0;
        }

        internal static int ResolveAnimationDisplayerOneTimeFrameDurationMsForTesting(IReadOnlyList<IDXObject> frames)
        {
            return ResolveAnimationDisplayerOneTimeFrameDurationMs(frames);
        }

        internal static int ResolveAnimationDisplayerRemoteItemMakeRestoreElapsedForTesting(
            bool previousSuccess,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            bool currentSuccess,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerRemoteItemMakeRestoreElapsedCore(
                previousSuccess,
                previousActionName,
                previousFacingRight,
                previousAnimationStartTime,
                currentSuccess,
                currentActionName,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static int ResolveAnimationDisplayerRemoteGenericUserStateRestoreElapsedForTesting(
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerRemoteGenericUserStateRestoreElapsedCore(
                previousEffectUol,
                previousActionName,
                previousFacingRight,
                previousAnimationStartTime,
                currentEffectUol,
                currentActionName,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static int ResolveAnimationDisplayerRemoteMakerSkillRestoreElapsedForTesting(
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerRemoteMakerSkillRestoreElapsedCore(
                previousEffectUol,
                previousActionName,
                previousFacingRight,
                previousAnimationStartTime,
                currentEffectUol,
                currentActionName,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static int ResolveAnimationDisplayerRemoteUpgradeTombRestoreElapsedForTesting(
            int previousItemId,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            int currentItemId,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerRemoteUpgradeTombRestoreElapsedCore(
                previousItemId,
                previousActionName,
                previousFacingRight,
                previousAnimationStartTime,
                currentItemId,
                currentActionName,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static int ResolveAnimationDisplayerRemoteMobAttackHitRestoreElapsedForTesting(
            int previousMobTemplateId,
            sbyte previousAttackIndex,
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            bool previousAttachToOwner,
            int previousAnimationStartTime,
            int currentMobTemplateId,
            sbyte currentAttackIndex,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            bool currentAttachToOwner,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerRemoteMobAttackHitRestoreElapsedCore(
                previousMobTemplateId,
                previousAttackIndex,
                previousEffectUol,
                previousActionName,
                previousFacingRight,
                previousAttachToOwner,
                previousAnimationStartTime,
                currentMobTemplateId,
                currentAttackIndex,
                currentEffectUol,
                currentActionName,
                currentFacingRight,
                currentAttachToOwner,
                currentTime,
                durationMs);
        }

        internal static int ResolveAnimationDisplayerRemotePacketOwnedStringEffectRestoreElapsedForTesting(
            byte previousEffectType,
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            byte currentEffectType,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerRemotePacketOwnedStringEffectRestoreElapsedCore(
                previousEffectType,
                previousEffectUol,
                previousActionName,
                previousFacingRight,
                previousAnimationStartTime,
                currentEffectType,
                currentEffectUol,
                currentActionName,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static string BuildAnimationDisplayerRemotePacketOwnedStringEffectOwnerSlotKeyForTesting(
            byte effectType,
            string effectUol)
        {
            return BuildAnimationDisplayerRemotePacketOwnedStringEffectOwnerSlotKey(effectType, effectUol);
        }

        internal static int ResolveAnimationDisplayerRemoteHookingChainRestoreElapsedForTesting(
            int previousSkillId,
            int previousMobObjectId,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            int currentSkillId,
            int currentMobObjectId,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerRemoteHookingChainRestoreElapsedCore(
                previousSkillId,
                previousMobObjectId,
                previousActionName,
                previousFacingRight,
                previousAnimationStartTime,
                currentSkillId,
                currentMobObjectId,
                currentActionName,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static string BuildAnimationDisplayerRemoteHookingChainOwnerSlotKeyForTesting(
            int skillId,
            int mobObjectId)
        {
            return BuildAnimationDisplayerRemoteHookingChainOwnerSlotKey(skillId, mobObjectId);
        }

        internal static int ResolveAnimationDisplayerRemoteSkillUseRestoreElapsedForTesting(
            int previousSkillId,
            string previousBranchName,
            int previousVariantIndex,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            int currentSkillId,
            string currentBranchName,
            int currentVariantIndex,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerRemoteSkillUseRestoreElapsedCore(
                previousSkillId,
                previousBranchName,
                previousVariantIndex,
                previousActionName,
                previousFacingRight,
                previousAnimationStartTime,
                currentSkillId,
                currentBranchName,
                currentVariantIndex,
                currentActionName,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static string BuildAnimationDisplayerRemoteSkillUseOwnerSlotKeyForTesting(
            int skillId,
            string branchName,
            int variantIndex)
        {
            return BuildAnimationDisplayerRemoteSkillUseOwnerSlotKey(skillId, branchName, variantIndex);
        }

        internal static int ResolveAnimationDisplayerPacketOwnedMonsterBookCardGetRestoreElapsedForTesting(
            int previousItemId,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            int currentItemId,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerPacketOwnedMonsterBookCardGetRestoreElapsedCore(
                previousItemId,
                previousActionName,
                previousFacingRight,
                previousAnimationStartTime,
                currentItemId,
                currentActionName,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static string BuildAnimationDisplayerPacketOwnedMonsterBookCardGetOwnerSlotKeyForTesting(int itemId)
        {
            return BuildAnimationDisplayerPacketOwnedMonsterBookCardGetOwnerSlotKey(itemId);
        }

        internal static string BuildAnimationDisplayerLocalPacketOwnedBuffItemUseOwnerSlotKeyForTesting(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedBuffItemUseOwnerSlotKey(effectUol);
        }

        internal static string BuildAnimationDisplayerLocalPacketOwnedItemUnreleaseOwnerSlotKeyForTesting(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedItemUnreleaseOwnerSlotKey(effectUol);
        }

        internal static string BuildAnimationDisplayerLocalPacketOwnedCombatFeedbackOwnerSlotKeyForTesting(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedCombatFeedbackOwnerSlotKey(effectUol);
        }

        internal static string BuildAnimationDisplayerLocalPacketOwnedCoolOwnerSlotKeyForTesting(string effectUol)
        {
            return BuildAnimationDisplayerLocalPacketOwnedCoolOwnerSlotKey(effectUol);
        }

        internal static string BuildAnimationDisplayerPacketOwnedFallingOwnerSlotKeyForTesting(string effectUol)
        {
            return BuildAnimationDisplayerPacketOwnedFallingOwnerSlotKey(effectUol);
        }

        internal static string BuildAnimationDisplayerPacketOwnedExplosionOwnerSlotKeyForTesting(string effectUol)
        {
            return BuildAnimationDisplayerPacketOwnedExplosionOwnerSlotKey(effectUol);
        }

        internal static int ResolveAnimationDisplayerLocalPacketOwnedBasicOneTimeRestoreElapsedForTesting(
            string previousEffectUol,
            string previousActionName,
            bool previousFacingRight,
            int previousAnimationStartTime,
            string currentEffectUol,
            string currentActionName,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerLocalPacketOwnedBasicOneTimeRestoreElapsedCore(
                previousEffectUol,
                previousActionName,
                previousFacingRight,
                previousAnimationStartTime,
                currentEffectUol,
                currentActionName,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static int ResolveAnimationDisplayerPacketOwnedMobOneTimeRestoreElapsedForTesting(
            string previousOwnerKind,
            int previousMobTemplateId,
            string previousAttackAction,
            string previousEffectUol,
            bool previousFacingRight,
            int previousAnimationStartTime,
            string currentOwnerKind,
            int currentMobTemplateId,
            string currentAttackAction,
            string currentEffectUol,
            bool currentFacingRight,
            int currentTime,
            int durationMs)
        {
            return ResolveAnimationDisplayerPacketOwnedMobOneTimeRestoreElapsedCore(
                previousOwnerKind,
                previousMobTemplateId,
                previousAttackAction,
                previousEffectUol,
                previousFacingRight,
                previousAnimationStartTime,
                currentOwnerKind,
                currentMobTemplateId,
                currentAttackAction,
                currentEffectUol,
                currentFacingRight,
                currentTime,
                durationMs);
        }

        internal static string BuildAnimationDisplayerPacketOwnedMobOneTimeOwnerSlotKeyForTesting(
            string ownerKind,
            int mobTemplateId,
            string attackAction,
            string effectUol)
        {
            return BuildAnimationDisplayerPacketOwnedMobOneTimeOwnerSlotKey(
                ownerKind,
                mobTemplateId,
                attackAction,
                effectUol);
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

            IReadOnlyList<string> requestedBranchNames =
                BuildAnimationDisplayerBuffAppliedRequestedBranchNames(buff.SkillData);
            if (requestedBranchNames == null || requestedBranchNames.Count == 0)
            {
                return;
            }

            SkillCastInfo castInfo = BuildAnimationDisplayerLocalSkillUseRequest(
                buff.SkillData.SkillId,
                buff.SkillData,
                buff.StartTime,
                effectAnimation: null,
                secondaryEffectAnimation: null,
                requestedBranchNames: requestedBranchNames);
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
            TryRegisterAnimationDisplayerPrepareOwner(prepared, effectAnimation, secondaryEffectAnimation);
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
            int prepareOwnerId = ResolveAnimationDisplayerPrepareOwnerId();
            if (prepareOwnerId > 0)
            {
                _animationEffects?.RemovePrepareAnimation(prepareOwnerId);
            }
            TryRegisterAnimationDisplayerSkillUse(castInfo);
        }

        private bool TryRegisterAnimationDisplayerPrepareOwner(
            PreparedSkill prepared,
            SkillAnimation effectAnimation,
            SkillAnimation secondaryEffectAnimation)
        {
            if (prepared?.SkillData == null || _animationEffects == null)
            {
                return false;
            }

            if ((effectAnimation?.Frames.Count ?? 0) <= 0
                && (secondaryEffectAnimation?.Frames.Count ?? 0) <= 0)
            {
                return false;
            }

            PlayerCharacter localPlayer = _playerManager?.Player;
            int ownerCharacterId = ResolveAnimationDisplayerPrepareOwnerId();
            if (ownerCharacterId <= 0)
            {
                return false;
            }

            TryResolveAnimationDisplayerSkillUseOwner(
                ownerCharacterId,
                out Func<Vector2> getOwnerPosition,
                out Func<bool> getOwnerFacingRight);
            Vector2 fallbackPosition = getOwnerPosition?.Invoke()
                                       ?? new Vector2(localPlayer?.X ?? 0f, localPlayer?.Y ?? 0f);
            bool fallbackFlip = !(getOwnerFacingRight?.Invoke() ?? localPlayer?.FacingRight ?? true);
            int durationMs = prepared.IsKeydownSkill
                ? Math.Max(prepared.HudGaugeDurationMs, prepared.Duration)
                : prepared.Duration;
            return _animationEffects.RegisterPrepareAnimation(
                ownerCharacterId,
                effectAnimation?.ToTextureFrames(),
                secondaryEffectAnimation?.ToTextureFrames(),
                getOwnerPosition,
                () => !(getOwnerFacingRight?.Invoke() ?? localPlayer?.FacingRight ?? true),
                fallbackPosition,
                fallbackFlip,
                prepared.StartTime,
                durationMs) >= 0;
        }

        private int ResolveAnimationDisplayerPrepareOwnerId()
        {
            return _playerManager?.Player?.Build?.Id ?? 0;
        }

        internal static bool ShouldRegisterLocalSkillCastThroughClientShowSkillEffectDirectPathForTesting(
            SkillCastInfo castInfo)
        {
            if (castInfo?.SkillData == null
                || castInfo.SkillId <= 0)
            {
                return false;
            }

            SkillData skill = castInfo.SkillData;
            if (skill.IsAttack
                || skill.IsPrepareSkill
                || skill.IsKeydownSkill)
            {
                return false;
            }

            if (castInfo.DelayRateOverride is > 0)
            {
                return true;
            }

            return HasClientSkillEffectRequestShapingForTesting(
                castInfo.RequestedBranchNames,
                castInfo.OriginOffset,
                castInfo.FollowOwnerFacing,
                castInfo.FollowOwnerPosition,
                castInfo.FacingRightOverride);
        }

        internal static bool ShouldRouteLocalSkillCastThroughClientSkillEffectRequestSeamForTesting(
            SkillCastInfo castInfo)
        {
            if (!ShouldRegisterLocalSkillCastThroughClientShowSkillEffectDirectPathForTesting(castInfo))
            {
                return false;
            }

            // Local non-melee cast visuals now stay on the direct owner path in
            // `HandlePlayerSkillCast` while explicit caller-owned requests
            // (`OnClientSkillEffectRequested`) remain on the request seam.
            return false;
        }

        internal static bool HasClientSkillEffectRequestShapingForTesting(
            IReadOnlyList<string> requestedBranchNames,
            Point originOffset,
            bool followOwnerFacing,
            bool followOwnerPosition,
            bool? facingRightOverride = null)
        {
            if (requestedBranchNames != null && requestedBranchNames.Count > 0)
            {
                return true;
            }

            if (originOffset != Point.Zero)
            {
                return true;
            }

            if (!followOwnerFacing || !followOwnerPosition)
            {
                return true;
            }

            return facingRightOverride.HasValue;
        }

        private bool TryRegisterAnimationDisplayerLocalSkillUseRequest(SkillUseEffectRequest request)
        {
            if (request?.EffectSkillId <= 0 || _animationEffects == null)
            {
                return false;
            }

            int effectSkillId = request.EffectSkillId;
            int sourceSkillId = request.SourceSkillId;
            SkillData requestedEffectSkill = _playerManager?.Skills?.GetSkillData(effectSkillId);
            SkillData sourceSkill = _playerManager?.Skills?.GetSkillData(sourceSkillId);
            SkillData effectSkill = requestedEffectSkill ?? sourceSkill;
            int branchSkillId = ResolveAnimationDisplayerLocalRequestBranchSkillId(
                effectSkillId,
                sourceSkillId,
                request,
                requestedEffectSkill,
                sourceSkill);
            PlayerCharacter localPlayer = _playerManager?.Player;
            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            Vector2 casterPosition = request.WorldOrigin ?? new Vector2(localPlayer?.X ?? 0f, localPlayer?.Y ?? 0f);
            bool facingRight = localPlayer?.FacingRight ?? true;
            SkillCastInfo castInfo = BuildAnimationDisplayerLocalSkillUseRequest(
                branchSkillId,
                effectSkill,
                request.RequestTime > 0 ? request.RequestTime : currTickCount,
                effectSkill?.Effect,
                effectSkill?.EffectSecondary,
                casterPosition,
                request.BranchNames,
                request.EffectBranchLastIndex,
                request.OriginOffset,
                request.FollowOwnerPosition,
                request.FollowOwnerFacing,
                request.FacingRightOverride);
            castInfo.CasterId = localCharacterId;
            castInfo.FacingRight = request.FacingRightOverride ?? facingRight;
            castInfo.DelayRateOverride = request.DelayRateOverride;
            return TryRegisterAnimationDisplayerSkillUse(castInfo);
        }

        private int ResolveAnimationDisplayerLocalRequestBranchSkillId(
            int effectSkillId,
            int sourceSkillId,
            SkillUseEffectRequest request,
            SkillData requestedEffectSkill,
            SkillData sourceSkill)
        {
            return ResolveAnimationDisplayerLocalRequestBranchSkillIdForTesting(
                effectSkillId,
                sourceSkillId,
                requestedEffectSkillExists: requestedEffectSkill != null,
                sourceSkillExists: sourceSkill != null,
                hasExplicitRequestedBranches: request?.BranchNames?.Count > 0);
        }

        internal static int ResolveAnimationDisplayerLocalRequestBranchSkillIdForTesting(
            int effectSkillId,
            int sourceSkillId,
            bool requestedEffectSkillExists,
            bool sourceSkillExists,
            bool hasExplicitRequestedBranches)
        {
            if (requestedEffectSkillExists)
            {
                return effectSkillId;
            }

            if (!sourceSkillExists || sourceSkillId <= 0)
            {
                return effectSkillId;
            }

            if (hasExplicitRequestedBranches)
            {
                // `CUserLocal::OnKeyDownSkillEnd` can issue `SendSkillEffectRequest` with
                // client effect ids (`35000001`, `35100009`) that are not authored as skill
                // nodes in v95 exports; the visual branch remains on the source keydown skill.
                return sourceSkillId;
            }

            // Other local `CUser::ShowSkillEffect` request ids can also be packet/client-only
            // shims (for example repeat/timeout and recovery ids) with no mounted skill node.
            // Keep branch ownership on the source skill in the same shared seam so authored
            // `effect*` branches continue to resolve through `Effect_SkillUse`.
            return sourceSkillId;
        }

        private SkillCastInfo BuildAnimationDisplayerLocalSkillUseRequest(
            int skillId,
            SkillData skillData,
            int currentTime,
            SkillAnimation effectAnimation,
            SkillAnimation secondaryEffectAnimation,
            Vector2? casterPositionOverride = null,
            IReadOnlyList<string> requestedBranchNames = null,
            int? effectBranchLastIndex = null,
            Point originOffset = default,
            bool followOwnerPosition = true,
            bool followOwnerFacing = true,
            bool? facingRightOverride = null)
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
                EffectBranchLastIndex = effectBranchLastIndex,
                OriginOffset = originOffset,
                FollowOwnerPosition = followOwnerPosition,
                FollowOwnerFacing = followOwnerFacing,
                FacingRightOverride = facingRightOverride
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

        private void HandleRemoteAnimationDisplayerHookingChain(RemoteUserActorPool.RemoteHookingChainPresentation presentation)
        {
            if (_animationEffects == null
                || presentation.Skill == null
                || presentation.MobObjectIds == null
                || presentation.MobObjectIds.Count == 0
                || !SkillManager.TryResolveSecondaryHookingChainFrames(
                    presentation.Skill,
                    out List<IDXObject> hookFrames,
                    out List<IDXObject> chainFrames))
            {
                return;
            }

            TryResolveAnimationDisplayerSkillUseOwner(
                presentation.CharacterId,
                out Func<Vector2> getOwnerPosition,
                out Func<bool> getOwnerFacingRight);
            Vector2 fallbackOwnerPosition = getOwnerPosition?.Invoke() ?? Vector2.Zero;
            bool facingRight = getOwnerFacingRight?.Invoke() ?? presentation.FacingRight;
            string ownerActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
            if (_remoteUserPool?.TryGetActor(presentation.CharacterId, out RemoteUserActor actor) == true
                && actor != null)
            {
                ownerActionName = ResolveAnimationDisplayerRemotePacketOwnedActionName(actor);
            }

            int attackWindowMs = SkillManager.ResolveSecondaryHookingChainAttackWindowMs(presentation.Skill);
            int attackTimeMs = presentation.CurrentTime + attackWindowMs;
            int durationMs = ResolveAnimationDisplayerRemoteHookingChainDurationMs(attackWindowMs);

            for (int i = 0; i < presentation.MobObjectIds.Count; i++)
            {
                int mobObjectId = presentation.MobObjectIds[i];
                MobItem mob = _mobPool?.GetMob(mobObjectId);
                if (mob?.MovementInfo == null)
                {
                    continue;
                }

                int initialElapsedMs = ResolveAnimationDisplayerRemoteHookingChainInitialElapsed(
                    presentation.CharacterId,
                    presentation.Skill.SkillId,
                    mobObjectId,
                    ownerActionName,
                    facingRight,
                    presentation.CurrentTime,
                    durationMs);
                _animationEffects.RegisterHookingChainAnimation(
                    hookFrames,
                    chainFrames,
                    presentation.CharacterId,
                    getOwnerPosition,
                    fallbackOwnerPosition,
                    new Vector2(mob.MovementInfo.X, mob.MovementInfo.Y),
                    left: !facingRight,
                    attackTimeMs,
                    presentation.CurrentTime,
                    zOrder: 0,
                    initialElapsedMs: initialElapsedMs);
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
            bool branchFacingRight = branchRequest.FacingRightOverride ?? facingRight;
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
            if (branchRequest.FacingRightOverride.HasValue)
            {
                bool facingRightOverride = branchRequest.FacingRightOverride.Value;
                branchOwnerFacingRight = () => facingRightOverride;
            }

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

            RemoteUserActor remoteOwner = null;
            bool hasRemoteOwnerContext =
                _remoteUserPool?.TryGetActor(ownerCharacterId, out remoteOwner) == true
                && remoteOwner != null;
            string remoteOwnerActionName = hasRemoteOwnerContext
                ? ResolveAnimationDisplayerRemotePacketOwnedActionName(remoteOwner)
                : null;
            bool remoteOwnerFacingRight = hasRemoteOwnerContext
                ? remoteOwner.FacingRight
                : branchFacingRight;
            bool registered = false;
            for (int i = 0; i < variants.Count; i++)
            {
                List<IDXObject> adjustedFrames = ApplyAnimationDisplayerDelayRate(variants[i], delayRate);
                if (!Animation.AnimationEffects.HasFrames(adjustedFrames))
                {
                    continue;
                }

                int initialElapsedMs = hasRemoteOwnerContext
                    ? ResolveAnimationDisplayerRemoteSkillUseInitialElapsed(
                        ownerCharacterId,
                        skillId,
                        branchName,
                        i,
                        remoteOwnerActionName,
                        remoteOwnerFacingRight,
                        currentTime,
                        ResolveAnimationDisplayerOneTimeFrameDurationMs(adjustedFrames))
                    : 0;

                if (branchOwnerPosition != null || branchOwnerFacingRight != null)
                {
                    _animationEffects.AddOneTimeAttached(
                        adjustedFrames,
                        branchOwnerPosition,
                        branchOwnerFacingRight,
                        branchX,
                        branchY,
                        branchFacingRight,
                        currentTime,
                        initialElapsedMs: initialElapsedMs);
                }
                else
                {
                    _animationEffects.AddOneTimeAttached(
                        adjustedFrames,
                        null,
                        null,
                        branchX,
                        branchY,
                        branchFacingRight,
                        currentTime,
                        initialElapsedMs: initialElapsedMs);
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
            int? effectBranchLastIndex = castInfo?.EffectBranchLastIndex;

            if (!string.IsNullOrWhiteSpace(castInfo?.EffectAnimation?.Name)
                && ShouldIncludeAnimationDisplayerEffectBranchForTesting(castInfo.EffectAnimation.Name, effectBranchLastIndex)
                && seen.Add(castInfo.EffectAnimation.Name))
            {
                yield return castInfo.EffectAnimation.Name;
            }

            if (!string.IsNullOrWhiteSpace(castInfo?.SecondaryEffectAnimation?.Name)
                && ShouldIncludeAnimationDisplayerEffectBranchForTesting(castInfo.SecondaryEffectAnimation.Name, effectBranchLastIndex)
                && seen.Add(castInfo.SecondaryEffectAnimation.Name))
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
                if (!IsAnimationDisplayerSkillUseBranchName(child?.Name)
                    || !ShouldIncludeAnimationDisplayerEffectBranchForTesting(child.Name, effectBranchLastIndex)
                    || !seen.Add(child.Name))
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
            int? effectBranchLastIndex = castInfo?.EffectBranchLastIndex;
            if (castInfo?.RequestedBranchNames != null)
            {
                for (int i = 0; i < castInfo.RequestedBranchNames.Count; i++)
                {
                    string branchName = castInfo.RequestedBranchNames[i];
                    if (string.IsNullOrWhiteSpace(branchName)
                        || !ShouldIncludeAnimationDisplayerEffectBranchForTesting(branchName, effectBranchLastIndex)
                        || !seen.Add(branchName))
                    {
                        continue;
                    }

                    yield return new AnimationDisplayerSkillUseBranchRequest(
                        branchName,
                        castInfo.OriginOffset,
                        castInfo.FollowOwnerFacing,
                        castInfo.FollowOwnerPosition,
                        castInfo.FacingRightOverride);
                }

                if (seen.Count > 0)
                {
                    yield break;
                }
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
                    castInfo?.FollowOwnerPosition ?? true,
                    castInfo?.FacingRightOverride);
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
            TryAddAnimationDisplayerRequestedBranchName(skillData?.SpecialAffectedEffect?.Name, ref resolvedBranchNames);
            return resolvedBranchNames != null
                ? (IReadOnlyList<string>)resolvedBranchNames
                : Array.Empty<string>();
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

        internal static bool ShouldIncludeAnimationDisplayerEffectBranchForTesting(
            string branchName,
            int? effectBranchLastIndex)
        {
            int? effectBranchIndex = TryResolveAnimationDisplayerEffectBranchIndex(branchName);
            if (!effectBranchIndex.HasValue)
            {
                return true;
            }

            if (!effectBranchLastIndex.HasValue)
            {
                return true;
            }

            if (effectBranchLastIndex.Value < 0)
            {
                return false;
            }

            return effectBranchIndex.Value <= effectBranchLastIndex.Value;
        }

        private static int? TryResolveAnimationDisplayerEffectBranchIndex(string branchName)
        {
            if (!IsAnimationDisplayerSkillUseBranchName(branchName))
            {
                return null;
            }

            if (branchName.Length == "effect".Length)
            {
                return 0;
            }

            string suffix = branchName.Substring("effect".Length);
            return int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                ? index
                : null;
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

            if (TryResolveRemotePacketOwnedUtilityEffectAliasUol(normalized, out string utilityAliasUol))
            {
                return utilityAliasUol;
            }

            return normalized;
        }

        private static bool TryResolveRemotePacketOwnedUtilityEffectAliasUol(
            string normalizedEffectPath,
            out string aliasedEffectUol)
        {
            aliasedEffectUol = null;
            if (string.IsNullOrWhiteSpace(normalizedEffectPath))
            {
                return false;
            }

            static bool StartsWithPath(string value, string prefix)
            {
                return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            string normalized = normalizedEffectPath.Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (string.Equals(normalized, "BasicEff", StringComparison.OrdinalIgnoreCase))
            {
                aliasedEffectUol = "Effect/BasicEff.img";
                return true;
            }

            if (StartsWithPath(normalized, "BasicEff/"))
            {
                aliasedEffectUol = $"Effect/BasicEff.img/{normalized["BasicEff/".Length..]}";
                return true;
            }

            if (StartsWithPath(normalized, "Catch")
                || StartsWithPath(normalized, "Flame/")
                || StartsWithPath(normalized, "Transform")
                || StartsWithPath(normalized, "TransformOnLadder")
                || StartsWithPath(normalized, "CoolHit/")
                || StartsWithPath(normalized, "SquibEffect")
                || StartsWithPath(normalized, "SquibEffect2"))
            {
                string utilityPath = normalized;
                if (StartsWithPath(normalized, "SquibEffect"))
                {
                    utilityPath = $"Flame/{normalized}";
                }

                aliasedEffectUol = $"Effect/BasicEff.img/{utilityPath}";
                return true;
            }

            return false;
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

        internal static bool IsAnimationDisplayerPacketOwnedFollowRegistrationKey(int registrationKey)
        {
            return registrationKey < 0
                   && (registrationKey & AnimationDisplayerPacketOwnedFollowRegistrationMask)
                   == AnimationDisplayerPacketOwnedFollowRegistrationMask;
        }

        internal static bool ShouldAllowAnimationDisplayerGenericFollowFallback(int registrationKey)
        {
            return !IsAnimationDisplayerPacketOwnedFollowRegistrationKey(registrationKey);
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

        private IReadOnlyList<AnimationDisplayerResolvedFollowEquipmentEntry> ResolveAnimationDisplayerFollowEquipmentDefinitions(int ownerCharacterId)
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

            return ResolveAnimationDisplayerFollowEquipmentDefinitions(build);
        }

        private IReadOnlyList<AnimationDisplayerResolvedFollowEquipmentEntry> ResolveAnimationDisplayerFollowEquipmentDefinitions(CharacterBuild build)
        {
            if (build == null)
            {
                return Array.Empty<AnimationDisplayerResolvedFollowEquipmentEntry>();
            }

            var definitions = new List<AnimationDisplayerResolvedFollowEquipmentEntry>();
            foreach (AnimationDisplayerFollowEquipmentCandidate candidate in EnumerateAnimationDisplayerFollowEquipmentCandidates(build))
            {
                if (candidate.ItemId <= 0)
                {
                    continue;
                }

                int candidateIdentity = BuildAnimationDisplayerFollowCandidateIdentity(candidate);
                (int ItemId, EquipSlot Slot, int ClientEquipIndex) cacheKey =
                    BuildAnimationDisplayerFollowEquipmentCacheKey(candidate);
                if (_animationDisplayerFollowEquipmentCache.TryGetValue(cacheKey, out AnimationDisplayerFollowEquipmentDefinition cachedDefinition))
                {
                    if (cachedDefinition != null)
                    {
                        definitions.Add(new AnimationDisplayerResolvedFollowEquipmentEntry(cachedDefinition, candidateIdentity));
                    }

                    continue;
                }

                AnimationDisplayerFollowEquipmentDefinition loadedDefinition = LoadAnimationDisplayerFollowEquipmentDefinition(
                    candidate.ItemId,
                    candidate.Slot,
                    cacheKey.ClientEquipIndex);
                _animationDisplayerFollowEquipmentCache[cacheKey] = loadedDefinition;
                if (loadedDefinition != null)
                {
                    definitions.Add(new AnimationDisplayerResolvedFollowEquipmentEntry(loadedDefinition, candidateIdentity));
                }
            }

            return definitions;
        }

        internal static IEnumerable<AnimationDisplayerFollowEquipmentCandidate> EnumerateAnimationDisplayerFollowEquipmentCandidates(CharacterBuild build)
        {
            if (build == null)
            {
                yield break;
            }

            for (int i = 0; i < AnimationDisplayerFollowCandidateEquipSlots.Length; i++)
            {
                EquipSlot slot = AnimationDisplayerFollowCandidateEquipSlots[i];
                if (build.Equipment.TryGetValue(slot, out CharacterPart visiblePart) && visiblePart?.ItemId > 0)
                {
                    yield return new AnimationDisplayerFollowEquipmentCandidate(visiblePart.ItemId, slot, i, IsHidden: false);
                }

                if (build.HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart) && hiddenPart?.ItemId > 0)
                {
                    yield return new AnimationDisplayerFollowEquipmentCandidate(hiddenPart.ItemId, slot, i, IsHidden: true);
                }
            }
        }

        internal static int BuildAnimationDisplayerFollowCandidateIdentity(AnimationDisplayerFollowEquipmentCandidate candidate)
        {
            int resolvedClientEquipIndex = ResolveAnimationDisplayerFollowCandidateClientEquipIndex(candidate);
            unchecked
            {
                int identity = 17;
                identity = (identity * 31) + candidate.ItemId;
                identity = (identity * 31) + (int)candidate.Slot;
                identity = (identity * 31) + resolvedClientEquipIndex;
                identity = (identity * 31) + (candidate.IsHidden ? 1 : 0);
                return identity;
            }
        }

        internal static (int ItemId, EquipSlot Slot, int ClientEquipIndex) BuildAnimationDisplayerFollowEquipmentCacheKey(
            AnimationDisplayerFollowEquipmentCandidate candidate)
        {
            return (candidate.ItemId, candidate.Slot, ResolveAnimationDisplayerFollowCandidateClientEquipIndex(candidate));
        }

        internal static int ResolveAnimationDisplayerFollowCandidateClientEquipIndex(
            AnimationDisplayerFollowEquipmentCandidate candidate)
        {
            return candidate.ClientEquipIndex >= 0
                ? candidate.ClientEquipIndex
                : ResolveAnimationDisplayerFollowClientEquipIndex(candidate.Slot);
        }

        internal static int BuildAnimationDisplayerFollowCandidateSignature(
            IEnumerable<AnimationDisplayerFollowEquipmentCandidate> candidates)
        {
            if (candidates == null)
            {
                return 0;
            }

            unchecked
            {
                int signature = 17;
                foreach (AnimationDisplayerFollowEquipmentCandidate candidate in candidates)
                {
                    int resolvedClientEquipIndex = ResolveAnimationDisplayerFollowCandidateClientEquipIndex(candidate);
                    signature = (signature * 31) + candidate.ItemId;
                    signature = (signature * 31) + (int)candidate.Slot;
                    signature = (signature * 31) + resolvedClientEquipIndex;
                    signature = (signature * 31) + (candidate.IsHidden ? 1 : 0);
                }

                return signature;
            }
        }

        private int ResolveAnimationDisplayerFollowCandidateSignature(int ownerCharacterId)
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

            return BuildAnimationDisplayerFollowCandidateSignature(
                EnumerateAnimationDisplayerFollowEquipmentCandidates(build));
        }

        private AnimationDisplayerFollowEquipmentDefinition LoadAnimationDisplayerFollowEquipmentDefinition(
            int itemId,
            EquipSlot sourceEquipSlot,
            int clientEquipIndex)
        {
            WzSubProperty effectProperty = ResolveAnimationDisplayerFollowEquipmentProperty(itemId);
            if (effectProperty == null)
            {
                return null;
            }

            if (!ShouldUseAnimationDisplayerFollowParticleOwner(
                    GetAnimationDisplayerNumericValue(effectProperty, "animate"),
                    GetAnimationDisplayerNumericValue(effectProperty, "fixed"),
                    GetAnimationDisplayerNumericValue(effectProperty, "pos")))
            {
                return null;
            }

            string effectPath = NormalizeAnimationDisplayerPath(effectProperty["path"]?.GetString());
            string effectUol = BuildAnimationDisplayerFollowEquipmentEffectUol(effectPath);
            IReadOnlyList<List<IDXObject>> effectFrameVariants = LoadAnimationDisplayerFollowEquipmentFrameVariants(
                effectPath,
                out IReadOnlyList<int> effectVariantIndices,
                out bool hasIndexedNumericVariantBranches);
            bool shouldAttemptRootEffectFallback = !hasIndexedNumericVariantBranches;
            if (!Animation.AnimationEffects.HasFrameVariants(effectFrameVariants)
                && (!shouldAttemptRootEffectFallback
                    || !Animation.AnimationEffects.HasFrames(LoadAnimationDisplayerFrames(effectUol))))
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
                SourceEquipSlot = sourceEquipSlot,
                ClientEquipIndex = clientEquipIndex >= 0
                    ? clientEquipIndex
                    : ResolveAnimationDisplayerFollowClientEquipIndex(sourceEquipSlot),
                EffectUol = effectUol,
                EffectFrameVariants = effectFrameVariants,
                EffectVariantIndices = effectVariantIndices,
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

        private IReadOnlyList<List<IDXObject>> LoadAnimationDisplayerFollowEquipmentFrameVariants(
            string effectPath,
            out IReadOnlyList<int> loadedVariantIndices,
            out bool hasIndexedNumericVariantBranches)
        {
            loadedVariantIndices = null;
            string[] variantUols = EnumerateAnimationDisplayerFollowEquipmentEffectVariantUols(
                effectPath,
                ResolveAnimationDisplayerPropertyStatic(effectPath),
                out hasIndexedNumericVariantBranches);
            if (variantUols.Length == 0)
            {
                return null;
            }

            var variants = new List<List<IDXObject>>(variantUols.Length);
            var variantIndices = new List<int>(variantUols.Length);
            for (int i = 0; i < variantUols.Length; i++)
            {
                List<IDXObject> frames = LoadAnimationDisplayerFrames(variantUols[i]);
                if (Animation.AnimationEffects.HasFrames(frames))
                {
                    variants.Add(frames);
                    string segment = variantUols[i].Split('/')[^1];
                    if (TryParseAnimationDisplayerNonNegativeIndexSegment(segment, out int variantIndex))
                    {
                        variantIndices.Add(variantIndex);
                    }
                }
            }

            if (variants.Count <= 0)
            {
                return null;
            }

            loadedVariantIndices = variantIndices.Count == variants.Count
                ? variantIndices
                : null;
            return variants;
        }

        internal static string[] EnumerateAnimationDisplayerFollowEquipmentEffectVariantUols(
            string effectPath,
            WzImageProperty effectRootProperty)
        {
            return EnumerateAnimationDisplayerFollowEquipmentEffectVariantUols(
                effectPath,
                effectRootProperty,
                out _);
        }

        internal static string[] EnumerateAnimationDisplayerFollowEquipmentEffectVariantUols(
            string effectPath,
            WzImageProperty effectRootProperty,
            out bool hasIndexedNumericVariantBranches)
        {
            hasIndexedNumericVariantBranches = false;
            string normalizedEffectPath = NormalizeAnimationDisplayerPath(effectPath);
            WzImageProperty resolvedRoot = ResolveAnimationDisplayerLinkedRealProperty(effectRootProperty);
            if (string.IsNullOrWhiteSpace(normalizedEffectPath))
            {
                return Array.Empty<string>();
            }

            WzPropertyCollection rootChildren = resolvedRoot?.WzProperties;
            int childCount = rootChildren?.Count ?? 0;
            if (childCount <= 0)
            {
                return Array.Empty<string>();
            }

            var variantBranches = new List<(int Index, string Segment)>();
            var seenSegments = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < childCount; i++)
            {
                WzImageProperty childProperty = rootChildren[i];
                string childName = childProperty?.Name;
                if (!TryParseAnimationDisplayerNonNegativeIndexSegment(childName, out int index)
                    || !seenSegments.Add(childName))
                {
                    continue;
                }

                hasIndexedNumericVariantBranches = true;
                WzImageProperty variantProperty = ResolveAnimationDisplayerLinkedRealProperty(childProperty);
                if (!IsAnimationDisplayerFollowEffectVariantPropertyLoadable(variantProperty))
                {
                    continue;
                }

                variantBranches.Add((index, childName));
            }

            if (variantBranches.Count == 0)
            {
                return Array.Empty<string>();
            }

            variantBranches.Sort(static (left, right) =>
            {
                int indexComparison = left.Index.CompareTo(right.Index);
                return indexComparison != 0
                    ? indexComparison
                    : StringComparer.Ordinal.Compare(left.Segment, right.Segment);
            });

            var variantUols = new List<string>(variantBranches.Count);
            for (int i = 0; i < variantBranches.Count; i++)
            {
                variantUols.Add($"{normalizedEffectPath}/{variantBranches[i].Segment}");
            }

            return variantUols.ToArray();
        }

        private static bool IsAnimationDisplayerFollowEffectVariantPropertyLoadable(WzImageProperty property)
        {
            if (property == null)
            {
                return false;
            }

            return IsAnimationDisplayerFollowEffectVariantPropertyLoadable(
                property,
                new HashSet<WzImageProperty>());
        }

        private static bool IsAnimationDisplayerFollowEffectVariantPropertyLoadable(
            WzImageProperty property,
            HashSet<WzImageProperty> visited)
        {
            WzImageProperty resolvedProperty = ResolveAnimationDisplayerLinkedRealProperty(property);
            if (resolvedProperty == null || !visited.Add(resolvedProperty))
            {
                return false;
            }

            if (resolvedProperty is WzCanvasProperty)
            {
                return true;
            }

            WzPropertyCollection children = resolvedProperty.WzProperties;
            int childCount = children?.Count ?? 0;
            for (int i = 0; i < childCount; i++)
            {
                if (IsAnimationDisplayerFollowEffectVariantPropertyLoadable(children[i], visited))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseAnimationDisplayerNonNegativeIndexSegment(string segment, out int index)
        {
            index = 0;
            if (string.IsNullOrEmpty(segment))
            {
                return false;
            }

            if (segment.Length > 1 && segment[0] == '0')
            {
                return false;
            }

            for (int i = 0; i < segment.Length; i++)
            {
                char character = segment[i];
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }

            return int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out index);
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

        internal static bool ShouldUseAnimationDisplayerFollowParticleOwner(
            int? authoredAnimate,
            int? authoredFixed,
            int? authoredPos)
        {
            // CItemEffectManager::LoadItemEffect branches to CAnimateEffect only from the authored animate flag.
            // Other effect metadata must not suppress the CParticleEffect/FOLLOWINFO owner path.
            return authoredAnimate.GetValueOrDefault() == 0;
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

        internal static int ResolveAnimationDisplayerFollowClientEquipIndex(EquipSlot sourceEquipSlot)
        {
            for (int index = 0; index < AnimationDisplayerFollowCandidateEquipSlots.Length; index++)
            {
                if (AnimationDisplayerFollowCandidateEquipSlots[index] == sourceEquipSlot)
                {
                    return index;
                }
            }

            return -1;
        }

        internal static bool ResolveAnimationDisplayerFollowOriginUsesFace(int clientEquipIndex, bool usesRelativeEmission)
        {
            if (usesRelativeEmission)
            {
                return true;
            }

            return clientEquipIndex is 0 or 1 or 2 or 3 or 4 or 9 or 12 or 13 or 15 or 16;
        }

        internal static Point? ResolveAnimationDisplayerOwnerBodyOrigin(Vector2 ownerPosition, AssembledFrame frame)
        {
            if (frame == null)
            {
                return null;
            }

            return new Point(
                (int)Math.Round(ownerPosition.X),
                (int)Math.Round(ownerPosition.Y) - frame.FeetOffset);
        }

        internal static Point? ResolveAnimationDisplayerOwnerMapPoint(
            Vector2 ownerPosition,
            bool facingRight,
            AssembledFrame frame,
            string mapPointName)
        {
            if (frame?.MapPoints == null
                || string.IsNullOrWhiteSpace(mapPointName)
                || !frame.MapPoints.TryGetValue(mapPointName, out Point localPoint))
            {
                return null;
            }

            int worldX = AvatarActionLayerCoordinator.ResolveWorldMapPointX(
                mapPointName,
                (int)Math.Round(ownerPosition.X),
                facingRight,
                localPoint.X);
            int worldY = (int)Math.Round(ownerPosition.Y) - frame.FeetOffset + localPoint.Y;
            return new Point(worldX, worldY);
        }

        private Func<Vector2> ResolveAnimationDisplayerFollowTargetPosition(
            int ownerCharacterId,
            Func<Vector2> fallbackPosition,
            AnimationDisplayerFollowEquipmentDefinition followDefinition)
        {
            if (followDefinition == null || fallbackPosition == null)
            {
                return fallbackPosition;
            }

            if (ownerCharacterId <= 0)
            {
                return () => ResolveAnimationDisplayerFollowFallbackOrigin(fallbackPosition);
            }

            return () =>
            {
                int resolvedClientEquipIndex = followDefinition.ClientEquipIndex >= 0
                    ? followDefinition.ClientEquipIndex
                    : ResolveAnimationDisplayerFollowClientEquipIndex(followDefinition.SourceEquipSlot);
                bool useFaceOrigin = ResolveAnimationDisplayerFollowOriginUsesFace(
                    resolvedClientEquipIndex,
                    followDefinition.UsesRelativeEmission);
                if (useFaceOrigin)
                {
                    Point? faceOrigin = TryResolveAnimationDisplayerOwnerMapPoint(
                        ownerCharacterId,
                        AvatarActionLayerCoordinator.ClientFaceOriginMapPoint)
                        ?? TryResolveAnimationDisplayerOwnerMapPoint(ownerCharacterId, "brow");
                    if (faceOrigin.HasValue)
                    {
                        return new Vector2(faceOrigin.Value.X, faceOrigin.Value.Y);
                    }
                }

                Point? bodyOrigin = TryResolveAnimationDisplayerOwnerBodyOrigin(ownerCharacterId);
                return bodyOrigin.HasValue
                    ? new Vector2(bodyOrigin.Value.X, bodyOrigin.Value.Y)
                    : ResolveAnimationDisplayerFollowFallbackOrigin(fallbackPosition);
            };
        }

        private static Vector2 ResolveAnimationDisplayerFollowFallbackOrigin(Func<Vector2> fallbackPosition)
        {
            Vector2 fallback = fallbackPosition();
            return new Vector2(fallback.X, fallback.Y + AnimationDisplayerUserStateOffsetY);
        }

        private Point? TryResolveAnimationDisplayerOwnerBodyOrigin(int ownerCharacterId)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Build?.Id == ownerCharacterId)
            {
                return player.TryGetCurrentBodyOrigin(currTickCount);
            }

            if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor actor) != true || actor == null)
            {
                return null;
            }

            AssembledFrame frame = actor.GetFrameAtTimeForRendering(currTickCount);
            return ResolveAnimationDisplayerOwnerBodyOrigin(actor.Position, frame);
        }

        private Point? TryResolveAnimationDisplayerOwnerMapPoint(int ownerCharacterId, string mapPointName)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Build?.Id == ownerCharacterId)
            {
                return player.TryGetCurrentBodyMapPoint(mapPointName, currTickCount);
            }

            if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor actor) != true || actor == null)
            {
                return null;
            }

            AssembledFrame frame = actor.GetFrameAtTimeForRendering(currTickCount);
            return ResolveAnimationDisplayerOwnerMapPoint(actor.Position, actor.FacingRight, frame, mapPointName);
        }

        internal static Rectangle BuildAnimationDisplayerFollowEquipmentEmissionArea(WzImageProperty effectProperty)
        {
            int left = GetAnimationDisplayerNumericValue(effectProperty, "left")
                ?? AnimationDisplayerFollowDefaultEmissionLeft;
            int top = GetAnimationDisplayerNumericValue(effectProperty, "top")
                ?? AnimationDisplayerFollowDefaultEmissionTop;
            int right = GetAnimationDisplayerNumericValue(effectProperty, "right")
                ?? AnimationDisplayerFollowDefaultEmissionRight;
            int bottom = GetAnimationDisplayerNumericValue(effectProperty, "bottom")
                ?? AnimationDisplayerFollowDefaultEmissionBottom;
            int width = Math.Max(1, right - left);
            int height = Math.Max(1, bottom - top);
            return new Rectangle(left, top, width, height);
        }

        internal static IReadOnlyList<Vector2> LoadAnimationDisplayerFollowEquipmentGenerationPoints(WzImageProperty generationPointProperty)
        {
            if (generationPointProperty == null)
            {
                return null;
            }

            WzImageProperty resolvedGenerationPointProperty =
                ResolveAnimationDisplayerLinkedRealProperty(generationPointProperty);
            WzPropertyCollection children = resolvedGenerationPointProperty?.WzProperties;
            int childCount = children?.Count ?? 0;
            if (childCount <= 0)
            {
                return null;
            }

            var points = new List<Vector2>();
            for (int index = 0; index < childCount; index++)
            {
                WzImageProperty indexedRow =
                    resolvedGenerationPointProperty[index.ToString(CultureInfo.InvariantCulture)];
                if (indexedRow == null)
                {
                    break;
                }

                WzImageProperty resolvedChild = ResolveAnimationDisplayerLinkedRealProperty(indexedRow);
                if (resolvedChild is not WzVectorProperty point)
                {
                    break;
                }

                points.Add(new Vector2(point.X?.GetInt() ?? 0, point.Y?.GetInt() ?? 0));
            }

            return points.Count > 0
                ? points
                : null;
        }

        private static WzImageProperty ResolveAnimationDisplayerLinkedRealProperty(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            return WzInfoTools.GetRealProperty(property.GetLinkedWzImageProperty() ?? property);
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
                ResolveAnimationDisplayerFollowEquipmentSpawnOffsetMaxY(
                    GetAnimationDisplayerNumericValue(effectProperty, "y1"),
                    GetAnimationDisplayerNumericValue(effectProperty, "ry1"),
                    GetAnimationDisplayerNumericValue(effectProperty, "dy"),
                    spawnOffsetMin.Y));
        }

        internal static int ResolveAnimationDisplayerFollowEquipmentSpawnOffsetMaxY(
            int? authoredY1,
            int? authoredRy1,
            int? authoredDy,
            int fallbackMinY)
        {
            return authoredY1
                ?? authoredRy1
                ?? authoredDy
                ?? fallbackMinY;
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

        internal static bool ResolveAnimationDisplayerFollowSpawnUsesEmissionBox(
            bool commandRelativeEmission,
            bool hasFollowDefinition,
            bool usesEquipmentEmission)
        {
            return !commandRelativeEmission
                || (hasFollowDefinition && !usesEquipmentEmission);
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
            foreach (KeyValuePair<int, List<AnimationDisplayerFollowRegistrationEntry>> entry in _animationDisplayerFollowAnimationIds)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                for (int i = 0; i < entry.Value.Count; i++)
                {
                    AnimationDisplayerFollowRegistrationEntry followEntry = entry.Value[i];
                    if (followEntry?.FollowId >= 0)
                    {
                        _animationEffects.RemoveFollow(followEntry.FollowId);
                    }
                }
            }

            _animationDisplayerFollowAnimationIds.Clear();
            _animationDisplayerFollowRegistrationSignatures.Clear();
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

            if (_animationDisplayerFollowAnimationIds.TryGetValue(registrationKey, out List<AnimationDisplayerFollowRegistrationEntry> followEntries))
            {
                if (followEntries != null)
                {
                    for (int i = 0; i < followEntries.Count; i++)
                    {
                        AnimationDisplayerFollowRegistrationEntry followEntry = followEntries[i];
                        if (followEntry?.FollowId >= 0)
                        {
                            _animationEffects.RemoveFollow(followEntry.FollowId);
                        }
                    }
                }

                _animationDisplayerFollowAnimationIds.Remove(registrationKey);
            }
            _animationDisplayerFollowRegistrationSignatures.Remove(registrationKey);

            if (IsAnimationDisplayerPacketOwnedFollowRegistrationKey(registrationKey))
            {
                _packetOwnedAnimationDisplayerFollowDriverId = 0;
                if (_packetOwnedAnimationDisplayerFollowRegistrationKey == registrationKey)
                {
                    _packetOwnedAnimationDisplayerFollowRegistrationKey = 0;
                }
            }
        }

        private void SyncPacketOwnedAnimationDisplayerFollow()
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            int registrationKey = BuildAnimationDisplayerPacketOwnedFollowRegistrationKey(localCharacterId);
            ClearAnimationDisplayerStalePacketOwnedFollowRegistrations(registrationKey);
            if (_packetOwnedAnimationDisplayerFollowRegistrationKey != 0
                && _packetOwnedAnimationDisplayerFollowRegistrationKey != registrationKey)
            {
                ClearAnimationDisplayerFollowRegistration(_packetOwnedAnimationDisplayerFollowRegistrationKey);
            }

            if (localCharacterId <= 0 || registrationKey == 0)
            {
                ClearAnimationDisplayerFollowRegistration(registrationKey);
                _packetOwnedAnimationDisplayerFollowDriverId = 0;
                _packetOwnedAnimationDisplayerFollowRegistrationKey = 0;
                return;
            }

            PlayerCharacter player = _playerManager?.Player;
            if (_localFollowRuntime.AttachedDriverId <= 0 || player == null)
            {
                ClearAnimationDisplayerFollowRegistration(registrationKey);
                _packetOwnedAnimationDisplayerFollowDriverId = 0;
                _packetOwnedAnimationDisplayerFollowRegistrationKey = 0;
                return;
            }

            int attachedDriverId = _localFollowRuntime.AttachedDriverId;
            int currentFollowSignature = ResolveAnimationDisplayerFollowCandidateSignature(localCharacterId);
            if (_packetOwnedAnimationDisplayerFollowDriverId == attachedDriverId
                && HasAnimationDisplayerFollowRegistration(registrationKey)
                && _animationDisplayerFollowRegistrationSignatures.TryGetValue(registrationKey, out int activeFollowSignature)
                && activeFollowSignature == currentFollowSignature)
            {
                _packetOwnedAnimationDisplayerFollowRegistrationKey = registrationKey;
                return;
            }

            if (TryRegisterAnimationDisplayerFollow(
                registrationKey,
                localCharacterId,
                () => _playerManager?.Player?.Position ?? player.Position,
                relativeEmission: true))
            {
                _packetOwnedAnimationDisplayerFollowDriverId = attachedDriverId;
                _packetOwnedAnimationDisplayerFollowRegistrationKey = registrationKey;
            }
            else
            {
                _packetOwnedAnimationDisplayerFollowRegistrationKey = 0;
            }
        }

        private void ClearAnimationDisplayerStalePacketOwnedFollowRegistrations(int activeRegistrationKey)
        {
            if (_animationDisplayerFollowAnimationIds.Count <= 0)
            {
                return;
            }

            List<int> staleKeys = null;
            foreach (KeyValuePair<int, List<AnimationDisplayerFollowRegistrationEntry>> entry in _animationDisplayerFollowAnimationIds)
            {
                if (!IsAnimationDisplayerPacketOwnedFollowRegistrationKey(entry.Key)
                    || entry.Key == activeRegistrationKey)
                {
                    continue;
                }

                staleKeys ??= new List<int>();
                staleKeys.Add(entry.Key);
            }

            if (staleKeys == null)
            {
                return;
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                ClearAnimationDisplayerFollowRegistration(staleKeys[i]);
            }
        }

        private bool HasAnimationDisplayerFollowRegistration(int registrationKey)
        {
            return registrationKey != 0
                && _animationDisplayerFollowAnimationIds.TryGetValue(registrationKey, out List<AnimationDisplayerFollowRegistrationEntry> followEntries)
                && followEntries != null
                && followEntries.Count > 0;
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
