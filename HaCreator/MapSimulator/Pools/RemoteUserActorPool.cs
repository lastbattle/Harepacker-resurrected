using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Pools
{
    public enum RemoteRelationshipOverlayType
    {
        Generic = 0,
        Couple = 1,
        Friendship = 2,
        NewYearCard = 3,
        Marriage = 4
    }

    /// <summary>
    /// Shared owner for remote user actors. This gives the simulator a single seam
    /// for remote avatar look decode, map insertion/removal, world rendering,
    /// helper markers, team state, chairs, mounts, and prepared-skill overlays.
    /// </summary>
    public sealed class RemoteUserActorPool
    {
        public readonly record struct RemoteSkillUsePresentation(
            int CharacterId,
            int SkillId,
            int? ActionSpeed,
            bool FacingRight,
            int CurrentTime,
            IReadOnlyList<string> BranchNames = null,
            Vector2? WorldOrigin = null,
            bool FollowOwnerPosition = true,
            bool FollowOwnerFacing = true,
            int? DelayRateOverride = null,
            Point OriginOffset = default);
        public readonly record struct RemoteUpgradeTombPresentation(
            int CharacterId,
            int ItemId,
            Vector2 Position,
            int CurrentTime);
        public readonly record struct RemoteGenericUserStatePresentation(
            int CharacterId,
            int CurrentTime);
        public readonly record struct RemoteItemMakePresentation(
            int CharacterId,
            int ResultCode,
            bool Success,
            int CurrentTime);
        public readonly record struct RemoteHitFeedbackPresentation(
            int CharacterId,
            Vector2 Position,
            int Delta,
            int CurrentTime,
            byte GuardType = 0);
        public readonly record struct RemoteMobAttackHitPresentation(
            int CharacterId,
            int MobTemplateId,
            sbyte AttackIndex,
            string EffectPath,
            Vector2 Position,
            bool FacingRight,
            int CurrentTime,
            bool UsesAuthoredWzHitNode = false,
            bool AttachToOwner = false);
        public readonly record struct RemoteMobDamageInfoPresentation(
            int CharacterId,
            int MobId,
            int MobTemplateId,
            byte HitAction,
            Point HitPosition,
            int Damage,
            byte DamagePercent,
            bool PowerGuard,
            bool CappedByMobMaxHp,
            int CurrentTime);
        public readonly record struct RemoteFieldSoundPresentation(
            int CharacterId,
            string SoundPath,
            int CurrentTime);
        public readonly record struct RemoteClientSoundPresentation(
            int CharacterId,
            string SoundPath,
            string DefaultImageName,
            int CurrentTime,
            Vector2? WorldOrigin = null);
        public readonly record struct RemoteStringEffectPresentation(
            int CharacterId,
            byte EffectType,
            string EffectPath,
            int CurrentTime,
            bool UseOwnerFacing,
            bool AttachToOwner = true,
            int? SecondaryInt32Value = null,
            int[] TrailingInt32Values = null,
            byte[] StringBranchPrefixBytes = null,
            Vector2? WorldOrigin = null);
        public readonly record struct RemoteChatLogMessagePresentation(
            int CharacterId,
            byte EffectType,
            int StringPoolId,
            string Message,
            int ChatLogType,
            int CurrentTime);
        public readonly record struct RemoteStatusBarEffectPresentation(
            int CharacterId,
            byte EffectType,
            string EffectName,
            int CurrentTime);
        public readonly record struct RemoteGrenadePresentation(
            int CharacterId,
            int SkillId,
            int SkillLevel,
            int OwnerLevel,
            Point Target,
            int KeyDownTime,
            Vector2 Impact,
            bool FacingRight,
            int CurrentTime,
            bool GravityFree,
            bool UsesMonsterBombGauge,
            int InitDelayMs,
            int ExplosionDelayMs,
            int DragX,
            int DragY,
            int LimitTimeMs = 0);
        public readonly record struct RemoteHookingChainPresentation(
            int CharacterId,
            SkillData Skill,
            IReadOnlyList<int> MobObjectIds,
            bool FacingRight,
            int CurrentTime);

        private readonly record struct RemoteMechanicModePresentation(
            string StandActionName,
            string WalkActionName,
            string AttackActionName,
            string ProneActionName);

        public sealed class RemoteTemporaryStatAvatarEffectState
        {
            public int SkillId { get; set; }
            public SkillData Skill { get; set; }
            public SkillAnimation OverlayAnimation { get; set; }
            public SkillAnimation OverlaySecondaryAnimation { get; set; }
            public SkillAnimation UnderFaceAnimation { get; set; }
            public SkillAnimation UnderFaceSecondaryAnimation { get; set; }
            public int SimulatedAffectedLayerHandleId { get; set; }
            public int SimulatedAffectedLayerHandleRefCount { get; private set; }
            public bool TerminateRequested { get; private set; }
            public bool IsTerminated { get; private set; }
            public int AnimationStartTime { get; set; }
            public int TransitionStartTime { get; set; } = int.MinValue;
            public int TransitionDurationMs { get; set; }
            public float TransitionStartAlpha { get; set; } = 1f;
            public float TransitionEndAlpha { get; set; } = 1f;

            public void CaptureAffectedLayerReference()
            {
                SimulatedAffectedLayerHandleRefCount = SimulatedAffectedLayerHandleId > 0 ? 1 : 0;
                TerminateRequested = false;
                IsTerminated = false;
            }

            public void ReleaseAffectedLayerReference()
            {
                TerminateRequested = true;
                IsTerminated = true;
                SimulatedAffectedLayerHandleRefCount = 0;
            }
        }

        private sealed class RemoteAuxiliaryLayerOwnerCounterState
        {
            public int SkillId { get; set; }
            public string ActionName { get; set; }
            public bool FacingRight { get; set; }
            public int AnimationElapsedMs { get; set; }
            public int LastUpdateTimeMs { get; set; }
        }

        private enum RemoteTemporaryStatAvatarEffectOwnerFamily
        {
            SoulArrow = 0,
            WeaponCharge = 1,
            Aura = 2,
            MoreWild = 3,
            Barrier = 4,
            BlessingArmor = 5,
            RepeatEffect = 6,
            MagicShield = 7,
            FinalCut = 8
        }

        public sealed class RemoteShadowPartnerPresentationState
        {
            public Point CurrentClientOffsetPx { get; set; }
            public Point ClientOffsetStartPx { get; set; }
            public Point ClientOffsetTargetPx { get; set; }
            public int ClientOffsetTransitionStartTime { get; set; }
            public bool IsInitialized { get; set; }
            public bool IsActionInitialized { get; set; }
            public string ObservedPlayerActionName { get; set; }
            public PlayerState ObservedPlayerState { get; set; }
            public bool ObservedPlayerFacingRight { get; set; }
            public int? ObservedRawActionCode { get; set; }
            public int ObservedPlayerActionTriggerTime { get; set; } = int.MinValue;
            public string CurrentActionName { get; set; }
            public SkillAnimation CurrentPlaybackAnimation { get; set; }
            public int CurrentActionStartTime { get; set; }
            public bool CurrentFacingRight { get; set; }
            public string PendingActionName { get; set; }
            public SkillAnimation PendingPlaybackAnimation { get; set; }
            public int PendingActionReadyTime { get; set; }
            public bool PendingFacingRight { get; set; }
            public bool PendingForceReplay { get; set; }
            public string QueuedActionName { get; set; }
            public SkillAnimation QueuedPlaybackAnimation { get; set; }
            public bool QueuedFacingRight { get; set; }
            public bool QueuedForceReplay { get; set; }
            public int LastForcedReplayActionTriggerTime { get; set; } = int.MinValue;
        }

        public sealed class RemotePacketOwnedEmotionState
        {
            public int ItemId { get; init; }
            public int EmotionId { get; init; }
            public string EmotionName { get; init; }
            public bool ByItemOption { get; init; }
            public SkillAnimation EffectAnimation { get; init; }
            public int AnimationStartTime { get; init; }
            public int ExpireTime { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
        }

        public sealed class RemoteHitState
        {
            public sbyte AttackIndex { get; init; }
            public int Damage { get; init; }
            public int? MobTemplateId { get; init; }
            public bool MobHitFacingLeft { get; init; }
            public bool HasMobHit { get; init; }
            public byte MobHitDamagePercent { get; init; }
            public bool PowerGuard { get; init; }
            public int? MobId { get; init; }
            public byte? MobHitAction { get; init; }
            public short? MobHitX { get; init; }
            public short? MobHitY { get; init; }
            public byte IncDecType { get; init; }
            public byte HitFlags { get; init; }
            public int HpDelta { get; init; }
            public int? SkillId { get; init; }
            public byte[] RawTrailingPayload { get; init; }
            public string HitString { get; init; }
            public int? HitSecondaryInt32Value { get; init; }
            public int[] HitTrailingInt32Values { get; init; }
            public byte[] HitStringBranchPrefixBytes { get; init; }
            public int PacketTime { get; init; }
        }

        public sealed class RemoteTransientSkillUseAvatarEffectState
        {
            public int RegistrationKey { get; init; }
            public SkillAnimation OverlayAnimation { get; init; }
            public SkillAnimation UnderFaceAnimation { get; init; }
            public int AnimationStartTime { get; init; }
        }

        public sealed class RemoteTransientItemEffectState
        {
            public int ItemId { get; init; }
            public ItemEffectAnimationSet Effect { get; init; }
            public int SimulatedOwnerLayerHandleId { get; init; }
            public int SimulatedOwnerLayerHandleRefCount { get; private set; }
            public bool TerminateRequested { get; private set; }
            public bool IsTerminated { get; private set; }
            public int AnimationStartTime { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }

            public void CaptureRegisteredLayerReference()
            {
                SimulatedOwnerLayerHandleRefCount = SimulatedOwnerLayerHandleId > 0 ? 1 : 0;
                TerminateRequested = false;
                IsTerminated = false;
            }

            public void ReleaseLayerReference()
            {
                TerminateRequested = true;
                IsTerminated = true;
                SimulatedOwnerLayerHandleRefCount = 0;
            }
        }

        public sealed class RemoteActiveEffectMotionBlurState
        {
            public ActiveEffectItemMotionBlurDefinition Definition { get; init; }
            public int ActiveItemId { get; init; }
            public int SimulatedAnimationStateId { get; init; }
            public int SimulatedOverlayLayerHandleId { get; init; }
            public int SimulatedOverlayLayerHandleRefCount { get; private set; }
            public int AnimationStartTime { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public int NextSampleTime { get; set; }
            public bool TerminateRequested { get; private set; }
            public bool IsTerminated { get; private set; }
            public IReadOnlyDictionary<AvatarRenderLayer, int> SimulatedLayerHandleIds { get; set; } =
                new Dictionary<AvatarRenderLayer, int>();
            public IReadOnlyDictionary<AvatarRenderLayer, int> SimulatedLayerHandleRefCounts { get; private set; } =
                new Dictionary<AvatarRenderLayer, int>();
            internal IReadOnlyList<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation> SimulatedLayerReferenceOperations { get; private set; }
                = Array.Empty<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation>();
            public List<RemoteActiveEffectMotionBlurSnapshot> Snapshots { get; } = new();
            private int _simulatedOverlaySnapshotRefCount;
            private readonly Dictionary<(AvatarRenderLayer Layer, int HandleId), int> _simulatedLayerSnapshotRefCounts = new();

            public void CaptureRegisteredLayerReferences()
            {
                SimulatedOverlayLayerHandleRefCount = SimulatedOverlayLayerHandleId > 0 ? 1 : 0;
                _simulatedOverlaySnapshotRefCount = 0;
                _simulatedLayerSnapshotRefCounts.Clear();
                var operations = new List<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation>();
                if (SimulatedOverlayLayerHandleId > 0)
                {
                    operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.CaptureOwnerReference(
                        layerCode: -1,
                        SimulatedOverlayLayerHandleId,
                        SimulatedOverlayLayerHandleRefCount));
                }

                SimulatedLayerHandleIds = SimulatedLayerHandleIds?
                    .Where(static entry => entry.Value > 0)
                    .ToDictionary(static entry => entry.Key, static entry => entry.Value)
                    ?? new Dictionary<AvatarRenderLayer, int>();
                SimulatedLayerHandleRefCounts = SimulatedLayerHandleIds?
                    .Where(static entry => entry.Value > 0)
                    .ToDictionary(static entry => entry.Key, static _ => 1)
                    ?? new Dictionary<AvatarRenderLayer, int>();
                foreach ((AvatarRenderLayer layer, int handleId) in SimulatedLayerHandleIds)
                {
                    operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.CaptureOwnerReference(
                        (int)layer,
                        handleId,
                        SimulatedLayerHandleRefCounts.TryGetValue(layer, out int refCount)
                            ? refCount
                            : 1));
                }

                SimulatedLayerReferenceOperations = operations;
                TerminateRequested = false;
                IsTerminated = false;
            }

            public void MarkTerminated()
            {
                TerminateRequested = true;
                IsTerminated = true;
                for (int i = Snapshots.Count - 1; i >= 0; i--)
                {
                    ReleaseSnapshotLayerReferences(Snapshots[i]);
                }

                Snapshots.Clear();
                var operations = SimulatedLayerReferenceOperations?.ToList()
                    ?? new List<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation>();
                if (SimulatedOverlayLayerHandleRefCount > 0 && SimulatedOverlayLayerHandleId > 0)
                {
                    operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.ReleaseOwnerReference(
                        layerCode: -1,
                        SimulatedOverlayLayerHandleId));
                }

                if (SimulatedLayerHandleRefCounts != null)
                {
                    foreach ((AvatarRenderLayer layer, int refCount) in SimulatedLayerHandleRefCounts)
                    {
                        if (refCount <= 0
                            || SimulatedLayerHandleIds == null
                            || !SimulatedLayerHandleIds.TryGetValue(layer, out int handleId)
                            || handleId <= 0)
                        {
                            continue;
                        }

                        operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.ReleaseOwnerReference(
                            (int)layer,
                            handleId));
                    }
                }

                SimulatedOverlayLayerHandleRefCount = 0;
                SimulatedLayerHandleRefCounts = SimulatedLayerHandleRefCounts?
                    .ToDictionary(static entry => entry.Key, static _ => 0)
                    ?? new Dictionary<AvatarRenderLayer, int>();
                SimulatedLayerReferenceOperations = operations;
            }

            public void ReleaseSnapshotLayerReferences(RemoteActiveEffectMotionBlurSnapshot snapshot)
            {
                if (snapshot == null)
                {
                    return;
                }

                var operations = SimulatedLayerReferenceOperations?.ToList()
                    ?? new List<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation>();
                if (snapshot.SimulatedOverlayLayerHandleId > 0
                    && snapshot.SimulatedOverlayLayerHandleRefCount > 1)
                {
                    _simulatedOverlaySnapshotRefCount = Math.Max(0, _simulatedOverlaySnapshotRefCount - 1);
                    operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.ReleaseSnapshotReference(
                        layerCode: -1,
                        snapshot.SimulatedOverlayLayerHandleId,
                        SimulatedOverlayLayerHandleRefCount + _simulatedOverlaySnapshotRefCount));
                }

                if (snapshot.Layers != null)
                {
                    for (int i = 0; i < snapshot.Layers.Count; i++)
                    {
                        RemoteActiveEffectMotionBlurLayerSnapshot layer = snapshot.Layers[i];
                        if (layer == null)
                        {
                            continue;
                        }

                        if (layer.SimulatedLayerHandleId > 0 && layer.SimulatedLayerHandleRefCount > 1)
                        {
                            int remainingSnapshotRefCount = 0;
                            var snapshotRefKey = (Layer: layer.SourceLayer, HandleId: layer.SimulatedLayerHandleId);
                            if (_simulatedLayerSnapshotRefCounts.TryGetValue(snapshotRefKey, out int existingSnapshotRefCount))
                            {
                                remainingSnapshotRefCount = Math.Max(0, existingSnapshotRefCount - 1);
                                if (remainingSnapshotRefCount > 0)
                                {
                                    _simulatedLayerSnapshotRefCounts[snapshotRefKey] = remainingSnapshotRefCount;
                                }
                                else
                                {
                                    _simulatedLayerSnapshotRefCounts.Remove(snapshotRefKey);
                                }
                            }

                            operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.ReleaseSnapshotReference(
                                (int)layer.SourceLayer,
                                layer.SimulatedLayerHandleId,
                                SimulatedLayerHandleRefCounts != null
                                    && SimulatedLayerHandleRefCounts.TryGetValue(layer.SourceLayer, out int ownerRefCount)
                                    ? ownerRefCount + remainingSnapshotRefCount
                                    : remainingSnapshotRefCount));
                        }

                        if (layer.SimulatedSnapshotLayerHandleId > 0)
                        {
                            operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.ReleaseSnapshotReference(
                                (int)layer.SourceLayer,
                                layer.SimulatedSnapshotLayerHandleId,
                                refCount: 0));
                        }

                        if (layer.SimulatedRepeatAnimationStateId > 0)
                        {
                            operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.ReleaseRepeatAnimation(
                                (int)layer.SourceLayer,
                                layer.SimulatedRepeatAnimationStateId,
                                refCount: 0));
                        }
                    }
                }

                SimulatedLayerReferenceOperations = operations;
            }

            internal IReadOnlyList<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation> CaptureSnapshotLayerReferences(
                IReadOnlyList<RemoteActiveEffectMotionBlurLayerSnapshot> layerSnapshots,
                out int overlayRefCount)
            {
                var operations = new List<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation>();
                overlayRefCount = 0;
                if (SimulatedOverlayLayerHandleId > 0 && SimulatedOverlayLayerHandleRefCount > 0)
                {
                    _simulatedOverlaySnapshotRefCount++;
                    overlayRefCount = SimulatedOverlayLayerHandleRefCount + _simulatedOverlaySnapshotRefCount;
                    operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.CopySnapshotReference(
                        layerCode: -1,
                        SimulatedOverlayLayerHandleId,
                        overlayRefCount));
                }

                if (layerSnapshots == null)
                {
                    SimulatedLayerReferenceOperations = SimulatedLayerReferenceOperations
                        .Concat(operations)
                        .ToArray();
                    return operations;
                }

                for (int i = 0; i < layerSnapshots.Count; i++)
                {
                    RemoteActiveEffectMotionBlurLayerSnapshot snapshot = layerSnapshots[i];
                    if (snapshot == null)
                    {
                        continue;
                    }

                    int layerCode = (int)snapshot.SourceLayer;
                    if (snapshot.SimulatedLayerHandleId > 0)
                    {
                        var snapshotRefKey = (Layer: snapshot.SourceLayer, HandleId: snapshot.SimulatedLayerHandleId);
                        _simulatedLayerSnapshotRefCounts.TryGetValue(snapshotRefKey, out int snapshotRefCount);
                        snapshotRefCount++;
                        _simulatedLayerSnapshotRefCounts[snapshotRefKey] = snapshotRefCount;
                        int ownerRefCount = SimulatedLayerHandleRefCounts != null
                            && SimulatedLayerHandleRefCounts.TryGetValue(snapshot.SourceLayer, out int resolvedOwnerRefCount)
                            ? resolvedOwnerRefCount
                            : 0;
                        operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.CopySnapshotReference(
                            layerCode,
                            snapshot.SimulatedLayerHandleId,
                            ownerRefCount + snapshotRefCount));
                    }

                    if (snapshot.SimulatedSnapshotLayerHandleId > 0)
                    {
                        operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.CaptureOwnerReference(
                            layerCode,
                            snapshot.SimulatedSnapshotLayerHandleId,
                            snapshot.SimulatedSnapshotLayerHandleRefCount));
                    }

                    if (snapshot.SimulatedRepeatAnimationStateId > 0)
                    {
                        operations.Add(AnimationEffects.SecondaryMotionBlurLayerReferenceOperation.RegisterRepeatAnimation(
                            layerCode,
                            snapshot.SimulatedRepeatAnimationStateId,
                            refCount: 1));
                    }
                }

                SimulatedLayerReferenceOperations = SimulatedLayerReferenceOperations
                    .Concat(operations)
                    .ToArray();
                return operations;
            }
        }

        public sealed class RemoteActiveEffectItemEffectState
        {
            public int ItemId { get; init; }
            public ItemEffectAnimationSet Effect { get; init; }
            public int SimulatedOwnerLayerHandleId { get; init; }
            public int SimulatedOwnerLayerHandleRefCount { get; private set; }
            public bool VisibleLayerSuppressed { get; private set; }
            public bool TerminateRequested { get; private set; }
            public bool IsTerminated { get; private set; }
            public int AnimationStartTime { get; init; }
            public Vector2 WorldOrigin { get; init; }
            public string OwnerActionName { get; init; }
            public bool OwnerFacingRight { get; init; }
            public bool FollowOwner { get; init; }

            public void CaptureRegisteredLayerReference()
            {
                SimulatedOwnerLayerHandleRefCount = SimulatedOwnerLayerHandleId > 0 ? 1 : 0;
                VisibleLayerSuppressed = false;
                TerminateRequested = false;
                IsTerminated = false;
            }

            public void ReleaseVisibleLayerReference()
            {
                SimulatedOwnerLayerHandleRefCount = 0;
                VisibleLayerSuppressed = true;
            }

            public void UpdateVisibleLayerAdmission(bool admitted)
            {
                if (admitted)
                {
                    CaptureRegisteredLayerReference();
                    return;
                }

                ReleaseVisibleLayerReference();
            }

            public void MarkTerminated()
            {
                TerminateRequested = true;
                IsTerminated = true;
                VisibleLayerSuppressed = true;
                SimulatedOwnerLayerHandleRefCount = 0;
            }
        }

        public sealed class RemoteDragonCompanionPresentationState
        {
            public Vector2 VisualAnchor { get; set; }
            public Vector2 FollowVelocity { get; set; }
            public int LastFollowUpdateTime { get; set; } = int.MinValue;
            public bool FollowActive { get; set; }
            public int ActiveVerticalFollowState { get; set; }
            public int ActiveVerticalCheckCount { get; set; }
            public int ActiveFollowReleaseStableFrames { get; set; }
            public string ActionName { get; set; }
            public int ActionStartTime { get; set; } = int.MinValue;
            public int OwnerActionStartTime { get; set; } = int.MinValue;
            public int SimulatedLayerHandleId { get; set; }
            public int SimulatedLayerHandleRefCount { get; private set; }
            public bool TerminateRequested { get; private set; }
            public bool IsTerminated { get; private set; }

            public void CaptureRegisteredLayerReference()
            {
                SimulatedLayerHandleRefCount = SimulatedLayerHandleId > 0 ? 1 : 0;
                TerminateRequested = false;
                IsTerminated = false;
            }

            public void MarkTerminated()
            {
                TerminateRequested = true;
                IsTerminated = true;
                SimulatedLayerHandleRefCount = 0;
            }
        }

        public readonly record struct RemoteDragonCompanionRenderState(
            int CharacterId,
            int JobId,
            string ActionName,
            Vector2 TargetAnchor,
            Vector2 VisualAnchor,
            bool FacingRight,
            int ActionElapsedMs,
            int FrameHeight,
            int OriginX);

        public sealed class RemoteActiveEffectMotionBlurSnapshot
        {
            public IReadOnlyList<RemoteActiveEffectMotionBlurLayerSnapshot> Layers { get; init; } = Array.Empty<RemoteActiveEffectMotionBlurLayerSnapshot>();
            public int FeetOffset { get; init; }
            public Vector2 Position { get; init; }
            public bool FacingRight { get; init; }
            public int SampleTime { get; init; }
            public int SimulatedOverlayLayerHandleId { get; init; }
            public int SimulatedOverlayLayerHandleRefCount { get; init; }
            internal IReadOnlyList<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation> SimulatedLayerReferenceOperations { get; init; }
                = Array.Empty<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation>();
        }

        public sealed class RemoteActiveEffectMotionBlurLayerSnapshot
        {
            public int DrawOrder { get; init; }
            public AvatarRenderLayer SourceLayer { get; init; } = AvatarRenderLayer.OverCharacter;
            public int SourceLayerCaptureOrder { get; init; }
            public int SimulatedLayerHandleId { get; init; }
            public int SimulatedLayerHandleRefCount { get; init; }
            public int SimulatedSnapshotLayerHandleId { get; init; }
            public int SimulatedSnapshotLayerHandleRefCount { get; init; }
            public int SimulatedRepeatAnimationStateId { get; init; }
            public IReadOnlyList<AssembledPart> Parts { get; init; } = Array.Empty<AssembledPart>();
        }

        private readonly struct RemoteActiveEffectMotionBlurOwnerSample
        {
            public RemoteActiveEffectMotionBlurOwnerSample(Vector2 position, bool facingRight)
            {
                Position = position;
                FacingRight = facingRight;
            }

            public Vector2 Position { get; }
            public bool FacingRight { get; }
        }

        private sealed class PendingRemoteTransientSkillUseAvatarEffectState
        {
            public int RegistrationKey { get; init; }
            public SkillAnimation OverlayAnimation { get; init; }
            public SkillAnimation UnderFaceAnimation { get; init; }
        }

        internal readonly record struct PortableChairPairParticipant(
            int CharacterId,
            PortableChair Chair,
            Vector2 Position,
            bool FacingRight,
            int? PreferredPairCharacterId,
            int? ExistingPairCharacterId,
            bool IsChairSessionActive,
            bool IsVisibleInWorld,
            bool IsRelationshipOverlaySuppressed);

        internal readonly record struct PortableChairPairRecord(
            int CharacterId,
            int ItemId,
            int? PreferredPairCharacterId,
            int? PairCharacterId = null,
            int Status = 0)
        {
            public bool IsActive => Status != 0 && PairCharacterId.HasValue && PairCharacterId.Value > 0;
        }

        private const float FollowDriverGroundHorizontalOffset = 50f;
        private const float FollowDriverLadderRopeVerticalOffset = 30f;
        private const float RemoteDragonGroundSideOffset = 42f;
        private const float RemoteDragonGroundVerticalOffset = -12f;
        private const float RemoteDragonLadderSideOffset = 34f;
        private const float RemoteDragonLadderVerticalOffset = 18f;
        private const float RemoteDragonKeyDownBarHalfWidth = 36f;
        private const float RemoteDragonKeyDownBarVerticalGap = 30f;
        private const float RemoteDragonFollowMinSpeed = 18f;
        private const float RemoteDragonPassiveHoldDistance = 5f;
        private const float RemoteDragonPassiveArrivalDistance = 4f;
        private const float RemoteDragonPassiveHorizontalResponse = 3.2f;
        private const float RemoteDragonPassiveVerticalResponse = 3.8f;
        private const float RemoteDragonPassiveMaxHorizontalSpeed = 92f;
        private const float RemoteDragonPassiveMaxVerticalSpeed = 108f;
        private const float RemoteDragonPassiveHorizontalForceScale = 0.3f;
        private const float RemoteDragonPassiveVerticalForceScale = 0.34f;
        private const float RemoteDragonActiveFollowDistanceX = 5f;
        private const float RemoteDragonActiveFollowStepX = 7f;
        private const float RemoteDragonActiveFollowVerticalCheckDistance = 30f;
        private const float RemoteDragonActiveFollowImmediateVerticalDistance = 100f;
        private const int RemoteDragonActiveFollowReleaseStableFrameCount = 6;
        private const int RemoteDragonActiveFollowVerticalCheckFrames = 5;
        private const int RemoteDragonPassiveFollowStepMilliseconds = 30;
        private const int MechanicTamingMobItemId = 1932016;
        private const int PaladinDamageReactiveSpecialSkillId = 1220006;
        private const int CarryItemEffectMaximumCount = 99;
        private const int CarryItemEffectAnimationOffsetMs = 120;
        private const int CarryItemEffectRotationResetDurationMs = 2000;
        private const int CarryItemEffectRandomRotationRangeDegrees = 360;
        private const int RelationshipOverlayVisibleRangeX = 700;
        private const int RelationshipOverlayVisibleRangeY = 500;
        private const int RelationshipOverlayNearRangeX = 100;
        private const int RelationshipOverlayNearRangeY = 100;
        private const int NewYearCardOverlayNearRangeX = 250;
        private const int NewYearCardOverlayNearRangeY = 250;
        private const int NewYearCardDefaultItemId = 4300000;
        private const int RemoteShadowPartnerClientSideOffsetPx = 30;
        private const int RemoteShadowPartnerClientBackActionOffsetYPx = 50;
        private const int RemoteShadowPartnerTransitionDurationMs = 200;
        private const int RemoteShadowPartnerAttackDelayMs = 90;
        private const int RemoteMoreWildDamageUpSkillId = 35121010;
        private const int RemoteMovingShootNoPrepareAnimationSkillId = 33121009;
        private const int RemoteReceiveHpGaugeCanvasWidth = 52;
        private const int RemoteReceiveHpGaugeCanvasHeight = 10;
        private const int RemoteReceiveHpGaugeFillWidth = 46;
        private const int RemoteReceiveHpGaugeFillHeight = 4;
        private const int RemoteReceiveHpGaugeFillLeft = 3;
        private const int RemoteReceiveHpGaugeFillTop = 3;
        private const int RemoteReceiveHpGaugeFillAccentTop = 6;
        private const int RemoteReceiveHpGaugeVerticalPadding = 4;
        private static readonly AvatarRenderLayer[] RemoteActiveEffectMotionBlurLayerCaptureOrder =
        {
            AvatarRenderLayer.Face,
            AvatarRenderLayer.OverFace,
            AvatarRenderLayer.UnderFace,
            AvatarRenderLayer.OverCharacter,
            AvatarRenderLayer.UnderCharacter
        };
        private const int RemoteGrenadeSkillId = 5201002;
        private const int MonsterBombSkillId = 4341003;
        private const int RemoteHitAssassinateEmotionSkillId = 4120002;
        private const int RemoteHitAssassinateMirrorEmotionSkillId = 4220002;
        private const int MonsterBombMinimumKeyDownMs = 500;
        private const int MonsterBombGaugeDistance = 900;
        private const int MonsterBombGaugeDistanceSquaredPlusFloor = 832500;
        private const int MonsterBombInitDelayMs = 450;
        private const int MonsterBombExplosionDelayMs = 950;
        private const int MonsterBombLimitTimeMs = 950;
        private const int MonsterBombCollisionRadius = 145;
        private const int MonsterBombAnimationLayerOffsetX = 50;
        private const int GenericGrenadeImpactScale = 600;
        private const int GenericGrenadeFlightLaunchOffsetX = 1;
        private const int GenericGrenadeFlightLaunchOffsetY = -20;
        private const int GrenadeDragScale = 100000;
        private static readonly int[] RemoteShadowPartnerSkillIds =
        {
            4111002,
            4211008,
            14111000
        };
        private static readonly int[] RemoteSoulArrowSkillIds =
        {
            4121006,
            33101003,
            13101003,
            3201004,
            3101004
        };
        private static readonly int[] RemoteAuraSkillIds =
        {
            RemoteUserTemporaryStatKnownState.AdvancedDarkAuraSkillId,
            RemoteUserTemporaryStatKnownState.AdvancedBlueAuraSkillId,
            RemoteUserTemporaryStatKnownState.AdvancedYellowAuraSkillId,
            RemoteUserTemporaryStatKnownState.DarkAuraSkillId,
            RemoteUserTemporaryStatKnownState.BlueAuraSkillId,
            RemoteUserTemporaryStatKnownState.YellowAuraSkillId
        };
        private static readonly int[] RemoteBarrierSkillIds =
        {
            21120007,
            23111005,
            20011010,
            20001010,
            10001010
        };
        private static readonly int[] RemoteBlessingArmorSkillIds =
        {
            RemoteUserTemporaryStatKnownState.PaladinBlessingArmorSkillId,
            RemoteUserTemporaryStatKnownState.BishopBlessingArmorSkillId
        };
        private static readonly int[] RemoteWeaponChargeSkillIds =
        {
            1211004,
            1211006,
            1211008,
            1221004,
            15101006,
            21111005
        };
        private static readonly int[] RemoteEnergyChargeSkillIds =
        {
            5110001,
            15100004
        };
        private const int BuccaneerEnergyChargeMinimumFullChargeValue = 105;
        private const int ThunderBreakerEnergyChargeMinimumFullChargeValue = 160;
        private static readonly int[] RemoteMoreWildDamageUpSkillIds =
        {
            RemoteMoreWildDamageUpSkillId
        };
        private static readonly HashSet<int> RemotePreparedSkillUseEffectSkillIds = new()
        {
            35001001,
            35101009
        };
        private const string RemoteTemporaryStatSoulArrowActionOwnerName = "aux.remote.temporaryStat.soulArrow.persistent";
        private const string RemoteTemporaryStatWeaponChargeActionOwnerName = "aux.remote.temporaryStat.weaponCharge.persistent";
        private const string RemoteTemporaryStatAuraActionOwnerName = "aux.remote.temporaryStat.aura.persistent";
        private const int RemoteTemporaryStatAffectedLayerTransitionDurationMs = 180;
        private const int RemoteTemporaryStatAffectedLayerShiftCadenceUpdates = 0x21;
        private const int RemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationFallbackMs = 16;
        private const int RemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationMinimumMs = 8;
        private const int RemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationMaximumMs = 50;
        private const string RemoteTemporaryStatMoreWildActionOwnerName = "aux.remote.temporaryStat.moreWild.persistent";
        private const string RemoteTemporaryStatBarrierActionOwnerName = "aux.remote.temporaryStat.barrier.persistent";
        private const string RemoteTemporaryStatBlessingArmorActionOwnerName = "aux.remote.temporaryStat.blessingArmor.persistent";
        private const string RemoteTemporaryStatRepeatEffectActionOwnerName = "aux.remote.temporaryStat.repeatEffect.persistent";
        private const string RemoteTemporaryStatMagicShieldActionOwnerName = "aux.remote.temporaryStat.magicShield.persistent";
        private const string RemoteTemporaryStatFinalCutActionOwnerName = "aux.remote.temporaryStat.finalCut.persistent";
        private const string RemotePacketOwnedEmotionActionOwnerName = "aux.remote.packetOwnedEmotion.persistent";
        private const string RemoteEffectByItemActionOwnerName = "aux.remote.effectByItem.oneTime";
        private const string RemoteCarryItemEffectActionOwnerName = "aux.remote.carryItemEffect.persistent";
        private const string RemoteCompletedSetItemEffectActionOwnerName = "aux.remote.completedSetItemEffect.persistent";
        private const string RemoteQuestDeliveryActionOwnerName = "aux.remote.questDelivery.persistent";
        private const string RemoteActiveEffectMotionBlurActionOwnerName = "aux.remote.activeEffectMotionBlur.persistent";
        private const string RemoteActiveEffectItemEffectActionOwnerName = "aux.remote.activeEffectItemEffect.persistent";
        private const string RemoteRelationshipOverlayGenericActionOwnerName = "aux.remote.relationshipOverlay.generic.oneTime";
        private const string RemoteRelationshipOverlayCoupleActionOwnerName = "aux.remote.relationshipOverlay.couple.persistent";
        private const string RemoteRelationshipOverlayFriendshipActionOwnerName = "aux.remote.relationshipOverlay.friendship.persistent";
        private const string RemoteRelationshipOverlayNewYearCardActionOwnerName = "aux.remote.relationshipOverlay.newYearCard.persistent";
        private const string RemoteRelationshipOverlayMarriageActionOwnerName = "aux.remote.relationshipOverlay.marriage.persistent";
        private static readonly Color RemoteShadowPartnerTint = new(255, 255, 255, 150);
        private static readonly EquipSlot[] BattlefieldAppearanceSlots =
        {
            EquipSlot.Cap,
            EquipSlot.Coat,
            EquipSlot.Longcoat,
            EquipSlot.Pants,
            EquipSlot.Shoes,
            EquipSlot.Glove,
            EquipSlot.Cape,
        };
        private static readonly Dictionary<int, RemoteDragonHudMetadata> RemoteDragonHudMetadataCache = new();
        private static readonly IComparer<RemoteUserActor> VisibleWorldActorComparer = Comparer<RemoteUserActor>.Create(static (left, right) =>
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return 1;
            }

            if (right is null)
            {
                return -1;
            }

            int yComparison = left.Position.Y.CompareTo(right.Position.Y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        });
        private static readonly IReadOnlyDictionary<int, RemoteMechanicModePresentation> RemoteMechanicModePresentationBySkillId =
            new Dictionary<int, RemoteMechanicModePresentation>
            {
                [35121005] = new("tank_stand", "tank_walk", "tank", "tank_prone"),
                [35111004] = new("siege_stand", "siege_stand", "siege", "siege_stand"),
                [35121013] = new("tank_siegestand", "tank_siegestand", "tank_siegeattack", "tank_siegestand")
            };

        private readonly Dictionary<int, RemoteUserActor> _actorsById = new();
        private readonly Dictionary<string, int> _actorIdsByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, PortableChairPairRecord> _portableChairPairRecordsByCharacterId = new();
        private int? _localPortableChairPreferredPairCharacterId;
        private readonly Dictionary<RemoteRelationshipOverlayType, Dictionary<int, RemoteUserRelationshipRecord>> _relationshipRecordsByOwnerCharacterId = new();
        private readonly Dictionary<RemoteRelationshipOverlayType, Dictionary<RemoteRelationshipRecordDispatchKey, int>> _relationshipRecordOwnerByDispatchKey = new();
        private readonly List<RemoteUserActor> _visibleWorldActorsBuffer = new();
        private readonly List<StatusBarPreparedSkillRenderData> _preparedSkillWorldOverlayBuffer = new();
        private readonly List<MinimapUI.TrackedUserMarker> _helperMarkerBuffer = new();
        private static readonly RemoteRelationshipOverlayType[] RelationshipRecordOverlayTypes =
        {
            RemoteRelationshipOverlayType.Couple,
            RemoteRelationshipOverlayType.Friendship,
            RemoteRelationshipOverlayType.NewYearCard,
            RemoteRelationshipOverlayType.Marriage
        };
        private readonly HashSet<(int LeftId, int RightId)> _renderedCouplePairsBuffer = new();
        private readonly HashSet<(RemoteRelationshipOverlayType Type, int ItemId, int LeftId, int RightId)> _renderedItemEffectPairsBuffer = new();
        private readonly Dictionary<int, List<PendingRemoteTransientSkillUseAvatarEffectState>> _pendingTransientSkillUseAvatarEffectsByCharacterId = new();
        private readonly Dictionary<int, Dictionary<string, RemoteAuxiliaryLayerOwnerCounterState>> _remoteAuxiliaryLayerOwnerCountersByCharacterId = new();
        private readonly Dictionary<int, bool> _remoteDamageReactiveSpecialBranchCache = new();
        private const int MakerSkillEffectStringPoolId = 0x931;
        private const int MakerResultMessageStringPoolId = 0x1493;
        private const int IncubatorMessageStringPoolId = 0x1559;
        private const int ItemMakeSuccessSoundStringPoolId = 0x507;
        private const int ItemMakeFailureSoundStringPoolId = 0x508;
        private const int ConsumeEffectSoundPathStringPoolId = 0x130C;
        private const int RemoteEffectChatLogType = 7;
        private const string MakerSkillEffectFallbackPath = "Effect/BasicEff.img/DoubleJump";
        private const string MakerResultMessageFallbackFormat = "Maker result {0}.";
        private const string IncubatorMessageFallbackText = "Incubator message.";
        private const string EvolRingStatusBarEffectName = "Eff_EvolRing";
        private const string ItemMakeSuccessSoundFallbackPath = "Sound/Game.img/EnchantSuccess";
        private const string ItemMakeFailureSoundFallbackPath = "Sound/Game.img/EnchantFailure";
        private const string ConsumeEffectSoundPathFallbackFormat = "Sound/ConsumeEffect.img/{0}/Use";
        private int _preparedSkillWorldOverlayCount;
        private int _helperMarkerCount;
        private CharacterLoader _loader;
        private SkillLoader _skillLoader;
        private DragonActionLoader _remoteDragonActionLoader;
        private GraphicsDevice _remoteDragonActionLoaderDevice;
        private Texture2D _pixelTexture;
        private GraphicsDevice _pixelTextureDevice;
        private Func<int, IReadOnlyList<ActiveSummon>> _packetOwnedSummonResolver;

        public int Count => _actorsById.Count;
        public IEnumerable<RemoteUserActor> Actors => _actorsById.Values;
        public event Action<RemoteSkillUsePresentation> SkillUseRegistered;
        public event Action<RemoteUpgradeTombPresentation> UpgradeTombEffectRegistered;
        public event Action<RemoteGenericUserStatePresentation> GenericUserStateRegistered;
        public event Action<RemoteItemMakePresentation> ItemMakeRegistered;
        public event Action<RemoteHitFeedbackPresentation> HitFeedbackRegistered;
        public event Action<RemoteMobAttackHitPresentation> MobAttackHitEffectRegistered;
        public event Action<RemoteMobDamageInfoPresentation> MobDamageInfoRegistered;
        public event Action<RemoteFieldSoundPresentation> FieldSoundRegistered;
        public event Action<RemoteClientSoundPresentation> ClientSoundRegistered;
        public event Action<RemoteStringEffectPresentation> StringEffectRegistered;
        public event Action<RemoteChatLogMessagePresentation> ChatLogMessageRegistered;
        public event Action<RemoteStatusBarEffectPresentation> StatusBarEffectRegistered;
        public event Action<RemoteGrenadePresentation> GrenadeRegistered;
        public event Action<RemoteHookingChainPresentation> HookingChainRegistered;
        public int PreparedSkillWorldOverlayCount => _preparedSkillWorldOverlayCount;
        public int HelperMarkerCount => _helperMarkerCount;
        public Action<int, string> ActorRemovedCallback { get; set; }

        public void SetPacketOwnedSummonResolver(Func<int, IReadOnlyList<ActiveSummon>> packetOwnedSummonResolver)
        {
            _packetOwnedSummonResolver = packetOwnedSummonResolver;
        }

        public void Initialize(CharacterLoader loader, SkillLoader skillLoader)
        {
            _loader = loader;
            _skillLoader = skillLoader;
            EnsureRelationshipRecordTablesInitialized();
        }

        public void Clear()
        {
            foreach (RemoteUserActor actor in _actorsById.Values.ToArray())
            {
                ClearActorFollowLinks(actor);
                ClearRemoteActiveEffectMotionBlurState(actor);
                ClearRemoteActiveEffectItemEffectState(actor);
                ReleaseCarryItemEffectLayerReferenceForParity(actor);
                ReleaseCompletedSetItemEffectLayerReferenceForParity(actor);
                ClearRemoteTransientItemEffectStates(actor);
                ClearRemoteDragonCompanionState(actor);
                NotifyActorRemoved(actor.CharacterId, actor.Name);
            }

            _actorsById.Clear();
            _actorIdsByName.Clear();
            _pendingTransientSkillUseAvatarEffectsByCharacterId.Clear();
            _remoteAuxiliaryLayerOwnerCountersByCharacterId.Clear();
            _portableChairPairRecordsByCharacterId.Clear();
            _localPortableChairPreferredPairCharacterId = null;
            ClearRelationshipRecordTables();
        }

        public void RemoveBySourceTag(string sourceTag)
        {
            if (string.IsNullOrWhiteSpace(sourceTag))
            {
                return;
            }

            foreach (int characterId in _actorsById.Values
                .Where(actor => string.Equals(actor.SourceTag, sourceTag.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(actor => actor.CharacterId)
                .ToArray())
            {
                if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
                {
                    ClearActorFollowLinks(actor);
                    ClearRemoteActiveEffectMotionBlurState(actor);
                    ClearRemoteActiveEffectItemEffectState(actor);
                    ReleaseCarryItemEffectLayerReferenceForParity(actor);
                    ReleaseCompletedSetItemEffectLayerReferenceForParity(actor);
                    ClearRemoteTransientItemEffectStates(actor);
                    ClearRemoteDragonCompanionState(actor);
                    ClearPortableChairPairRecord(characterId);
                    PurgeRelationshipRecordsForActor(characterId);
                    NotifyActorRemoved(actor.CharacterId, actor.Name);
                    _actorIdsByName.Remove(actor.Name);
                    _actorsById.Remove(characterId);
                    _pendingTransientSkillUseAvatarEffectsByCharacterId.Remove(characterId);
                    _remoteAuxiliaryLayerOwnerCountersByCharacterId.Remove(characterId);
                }
            }
        }

        public void RemoveBySourceTagExcept(string sourceTag, IReadOnlySet<int> keepCharacterIds)
        {
            if (string.IsNullOrWhiteSpace(sourceTag))
            {
                return;
            }

            keepCharacterIds ??= new HashSet<int>();
            foreach (int characterId in _actorsById.Values
                .Where(actor => string.Equals(actor.SourceTag, sourceTag.Trim(), StringComparison.OrdinalIgnoreCase)
                    && !keepCharacterIds.Contains(actor.CharacterId))
                .Select(actor => actor.CharacterId)
                .ToArray())
            {
                if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
                {
                    ClearActorFollowLinks(actor);
                    ClearRemoteActiveEffectMotionBlurState(actor);
                    ClearRemoteActiveEffectItemEffectState(actor);
                    ReleaseCarryItemEffectLayerReferenceForParity(actor);
                    ReleaseCompletedSetItemEffectLayerReferenceForParity(actor);
                    ClearRemoteTransientItemEffectStates(actor);
                    ClearRemoteDragonCompanionState(actor);
                    ClearPortableChairPairRecord(characterId);
                    PurgeRelationshipRecordsForActor(characterId);
                    NotifyActorRemoved(actor.CharacterId, actor.Name);
                    _actorIdsByName.Remove(actor.Name);
                    _actorsById.Remove(characterId);
                    _remoteAuxiliaryLayerOwnerCountersByCharacterId.Remove(characterId);
                }
            }
        }

        public bool TryGetActor(int characterId, out RemoteUserActor actor)
        {
            return _actorsById.TryGetValue(characterId, out actor);
        }

        public bool TryGetActorByName(string name, out RemoteUserActor actor)
        {
            actor = null;
            return !string.IsNullOrWhiteSpace(name)
                   && _actorIdsByName.TryGetValue(name.Trim(), out int characterId)
                   && _actorsById.TryGetValue(characterId, out actor);
        }

        public bool TryGetPosition(string name, out Vector2 position)
        {
            if (TryGetActorByName(name, out RemoteUserActor actor))
            {
                position = actor.Position;
                return true;
            }

            position = default;
            return false;
        }

        public bool TryAddOrUpdate(
            int characterId,
            CharacterBuild build,
            Vector2 position,
            out string message,
            bool facingRight = true,
            string actionName = null,
            string sourceTag = null,
            bool isVisibleInWorld = true)
        {
            message = null;
            if (characterId <= 0)
            {
                message = "Remote character ID must be positive.";
                return false;
            }

            if (build == null)
            {
                message = "Remote character build is required.";
                return false;
            }

            build.Id = characterId;
            if (string.IsNullOrWhiteSpace(build.Name))
            {
                build.Name = $"Remote{characterId}";
            }

            if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                string previousName = actor.Name;
                actor.BeginMeleeAfterImageFade(Environment.TickCount);
                ResetBattlefieldAppearanceState(actor);
                actor.Build = build;
                actor.Name = build.Name.Trim();
                actor.Position = position;
                actor.FacingRight = facingRight;
                SetActorAction(actor, actionName, actor.Build.ActivePortableChair != null, Environment.TickCount);
                actor.SourceTag = string.IsNullOrWhiteSpace(sourceTag) ? actor.SourceTag : sourceTag.Trim();
                actor.IsVisibleInWorld = isVisibleInWorld;
                SyncTemporaryStatPresentation(actor);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
                UpdateNameLookup(previousName, actor.Name, characterId);
                SyncRelationshipOverlaysFromRecords(actor.CharacterId, Environment.TickCount);
                ApplyPendingTransientSkillUseAvatarEffects(actor, Environment.TickCount);
                return true;
            }

            RemoteUserActor created = new RemoteUserActor(
                characterId,
                build.Name.Trim(),
                build,
                position,
                facingRight,
                NormalizeActionName(actionName, build.ActivePortableChair != null),
                string.IsNullOrWhiteSpace(sourceTag) ? "remote" : sourceTag.Trim(),
                isVisibleInWorld);
            created.BaseActionName = created.ActionName;
            RegisterMeleeAfterImage(created, 0, created.ActionName, Environment.TickCount, 10, 0);
            _actorsById[characterId] = created;
            _actorIdsByName[created.Name] = characterId;
            SyncTemporaryStatPresentation(created);
            SyncRelationshipOverlaysFromRecords(created.CharacterId, Environment.TickCount);
            ApplyPendingTransientSkillUseAvatarEffects(created, Environment.TickCount);
            return true;
        }

        public bool TryAddOrUpdateAvatarLook(
            int characterId,
            string name,
            LoginAvatarLook avatarLook,
            CharacterBuild template,
            Vector2 position,
            out string message,
            bool facingRight,
            string actionName,
            string sourceTag,
            bool isVisibleInWorld)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (avatarLook == null)
            {
                message = "AvatarLook payload is required.";
                return false;
            }

            CharacterBuild build = _loader.LoadFromAvatarLook(avatarLook, template);
            if (build == null)
            {
                message = "AvatarLook could not be converted into a remote character build.";
                return false;
            }

            ClearTemplateOnlyProfileState(build);

            if (!string.IsNullOrWhiteSpace(name))
            {
                build.Name = name.Trim();
            }

            return TryAddOrUpdate(
                characterId,
                build,
                position,
                out message,
                facingRight,
                actionName,
                sourceTag,
                isVisibleInWorld);
        }

        private static void ClearTemplateOnlyProfileState(CharacterBuild build)
        {
            if (build == null)
            {
                return;
            }

            // AvatarLook packets do not author these profile-window fields. Clear them so
            // remote inspect does not inherit unrelated local-player metadata from the
            // loader template used for visual decode fallbacks.
            build.ActivePortableChair = null;
            build.HasAuthoritativeProfileLevel = false;
            build.HasAuthoritativeProfileJob = false;
            build.HasAuthoritativeProfileGuild = false;
            build.HasAuthoritativeProfileAlliance = false;
            build.HasAuthoritativeProfileFame = false;
            build.HasAuthoritativeProfileWorldRank = false;
            build.HasAuthoritativeProfileJobRank = false;
            build.HasAuthoritativeProfileRide = false;
            build.HasAuthoritativeProfileTraits = false;
            build.HasAuthoritativeProfilePendantSlot = false;
            build.HasAuthoritativeProfilePocketSlot = false;
            build.HasAuthoritativeProfileMedal = false;
            build.HasAuthoritativeProfileCollection = false;
            build.HasAuthoritativeProfileMarriage = false;
            build.IsProfileMarried = false;
            build.GuildName = string.Empty;
            build.AllianceName = string.Empty;
            build.Fame = 0;
            build.WorldRank = 0;
            build.JobRank = 0;
            build.HasMonsterRiding = false;
            build.HasPendantSlotExtension = false;
            build.HasPocketSlot = false;
            build.TraitCharisma = 0;
            build.TraitInsight = 0;
            build.TraitWill = 0;
            build.TraitCraft = 0;
            build.TraitSense = 0;
            build.TraitCharm = 0;
        }

        public bool TryApplyProfileMetadata(
            int characterId,
            int? level,
            string guildName,
            int? jobId,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor) || actor?.Build == null)
            {
                message = $"Remote user {characterId} does not exist.";
                return false;
            }

            CharacterBuild build = actor.Build;
            ApplyBasicProfileMetadata(build, level, guildName, jobId);

            message = $"Remote user {characterId} profile metadata applied.";
            return true;
        }

        public bool TryApplyProfileMetadata(RemoteUserProfilePacket packet, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor) || actor?.Build == null)
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            CharacterBuild build = actor.Build;
            ApplyBasicProfileMetadata(build, packet.Level, packet.GuildName, packet.JobId);

            if (packet.AllianceName != null)
            {
                build.AllianceName = string.IsNullOrWhiteSpace(packet.AllianceName) ? string.Empty : packet.AllianceName.Trim();
                build.HasAuthoritativeProfileAlliance = true;
            }

            if (packet.Fame.HasValue)
            {
                build.Fame = packet.Fame.Value;
                build.HasAuthoritativeProfileFame = true;
            }

            if (packet.WorldRank.HasValue)
            {
                build.WorldRank = Math.Max(0, packet.WorldRank.Value);
                build.HasAuthoritativeProfileWorldRank = true;
            }

            if (packet.JobRank.HasValue)
            {
                build.JobRank = Math.Max(0, packet.JobRank.Value);
                build.HasAuthoritativeProfileJobRank = true;
            }

            if (packet.PreviousWorldRank.HasValue)
            {
                build.ProfilePreviousWorldRank = Math.Max(0, packet.PreviousWorldRank.Value);
            }

            if (packet.PreviousJobRank.HasValue)
            {
                build.ProfilePreviousJobRank = Math.Max(0, packet.PreviousJobRank.Value);
            }

            if (packet.HasRide.HasValue)
            {
                build.HasMonsterRiding = packet.HasRide.Value;
                build.HasAuthoritativeProfileRide = true;
                if (!packet.HasRide.Value)
                {
                    ClearProfileRide(build);
                }
            }

            if (packet.RideVehicleItemId.HasValue || packet.RideSaddleItemId.HasValue || packet.IsRidingInField.HasValue)
            {
                build.ProfileRideVehicleItemId = Math.Max(0, packet.RideVehicleItemId ?? 0);
                build.ProfileRideSaddleItemId = Math.Max(0, packet.RideSaddleItemId ?? 0);
                build.IsProfileRidingInField = packet.IsRidingInField;
                build.HasMonsterRiding = packet.IsRidingInField == true || build.ProfileRideVehicleItemId > 0 || build.HasMonsterRiding;
                build.HasAuthoritativeProfileRide = true;
            }

            if (packet.HasPendantSlot.HasValue)
            {
                build.HasPendantSlotExtension = packet.HasPendantSlot.Value;
                build.HasAuthoritativeProfilePendantSlot = true;
            }

            if (packet.HasPocketSlot.HasValue)
            {
                build.HasPocketSlot = packet.HasPocketSlot.Value;
                build.HasAuthoritativeProfilePocketSlot = true;
            }

            bool hasTraitUpdate = false;
            if (packet.TraitCharisma.HasValue)
            {
                build.TraitCharisma = Math.Max(0, packet.TraitCharisma.Value);
                hasTraitUpdate = true;
            }

            if (packet.TraitInsight.HasValue)
            {
                build.TraitInsight = Math.Max(0, packet.TraitInsight.Value);
                hasTraitUpdate = true;
            }

            if (packet.TraitWill.HasValue)
            {
                build.TraitWill = Math.Max(0, packet.TraitWill.Value);
                hasTraitUpdate = true;
            }

            if (packet.TraitCraft.HasValue)
            {
                build.TraitCraft = Math.Max(0, packet.TraitCraft.Value);
                hasTraitUpdate = true;
            }

            if (packet.TraitSense.HasValue)
            {
                build.TraitSense = Math.Max(0, packet.TraitSense.Value);
                hasTraitUpdate = true;
            }

            if (packet.TraitCharm.HasValue)
            {
                build.TraitCharm = Math.Max(0, packet.TraitCharm.Value);
                hasTraitUpdate = true;
            }

            if (hasTraitUpdate)
            {
                build.HasAuthoritativeProfileTraits = true;
            }

            if (packet.HasMedal.HasValue)
            {
                build.ProfileHasMedal = packet.HasMedal.Value;
                build.HasAuthoritativeProfileMedal = true;
            }

            if (packet.HasCollection.HasValue)
            {
                build.HasAuthoritativeProfileCollection = true;
                if (!packet.HasCollection.Value)
                {
                    ClearProfileCollection(build);
                }
            }

            if (packet.PetProfiles != null)
            {
                build.RemotePetProfiles = packet.PetProfiles
                    .Where(pet => pet.ItemId > 0)
                    .Take(3)
                    .ToArray();
                build.RemotePetItemIds = build.RemotePetProfiles
                    .Select(pet => pet.ItemId)
                    .ToArray();
            }

            if (packet.MonsterBookOwnedCardTypes.HasValue
                || packet.MonsterBookTotalCardTypes.HasValue
                || packet.MonsterBookCompletedCardTypes.HasValue
                || packet.MonsterBookTotalOwnedCopies.HasValue)
            {
                build.ProfileMonsterBookOwnedCardTypes = Math.Max(0, packet.MonsterBookOwnedCardTypes ?? 0);
                build.ProfileMonsterBookTotalCardTypes = Math.Max(0, packet.MonsterBookTotalCardTypes ?? 0);
                build.ProfileMonsterBookCompletedCardTypes = Math.Max(0, packet.MonsterBookCompletedCardTypes ?? 0);
                build.ProfileMonsterBookTotalOwnedCopies = Math.Max(0, packet.MonsterBookTotalOwnedCopies ?? 0);
                build.HasAuthoritativeProfileCollection = true;
            }

            if (packet.MakerGenericLevel.HasValue
                || packet.MakerGloveLevel.HasValue
                || packet.MakerShoeLevel.HasValue
                || packet.MakerToyLevel.HasValue
                || packet.MakerSuccessfulCrafts.HasValue
                || packet.MakerDiscoveredRecipeCount.HasValue
                || packet.MakerUnlockedHiddenRecipeCount.HasValue)
            {
                ApplyProfileMakerProgression(build, packet);
                build.HasAuthoritativeProfileCollection = true;
            }

            message = $"Remote user {packet.CharacterId} profile metadata applied.";
            return true;
        }

        private static void ApplyProfileMakerProgression(CharacterBuild build, RemoteUserProfilePacket packet)
        {
            build.ProfileMakerGenericLevel = Math.Max(1, packet.MakerGenericLevel ?? 1);
            build.ProfileMakerGloveLevel = Math.Max(1, packet.MakerGloveLevel ?? 1);
            build.ProfileMakerShoeLevel = Math.Max(1, packet.MakerShoeLevel ?? 1);
            build.ProfileMakerToyLevel = Math.Max(1, packet.MakerToyLevel ?? 1);
            build.ProfileMakerGenericProgress = Math.Max(0, packet.MakerGenericProgress ?? 0);
            build.ProfileMakerGloveProgress = Math.Max(0, packet.MakerGloveProgress ?? 0);
            build.ProfileMakerShoeProgress = Math.Max(0, packet.MakerShoeProgress ?? 0);
            build.ProfileMakerToyProgress = Math.Max(0, packet.MakerToyProgress ?? 0);
            build.ProfileMakerSuccessfulCrafts = Math.Max(0, packet.MakerSuccessfulCrafts ?? 0);
            build.ProfileMakerDiscoveredRecipeCount = Math.Max(0, packet.MakerDiscoveredRecipeCount ?? 0);
            build.ProfileMakerUnlockedHiddenRecipeCount = Math.Max(0, packet.MakerUnlockedHiddenRecipeCount ?? 0);
        }

        private static void ClearProfileRide(CharacterBuild build)
        {
            build.ProfileRideVehicleItemId = 0;
            build.ProfileRideSaddleItemId = 0;
            build.IsProfileRidingInField = false;
        }

        private static void ClearProfileCollection(CharacterBuild build)
        {
            build.ProfileMonsterBookOwnedCardTypes = 0;
            build.ProfileMonsterBookTotalCardTypes = 0;
            build.ProfileMonsterBookCompletedCardTypes = 0;
            build.ProfileMonsterBookTotalOwnedCopies = 0;
            build.ProfileMakerGenericLevel = 1;
            build.ProfileMakerGloveLevel = 1;
            build.ProfileMakerShoeLevel = 1;
            build.ProfileMakerToyLevel = 1;
            build.ProfileMakerGenericProgress = 0;
            build.ProfileMakerGloveProgress = 0;
            build.ProfileMakerShoeProgress = 0;
            build.ProfileMakerToyProgress = 0;
            build.ProfileMakerSuccessfulCrafts = 0;
            build.ProfileMakerDiscoveredRecipeCount = 0;
            build.ProfileMakerUnlockedHiddenRecipeCount = 0;
        }

        private static void ApplyBasicProfileMetadata(CharacterBuild build, int? level, string guildName, int? jobId)
        {
            if (build == null)
            {
                return;
            }

            if (level.HasValue && level.Value > 0)
            {
                build.Level = Math.Max(1, level.Value);
                build.HasAuthoritativeProfileLevel = true;
            }

            if (jobId.HasValue && jobId.Value >= 0)
            {
                build.Job = jobId.Value;
                build.JobName = SkillDataLoader.GetJobName(jobId.Value);
                build.HasAuthoritativeProfileJob = true;
            }

            if (guildName != null)
            {
                build.GuildName = string.IsNullOrWhiteSpace(guildName) ? string.Empty : guildName.Trim();
                build.HasAuthoritativeProfileGuild = true;
            }
        }

        public bool TryApplyGuildMark(
            int characterId,
            int markBackgroundId,
            int markBackgroundColor,
            int markId,
            int markColor,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor) || actor?.Build == null)
            {
                message = $"Remote user {characterId} does not exist.";
                return false;
            }

            actor.Build.GuildMarkBackgroundId = markBackgroundId > 0 ? markBackgroundId : null;
            actor.Build.GuildMarkBackgroundColor = markBackgroundColor;
            actor.Build.GuildMarkId = markId > 0 ? markId : null;
            actor.Build.GuildMarkColor = markColor;
            message = $"Remote user {characterId} guild mark applied.";
            return true;
        }

        public bool HasActiveMarriageRelationshipRecord(int characterId)
        {
            if (characterId <= 0)
            {
                return false;
            }

            EnsureRelationshipRecordTablesInitialized();
            Dictionary<int, RemoteUserRelationshipRecord> marriageTable = GetRelationshipRecordTable(RemoteRelationshipOverlayType.Marriage);
            if (marriageTable.TryGetValue(characterId, out RemoteUserRelationshipRecord directRecord) &&
                directRecord.IsActive)
            {
                return true;
            }

            foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in marriageTable)
            {
                RemoteUserRelationshipRecord marriageRecord = entry.Value;
                if (!marriageRecord.IsActive)
                {
                    continue;
                }

                if (ResolveMarriagePairCharacterId(entry.Key, marriageRecord) == characterId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetRelationshipRecord(
            RemoteRelationshipOverlayType relationshipType,
            int ownerCharacterId,
            out RemoteUserRelationshipRecord relationshipRecord)
        {
            relationshipRecord = default;
            if (ownerCharacterId <= 0 || relationshipType == RemoteRelationshipOverlayType.Generic)
            {
                return false;
            }

            EnsureRelationshipRecordTablesInitialized();
            return GetRelationshipRecordTable(relationshipType).TryGetValue(ownerCharacterId, out relationshipRecord)
                && relationshipRecord.IsActive;
        }

        public bool TryMove(int characterId, Vector2 position, bool? facingRight, string actionName, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.Position = position;
            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            if (!string.IsNullOrWhiteSpace(actionName))
            {
                actor.BeginMeleeAfterImageFade(Environment.TickCount);
                SetActorAction(
                    actor,
                    actionName,
                    actor.Build.ActivePortableChair != null,
                    Environment.TickCount,
                    forceReplay: true,
                    rawActionCode: CharacterPart.TryGetClientRawActionCode(actionName, out int actionCode) ? actionCode : null);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            }

            return true;
        }

        public bool TryApplyFollowCharacter(
            int characterId,
            int driverId,
            bool transferField,
            Vector2? transferPosition,
            int localCharacterId,
            Vector2 localCharacterPosition,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (driverId > 0 && driverId == characterId)
            {
                message = $"Remote character {characterId} cannot follow itself.";
                return false;
            }

            int previousDriverId = actor.FollowDriverId;
            ClearDriverPassengerLink(previousDriverId, characterId);

            if (driverId > 0)
            {
                actor.FollowDriverId = driverId;
                AssignDriverPassengerLink(driverId, characterId);
                return true;
            }

            actor.FollowDriverId = 0;
            if (transferField && transferPosition.HasValue)
            {
                actor.Position = transferPosition.Value;
            }
            else if (previousDriverId > 0)
            {
                if (previousDriverId == localCharacterId)
                {
                    actor.Position = localCharacterPosition;
                }
                else if (_actorsById.TryGetValue(previousDriverId, out RemoteUserActor previousDriver))
                {
                    actor.Position = previousDriver.Position;
                }
            }

            return true;
        }

        public bool TryAssignLocalPassengerToDriver(int driverId, int localPassengerId, out string message)
        {
            message = null;
            if (driverId <= 0)
            {
                message = "Remote driver ID must be positive.";
                return false;
            }

            if (localPassengerId <= 0)
            {
                message = "Local passenger ID must be positive.";
                return false;
            }

            if (!_actorsById.TryGetValue(driverId, out _))
            {
                message = $"Remote driver {driverId} does not exist.";
                return false;
            }

            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.FollowPassengerId == localPassengerId)
                {
                    actor.FollowPassengerId = 0;
                }
            }

            AssignDriverPassengerLink(driverId, localPassengerId);
            return true;
        }

        public bool TryClearLocalPassengerFromDriver(int driverId, int localPassengerId, out string message)
        {
            message = null;
            if (driverId <= 0)
            {
                message = "Remote driver ID must be positive.";
                return false;
            }

            if (localPassengerId <= 0)
            {
                message = "Local passenger ID must be positive.";
                return false;
            }

            if (!_actorsById.TryGetValue(driverId, out RemoteUserActor driverActor))
            {
                message = $"Remote driver {driverId} does not exist.";
                return false;
            }

            if (driverActor.FollowPassengerId == localPassengerId)
            {
                driverActor.FollowPassengerId = 0;
            }

            return true;
        }

        public bool TrySetAction(int characterId, string actionName, bool? facingRight, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.BeginMeleeAfterImageFade(Environment.TickCount);
            SetActorAction(
                actor,
                actionName,
                actor.Build.ActivePortableChair != null,
                Environment.TickCount,
                forceReplay: true,
                rawActionCode: CharacterPart.TryGetClientRawActionCode(actionName, out int actionCode) ? actionCode : null);
            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            actor.MovementDrivenActionSelection = false;
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryRegisterMeleeAfterImage(
            int characterId,
            int skillId,
            string actionName,
            int? actionCode,
            int masteryPercent,
            int chargeSkillId,
            int? actionSpeed,
            int? preparedSkillReleaseFollowUpValue,
            IReadOnlyList<RemoteUserMeleeAttackMobHit> mobHits,
            bool? facingRight,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            if (TryApplyRemotePreparedSkillRelease(actor, skillId, preparedSkillReleaseFollowUpValue, out int releasedPreparedSkillId))
            {
                TryRegisterRemotePreparedSkillReleaseSkillUse(actor, releasedPreparedSkillId, currentTime);
            }

            SkillData skill = null;
            if (skillId > 0 && _skillLoader != null)
            {
                skill = _skillLoader.LoadSkill(skillId);
            }

            int chargeElement = ResolveRemoteAfterImageChargeElement(
                actor,
                chargeSkillId,
                skillId,
                skill,
                admitSkillElementAttribute: true);
            string resolvedActionName = ResolveRemoteMeleeActionName(
                actor,
                skill,
                actionName,
                actionCode,
                masteryPercent,
                chargeElement);

            if (!string.IsNullOrWhiteSpace(resolvedActionName))
            {
                actor.BeginMeleeAfterImageFade(currentTime);
                SetActorAction(
                    actor,
                    resolvedActionName,
                    actor.Build.ActivePortableChair != null,
                    currentTime,
                    forceReplay: true,
                    rawActionCode: actionCode);
            }
            RegisterMeleeAfterImage(actor, skillId, actor.ActionName, currentTime, masteryPercent, chargeElement, actionCode);
            if (skillId > 0)
            {
                SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                    actor.CharacterId,
                    skillId,
                    actionSpeed,
                    actor.FacingRight,
                    currentTime));
                TryRegisterRemoteHookingChainPresentation(actor, skill, mobHits, currentTime);
            }

            return true;
        }

        public bool TryApplyTransientSkillUseAvatarEffect(
            int characterId,
            int registrationKey,
            SkillAnimation overlayAnimation,
            SkillAnimation underFaceAnimation,
            int currentTime,
            out string message)
        {
            message = null;
            if (registrationKey <= 0 || (overlayAnimation == null && underFaceAnimation == null))
            {
                message = "Transient skill-use avatar effect data is incomplete.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            ApplyTransientSkillUseAvatarEffect(actor, registrationKey, overlayAnimation, underFaceAnimation, currentTime);
            return true;
        }

        private void TryRegisterRemoteHookingChainPresentation(
            RemoteUserActor actor,
            SkillData skill,
            IReadOnlyList<RemoteUserMeleeAttackMobHit> mobHits,
            int currentTime)
        {
            if (actor == null
                || skill == null
                || mobHits == null
                || mobHits.Count == 0
                || !SkillManager.ShouldRegisterSecondaryHookingChainOwner(skill)
                || !SkillManager.TryResolveSecondaryHookingChainFrames(skill, out _, out _))
            {
                return;
            }

            List<int> mobObjectIds = new(mobHits.Count);
            for (int i = 0; i < mobHits.Count; i++)
            {
                int mobObjectId = mobHits[i].MobId;
                if (mobObjectId > 0 && !mobObjectIds.Contains(mobObjectId))
                {
                    mobObjectIds.Add(mobObjectId);
                }
            }

            if (mobObjectIds.Count == 0)
            {
                return;
            }

            HookingChainRegistered?.Invoke(new RemoteHookingChainPresentation(
                actor.CharacterId,
                skill,
                mobObjectIds,
                actor.FacingRight,
                currentTime));
        }

        public bool TryQueueTransientSkillUseAvatarEffect(
            int characterId,
            int registrationKey,
            SkillAnimation overlayAnimation,
            SkillAnimation underFaceAnimation,
            int currentTime,
            out string message)
        {
            message = null;
            if (characterId <= 0)
            {
                message = "Remote character ID must be positive.";
                return false;
            }

            if (registrationKey <= 0 || (overlayAnimation == null && underFaceAnimation == null))
            {
                message = "Transient skill-use avatar effect data is incomplete.";
                return false;
            }

            if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                ApplyTransientSkillUseAvatarEffect(actor, registrationKey, overlayAnimation, underFaceAnimation, currentTime);
                return true;
            }

            if (!_pendingTransientSkillUseAvatarEffectsByCharacterId.TryGetValue(characterId, out var pendingEffects))
            {
                pendingEffects = new List<PendingRemoteTransientSkillUseAvatarEffectState>();
                _pendingTransientSkillUseAvatarEffectsByCharacterId[characterId] = pendingEffects;
            }

            pendingEffects.RemoveAll(effect => effect?.RegistrationKey == registrationKey);
            pendingEffects.Add(new PendingRemoteTransientSkillUseAvatarEffectState
            {
                RegistrationKey = registrationKey,
                OverlayAnimation = overlayAnimation,
                UnderFaceAnimation = underFaceAnimation
            });
            message = $"Queued transient skill-use avatar effect for remote character {characterId}.";
            return true;
        }

        public bool TryQueueTransientSkillUseAvatarEffect(
            int characterId,
            int registrationKey,
            SkillAnimation overlayAnimation,
            SkillAnimation underFaceAnimation,
            out string message)
        {
            return TryQueueTransientSkillUseAvatarEffect(
                characterId,
                registrationKey,
                overlayAnimation,
                underFaceAnimation,
                Environment.TickCount,
                out message);
        }

        private static void ApplyTransientSkillUseAvatarEffect(
            RemoteUserActor actor,
            int registrationKey,
            SkillAnimation overlayAnimation,
            SkillAnimation underFaceAnimation,
            int currentTime)
        {
            actor.TransientSkillUseAvatarEffects.RemoveAll(effect => effect?.RegistrationKey == registrationKey);
            actor.TransientSkillUseAvatarEffects.Add(new RemoteTransientSkillUseAvatarEffectState
            {
                RegistrationKey = registrationKey,
                OverlayAnimation = overlayAnimation,
                UnderFaceAnimation = underFaceAnimation,
                AnimationStartTime = currentTime
            });
        }

        private void ApplyPendingTransientSkillUseAvatarEffects(RemoteUserActor actor, int currentTime)
        {
            if (actor == null ||
                !_pendingTransientSkillUseAvatarEffectsByCharacterId.TryGetValue(actor.CharacterId, out var pendingEffects) ||
                pendingEffects == null ||
                pendingEffects.Count == 0)
            {
                return;
            }

            for (int i = 0; i < pendingEffects.Count; i++)
            {
                PendingRemoteTransientSkillUseAvatarEffectState pendingEffect = pendingEffects[i];
                if (pendingEffect == null)
                {
                    continue;
                }

                ApplyTransientSkillUseAvatarEffect(
                    actor,
                    pendingEffect.RegistrationKey,
                    pendingEffect.OverlayAnimation,
                    pendingEffect.UnderFaceAnimation,
                    currentTime);
            }

            _pendingTransientSkillUseAvatarEffectsByCharacterId.Remove(actor.CharacterId);
        }

        public bool TryApplyMoveAction(int characterId, byte moveAction, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.LastMoveActionRaw = moveAction;
            actor.FacingRight = DecodeFacingRight(moveAction);
            actor.BeginMeleeAfterImageFade(Environment.TickCount);
            SetActorAction(
                actor,
                ResolveActionName(actor, MoveActionFromRaw(moveAction)),
                actor.Build.ActivePortableChair != null,
                Environment.TickCount,
                rawActionCode: moveAction);
            actor.MovementDrivenActionSelection = true;
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryApplyMoveSnapshot(int characterId, PlayerMovementSyncSnapshot movementSnapshot, byte moveAction, int currentTime, out string message)
        {
            message = null;
            if (movementSnapshot == null)
            {
                message = "Remote movement snapshot is required.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.MovementSnapshot = movementSnapshot;
            actor.LastMoveActionRaw = moveAction;
            actor.MovementDrivenActionSelection = true;
            ApplyMovementSnapshot(actor, currentTime);
            return true;
        }

        public bool TrySetHelperMarker(int characterId, MinimapUI.HelperMarkerType? markerType, bool showDirectionOverlay, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.HelperMarkerType = markerType;
            actor.HasPacketAuthoredHelperState = true;
            actor.ShowDirectionOverlay = showDirectionOverlay;
            return true;
        }

        public bool TrySetBattlefieldTeam(int characterId, int? teamId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.BattlefieldTeamId = teamId;
            return true;
        }

        public void SyncBattlefieldAppearance(BattlefieldField battlefield)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                SyncBattlefieldAppearance(actor, battlefield);
            }
        }

        public bool TrySetPortableChair(
            int characterId,
            int? chairItemId,
            out string message,
            int? pairCharacterId = null,
            bool allowPairRecordCreateFromSeatState = true)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            ClearPortableChairMountState(actor);
            if (!chairItemId.HasValue || chairItemId.Value <= 0)
            {
                actor.Build.ActivePortableChair = null;
                actor.PreferredPortableChairPairCharacterId = null;
                SyncPortableChairPairRecordFromSeatState(
                    characterId,
                    chairItemId ?? 0,
                    pairCharacterId,
                    allowPairRecordCreateFromSeatState);
                SetActorAction(actor, CharacterPart.GetActionString(CharacterAction.Stand1), allowSitFallback: false, Environment.TickCount);
                SyncTemporaryStatPresentation(actor);
                actor.ClearMeleeAfterImage();
                return true;
            }

            PortableChair chair = _loader.LoadPortableChair(chairItemId.Value);
            if (chair == null)
            {
                message = $"Portable chair {chairItemId.Value} could not be loaded.";
                return false;
            }

            actor.Build.ActivePortableChair = chair;
            actor.PreferredPortableChairPairCharacterId = pairCharacterId;
            SyncPortableChairPairRecordFromSeatState(
                characterId,
                chair.ItemId,
                pairCharacterId,
                allowPairRecordCreateFromSeatState);

            ApplyPortableChairMount(actor, chair);
            SetActorAction(actor, "sit", allowSitFallback: true, Environment.TickCount);
            SyncTemporaryStatPresentation(actor);
            actor.ClearMeleeAfterImage();
            return true;
        }

        public bool TryApplyPortableChairRecordAdd(
            RemoteUserPortableChairRecordAddPacket packet,
            out string message)
        {
            message = null;
            if (packet.CharacterId <= 0)
            {
                message = "Portable-chair record add requires a positive character ID.";
                return false;
            }

            if (packet.ChairItemId <= 0)
            {
                message = "Portable-chair record add requires a positive chair item ID.";
                return false;
            }

            if (!IsCoupleChairRecordItemIdForClientParity(packet.ChairItemId))
            {
                message = $"Portable-chair record add item {packet.ChairItemId} is not a couple-chair record item.";
                return false;
            }

            RemoteUserPortableChairRecordAddPacket normalizedPacket =
                NormalizePortableChairRecordAddForClientParity(packet);

            TryApplyPortableChairRecordRemove(
                new RemoteUserPortableChairRecordRemovePacket(normalizedPacket.CharacterId),
                out _);
            SyncPortableChairPairRecord(
                normalizedPacket.CharacterId,
                normalizedPacket.ChairItemId,
                ResolvePortableChairPairPreference(normalizedPacket.CharacterId),
                normalizedPacket.PairCharacterId,
                normalizedPacket.Status);

            message = _actorsById.ContainsKey(normalizedPacket.CharacterId)
                ? $"Remote user {normalizedPacket.CharacterId} couple-chair record applied."
                : $"Stored remote couple-chair record for inactive owner {normalizedPacket.CharacterId}.";
            return true;
        }

        public bool TryApplyPortableChairRecordRemove(
            RemoteUserPortableChairRecordRemovePacket packet,
            out string message)
        {
            message = $"No remote couple-chair record matched character {packet.CharacterId}.";
            if (!_portableChairPairRecordsByCharacterId.TryGetValue(packet.CharacterId, out PortableChairPairRecord existingRecord))
            {
                return true;
            }

            if (ShouldClearPortableChairPartnerRecordOnRemoveForParity(existingRecord)
                && _portableChairPairRecordsByCharacterId.TryGetValue(existingRecord.PairCharacterId.Value, out PortableChairPairRecord partnerRecord))
            {
                _portableChairPairRecordsByCharacterId[existingRecord.PairCharacterId.Value] = partnerRecord with
                {
                    PairCharacterId = null,
                    Status = 0
                };
            }

            ClearPortableChairPairRecord(packet.CharacterId);
            message = $"Removed remote couple-chair record for {packet.CharacterId}.";
            return true;
        }

        public bool TryApplyPortableChairRecordEventFromSetActivePortableChairForParity(
            int characterId,
            int? chairItemId,
            out string message)
        {
            message = null;
            if (!TryResolvePortableChairRecordEventFromSetActivePortableChairForParity(
                    characterId,
                    chairItemId,
                    out RemoteUserPortableChairRecordAddPacket addPacket,
                    out RemoteUserPortableChairRecordRemovePacket removePacket))
            {
                message = $"Portable-chair record event requires a positive character ID; received {characterId}.";
                return false;
            }

            if (addPacket.ChairItemId > 0)
            {
                return TryApplyPortableChairRecordAdd(addPacket, out message);
            }

            return TryApplyPortableChairRecordRemove(removePacket, out message);
        }

        internal static bool TryResolvePortableChairRecordEventFromSetActivePortableChairForParity(
            int characterId,
            int? chairItemId,
            out RemoteUserPortableChairRecordAddPacket addPacket,
            out RemoteUserPortableChairRecordRemovePacket removePacket)
        {
            addPacket = default;
            removePacket = default;
            if (characterId <= 0)
            {
                return false;
            }

            int resolvedChairItemId = chairItemId.GetValueOrDefault();
            if (IsCoupleChairRecordItemIdForClientParity(resolvedChairItemId))
            {
                addPacket = new RemoteUserPortableChairRecordAddPacket(characterId, resolvedChairItemId);
            }
            else
            {
                removePacket = new RemoteUserPortableChairRecordRemovePacket(characterId);
            }

            return true;
        }

        public bool TrySetMount(int characterId, int? tamingMobItemId, out string message)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (!tamingMobItemId.HasValue || tamingMobItemId.Value <= 0)
            {
                actor.Build.Unequip(EquipSlot.TamingMob);
                SyncTemporaryStatPresentation(actor);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
                return true;
            }

            CharacterPart mountPart = _loader.LoadEquipment(tamingMobItemId.Value);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                message = $"Item {tamingMobItemId.Value} is not a taming mob mount.";
                return false;
            }

            actor.Build.Equip(mountPart);
            SyncTemporaryStatPresentation(actor);
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryApplyActiveEffectItem(RemoteUserActiveEffectItemPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            if (!packet.ItemId.HasValue || packet.ItemId.Value <= 0)
            {
                ClearPacketOwnedEmotionState(actor, currentTime);
                ClearRemoteActiveEffectMotionBlurState(actor, currentTime);
                ClearRemoteActiveEffectItemEffectState(actor, currentTime);
                message = $"Remote user {packet.CharacterId} active effect item cleared.";
                return true;
            }

            bool hasEmotionSelection = PacketOwnedAvatarEmotionResolver.TryResolveItemEmotion(
                packet.ItemId.Value,
                currentTime,
                out PacketOwnedAvatarEmotionSelection selection,
                out bool byItemOption,
                out string emotionError);
            if (hasEmotionSelection)
            {
                ApplyPacketOwnedEmotionState(actor, packet.ItemId.Value, selection, byItemOption, currentTime);
            }
            else
            {
                ClearPacketOwnedEmotionState(actor, currentTime);
            }

            bool hasMotionBlur = ActiveEffectItemMotionBlurResolver.TryResolve(
                packet.ItemId.Value,
                out ActiveEffectItemMotionBlurDefinition motionBlurDefinition,
                out string motionBlurError);
            if (hasMotionBlur)
            {
                ApplyRemoteActiveEffectMotionBlurState(actor, motionBlurDefinition, currentTime);
            }
            else
            {
                ClearRemoteActiveEffectMotionBlurState(actor, currentTime);
            }

            bool hasActiveEffectItemAnimation = TryApplyRemoteActiveEffectItemEffectState(
                actor,
                packet.ItemId.Value,
                currentTime,
                out bool activeEffectItemFollowOwner,
                out string activeEffectItemError);

            if (!hasEmotionSelection && !hasMotionBlur && !hasActiveEffectItemAnimation)
            {
                message = $"Remote user active effect item {packet.ItemId.Value} has no supported simulator presentation. Emotion: {emotionError} MotionBlur: {motionBlurError} ItemEffect: {activeEffectItemError}";
                return true;
            }

            string emotionSummary = hasEmotionSelection
                ? $"applied as {(byItemOption ? "item-option emotion" : "random-emotion item")} '{selection.EmotionName}' ({selection.EmotionId})"
                : "did not resolve packet-owned emotion";
            string motionBlurSummary = hasMotionBlur
                ? $"registered spectrum motion blur delay={motionBlurDefinition.DelayMs}, interval={motionBlurDefinition.IntervalMs}, alpha={motionBlurDefinition.Alpha}, follow={(motionBlurDefinition.Follow ? 1 : 0)}"
                : $"did not resolve spectrum motion blur ({motionBlurError})";
            string activeEffectItemSummary = hasActiveEffectItemAnimation
                ? $"registered active-effect owner animation follow={(activeEffectItemFollowOwner ? 1 : 0)}"
                : $"did not resolve active-effect owner animation ({activeEffectItemError})";
            message = $"Remote user {packet.CharacterId} active effect item {packet.ItemId.Value} {emotionSummary}; {motionBlurSummary}; {activeEffectItemSummary}.";
            return true;
        }

        private bool TryApplyTransientItemEffect(RemoteUserActor actor, int itemId, int currentTime, out string message)
        {
            message = null;
            if (actor == null)
            {
                message = "Remote user item-effect target is not active.";
                return false;
            }

            actor.LastEffectByItemId = itemId;
            if (_loader == null)
            {
                message = $"Remote user {actor.CharacterId} effect-by-item {itemId} preserved without a character loader.";
                return true;
            }

            ItemEffectAnimationSet effect = _loader.LoadItemEffectAnimationSet(itemId);
            if (effect == null)
            {
                message = $"Remote user {actor.CharacterId} effect-by-item {itemId} preserved without a loadable Effect/ItemEff.img animation.";
                return true;
            }

            actor.TransientItemEffects.RemoveAll(static state => state?.Effect == null);
            string ownerActionName = ResolveRemoteTemporaryStatAvatarEffectActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            bool hasRestoredAnimationElapsed = TryRestoreRemoteAuxiliaryLayerOwnerCounter(
                actor.CharacterId,
                RemoteEffectByItemActionOwnerName,
                itemId,
                ownerActionName,
                ownerFacingRight,
                out int restoredAnimationElapsedMs);

            for (int i = actor.TransientItemEffects.Count - 1; i >= 0; i--)
            {
                RemoteTransientItemEffectState existingState = actor.TransientItemEffects[i];
                if (existingState?.ItemId != itemId)
                {
                    continue;
                }

                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemoteEffectByItemActionOwnerName,
                    existingState.ItemId,
                    existingState.OwnerActionName,
                    existingState.OwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, existingState.AnimationStartTime),
                    currentTime);
                existingState.ReleaseLayerReference();
                actor.TransientItemEffects.RemoveAt(i);
            }

            actor.TransientItemEffects.Add(new RemoteTransientItemEffectState
            {
                ItemId = itemId,
                Effect = effect,
                SimulatedOwnerLayerHandleId = NextRemoteEffectByItemLayerHandleId(),
                AnimationStartTime = hasRestoredAnimationElapsed
                    ? unchecked(currentTime - restoredAnimationElapsedMs)
                    : currentTime,
                OwnerActionName = ownerActionName,
                OwnerFacingRight = ownerFacingRight
            });
            actor.TransientItemEffects[^1].CaptureRegisteredLayerReference();
            message = $"Remote user {actor.CharacterId} effect subtype {(byte)RemoteUserEffectSubtype.EffectByItem} registered Effect/ItemEff.img/{itemId} presentation.";
            return true;
        }

        public bool TryApplyEnterFieldAvatarPresentation(
            RemoteUserEnterFieldPacket packet,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            ApplyCarryItemEffectStateForParity(actor, packet.CarryItemEffect, currentTime);
            ApplyCompletedSetItemEffectStateForParity(actor, packet.CompletedSetItemId, currentTime);
            return TryApplyActiveEffectItem(
                new RemoteUserActiveEffectItemPacket(packet.CharacterId, packet.ActiveEffectItemId),
                currentTime,
                out message);
        }

        private void ApplyCarryItemEffectStateForParity(RemoteUserActor actor, int? carryItemEffectId, int currentTime)
        {
            if (actor == null)
            {
                return;
            }

            int? normalizedCarryItemEffectId = carryItemEffectId is > 0
                ? carryItemEffectId
                : null;
            if (actor.CarryItemEffectId == normalizedCarryItemEffectId)
            {
                return;
            }

            string ownerActionName = ResolveRemoteTemporaryStatAvatarEffectActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            if (actor.CarryItemEffectId is int previousCarryItemEffectId
                && actor.CarryItemEffectAppliedTime != int.MinValue)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemoteCarryItemEffectActionOwnerName,
                    previousCarryItemEffectId,
                    ownerActionName,
                    ownerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.CarryItemEffectAppliedTime),
                    currentTime);
            }

            ReleaseCarryItemEffectLayerReferenceForParity(actor);
            actor.CarryItemEffectId = normalizedCarryItemEffectId;
            if (!normalizedCarryItemEffectId.HasValue)
            {
                actor.CarryItemEffectAppliedTime = int.MinValue;
                return;
            }

            bool hasRestoredAnimationElapsed = TryRestoreRemoteAuxiliaryLayerOwnerCounter(
                actor.CharacterId,
                RemoteCarryItemEffectActionOwnerName,
                normalizedCarryItemEffectId.Value,
                ownerActionName,
                ownerFacingRight,
                out int restoredAnimationElapsedMs);
            actor.CarryItemEffectAppliedTime = hasRestoredAnimationElapsed
                ? unchecked(currentTime - restoredAnimationElapsedMs)
                : currentTime;
            actor.CarryItemEffectLayerHandleId = NextRemoteCarryItemEffectLayerHandleId();
            UpdateCarryItemEffectLayerAdmissionForParity(actor);
        }

        private void ApplyCompletedSetItemEffectStateForParity(RemoteUserActor actor, int completedSetItemId, int currentTime)
        {
            if (actor == null)
            {
                return;
            }

            int normalizedSetItemId = Math.Max(0, completedSetItemId);
            if (actor.CompletedSetItemId == normalizedSetItemId)
            {
                return;
            }

            string ownerActionName = ResolveRemoteTemporaryStatAvatarEffectActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            if (actor.CompletedSetItemId > 0
                && actor.CompletedSetItemEffectAppliedTime != int.MinValue)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemoteCompletedSetItemEffectActionOwnerName,
                    actor.CompletedSetItemId,
                    ownerActionName,
                    ownerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.CompletedSetItemEffectAppliedTime),
                    currentTime);
            }

            ReleaseCompletedSetItemEffectLayerReferenceForParity(actor);
            actor.CompletedSetItemId = normalizedSetItemId;
            if (normalizedSetItemId <= 0)
            {
                actor.CompletedSetItemEffectAppliedTime = int.MinValue;
                return;
            }

            bool hasRestoredAnimationElapsed = TryRestoreRemoteAuxiliaryLayerOwnerCounter(
                actor.CharacterId,
                RemoteCompletedSetItemEffectActionOwnerName,
                normalizedSetItemId,
                ownerActionName,
                ownerFacingRight,
                out int restoredAnimationElapsedMs);
            actor.CompletedSetItemEffectAppliedTime = hasRestoredAnimationElapsed
                ? unchecked(currentTime - restoredAnimationElapsedMs)
                : currentTime;
            actor.CompletedSetItemEffectLayerHandleId = NextRemoteCompletedSetItemEffectLayerHandleId();
            UpdateCompletedSetItemEffectLayerAdmissionForParity(actor);
        }

        public bool TrySetItemEffect(int characterId, int? itemId, int? pairCharacterId, int currentTime, out string message)
        {
            return TrySetItemEffect(
                characterId,
                RemoteRelationshipOverlayType.Generic,
                itemId,
                pairCharacterId,
                currentTime,
                out message);
        }

        public bool TrySetItemEffect(
            int characterId,
            RemoteRelationshipOverlayType relationshipType,
            int? itemId,
            int? pairCharacterId,
            int currentTime,
            out string message)
        {
            return TrySetItemEffect(
                characterId,
                relationshipType,
                itemId,
                pairCharacterId,
                currentTime,
                out message,
                itemSerial: null,
                pairItemSerial: null);
        }

        private bool TrySetItemEffect(
            int characterId,
            RemoteRelationshipOverlayType relationshipType,
            int? itemId,
            int? pairCharacterId,
            int currentTime,
            out string message,
            long? itemSerial,
            long? pairItemSerial)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (!itemId.HasValue || itemId.Value <= 0)
            {
                ClearRelationshipOverlayStateForParity(actor, relationshipType, currentTime);
                return true;
            }

            int resolvedItemId = itemId.Value;
            ItemEffectAnimationSet effect = _loader.LoadItemEffectAnimationSet(resolvedItemId);
            if (relationshipType == RemoteRelationshipOverlayType.NewYearCard
                && effect == null
                && resolvedItemId != NewYearCardDefaultItemId)
            {
                resolvedItemId = NewYearCardDefaultItemId;
                effect = _loader.LoadItemEffectAnimationSet(resolvedItemId);
            }

            if (effect == null && relationshipType != RemoteRelationshipOverlayType.NewYearCard)
            {
                message = $"Item effect {resolvedItemId} could not be loaded from Effect/ItemEff.img.";
                return false;
            }

            string ownerActionName = ResolveRemoteTemporaryStatAvatarEffectActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            string ownerName = ResolveRemoteRelationshipOverlayActionOwnerName(relationshipType);
            int ownerSkillId = ResolveRemoteRelationshipOverlayCounterSkillId(relationshipType, resolvedItemId);
            int restoredAnimationElapsedMs = 0;
            if (actor.RelationshipOverlays.TryGetValue(relationshipType, out RemoteRelationshipOverlayState existingOverlay)
                && existingOverlay != null)
            {
                if (existingOverlay.ItemId == resolvedItemId
                    && existingOverlay.PairCharacterId == pairCharacterId
                    && existingOverlay.ItemSerial == itemSerial
                    && existingOverlay.PairItemSerial == pairItemSerial
                    && string.Equals(existingOverlay.OwnerActionName, ownerActionName, StringComparison.OrdinalIgnoreCase)
                    && existingOverlay.OwnerFacingRight == ownerFacingRight)
                {
                    return true;
                }

                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    ownerName,
                    ResolveRemoteRelationshipOverlayCounterSkillId(relationshipType, existingOverlay.ItemId),
                    existingOverlay.OwnerActionName,
                    existingOverlay.OwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, existingOverlay.StartTime),
                    currentTime);
            }

            bool hasRestoredAnimationElapsed = TryRestoreRemoteAuxiliaryLayerOwnerCounter(
                actor.CharacterId,
                ownerName,
                ownerSkillId,
                ownerActionName,
                ownerFacingRight,
                out restoredAnimationElapsedMs);

            actor.RelationshipOverlays[relationshipType] = new RemoteRelationshipOverlayState
            {
                RelationshipType = relationshipType,
                ItemId = resolvedItemId,
                ItemSerial = itemSerial,
                PairItemSerial = pairItemSerial,
                PairCharacterId = pairCharacterId,
                Effect = NormalizeRelationshipOverlayEffect(
                    effect,
                    relationshipType,
                    shouldLoop: relationshipType != RemoteRelationshipOverlayType.Generic),
                StartTime = hasRestoredAnimationElapsed
                    ? unchecked(currentTime - restoredAnimationElapsedMs)
                    : currentTime,
                OwnerActionName = ownerActionName,
                OwnerFacingRight = ownerFacingRight
            };
            return true;
        }

        public bool TryApplyAvatarModified(
            RemoteUserAvatarModifiedPacket packet,
            int currentTime,
            out string message,
            bool applyRelationshipRecords = true)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            if (packet.AvatarLook != null)
            {
                if (_loader == null)
                {
                    message = "Character loader is not available.";
                    return false;
                }

                CharacterBuild updatedBuild = _loader.LoadFromAvatarLook(packet.AvatarLook, actor.Build?.Clone());
                if (updatedBuild == null)
                {
                    message = $"AvatarLook refresh for remote user {packet.CharacterId} could not be applied.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(actor.Name))
                {
                    updatedBuild.Name = actor.Name.Trim();
                }

                actor.BeginMeleeAfterImageFade(currentTime);
                actor.Build = updatedBuild;
                actor.BaseActionName = NormalizeActionName(actor.BaseActionName, actor.Build.ActivePortableChair != null);
                if (actor.Build.ActivePortableChair != null)
                {
                    ApplyPortableChairMount(actor, actor.Build.ActivePortableChair);
                }
                else
                {
                    ClearPortableChairMountState(actor);
                }

                SyncTemporaryStatPresentation(actor);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, currentTime, 10, 0);
            }

            if (packet.Speed.HasValue && actor.Build != null)
            {
                actor.Build.Speed = packet.Speed.Value;
            }

            if (packet.CarryItemEffect.HasValue)
            {
                ApplyCarryItemEffectStateForParity(actor, packet.CarryItemEffect, currentTime);
            }

            ApplyCompletedSetItemEffectStateForParity(actor, packet.CompletedSetItemId, currentTime);

            if (!applyRelationshipRecords)
            {
                message = $"Remote user {packet.CharacterId} avatar-modified state applied.";
                return true;
            }

            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.Couple,
                    packet.CoupleRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.Friendship,
                    packet.FriendshipRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.NewYearCard,
                    packet.NewYearCardRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            RemoteUserRelationshipRecord marriageRecord = packet.MarriageRecord.IsActive
                ? packet.MarriageRecord with
                {
                    PairCharacterId = ResolveMarriagePairCharacterId(packet.CharacterId, packet.MarriageRecord)
                }
                : packet.MarriageRecord;
            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.Marriage,
                    marriageRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            message = $"Remote user {packet.CharacterId} avatar-modified state applied.";
            return true;
        }

        public bool TryApplyRelationshipRecordAdd(
            RemoteUserRelationshipRecordPacket packet,
            int currentTime,
            out string message)
        {
            message = null;
            if (!packet.RelationshipRecord.IsActive)
            {
                message = $"{packet.RelationshipType} relationship record is not active.";
                return false;
            }

            int? ownerCharacterId = packet.RelationshipRecord.CharacterId;
            if (!ownerCharacterId.HasValue || ownerCharacterId.Value <= 0)
            {
                message = $"{packet.RelationshipType} relationship record did not specify a valid owner character ID.";
                return false;
            }

            if (!_actorsById.ContainsKey(ownerCharacterId.Value))
            {
                message = $"Rejected remote {packet.RelationshipType} relationship record for inactive owner {ownerCharacterId.Value}.";
                return false;
            }

            EnsureRelationshipRecordTablesInitialized();
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(packet.RelationshipType);
            Dictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable = GetRelationshipRecordDispatchOwnerTable(packet.RelationshipType);
            if (!TryNormalizeRelationshipRecordAdd(packet, recordTable, dispatchTable, out RemoteUserRelationshipRecord normalizedRecord, out string normalizeMessage))
            {
                message = normalizeMessage;
                return false;
            }

            ownerCharacterId = normalizedRecord.CharacterId;
            if (!ownerCharacterId.HasValue || ownerCharacterId.Value <= 0)
            {
                message = $"{packet.RelationshipType} relationship record did not resolve a valid owner character ID.";
                return false;
            }

            if (!_actorsById.ContainsKey(ownerCharacterId.Value))
            {
                message = $"Rejected remote {packet.RelationshipType} relationship record for inactive resolved owner {ownerCharacterId.Value}.";
                return false;
            }

            int? pairCharacterId = packet.RelationshipType == RemoteRelationshipOverlayType.Marriage
                ? ResolveMarriagePairCharacterId(ownerCharacterId.Value, normalizedRecord)
                : normalizedRecord.PairCharacterId;
            normalizedRecord = normalizedRecord with
            {
                PairCharacterId = pairCharacterId
            };
            RemoveRelationshipRecordDispatchKeysForOwner(packet.RelationshipType, ownerCharacterId.Value);
            recordTable[ownerCharacterId.Value] = normalizedRecord;
            RegisterRelationshipRecordDispatchKeys(packet.RelationshipType, packet.DispatchKey, normalizedRecord, ownerCharacterId.Value);
            RefreshRelationshipOverlays(packet.RelationshipType, currentTime);

            message = _actorsById.ContainsKey(ownerCharacterId.Value)
                ? $"Remote user {ownerCharacterId.Value} {packet.RelationshipType} relationship record applied."
                : $"Stored remote {packet.RelationshipType} relationship record for inactive owner {ownerCharacterId.Value}.";
            return true;
        }

        private static bool TryNormalizeRelationshipRecordAdd(
            RemoteUserRelationshipRecordPacket packet,
            IReadOnlyDictionary<int, RemoteUserRelationshipRecord> recordTable,
            IReadOnlyDictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable,
            out RemoteUserRelationshipRecord normalizedRecord,
            out string message)
        {
            normalizedRecord = packet.RelationshipRecord;
            message = null;
            if (packet.PayloadKind != RemoteRelationshipRecordAddPayloadKind.PairLookup
                || packet.RelationshipType is not (RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship))
            {
                return true;
            }

            int ownerCharacterId = normalizedRecord.CharacterId ?? 0;
            if (ownerCharacterId <= 0 || recordTable == null)
            {
                message = $"{packet.RelationshipType} pair-item lookup add requires an active owner record with item serial state.";
                return false;
            }

            long? pairLookupSerial = packet.PairLookupSerial;
            if (!pairLookupSerial.HasValue
                || !TryResolvePairLookupMatchedOwnerCharacterId(
                    ownerCharacterId,
                    pairLookupSerial.Value,
                    recordTable,
                    dispatchTable,
                    out int matchedOwnerCharacterId)
                || matchedOwnerCharacterId <= 0
                || matchedOwnerCharacterId == ownerCharacterId
                || !recordTable.TryGetValue(matchedOwnerCharacterId, out RemoteUserRelationshipRecord matchedRecord)
                || !matchedRecord.IsActive)
            {
                message = pairLookupSerial.HasValue
                    ? $"{packet.RelationshipType} pair-item lookup serial {pairLookupSerial.Value} did not match an active partner record."
                    : $"{packet.RelationshipType} pair-item lookup add requires a valid pair-item serial.";
                return false;
            }

            bool hasExistingOwnerRecord = recordTable.TryGetValue(ownerCharacterId, out RemoteUserRelationshipRecord existingRecord)
                && existingRecord.IsActive;
            long? ownerItemSerial = hasExistingOwnerRecord
                ? existingRecord.ItemSerial
                : null;
            if (!ownerItemSerial.HasValue && packet.DispatchKey.Kind == RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial)
            {
                ownerItemSerial = packet.DispatchKey.Serial;
            }

            long? matchedItemSerial = matchedRecord.ItemSerial;
            if (!ownerItemSerial.HasValue || !matchedItemSerial.HasValue)
            {
                message = $"{packet.RelationshipType} pair-item lookup add requires both matched users to have item serial state.";
                return false;
            }

            bool packetOwnerIsCanonicalOwner = IsPairLookupPacketOwnerCanonicalOwner(
                ownerCharacterId,
                matchedOwnerCharacterId,
                matchedRecord);
            int entryOwnerCharacterId = packetOwnerIsCanonicalOwner ? ownerCharacterId : matchedOwnerCharacterId;
            int entryPairCharacterId = packetOwnerIsCanonicalOwner ? matchedOwnerCharacterId : ownerCharacterId;
            long entryOwnerItemSerial = packetOwnerIsCanonicalOwner ? ownerItemSerial.Value : matchedItemSerial.Value;
            long entryPairItemSerial = packetOwnerIsCanonicalOwner ? matchedItemSerial.Value : ownerItemSerial.Value;

            normalizedRecord = normalizedRecord with
            {
                ItemId = normalizedRecord.ItemId > 0
                    ? normalizedRecord.ItemId
                    : hasExistingOwnerRecord
                        ? existingRecord.ItemId
                        : matchedRecord.ItemId,
                ItemSerial = entryOwnerItemSerial,
                PairItemSerial = entryPairItemSerial,
                CharacterId = entryOwnerCharacterId,
                PairCharacterId = entryPairCharacterId
            };
            return true;
        }

        private static bool IsPairLookupPacketOwnerCanonicalOwner(
            int packetOwnerCharacterId,
            int matchedOwnerCharacterId,
            RemoteUserRelationshipRecord matchedRecord)
        {
            int recoveredOwnerCharacterId = matchedRecord.CharacterId.GetValueOrDefault();
            int recoveredPairCharacterId = matchedRecord.PairCharacterId.GetValueOrDefault();
            if (recoveredOwnerCharacterId > 0 || recoveredPairCharacterId > 0)
            {
                if (recoveredOwnerCharacterId == packetOwnerCharacterId
                    || recoveredPairCharacterId == matchedOwnerCharacterId)
                {
                    return true;
                }

                if (recoveredOwnerCharacterId == matchedOwnerCharacterId
                    || recoveredPairCharacterId == packetOwnerCharacterId)
                {
                    return false;
                }
            }

            return packetOwnerCharacterId <= matchedOwnerCharacterId;
        }

        private static bool TryResolvePairLookupMatchedOwnerCharacterId(
            int ownerCharacterId,
            long pairLookupSerial,
            IReadOnlyDictionary<int, RemoteUserRelationshipRecord> recordTable,
            IReadOnlyDictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable,
            out int matchedOwnerCharacterId)
        {
            matchedOwnerCharacterId = 0;
            if (recordTable != null)
            {
                foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in recordTable)
                {
                    if (entry.Key == ownerCharacterId
                        || !entry.Value.IsActive
                        || entry.Value.PairItemSerial != pairLookupSerial)
                    {
                        continue;
                    }

                    matchedOwnerCharacterId = entry.Key;
                    return true;
                }
            }

            if (dispatchTable == null)
            {
                return false;
            }

            return dispatchTable.TryGetValue(
                new RemoteRelationshipRecordDispatchKey(
                    RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                    pairLookupSerial,
                    CharacterId: null),
                out matchedOwnerCharacterId);
        }

        public bool TryApplyRelationshipRecordRemove(
            RemoteUserRelationshipRecordRemovePacket packet,
            out string message)
        {
            message = null;
            EnsureRelationshipRecordTablesInitialized();
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(packet.RelationshipType);
            Dictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable = GetRelationshipRecordDispatchOwnerTable(packet.RelationshipType);
            List<int> removedOwnerCharacterIds = new();
            if (packet.DispatchKey.HasValue
                && dispatchTable.TryGetValue(packet.DispatchKey, out int dispatchOwnerCharacterId)
                && recordTable.ContainsKey(dispatchOwnerCharacterId))
            {
                RemoveRelationshipRecordOwner(packet.RelationshipType, dispatchOwnerCharacterId, recordTable);
                removedOwnerCharacterIds.Add(dispatchOwnerCharacterId);
            }

            foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in recordTable.ToArray())
            {
                int ownerCharacterId = entry.Key;
                if (removedOwnerCharacterIds.Contains(ownerCharacterId))
                {
                    continue;
                }

                RemoteUserRelationshipRecord record = entry.Value;
                int? candidatePairCharacterId = packet.RelationshipType == RemoteRelationshipOverlayType.Marriage
                    ? ResolveMarriagePairCharacterId(ownerCharacterId, record)
                    : record.PairCharacterId;
                if (!DoesRelationshipRecordRemovalMatch(
                        packet,
                        ownerCharacterId,
                        record.ItemSerial,
                        record.PairItemSerial,
                        candidatePairCharacterId))
                {
                    continue;
                }

                RemoveRelationshipRecordOwner(packet.RelationshipType, ownerCharacterId, recordTable);
                removedOwnerCharacterIds.Add(ownerCharacterId);
            }

            int removedCount = removedOwnerCharacterIds.Count;
            if (removedCount == 0)
            {
                string discriminator = packet.ItemSerial.HasValue
                    ? $"serial {packet.ItemSerial.Value}"
                    : packet.CharacterId.HasValue
                        ? $"character {packet.CharacterId.Value}"
                        : "record";
                message = $"No remote {packet.RelationshipType} relationship overlay matched {discriminator}.";
                return false;
            }

                message = removedCount == 1
                ? $"Removed 1 {packet.RelationshipType} relationship overlay."
                : $"Removed {removedCount} {packet.RelationshipType} relationship overlays.";

            RefreshRelationshipOverlays(packet.RelationshipType, Environment.TickCount);
            return true;
        }

        public bool TryClearRelationshipRecordOwner(
            RemoteRelationshipOverlayType relationshipType,
            int ownerCharacterId,
            int currentTime,
            out string message)
        {
            message = null;
            if (ownerCharacterId <= 0)
            {
                message = $"{relationshipType} relationship record clear requires a valid owner character ID.";
                return false;
            }

            EnsureRelationshipRecordTablesInitialized();
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(relationshipType);
            if (!recordTable.ContainsKey(ownerCharacterId))
            {
                message = $"No remote {relationshipType} relationship record is stored for owner {ownerCharacterId}.";
                return true;
            }

            RemoveRelationshipRecordOwner(relationshipType, ownerCharacterId, recordTable);
            RefreshRelationshipOverlays(relationshipType, currentTime);
            message = $"Cleared remote {relationshipType} relationship record for owner {ownerCharacterId}.";
            return true;
        }

        public bool TryApplyTemporaryStatSnapshot(
            int characterId,
            RemoteUserTemporaryStatSnapshot temporaryStats,
            ushort delay,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote user {characterId} does not exist.";
                return false;
            }

            temporaryStats = ReconcileChargeSkillIdFromPriorSnapshot(
                temporaryStats,
                actor.TemporaryStats);
            actor.TemporaryStats = temporaryStats;
            actor.TemporaryStatDelay = delay;
            actor.PendingTemporaryStatTimelineReseed = true;
            SyncTemporaryStatPresentation(actor);
            message = temporaryStats.HasPayload
                ? $"Remote user {characterId} temporary-stat snapshot applied."
                : $"Remote user {characterId} temporary-stat snapshot cleared.";
            return true;
        }

        public bool TryApplyTemporaryStatSet(
            RemoteUserTemporaryStatSetPacket packet,
            out string message)
        {
            return TryApplyTemporaryStatSnapshot(
                packet.CharacterId,
                packet.TemporaryStats,
                packet.Delay,
                out message);
        }

        public bool TryApplyTemporaryStatReset(
            RemoteUserTemporaryStatResetPacket packet,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            int[] currentMaskWords = actor.TemporaryStats.MaskWords ?? Array.Empty<int>();
            int[] resetMaskWords = packet.MaskWords ?? Array.Empty<int>();
            int maskWordCount = Math.Max(currentMaskWords.Length, resetMaskWords.Length);
            if (maskWordCount == 0)
            {
                actor.TemporaryStats = default;
                actor.TemporaryStatDelay = 0;
                SyncTemporaryStatPresentation(actor);
                message = $"Remote user {packet.CharacterId} temporary-stat mask cleared.";
                return true;
            }

            int[] remainingMaskWords = new int[maskWordCount];
            for (int i = 0; i < maskWordCount; i++)
            {
                int currentWord = i < currentMaskWords.Length ? currentMaskWords[i] : 0;
                int resetWord = i < resetMaskWords.Length ? resetMaskWords[i] : 0;
                int remainingWord = currentWord & ~resetWord;
                remainingMaskWords[i] = remainingWord;
            }

            actor.TemporaryStats = RemoteUserPacketCodec.ApplyResetMask(actor.TemporaryStats, remainingMaskWords);
            actor.TemporaryStatDelay = 0;
            SyncTemporaryStatPresentation(actor);
            message = actor.TemporaryStats.HasActiveMaskBits
                ? $"Remote user {packet.CharacterId} temporary-stat mask updated."
                : $"Remote user {packet.CharacterId} temporary-stat mask cleared.";
            return true;
        }

        public bool TrySetPreparedSkill(
            int characterId,
            int skillId,
            string skillName,
            int durationMs,
            string skinKey,
            bool isKeydownSkill,
            bool isHolding,
            int gaugeDurationMs,
            int maxHoldDurationMs,
            PreparedSkillHudTextVariant textVariant,
            bool showText,
            int currentTime,
            out string message,
            int prepareDurationMs = 0,
            bool autoEnterHold = false)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.PreparedSkill = new RemotePreparedSkillState
            {
                SkillId = skillId,
                SkillName = PreparedSkillHudRules.ResolveDisplayName(skillId, skillName),
                SkinKey = string.IsNullOrWhiteSpace(skinKey) ? "KeyDownBar" : skinKey.Trim(),
                DurationMs = Math.Max(0, durationMs),
                PrepareDurationMs = Math.Max(0, prepareDurationMs),
                GaugeDurationMs = ResolvePreparedSkillGaugeDurationForOverlay(
                    textVariant,
                    gaugeDurationMs,
                    durationMs),
                StartTime = currentTime,
                IsKeydownSkill = isKeydownSkill,
                IsHolding = isHolding,
                AutoEnterHold = autoEnterHold,
                MaxHoldDurationMs = Math.Max(0, maxHoldDurationMs),
                TextVariant = textVariant,
                ShowText = showText
            };

            if (RemotePreparedSkillUseEffectSkillIds.Contains(skillId))
            {
                IReadOnlyList<string> branchNames = ResolveRemotePreparedSkillUseStartBranchNames(skillId, isHolding);
                SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                    actor.CharacterId,
                    skillId,
                    null,
                    actor.FacingRight,
                    currentTime,
                    branchNames));
            }

            return true;
        }

        public bool TryApplyMovingShootAttackPrepare(
            RemoteUserMovingShootAttackPreparePacket packet,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote character {packet.CharacterId} does not exist.";
                return false;
            }

            actor.FacingRight = packet.FacingRight;
            actor.MovingShootPreparedSkillId = packet.SkillId;

            if (packet.SkillId != RemoteMovingShootNoPrepareAnimationSkillId)
            {
                actor.PreparedSkill = null;
            }

            if (!string.IsNullOrWhiteSpace(packet.ActionName))
            {
                actor.BeginMeleeAfterImageFade(currentTime);
                SetActorAction(
                    actor,
                    packet.ActionName,
                    actor.Build?.ActivePortableChair != null,
                    currentTime,
                    forceReplay: true,
                    rawActionCode: packet.ActionCode);
            }

            actor.MovementDrivenActionSelection = false;
            RegisterMeleeAfterImage(
                actor,
                packet.SkillId,
                actor.ActionName,
                currentTime,
                masteryPercent: 10,
                chargeElement: 0,
                rawActionCode: packet.ActionCode);

            if (packet.SkillId > 0 && packet.SkillId != RemoteMovingShootNoPrepareAnimationSkillId)
            {
                SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                    actor.CharacterId,
                    packet.SkillId,
                    packet.ActionSpeed,
                    actor.FacingRight,
                    currentTime));
            }

            message = $"Remote user {packet.CharacterId} moving-shoot prepare applied for skill {packet.SkillId}.";
            return true;
        }

        public bool TryApplyEmotion(RemoteUserEmotionPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            if (!PacketOwnedAvatarEmotionResolver.TryResolveEmotionName(packet.EmotionId, out string emotionName))
            {
                message = $"Remote user {packet.CharacterId} emotion id {packet.EmotionId} is not supported by the shared avatar-emotion owner.";
                return false;
            }

            ApplyRemoteEmotionState(
                actor,
                itemId: 0,
                packet.EmotionId,
                emotionName,
                packet.ByItemOption,
                currentTime,
                packet.DurationMs,
                loadEffectAnimation: false);
            message = $"Remote user {packet.CharacterId} emotion '{emotionName}' ({packet.EmotionId}) applied for {Math.Max(0, packet.DurationMs)} ms.";
            return true;
        }

        public bool TryApplyHit(RemoteUserHitPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            actor.LastHit = new RemoteHitState
            {
                AttackIndex = packet.AttackIndex,
                Damage = packet.Damage,
                MobTemplateId = packet.MobTemplateId,
                MobHitFacingLeft = packet.MobHitFacingLeft,
                HasMobHit = packet.HasMobHit,
                MobHitDamagePercent = packet.MobHitDamagePercent,
                PowerGuard = packet.PowerGuard,
                MobId = packet.MobId,
                MobHitAction = packet.MobHitAction,
                MobHitX = packet.MobHitX,
                MobHitY = packet.MobHitY,
                IncDecType = packet.IncDecType,
                HitFlags = packet.HitFlags,
                HpDelta = packet.HpDelta,
                SkillId = packet.SkillId,
                RawTrailingPayload = packet.RawTrailingPayload,
                HitString = packet.HitString,
                HitSecondaryInt32Value = packet.HitSecondaryInt32Value,
                HitTrailingInt32Values = packet.HitTrailingInt32Values,
                HitStringBranchPrefixBytes = packet.HitStringBranchPrefixBytes,
                PacketTime = currentTime
            };

            if (TryResolveRemoteHitStanceSpecialEffectSkillId(actor?.Build?.Job ?? 0, packet.HitFlags, out int stanceSkillId))
            {
                RegisterRemoteHitSpecialSkillUseEffect(actor, stanceSkillId, currentTime);
            }

            int damageReactiveSkillId = 0;
            int packetSpecialSkillId = 0;
            bool registeredDamageReactiveSkillEffect = false;
            bool registeredPacketSkillEffect = false;
            if (packet.SkillId is > 0)
            {
                registeredPacketSkillEffect = TryRegisterRemoteHitPacketSpecialSkillUseEffect(
                    packet.SkillId,
                    HasRemoteAnimationDisplayerSpecialBranch,
                    skillId => RegisterRemoteHitSpecialSkillUseEffect(actor, skillId, currentTime),
                    out packetSpecialSkillId);
            }
            else if (ShouldTryRegisterRemoteHitDamageReactiveSpecialEffect(packet.HpDelta, packet.SkillId))
            {
                int damageReactiveRollPercent = ResolveRemoteHitDamageReactiveRandomRollPercentForParity(packet);
                registeredDamageReactiveSkillEffect = TryRegisterRemoteHitDamageReactiveSpecialEffect(
                    actor?.Build?.Job ?? 0,
                    actor?.TemporaryStats.KnownState ?? default,
                    skillId => _skillLoader?.LoadSkill(skillId),
                    HasRemoteAnimationDisplayerSpecialBranch,
                    skillId => RegisterRemoteHitSpecialSkillUseEffect(actor, skillId, currentTime),
                    randomRollPercent: damageReactiveRollPercent,
                    out damageReactiveSkillId);
            }

            if (TryResolveRemoteHitMobAttackEffectPath(packet, out string mobAttackEffectPath))
            {
                bool usesAuthoredHitNode = TryResolveAuthoredMobAttackHitEffectPath(packet, out _);
                bool attachToOwner = TryResolveRemoteHitMobAttackAttachToOwnerForParity(packet, out bool resolvedAttachToOwner)
                                     && resolvedAttachToOwner;
                Vector2 mobAttackSoundOrigin = ResolveRemoteHitMobAttackSoundOriginForParity(
                    actor,
                    packet,
                    currentTime);
                Vector2 mobAttackEffectAnchor = ResolveRemoteHitMobAttackEffectAnchorForParity(
                    actor,
                    packet,
                    currentTime,
                    attachToOwner);
                bool mobAttackEffectFacingRight = ResolveRemoteHitMobAttackEffectFacingForParity(
                    actor,
                    packet,
                    attachToOwner);
                MobAttackHitEffectRegistered?.Invoke(new RemoteMobAttackHitPresentation(
                    packet.CharacterId,
                    packet.MobTemplateId.Value,
                    packet.AttackIndex,
                    mobAttackEffectPath,
                    mobAttackEffectAnchor,
                    mobAttackEffectFacingRight,
                    currentTime,
                    usesAuthoredHitNode,
                    attachToOwner));

                string mobAttackSoundDescriptor = BuildRemoteMobAttackHitSoundDescriptorForParity(
                    packet.MobTemplateId.GetValueOrDefault(),
                    packet.AttackIndex);
                if (!string.IsNullOrWhiteSpace(mobAttackSoundDescriptor))
                {
                    RegisterClientSoundPresentation(
                        packet.CharacterId,
                        mobAttackSoundDescriptor,
                        "Mob.img",
                        currentTime,
                        mobAttackSoundOrigin);
                }
            }

            if (TryBuildRemoteMobDamageInfoPresentation(packet, currentTime, out RemoteMobDamageInfoPresentation mobDamageInfo))
            {
                MobDamageInfoRegistered?.Invoke(mobDamageInfo);
            }

            if (packet.HpDelta > 0)
            {
                if (actor.PacketOwnedEmotion?.ByItemOption != true
                    && PacketOwnedAvatarEmotionResolver.TryResolveEmotionName(1, out string hitEmotionName))
                {
                    ApplyRemoteEmotionState(
                        actor,
                        itemId: 0,
                        emotionId: 1,
                        emotionName: hitEmotionName,
                        byItemOption: false,
                        currentTime,
                        durationMs: 1500,
                        loadEffectAnimation: false);
                }

                HitFeedbackRegistered?.Invoke(new RemoteHitFeedbackPresentation(
                    packet.CharacterId,
                    ResolveStandardWorldAnchor(actor, currentTime, verticalOffset: 24f),
                    -packet.HpDelta,
                    currentTime));
                message = registeredPacketSkillEffect
                    ? $"Remote user {packet.CharacterId} hit packet applied for {packet.HpDelta} damage and registered special skill effect {packetSpecialSkillId}."
                    : registeredDamageReactiveSkillEffect
                        ? $"Remote user {packet.CharacterId} hit packet applied for {packet.HpDelta} damage and registered damage-reactive special skill effect {damageReactiveSkillId}."
                        : $"Remote user {packet.CharacterId} hit packet applied for {packet.HpDelta} damage.";
                return true;
            }

            if (packet.HpDelta == 0)
            {
                HitFeedbackRegistered?.Invoke(new RemoteHitFeedbackPresentation(
                    packet.CharacterId,
                    ResolveStandardWorldAnchor(actor, currentTime, verticalOffset: 24f),
                    0,
                    currentTime,
                    packet.IncDecType));
            }

            if (registeredPacketSkillEffect)
            {
                if (ShouldApplyRemoteHitPacketSpecialEmotion(packet.SkillId)
                    && PacketOwnedAvatarEmotionResolver.TryResolveEmotionName(2, out string packetSkillEmotionName))
                {
                    ApplyRemoteEmotionState(
                        actor,
                        itemId: 0,
                        emotionId: 2,
                        emotionName: packetSkillEmotionName,
                        byItemOption: false,
                        currentTime,
                        durationMs: 1500,
                        loadEffectAnimation: false);
                }

                message = $"Remote user {packet.CharacterId} hit packet stored and registered special skill effect {packetSpecialSkillId}.";
            }
            else if (registeredDamageReactiveSkillEffect)
            {
                message = $"Remote user {packet.CharacterId} hit packet stored and registered damage-reactive special skill effect {damageReactiveSkillId}.";
            }
            else
            {
                message = $"Remote user {packet.CharacterId} hit packet stored.";
            }
            return true;
        }

        internal static bool TryBuildRemoteMobDamageInfoPresentation(
            RemoteUserHitPacket packet,
            int currentTime,
            out RemoteMobDamageInfoPresentation presentation)
        {
            presentation = default;
            if (!packet.HasMobHit
                || packet.MobId.GetValueOrDefault() <= 0
                || packet.MobTemplateId.GetValueOrDefault() <= 0
                || !packet.MobHitAction.HasValue
                || !packet.MobHitX.HasValue
                || !packet.MobHitY.HasValue)
            {
                return false;
            }

            int uncappedDamage = Math.Max(0, packet.Damage) * packet.MobHitDamagePercent / 100;
            int damage = uncappedDamage;
            bool cappedByMobMaxHp = false;
            if (packet.PowerGuard
                && TryResolveMobMaxHp(packet.MobTemplateId.Value, out int maxHp)
                && maxHp > 0)
            {
                int cap = maxHp / 2;
                if (damage > cap)
                {
                    damage = cap;
                    cappedByMobMaxHp = true;
                }
            }

            presentation = new RemoteMobDamageInfoPresentation(
                packet.CharacterId,
                packet.MobId.Value,
                packet.MobTemplateId.Value,
                packet.MobHitAction.Value,
                new Point(packet.MobHitX.Value, packet.MobHitY.Value),
                damage,
                packet.MobHitDamagePercent,
                packet.PowerGuard,
                cappedByMobMaxHp,
                currentTime);
            return true;
        }

        internal static bool TryRegisterRemoteHitPacketSpecialSkillUseEffect(
            int? packetSkillId,
            Func<int, bool> hasSpecialBranch,
            Action<int> registerSkillUseEffect,
            out int skillId)
        {
            skillId = packetSkillId.GetValueOrDefault();
            if (skillId <= 0 || hasSpecialBranch == null || registerSkillUseEffect == null)
            {
                skillId = 0;
                return false;
            }

            // Client `CUserRemote::OnHit` decodes an explicit skill id for the `-1` tail,
            // but `CUser::ShowSkillSpecialEffect` still exits if `SKILLENTRY::GetSpecialUOL` is empty.
            if (!hasSpecialBranch(skillId))
            {
                skillId = 0;
                return false;
            }

            registerSkillUseEffect(skillId);
            return true;
        }

        internal static bool TryResolveRemoteHitMobAttackEffectPath(RemoteUserHitPacket packet, out string effectPath)
        {
            effectPath = null;
            if (packet.AttackIndex < 0 || packet.MobTemplateId.GetValueOrDefault() <= 0)
            {
                return false;
            }

            string packetHitString = packet.HitString?.Trim();
            if (!string.IsNullOrWhiteSpace(packetHitString))
            {
                // Client `CUserRemote::OnHit` can carry a pre-resolved MobAttackInfo::sHit string.
                // Keep packet ownership for that authored path instead of forcing a local WZ lookup.
                effectPath = packetHitString;
                return true;
            }

            int attackNumber = packet.AttackIndex + 1;
            if (attackNumber <= 0)
            {
                return false;
            }

            if (TryResolveAuthoredMobAttackHitEffectPath(packet, out effectPath))
            {
                return true;
            }

            // CUserRemote::OnHit only plays the mob-hit visual when the attack row resolves
            // to a non-empty authored sHit path (or when packet-owned hit-string data exists).
            // Avoid synthesizing fallback UOL paths that the client never emits.
            return false;
        }

        private void ApplyMarriageProfileAuthorityFromRelationshipState(
            int characterId,
            bool hasAuthoritativeMarriageState)
        {
            if (characterId <= 0
                || !_actorsById.TryGetValue(characterId, out RemoteUserActor actor)
                || actor?.Build == null)
            {
                return;
            }

            actor.Build.HasAuthoritativeProfileMarriage = hasAuthoritativeMarriageState;
            actor.Build.IsProfileMarried = hasAuthoritativeMarriageState
                && HasActiveMarriageRelationshipRecord(characterId);
        }

        private static RemoteUserTemporaryStatSnapshot ReconcileChargeSkillIdFromPriorSnapshot(
            RemoteUserTemporaryStatSnapshot snapshot,
            RemoteUserTemporaryStatSnapshot priorSnapshot)
        {
            if (!snapshot.HasWeaponCharge || snapshot.KnownState.ChargeSkillId.HasValue)
            {
                return snapshot;
            }

            int preferredChargeSkillId = AfterImageChargeSkillResolver.ResolvePreferredChargeSkillIdFromWeaponChargeValue(
                preferredSkillId: 0,
                snapshot.KnownState.WeaponChargeValue);
            if (!AfterImageChargeSkillResolver.IsKnownChargeSkillId(preferredChargeSkillId))
            {
                preferredChargeSkillId = priorSnapshot.KnownState.ChargeSkillId.GetValueOrDefault();
            }

            if (!AfterImageChargeSkillResolver.IsKnownChargeSkillId(preferredChargeSkillId))
            {
                preferredChargeSkillId = AfterImageChargeSkillResolver.ResolvePreferredChargeSkillIdFromWeaponChargeValue(
                    preferredSkillId: 0,
                    priorSnapshot.KnownState.WeaponChargeValue);
            }

            if (!AfterImageChargeSkillResolver.IsKnownChargeSkillId(preferredChargeSkillId))
            {
                return snapshot;
            }

            int? resolvedChargeSkillId = ResolveChargeSkillIdFromTemporaryStats(snapshot, preferredChargeSkillId);
            if (!resolvedChargeSkillId.HasValue)
            {
                RemoteUserTemporaryStatKnownState noHintKnownState = snapshot.KnownState with
                {
                    ChargeSkillId = preferredChargeSkillId
                };

                return snapshot with
                {
                    KnownState = noHintKnownState
                };
            }

            RemoteUserTemporaryStatKnownState knownState = snapshot.KnownState with
            {
                ChargeSkillId = resolvedChargeSkillId.Value
            };

            return snapshot with
            {
                KnownState = knownState
            };
        }

        internal static RemoteUserTemporaryStatSnapshot ReconcileChargeSkillIdFromPriorSnapshotForTesting(
            RemoteUserTemporaryStatSnapshot snapshot,
            RemoteUserTemporaryStatSnapshot priorSnapshot)
        {
            return ReconcileChargeSkillIdFromPriorSnapshot(snapshot, priorSnapshot);
        }

        internal static Vector2 ResolveRemoteHitMobAttackSoundOriginForParity(
            RemoteUserActor actor,
            RemoteUserHitPacket packet,
            int currentTime)
        {
            return ResolveRemoteHitMobAttackEffectAnchorForParity(
                actor,
                packet,
                currentTime,
                attachToOwner: false);
        }

        internal static Vector2 ResolveRemoteHitMobAttackEffectAnchorForParity(
            RemoteUserActor actor,
            RemoteUserHitPacket packet,
            int currentTime,
            bool attachToOwner = false)
        {
            if (actor == null)
            {
                return Vector2.Zero;
            }

            if (attachToOwner)
            {
                // Client `CUserRemote::OnHit` uses the owner vec-control attachment path
                // when `MobAttackInfo::bHitAttach` is enabled.
                return actor.Position;
            }

            if (actor.Assembler == null)
            {
                return ResolveStandardWorldAnchor(actor, currentTime, verticalOffset: 24f);
            }

            AssembledFrame frame = actor.GetFrameAtTimeForRendering(currentTime);
            if (frame == null)
            {
                return ResolveStandardWorldAnchor(actor, currentTime, verticalOffset: 24f);
            }

            Rectangle sampledBodyRect = TryResolveRemoteHitBodyRectForParity(frame, out Rectangle bodyRect)
                ? bodyRect
                : frame.Bounds;
            int worldLeft = (int)MathF.Round(actor.Position.X + sampledBodyRect.Left);
            int worldTop = (int)MathF.Round(actor.Position.Y - frame.FeetOffset + sampledBodyRect.Top);
            int worldRightExclusive = Math.Max(worldLeft + 1, worldLeft + sampledBodyRect.Width);
            int worldBottomExclusive = Math.Max(worldTop + 1, worldTop + sampledBodyRect.Height);
            int randomSeed = ResolveRemoteHitMobAttackAnchorSeed(packet, currentTime, actor.CharacterId);
            Point randomPoint = ResolveRemoteHitBodyRectRandomPointForParity(
                worldLeft,
                worldTop,
                worldRightExclusive,
                worldBottomExclusive,
                randomSeed);
            return randomPoint.ToVector2();
        }

        internal static bool ResolveRemoteHitMobAttackEffectFacingForParity(
            RemoteUserActor actor,
            RemoteUserHitPacket packet,
            bool attachToOwner)
        {
            if (attachToOwner)
            {
                return actor?.FacingRight ?? true;
            }

            return !packet.MobHitFacingLeft;
        }

        internal static bool TryResolveRemoteHitMobAttackAttachToOwnerForParity(
            RemoteUserHitPacket packet,
            out bool attachToOwner)
        {
            attachToOwner = false;
            int mobTemplateId = packet.MobTemplateId.GetValueOrDefault();
            int attackNumber = packet.AttackIndex + 1;
            if (mobTemplateId <= 0 || attackNumber <= 0)
            {
                return false;
            }

            if (TryResolveMobAttackHitAttachToOwnerForTemplate(mobTemplateId, attackNumber, out attachToOwner))
            {
                return true;
            }

            if (!TryResolveLinkedMobTemplateId(mobTemplateId, out int linkedMobTemplateId)
                || linkedMobTemplateId == mobTemplateId)
            {
                return false;
            }

            return TryResolveMobAttackHitAttachToOwnerForTemplate(linkedMobTemplateId, attackNumber, out attachToOwner);
        }

        private static bool TryResolveMobAttackHitAttachToOwnerForTemplate(
            int mobTemplateId,
            int attackNumber,
            out bool attachToOwner)
        {
            attachToOwner = false;
            if (mobTemplateId <= 0 || attackNumber <= 0)
            {
                return false;
            }

            WzImage mobImage = TryFindMobImage(mobTemplateId);
            if (mobImage == null)
            {
                return false;
            }

            WzImageProperty hitNode = WzInfoTools.GetRealProperty(mobImage[$"attack{attackNumber}"]?["info"]?["hit"]);
            if (hitNode == null)
            {
                return false;
            }

            attachToOwner = ResolveMobAttackHitAttachToOwnerFromHitNodeForParity(hitNode);
            return true;
        }

        internal static bool ResolveMobAttackHitAttachToOwnerFromHitNodeForParity(WzImageProperty hitNode)
        {
            if (hitNode == null)
            {
                return false;
            }

            WzImageProperty resolvedHitNode = WzInfoTools.GetRealProperty(hitNode);
            int? explicitAttach = GetWzIntValue(
                WzInfoTools.GetRealProperty(resolvedHitNode?["attach"])
                ?? WzInfoTools.GetRealProperty(resolvedHitNode?["bHitAttach"])
                ?? WzInfoTools.GetRealProperty(resolvedHitNode?["hitAttach"]));
            if (explicitAttach.HasValue)
            {
                return explicitAttach.Value > 0;
            }

            int? facingAttach = GetWzIntValue(
                WzInfoTools.GetRealProperty(resolvedHitNode?["attachfacing"])
                ?? WzInfoTools.GetRealProperty(resolvedHitNode?["bFacingAttach"])
                ?? WzInfoTools.GetRealProperty(resolvedHitNode?["bFacingAttatch"])
                ?? WzInfoTools.GetRealProperty(resolvedHitNode?["facingAttach"]));
            return facingAttach.GetValueOrDefault() > 0;
        }

        internal static bool TryResolveRemoteHitBodyRectForParity(AssembledFrame frame, out Rectangle bodyRect)
        {
            bodyRect = Rectangle.Empty;
            if (frame?.Parts == null || frame.Parts.Count == 0)
            {
                return false;
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            bool foundPart = false;
            for (int i = 0; i < frame.Parts.Count; i++)
            {
                AssembledPart part = frame.Parts[i];
                if (!ShouldIncludeRemoteHitBodyRectPartForParity(part))
                {
                    continue;
                }

                int width = Math.Max(0, part.Texture.Width);
                int height = Math.Max(0, part.Texture.Height);
                if (width <= 0 || height <= 0)
                {
                    continue;
                }

                int left = part.OffsetX;
                int top = part.OffsetY;
                int right = left + width;
                int bottom = top + height;
                minX = Math.Min(minX, left);
                minY = Math.Min(minY, top);
                maxX = Math.Max(maxX, right);
                maxY = Math.Max(maxY, bottom);
                foundPart = true;
            }

            if (!foundPart || maxX <= minX || maxY <= minY)
            {
                return false;
            }

            bodyRect = new Rectangle(minX, minY, maxX - minX, maxY - minY);
            return true;
        }

        private static bool ShouldIncludeRemoteHitBodyRectPartForParity(AssembledPart part)
        {
            if (part?.Texture == null || part.SourcePortableChairLayer != null)
            {
                return false;
            }

            return part.PartType switch
            {
                CharacterPartType.PortableChair => false,
                CharacterPartType.Weapon => false,
                CharacterPartType.WeaponOverGlove => false,
                CharacterPartType.WeaponOverHand => false,
                CharacterPartType.WeaponOverBody => false,
                CharacterPartType.WeaponBelowArm => false,
                CharacterPartType.Shield => false,
                CharacterPartType.Cape => false,
                _ => true
            };
        }

        internal static Point ResolveRemoteHitBodyRectRandomPointForParity(
            int leftInclusive,
            int topInclusive,
            int rightExclusive,
            int bottomExclusive,
            int randomSeed)
        {
            int width = Math.Max(1, rightExclusive - leftInclusive);
            int height = Math.Max(1, bottomExclusive - topInclusive);
            int randomX = leftInclusive + ResolveRemoteHitRandomOffsetForParity(randomSeed ^ unchecked((int)0x9E3779B9u), width);
            int randomY = topInclusive + ResolveRemoteHitRandomOffsetForParity(randomSeed ^ unchecked((int)0x85EBCA77u), height);
            return new Point(randomX, randomY);
        }

        private static int ResolveRemoteHitRandomOffsetForParity(int seed, int span)
        {
            if (span <= 1)
            {
                return 0;
            }

            uint state = unchecked((uint)seed);
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % (uint)span);
        }

        private static int ResolveRemoteHitMobAttackAnchorSeed(RemoteUserHitPacket packet, int currentTime, int characterId)
        {
            unchecked
            {
                return (packet.MobTemplateId.GetValueOrDefault() * 397)
                    ^ (packet.AttackIndex * 131)
                    ^ (packet.Damage * 17)
                    ^ (characterId * 31)
                    ^ currentTime;
            }
        }

        public static string BuildRemoteMobAttackHitSoundDescriptorForParity(int mobTemplateId, sbyte attackIndex)
        {
            if (mobTemplateId <= 0 || attackIndex < 0)
            {
                return null;
            }

            int attackNumber = attackIndex + 1;
            return attackNumber <= 0
                ? null
                : string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Mob/{0:D7}/Attack{1}",
                    mobTemplateId,
                    attackNumber);
        }

        public static bool ShouldApplyRemoteHitPacketSpecialEmotion(int? packetSkillId)
        {
            int skillId = packetSkillId.GetValueOrDefault();
            return skillId == RemoteHitAssassinateEmotionSkillId
                || skillId == RemoteHitAssassinateMirrorEmotionSkillId;
        }

        private static bool TryResolveAuthoredMobAttackHitEffectPath(RemoteUserHitPacket packet, out string effectPath)
        {
            effectPath = null;
            int mobTemplateId = packet.MobTemplateId.GetValueOrDefault();
            int attackNumber = packet.AttackIndex + 1;
            if (mobTemplateId <= 0 || attackNumber <= 0)
            {
                return false;
            }

            if (TryResolveMobAttackHitEffectPath(mobTemplateId, attackNumber, out effectPath))
            {
                return true;
            }

            if (!TryResolveLinkedMobTemplateId(mobTemplateId, out int linkedMobTemplateId)
                || linkedMobTemplateId == mobTemplateId)
            {
                return false;
            }

            return TryResolveMobAttackHitEffectPath(linkedMobTemplateId, attackNumber, out effectPath);
        }

        private static bool TryResolveMobAttackHitEffectPath(int mobTemplateId, int attackNumber, out string effectPath)
        {
            effectPath = null;
            WzImage mobImage = TryFindMobImage(mobTemplateId);
            if (mobImage == null)
            {
                return false;
            }

            WzImageProperty hitNode = WzInfoTools.GetRealProperty(mobImage[$"attack{attackNumber}"]?["info"]?["hit"]);
            if (hitNode == null)
            {
                return false;
            }

            return TryResolveMobAttackHitEffectPathFromNodeForParity(
                hitNode,
                mobTemplateId,
                attackNumber,
                out effectPath);
        }

        internal static bool TryResolveMobAttackHitEffectPathFromNodeForParity(
            WzImageProperty hitNode,
            int mobTemplateId,
            int attackNumber,
            out string effectPath)
        {
            effectPath = null;
            if (hitNode == null || mobTemplateId <= 0 || attackNumber <= 0)
            {
                return false;
            }

            WzImageProperty resolvedHitNode = WzInfoTools.GetRealProperty(hitNode);
            if (resolvedHitNode is WzStringProperty stringHitNode)
            {
                string authoredPath = stringHitNode.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(authoredPath))
                {
                    // CUserRemote::OnHit uses the authored MobAttackInfo::sHit string when present.
                    effectPath = authoredPath;
                    return true;
                }
            }

            effectPath = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Mob/{0:D7}.img/attack{1}/info/hit",
                mobTemplateId,
                attackNumber);
            return true;
        }

        private static bool TryResolveLinkedMobTemplateId(int mobTemplateId, out int linkedMobTemplateId)
        {
            linkedMobTemplateId = 0;
            WzImage mobImage = TryFindMobImage(mobTemplateId);
            if (mobImage?["info"]?["link"] is not WzStringProperty linkProperty
                || string.IsNullOrWhiteSpace(linkProperty.Value))
            {
                return false;
            }

            return int.TryParse(
                linkProperty.Value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out linkedMobTemplateId)
                && linkedMobTemplateId > 0;
        }

        private static bool TryResolveMobMaxHp(int mobTemplateId, out int maxHp)
        {
            maxHp = 0;
            WzImage mobImage = TryFindMobImage(mobTemplateId);
            if (mobImage == null)
            {
                return false;
            }

            int? value = GetWzIntValue(WzInfoTools.GetRealProperty(mobImage["info"]?["maxHP"]));
            if (value.GetValueOrDefault() <= 0)
            {
                return false;
            }

            maxHp = value.Value;
            return true;
        }

        private static WzImage TryFindMobImage(int mobTemplateId)
        {
            if (mobTemplateId <= 0)
            {
                return null;
            }

            WzImage mobImage = global::HaCreator.Program.FindImage(
                "Mob",
                mobTemplateId.ToString("D7", System.Globalization.CultureInfo.InvariantCulture) + ".img");
            if (mobImage != null && !mobImage.Parsed)
            {
                mobImage.ParseImage();
            }

            return mobImage;
        }

        internal static bool TryResolveRemoteHitStanceSpecialEffectSkillId(int jobId, byte hitFlags, out int skillId)
        {
            skillId = 0;
            if ((hitFlags & 1) == 0)
            {
                return false;
            }

            skillId = (hitFlags & 2) != 0
                ? 33110000
                : ResolveRemoteHitStanceSkillId(jobId);
            return skillId > 0;
        }

        internal static int ResolveRemoteHitStanceSkillId(int jobId)
        {
            // Client `get_stance_skill_id` is used by `CUserRemote::OnHit` before `ShowSkillSpecialEffect`.
            return jobId switch
            {
                112 => 1121002,
                122 => 1221002,
                132 => 1321002,
                2112 => 21121003,
                3212 => 32121005,
                > 3309 and <= 3312 => 33101006,
                _ => 0
            };
        }

        internal static bool TryResolveRemoteHitDamageReactiveSpecialEffectSkillId(
            int jobId,
            RemoteUserTemporaryStatKnownState knownState,
            Func<int, SkillData> loadSkill,
            Func<int, bool> hasSpecialBranch,
            int? randomRollPercent,
            out int skillId)
        {
            skillId = 0;
            if (loadSkill == null)
            {
                return false;
            }

            foreach (int candidateSkillId in EnumerateRemoteHitDamageReactiveSkillCandidateIds(jobId, knownState))
            {
                SkillData skill = loadSkill(candidateSkillId);
                if (!SkillManager.IsDamageReactiveSpecialSkillUseEffectCandidate(
                        skill,
                        HasRemoteDamageReactiveSpecialBranch(skill, hasSpecialBranch)))
                {
                    continue;
                }

                int prop = ResolveRemoteHitDamageReactiveProp(skill);
                if (!SkillManager.ShouldTriggerDamageReactiveSpecialEffect(prop, randomRollPercent))
                {
                    continue;
                }

                skillId = candidateSkillId;
                return true;
            }

            return false;
        }

        internal static bool TryResolveRemoteHitDamageReactiveSpecialEffectSkillId(
            int jobId,
            RemoteUserTemporaryStatKnownState knownState,
            Func<int, SkillData> loadSkill,
            int? randomRollPercent,
            out int skillId)
        {
            return TryResolveRemoteHitDamageReactiveSpecialEffectSkillId(
                jobId,
                knownState,
                loadSkill,
                hasSpecialBranch: null,
                randomRollPercent,
                out skillId);
        }

        internal static bool TryRegisterRemoteHitDamageReactiveSpecialEffect(
            int jobId,
            RemoteUserTemporaryStatKnownState knownState,
            Func<int, SkillData> loadSkill,
            Func<int, bool> hasSpecialBranch,
            Action<int> registerSkillUseEffect,
            int? randomRollPercent,
            out int skillId)
        {
            skillId = 0;
            if (registerSkillUseEffect == null
                || !TryResolveRemoteHitDamageReactiveSpecialEffectSkillId(
                    jobId,
                    knownState,
                    loadSkill,
                    hasSpecialBranch,
                    randomRollPercent,
                    out skillId))
            {
                return false;
            }

            registerSkillUseEffect(skillId);
            return true;
        }

        internal static bool ShouldTryRegisterRemoteHitDamageReactiveSpecialEffect(
            int hpDelta,
            int? packetSkillId)
        {
            // Client `CUserRemote::OnHit` reserves `nHPDamage == -1` for the
            // dedicated explicit packet skill-id tail handled before this path.
            // Ordinary HP-damage packets do not scan active temporary stats for WZ
            // `info/condition = damaged` rows; those chance owners stay local to
            // `CUserLocal::SetDamaged`.
            return false;
        }

        internal static int ResolveRemoteHitDamageReactiveRandomRollPercentForParity(RemoteUserHitPacket packet)
        {
            unchecked
            {
                // Keep damaged-condition trigger ownership bound to the decoded `OnHit` packet
                // surface rather than global simulator RNG so replayed packets stay stable.
                int seed = (packet.CharacterId * 397)
                           ^ (packet.AttackIndex * 131)
                           ^ (packet.Damage * 17)
                           ^ (packet.HpDelta * 31)
                           ^ (packet.MobTemplateId.GetValueOrDefault() * 53)
                           ^ (packet.MobId.GetValueOrDefault() * 71)
                           ^ (packet.HitFlags * 11)
                           ^ (packet.IncDecType * 19)
                           ^ (packet.SkillId.GetValueOrDefault() * 29);
                return ResolveRemoteHitRandomOffsetForParity(seed, 100);
            }
        }

        internal static IReadOnlyList<int> EnumerateRemoteHitDamageReactiveSkillCandidateIds(
            int jobId,
            RemoteUserTemporaryStatKnownState knownState)
        {
            var candidateSkillIds = new List<int>(8);
            var seen = new HashSet<int>();

            void TryAddCandidate(int candidateSkillId)
            {
                if (candidateSkillId > 0 && seen.Add(candidateSkillId))
                {
                    candidateSkillIds.Add(candidateSkillId);
                }
            }

            if (jobId is >= 1220 and <= 1222
                && knownState.ExtendedState.HasDefenseState)
            {
                // WZ `Skill/122.img/skill/1220006` is Blocking: `info/type = 51`,
                // `info/condition = damaged`, `info/mes = stun`, and an authored `special` branch.
                // Remote temp-stat decode exposes that client defense-state surface separately from Blessing Armor.
                TryAddCandidate(PaladinDamageReactiveSpecialSkillId);
            }

            if (knownState.HasBlessingArmor)
            {
                if (jobId is >= 1220 and <= 1222)
                {
                    TryAddCandidate(PaladinDamageReactiveSpecialSkillId);
                }

                TryAddCandidate(knownState.ResolveBlessingArmorSkillId(jobId) ?? 0);
            }

            TryAddCandidate(knownState.MagicShieldSkillId ?? 0);
            TryAddCandidate(knownState.FinalCutSkillId ?? 0);
            TryAddCandidate(knownState.ChargeSkillId ?? 0);
            TryAddCandidate(knownState.RepeatEffectSkillId ?? 0);
            TryAddCandidate(knownState.ActiveAuraSkillId ?? 0);

            return candidateSkillIds;
        }

        private static bool HasRemoteDamageReactiveSpecialAnimation(SkillData skill)
        {
            return skill?.AvatarOverlayEffect?.Frames?.Count > 0
                   || skill?.AvatarUnderFaceEffect?.Frames?.Count > 0;
        }

        private static bool HasRemoteDamageReactiveSpecialBranch(SkillData skill, Func<int, bool> hasSpecialBranch)
        {
            if (skill?.SkillId <= 0)
            {
                return false;
            }

            return hasSpecialBranch?.Invoke(skill.SkillId) == true
                   || HasRemoteDamageReactiveSpecialAnimation(skill);
        }

        private bool HasRemoteAnimationDisplayerSpecialBranch(int skillId)
        {
            if (skillId <= 0)
            {
                return false;
            }

            if (_remoteDamageReactiveSpecialBranchCache.TryGetValue(skillId, out bool cached))
            {
                return cached;
            }

            WzImage skillImage = Program.FindImage("Skill", $"{skillId / 10000}.img");
            skillImage?.ParseImage();
            bool hasSpecial = skillImage?["skill"]?[skillId.ToString("D7", System.Globalization.CultureInfo.InvariantCulture)]?["special"] != null;
            _remoteDamageReactiveSpecialBranchCache[skillId] = hasSpecial;
            return hasSpecial;
        }

        private static int ResolveRemoteHitDamageReactiveProp(SkillData skill)
        {
            if (skill?.Levels == null || skill.Levels.Count == 0)
            {
                return 0;
            }

            if (skill.Levels.TryGetValue(1, out SkillLevelData levelOneData))
            {
                return levelOneData?.Prop ?? 0;
            }

            foreach (SkillLevelData levelData in skill.Levels
                         .OrderBy(static pair => pair.Key)
                         .Select(static pair => pair.Value))
            {
                if (levelData != null)
                {
                    return levelData.Prop;
                }
            }

            return 0;
        }

        private void RegisterRemoteHitSpecialSkillUseEffect(RemoteUserActor actor, int skillId, int currentTime)
        {
            if (!TryCreateRemoteHitSpecialSkillUsePresentation(
                    actor?.CharacterId ?? 0,
                    actor?.FacingRight ?? true,
                    skillId,
                    currentTime,
                    out RemoteSkillUsePresentation presentation))
            {
                return;
            }

            SkillUseRegistered?.Invoke(presentation);
        }

        internal static bool TryCreateRemoteHitSpecialSkillUsePresentation(
            int characterId,
            bool facingRight,
            int skillId,
            int currentTime,
            out RemoteSkillUsePresentation presentation)
        {
            presentation = default;
            if (characterId <= 0 || skillId <= 0)
            {
                return false;
            }

            presentation = new RemoteSkillUsePresentation(
                characterId,
                skillId,
                null,
                facingRight,
                currentTime,
                new[] { "special" },
                WorldOrigin: null,
                FollowOwnerPosition: true,
                FollowOwnerFacing: false,
                DelayRateOverride: 1000);
            return true;
        }

        public bool TryApplyEffect(RemoteUserEffectPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            actor.LastEffect = packet;
            switch (packet.KnownSubtype)
            {
                case RemoteUserEffectSubtype.GenericUserState:
                    GenericUserStateRegistered?.Invoke(new RemoteGenericUserStatePresentation(
                        packet.CharacterId,
                        currentTime));
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered generic user-state presentation.";
                    return true;

                case RemoteUserEffectSubtype.SkillUse:
                    int skillId = packet.SkillId.GetValueOrDefault();
                    if (skillId <= 0)
                    {
                        message = $"Remote user {packet.CharacterId} skill-use effect skill ID {skillId} is invalid.";
                        return false;
                    }

                    SkillData skill = _skillLoader?.LoadSkill(skillId);
                    string appointedActionName = ResolveRemoteSkillUseAppointedActionNameForParity(
                        skill,
                        packet.CharacterLevel,
                        packet.SkillLevel,
                        currentTime);
                    if (!string.IsNullOrWhiteSpace(appointedActionName))
                    {
                        actor.BeginMeleeAfterImageFade(currentTime);
                        SetActorAction(
                            actor,
                            appointedActionName,
                            actor.Build?.ActivePortableChair != null,
                            currentTime,
                            forceReplay: true,
                            rawActionCode: CharacterPart.TryGetClientRawActionCode(appointedActionName, out int rawActionCode)
                                ? rawActionCode
                                : null);
                        RegisterMeleeAfterImage(actor, skillId, actor.ActionName, currentTime, 10, 0);
                    }

                    SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                        packet.CharacterId,
                        skillId,
                        null,
                        actor.FacingRight,
                        currentTime));
                    bool dragonFollowUpApplied = TryApplyRemoteDragonOfficialSkillUseFollowUp(
                        actor,
                        skillId,
                        appointedActionName,
                        currentTime,
                        out string dragonActionName);
                    message = string.IsNullOrWhiteSpace(appointedActionName)
                        ? $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered skill-use presentation for skill {skillId}."
                        : $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered skill-use presentation for skill {skillId} and appointed action {appointedActionName}.";
                    if (dragonFollowUpApplied)
                    {
                        message = $"{message} Dragon attack action {dragonActionName} was also registered.";
                    }

                    return true;

                case RemoteUserEffectSubtype.ItemMake:
                    int itemMakeResultCode = packet.Int32Value.GetValueOrDefault();
                    ItemMakeRegistered?.Invoke(new RemoteItemMakePresentation(
                        packet.CharacterId,
                        itemMakeResultCode,
                        itemMakeResultCode == 0,
                        currentTime));
                    RegisterClientSoundPresentation(
                        packet.CharacterId,
                        ResolveRemoteSoundDescriptor(
                            itemMakeResultCode == 0
                                ? ItemMakeSuccessSoundStringPoolId
                                : ItemMakeFailureSoundStringPoolId,
                            itemMakeResultCode == 0
                                ? ItemMakeSuccessSoundFallbackPath
                                : ItemMakeFailureSoundFallbackPath),
                        defaultImageName: null,
                        currentTime);
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered item-make {(itemMakeResultCode == 0 ? "success" : "failure")} presentation.";
                    return true;

                case RemoteUserEffectSubtype.MakerSkill:
                    string makerSkillEffectPath = MapleStoryStringPool.GetOrFallback(
                        MakerSkillEffectStringPoolId,
                        MakerSkillEffectFallbackPath);
                    StringEffectRegistered?.Invoke(new RemoteStringEffectPresentation(
                        packet.CharacterId,
                        packet.EffectType,
                        makerSkillEffectPath,
                        currentTime,
                        UseOwnerFacing: false,
                        AttachToOwner: true,
                        SecondaryInt32Value: packet.SecondaryInt32Value,
                        TrailingInt32Values: packet.TrailingInt32Values,
                        StringBranchPrefixBytes: packet.StringBranchPrefixBytes));
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered maker-skill fixed effect {makerSkillEffectPath}.";
                    return true;

                case RemoteUserEffectSubtype.NoOp:
                    // CUser::OnEffect case 21 is an explicit no-op branch.
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} consumed as no-op.";
                    return true;

                case RemoteUserEffectSubtype.EffectByItem:
                    int itemEffectItemId = packet.Int32Value.GetValueOrDefault();
                    if (itemEffectItemId <= 0)
                    {
                        message = $"Remote user {packet.CharacterId} item-effect packet item ID {itemEffectItemId} is invalid.";
                        return false;
                    }

                    bool transientItemEffectApplied = TryApplyTransientItemEffect(actor, itemEffectItemId, currentTime, out message);
                    if (transientItemEffectApplied
                        && TryResolveRemoteConsumeEffectSoundDescriptor(itemEffectItemId, out string effectByItemSoundDescriptor))
                    {
                        RegisterClientSoundPresentation(
                            packet.CharacterId,
                            effectByItemSoundDescriptor,
                            defaultImageName: null,
                            currentTime);
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            message = $"{message} Item-use sound {effectByItemSoundDescriptor} was also registered.";
                        }
                    }

                    return transientItemEffectApplied;

                case RemoteUserEffectSubtype.ReservedEffect:
                case RemoteUserEffectSubtype.CarnivalReservedEffect:
                case RemoteUserEffectSubtype.StringEffect:
                case RemoteUserEffectSubtype.ItemSoundStringEffect:
                    if (packet.KnownSubtype == RemoteUserEffectSubtype.ItemSoundStringEffect
                        && TryResolveRemoteConsumeEffectSoundDescriptor(
                            packet.Int32Value.GetValueOrDefault(),
                            out string itemSoundEffectDescriptor))
                    {
                        RegisterClientSoundPresentation(
                            packet.CharacterId,
                            itemSoundEffectDescriptor,
                            defaultImageName: null,
                            currentTime);
                    }

                    if (packet.KnownSubtype == RemoteUserEffectSubtype.CarnivalReservedEffect
                        && !IsAriantArenaRemoteActor(actor))
                    {
                        message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} ignored outside Ariant field ownership.";
                        return true;
                    }

                    if (string.IsNullOrWhiteSpace(packet.StringValue))
                    {
                        message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} consumed without string presentation because the packet path is empty.";
                        return true;
                    }

                    StringEffectRegistered?.Invoke(new RemoteStringEffectPresentation(
                        packet.CharacterId,
                        packet.EffectType,
                        packet.StringValue,
                        currentTime,
                        packet.KnownSubtype == RemoteUserEffectSubtype.CarnivalReservedEffect,
                        ShouldAttachRemotePacketOwnedStringEffectForParity(packet.KnownSubtype),
                        packet.SecondaryInt32Value,
                        packet.TrailingInt32Values,
                        packet.StringBranchPrefixBytes,
                        ResolveRemotePacketOwnedStringEffectWorldOriginForParity(packet, actor.Position)));
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered packet-owned string effect {packet.StringValue}.";
                    return true;

                case RemoteUserEffectSubtype.FieldSound:
                    if (string.IsNullOrWhiteSpace(packet.StringValue))
                    {
                        message = $"Remote user {packet.CharacterId} field-sound descriptor is empty.";
                        return false;
                    }

                    FieldSoundRegistered?.Invoke(new RemoteFieldSoundPresentation(
                        packet.CharacterId,
                        packet.StringValue,
                        currentTime));
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered field sound {packet.StringValue}.";
                    return true;

                case RemoteUserEffectSubtype.MakerResultMessage:
                    int makerResultCode = packet.Int32Value.GetValueOrDefault();
                    string makerResultMessage = FormatRemoteEffectChatLogMessage(
                        MakerResultMessageStringPoolId,
                        MakerResultMessageFallbackFormat,
                        makerResultCode);
                    ChatLogMessageRegistered?.Invoke(new RemoteChatLogMessagePresentation(
                        packet.CharacterId,
                        packet.EffectType,
                        MakerResultMessageStringPoolId,
                        makerResultMessage,
                        RemoteEffectChatLogType,
                        currentTime));
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered maker-result chat-log message {makerResultCode}.";
                    return true;

                case RemoteUserEffectSubtype.IncubatorMessage:
                    string incubatorMessage = MapleStoryStringPool.GetOrFallback(
                        IncubatorMessageStringPoolId,
                        IncubatorMessageFallbackText);
                    ChatLogMessageRegistered?.Invoke(new RemoteChatLogMessagePresentation(
                        packet.CharacterId,
                        packet.EffectType,
                        IncubatorMessageStringPoolId,
                        incubatorMessage,
                        RemoteEffectChatLogType,
                        currentTime));
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered incubator chat-log message.";
                    return true;

                case RemoteUserEffectSubtype.IncDecHp:
                    int delta = packet.Int32Value.GetValueOrDefault();
                    HitFeedbackRegistered?.Invoke(new RemoteHitFeedbackPresentation(
                        packet.CharacterId,
                        ResolveStandardWorldAnchor(actor, currentTime, verticalOffset: 24f),
                        delta,
                        currentTime));
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered inc/dec HP delta {delta}.";
                    return true;

                case RemoteUserEffectSubtype.QuestDeliveryStart:
                    int itemId = packet.Int32Value.GetValueOrDefault();
                    if (itemId <= 0)
                    {
                        message = $"Remote user {packet.CharacterId} quest-delivery item ID {itemId} is invalid.";
                        return false;
                    }

                    ApplyRemoteQuestDeliveryState(actor, itemId, currentTime);
                    if (TryResolveRemoteConsumeEffectSoundDescriptor(itemId, out string questDeliverySoundDescriptor))
                    {
                        RegisterClientSoundPresentation(
                            packet.CharacterId,
                            questDeliverySoundDescriptor,
                            defaultImageName: null,
                            currentTime);
                    }
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} started quest-delivery presentation with item {itemId}.";
                    return true;

                case RemoteUserEffectSubtype.QuestDeliveryEnd:
                    ClearRemoteQuestDeliveryState(actor, currentTime);
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} cleared quest-delivery presentation.";
                    return true;

                case RemoteUserEffectSubtype.EvolRingStatusBar:
                    StatusBarEffectRegistered?.Invoke(new RemoteStatusBarEffectPresentation(
                        packet.CharacterId,
                        packet.EffectType,
                        EvolRingStatusBarEffectName,
                        currentTime));
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} registered status-bar {EvolRingStatusBarEffectName} presentation.";
                    return true;

                default:
                    message = $"Remote user {packet.CharacterId} effect subtype {packet.EffectType} preserved without a recovered simulator presentation.";
                    return true;
            }
        }

        private static string FormatRemoteEffectChatLogMessage(
            int stringPoolId,
            string fallbackFormat,
            int value)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            try
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, format, value);
            }
            catch (FormatException)
            {
                return $"{format} ({value.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
            }
        }

        private void RegisterClientSoundPresentation(
            int characterId,
            string soundPath,
            string defaultImageName,
            int currentTime,
            Vector2? worldOrigin = null)
        {
            if (string.IsNullOrWhiteSpace(soundPath))
            {
                return;
            }

            ClientSoundRegistered?.Invoke(new RemoteClientSoundPresentation(
                characterId,
                soundPath,
                defaultImageName,
                currentTime,
                worldOrigin));
        }

        private static string ResolveRemoteSoundDescriptor(int stringPoolId, string fallbackDescriptor)
        {
            return MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackDescriptor);
        }

        internal static bool ShouldAttachRemotePacketOwnedStringEffectForParity(RemoteUserEffectSubtype? subtype)
        {
            // CUser::OnEffect uses packet-time world anchors for:
            // - case 20 (ReservedEffect): Effect_Reserved(x,y)
            // - case 25 (StringEffect): Effect_General(..., x, y, ...)
            // - case 26 (ItemSoundStringEffect): Effect_General(..., x, y, ...)
            // Keep those detached from live vecctrl follow.
            return subtype != RemoteUserEffectSubtype.ReservedEffect
                && subtype != RemoteUserEffectSubtype.StringEffect
                && subtype != RemoteUserEffectSubtype.ItemSoundStringEffect;
        }

        internal static Vector2? ResolveRemotePacketOwnedStringEffectWorldOriginForParity(RemoteUserEffectPacket packet)
        {
            return ResolveRemotePacketOwnedStringEffectWorldOriginForParity(packet, null);
        }

        internal static Vector2? ResolveRemotePacketOwnedStringEffectWorldOriginForParity(
            RemoteUserEffectPacket packet,
            Vector2? ownerPosition)
        {
            if (packet.KnownSubtype != RemoteUserEffectSubtype.ReservedEffect
                && packet.KnownSubtype != RemoteUserEffectSubtype.StringEffect
                && packet.KnownSubtype != RemoteUserEffectSubtype.ItemSoundStringEffect)
            {
                return null;
            }

            int[] trailing = packet.TrailingInt32Values;
            if (trailing == null || trailing.Length <= 0)
            {
                return ownerPosition;
            }

            if (trailing.Length >= 3)
            {
                return new Vector2(trailing[1], trailing[2]);
            }

            if (trailing.Length >= 2)
            {
                return new Vector2(trailing[0], trailing[1]);
            }

            return ownerPosition;
        }

        private static bool IsAriantArenaRemoteActor(RemoteUserActor actor)
        {
            return actor != null
                && string.Equals(actor.SourceTag, "ariantarena", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryResolveRemoteConsumeEffectSoundDescriptor(int itemId, out string descriptor)
        {
            descriptor = null;
            if (itemId <= 0)
            {
                return false;
            }

            string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                ConsumeEffectSoundPathStringPoolId,
                ConsumeEffectSoundPathFallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            descriptor = string.Format(System.Globalization.CultureInfo.InvariantCulture, compositeFormat, itemId);
            return !string.IsNullOrWhiteSpace(descriptor);
        }

        internal static string ResolveRemoteSkillUseAppointedActionNameForParity(
            SkillData skill,
            byte? characterLevel,
            byte? skillLevel,
            int currentTime)
        {
            if (skill?.ActionNames == null || skill.ActionNames.Count == 0)
            {
                return string.Empty;
            }

            List<string> actionNames = null;
            for (int i = 0; i < skill.ActionNames.Count; i++)
            {
                string actionName = skill.ActionNames[i];
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                actionNames ??= new List<string>(skill.ActionNames.Count);
                actionNames.Add(actionName.Trim());
            }

            if (actionNames == null || actionNames.Count == 0)
            {
                return string.Empty;
            }

            if (actionNames.Count == 1)
            {
                return actionNames[0];
            }

            // The client calls rand() for SKILLENTRY::GetRandomAppointedAction without a packet seed.
            // Mix the packet-owned fields with the current frame time so repeated remote skill-use packets
            // still fan out across the authored action table instead of pinning the first row forever.
            int selectionSeed = currentTime
                ^ (skill.SkillId * 397)
                ^ (characterLevel.GetValueOrDefault() << 8)
                ^ skillLevel.GetValueOrDefault();
            int selectedIndex = ((selectionSeed % actionNames.Count) + actionNames.Count) % actionNames.Count;
            return actionNames[selectedIndex];
        }

        private static bool TryApplyRemoteDragonOfficialSkillUseFollowUp(
            RemoteUserActor actor,
            int skillId,
            string appointedActionName,
            int currentTime,
            out string dragonActionName)
        {
            dragonActionName = null;
            if (actor?.Build == null
                || !TryResolveRemoteDragonHudMetadata(actor.Build.Job, out RemoteDragonHudMetadata metadata)
                || !TryResolveRemoteDragonOfficialSkillUseActionForParity(actor.Build.Job, appointedActionName, metadata, out dragonActionName))
            {
                return false;
            }

            actor.RemoteDragonAttackSkillId = skillId;
            actor.RemoteDragonAttackActionName = dragonActionName;
            actor.RemoteDragonAttackStartTime = currentTime;

            if (actor.PreparedSkill != null
                && actor.PreparedSkill.SkillId == skillId)
            {
                actor.PreparedSkill.DragonActionName = dragonActionName;
                actor.PreparedSkill.DragonActionStartTime = currentTime;
                actor.PreparedSkill.DragonOwnerActionStartTime = int.MinValue;
            }

            return true;
        }

        internal static bool TryResolveRemoteDragonOfficialSkillUseActionForParity(
            int jobId,
            string appointedActionName,
            RemoteDragonHudMetadata metadata,
            out string dragonActionName)
        {
            dragonActionName = null;
            if (jobId < 2200
                || jobId > 2218
                || string.IsNullOrWhiteSpace(appointedActionName)
                || metadata.ActionTimelines == null
                || metadata.ActionTimelines.Count == 0)
            {
                return false;
            }

            foreach (string candidate in EnumerateRemoteDragonActionCandidates(appointedActionName))
            {
                if (IsExplicitRemoteDragonAction(candidate)
                    && !DragonActionLoader.IsClientHeldActionName(candidate)
                    && metadata.HasAction(candidate))
                {
                    dragonActionName = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryApplyUpgradeTombEffect(RemoteUserUpgradeTombPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.ContainsKey(packet.CharacterId))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            UpgradeTombEffectRegistered?.Invoke(new RemoteUpgradeTombPresentation(
                packet.CharacterId,
                packet.ItemId,
                new Vector2(packet.PositionX, packet.PositionY),
                currentTime));
            message = $"Remote user {packet.CharacterId} upgrade tomb effect registered at ({packet.PositionX}, {packet.PositionY}).";
            return true;
        }

        public bool TryApplyReceiveHp(RemoteUserReceiveHpPacket packet, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            if (packet.MaxHp <= 0)
            {
                message = $"Remote user {packet.CharacterId} receive-HP max HP {packet.MaxHp} is invalid.";
                return false;
            }

            (int partyHpPercent, int gaugePos) = ResolveRemoteReceiveHpGaugeForParity(
                packet.CurrentHp,
                packet.MaxHp);
            actor.PartyCurrentHp = packet.CurrentHp;
            actor.PartyMaxHp = packet.MaxHp;
            actor.PartyHpPercent = partyHpPercent;
            actor.PartyHpGaugePos = gaugePos;
            message = $"Remote user {packet.CharacterId} receive-HP gauge updated to {packet.CurrentHp}/{packet.MaxHp}.";
            return true;
        }

        internal static (int PartyHpPercent, int GaugePos) ResolveRemoteReceiveHpGaugeForParity(
            int currentHp,
            int maxHp)
        {
            if (maxHp <= 0)
            {
                return (0, 0);
            }

            // Client `CUserRemote::OnReceiveHP` stores the raw integer ratios.
            int partyHpPercent = (int)((long)currentHp * 100L / maxHp);
            int gaugePos = (int)((long)RemoteReceiveHpGaugeFillWidth * currentHp / maxHp);
            return (partyHpPercent, gaugePos);
        }

        internal static int ClampRemoteReceiveHpGaugeFillWidthForParity(int gaugePos)
        {
            return Math.Clamp(gaugePos, 0, RemoteReceiveHpGaugeFillWidth);
        }

        public bool TryApplyThrowGrenade(RemoteUserThrowGrenadePacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} is not active.";
                return false;
            }

            if (TryApplyRemotePreparedSkillRelease(actor, packet.SkillId, preparedSkillReleaseFollowUpValue: null, out int releasedPreparedSkillId))
            {
                TryRegisterRemotePreparedSkillReleaseSkillUse(actor, releasedPreparedSkillId, currentTime);
            }
            actor.LastThrowGrenadeSkillId = packet.SkillId;
            actor.LastThrowGrenadeSkillLevel = packet.SkillLevel;
            actor.LastThrowGrenadeTarget = new Point(packet.X, packet.Y);
            actor.LastThrowGrenadeKeyDownTime = packet.KeyDownTime;
            actor.LastThrowGrenadePacketTime = currentTime;
            RemoteGrenadePresentation grenadePresentation = BuildRemoteThrowGrenadePresentationForParity(
                actor.CharacterId,
                packet.SkillId,
                packet.SkillLevel,
                Math.Max(1, actor.Build?.Level ?? 1),
                new Point(packet.X, packet.Y),
                packet.KeyDownTime,
                actor.FacingRight,
                currentTime,
                ResolveRemoteGrenadeMaxGaugeTimeMs(packet.SkillId));
            GrenadeRegistered?.Invoke(grenadePresentation);
            SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                actor.CharacterId,
                packet.SkillId,
                null,
                actor.FacingRight,
                currentTime,
                new[] { "special" },
                new Vector2(packet.X, packet.Y),
                FollowOwnerPosition: false,
                FollowOwnerFacing: false,
                DelayRateOverride: 1000));
            message = $"Remote user {packet.CharacterId} throw-grenade packet stored for skill {packet.SkillId} at ({packet.X}, {packet.Y}).";
            return true;
        }

        public static RemoteGrenadePresentation BuildRemoteThrowGrenadePresentationForParity(
            int characterId,
            int skillId,
            int skillLevel,
            int ownerLevel,
            Point target,
            int keyDownTime,
            bool facingRight,
            int currentTime,
            int maxGaugeTimeMs = 0)
        {
            int clampedKeyDownTime = ResolveRemoteThrowGrenadeKeyDownTimeMs(skillId, keyDownTime);
            if (skillId == MonsterBombSkillId)
            {
                int gaugeTime = Math.Max(MonsterBombMinimumKeyDownMs, clampedKeyDownTime);
                int maxGauge = maxGaugeTimeMs > 0 ? maxGaugeTimeMs : ResolveRemoteGrenadeMaxGaugeTimeMs(skillId);
                int impactMagnitudeX = (int)((double)gaugeTime / maxGauge * MonsterBombGaugeDistance);
                int squaredY = Math.Max(0, MonsterBombGaugeDistanceSquaredPlusFloor - (impactMagnitudeX * impactMagnitudeX));
                int impactMagnitudeY = (int)Math.Sqrt(squaredY);
                float impactX = facingRight ? impactMagnitudeX : -impactMagnitudeX;
                int dragDenominator = Math.Max(1, impactMagnitudeX + impactMagnitudeY);
                return new RemoteGrenadePresentation(
                    characterId,
                    skillId,
                    skillLevel,
                    Math.Max(1, ownerLevel),
                    target,
                    clampedKeyDownTime,
                    new Vector2(impactX, impactMagnitudeY),
                    facingRight,
                    currentTime,
                    GravityFree: true,
                    UsesMonsterBombGauge: true,
                    InitDelayMs: MonsterBombInitDelayMs,
                    ExplosionDelayMs: MonsterBombExplosionDelayMs,
                    DragX: (int)(GrenadeDragScale * (impactMagnitudeX / (double)dragDenominator)),
                    DragY: (int)(GrenadeDragScale * (impactMagnitudeY / (double)dragDenominator)),
                    LimitTimeMs: MonsterBombLimitTimeMs);
            }

            int impactMagnitude = ResolveRemoteGenericGrenadeImpactMagnitudeForParity(clampedKeyDownTime);
            return new RemoteGrenadePresentation(
                characterId,
                skillId,
                skillLevel,
                Math.Max(1, ownerLevel),
                target,
                clampedKeyDownTime,
                new Vector2(facingRight ? impactMagnitude : -impactMagnitude, -impactMagnitude),
                facingRight,
                currentTime,
                GravityFree: false,
                UsesMonsterBombGauge: false,
                InitDelayMs: 0,
                ExplosionDelayMs: 0,
                DragX: 0,
                DragY: 0,
                LimitTimeMs: 0);
        }

        internal static bool ResolveRemoteGrenadeFacingForParity(RemoteGrenadePresentation presentation)
        {
            return presentation.FacingRight;
        }

        public static Vector2 ResolveRemoteGrenadeFlightOriginForParity(RemoteGrenadePresentation presentation)
        {
            Vector2 spawnPoint = new(presentation.Target.X, presentation.Target.Y);
            if (presentation.UsesMonsterBombGauge)
            {
                return spawnPoint;
            }

            // Client CGrenade::Init starts non-body-bomb vec-control at (x +/- 1, y - 20)
            // while the object itself is still created at packet position (x, y).
            int offsetX = presentation.FacingRight
                ? GenericGrenadeFlightLaunchOffsetX
                : -GenericGrenadeFlightLaunchOffsetX;
            return new Vector2(
                spawnPoint.X + offsetX,
                spawnPoint.Y + GenericGrenadeFlightLaunchOffsetY);
        }

        public static int ResolveRemoteGrenadeFlightStartTimeForParity(RemoteGrenadePresentation presentation)
        {
            return presentation.CurrentTime + Math.Max(0, presentation.InitDelayMs);
        }

        public static int ResolveRemoteGrenadeExplosionTimeForParity(
            RemoteGrenadePresentation presentation,
            SkillAnimation ballAnimation)
        {
            // CUser::ThrowGrenade writes CGrenade::Init tLimitTime for Monster Bomb.
            // Keep that packet-owned window as a cap on pre-explosion time, then
            // allow explosion playback to own the final expiry segment.
            int limitCapTime = presentation.LimitTimeMs > 0
                ? presentation.CurrentTime + presentation.LimitTimeMs
                : int.MaxValue;

            if (presentation.ExplosionDelayMs > presentation.InitDelayMs)
            {
                return Math.Min(
                    presentation.CurrentTime + presentation.ExplosionDelayMs,
                    limitCapTime);
            }

            return Math.Min(
                ResolveRemoteGrenadeFlightStartTimeForParity(presentation)
                    + ResolveRemoteGrenadeAnimationDurationMs(ballAnimation),
                limitCapTime);
        }

        public static int ResolveRemoteGrenadeExpireTimeForParity(
            RemoteGrenadePresentation presentation,
            SkillAnimation ballAnimation,
            SkillAnimation explosionAnimation)
        {
            return ResolveRemoteGrenadeExplosionTimeForParity(presentation, ballAnimation)
                + ResolveRemoteGrenadeAnimationDurationMs(explosionAnimation);
        }

        public static Vector2 ResolveRemoteGrenadePositionForParity(
            Vector2 origin,
            Vector2 impact,
            int elapsedMs,
            int flightDurationMs = 1000,
            int dragX = 0,
            int dragY = 0,
            bool gravityFree = false)
        {
            int clampedDuration = Math.Max(1, flightDurationMs);
            float progress = Math.Clamp(Math.Max(0, elapsedMs) / (float)clampedDuration, 0f, 1f);
            Vector2 position = origin + (impact * progress);

            if (!gravityFree && progress > 0f)
            {
                // v95 CGrenade::Init sets m_bGravityFree for Monster Bomb and keeps
                // non-body-bomb throws on regular vec-control gravity.
                float elapsedSeconds = Math.Max(0, elapsedMs) / 1000f;
                position.Y += 0.5f * CVecCtrl.GravityAcceleration * elapsedSeconds * elapsedSeconds;
            }

            if (dragY > 0 && progress > 0f && progress < 1f)
            {
                // Client `CUser::ThrowGrenade` writes drag values for Monster Bomb through
                // `CGrenade::SetDragValue`, and the vec-control update bends flight away
                // from a pure linear segment before the impact point.
                float normalizedDragY = Math.Clamp(dragY / (float)GrenadeDragScale, 0f, 1f);
                float arcOffset = Math.Abs(impact.Y) * normalizedDragY * (4f * progress * (1f - progress));
                position.Y -= arcOffset;
            }

            if (dragX > 0 && progress > 0f && progress < 1f)
            {
                float normalizedDragX = Math.Clamp(dragX / (float)GrenadeDragScale, 0f, 1f);
                float horizontalEase = Math.Abs(impact.X) * normalizedDragX * 0.15f * (progress * (1f - progress));
                position.X += Math.Sign(impact.X) * horizontalEase;
            }

            return position;
        }

        public static Vector2 ResolveRemoteGrenadeRenderOffsetForParity(
            RemoteGrenadePresentation presentation,
            bool includeRotateLayerCollisionCompensation = true)
        {
            if (!presentation.UsesMonsterBombGauge)
            {
                return Vector2.Zero;
            }

            // CGrenade::PrepareAnimationLayer always keeps the signed horizontal shift.
            // The rotate-layer collision Offset(...) is additionally gated by the
            // client layer flip flag, which corresponds to the left-facing grenade.
            int collisionOffsetX = 0;
            int collisionOffsetY = 0;
            if (includeRotateLayerCollisionCompensation
                && !presentation.FacingRight)
            {
                int angle = ResolveRemoteGrenadeBallAngleDegreesForParity(presentation);
                double radians = angle * (Math.PI / 180d);
                collisionOffsetX = (int)(Math.Cos(radians) * MonsterBombCollisionRadius);
                collisionOffsetY = (int)(Math.Sin(radians) * MonsterBombCollisionRadius);
            }

            int horizontalShift = presentation.FacingRight
                ? -MonsterBombAnimationLayerOffsetX
                : MonsterBombAnimationLayerOffsetX;
            return new Vector2(
                horizontalShift - collisionOffsetX,
                -collisionOffsetY);
        }

        private static int ResolveRemoteGrenadeAnimationDurationMs(SkillAnimation animation)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return 0;
            }

            if (animation.TotalDuration <= 0)
            {
                animation.CalculateDuration();
            }

            return Math.Max(0, animation.TotalDuration);
        }

        private static int ResolveRemoteThrowGrenadeKeyDownTimeMs(int skillId, int keyDownTime)
        {
            // CUser::ThrowGrenade uses raw packet tKeyDown for non-body-bomb impact scaling,
            // while Monster Bomb keeps only the >= 500 floor before gauge conversion.
            return skillId == MonsterBombSkillId
                ? Math.Max(MonsterBombMinimumKeyDownMs, keyDownTime)
                : keyDownTime;
        }

        private static int ResolveRemoteGenericGrenadeImpactMagnitudeForParity(int keyDownTimeMs)
        {
            // Client CUser::ThrowGrenade computes non-4341003 impact as
            // (double)tKeyDown / 1000.0 * 600.0 and then truncates to int.
            double scaledImpact = keyDownTimeMs / 1000d * GenericGrenadeImpactScale;
            return (int)scaledImpact;
        }

        private static int ResolveRemoteGrenadeBallAngleDegreesForParity(RemoteGrenadePresentation presentation)
        {
            if (!presentation.UsesMonsterBombGauge)
            {
                return 0;
            }

            double absX = Math.Abs(presentation.Impact.X);
            double absY = Math.Abs(presentation.Impact.Y);
            if (absX <= 0d)
            {
                return 90;
            }

            return (int)(Math.Atan(absY / absX) * (180d / Math.PI));
        }

        private static int ResolveRemoteGrenadeMaxGaugeTimeMs(int skillId)
        {
            if (skillId != MonsterBombSkillId)
            {
                return 1000;
            }

            WzImage skillImage = Program.FindImage("Skill", $"{skillId / 10000}.img");
            skillImage?.ParseImage();
            if (skillImage?["skill"]?[skillId.ToString("D7", System.Globalization.CultureInfo.InvariantCulture)]?["common"]?["time"] is WzStringProperty stringTime
                && int.TryParse(stringTime.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int seconds)
                && seconds > 0)
            {
                return seconds * 1000;
            }

            if (skillImage?["skill"]?[skillId.ToString("D7", System.Globalization.CultureInfo.InvariantCulture)]?["common"]?["time"] is WzIntProperty intTime
                && intTime.Value > 0)
            {
                return intTime.Value * 1000;
            }

            return 3000;
        }

        public bool TryClearPreparedSkill(int characterId, int currentTime, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (actor.PreparedSkill != null
                && RemotePreparedSkillUseEffectSkillIds.Contains(actor.PreparedSkill.SkillId))
            {
                IReadOnlyList<string> branchNames = ResolveRemotePreparedSkillUseReleaseBranchNames(actor.PreparedSkill.SkillId);
                SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                    actor.CharacterId,
                    actor.PreparedSkill.SkillId,
                    null,
                    actor.FacingRight,
                    currentTime,
                    branchNames));
            }

            actor.PreparedSkill = null;
            actor.MovingShootPreparedSkillId = 0;
            return true;
        }

        public bool TrySetWorldVisibility(int characterId, bool isVisibleInWorld, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.IsVisibleInWorld = isVisibleInWorld;
            return true;
        }

        public bool TryRemove(int characterId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            ClearActorFollowLinks(actor);
            ClearRemoteActiveEffectMotionBlurState(actor);
            ClearRemoteActiveEffectItemEffectState(actor);
            ReleaseCarryItemEffectLayerReferenceForParity(actor);
            ReleaseCompletedSetItemEffectLayerReferenceForParity(actor);
            ClearRemoteTransientItemEffectStates(actor);
            ClearRemoteDragonCompanionState(actor);
            ClearPortableChairPairRecord(characterId);
            PurgeRelationshipRecordsForActor(characterId);
            NotifyActorRemoved(actor.CharacterId, actor.Name);
            _actorsById.Remove(characterId);
            _actorIdsByName.Remove(actor.Name);
            _pendingTransientSkillUseAvatarEffectsByCharacterId.Remove(characterId);
            _remoteAuxiliaryLayerOwnerCountersByCharacterId.Remove(characterId);
            return true;
        }

        private void NotifyActorRemoved(int characterId, string name)
        {
            ActorRemovedCallback?.Invoke(characterId, name);
        }

        public void Update(int currentTime, PlayerCharacter localPlayer = null)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.FollowDriverId <= 0 && actor.MovementSnapshot != null)
                {
                    ApplyMovementSnapshot(actor, currentTime);
                }

                ApplyFollowDriverState(actor, localPlayer);

                if (actor.PreparedSkill != null
                    && (actor.PreparedSkill.DurationMs > 0 || actor.PreparedSkill.AutoEnterHold))
                {
                    if (!TryResolvePreparedSkillPhase(actor.PreparedSkill, currentTime, out _, out _, out _, out _, out _))
                    {
                        actor.PreparedSkill = null;
                    }
                }

                foreach (KeyValuePair<RemoteRelationshipOverlayType, RemoteRelationshipOverlayState> overlayEntry in actor.RelationshipOverlays.ToArray())
                {
                    RemoteRelationshipOverlayState overlay = overlayEntry.Value;
                    if (overlay?.Effect == null
                        || overlay.RelationshipType != RemoteRelationshipOverlayType.Generic
                        || overlay.Effect.TotalDurationMs <= 0
                        || !HasRemoteClientOwnedAuxiliaryAvatarLayerDurationElapsed(
                            currentTime,
                            overlay.StartTime,
                            overlay.Effect.TotalDurationMs))
                    {
                        continue;
                    }

                    ClearRelationshipOverlayStateForParity(actor, overlayEntry.Key, currentTime);
                }

                UpdatePacketOwnedEmotionState(actor, currentTime);
                UpdateRemoteActiveEffectMotionBlurState(actor, currentTime);
                UpdateRemoteActiveEffectItemEffectLayerAdmission(actor);
                UpdateCarryItemEffectLayerAdmissionForParity(actor);
                UpdateCompletedSetItemEffectLayerAdmissionForParity(actor);
                UpdateTransientItemEffects(actor, currentTime);
                UpdateTransientSkillUseAvatarEffects(actor, currentTime);
                actor.UpdateMeleeAfterImage(currentTime);
            }
        }

        public void SyncPortableChairPairState(PlayerCharacter player)
        {
            if (player == null)
            {
                return;
            }

            player.ClearPortableChairExternalOwnerPair();
            PortableChair chair = player.Build?.ActivePortableChair;
            bool requestsExternalPair = chair?.IsCoupleChair == true;
            player.SetPortableChairPairRequestActive(requestsExternalPair);
            int localCharacterId = player.Build?.Id ?? 0;
            Dictionary<int, int> pairMap = BuildPortableChairPairMap(player, preferVisibleOnly: true);
            if (requestsExternalPair)
            {
                if (pairMap.TryGetValue(localCharacterId, out int pairCharacterId)
                    && _actorsById.TryGetValue(pairCharacterId, out RemoteUserActor pairActor))
                {
                    player.SetPortableChairExternalPair(pairActor.Position, pairActor.FacingRight);
                }
                else
                {
                    player.ClearPortableChairExternalPair();
                }
            }

            if (TryResolvePortableChairOwnerForLocalPlayer(player, pairMap, out RemoteUserActor ownerActor))
            {
                player.SetPortableChairExternalOwnerPair(
                    ownerActor.Build.ActivePortableChair,
                    ownerActor.Position,
                    ownerActor.FacingRight);
            }
        }

        public int? LocalPortableChairPreferredPairCharacterId => _localPortableChairPreferredPairCharacterId;

        public void ClearLocalPortableChairPairPreference()
        {
            _localPortableChairPreferredPairCharacterId = null;
        }

        public bool TrySetLocalPortableChairPairPreference(PlayerCharacter localPlayer, int? pairCharacterId, out string message)
        {
            message = null;
            if (pairCharacterId is null or <= 0)
            {
                _localPortableChairPreferredPairCharacterId = null;
                message = "Cleared the preferred couple-chair partner.";
                return true;
            }

            if (localPlayer?.Build == null)
            {
                message = "Local player runtime is not available.";
                return false;
            }

            int localCharacterId = localPlayer.Build.Id;
            if (pairCharacterId.Value == localCharacterId)
            {
                message = "The local player cannot pair with itself.";
                return false;
            }

            if (!_actorsById.TryGetValue(pairCharacterId.Value, out RemoteUserActor actor))
            {
                message = $"Remote user {pairCharacterId.Value} is not active.";
                return false;
            }

            _localPortableChairPreferredPairCharacterId = pairCharacterId.Value;

            PortableChair localChair = localPlayer.Build.ActivePortableChair;
            PortableChair remoteChair = actor.Build?.ActivePortableChair;
            if (localChair?.IsCoupleChair == true
                && remoteChair?.IsCoupleChair == true
                && localChair.ItemId == remoteChair.ItemId)
            {
                message = $"Preferred couple-chair partner set to {actor.Name} ({actor.CharacterId}).";
            }
            else
            {
                message = $"Preferred couple-chair partner set to {actor.Name} ({actor.CharacterId}); pairing will apply once both users share a valid couple-chair state.";
            }

            return true;
        }

        public IReadOnlyList<StatusBarPreparedSkillRenderData> BuildPreparedSkillWorldOverlays(int currentTime)
        {
            _preparedSkillWorldOverlayCount = 0;
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (!ShouldIncludePreparedSkillWorldOverlay(actor))
                {
                    continue;
                }

                StatusBarPreparedSkillRenderData overlay = BuildPreparedSkillWorldOverlay(actor, currentTime, _preparedSkillWorldOverlayCount);
                if (overlay == null)
                {
                    continue;
                }

                _preparedSkillWorldOverlayCount++;
            }

            return _preparedSkillWorldOverlayBuffer;
        }

        private static bool ShouldIncludePreparedSkillWorldOverlay(RemoteUserActor actor)
        {
            return actor?.IsVisibleInWorld == true
                && !actor.HiddenLikeClient
                && actor.PreparedSkill != null
                && PreparedSkillHudRules.ShouldShowRemotePreparedSkillHud(
                    actor.PreparedSkill.SkillId,
                    actor.PreparedSkill.IsKeydownSkill);
        }

        internal static bool ShouldIncludePreparedSkillWorldOverlayForTesting(
            bool isVisibleInWorld,
            bool hiddenLikeClient,
            RemotePreparedSkillState prepared)
        {
            return isVisibleInWorld
                && !hiddenLikeClient
                && prepared != null
                && PreparedSkillHudRules.ShouldShowRemotePreparedSkillHud(
                    prepared.SkillId,
                    prepared.IsKeydownSkill);
        }

        private StatusBarPreparedSkillRenderData BuildPreparedSkillWorldOverlay(RemoteUserActor actor, int currentTime, int bufferIndex)
        {
            RemotePreparedSkillState prepared = actor?.PreparedSkill;
            if (prepared == null)
            {
                return null;
            }

            if (!TryResolvePreparedSkillPhase(
                    prepared,
                    currentTime,
                    out int remainingMs,
                    out int duration,
                    out float progress,
                    out bool isHolding,
                    out int holdElapsedMs))
            {
                return null;
            }

            StatusBarPreparedSkillRenderData overlay = GetOrCreatePreparedSkillWorldOverlay(bufferIndex);
            overlay.SkillId = prepared.SkillId;
            overlay.SkillName = prepared.SkillName;
            overlay.SkinKey = prepared.SkinKey;
            overlay.Surface = PreparedSkillHudSurface.World;
            overlay.RemainingMs = remainingMs;
            overlay.DurationMs = duration;
            overlay.GaugeDurationMs = ResolvePreparedSkillGaugeDurationForOverlay(
                prepared.TextVariant,
                prepared.GaugeDurationMs,
                duration);
            overlay.Progress = progress;
            overlay.IsKeydownSkill = prepared.IsKeydownSkill;
            overlay.IsPreparingPhase = prepared.AutoEnterHold && !isHolding && prepared.PrepareDurationMs > 0;
            overlay.IsHolding = isHolding;
            overlay.PrepareRemainingMs = overlay.IsPreparingPhase ? remainingMs : 0;
            overlay.HoldElapsedMs = holdElapsedMs;
            overlay.MaxHoldDurationMs = prepared.MaxHoldDurationMs;
            overlay.TextVariant = prepared.TextVariant;
            overlay.ShowText = prepared.ShowText && !PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId);
            if (!TryResolvePreparedSkillWorldAnchor(actor, prepared, currentTime, isHolding, out Vector2 worldAnchor))
            {
                return null;
            }

            overlay.WorldAnchor = worldAnchor;
            return overlay;
        }

        internal static bool TryResolvePreparedSkillPhase(
            RemotePreparedSkillState prepared,
            int currentTime,
            out int remainingMs,
            out int durationMs,
            out float progress,
            out bool isHolding,
            out int holdElapsedMs)
        {
            remainingMs = 0;
            durationMs = 0;
            progress = 0f;
            isHolding = false;
            holdElapsedMs = 0;

            if (prepared == null)
            {
                return false;
            }

            int elapsed = Math.Max(0, currentTime - prepared.StartTime);
            if (prepared.AutoEnterHold)
            {
                if (prepared.PrepareDurationMs > 0 && elapsed < prepared.PrepareDurationMs)
                {
                    durationMs = prepared.PrepareDurationMs;
                    remainingMs = Math.Max(0, prepared.PrepareDurationMs - elapsed);
                    progress = durationMs > 0
                        ? MathHelper.Clamp(elapsed / (float)durationMs, 0f, 1f)
                        : 0f;
                    return true;
                }

                int holdDurationMs = Math.Max(0, prepared.MaxHoldDurationMs);
                holdElapsedMs = Math.Max(0, elapsed - prepared.PrepareDurationMs);
                if (holdDurationMs > 0 && holdElapsedMs >= holdDurationMs)
                {
                    return false;
                }

                durationMs = holdDurationMs;
                remainingMs = holdDurationMs > 0
                    ? Math.Max(0, holdDurationMs - holdElapsedMs)
                    : 0;
                progress = holdDurationMs > 0
                    ? MathHelper.Clamp(holdElapsedMs / (float)holdDurationMs, 0f, 1f)
                    : 1f;
                isHolding = true;
                return true;
            }

            durationMs = Math.Max(0, prepared.DurationMs);
            if (durationMs > 0 && elapsed >= durationMs)
            {
                return false;
            }

            remainingMs = durationMs > 0
                ? Math.Max(0, durationMs - elapsed)
                : 0;
            progress = durationMs > 0
                ? MathHelper.Clamp(elapsed / (float)durationMs, 0f, 1f)
                : 0f;
            isHolding = prepared.IsHolding;
            holdElapsedMs = isHolding ? elapsed : 0;
            return true;
        }

        private static int ResolvePreparedSkillGaugeDurationForOverlay(
            PreparedSkillHudTextVariant textVariant,
            int gaugeDurationMs,
            int durationMs)
        {
            if (gaugeDurationMs > 0)
            {
                return gaugeDurationMs;
            }

            // Client parity: release-armed families can keep prepare/release caption
            // ownership even when no explicit gauge window exists.
            if (textVariant == PreparedSkillHudTextVariant.ReleaseArmed)
            {
                return 0;
            }

            return Math.Max(0, durationMs);
        }

        internal static int ResolvePreparedSkillGaugeDurationForOverlayForTesting(
            PreparedSkillHudTextVariant textVariant,
            int gaugeDurationMs,
            int durationMs)
        {
            return ResolvePreparedSkillGaugeDurationForOverlay(textVariant, gaugeDurationMs, durationMs);
        }

        private bool TryResolvePreparedSkillWorldAnchor(
            RemoteUserActor actor,
            RemotePreparedSkillState prepared,
            int currentTime,
            bool isHolding,
            out Vector2 anchor)
        {
            anchor = Vector2.Zero;
            if (actor == null)
            {
                return false;
            }

            if (prepared != null
                && PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId))
            {
                if (TryResolveRemoteDragonKeyDownBarAnchor(actor, prepared, currentTime, isHolding, out Vector2 dragonAnchor))
                {
                    anchor = dragonAnchor;
                    return true;
                }

                return false;
            }

            if (prepared?.SkillId == PreparedSkillHudRules.SG88SkillId)
            {
                anchor = ResolveRemoteSg88KeyDownBarAnchor(actor, _packetOwnedSummonResolver);
                return true;
            }

            anchor = ResolveStandardPreparedSkillWorldAnchor(actor, currentTime);
            return true;
        }

        private static Vector2 ResolveStandardPreparedSkillWorldAnchor(RemoteUserActor actor, int currentTime)
        {
            return ResolveStandardWorldAnchor(actor, currentTime, 18f);
        }

        private static Vector2 ResolveRemoteSg88KeyDownBarAnchor(
            RemoteUserActor actor,
            Func<int, IReadOnlyList<ActiveSummon>> registeredSummonResolver)
        {
            if (actor == null)
            {
                return PreparedSkillHudRules.ResolveClientSg88KeyDownBarAnchor(Vector2.Zero);
            }

            if (TryResolveSg88SummonVector(actor.PacketOwnedSummons?.Summons, out Vector2 summonVector))
            {
                return PreparedSkillHudRules.ResolveClientSg88KeyDownBarAnchor(summonVector);
            }

            IReadOnlyList<ActiveSummon> registeredSummons = registeredSummonResolver?.Invoke(actor.CharacterId);
            if (TryResolveSg88SummonVector(registeredSummons, out summonVector))
            {
                return PreparedSkillHudRules.ResolveClientSg88KeyDownBarAnchor(summonVector);
            }

            return PreparedSkillHudRules.ResolveClientSg88KeyDownBarAnchor(actor.Position);
        }

        private static Vector2 ResolveRemoteSg88KeyDownBarAnchor(
            IReadOnlyList<ActiveSummon> packetOwnedSummons,
            Vector2 ownerVectorControlPosition)
        {
            if (TryResolveSg88SummonVector(packetOwnedSummons, out Vector2 summonVector))
            {
                return PreparedSkillHudRules.ResolveClientSg88KeyDownBarAnchor(summonVector);
            }

            return PreparedSkillHudRules.ResolveClientSg88KeyDownBarAnchor(ownerVectorControlPosition);
        }

        private static bool TryResolveSg88SummonVector(
            IReadOnlyList<ActiveSummon> packetOwnedSummons,
            out Vector2 summonVector)
        {
            summonVector = Vector2.Zero;
            if (packetOwnedSummons != null)
            {
                for (int i = 0; i < packetOwnedSummons.Count; i++)
                {
                    ActiveSummon summon = packetOwnedSummons[i];
                    if (summon?.SkillId == PreparedSkillHudRules.SG88SkillId)
                    {
                        summonVector = new Vector2(summon.PositionX, summon.PositionY);
                        return true;
                    }
                }
            }

            return false;
        }

        internal static Vector2 ResolveRemoteSg88KeyDownBarAnchorForTesting(
            IReadOnlyList<ActiveSummon> packetOwnedSummons,
            Vector2 ownerVectorControlPosition)
        {
            return ResolveRemoteSg88KeyDownBarAnchor(packetOwnedSummons, ownerVectorControlPosition);
        }

        internal static Vector2 ResolveRemoteSg88KeyDownBarAnchorForTesting(
            IReadOnlyList<ActiveSummon> actorPacketOwnedSummons,
            IReadOnlyList<ActiveSummon> registeredOwnerSummons,
            Vector2 ownerVectorControlPosition)
        {
            var actor = new RemoteUserActor(
                1,
                "Remote1",
                new CharacterBuild { Id = 1, Name = "Remote1" },
                ownerVectorControlPosition,
                true,
                "stand1",
                "test",
                true);
            if (actorPacketOwnedSummons != null)
            {
                foreach (ActiveSummon summon in actorPacketOwnedSummons)
                {
                    actor.PacketOwnedSummons.AddOrReplace(summon);
                }
            }

            return ResolveRemoteSg88KeyDownBarAnchor(actor, _ => registeredOwnerSummons);
        }

        private static Vector2 ResolveStandardWorldAnchor(RemoteUserActor actor, int currentTime, float verticalOffset)
        {
            AssembledFrame frame = actor?.GetFrameAtTimeForRendering(currentTime);
            if (frame != null)
            {
                float topY = actor.Position.Y - frame.FeetOffset + frame.Bounds.Top;
                return new Vector2(actor.Position.X, topY - verticalOffset);
            }

            return new Vector2(actor.Position.X, actor.Position.Y - (verticalOffset + 62f));
        }

        private static bool TryResolveRemoteDragonKeyDownBarAnchor(
            RemoteUserActor actor,
            RemotePreparedSkillState prepared,
            int currentTime,
            bool isHolding,
            out Vector2 anchor)
        {
            anchor = Vector2.Zero;
            if (actor?.Build == null
                || !TryResolveRemoteDragonHudMetadata(actor.Build.Job, out RemoteDragonHudMetadata metadata))
            {
                return false;
            }

            AssembledFrame ownerFrame = actor?.GetFrameAtTimeForRendering(currentTime);
            float ownerBodyOriginY = ownerFrame != null
                ? actor.Position.Y - ownerFrame.FeetOffset
                : actor.Position.Y;
            string ownerPacketActionName = ResolveRemoteDragonOwnerPacketActionName(actor);
            string dragonActionName;
            int dragonActionElapsedMs;
            if (!TryResolveRemoteDragonPacketOwnedAttackAction(
                    actor,
                    prepared,
                    metadata,
                    currentTime,
                    out dragonActionName,
                    out dragonActionElapsedMs))
            {
                ResolveRemoteDragonActionSelection(
                    prepared,
                    isHolding,
                    ownerPacketActionName,
                    actor.BaseActionRawCode,
                    metadata,
                    out dragonActionName,
                    out bool useOwnerActionTimeline);
                dragonActionElapsedMs = ResolveRemoteDragonActionElapsedMs(
                    prepared,
                    currentTime,
                    dragonActionName,
                    isHolding,
                    useOwnerActionTimeline,
                    actor.BaseActionStartTime);
            }

            int dragonFrameHeight = metadata.ResolveFrameHeight(dragonActionName, dragonActionElapsedMs);
            Vector2 dragonAnchor = ResolveRemoteDragonAnchor(
                actor,
                ownerFrame,
                ownerPacketActionName,
                actor.BaseActionRawCode,
                ownerBodyOriginY,
                dragonActionName,
                dragonActionElapsedMs,
                metadata);
            dragonAnchor = ResolveRemoteDragonHudVisualAnchor(prepared, dragonAnchor, currentTime);
            anchor = new Vector2(
                dragonAnchor.X - RemoteDragonKeyDownBarHalfWidth,
                dragonAnchor.Y - dragonFrameHeight - RemoteDragonKeyDownBarVerticalGap);
            return true;
        }

        private static bool TryResolveRemoteDragonPacketOwnedAttackAction(
            RemoteUserActor actor,
            RemotePreparedSkillState prepared,
            RemoteDragonHudMetadata metadata,
            int currentTime,
            out string actionName,
            out int elapsedMs)
        {
            actionName = null;
            elapsedMs = 0;
            if (actor == null
                || prepared == null
                || actor.RemoteDragonAttackSkillId != prepared.SkillId
                || string.IsNullOrWhiteSpace(actor.RemoteDragonAttackActionName)
                || actor.RemoteDragonAttackStartTime == int.MinValue
                || metadata.ActionTimelines == null
                || !metadata.ActionTimelines.TryGetValue(actor.RemoteDragonAttackActionName, out RemoteDragonHudAnimationTimeline timeline))
            {
                return false;
            }

            elapsedMs = Math.Max(0, currentTime - actor.RemoteDragonAttackStartTime);
            if (!timeline.Loop
                && timeline.TotalDurationMs > 0
                && elapsedMs >= timeline.TotalDurationMs)
            {
                actor.RemoteDragonAttackSkillId = null;
                actor.RemoteDragonAttackActionName = null;
                actor.RemoteDragonAttackStartTime = int.MinValue;
                return false;
            }

            actionName = actor.RemoteDragonAttackActionName;
            prepared.DragonActionName = actionName;
            prepared.DragonActionStartTime = actor.RemoteDragonAttackStartTime;
            prepared.DragonOwnerActionStartTime = int.MinValue;
            return true;
        }

        internal static bool TryResolveRemoteDragonCompanionRenderStateForParity(
            RemoteUserActor actor,
            int currentTime,
            out RemoteDragonCompanionRenderState renderState)
        {
            renderState = default;
            if (actor?.Build == null
                || !TryResolveRemoteDragonHudMetadata(actor.Build.Job, out RemoteDragonHudMetadata metadata))
            {
                return false;
            }

            return TryResolveRemoteDragonCompanionRenderStateForParity(actor, metadata, currentTime, out renderState);
        }

        internal static bool TryResolveRemoteDragonCompanionRenderStateForParity(
            RemoteUserActor actor,
            RemoteDragonHudMetadata metadata,
            int currentTime,
            out RemoteDragonCompanionRenderState renderState)
        {
            renderState = default;
            if (actor?.Build == null
                || metadata.ActionTimelines == null
                || metadata.ActionTimelines.Count == 0)
            {
                return false;
            }

            actor.RemoteDragonCompanion ??= new RemoteDragonCompanionPresentationState();
            RemoteDragonCompanionPresentationState dragon = actor.RemoteDragonCompanion;
            EnsureRemoteDragonCompanionLayerReference(dragon);
            AssembledFrame ownerFrame = actor.GetFrameAtTimeForRendering(currentTime);
            float ownerBodyOriginY = ownerFrame != null
                ? actor.Position.Y - ownerFrame.FeetOffset
                : actor.Position.Y;
            string ownerPacketActionName = ResolveRemoteDragonOwnerPacketActionName(actor);

            string dragonActionName;
            int dragonActionElapsedMs;
            if (!TryResolveRemoteDragonPacketOwnedAttackAction(
                    actor,
                    metadata,
                    currentTime,
                    out dragonActionName,
                    out dragonActionElapsedMs))
            {
                ResolveRemoteDragonCompanionActionSelection(
                    dragon,
                    ownerPacketActionName,
                    actor.BaseActionRawCode,
                    actor.BaseActionStartTime,
                    metadata,
                    currentTime,
                    out dragonActionName,
                    out dragonActionElapsedMs);
            }

            Vector2 targetAnchor = ResolveRemoteDragonAnchor(
                actor,
                ownerFrame,
                ownerPacketActionName,
                actor.BaseActionRawCode,
                ownerBodyOriginY,
                dragonActionName,
                dragonActionElapsedMs,
                metadata);
            Vector2 visualAnchor = ResolveRemoteDragonCompanionVisualAnchor(dragon, targetAnchor, currentTime);
            renderState = new RemoteDragonCompanionRenderState(
                actor.CharacterId,
                actor.Build.Job,
                dragonActionName,
                targetAnchor,
                visualAnchor,
                actor.FacingRight,
                dragonActionElapsedMs,
                metadata.ResolveFrameHeight(dragonActionName, dragonActionElapsedMs),
                metadata.ResolveOriginX(dragonActionName, dragonActionElapsedMs));
            return true;
        }

        private static bool TryResolveRemoteDragonPacketOwnedAttackAction(
            RemoteUserActor actor,
            RemoteDragonHudMetadata metadata,
            int currentTime,
            out string actionName,
            out int elapsedMs)
        {
            actionName = null;
            elapsedMs = 0;
            if (actor == null
                || string.IsNullOrWhiteSpace(actor.RemoteDragonAttackActionName)
                || actor.RemoteDragonAttackStartTime == int.MinValue
                || metadata.ActionTimelines == null
                || !metadata.ActionTimelines.TryGetValue(actor.RemoteDragonAttackActionName, out RemoteDragonHudAnimationTimeline timeline))
            {
                return false;
            }

            elapsedMs = Math.Max(0, currentTime - actor.RemoteDragonAttackStartTime);
            if (!timeline.Loop
                && timeline.TotalDurationMs > 0
                && elapsedMs >= timeline.TotalDurationMs)
            {
                actor.RemoteDragonAttackSkillId = null;
                actor.RemoteDragonAttackActionName = null;
                actor.RemoteDragonAttackStartTime = int.MinValue;
                return false;
            }

            actionName = actor.RemoteDragonAttackActionName;
            return true;
        }

        private static void ResolveRemoteDragonCompanionActionSelection(
            RemoteDragonCompanionPresentationState dragon,
            string ownerActionName,
            int? ownerRawActionCode,
            int ownerActionStartTime,
            RemoteDragonHudMetadata metadata,
            int currentTime,
            out string actionName,
            out int elapsedMs)
        {
            if (TryResolveRemoteExplicitDragonActionName(
                    ownerActionName,
                    ownerRawActionCode,
                    metadata,
                    out string explicitActionName,
                    out bool useOwnerActionTimeline))
            {
                actionName = explicitActionName;
                elapsedMs = ResolveRemoteDragonCompanionActionElapsedMs(
                    dragon,
                    actionName,
                    currentTime,
                    useOwnerActionTimeline,
                    ownerActionStartTime);
                return;
            }

            actionName = ShouldUseRemoteDragonMoveAction(ownerActionName, ownerRawActionCode) && metadata.HasAction("move")
                ? "move"
                : "stand";
            elapsedMs = ResolveRemoteDragonCompanionActionElapsedMs(
                dragon,
                actionName,
                currentTime,
                useOwnerActionTimeline: false,
                ownerActionStartTime: int.MinValue);
        }

        private static int ResolveRemoteDragonCompanionActionElapsedMs(
            RemoteDragonCompanionPresentationState dragon,
            string actionName,
            int currentTime,
            bool useOwnerActionTimeline,
            int ownerActionStartTime)
        {
            if (dragon == null)
            {
                return 0;
            }

            useOwnerActionTimeline = useOwnerActionTimeline
                && ownerActionStartTime != int.MinValue;
            int actionStartTime = useOwnerActionTimeline
                ? ownerActionStartTime
                : currentTime;
            bool actionChanged = !string.Equals(dragon.ActionName, actionName, StringComparison.OrdinalIgnoreCase);
            bool ownerTimelineChanged = useOwnerActionTimeline
                && dragon.OwnerActionStartTime != ownerActionStartTime;
            bool localTimelineMissing = !useOwnerActionTimeline
                && dragon.ActionStartTime == int.MinValue;

            if (actionChanged || ownerTimelineChanged || localTimelineMissing)
            {
                dragon.ActionName = actionName;
                dragon.ActionStartTime = actionStartTime;
                dragon.OwnerActionStartTime = useOwnerActionTimeline
                    ? ownerActionStartTime
                    : int.MinValue;
            }

            return Math.Max(0, currentTime - dragon.ActionStartTime);
        }

        private static Vector2 ResolveRemoteDragonCompanionVisualAnchor(
            RemoteDragonCompanionPresentationState dragon,
            Vector2 targetAnchor,
            int currentTime)
        {
            if (dragon == null)
            {
                return targetAnchor;
            }

            if (dragon.LastFollowUpdateTime == int.MinValue)
            {
                dragon.VisualAnchor = targetAnchor;
                dragon.FollowVelocity = Vector2.Zero;
                dragon.LastFollowUpdateTime = currentTime;
                return dragon.VisualAnchor;
            }

            int elapsedMs = Math.Max(0, currentTime - dragon.LastFollowUpdateTime);
            dragon.LastFollowUpdateTime = currentTime;
            if (elapsedMs <= 0)
            {
                return dragon.VisualAnchor;
            }

            if (DragonCompanionRuntime.ShouldSnapActiveFollowToTarget(dragon.VisualAnchor, targetAnchor))
            {
                dragon.VisualAnchor = targetAnchor;
                dragon.FollowVelocity = Vector2.Zero;
                dragon.FollowActive = false;
                dragon.ActiveVerticalFollowState = 0;
                dragon.ActiveVerticalCheckCount = 0;
                dragon.ActiveFollowReleaseStableFrames = 0;
                return dragon.VisualAnchor;
            }

            UpdateRemoteDragonCompanionFollowState(dragon, targetAnchor);
            if (dragon.FollowActive)
            {
                float nextX = DragonCompanionRuntime.ResolveClientActiveFollowHorizontalStep(
                    dragon.VisualAnchor.X,
                    targetAnchor.X,
                    out double velocityX);
                float nextY = ResolveRemoteDragonCompanionActiveFollowVerticalStep(
                    dragon.VisualAnchor.Y,
                    targetAnchor.Y,
                    dragon,
                    out double velocityY);
                dragon.VisualAnchor = new Vector2(nextX, nextY);
                dragon.FollowVelocity = new Vector2((float)velocityX, (float)velocityY);
                return dragon.VisualAnchor;
            }

            float stepSeconds = RemoteDragonPassiveFollowStepMilliseconds / 1000f;
            Vector2 visualAnchor = dragon.VisualAnchor;
            Vector2 velocity = dragon.FollowVelocity;
            visualAnchor.X = ResolveRemoteDragonPassiveFollowAxis(
                visualAnchor.X,
                targetAnchor.X,
                ref velocity.X,
                stepSeconds,
                RemoteDragonPassiveHorizontalResponse,
                RemoteDragonPassiveMaxHorizontalSpeed,
                CVecCtrl.WalkAcceleration * RemoteDragonPassiveHorizontalForceScale);
            visualAnchor.Y = ResolveRemoteDragonPassiveFollowAxis(
                visualAnchor.Y,
                targetAnchor.Y,
                ref velocity.Y,
                stepSeconds,
                RemoteDragonPassiveVerticalResponse,
                RemoteDragonPassiveMaxVerticalSpeed,
                CVecCtrl.AirDragDeceleration * RemoteDragonPassiveVerticalForceScale);
            dragon.VisualAnchor = visualAnchor;
            dragon.FollowVelocity = velocity;
            return dragon.VisualAnchor;
        }

        private static void EnsureRemoteDragonCompanionLayerReference(RemoteDragonCompanionPresentationState dragon)
        {
            if (dragon == null || dragon.SimulatedLayerHandleRefCount > 0)
            {
                return;
            }

            if (dragon.SimulatedLayerHandleId <= 0)
            {
                dragon.SimulatedLayerHandleId = NextRemoteDragonCompanionLayerHandleId();
            }

            dragon.CaptureRegisteredLayerReference();
        }

        private static void UpdateRemoteDragonCompanionFollowState(
            RemoteDragonCompanionPresentationState dragon,
            Vector2 targetAnchor)
        {
            float horizontalDelta = Math.Abs(targetAnchor.X - dragon.VisualAnchor.X);
            float verticalDelta = Math.Abs(targetAnchor.Y - dragon.VisualAnchor.Y);
            bool hasMomentum = Math.Abs(dragon.FollowVelocity.X) > RemoteDragonFollowMinSpeed
                || Math.Abs(dragon.FollowVelocity.Y) > RemoteDragonFollowMinSpeed;
            bool shouldEngage = hasMomentum
                || horizontalDelta > RemoteDragonActiveFollowDistanceX + RemoteDragonActiveFollowStepX
                || verticalDelta > RemoteDragonActiveFollowVerticalCheckDistance;
            bool shouldHold = hasMomentum
                || horizontalDelta > RemoteDragonActiveFollowDistanceX
                || verticalDelta > RemoteDragonActiveFollowVerticalCheckDistance;

            if (dragon.FollowActive)
            {
                if (shouldHold)
                {
                    dragon.ActiveFollowReleaseStableFrames = 0;
                    return;
                }

                dragon.ActiveFollowReleaseStableFrames++;
                dragon.FollowActive = dragon.ActiveFollowReleaseStableFrames < RemoteDragonActiveFollowReleaseStableFrameCount;
                return;
            }

            dragon.ActiveFollowReleaseStableFrames = 0;
            dragon.FollowActive = shouldEngage;
        }

        private static float ResolveRemoteDragonCompanionActiveFollowVerticalStep(
            float currentY,
            float targetY,
            RemoteDragonCompanionPresentationState dragon,
            out double velocityY)
        {
            float deltaY = targetY - currentY;
            float absoluteDeltaY = Math.Abs(deltaY);
            if (dragon.ActiveVerticalFollowState == 0)
            {
                if (absoluteDeltaY > RemoteDragonActiveFollowVerticalCheckDistance)
                {
                    dragon.ActiveVerticalCheckCount++;
                    if (dragon.ActiveVerticalCheckCount >= RemoteDragonActiveFollowVerticalCheckFrames)
                    {
                        dragon.ActiveVerticalFollowState = 1;
                    }
                }
                else
                {
                    dragon.ActiveVerticalCheckCount = 0;
                }
            }
            else
            {
                dragon.ActiveVerticalCheckCount = 0;
            }

            bool shouldMoveVertically = dragon.ActiveVerticalFollowState != 0
                || Math.Abs(deltaY) > RemoteDragonActiveFollowImmediateVerticalDistance;
            if (!shouldMoveVertically)
            {
                velocityY = 0d;
                return currentY;
            }

            if (dragon.ActiveVerticalFollowState < 0 && currentY == targetY)
            {
                dragon.ActiveVerticalFollowState = 0;
                velocityY = deltaY >= 0f ? 1d : -1d;
                return currentY;
            }

            int verticalStep = Math.Max(1, (int)(MathF.Min(17f, absoluteDeltaY / 10f) + 1f));
            float nextY = deltaY >= 0f
                ? Math.Min(targetY, currentY + verticalStep)
                : Math.Max(targetY, currentY - verticalStep);
            dragon.ActiveVerticalFollowState = nextY == targetY ? -1 : 1;
            velocityY = deltaY >= 0f ? 1d : -1d;
            return nextY;
        }

        internal static Vector2 ResolveRemoteDragonHudVisualAnchorForTesting(
            RemotePreparedSkillState prepared,
            Vector2 targetAnchor,
            int currentTime)
        {
            return ResolveRemoteDragonHudVisualAnchor(prepared, targetAnchor, currentTime);
        }

        private static Vector2 ResolveRemoteDragonHudVisualAnchor(
            RemotePreparedSkillState prepared,
            Vector2 targetAnchor,
            int currentTime)
        {
            if (prepared == null)
            {
                return targetAnchor;
            }

            if (prepared.DragonLastFollowUpdateTime == int.MinValue)
            {
                prepared.DragonVisualAnchor = targetAnchor;
                prepared.DragonFollowVelocity = Vector2.Zero;
                prepared.DragonLastFollowUpdateTime = currentTime;
                return prepared.DragonVisualAnchor;
            }

            int elapsedMs = Math.Max(0, currentTime - prepared.DragonLastFollowUpdateTime);
            prepared.DragonLastFollowUpdateTime = currentTime;
            if (elapsedMs <= 0)
            {
                return prepared.DragonVisualAnchor;
            }

            if (DragonCompanionRuntime.ShouldSnapActiveFollowToTarget(prepared.DragonVisualAnchor, targetAnchor))
            {
                prepared.DragonVisualAnchor = targetAnchor;
                prepared.DragonFollowVelocity = Vector2.Zero;
                prepared.DragonFollowActive = false;
                prepared.DragonActiveVerticalFollowState = 0;
                prepared.DragonActiveVerticalCheckCount = 0;
                prepared.DragonActiveFollowReleaseStableFrames = 0;
                return prepared.DragonVisualAnchor;
            }

            UpdateRemoteDragonHudFollowState(prepared, targetAnchor);
            if (prepared.DragonFollowActive)
            {
                double velocityX;
                double velocityY;
                float nextX = DragonCompanionRuntime.ResolveClientActiveFollowHorizontalStep(
                    prepared.DragonVisualAnchor.X,
                    targetAnchor.X,
                    out velocityX);
                float nextY = ResolveRemoteDragonActiveFollowVerticalStep(
                    prepared.DragonVisualAnchor.Y,
                    targetAnchor.Y,
                    prepared,
                    out velocityY);
                prepared.DragonVisualAnchor = new Vector2(nextX, nextY);
                prepared.DragonFollowVelocity = new Vector2((float)velocityX, (float)velocityY);
                return prepared.DragonVisualAnchor;
            }

            float stepSeconds = RemoteDragonPassiveFollowStepMilliseconds / 1000f;
            Vector2 visualAnchor = prepared.DragonVisualAnchor;
            Vector2 velocity = prepared.DragonFollowVelocity;
            visualAnchor.X = ResolveRemoteDragonPassiveFollowAxis(
                visualAnchor.X,
                targetAnchor.X,
                ref velocity.X,
                stepSeconds,
                RemoteDragonPassiveHorizontalResponse,
                RemoteDragonPassiveMaxHorizontalSpeed,
                CVecCtrl.WalkAcceleration * RemoteDragonPassiveHorizontalForceScale);
            visualAnchor.Y = ResolveRemoteDragonPassiveFollowAxis(
                visualAnchor.Y,
                targetAnchor.Y,
                ref velocity.Y,
                stepSeconds,
                RemoteDragonPassiveVerticalResponse,
                RemoteDragonPassiveMaxVerticalSpeed,
                CVecCtrl.AirDragDeceleration * RemoteDragonPassiveVerticalForceScale);
            prepared.DragonVisualAnchor = visualAnchor;
            prepared.DragonFollowVelocity = velocity;
            return prepared.DragonVisualAnchor;
        }

        private static void UpdateRemoteDragonHudFollowState(RemotePreparedSkillState prepared, Vector2 targetAnchor)
        {
            float horizontalDelta = Math.Abs(targetAnchor.X - prepared.DragonVisualAnchor.X);
            float verticalDelta = Math.Abs(targetAnchor.Y - prepared.DragonVisualAnchor.Y);
            bool hasMomentum = Math.Abs(prepared.DragonFollowVelocity.X) > RemoteDragonFollowMinSpeed
                || Math.Abs(prepared.DragonFollowVelocity.Y) > RemoteDragonFollowMinSpeed;
            bool shouldEngage = hasMomentum
                || horizontalDelta > RemoteDragonActiveFollowDistanceX + RemoteDragonActiveFollowStepX
                || verticalDelta > RemoteDragonActiveFollowVerticalCheckDistance;
            bool shouldHold = hasMomentum
                || horizontalDelta > RemoteDragonActiveFollowDistanceX
                || verticalDelta > RemoteDragonActiveFollowVerticalCheckDistance;

            if (prepared.DragonFollowActive)
            {
                if (shouldHold)
                {
                    prepared.DragonActiveFollowReleaseStableFrames = 0;
                    return;
                }

                prepared.DragonActiveFollowReleaseStableFrames++;
                prepared.DragonFollowActive = prepared.DragonActiveFollowReleaseStableFrames < RemoteDragonActiveFollowReleaseStableFrameCount;
                return;
            }

            prepared.DragonActiveFollowReleaseStableFrames = 0;
            prepared.DragonFollowActive = shouldEngage;
        }

        private static float ResolveRemoteDragonActiveFollowVerticalStep(
            float currentY,
            float targetY,
            RemotePreparedSkillState prepared,
            out double velocityY)
        {
            float deltaY = targetY - currentY;
            float absoluteDeltaY = Math.Abs(deltaY);
            if (prepared.DragonActiveVerticalFollowState == 0)
            {
                if (absoluteDeltaY > RemoteDragonActiveFollowVerticalCheckDistance)
                {
                    prepared.DragonActiveVerticalCheckCount++;
                    if (prepared.DragonActiveVerticalCheckCount >= RemoteDragonActiveFollowVerticalCheckFrames)
                    {
                        prepared.DragonActiveVerticalFollowState = 1;
                    }
                }
                else
                {
                    prepared.DragonActiveVerticalCheckCount = 0;
                }
            }
            else
            {
                prepared.DragonActiveVerticalCheckCount = 0;
            }

            bool shouldMoveVertically = prepared.DragonActiveVerticalFollowState != 0
                || Math.Abs(deltaY) > RemoteDragonActiveFollowImmediateVerticalDistance;
            if (!shouldMoveVertically)
            {
                velocityY = 0d;
                return currentY;
            }

            if (prepared.DragonActiveVerticalFollowState < 0 && currentY == targetY)
            {
                prepared.DragonActiveVerticalFollowState = 0;
                velocityY = deltaY >= 0f ? 1d : -1d;
                return currentY;
            }

            int verticalStep = Math.Max(1, (int)(MathF.Min(17f, absoluteDeltaY / 10f) + 1f));
            float nextY = deltaY >= 0f
                ? Math.Min(targetY, currentY + verticalStep)
                : Math.Max(targetY, currentY - verticalStep);
            prepared.DragonActiveVerticalFollowState = nextY == targetY ? -1 : 1;
            velocityY = deltaY >= 0f ? 1d : -1d;
            return nextY;
        }

        private static float ResolveRemoteDragonPassiveFollowAxis(
            float current,
            float target,
            ref float velocity,
            float deltaSeconds,
            float responseScale,
            float maxSpeed,
            double force)
        {
            float delta = target - current;
            if (Math.Abs(delta) <= RemoteDragonPassiveArrivalDistance)
            {
                velocity = 0f;
                return target;
            }

            if (Math.Abs(delta) <= RemoteDragonPassiveHoldDistance)
            {
                double holdVelocity = velocity;
                CVecCtrl.DecSpeed(ref holdVelocity, Math.Max(force, CVecCtrl.WalkDeceleration), PhysicsConstants.Instance.DefaultMass, 0d, deltaSeconds);
                velocity = (float)holdVelocity;
                return current + velocity * deltaSeconds;
            }

            double axisVelocity = velocity;
            double directedForce = Math.Max(force * Math.Max(0.1f, responseScale), CVecCtrl.WalkAcceleration);
            double directedMaxSpeed = Math.Max(RemoteDragonPassiveArrivalDistance, maxSpeed * Math.Max(0.1f, responseScale));
            bool movingTowardTarget = Math.Sign(delta) == Math.Sign(axisVelocity) || Math.Abs(axisVelocity) <= RemoteDragonFollowMinSpeed;
            if (!movingTowardTarget)
            {
                CVecCtrl.DecSpeed(ref axisVelocity, Math.Max(directedForce, CVecCtrl.WalkDeceleration), PhysicsConstants.Instance.DefaultMass, 0d, deltaSeconds);
            }
            else
            {
                double targetVelocity = Math.Sign(delta) * directedMaxSpeed;
                CVecCtrl.AccSpeed(ref axisVelocity, Math.Abs(directedForce), PhysicsConstants.Instance.DefaultMass, Math.Abs(targetVelocity), deltaSeconds);
                axisVelocity = Math.Sign(delta) * Math.Abs(axisVelocity);
            }

            velocity = (float)axisVelocity;
            return current + velocity * deltaSeconds;
        }

        internal static string ResolveRemoteDragonOwnerPacketActionNameForTesting(string baseActionName, string visibleActionName)
        {
            return ResolveRemoteDragonOwnerPacketActionName(baseActionName, visibleActionName);
        }

        private static string ResolveRemoteDragonOwnerPacketActionName(RemoteUserActor actor)
        {
            return ResolveRemoteDragonOwnerPacketActionName(actor?.BaseActionName, actor?.ActionName);
        }

        private static string ResolveRemoteDragonOwnerPacketActionName(string baseActionName, string visibleActionName)
        {
            return string.IsNullOrWhiteSpace(baseActionName)
                ? visibleActionName
                : baseActionName;
        }

        private static Vector2 ResolveRemoteDragonAnchor(
            RemoteUserActor actor,
            AssembledFrame ownerFrame,
            string ownerActionName,
            int? ownerRawActionCode,
            float ownerBodyOriginY,
            string dragonActionName,
            int dragonActionElapsedMs,
            RemoteDragonHudMetadata metadata)
        {
            float side = actor.FacingRight ? -1f : 1f;
            int originX = metadata.ResolveOriginX(dragonActionName, dragonActionElapsedMs);

            if (UsesRemoteDragonLadderAnchor(ownerActionName, ownerRawActionCode))
            {
                float ladderHorizontalOffset = Math.Max(RemoteDragonLadderSideOffset, originX * 0.45f);
                return new Vector2(
                    actor.Position.X + (side * ladderHorizontalOffset),
                    ownerBodyOriginY + RemoteDragonLadderVerticalOffset);
            }

            float groundHorizontalOffset = Math.Max(RemoteDragonGroundSideOffset, originX * 0.55f);
            if (ownerFrame != null && !ownerFrame.Bounds.IsEmpty)
            {
                Rectangle bounds = ownerFrame.Bounds;
                float anchorX = actor.FacingRight
                    ? actor.Position.X + bounds.Left + side * groundHorizontalOffset
                    : actor.Position.X + bounds.Right + side * groundHorizontalOffset;
                return new Vector2(anchorX, actor.Position.Y + RemoteDragonGroundVerticalOffset);
            }

            return new Vector2(
                actor.Position.X + (side * groundHorizontalOffset),
                ownerBodyOriginY + RemoteDragonGroundVerticalOffset);
        }

        private static bool TryResolveRemoteDragonHudMetadata(int jobId, out RemoteDragonHudMetadata metadata)
        {
            metadata = default;
            int dragonJob = jobId switch
            {
                >= 2200 and <= 2218 => jobId,
                _ => 0
            };
            if (dragonJob == 0)
            {
                return false;
            }

            if (RemoteDragonHudMetadataCache.TryGetValue(dragonJob, out metadata))
            {
                return true;
            }

            WzImage image = FindRemoteDragonHudImage(dragonJob);
            if (image == null)
            {
                return false;
            }

            var actionTimelines = new Dictionary<string, RemoteDragonHudAnimationTimeline>(StringComparer.OrdinalIgnoreCase);
            int standOriginX = 79;
            int moveOriginX = standOriginX;

            foreach (string enumeratedActionName in DragonActionLoader.EnumerateRenderableImageActionNames(image))
            {
                string actionName = DragonActionLoader.NormalizeClientActionName(enumeratedActionName)
                    ?? enumeratedActionName;
                WzSubProperty actionNode = DragonActionLoader.FindActionNode(image, enumeratedActionName);
                if (actionNode == null
                    || !TryReadRemoteDragonFrameMetrics(actionNode, out int originX, out RemoteDragonHudAnimationTimeline timeline))
                {
                    continue;
                }

                if (!actionTimelines.ContainsKey(actionName))
                {
                    actionTimelines[actionName] = timeline;
                }

                if (string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase))
                {
                    standOriginX = originX;
                }
                else if (string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase))
                {
                    moveOriginX = originX;
                }
            }

            if (actionTimelines.Count == 0)
            {
                return false;
            }

            metadata = new RemoteDragonHudMetadata(standOriginX, moveOriginX, actionTimelines);
            RemoteDragonHudMetadataCache[dragonJob] = metadata;
            return true;
        }

        internal static bool TryResolveRemoteDragonHudMetadataForTesting(int jobId, out RemoteDragonHudMetadata metadata)
        {
            return TryResolveRemoteDragonHudMetadata(jobId, out metadata);
        }

        private static WzImage FindRemoteDragonHudImage(int dragonJob)
        {
            if (dragonJob <= 0)
            {
                return null;
            }

            string resolvedImagePath = DragonActionLoader.ResolveDragonImagePath(dragonJob);
            if (!string.IsNullOrWhiteSpace(resolvedImagePath)
                && resolvedImagePath.StartsWith("Skill/", StringComparison.OrdinalIgnoreCase))
            {
                string relativeImagePath = resolvedImagePath.Substring("Skill/".Length);
                WzImage resolvedImage = global::HaCreator.Program.FindImage("Skill", relativeImagePath);
                if (resolvedImage != null)
                {
                    return resolvedImage;
                }
            }

            return global::HaCreator.Program.FindImage("Skill", $"Dragon/{dragonJob}.img")
                ?? global::HaCreator.Program.FindImage("Skill", $"{dragonJob}.img");
        }

        private static bool TryReadRemoteDragonFrameMetrics(
            WzSubProperty actionNode,
            out int originX,
            out RemoteDragonHudAnimationTimeline timeline)
        {
            originX = 0;
            timeline = default;

            List<RemoteDragonHudFrameMetrics> frames = new();
            foreach (WzCanvasProperty frame in actionNode.WzProperties
                         .OfType<WzCanvasProperty>()
                         .OrderBy(static canvas => ParseRemoteDragonFrameIndex(canvas.Name)))
            {
                WzCanvasProperty metadataCanvas = ResolveRemoteDragonMetadataCanvas(frame);
                if (metadataCanvas?["origin"] is not WzVectorProperty origin
                    || metadataCanvas["lt"] is not WzVectorProperty lt
                    || metadataCanvas["rb"] is not WzVectorProperty rb)
                {
                    continue;
                }

                int height = Math.Max(1, rb.Y.Value - lt.Y.Value);
                int delayMs = Math.Max(1, GetWzIntValue(metadataCanvas["delay"]) ?? 100);
                frames.Add(new RemoteDragonHudFrameMetrics(origin.X.Value, height, delayMs));
                if (frames.Count == 1)
                {
                    originX = origin.X.Value;
                }
            }

            if (frames.Count == 0)
            {
                return false;
            }

            timeline = new RemoteDragonHudAnimationTimeline(
                IsRemoteDragonActionLooping(actionNode.Name),
                frames);
            return true;
        }

        private static WzCanvasProperty ResolveRemoteDragonMetadataCanvas(WzCanvasProperty canvas)
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

        internal static bool IsRemoteDragonActionLooping(string actionName)
        {
            string normalizedActionName = DragonActionLoader.NormalizeClientActionName(actionName) ?? actionName;
            return string.Equals(normalizedActionName, "stand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedActionName, "move", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(normalizedActionName)
                    && normalizedActionName.EndsWith("_prepare", StringComparison.OrdinalIgnoreCase));
        }

        private static int? GetWzIntValue(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)longProperty.Value,
                _ => null
            };
        }

        private static int ParseRemoteDragonFrameIndex(string value)
        {
            return int.TryParse(value, out int parsed) ? parsed : int.MaxValue;
        }

        internal static string ResolveRemoteDragonActionName(
            RemotePreparedSkillState prepared,
            bool isHolding,
            string ownerActionName,
            int? ownerRawActionCode,
            RemoteDragonHudMetadata metadata)
        {
            ResolveRemoteDragonActionSelection(
                prepared,
                isHolding,
                ownerActionName,
                ownerRawActionCode,
                metadata,
                out string actionName,
                out _);
            return actionName;
        }

        internal static bool TryResolveRemoteExplicitDragonActionName(
            string ownerActionName,
            int? ownerRawActionCode,
            RemoteDragonHudMetadata metadata,
            out string actionName,
            out bool useOwnerActionTimeline)
        {
            actionName = null;
            useOwnerActionTimeline = false;
            if (metadata.ActionTimelines == null || metadata.ActionTimelines.Count == 0)
            {
                return false;
            }

            if (ownerRawActionCode.HasValue
                && DragonActionLoader.TryGetClientActionNameFromRawActionCode(ownerRawActionCode.Value, out string rawActionName))
            {
                foreach (string candidate in EnumerateRemoteDragonActionCandidates(rawActionName))
                {
                    if (!metadata.HasAction(candidate))
                    {
                        continue;
                    }

                    actionName = candidate;
                    useOwnerActionTimeline = IsExplicitRemoteDragonAction(candidate);
                    return true;
                }

                return false;
            }

            foreach (string candidate in EnumerateRemoteDragonActionCandidates(ownerActionName))
            {
                if (metadata.HasAction(candidate))
                {
                    actionName = candidate;
                    useOwnerActionTimeline = IsExplicitRemoteDragonAction(candidate);
                    return true;
                }
            }

            return false;
        }

        internal static void ResolveRemoteDragonActionSelection(
            RemotePreparedSkillState prepared,
            bool isHolding,
            string ownerActionName,
            int? ownerRawActionCode,
            RemoteDragonHudMetadata metadata,
            out string actionName,
            out bool useOwnerActionTimeline)
        {
            if (TryResolveRemoteExplicitDragonActionName(
                    ownerActionName,
                    ownerRawActionCode,
                    metadata,
                    out string explicitActionName,
                    out bool explicitOwnerTimeline))
            {
                actionName = explicitActionName;
                useOwnerActionTimeline = explicitOwnerTimeline;
                return;
            }

            useOwnerActionTimeline = false;
            if (isHolding)
            {
                if (ShouldUseRemoteDragonMoveAction(ownerActionName, ownerRawActionCode)
                    && metadata.HasAction("move"))
                {
                    actionName = "move";
                    return;
                }

                actionName = "stand";
                return;
            }

            actionName = prepared?.SkillId switch
            {
                22121000 => "icebreathe_prepare",
                22151001 => "breathe_prepare",
                _ => "stand"
            };
        }

        internal static bool ShouldUseRemoteDragonMoveAction(string ownerActionName, int? ownerRawActionCode = null)
        {
            string normalized = ResolveRemoteDragonOwnerActionName(ownerActionName, ownerRawActionCode);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return normalized.Equals("jump", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("ladder", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("rope", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("move", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("walk", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("swim", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("fly", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool UsesRemoteDragonLadderAnchor(string ownerActionName, int? ownerRawActionCode = null)
        {
            string normalized = ResolveRemoteDragonOwnerActionName(ownerActionName, ownerRawActionCode);
            return string.Equals(normalized, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "rope", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveRemoteDragonOwnerActionName(string ownerActionName, int? ownerRawActionCode)
        {
            if (ownerRawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(ownerRawActionCode.Value, out string rawActionName)
                && !string.IsNullOrWhiteSpace(rawActionName))
            {
                return NormalizeActionName(rawActionName, allowSitFallback: false);
            }

            return NormalizeActionName(ownerActionName, allowSitFallback: false);
        }

        private static IEnumerable<string> EnumerateRemoteDragonActionCandidates(string ownerActionName)
        {
            if (string.IsNullOrWhiteSpace(ownerActionName))
            {
                yield break;
            }

            string normalized = NormalizeActionName(ownerActionName, allowSitFallback: false);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            yield return normalized;

            string aliasedActionName = DragonActionLoader.NormalizeClientActionName(normalized);
            if (!string.IsNullOrWhiteSpace(aliasedActionName)
                && !string.Equals(aliasedActionName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                yield return aliasedActionName;
            }
        }

        private static bool IsExplicitRemoteDragonAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                && !string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase);
        }

        internal static int ResolveRemoteDragonActionElapsedMs(
            RemotePreparedSkillState prepared,
            int currentTime,
            string actionName,
            bool isHolding,
            bool useOwnerActionTimeline = false,
            int ownerActionStartTime = int.MinValue)
        {
            if (prepared == null)
            {
                return 0;
            }

            useOwnerActionTimeline = useOwnerActionTimeline
                && ownerActionStartTime != int.MinValue;
            int resolvedActionStartTime = useOwnerActionTimeline
                ? ownerActionStartTime
                : ResolveRemoteDragonPhaseStartTime(prepared, isHolding);

            bool actionChanged = !string.Equals(prepared.DragonActionName, actionName, StringComparison.OrdinalIgnoreCase);
            bool ownerTimelineChanged = useOwnerActionTimeline
                && prepared.DragonOwnerActionStartTime != ownerActionStartTime;
            bool phaseTimelineChanged = !useOwnerActionTimeline
                && prepared.DragonActionStartTime != resolvedActionStartTime;
            if (actionChanged
                || ownerTimelineChanged
                || phaseTimelineChanged
                || prepared.DragonActionStartTime == int.MinValue)
            {
                prepared.DragonActionName = actionName;
                prepared.DragonActionStartTime = resolvedActionStartTime;
                prepared.DragonOwnerActionStartTime = useOwnerActionTimeline
                    ? ownerActionStartTime
                    : int.MinValue;
            }

            return Math.Max(0, currentTime - prepared.DragonActionStartTime);
        }

        private static int ResolveRemoteDragonPhaseStartTime(RemotePreparedSkillState prepared, bool isHolding)
        {
            if (prepared == null)
            {
                return 0;
            }

            return isHolding
                ? prepared.StartTime + Math.Max(0, prepared.PrepareDurationMs)
                : prepared.StartTime;
        }

        public IReadOnlyList<MinimapUI.TrackedUserMarker> BuildHelperMarkers()
        {
            _helperMarkerCount = 0;
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                bool hasFriendshipOverlay = actor.RelationshipOverlays.ContainsKey(RemoteRelationshipOverlayType.Friendship);
                bool hasCoupleOverlay = actor.RelationshipOverlays.ContainsKey(RemoteRelationshipOverlayType.Couple);
                bool hasMarriageOverlay = actor.RelationshipOverlays.ContainsKey(RemoteRelationshipOverlayType.Marriage);
                bool hasNewYearCardOverlay = actor.RelationshipOverlays.ContainsKey(RemoteRelationshipOverlayType.NewYearCard);
                bool hasRelationshipHelperFallback = hasFriendshipOverlay || hasCoupleOverlay || hasMarriageOverlay || hasNewYearCardOverlay;
                if (!ShouldIncludePacketAuthoredMinimapHelper(
                        actor?.IsVisibleInWorld == true,
                        actor?.HiddenLikeClient == true,
                        actor?.HelperMarkerType.HasValue == true,
                        actor?.HasPacketAuthoredHelperState == true,
                        hasRelationshipHelperFallback,
                        actor?.BattlefieldTeamId))
                {
                    continue;
                }

                MinimapUI.HelperMarkerType markerType = ResolvePacketAuthoredMinimapHelperMarker(
                    actor.HelperMarkerType,
                    hasFriendshipOverlay,
                    hasCoupleOverlay,
                    hasMarriageOverlay,
                    hasNewYearCardOverlay,
                    actor.BattlefieldTeamId);

                MinimapUI.TrackedUserMarker marker = GetOrCreateHelperMarker(_helperMarkerCount++);
                marker.WorldX = actor.Position.X;
                marker.WorldY = actor.Position.Y;
                marker.MarkerType = markerType;
                marker.ShowDirectionOverlay = ResolvePacketAuthoredMinimapShowDirectionOverlay(
                    actor.ShowDirectionOverlay,
                    actor.BattlefieldTeamId);
                marker.TooltipText = actor.Name;
            }

            return _helperMarkerBuffer;
        }

        internal static bool ShouldIncludePacketAuthoredMinimapHelper(
            bool isVisibleInWorld,
            bool hiddenLikeClient,
            bool hasExplicitHelperMarker,
            bool hasPacketAuthoredHelperState,
            bool hasRelationshipHelperFallback,
            int? battlefieldTeamId)
        {
            return isVisibleInWorld
                && !hiddenLikeClient
                && (hasExplicitHelperMarker
                    || battlefieldTeamId.HasValue
                    || (!hasPacketAuthoredHelperState && hasRelationshipHelperFallback));
        }

        internal static MinimapUI.HelperMarkerType ResolvePacketAuthoredMinimapHelperMarker(
            MinimapUI.HelperMarkerType? explicitHelperMarkerType,
            bool hasFriendshipOverlay,
            bool hasCoupleOverlay,
            bool hasMarriageOverlay,
            bool hasNewYearCardOverlay,
            int? battlefieldTeamId)
        {
            if (battlefieldTeamId.HasValue)
            {
                return MinimapUI.HelperMarkerType.Match;
            }

            if (explicitHelperMarkerType.HasValue)
            {
                return explicitHelperMarkerType.Value;
            }

            return hasFriendshipOverlay || hasCoupleOverlay || hasMarriageOverlay || hasNewYearCardOverlay
                ? MinimapUI.HelperMarkerType.Friend
                : MinimapUI.HelperMarkerType.Another;
        }

        internal static bool ResolvePacketAuthoredMinimapShowDirectionOverlay(
            bool explicitHelperShowDirectionOverlay,
            int? battlefieldTeamId)
        {
            if (battlefieldTeamId.HasValue)
            {
                return true;
            }

            return explicitHelperShowDirectionOverlay;
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            SpriteFont font,
            PlayerCharacter localPlayer = null,
            StatusBarUI statusBarUi = null)
        {
            List<RemoteUserActor> visibleActors = BuildVisibleWorldActorBuffer();
            _renderedCouplePairsBuffer.Clear();
            _renderedItemEffectPairsBuffer.Clear();

            for (int i = 0; i < visibleActors.Count; i++)
            {
                RemoteUserActor actor = visibleActors[i];
                AssembledFrame frame = actor.GetFrameAtTimeForRendering(tickCount);
                if (frame == null)
                {
                    continue;
                }

                int screenX = (int)Math.Round(actor.Position.X) - mapShiftX + centerX;
                int screenY = (int)Math.Round(actor.Position.Y) - mapShiftY + centerY;
                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    drawFrontLayers: false,
                    _renderedCouplePairsBuffer);
                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawMeleeAfterImage(spriteBatch, skeletonMeshRenderer, actor, screenX, screenY, tickCount);
                DrawCarryItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawCompletedSetItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawRemoteActiveEffectItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawTransientItemEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount);
                DrawRemoteActiveEffectMotionBlur(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount);
                DrawRemoteDragonCompanion(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount);
                DrawRemoteActorFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    frame,
                    screenX,
                    screenY,
                    tickCount);
                DrawRelationshipOverlays(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    font,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    screenX,
                    screenY,
                    tickCount,
                    _renderedItemEffectPairsBuffer);
                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    drawFrontLayers: true,
                    _renderedCouplePairsBuffer);
                DrawCarryItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawCompletedSetItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawRemoteActiveEffectItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawRemoteTemporaryStatAvatarEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    frame,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawRemoteTransientSkillUseAvatarEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    frame,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawRemotePacketOwnedEmotionEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount);

                float topY = screenY - frame.FeetOffset + frame.Bounds.Top;
                DrawReceiveHpGauge(spriteBatch, actor, screenX, topY);

                if (font == null)
                {
                    continue;
                }

                IReadOnlyList<string> labelLines = BuildWorldActorLabelLines(actor);
                if (labelLines.Count == 0)
                {
                    continue;
                }

                float labelTopY = topY - ((labelLines.Count * font.LineSpacing) + ((labelLines.Count - 1) * 2f)) - 10f;
                if (actor.PartyHpGaugePos.HasValue)
                {
                    labelTopY -= RemoteReceiveHpGaugeCanvasHeight + RemoteReceiveHpGaugeVerticalPadding;
                }
                for (int lineIndex = 0; lineIndex < labelLines.Count; lineIndex++)
                {
                    string line = labelLines[lineIndex];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    Vector2 textSize = font.MeasureString(line);
                    Texture2D guildMarkBackground = null;
                    Texture2D guildMark = null;
                    bool drawGuildMark = lineIndex == 0
                        && !string.IsNullOrWhiteSpace(actor.Build?.GuildName)
                        && TryResolveGuildMarkTextures(spriteBatch.GraphicsDevice, actor, out guildMarkBackground, out guildMark);
                    float guildMarkWidth = drawGuildMark
                        ? Math.Max(guildMarkBackground?.Width ?? 0, guildMark?.Width ?? 0)
                        : 0f;
                    float totalWidth = textSize.X + (drawGuildMark ? guildMarkWidth + 4f : 0f);
                    Vector2 textPosition = new(
                        screenX - (totalWidth / 2f) + (drawGuildMark ? guildMarkWidth + 4f : 0f),
                        labelTopY + (lineIndex * (font.LineSpacing + 2f)));
                    Color textColor = lineIndex == labelLines.Count - 1
                        ? ResolveNameColor(actor)
                        : new Color(176, 226, 255);
                    if (drawGuildMark)
                    {
                        float iconLeft = textPosition.X - guildMarkWidth - 4f;
                        DrawGuildMarkIcon(
                            spriteBatch,
                            guildMarkBackground,
                            guildMark,
                            new Vector2(iconLeft, textPosition.Y + ((font.LineSpacing - Math.Max(guildMarkBackground?.Height ?? 0, guildMark?.Height ?? 0)) * 0.5f)));
                    }

                    DrawOutlinedText(spriteBatch, font, line, textPosition, Color.Black, textColor);
                }
            }
        }

        private void DrawReceiveHpGauge(SpriteBatch spriteBatch, RemoteUserActor actor, int screenX, float topY)
        {
            if (spriteBatch?.GraphicsDevice == null
                || actor?.PartyHpGaugePos is not int gaugePos)
            {
                return;
            }

            Texture2D pixelTexture = GetPixelTexture(spriteBatch.GraphicsDevice);
            if (pixelTexture == null)
            {
                return;
            }

            int left = screenX - (RemoteReceiveHpGaugeCanvasWidth / 2);
            int top = (int)Math.Round(topY) - RemoteReceiveHpGaugeCanvasHeight - RemoteReceiveHpGaugeVerticalPadding;
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle(left, top, RemoteReceiveHpGaugeCanvasWidth, RemoteReceiveHpGaugeCanvasHeight),
                Color.Black);
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle(left + 1, top + 1, RemoteReceiveHpGaugeCanvasWidth - 2, RemoteReceiveHpGaugeCanvasHeight - 2),
                Color.White);
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle(left + 2, top + 2, RemoteReceiveHpGaugeCanvasWidth - 4, RemoteReceiveHpGaugeCanvasHeight - 4),
                Color.Black);

            int fillWidth = ClampRemoteReceiveHpGaugeFillWidthForParity(gaugePos);
            if (fillWidth > 0)
            {
                spriteBatch.Draw(
                    pixelTexture,
                    new Rectangle(
                        left + RemoteReceiveHpGaugeFillLeft,
                        top + RemoteReceiveHpGaugeFillTop,
                        fillWidth,
                        RemoteReceiveHpGaugeFillHeight),
                    Color.Red);
                spriteBatch.Draw(
                    pixelTexture,
                    new Rectangle(
                        left + RemoteReceiveHpGaugeFillLeft,
                        top + RemoteReceiveHpGaugeFillAccentTop,
                        fillWidth,
                        1),
                    new Color(160, 0, 0));
            }
        }

        private void DrawRemoteDragonCompanion(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (spriteBatch?.GraphicsDevice == null
                || !TryResolveRemoteDragonCompanionRenderStateForParity(actor, currentTime, out RemoteDragonCompanionRenderState state))
            {
                return;
            }

            DragonActionLoader loader = GetOrCreateRemoteDragonActionLoader(spriteBatch.GraphicsDevice);
            SkillAnimation animation = loader?.GetOrLoadAnimation(state.JobId, state.ActionName);
            if (animation?.TryGetFrameAtTime(state.ActionElapsedMs, out SkillFrame frame, out int frameElapsedMs) != true
                || frame?.Texture == null)
            {
                return;
            }

            float frameAlpha = ResolveFrameAlpha(frame, frameElapsedMs);
            if (frameAlpha <= 0.01f)
            {
                return;
            }

            int screenX = (int)Math.Round(state.VisualAnchor.X) - mapShiftX + centerX;
            int screenY = (int)Math.Round(state.VisualAnchor.Y) - mapShiftY + centerY;
            Point drawPosition = ResolveRemoteDragonCompanionFrameDrawPositionForParity(
                screenX,
                screenY,
                frame.Origin,
                frame.Bounds.Width,
                state.FacingRight);
            frame.Texture.DrawBackground(
                spriteBatch,
                skeletonMeshRenderer,
                null,
                drawPosition.X,
                drawPosition.Y,
                Color.White * frameAlpha,
                !state.FacingRight,
                null);
        }

        internal static Point ResolveRemoteDragonCompanionFrameDrawPositionForParity(
            int anchorScreenX,
            int anchorScreenY,
            Point frameOrigin,
            int frameBoundsWidth,
            bool facingRight)
        {
            int drawX = facingRight
                ? anchorScreenX - frameOrigin.X
                : anchorScreenX + frameOrigin.X - Math.Max(1, frameBoundsWidth);
            int drawY = anchorScreenY - frameOrigin.Y;
            return new Point(drawX, drawY);
        }

        private DragonActionLoader GetOrCreateRemoteDragonActionLoader(GraphicsDevice device)
        {
            if (device == null)
            {
                return null;
            }

            if (_remoteDragonActionLoader == null || !ReferenceEquals(_remoteDragonActionLoaderDevice, device))
            {
                _remoteDragonActionLoader = new DragonActionLoader(device);
                _remoteDragonActionLoaderDevice = device;
            }

            return _remoteDragonActionLoader;
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

        private Texture2D GetPixelTexture(GraphicsDevice device)
        {
            if (device == null)
            {
                return null;
            }

            if (_pixelTexture == null || _pixelTexture.IsDisposed || !ReferenceEquals(_pixelTextureDevice, device))
            {
                _pixelTexture = new Texture2D(device, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
                _pixelTextureDevice = device;
            }

            return _pixelTexture;
        }

        private List<RemoteUserActor> BuildVisibleWorldActorBuffer()
        {
            _visibleWorldActorsBuffer.Clear();
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.IsVisibleInWorld)
                {
                    _visibleWorldActorsBuffer.Add(actor);
                }
            }

            if (_visibleWorldActorsBuffer.Count > 1)
            {
                _visibleWorldActorsBuffer.Sort(VisibleWorldActorComparer);
            }

            return _visibleWorldActorsBuffer;
        }

        private StatusBarPreparedSkillRenderData GetOrCreatePreparedSkillWorldOverlay(int index)
        {
            while (_preparedSkillWorldOverlayBuffer.Count <= index)
            {
                _preparedSkillWorldOverlayBuffer.Add(new StatusBarPreparedSkillRenderData());
            }

            return _preparedSkillWorldOverlayBuffer[index];
        }

        private MinimapUI.TrackedUserMarker GetOrCreateHelperMarker(int index)
        {
            while (_helperMarkerBuffer.Count <= index)
            {
                _helperMarkerBuffer.Add(new MinimapUI.TrackedUserMarker());
            }

            return _helperMarkerBuffer[index];
        }

        private void DrawPortableChairCoupleMidpointEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime,
            bool drawFrontLayers,
            ISet<(int LeftId, int RightId)> renderedPairs)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleMidpointLayers == null
                || chair.CoupleMidpointLayers.Count == 0)
            {
                return;
            }

            if (TryResolvePortableChairPairWithLocalPlayer(actor, chair, localPlayer, out _, out _))
            {
                return;
            }

            RemoteUserActor partnerActor = FindPortableChairPairActor(
                chair,
                actor.CharacterId,
                actor.FacingRight,
                actor.Position.X,
                actor.Position.Y,
                skipCharacterId: actor.CharacterId,
                preferVisibleOnly: true);
            if (partnerActor == null)
            {
                return;
            }

            var pairKey = actor.CharacterId < partnerActor.CharacterId
                ? (actor.CharacterId, partnerActor.CharacterId)
                : (partnerActor.CharacterId, actor.CharacterId);
            if (drawFrontLayers)
            {
                if (!renderedPairs.Add(pairKey))
                {
                    return;
                }
            }
            else if (renderedPairs.Contains(pairKey))
            {
                return;
            }

            int midpointScreenX = (int)Math.Round((actor.Position.X + partnerActor.Position.X) * 0.5f) - mapShiftX + centerX;
            int midpointScreenY = (int)Math.Round((actor.Position.Y + partnerActor.Position.Y) * 0.5f)
                - mapShiftY
                + centerY
                + PlayerCharacter.PortableChairCoupleMidpointScreenYOffset;
            int animationTime = currentTime;
            for (int i = 0; i < chair.CoupleMidpointLayers.Count; i++)
            {
                PortableChairLayer layer = chair.CoupleMidpointLayers[i];
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame layerFrame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, animationTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    layerFrame,
                    midpointScreenX,
                    midpointScreenY,
                    actor.FacingRight);
            }
        }

        private void DrawPortableChairCoupleSharedLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleSharedLayers == null
                || chair.CoupleSharedLayers.Count == 0)
            {
                return;
            }

            bool hasPair = TryResolvePortableChairPairWithLocalPlayer(actor, chair, localPlayer, out _, out _);
            if (!hasPair)
            {
                hasPair = FindPortableChairPairActor(
                              chair,
                              actor.CharacterId,
                              actor.FacingRight,
                              actor.Position.X,
                              actor.Position.Y,
                              skipCharacterId: actor.CharacterId,
                              preferVisibleOnly: true) != null;
            }

            if (!hasPair)
            {
                return;
            }

            PlayerCharacter.DrawPortableChairLayers(
                spriteBatch,
                skeletonMeshRenderer,
                chair.CoupleSharedLayers,
                screenX,
                screenY,
                actor.FacingRight,
                currentTime,
                drawFrontLayers);
        }

        private void DrawRelationshipOverlays(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            SpriteFont font,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int screenX,
            int screenY,
            int currentTime,
            ISet<(RemoteRelationshipOverlayType Type, int ItemId, int LeftId, int RightId)> renderedPairs)
        {
            if (actor?.RelationshipOverlays == null || actor.RelationshipOverlays.Count == 0)
            {
                return;
            }

            foreach (RemoteRelationshipOverlayState overlay in actor.RelationshipOverlays.Values.OrderBy(static value => value.RelationshipType))
            {
                DrawRelationshipOverlay(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    overlay,
                    font,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    screenX,
                    screenY,
                    currentTime,
                    renderedPairs);
            }
        }

        private void DrawCompletedSetItemEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (_loader == null
                || actor?.CompletedSetItemId <= 0)
            {
                return;
            }

            ItemEffectAnimationSet effect = _loader.LoadCompletedSetItemEffectAnimationSet(actor.CompletedSetItemId);
            if (effect?.OwnerLayers == null || effect.OwnerLayers.Count == 0)
            {
                return;
            }

            foreach (PortableChairLayer layer in effect.OwnerLayers)
            {
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                int animationTime = actor.CompletedSetItemEffectAppliedTime != int.MinValue
                    ? ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.CompletedSetItemEffectAppliedTime)
                    : currentTime;
                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, animationTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    frame,
                    screenX,
                    screenY,
                actor.FacingRight);
            }
        }

        private static void DrawRemoteActiveEffectItemEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            RemoteActiveEffectItemEffectState state = actor?.ActiveEffectItemEffect;
            if (!ShouldDrawRemoteActiveEffectItemEffectForParity(
                    state,
                    actor?.HiddenLikeClient == true,
                    actor?.HasMorphTemplate == true))
            {
                return;
            }

            Vector2 drawPosition = ResolveRemoteActiveEffectItemEffectDrawPositionForParity(state, actor.Position);
            int drawScreenX = screenX + (int)(drawPosition.X - actor.Position.X);
            int drawScreenY = screenY + (int)(drawPosition.Y - actor.Position.Y);
            int elapsedTime = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.AnimationStartTime);
            for (int i = 0; i < state.Effect.OwnerLayers.Count; i++)
            {
                PortableChairLayer layer = state.Effect.OwnerLayers[i];
                if (layer == null || (layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, elapsedTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    frame,
                    drawScreenX,
                    drawScreenY,
                    ResolveRemoteActiveEffectItemEffectFacingForParity(state, actor.FacingRight));
            }
        }

        private void DrawCarryItemEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (_loader == null
                || actor?.CarryItemEffectId is not int carryCount
                || carryCount <= 0)
            {
                return;
            }

            if (ShouldSuppressCarryItemEffectForParity(actor))
            {
                return;
            }

            CarryItemEffectDefinition effect = _loader.LoadCarryItemEffectDefinition();
            if (effect?.IsReady != true)
            {
                return;
            }

            (int totalTokenCount, int tensTokenCount) = ResolveCarryItemEffectTokenCounts(carryCount);
            if (totalTokenCount <= 0)
            {
                return;
            }

            for (int index = 0; index < totalTokenCount; index++)
            {
                PortableChairLayer layer = ResolveCarryItemEffectLayer(effect, index, tensTokenCount);
                if (layer?.Animation == null)
                {
                    continue;
                }

                Point offset = ResolveCarryItemEffectOffset(
                    index,
                    totalTokenCount,
                    tensTokenCount,
                    actor.FacingRight,
                    out bool isFrontLayer);
                if (isFrontLayer != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(
                    layer,
                    ResolveCarryItemEffectAnimationTime(currentTime, index));
                float rotationRadians = ResolveCarryItemEffectRotationRadiansForParity(
                    actor.CharacterId,
                    carryCount,
                    index,
                    actor.CarryItemEffectAppliedTime,
                    currentTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    frame,
                    screenX + offset.X,
                    screenY + offset.Y,
                    actor.FacingRight,
                    rotationRadians);
            }
        }

        internal static bool ShouldSuppressCarryItemEffectForParity(RemoteUserActor actor)
        {
            return actor == null
                || ShouldSuppressCarryItemEffectForParity(actor.HasMorphTemplate, actor.ActionName);
        }

        internal static bool ShouldSuppressCarryItemEffectForParity(
            bool hasMorphTemplate,
            string actionName)
        {
            return hasMorphTemplate
                || IsRelationshipOverlayGhostAction(actionName);
        }

        private static void UpdateCarryItemEffectLayerAdmissionForParity(RemoteUserActor actor)
        {
            if (actor == null || !actor.CarryItemEffectId.HasValue || actor.CarryItemEffectLayerHandleId <= 0)
            {
                return;
            }

            if (ShouldSuppressCarryItemEffectForParity(actor))
            {
                actor.CarryItemEffectLayerHandleRefCount = 0;
                actor.CarryItemEffectLayerSuppressed = true;
                return;
            }

            actor.CarryItemEffectLayerHandleRefCount = 1;
            actor.CarryItemEffectLayerSuppressed = false;
        }

        private static void ReleaseCarryItemEffectLayerReferenceForParity(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.CarryItemEffectLayerHandleRefCount = 0;
            actor.CarryItemEffectLayerSuppressed = true;
        }

        private static void UpdateCompletedSetItemEffectLayerAdmissionForParity(RemoteUserActor actor)
        {
            if (actor == null || actor.CompletedSetItemId <= 0 || actor.CompletedSetItemEffectLayerHandleId <= 0)
            {
                return;
            }

            actor.CompletedSetItemEffectLayerHandleRefCount = 1;
        }

        private static void ReleaseCompletedSetItemEffectLayerReferenceForParity(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.CompletedSetItemEffectLayerHandleRefCount = 0;
        }

        private void DrawRelationshipOverlay(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            RemoteRelationshipOverlayState overlay,
            SpriteFont font,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int screenX,
            int screenY,
            int currentTime,
            ISet<(RemoteRelationshipOverlayType Type, int ItemId, int LeftId, int RightId)> renderedPairs)
        {
            if (actor == null || overlay == null)
            {
                return;
            }

            int elapsedTime = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, overlay.StartTime);
            bool usesClientRelationshipAdmission = UsesClientRelationshipAdmission(overlay.RelationshipType);
            int relationshipStatus = usesClientRelationshipAdmission
                ? 0
                : 1;
            int partnerCharacterId = 0;
            Vector2 partnerPosition = Vector2.Zero;
            bool partnerFacingRight = actor.FacingRight;
            if (TryResolveRelationshipOverlayPair(
                    actor,
                    overlay,
                    localPlayer,
                    out int resolvedPartnerCharacterId,
                    out Vector2 resolvedPartnerPosition,
                    out bool resolvedPartnerFacingRight))
            {
                partnerCharacterId = resolvedPartnerCharacterId;
                partnerPosition = resolvedPartnerPosition;
                partnerFacingRight = resolvedPartnerFacingRight;
                if (usesClientRelationshipAdmission)
                {
                    relationshipStatus = ResolveRelationshipOverlayStatus(
                        actor.Position,
                        partnerPosition,
                        overlay.RelationshipType,
                        IsRelationshipOverlaySuppressed(actor),
                        IsRelationshipOverlayPartnerSuppressed(localPlayer, partnerCharacterId, actor.CharacterId));
                }
            }
            else if (usesClientRelationshipAdmission)
            {
                relationshipStatus = 0;
            }

            if ((relationshipStatus & 1) != 0 && overlay.Effect != null)
            {
                DrawItemEffectLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    overlay.Effect.OwnerLayers,
                    screenX,
                    screenY,
                    actor.FacingRight,
                    elapsedTime);
            }

            bool canDrawSharedRelationshipSurface = partnerCharacterId > 0 && (relationshipStatus & 2) != 0;
            bool hasSharedLayers = overlay.Effect?.SharedLayers != null && overlay.Effect.SharedLayers.Count > 0;
            if (!canDrawSharedRelationshipSurface || !hasSharedLayers)
            {
                return;
            }

            int leftId = Math.Min(actor.CharacterId, partnerCharacterId);
            int rightId = Math.Max(actor.CharacterId, partnerCharacterId);
            if (!renderedPairs.Add((overlay.RelationshipType, overlay.ItemId, leftId, rightId)))
            {
                return;
            }

            int midpointScreenX = (int)Math.Round((actor.Position.X + partnerPosition.X) * 0.5f) - mapShiftX + centerX;
            int midpointScreenY = (int)Math.Round((actor.Position.Y + partnerPosition.Y) * 0.5f) - mapShiftY + centerY
                + PlayerCharacter.PortableChairCoupleMidpointScreenYOffset;

            DrawItemEffectLayers(
                spriteBatch,
                skeletonMeshRenderer,
                overlay.Effect.SharedLayers,
                midpointScreenX,
                midpointScreenY,
                partnerFacingRight,
                elapsedTime);
        }

        private static void DrawItemEffectLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            IEnumerable<PortableChairLayer> layers,
            int screenX,
            int screenY,
            bool facingRight,
            int elapsedTime)
        {
            if (layers == null)
            {
                return;
            }

            foreach (PortableChairLayer layer in layers)
            {
                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, elapsedTime);
                PlayerCharacter.DrawPortableChairLayerFrame(spriteBatch, skeletonMeshRenderer, frame, screenX, screenY, facingRight);
            }
        }

        private static void DrawTransientItemEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime)
        {
            if (actor?.TransientItemEffects == null
                || actor.TransientItemEffects.Count == 0
                || actor.HiddenLikeClient)
            {
                return;
            }

            for (int i = 0; i < actor.TransientItemEffects.Count; i++)
            {
                RemoteTransientItemEffectState state = actor.TransientItemEffects[i];
                if (state?.Effect?.OwnerLayers == null)
                {
                    continue;
                }

                int elapsedTime = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.AnimationStartTime);
                DrawItemEffectLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    state.Effect.OwnerLayers,
                    screenX,
                    screenY,
                    ResolveTransientItemEffectFacingForParity(state, actor),
                    elapsedTime);
            }
        }

        internal static bool ResolveTransientItemEffectFacingForParity(
            RemoteTransientItemEffectState state,
            RemoteUserActor actor)
        {
            return state?.OwnerFacingRight ?? actor?.FacingRight ?? true;
        }

        public static int ResolveRelationshipOverlayStatus(
            Vector2 ownerPosition,
            Vector2 partnerPosition,
            RemoteRelationshipOverlayType relationshipType,
            bool ownerSuppressed,
            bool partnerSuppressed)
        {
            if (ownerSuppressed || partnerSuppressed)
            {
                return 0;
            }

            (int nearRangeX, int nearRangeY) = relationshipType switch
            {
                RemoteRelationshipOverlayType.NewYearCard => (NewYearCardOverlayNearRangeX, NewYearCardOverlayNearRangeY),
                _ => (RelationshipOverlayNearRangeX, RelationshipOverlayNearRangeY)
            };

            int deltaX = Math.Abs((int)Math.Round(ownerPosition.X - partnerPosition.X));
            int deltaY = Math.Abs((int)Math.Round(ownerPosition.Y - partnerPosition.Y));
            int status = 0;
            if (deltaX < RelationshipOverlayVisibleRangeX && deltaY < RelationshipOverlayVisibleRangeY)
            {
                status = 1;
            }

            if (deltaX < nearRangeX && deltaY < nearRangeY)
            {
                status |= 2;
            }

            return status;
        }

        internal static (int TotalTokenCount, int TensTokenCount) ResolveCarryItemEffectTokenCounts(int carryCount)
        {
            int clampedCount = Math.Clamp(carryCount, 0, CarryItemEffectMaximumCount);
            int tensTokenCount = clampedCount / 10;
            int totalTokenCount = tensTokenCount + (clampedCount % 10);
            return (totalTokenCount, tensTokenCount);
        }

        internal static Point ResolveCarryItemEffectOffset(
            int index,
            int totalTokenCount,
            int tensTokenCount,
            bool facingRight,
            out bool isFrontLayer)
        {
            int clampedTotalTokenCount = Math.Max(0, totalTokenCount);
            int clampedTensTokenCount = Math.Clamp(tensTokenCount, 0, clampedTotalTokenCount);
            int singleTokenCount = Math.Max(0, clampedTotalTokenCount - clampedTensTokenCount);
            int x;
            int y;

            if (index >= singleTokenCount)
            {
                int bundleIndex = index - singleTokenCount;
                int bundleColumn = Math.Max(0, bundleIndex % 5);
                int bundleRow = Math.Max(0, bundleIndex / 5);
                int bundleCountInFrontRow = clampedTensTokenCount % 5;
                if (bundleCountInFrontRow == 0 && clampedTensTokenCount > 0)
                {
                    bundleCountInFrontRow = 5;
                }

                x = (15 * bundleColumn) + (7 * (1 - bundleCountInFrontRow));
                y = (-15 * bundleRow) - (10 * Math.Max(0, (singleTokenCount - 1) / 5)) - 35;
            }
            else if (singleTokenCount <= 5)
            {
                x = 10 * ((index % 5) - 2);
                y = 10 * (-2 - (index / 5));
            }
            else if ((index % 10) >= 5)
            {
                int row = index / 5;
                x = 10 * index - (5 * singleTokenCount) - 20;
                y = 10 * (-2 - row);
            }
            else
            {
                x = 10 * ((index % 5) - 2);
                y = 10 * (-2 - (index / 5));
            }

            if (!facingRight)
            {
                x = -x;
            }

            // CUser::SetCarryItemEffect attaches these layers as a dedicated additional
            // overlay owner rather than splitting them around the avatar draw.
            isFrontLayer = true;
            return new Point(x, y);
        }

        internal static int ResolveCarryItemEffectAnimationTime(int currentTime, int index)
        {
            return currentTime + (index * CarryItemEffectAnimationOffsetMs);
        }

        internal static int ResolveCarryItemEffectInitialRotationDegreesForParity(
            int characterId,
            int carryCount,
            int index)
        {
            unchecked
            {
                uint state = (uint)characterId;
                state = (state * 16777619u) ^ (uint)Math.Max(0, carryCount);
                state = (state * 16777619u) ^ (uint)Math.Max(0, index);
                state ^= 0x9E3779B9u;
                return (int)(state % CarryItemEffectRandomRotationRangeDegrees);
            }
        }

        internal static int ResolveCarryItemEffectApplyElapsedMsForParity(int applyTime, int currentTime)
        {
            if (applyTime == int.MinValue)
            {
                return CarryItemEffectRotationResetDurationMs;
            }

            return ResolveRemoteTemporaryStatTickElapsedMs(currentTime, applyTime);
        }

        internal static float ResolveCarryItemEffectRotationRadiansForParity(
            int characterId,
            int carryCount,
            int index,
            int applyTime,
            int currentTime)
        {
            int initialDegrees = ResolveCarryItemEffectInitialRotationDegreesForParity(characterId, carryCount, index);
            if (initialDegrees <= 0)
            {
                return 0f;
            }

            int elapsed = ResolveCarryItemEffectApplyElapsedMsForParity(applyTime, currentTime);
            if (elapsed >= CarryItemEffectRotationResetDurationMs)
            {
                return 0f;
            }

            float remaining = 1f - (elapsed / (float)CarryItemEffectRotationResetDurationMs);
            return MathHelper.ToRadians(initialDegrees * remaining);
        }

        private bool TryResolveRelationshipOverlayPair(
            RemoteUserActor ownerActor,
            RemoteRelationshipOverlayState overlay,
            PlayerCharacter localPlayer,
            out int partnerCharacterId,
            out Vector2 partnerPosition,
            out bool partnerFacingRight)
        {
            partnerCharacterId = 0;
            partnerPosition = Vector2.Zero;
            partnerFacingRight = ownerActor?.FacingRight ?? true;
            if (ownerActor == null || overlay == null)
            {
                return false;
            }

            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            if (overlay.PairCharacterId.HasValue
                && localCharacterId > 0
                && overlay.PairCharacterId.Value == localCharacterId
                && localPlayer?.IsAlive == true
                && (!UsesClientRelationshipAdmission(overlay.RelationshipType)
                    || !IsRelationshipOverlaySuppressed(localPlayer, localCharacterId, ownerActor.CharacterId)))
            {
                partnerCharacterId = localCharacterId;
                partnerPosition = localPlayer.Position;
                partnerFacingRight = localPlayer.FacingRight;
                return true;
            }

            if (overlay.PairCharacterId.HasValue
                && overlay.PairCharacterId.Value > 0
                && overlay.PairCharacterId.Value != ownerActor.CharacterId
                && _actorsById.TryGetValue(overlay.PairCharacterId.Value, out RemoteUserActor explicitPartner)
                && explicitPartner.IsVisibleInWorld
                && (!UsesClientRelationshipAdmission(overlay.RelationshipType)
                    || !IsRelationshipOverlaySuppressed(explicitPartner)))
            {
                partnerCharacterId = explicitPartner.CharacterId;
                partnerPosition = explicitPartner.Position;
                partnerFacingRight = explicitPartner.FacingRight;
                return true;
            }

            RemoteUserActor fallbackPartner = _actorsById.Values
                .Where(candidate => candidate.IsVisibleInWorld
                    && candidate.CharacterId != ownerActor.CharacterId
                    && (!UsesClientRelationshipAdmission(overlay.RelationshipType)
                        || !IsRelationshipOverlaySuppressed(candidate))
                    && candidate.RelationshipOverlays.TryGetValue(overlay.RelationshipType, out RemoteRelationshipOverlayState candidateOverlay)
                    && DoesRelationshipOverlayStateMatch(
                        ownerActor.CharacterId,
                        overlay,
                        candidate.CharacterId,
                        candidateOverlay))
                .OrderBy(candidate =>
                {
                    candidate.RelationshipOverlays.TryGetValue(overlay.RelationshipType, out RemoteRelationshipOverlayState candidateOverlay);
                    return GetRelationshipOverlayPairCandidatePriority(
                        ownerActor.CharacterId,
                        overlay,
                        candidate.CharacterId,
                        candidateOverlay);
                })
                .ThenBy(candidate => Vector2.DistanceSquared(candidate.Position, ownerActor.Position))
                .FirstOrDefault();
            if (fallbackPartner == null)
            {
                return false;
            }

            partnerCharacterId = fallbackPartner.CharacterId;
            partnerPosition = fallbackPartner.Position;
            partnerFacingRight = fallbackPartner.FacingRight;
            return true;
        }

        private static bool UsesClientRelationshipAdmission(RemoteRelationshipOverlayType relationshipType)
        {
            return relationshipType != RemoteRelationshipOverlayType.Generic;
        }

        private static int GetRelationshipOverlayPairCandidatePriority(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            if (ownerOverlay == null || candidateOverlay == null)
            {
                return int.MaxValue;
            }

            if (DoesRelationshipOverlayStateMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay))
            {
                return 0;
            }

            if (ownerOverlay.PairCharacterId.HasValue
                && ownerOverlay.PairCharacterId.Value > 0
                && ownerOverlay.PairCharacterId.Value == candidateCharacterId)
            {
                return 1;
            }

            if (candidateOverlay.PairCharacterId.HasValue
                && candidateOverlay.PairCharacterId.Value > 0
                && candidateOverlay.PairCharacterId.Value == ownerCharacterId)
            {
                return 2;
            }

            return 3;
        }

        private static bool DoesRelationshipOverlayStateMatch(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            if (ownerOverlay == null
                || candidateOverlay == null
                || ownerOverlay.RelationshipType != candidateOverlay.RelationshipType
                || ownerOverlay.ItemId <= 0
                || ownerOverlay.ItemId != candidateOverlay.ItemId
                || ownerCharacterId <= 0
                || candidateCharacterId <= 0
                || ownerCharacterId == candidateCharacterId)
            {
                return false;
            }

            return ownerOverlay.RelationshipType switch
            {
                RemoteRelationshipOverlayType.Couple => DoRingRelationshipOverlayStatesMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay),
                RemoteRelationshipOverlayType.Friendship => DoRingRelationshipOverlayStatesMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay),
                RemoteRelationshipOverlayType.NewYearCard => DoNewYearCardRelationshipOverlayStatesMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay),
                RemoteRelationshipOverlayType.Marriage => DoMarriageRelationshipOverlayStatesMatch(
                    ownerCharacterId,
                    ownerOverlay,
                    candidateCharacterId,
                    candidateOverlay),
                _ => true
            };
        }

        private static bool DoRingRelationshipOverlayStatesMatch(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            return ownerOverlay.ItemSerial.HasValue
                && ownerOverlay.PairItemSerial.HasValue
                && candidateOverlay.ItemSerial.HasValue
                && candidateOverlay.PairItemSerial.HasValue
                && ownerOverlay.PairItemSerial.Value == candidateOverlay.ItemSerial.Value
                && candidateOverlay.PairItemSerial.Value == ownerOverlay.ItemSerial.Value
                && RelationshipRecordTargetsCharacter(ownerOverlay.PairCharacterId, candidateCharacterId)
                && RelationshipRecordTargetsCharacter(candidateOverlay.PairCharacterId, ownerCharacterId);
        }

        private static bool DoNewYearCardRelationshipOverlayStatesMatch(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            return ownerOverlay.ItemSerial.HasValue
                && candidateOverlay.ItemSerial.HasValue
                && ownerOverlay.ItemSerial.Value == candidateOverlay.ItemSerial.Value
                && RelationshipRecordTargetsCharacter(ownerOverlay.PairCharacterId, candidateCharacterId)
                && RelationshipRecordTargetsCharacter(candidateOverlay.PairCharacterId, ownerCharacterId);
        }

        private static bool DoMarriageRelationshipOverlayStatesMatch(
            int ownerCharacterId,
            RemoteRelationshipOverlayState ownerOverlay,
            int candidateCharacterId,
            RemoteRelationshipOverlayState candidateOverlay)
        {
            return RelationshipRecordTargetsCharacter(ownerOverlay.PairCharacterId, candidateCharacterId)
                && RelationshipRecordTargetsCharacter(candidateOverlay.PairCharacterId, ownerCharacterId);
        }

        private static bool IsRelationshipOverlaySuppressed(PlayerCharacter player, int expectedCharacterId, int ownerCharacterId)
        {
            if (player?.Build == null
                || !player.IsAlive
                || player.Build.Id <= 0
                || player.Build.Id != expectedCharacterId
                || player.Build.Id == ownerCharacterId)
            {
                return true;
            }

            return player.HasActiveMorphTransform
                || IsRelationshipOverlayGhostAction(player.CurrentActionName);
        }

        private static bool IsRelationshipOverlaySuppressed(RemoteUserActor actor)
        {
            if (actor == null || !actor.IsVisibleInWorld)
            {
                return true;
            }

            return actor.HasMorphTemplate
                || IsRelationshipOverlayGhostAction(actor.ActionName);
        }

        private void ClearRelationshipOverlayStateForParity(
            RemoteUserActor actor,
            RemoteRelationshipOverlayType relationshipType,
            int currentTime)
        {
            if (actor?.RelationshipOverlays == null
                || !actor.RelationshipOverlays.TryGetValue(relationshipType, out RemoteRelationshipOverlayState existingOverlay)
                || existingOverlay == null)
            {
                return;
            }

            if (actor.CharacterId > 0)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    ResolveRemoteRelationshipOverlayActionOwnerName(relationshipType),
                    ResolveRemoteRelationshipOverlayCounterSkillId(relationshipType, existingOverlay.ItemId),
                    string.IsNullOrWhiteSpace(existingOverlay.OwnerActionName)
                        ? ResolveRemoteTemporaryStatAvatarEffectActionName(actor)
                        : existingOverlay.OwnerActionName,
                    existingOverlay.OwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, existingOverlay.StartTime),
                    currentTime);
            }

            actor.RelationshipOverlays.Remove(relationshipType);
        }

        private void ApplyPacketOwnedEmotionState(
            RemoteUserActor actor,
            int itemId,
            PacketOwnedAvatarEmotionSelection selection,
            bool byItemOption,
            int currentTime)
        {
            ApplyRemoteEmotionState(
                actor,
                itemId,
                selection.EmotionId,
                selection.EmotionName,
                byItemOption,
                currentTime,
                durationMs: 0,
                loadEffectAnimation: true);
        }

        private void ApplyRemoteEmotionState(
            RemoteUserActor actor,
            int itemId,
            int emotionId,
            string emotionName,
            bool byItemOption,
            int currentTime,
            int durationMs,
            bool loadEffectAnimation)
        {
            if (actor == null)
            {
                return;
            }

            string ownerActionName = ResolveRemoteTemporaryStatAvatarEffectActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            int ownerSkillId = ResolveRemotePacketOwnedEmotionCounterSkillId(itemId, emotionId, byItemOption);
            bool hasRestoredAnimationElapsed = TryRestoreRemoteAuxiliaryLayerOwnerCounter(
                actor.CharacterId,
                RemotePacketOwnedEmotionActionOwnerName,
                ownerSkillId,
                ownerActionName,
                ownerFacingRight,
                out int restoredAnimationElapsedMs);
            if (actor.PacketOwnedEmotion != null)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemotePacketOwnedEmotionActionOwnerName,
                    ResolveRemotePacketOwnedEmotionCounterSkillId(
                        actor.PacketOwnedEmotion.ItemId,
                        actor.PacketOwnedEmotion.EmotionId,
                        actor.PacketOwnedEmotion.ByItemOption),
                    actor.PacketOwnedEmotion.OwnerActionName,
                    actor.PacketOwnedEmotion.OwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.PacketOwnedEmotion.AnimationStartTime),
                    currentTime);
            }

            SkillAnimation effectAnimation = loadEffectAnimation
                ? _loader?.LoadPacketOwnedEmotionEffectAnimation(emotionName)
                : null;
            int expireDuration = effectAnimation?.TotalDuration > 0
                ? effectAnimation.TotalDuration
                : Math.Max(0, durationMs);
            int expireTime = expireDuration > 0
                ? currentTime + expireDuration
                : 0;
            actor.PacketOwnedEmotion = new RemotePacketOwnedEmotionState
            {
                ItemId = itemId,
                EmotionId = emotionId,
                EmotionName = emotionName,
                ByItemOption = byItemOption,
                EffectAnimation = effectAnimation,
                AnimationStartTime = hasRestoredAnimationElapsed
                    ? unchecked(currentTime - restoredAnimationElapsedMs)
                    : currentTime,
                ExpireTime = expireTime,
                OwnerActionName = ownerActionName,
                OwnerFacingRight = ownerFacingRight
            };

            if (actor.Assembler != null)
            {
                actor.Assembler.FaceExpressionName = emotionName;
            }
        }

        private void ClearPacketOwnedEmotionState(RemoteUserActor actor, int currentTime = int.MinValue)
        {
            if (actor == null)
            {
                return;
            }

            if (currentTime != int.MinValue
                && actor.PacketOwnedEmotion != null)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemotePacketOwnedEmotionActionOwnerName,
                    ResolveRemotePacketOwnedEmotionCounterSkillId(
                        actor.PacketOwnedEmotion.ItemId,
                        actor.PacketOwnedEmotion.EmotionId,
                        actor.PacketOwnedEmotion.ByItemOption),
                    actor.PacketOwnedEmotion.OwnerActionName,
                    actor.PacketOwnedEmotion.OwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.PacketOwnedEmotion.AnimationStartTime),
                    currentTime);
            }

            actor.PacketOwnedEmotion = null;
            if (actor.Assembler != null)
            {
                actor.Assembler.FaceExpressionName = "default";
            }
        }

        private void ApplyRemoteQuestDeliveryState(RemoteUserActor actor, int itemId, int currentTime)
        {
            if (actor == null || itemId <= 0)
            {
                return;
            }

            string ownerActionName = ResolveRemoteTemporaryStatAvatarEffectActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            if (actor.PacketOwnedQuestDeliveryEffectItemId is int existingItemId
                && actor.PacketOwnedQuestDeliveryEffectAppliedTime != int.MinValue)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemoteQuestDeliveryActionOwnerName,
                    ResolveRemoteQuestDeliveryCounterSkillId(existingItemId),
                    string.IsNullOrWhiteSpace(actor.PacketOwnedQuestDeliveryOwnerActionName)
                        ? ownerActionName
                        : actor.PacketOwnedQuestDeliveryOwnerActionName,
                    actor.PacketOwnedQuestDeliveryOwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.PacketOwnedQuestDeliveryEffectAppliedTime),
                    currentTime);
            }

            bool hasRestoredAnimationElapsed = TryRestoreRemoteAuxiliaryLayerOwnerCounter(
                actor.CharacterId,
                RemoteQuestDeliveryActionOwnerName,
                ResolveRemoteQuestDeliveryCounterSkillId(itemId),
                ownerActionName,
                ownerFacingRight,
                out int restoredAnimationElapsedMs);
            actor.PacketOwnedQuestDeliveryEffectItemId = itemId;
            actor.PacketOwnedQuestDeliveryOwnerActionName = ownerActionName;
            actor.PacketOwnedQuestDeliveryOwnerFacingRight = ownerFacingRight;
            actor.PacketOwnedQuestDeliveryEffectAppliedTime = hasRestoredAnimationElapsed
                ? unchecked(currentTime - restoredAnimationElapsedMs)
                : currentTime;
        }

        private void ClearRemoteQuestDeliveryState(RemoteUserActor actor, int currentTime = int.MinValue)
        {
            if (actor == null)
            {
                return;
            }

            if (currentTime != int.MinValue
                && actor.PacketOwnedQuestDeliveryEffectItemId is int itemId
                && actor.PacketOwnedQuestDeliveryEffectAppliedTime != int.MinValue)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemoteQuestDeliveryActionOwnerName,
                    ResolveRemoteQuestDeliveryCounterSkillId(itemId),
                    string.IsNullOrWhiteSpace(actor.PacketOwnedQuestDeliveryOwnerActionName)
                        ? ResolveRemoteTemporaryStatAvatarEffectActionName(actor)
                        : actor.PacketOwnedQuestDeliveryOwnerActionName,
                    actor.PacketOwnedQuestDeliveryOwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.PacketOwnedQuestDeliveryEffectAppliedTime),
                    currentTime);
            }

            actor.PacketOwnedQuestDeliveryEffectItemId = null;
            actor.PacketOwnedQuestDeliveryEffectAppliedTime = int.MinValue;
            actor.PacketOwnedQuestDeliveryOwnerActionName = null;
            actor.PacketOwnedQuestDeliveryOwnerFacingRight = true;
        }

        private void ApplyRemoteActiveEffectMotionBlurState(
            RemoteUserActor actor,
            ActiveEffectItemMotionBlurDefinition definition,
            int currentTime)
        {
            if (actor == null || !definition.IsValid)
            {
                return;
            }

            string ownerActionName = ResolveRemoteTemporaryStatAvatarEffectActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            bool hasRestoredAnimationElapsed = TryRestoreRemoteAuxiliaryLayerOwnerCounter(
                actor.CharacterId,
                RemoteActiveEffectMotionBlurActionOwnerName,
                ResolveRemoteActiveEffectMotionBlurCounterSkillId(definition.ItemId),
                ownerActionName,
                ownerFacingRight,
                out int restoredAnimationElapsedMs);
            if (!hasRestoredAnimationElapsed
                && TryResolveRemoteActiveEffectMotionBlurCurrentStateElapsed(
                    actor.ActiveEffectMotionBlur,
                    definition.ItemId,
                    ownerActionName,
                    ownerFacingRight,
                    currentTime,
                    out restoredAnimationElapsedMs))
            {
                hasRestoredAnimationElapsed = true;
            }

            if (actor.ActiveEffectMotionBlur != null)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemoteActiveEffectMotionBlurActionOwnerName,
                    ResolveRemoteActiveEffectMotionBlurCounterSkillId(actor.ActiveEffectMotionBlur.ActiveItemId),
                    actor.ActiveEffectMotionBlur.OwnerActionName,
                    actor.ActiveEffectMotionBlur.OwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.ActiveEffectMotionBlur.AnimationStartTime),
                    currentTime);
            }

            IReadOnlyDictionary<AvatarRenderLayer, int> layerHandleIds = ResolveRemoteActiveEffectMotionBlurLayerHandleIds(
                actor.ActiveEffectMotionBlur?.SimulatedLayerHandleIds
                ?? actor.ActiveEffectMotionBlurLayerHandleIds);
            actor.ActiveEffectMotionBlurLayerHandleIds = layerHandleIds;
            int overlayLayerHandleId = ResolveRemoteActiveEffectMotionBlurOverlayLayerHandleId(actor);
            if (actor.ActiveEffectMotionBlur != null)
            {
                actor.ActiveEffectMotionBlur.MarkTerminated();
            }

            var state = new RemoteActiveEffectMotionBlurState
            {
                Definition = definition,
                ActiveItemId = definition.ItemId,
                SimulatedAnimationStateId = NextRemoteActiveEffectMotionBlurStateId(),
                SimulatedOverlayLayerHandleId = overlayLayerHandleId,
                AnimationStartTime = ResolveRemoteActiveEffectMotionBlurAnimationStartTime(
                    currentTime,
                    hasRestoredAnimationElapsed,
                    restoredAnimationElapsedMs),
                OwnerActionName = ownerActionName,
                OwnerFacingRight = ownerFacingRight,
                SimulatedLayerHandleIds = layerHandleIds,
                NextSampleTime = currentTime
            };
            state.CaptureRegisteredLayerReferences();
            actor.ActiveEffectMotionBlur = state;
        }

        internal static bool TryResolveRemoteActiveEffectMotionBlurCurrentStateElapsed(
            RemoteActiveEffectMotionBlurState existingState,
            int itemId,
            string ownerActionName,
            bool ownerFacingRight,
            int currentTime,
            out int restoredAnimationElapsedMs)
        {
            restoredAnimationElapsedMs = 0;
            if (existingState == null
                || existingState.ActiveItemId != itemId
                || existingState.OwnerFacingRight != ownerFacingRight
                || !string.Equals(existingState.OwnerActionName, ownerActionName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            restoredAnimationElapsedMs = ResolveRemoteTemporaryStatTickElapsedMs(
                currentTime,
                existingState.AnimationStartTime);
            return true;
        }

        internal static int ResolveRemoteActiveEffectMotionBlurAnimationStartTime(
            int currentTime,
            bool hasRestoredAnimationElapsed,
            int restoredAnimationElapsedMs)
        {
            return hasRestoredAnimationElapsed
                ? unchecked(currentTime - Math.Max(0, restoredAnimationElapsedMs))
                : currentTime;
        }

        private bool TryApplyRemoteActiveEffectItemEffectState(
            RemoteUserActor actor,
            int itemId,
            int currentTime,
            out bool followOwner,
            out string error)
        {
            followOwner = false;
            error = null;
            if (actor == null || itemId <= 0)
            {
                error = "Active-effect item target or item id is invalid.";
                return false;
            }

            if (_loader == null)
            {
                ClearRemoteActiveEffectItemEffectState(actor, currentTime);
                error = "Character loader is not available.";
                return false;
            }

            ItemEffectAnimationSet effect = _loader.LoadActiveEffectItemAnimationSet(itemId, out followOwner);
            if (effect == null)
            {
                ClearRemoteActiveEffectItemEffectState(actor, currentTime);
                error = $"Item {itemId} does not publish renderable Item/*/effect active-effect frames.";
                return false;
            }

            string ownerActionName = ResolveRemoteTemporaryStatAvatarEffectActionName(actor);
            bool ownerFacingRight = actor.FacingRight;
            bool hasRestoredAnimationElapsed = TryRestoreRemoteAuxiliaryLayerOwnerCounter(
                actor.CharacterId,
                RemoteActiveEffectItemEffectActionOwnerName,
                ResolveRemoteActiveEffectItemEffectCounterSkillId(itemId),
                ownerActionName,
                ownerFacingRight,
                out int restoredAnimationElapsedMs);
            if (!hasRestoredAnimationElapsed
                && actor.ActiveEffectItemEffect != null
                && actor.ActiveEffectItemEffect.ItemId == itemId
                && actor.ActiveEffectItemEffect.OwnerFacingRight == ownerFacingRight
                && string.Equals(actor.ActiveEffectItemEffect.OwnerActionName, ownerActionName, StringComparison.OrdinalIgnoreCase))
            {
                restoredAnimationElapsedMs = ResolveRemoteTemporaryStatTickElapsedMs(
                    currentTime,
                    actor.ActiveEffectItemEffect.AnimationStartTime);
                hasRestoredAnimationElapsed = true;
            }

            if (actor.ActiveEffectItemEffect != null)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemoteActiveEffectItemEffectActionOwnerName,
                    ResolveRemoteActiveEffectItemEffectCounterSkillId(actor.ActiveEffectItemEffect.ItemId),
                    actor.ActiveEffectItemEffect.OwnerActionName,
                    actor.ActiveEffectItemEffect.OwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.ActiveEffectItemEffect.AnimationStartTime),
                    currentTime);
            }

            actor.ActiveEffectItemEffect = new RemoteActiveEffectItemEffectState
            {
                ItemId = itemId,
                Effect = effect,
                SimulatedOwnerLayerHandleId = NextRemoteActiveEffectItemEffectLayerHandleId(),
                AnimationStartTime = hasRestoredAnimationElapsed
                    ? unchecked(currentTime - restoredAnimationElapsedMs)
                    : currentTime,
                WorldOrigin = actor.Position,
                OwnerActionName = ownerActionName,
                OwnerFacingRight = ownerFacingRight,
                FollowOwner = followOwner
            };
            UpdateRemoteActiveEffectItemEffectLayerAdmission(actor);
            return true;
        }

        internal static Vector2 ResolveRemoteActiveEffectItemEffectDrawPositionForParity(
            RemoteActiveEffectItemEffectState state,
            Vector2 ownerPosition)
        {
            if (state == null || state.FollowOwner)
            {
                return ownerPosition;
            }

            return state.WorldOrigin;
        }

        internal static bool ResolveRemoteActiveEffectItemEffectFacingForParity(
            RemoteActiveEffectItemEffectState state,
            bool ownerFacingRight)
        {
            if (state == null || state.FollowOwner)
            {
                return ownerFacingRight;
            }

            return state.OwnerFacingRight;
        }

        internal static bool ShouldDrawRemoteActiveEffectItemEffectForParity(
            RemoteActiveEffectItemEffectState state,
            bool hiddenLikeClient,
            bool hasMorphTemplate)
        {
            return state?.Effect?.OwnerLayers != null
                && state.Effect.OwnerLayers.Count > 0
                && !hiddenLikeClient
                && !hasMorphTemplate;
        }

        private static void UpdateRemoteActiveEffectItemEffectLayerAdmission(RemoteUserActor actor)
        {
            RemoteActiveEffectItemEffectState state = actor?.ActiveEffectItemEffect;
            if (state == null)
            {
                return;
            }

            state.UpdateVisibleLayerAdmission(ShouldDrawRemoteActiveEffectItemEffectForParity(
                state,
                actor.HiddenLikeClient,
                actor.HasMorphTemplate));
        }

        private void ClearRemoteActiveEffectMotionBlurState(RemoteUserActor actor, int currentTime = int.MinValue)
        {
            if (actor == null)
            {
                return;
            }

            if (currentTime != int.MinValue
                && actor.ActiveEffectMotionBlur != null)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemoteActiveEffectMotionBlurActionOwnerName,
                    ResolveRemoteActiveEffectMotionBlurCounterSkillId(actor.ActiveEffectMotionBlur.ActiveItemId),
                    actor.ActiveEffectMotionBlur.OwnerActionName,
                    actor.ActiveEffectMotionBlur.OwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.ActiveEffectMotionBlur.AnimationStartTime),
                    currentTime);
            }

            actor.ActiveEffectMotionBlur?.MarkTerminated();
            actor.ActiveEffectMotionBlur = null;
        }

        private void ClearRemoteActiveEffectItemEffectState(RemoteUserActor actor, int currentTime = int.MinValue)
        {
            if (actor == null)
            {
                return;
            }

            if (currentTime != int.MinValue
                && actor.ActiveEffectItemEffect != null)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    actor.CharacterId,
                    RemoteActiveEffectItemEffectActionOwnerName,
                    ResolveRemoteActiveEffectItemEffectCounterSkillId(actor.ActiveEffectItemEffect.ItemId),
                    actor.ActiveEffectItemEffect.OwnerActionName,
                    actor.ActiveEffectItemEffect.OwnerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.ActiveEffectItemEffect.AnimationStartTime),
                    currentTime);
            }

            actor.ActiveEffectItemEffect?.MarkTerminated();
            actor.ActiveEffectItemEffect = null;
        }

        private static void ClearRemoteDragonCompanionState(RemoteUserActor actor)
        {
            if (actor?.RemoteDragonCompanion == null)
            {
                return;
            }

            actor.RemoteDragonCompanion.MarkTerminated();
            actor.RemoteDragonCompanion = null;
        }

        private void UpdatePacketOwnedEmotionState(RemoteUserActor actor, int currentTime)
        {
            if (actor?.PacketOwnedEmotion == null)
            {
                return;
            }

            if (ShouldExpireRemotePacketOwnedEmotionForParity(actor.PacketOwnedEmotion, currentTime))
            {
                ClearPacketOwnedEmotionState(actor, currentTime);
            }
        }

        internal static bool ShouldExpireRemotePacketOwnedEmotionForTesting(
            RemotePacketOwnedEmotionState state,
            int currentTime)
        {
            return ShouldExpireRemotePacketOwnedEmotionForParity(state, currentTime);
        }

        private static bool ShouldExpireRemotePacketOwnedEmotionForParity(
            RemotePacketOwnedEmotionState state,
            int currentTime)
        {
            return state?.ExpireTime != 0
                   && ClientOwnedAvatarEffectParity.HasUnsignedTickReached(
                       currentTime,
                       state.ExpireTime);
        }

        private static void UpdateRemoteActiveEffectMotionBlurState(RemoteUserActor actor, int currentTime)
        {
            RemoteActiveEffectMotionBlurState state = actor?.ActiveEffectMotionBlur;
            if (state?.Definition.IsValid != true)
            {
                return;
            }

            while (currentTime >= state.NextSampleTime)
            {
                AssembledFrame frame = actor.GetFrameAtTimeForRendering(state.NextSampleTime);
                if (frame != null
                    && TryCreateRemoteActiveEffectMotionBlurLayerSnapshot(
                        frame,
                        state.SimulatedLayerHandleIds,
                        state.SimulatedLayerHandleRefCounts,
                        out List<RemoteActiveEffectMotionBlurLayerSnapshot> layerSnapshots))
                {
                    RemoteActiveEffectMotionBlurOwnerSample ownerSample =
                        ResolveRemoteActiveEffectMotionBlurSnapshotOwnerState(actor, state.NextSampleTime);
                    IReadOnlyList<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation> layerReferenceOperations =
                        BuildRemoteActiveEffectMotionBlurSnapshotLayerReferenceOperations(
                            state,
                            layerSnapshots,
                            out int simulatedOverlayLayerHandleRefCount);
                    state.Snapshots.Add(new RemoteActiveEffectMotionBlurSnapshot
                    {
                        Layers = layerSnapshots,
                        FeetOffset = frame.FeetOffset,
                        Position = ownerSample.Position,
                        FacingRight = ownerSample.FacingRight,
                        SampleTime = state.NextSampleTime,
                        SimulatedOverlayLayerHandleId = state.SimulatedOverlayLayerHandleId,
                        SimulatedLayerReferenceOperations = layerReferenceOperations,
                        SimulatedOverlayLayerHandleRefCount = simulatedOverlayLayerHandleRefCount
                    });
                }

                state.NextSampleTime += Math.Max(1, state.Definition.IntervalMs);
            }

            for (int i = state.Snapshots.Count - 1; i >= 0; i--)
            {
                if (!ActiveEffectItemMotionBlurResolver.ShouldRetainSnapshot(
                        state.Snapshots[i].SampleTime,
                        currentTime,
                        state.Definition))
                {
                    state.ReleaseSnapshotLayerReferences(state.Snapshots[i]);
                    state.Snapshots.RemoveAt(i);
                }
            }
        }

        private static RemoteActiveEffectMotionBlurOwnerSample ResolveRemoteActiveEffectMotionBlurSnapshotOwnerState(
            RemoteUserActor actor,
            int sampleTime)
        {
            return ResolveRemoteActiveEffectMotionBlurSnapshotOwnerState(
                actor?.MovementSnapshot,
                actor?.Position ?? Vector2.Zero,
                actor?.FacingRight ?? true,
                sampleTime);
        }

        private static RemoteActiveEffectMotionBlurOwnerSample ResolveRemoteActiveEffectMotionBlurSnapshotOwnerState(
            PlayerMovementSyncSnapshot movementSnapshot,
            Vector2 fallbackPosition,
            bool fallbackFacingRight,
            int sampleTime)
        {
            if (ShouldUseRemoteActiveEffectMotionBlurMovementSnapshot(movementSnapshot, sampleTime))
            {
                PassivePositionSnapshot sample = movementSnapshot.SampleAtTime(sampleTime);
                return new RemoteActiveEffectMotionBlurOwnerSample(
                    new Vector2(sample.X, sample.Y),
                    sample.FacingRight);
            }

            return new RemoteActiveEffectMotionBlurOwnerSample(fallbackPosition, fallbackFacingRight);
        }

        private static bool ShouldUseRemoteActiveEffectMotionBlurMovementSnapshot(
            PlayerMovementSyncSnapshot movementSnapshot,
            int sampleTime)
        {
            if (!TryResolveRemoteActiveEffectMotionBlurMovementSnapshotSampleTimeBounds(
                    movementSnapshot,
                    out int startTime,
                    out int endTime))
            {
                return false;
            }

            return sampleTime >= startTime && sampleTime <= endTime;
        }

        private static bool TryResolveRemoteActiveEffectMotionBlurMovementSnapshotSampleTimeBounds(
            PlayerMovementSyncSnapshot movementSnapshot,
            out int startTime,
            out int endTime)
        {
            startTime = 0;
            endTime = 0;
            if (movementSnapshot == null)
            {
                return false;
            }

            startTime = movementSnapshot.PassivePosition.TimeStamp;
            endTime = movementSnapshot.PassivePosition.TimeStamp;
            List<MovePathElement> movePath = movementSnapshot.MovePath;
            if (movePath == null || movePath.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < movePath.Count; i++)
            {
                int timeStamp = movePath[i].TimeStamp;
                int sampleEndTime = ResolveRemoteActiveEffectMotionBlurMovePathElementSampleEndTime(movePath[i]);
                if (timeStamp < startTime)
                {
                    startTime = timeStamp;
                }

                if (sampleEndTime > endTime)
                {
                    endTime = sampleEndTime;
                }
            }

            return true;
        }

        private static int ResolveRemoteActiveEffectMotionBlurMovePathElementSampleEndTime(MovePathElement element)
        {
            int duration = Math.Max(0, (int)element.Duration);
            long candidate = (long)element.TimeStamp + duration;
            if (candidate > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)Math.Max(element.TimeStamp, candidate);
        }

        internal static bool TryCreateRemoteActiveEffectMotionBlurLayerSnapshot(
            AssembledFrame frame,
            out List<RemoteActiveEffectMotionBlurLayerSnapshot> layerSnapshots)
        {
            return TryCreateRemoteActiveEffectMotionBlurLayerSnapshot(
                frame,
                layerHandleIds: null,
                layerHandleRefCounts: null,
                out layerSnapshots);
        }

        internal static bool TryCreateRemoteActiveEffectMotionBlurLayerSnapshot(
            AssembledFrame frame,
            IReadOnlyDictionary<AvatarRenderLayer, int> layerHandleIds,
            out List<RemoteActiveEffectMotionBlurLayerSnapshot> layerSnapshots)
        {
            return TryCreateRemoteActiveEffectMotionBlurLayerSnapshot(
                frame,
                layerHandleIds,
                layerHandleRefCounts: null,
                out layerSnapshots);
        }

        internal static bool TryCreateRemoteActiveEffectMotionBlurLayerSnapshot(
            AssembledFrame frame,
            IReadOnlyDictionary<AvatarRenderLayer, int> layerHandleIds,
            IReadOnlyDictionary<AvatarRenderLayer, int> layerHandleRefCounts,
            out List<RemoteActiveEffectMotionBlurLayerSnapshot> layerSnapshots)
        {
            layerSnapshots = new List<RemoteActiveEffectMotionBlurLayerSnapshot>(RemoteActiveEffectMotionBlurLayerCaptureOrder.Length);
            if (frame == null)
            {
                return false;
            }

            for (int i = 0; i < RemoteActiveEffectMotionBlurLayerCaptureOrder.Length; i++)
            {
                AvatarRenderLayer layer = RemoteActiveEffectMotionBlurLayerCaptureOrder[i];
                IReadOnlyList<AssembledPart> sourceParts = ResolveRemoteActiveEffectMotionBlurLayerParts(frame, layer);
                if (sourceParts == null || sourceParts.Count == 0)
                {
                    continue;
                }

                var capturedParts = new List<AssembledPart>(sourceParts.Count);
                for (int partIndex = 0; partIndex < sourceParts.Count; partIndex++)
                {
                    AssembledPart part = sourceParts[partIndex];
                    if (part?.Texture == null || !part.IsVisible)
                    {
                        continue;
                    }

                    capturedParts.Add(CloneRemoteActiveEffectMotionBlurPart(part));
                }

                if (capturedParts.Count == 0)
                {
                    continue;
                }

                layerSnapshots.Add(new RemoteActiveEffectMotionBlurLayerSnapshot
                {
                    DrawOrder = ResolveRemoteActiveEffectMotionBlurLayerDrawOrder(layer),
                    SourceLayer = layer,
                    SourceLayerCaptureOrder = i,
                    SimulatedLayerHandleId = ResolveRemoteActiveEffectMotionBlurLayerHandleId(layer, layerHandleIds),
                    SimulatedLayerHandleRefCount = ResolveRemoteActiveEffectMotionBlurLayerHandleRefCount(layer, layerHandleIds, layerHandleRefCounts),
                    SimulatedSnapshotLayerHandleId = NextRemoteActiveEffectMotionBlurLayerHandleId(),
                    SimulatedSnapshotLayerHandleRefCount = 1,
                    SimulatedRepeatAnimationStateId = NextRemoteActiveEffectMotionBlurStateId(),
                    Parts = capturedParts
                });
            }

            layerSnapshots.Sort((left, right) => left.DrawOrder.CompareTo(right.DrawOrder));
            return layerSnapshots.Count > 0;
        }

        private static int ResolveRemoteActiveEffectMotionBlurLayerHandleId(
            AvatarRenderLayer layer,
            IReadOnlyDictionary<AvatarRenderLayer, int> layerHandleIds)
        {
            if (layerHandleIds != null
                && layerHandleIds.TryGetValue(layer, out int handleId)
                && handleId > 0)
            {
                return handleId;
            }

            return 0;
        }

        private static int ResolveRemoteActiveEffectMotionBlurLayerHandleRefCount(
            AvatarRenderLayer layer,
            IReadOnlyDictionary<AvatarRenderLayer, int> layerHandleIds,
            IReadOnlyDictionary<AvatarRenderLayer, int> layerHandleRefCounts)
        {
            if (ResolveRemoteActiveEffectMotionBlurLayerHandleId(layer, layerHandleIds) <= 0)
            {
                return 0;
            }

            if (layerHandleRefCounts != null
                && layerHandleRefCounts.TryGetValue(layer, out int refCount))
            {
                return refCount > 0 ? refCount + 1 : 0;
            }

            return 2;
        }

        private static int ResolveRemoteActiveEffectMotionBlurCopiedOverlayLayerHandleRefCount(
            RemoteActiveEffectMotionBlurState state)
        {
            return state?.SimulatedOverlayLayerHandleId > 0
                && state.SimulatedOverlayLayerHandleRefCount > 0
                ? state.SimulatedOverlayLayerHandleRefCount + 1
                : 0;
        }

        internal static IReadOnlyList<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation> BuildRemoteActiveEffectMotionBlurSnapshotLayerReferenceOperations(
            RemoteActiveEffectMotionBlurState state,
            IReadOnlyList<RemoteActiveEffectMotionBlurLayerSnapshot> layerSnapshots)
        {
            return BuildRemoteActiveEffectMotionBlurSnapshotLayerReferenceOperations(
                state,
                layerSnapshots,
                out _);
        }

        internal static IReadOnlyList<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation> BuildRemoteActiveEffectMotionBlurSnapshotLayerReferenceOperations(
            RemoteActiveEffectMotionBlurState state,
            IReadOnlyList<RemoteActiveEffectMotionBlurLayerSnapshot> layerSnapshots,
            out int overlayRefCount)
        {
            overlayRefCount = 0;
            return state?.CaptureSnapshotLayerReferences(layerSnapshots, out overlayRefCount)
                ?? Array.Empty<AnimationEffects.SecondaryMotionBlurLayerReferenceOperation>();
        }

        private static IReadOnlyDictionary<AvatarRenderLayer, int> CreateRemoteActiveEffectMotionBlurLayerHandleIds()
        {
            var handleIds = new Dictionary<AvatarRenderLayer, int>(RemoteActiveEffectMotionBlurLayerCaptureOrder.Length);
            for (int i = 0; i < RemoteActiveEffectMotionBlurLayerCaptureOrder.Length; i++)
            {
                AvatarRenderLayer layer = RemoteActiveEffectMotionBlurLayerCaptureOrder[i];
                if (!handleIds.ContainsKey(layer))
                {
                    handleIds[layer] = NextRemoteActiveEffectMotionBlurLayerHandleId();
                }
            }

            return handleIds;
        }

        private static IReadOnlyDictionary<AvatarRenderLayer, int> ResolveRemoteActiveEffectMotionBlurLayerHandleIds(
            IReadOnlyDictionary<AvatarRenderLayer, int> existingLayerHandleIds)
        {
            if (IsCompleteRemoteActiveEffectMotionBlurLayerHandleIdMap(existingLayerHandleIds))
            {
                return existingLayerHandleIds;
            }

            return CreateRemoteActiveEffectMotionBlurLayerHandleIds();
        }

        private static bool IsCompleteRemoteActiveEffectMotionBlurLayerHandleIdMap(
            IReadOnlyDictionary<AvatarRenderLayer, int> layerHandleIds)
        {
            if (layerHandleIds == null)
            {
                return false;
            }

            for (int i = 0; i < RemoteActiveEffectMotionBlurLayerCaptureOrder.Length; i++)
            {
                AvatarRenderLayer layer = RemoteActiveEffectMotionBlurLayerCaptureOrder[i];
                if (!layerHandleIds.TryGetValue(layer, out int handleId) || handleId <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int NextRemoteActiveEffectMotionBlurStateId()
        {
            return SimulatedMotionBlurIdentitySource.NextAnimationStateId();
        }

        private static int ResolveRemoteActiveEffectMotionBlurOverlayLayerHandleId(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return 0;
            }

            if (actor.ActiveEffectMotionBlurOverlayLayerHandleId <= 0)
            {
                actor.ActiveEffectMotionBlurOverlayLayerHandleId = NextRemoteActiveEffectMotionBlurLayerHandleId();
            }

            return actor.ActiveEffectMotionBlurOverlayLayerHandleId;
        }

        private static int NextRemoteActiveEffectMotionBlurLayerHandleId()
        {
            return SimulatedMotionBlurIdentitySource.NextLayerHandleId();
        }

        private static int NextRemoteActiveEffectItemEffectLayerHandleId()
        {
            return SimulatedMotionBlurIdentitySource.NextLayerHandleId();
        }

        private static int NextRemoteEffectByItemLayerHandleId()
        {
            return SimulatedMotionBlurIdentitySource.NextLayerHandleId();
        }

        private static int NextRemoteCarryItemEffectLayerHandleId()
        {
            return SimulatedMotionBlurIdentitySource.NextLayerHandleId();
        }

        private static int NextRemoteCompletedSetItemEffectLayerHandleId()
        {
            return SimulatedMotionBlurIdentitySource.NextLayerHandleId();
        }

        private static int NextRemoteTemporaryStatAffectedLayerHandleId()
        {
            return SimulatedMotionBlurIdentitySource.NextLayerHandleId();
        }

        private static int NextRemoteDragonCompanionLayerHandleId()
        {
            return SimulatedMotionBlurIdentitySource.NextLayerHandleId();
        }

        internal static IReadOnlyDictionary<AvatarRenderLayer, int> CreateRemoteActiveEffectMotionBlurLayerHandleIdsForTesting()
        {
            return CreateRemoteActiveEffectMotionBlurLayerHandleIds();
        }

        internal static int NextRemoteActiveEffectMotionBlurStateIdForTesting()
        {
            return NextRemoteActiveEffectMotionBlurStateId();
        }

        internal static int NextRemoteActiveEffectMotionBlurLayerHandleIdForTesting()
        {
            return NextRemoteActiveEffectMotionBlurLayerHandleId();
        }

        internal static int NextRemoteActiveEffectItemEffectLayerHandleIdForTesting()
        {
            return NextRemoteActiveEffectItemEffectLayerHandleId();
        }

        internal static int NextRemoteEffectByItemLayerHandleIdForTesting()
        {
            return NextRemoteEffectByItemLayerHandleId();
        }

        internal static int NextRemoteCarryItemEffectLayerHandleIdForTesting()
        {
            return NextRemoteCarryItemEffectLayerHandleId();
        }

        internal static int NextRemoteCompletedSetItemEffectLayerHandleIdForTesting()
        {
            return NextRemoteCompletedSetItemEffectLayerHandleId();
        }

        internal static int NextRemoteDragonCompanionLayerHandleIdForTesting()
        {
            return NextRemoteDragonCompanionLayerHandleId();
        }

        internal static bool IsCompleteRemoteActiveEffectMotionBlurLayerHandleIdMapForTesting(
            IReadOnlyDictionary<AvatarRenderLayer, int> layerHandleIds)
        {
            return IsCompleteRemoteActiveEffectMotionBlurLayerHandleIdMap(layerHandleIds);
        }

        internal static IReadOnlyDictionary<AvatarRenderLayer, int> ResolveRemoteActiveEffectMotionBlurLayerHandleIdsForTesting(
            IReadOnlyDictionary<AvatarRenderLayer, int> existingLayerHandleIds)
        {
            return ResolveRemoteActiveEffectMotionBlurLayerHandleIds(existingLayerHandleIds);
        }

        internal static (Vector2 Position, bool FacingRight) ResolveRemoteActiveEffectMotionBlurSnapshotOwnerStateForTesting(
            PlayerMovementSyncSnapshot movementSnapshot,
            Vector2 fallbackPosition,
            bool fallbackFacingRight,
            int sampleTime)
        {
            RemoteActiveEffectMotionBlurOwnerSample sample = ResolveRemoteActiveEffectMotionBlurSnapshotOwnerState(
                movementSnapshot,
                fallbackPosition,
                fallbackFacingRight,
                sampleTime);
            return (sample.Position, sample.FacingRight);
        }

        internal static IReadOnlyList<AssembledPart> ResolveRemoteActiveEffectMotionBlurLayerParts(
            AssembledFrame frame,
            AvatarRenderLayer layer)
        {
            if (frame?.AvatarRenderLayers != null)
            {
                int layerIndex = (int)layer;
                if (layerIndex >= 0
                    && layerIndex < frame.AvatarRenderLayers.Length
                    && frame.AvatarRenderLayers[layerIndex] != null
                    && frame.AvatarRenderLayers[layerIndex].Count > 0)
                {
                    return frame.AvatarRenderLayers[layerIndex];
                }
            }

            return frame?.Parts?
                .Where(part => part != null && part.IsVisible && part.RenderLayer == layer)
                .ToArray();
        }

        internal static int ResolveRemoteActiveEffectMotionBlurLayerDrawOrder(AvatarRenderLayer layer)
        {
            return layer switch
            {
                AvatarRenderLayer.UnderCharacter => 0,
                AvatarRenderLayer.OverCharacter => 1,
                AvatarRenderLayer.UnderFace => 2,
                AvatarRenderLayer.Face => 3,
                AvatarRenderLayer.OverFace => 4,
                _ => 2
            };
        }

        private static AssembledPart CloneRemoteActiveEffectMotionBlurPart(AssembledPart source)
        {
            return new AssembledPart
            {
                Texture = source.Texture,
                OffsetX = source.OffsetX,
                OffsetY = source.OffsetY,
                ZLayer = source.ZLayer,
                ZIndex = source.ZIndex,
                VisibilityTokens = source.VisibilityTokens,
                VisibilityPriority = source.VisibilityPriority,
                IsVisible = source.IsVisible,
                SourcePart = source.SourcePart,
                Tint = source.Tint,
                PartType = source.PartType,
                SourcePortableChairLayer = source.SourcePortableChairLayer,
                RenderLayer = source.RenderLayer
            };
        }

        private void UpdateTransientItemEffects(RemoteUserActor actor, int currentTime)
        {
            if (actor?.TransientItemEffects == null || actor.TransientItemEffects.Count == 0)
            {
                return;
            }

            for (int i = actor.TransientItemEffects.Count - 1; i >= 0; i--)
            {
                RemoteTransientItemEffectState state = actor.TransientItemEffects[i];
                if (state == null || IsTransientItemEffectExpired(state, currentTime))
                {
                    if (state != null)
                    {
                        StoreRemoteAuxiliaryLayerOwnerCounter(
                            actor.CharacterId,
                            RemoteEffectByItemActionOwnerName,
                            state.ItemId,
                            state.OwnerActionName,
                            state.OwnerFacingRight,
                            ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.AnimationStartTime),
                            currentTime);
                        state.ReleaseLayerReference();
                    }

                    actor.TransientItemEffects.RemoveAt(i);
                }
            }
        }

        private void ClearRemoteTransientItemEffectStates(RemoteUserActor actor, int currentTime = int.MinValue)
        {
            if (actor?.TransientItemEffects == null || actor.TransientItemEffects.Count == 0)
            {
                return;
            }

            for (int i = actor.TransientItemEffects.Count - 1; i >= 0; i--)
            {
                RemoteTransientItemEffectState state = actor.TransientItemEffects[i];
                if (state != null)
                {
                    if (currentTime != int.MinValue)
                    {
                        StoreRemoteAuxiliaryLayerOwnerCounter(
                            actor.CharacterId,
                            RemoteEffectByItemActionOwnerName,
                            state.ItemId,
                            state.OwnerActionName,
                            state.OwnerFacingRight,
                            ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.AnimationStartTime),
                            currentTime);
                    }

                    state.ReleaseLayerReference();
                }

                actor.TransientItemEffects.RemoveAt(i);
            }
        }

        private static bool IsTransientItemEffectExpired(RemoteTransientItemEffectState state, int currentTime)
        {
            int duration = state?.Effect?.TotalDurationMs ?? 0;
            if (duration <= 0)
            {
                return true;
            }

            return HasRemoteClientOwnedAuxiliaryAvatarLayerDurationElapsed(
                currentTime,
                state.AnimationStartTime,
                duration);
        }

        private bool IsRelationshipOverlayPartnerSuppressed(PlayerCharacter localPlayer, int partnerCharacterId, int ownerCharacterId)
        {
            if (partnerCharacterId <= 0 || partnerCharacterId == ownerCharacterId)
            {
                return true;
            }

            if (localPlayer?.Build?.Id == partnerCharacterId)
            {
                return IsRelationshipOverlaySuppressed(localPlayer, partnerCharacterId, ownerCharacterId);
            }

            return !_actorsById.TryGetValue(partnerCharacterId, out RemoteUserActor actor)
                   || IsRelationshipOverlaySuppressed(actor);
        }

        private static bool IsRelationshipOverlayGhostAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Ghost), StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("ghost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "darksight", StringComparison.OrdinalIgnoreCase);
        }

        internal static PortableChairLayer ResolveCarryItemEffectLayer(
            CarryItemEffectDefinition effect,
            int index,
            int tensTokenCount)
        {
            if (index < tensTokenCount)
            {
                return effect.BundleLayer ?? effect.SingleLayerA ?? effect.SingleLayerB;
            }

            int singleIndex = index - tensTokenCount;
            if ((singleIndex & 1) == 0)
            {
                return effect.SingleLayerA ?? effect.SingleLayerB ?? effect.BundleLayer;
            }

            return effect.SingleLayerB ?? effect.SingleLayerA ?? effect.BundleLayer;
        }

        private static ItemEffectAnimationSet NormalizeRelationshipOverlayEffect(
            ItemEffectAnimationSet source,
            RemoteRelationshipOverlayType relationshipType,
            bool shouldLoop)
        {
            ItemEffectAnimationSet cloned = CloneRelationshipOverlayEffect(source, shouldLoop);
            if (cloned == null)
            {
                return null;
            }

            if (relationshipType == RemoteRelationshipOverlayType.NewYearCard
                && cloned.SharedLayers.Count == 0
                && cloned.OwnerLayers.Count > 0)
            {
                cloned.SharedLayers.AddRange(cloned.OwnerLayers);
                cloned.OwnerLayers.Clear();
            }

            return cloned;
        }

        private static ItemEffectAnimationSet CloneRelationshipOverlayEffect(ItemEffectAnimationSet source, bool shouldLoop)
        {
            if (source == null)
            {
                return null;
            }

            return new ItemEffectAnimationSet
            {
                ItemId = source.ItemId,
                OwnerLayers = CloneRelationshipOverlayLayers(source.OwnerLayers, shouldLoop),
                SharedLayers = CloneRelationshipOverlayLayers(source.SharedLayers, shouldLoop)
            };
        }

        private static List<PortableChairLayer> CloneRelationshipOverlayLayers(
            IEnumerable<PortableChairLayer> layers,
            bool shouldLoop)
        {
            List<PortableChairLayer> clones = new();
            if (layers == null)
            {
                return clones;
            }

            foreach (PortableChairLayer layer in layers)
            {
                if (layer == null)
                {
                    continue;
                }

                CharacterAnimation animation = null;
                if (layer.Animation != null)
                {
                    animation = new CharacterAnimation
                    {
                        Action = layer.Animation.Action,
                        ActionName = layer.Animation.ActionName,
                        Frames = layer.Animation.Frames,
                        Loop = shouldLoop
                    };
                    animation.CalculateTotalDuration();
                }

                clones.Add(new PortableChairLayer
                {
                    Name = layer.Name,
                    Animation = animation,
                    RelativeZ = layer.RelativeZ,
                    PositionHint = layer.PositionHint
                });
            }

            return clones;
        }

        public string DescribeStatus()
        {
            if (_actorsById.Count == 0)
            {
                return $"Remote user pool empty. {DescribeRelationshipRecordTableStatus()}";
            }

            return $"Remote user pool active, count={_actorsById.Count}, users={string.Join("; ", _actorsById.Values.OrderBy(static value => value.CharacterId).Select(static value => value.Describe()))}. {DescribeRelationshipRecordTableStatus()}";
        }

        private void AssignDriverPassengerLink(int driverId, int passengerId)
        {
            if (!_actorsById.TryGetValue(driverId, out RemoteUserActor driverActor))
            {
                return;
            }

            int previousPassengerId = driverActor.FollowPassengerId;
            if (previousPassengerId > 0
                && previousPassengerId != passengerId
                && _actorsById.TryGetValue(previousPassengerId, out RemoteUserActor previousPassenger)
                && previousPassenger.FollowDriverId == driverId)
            {
                previousPassenger.FollowDriverId = 0;
            }

            driverActor.FollowPassengerId = passengerId;
        }

        private void ClearDriverPassengerLink(int driverId, int passengerId)
        {
            if (driverId <= 0
                || !_actorsById.TryGetValue(driverId, out RemoteUserActor driverActor)
                || driverActor.FollowPassengerId != passengerId)
            {
                return;
            }

            driverActor.FollowPassengerId = 0;
        }

        private void ClearActorFollowLinks(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            ClearDriverPassengerLink(actor.FollowDriverId, actor.CharacterId);
            if (actor.FollowPassengerId > 0
                && _actorsById.TryGetValue(actor.FollowPassengerId, out RemoteUserActor passengerActor)
                && passengerActor.FollowDriverId == actor.CharacterId)
            {
                passengerActor.FollowDriverId = 0;
            }

            actor.FollowDriverId = 0;
            actor.FollowPassengerId = 0;
        }

        private bool TryApplyAvatarModifiedRelationshipRecord(
            RemoteUserActor actor,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            int currentTime,
            out string message)
        {
            if (actor == null)
            {
                message = "Remote actor is required.";
                return false;
            }

            EnsureRelationshipRecordTablesInitialized();
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(relationshipType);
            if (relationshipRecord.IsActive)
            {
                bool canUseExplicitRecordOwner = relationshipType is
                    RemoteRelationshipOverlayType.Couple or
                    RemoteRelationshipOverlayType.Friendship;
                int ownerCharacterId = canUseExplicitRecordOwner && relationshipRecord.CharacterId.GetValueOrDefault() > 0
                    ? relationshipRecord.CharacterId.Value
                    : actor.CharacterId;
                int? pairCharacterId = relationshipType == RemoteRelationshipOverlayType.Marriage
                    ? ResolveMarriagePairCharacterId(ownerCharacterId, relationshipRecord)
                    : relationshipRecord.PairCharacterId;
                RemoteUserRelationshipRecord normalizedRecord = relationshipRecord with
                {
                    CharacterId = ownerCharacterId,
                    PairCharacterId = pairCharacterId
                };
                normalizedRecord = PreserveAvatarModifiedCanonicalRelationshipRecord(
                    actor.CharacterId,
                    relationshipType,
                    normalizedRecord,
                    recordTable);
                ownerCharacterId = normalizedRecord.CharacterId.GetValueOrDefault(ownerCharacterId);
                RemoveRelationshipRecordDispatchKeysForOwner(relationshipType, ownerCharacterId);
                recordTable[ownerCharacterId] = normalizedRecord;
                RegisterRelationshipRecordDispatchKeys(
                    relationshipType,
                    default,
                    normalizedRecord,
                    ownerCharacterId);
            }
            else
            {
                foreach (int ownerCharacterId in ResolveAvatarModifiedRelationshipOwnerCharacterIdsToClear(
                             actor.CharacterId,
                             relationshipType,
                             relationshipRecord,
                             recordTable))
                {
                    RemoveRelationshipRecordDispatchKeysForOwner(relationshipType, ownerCharacterId);
                    recordTable.Remove(ownerCharacterId);
                }
            }

            RefreshRelationshipOverlays(relationshipType, currentTime);
            message = $"Remote user {actor.CharacterId} {relationshipType} relationship state refreshed.";
            return true;
        }

        private static RemoteUserRelationshipRecord PreserveAvatarModifiedCanonicalRelationshipRecord(
            int packetActorCharacterId,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            IReadOnlyDictionary<int, RemoteUserRelationshipRecord> recordTable)
        {
            if (relationshipType is not (RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship)
                || !relationshipRecord.IsActive
                || recordTable == null
                || relationshipRecord.ItemSerial == null
                || relationshipRecord.PairItemSerial == null)
            {
                return relationshipRecord;
            }

            int explicitOwnerCharacterId = relationshipRecord.CharacterId.GetValueOrDefault();
            int explicitPairCharacterId = relationshipRecord.PairCharacterId.GetValueOrDefault();
            if (explicitOwnerCharacterId > 0
                && explicitOwnerCharacterId != packetActorCharacterId
                && explicitPairCharacterId > 0)
            {
                return relationshipRecord;
            }

            foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in recordTable)
            {
                RemoteUserRelationshipRecord storedRecord = entry.Value;
                if (!storedRecord.IsActive
                    || storedRecord.ItemSerial == null
                    || storedRecord.PairItemSerial == null
                    || !DoAvatarModifiedRelationshipSerialPairsMatch(relationshipRecord, storedRecord))
                {
                    continue;
                }

                return relationshipRecord with
                {
                    ItemId = relationshipRecord.ItemId > 0 ? relationshipRecord.ItemId : storedRecord.ItemId,
                    ItemSerial = storedRecord.ItemSerial,
                    PairItemSerial = storedRecord.PairItemSerial,
                    CharacterId = storedRecord.CharacterId.GetValueOrDefault(entry.Key),
                    PairCharacterId = storedRecord.PairCharacterId.GetValueOrDefault(packetActorCharacterId)
                };
            }

            return relationshipRecord;
        }

        private static bool DoAvatarModifiedRelationshipSerialPairsMatch(
            RemoteUserRelationshipRecord incomingRecord,
            RemoteUserRelationshipRecord storedRecord)
        {
            return incomingRecord.ItemSerial.HasValue
                && incomingRecord.PairItemSerial.HasValue
                && storedRecord.ItemSerial.HasValue
                && storedRecord.PairItemSerial.HasValue
                && ((incomingRecord.ItemSerial.Value == storedRecord.ItemSerial.Value
                        && incomingRecord.PairItemSerial.Value == storedRecord.PairItemSerial.Value)
                    || (incomingRecord.ItemSerial.Value == storedRecord.PairItemSerial.Value
                        && incomingRecord.PairItemSerial.Value == storedRecord.ItemSerial.Value));
        }

        private static int ResolveAvatarModifiedRelationshipOwnerCharacterId(
            int actorCharacterId,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord)
        {
            if (relationshipType is (RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship)
                && relationshipRecord.CharacterId.GetValueOrDefault() > 0)
            {
                return relationshipRecord.CharacterId.Value;
            }

            return actorCharacterId;
        }

        private static IReadOnlyList<int> ResolveAvatarModifiedRelationshipOwnerCharacterIdsToClear(
            int actorCharacterId,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            IReadOnlyDictionary<int, RemoteUserRelationshipRecord> recordTable)
        {
            HashSet<int> ownerCharacterIds = new();
            int explicitOwnerCharacterId = ResolveAvatarModifiedRelationshipOwnerCharacterId(
                actorCharacterId,
                relationshipType,
                relationshipRecord);
            if (explicitOwnerCharacterId > 0)
            {
                ownerCharacterIds.Add(explicitOwnerCharacterId);
            }

            if (recordTable == null)
            {
                return ownerCharacterIds.ToArray();
            }

            foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in recordTable)
            {
                RemoteUserRelationshipRecord storedRecord = entry.Value;
                if (!storedRecord.IsActive)
                {
                    continue;
                }

                if (DoesAvatarModifiedRelationshipClearMatch(
                        actorCharacterId,
                        relationshipRecord,
                        entry.Key,
                        storedRecord))
                {
                    ownerCharacterIds.Add(entry.Key);
                }
            }

            return ownerCharacterIds.ToArray();
        }

        private static bool DoesAvatarModifiedRelationshipClearMatch(
            int actorCharacterId,
            RemoteUserRelationshipRecord clearRecord,
            int ownerCharacterId,
            RemoteUserRelationshipRecord storedRecord)
        {
            if (actorCharacterId > 0)
            {
                if (ownerCharacterId == actorCharacterId)
                {
                    return true;
                }

                if (storedRecord.CharacterId.GetValueOrDefault() == actorCharacterId
                    || storedRecord.PairCharacterId.GetValueOrDefault() == actorCharacterId)
                {
                    return true;
                }
            }

            if (clearRecord.CharacterId.GetValueOrDefault() > 0)
            {
                int clearCharacterId = clearRecord.CharacterId.Value;
                if (ownerCharacterId == clearCharacterId
                    || storedRecord.CharacterId.GetValueOrDefault() == clearCharacterId
                    || storedRecord.PairCharacterId.GetValueOrDefault() == clearCharacterId)
                {
                    return true;
                }
            }

            return DoesAvatarModifiedRelationshipSerialClearMatch(clearRecord.ItemSerial, storedRecord)
                || DoesAvatarModifiedRelationshipSerialClearMatch(clearRecord.PairItemSerial, storedRecord);
        }

        private static bool DoesAvatarModifiedRelationshipSerialClearMatch(
            long? clearSerial,
            RemoteUserRelationshipRecord storedRecord)
        {
            if (!clearSerial.HasValue)
            {
                return false;
            }

            long serial = clearSerial.Value;
            return (storedRecord.ItemSerial.HasValue && storedRecord.ItemSerial.Value == serial)
                || (storedRecord.PairItemSerial.HasValue && storedRecord.PairItemSerial.Value == serial);
        }

        private static int? ResolveMarriagePairCharacterId(int ownerCharacterId, RemoteUserRelationshipRecord relationshipRecord)
        {
            if (!relationshipRecord.IsActive)
            {
                return null;
            }

            if (relationshipRecord.CharacterId.HasValue
                && relationshipRecord.CharacterId.Value > 0
                && relationshipRecord.CharacterId.Value != ownerCharacterId)
            {
                return relationshipRecord.CharacterId.Value;
            }

            if (relationshipRecord.PairCharacterId.HasValue
                && relationshipRecord.PairCharacterId.Value > 0
                && relationshipRecord.PairCharacterId.Value != ownerCharacterId)
            {
                return relationshipRecord.PairCharacterId.Value;
            }

            return relationshipRecord.PairCharacterId;
        }

        private void SyncRelationshipOverlaysFromRecords(int ownerCharacterId, int currentTime)
        {
            if (!_actorsById.TryGetValue(ownerCharacterId, out RemoteUserActor actor))
            {
                return;
            }

            SyncRelationshipOverlayFromRecord(actor, RemoteRelationshipOverlayType.Couple, currentTime);
            SyncRelationshipOverlayFromRecord(actor, RemoteRelationshipOverlayType.Friendship, currentTime);
            SyncRelationshipOverlayFromRecord(actor, RemoteRelationshipOverlayType.NewYearCard, currentTime);
            SyncRelationshipOverlayFromRecord(actor, RemoteRelationshipOverlayType.Marriage, currentTime);
        }

        private void SyncRelationshipOverlayFromRecord(RemoteUserActor actor, RemoteRelationshipOverlayType relationshipType, int currentTime)
        {
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(relationshipType);
            if (!recordTable.TryGetValue(actor.CharacterId, out RemoteUserRelationshipRecord record)
                || !record.IsActive
                || record.ItemId <= 0)
            {
                ClearRelationshipOverlayStateForParity(actor, relationshipType, currentTime);
                return;
            }

            int? pairCharacterId = ResolveRelationshipOverlayPairCharacterIdFromRecord(actor.CharacterId, relationshipType, record);
            if (!pairCharacterId.HasValue || pairCharacterId.Value <= 0)
            {
                ClearRelationshipOverlayStateForParity(actor, relationshipType, currentTime);
                return;
            }

            TrySetItemEffect(
                actor.CharacterId,
                relationshipType,
                record.ItemId,
                pairCharacterId,
                currentTime,
                out _,
                record.ItemSerial,
                record.PairItemSerial);
        }

        private void RefreshRelationshipOverlays(RemoteRelationshipOverlayType relationshipType, int currentTime)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                SyncRelationshipOverlayFromRecord(actor, relationshipType, currentTime);
            }
        }

        private int? ResolveRelationshipOverlayPairCharacterIdFromRecord(
            int ownerCharacterId,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord)
        {
            int? explicitPairCharacterId = relationshipType == RemoteRelationshipOverlayType.Marriage
                ? ResolveMarriagePairCharacterId(ownerCharacterId, relationshipRecord)
                : relationshipRecord.PairCharacterId;
            if (relationshipType == RemoteRelationshipOverlayType.Generic)
            {
                return explicitPairCharacterId;
            }

            if (TryFindMatchedRemoteRelationshipRecordOwner(
                    ownerCharacterId,
                    relationshipType,
                    relationshipRecord,
                    explicitPairCharacterId,
                    out int matchedRemoteCharacterId))
            {
                return matchedRemoteCharacterId;
            }

            if (explicitPairCharacterId.HasValue
                && explicitPairCharacterId.Value > 0
                && _actorsById.ContainsKey(explicitPairCharacterId.Value))
            {
                return null;
            }

            return explicitPairCharacterId;
        }

        internal bool TryResolveRelationshipWhisperTarget(
            int ownerCharacterId,
            RemoteRelationshipOverlayType relationshipType,
            out string whisperTarget)
        {
            whisperTarget = null;
            if (ownerCharacterId <= 0)
            {
                return false;
            }

            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(relationshipType);
            if (recordTable.Count == 0
                || !recordTable.TryGetValue(ownerCharacterId, out RemoteUserRelationshipRecord relationshipRecord))
            {
                return false;
            }

            int? pairCharacterId = ResolveRelationshipOverlayPairCharacterIdFromRecord(ownerCharacterId, relationshipType, relationshipRecord);
            if (!pairCharacterId.HasValue
                || pairCharacterId.Value <= 0
                || !_actorsById.TryGetValue(pairCharacterId.Value, out RemoteUserActor targetActor))
            {
                return false;
            }

            string resolvedName = targetActor.Name?.Trim();
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                return false;
            }

            whisperTarget = resolvedName;
            return true;
        }

        private bool TryFindMatchedRemoteRelationshipRecordOwner(
            int ownerCharacterId,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            int? explicitPairCharacterId,
            out int matchedRemoteCharacterId)
        {
            matchedRemoteCharacterId = 0;
            Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(relationshipType);
            if (recordTable.Count == 0)
            {
                return false;
            }

            if (explicitPairCharacterId.HasValue
                && explicitPairCharacterId.Value > 0
                && explicitPairCharacterId.Value != ownerCharacterId
                && recordTable.TryGetValue(explicitPairCharacterId.Value, out RemoteUserRelationshipRecord explicitPartnerRecord)
                && DoRelationshipRecordsMatch(
                    relationshipType,
                    ownerCharacterId,
                    relationshipRecord,
                    explicitPairCharacterId.Value,
                    explicitPartnerRecord))
            {
                matchedRemoteCharacterId = explicitPairCharacterId.Value;
                return true;
            }

            foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in recordTable
                .OrderByDescending(candidate => explicitPairCharacterId.HasValue && candidate.Key == explicitPairCharacterId.Value)
                .ThenBy(candidate => candidate.Key))
            {
                if (entry.Key == ownerCharacterId)
                {
                    continue;
                }

                if (!DoRelationshipRecordsMatch(
                        relationshipType,
                        ownerCharacterId,
                        relationshipRecord,
                        entry.Key,
                        entry.Value))
                {
                    continue;
                }

                matchedRemoteCharacterId = entry.Key;
                return true;
            }

            return false;
        }

        private void EnsureRelationshipRecordTablesInitialized()
        {
            EnsureRelationshipRecordTable(RemoteRelationshipOverlayType.Couple);
            EnsureRelationshipRecordTable(RemoteRelationshipOverlayType.Friendship);
            EnsureRelationshipRecordTable(RemoteRelationshipOverlayType.NewYearCard);
            EnsureRelationshipRecordTable(RemoteRelationshipOverlayType.Marriage);
        }

        private void EnsureRelationshipRecordTable(RemoteRelationshipOverlayType relationshipType)
        {
            if (!_relationshipRecordsByOwnerCharacterId.ContainsKey(relationshipType))
            {
                _relationshipRecordsByOwnerCharacterId[relationshipType] = new Dictionary<int, RemoteUserRelationshipRecord>();
            }

            if (!_relationshipRecordOwnerByDispatchKey.ContainsKey(relationshipType))
            {
                _relationshipRecordOwnerByDispatchKey[relationshipType] = new Dictionary<RemoteRelationshipRecordDispatchKey, int>();
            }
        }

        private void ClearRelationshipRecordTables()
        {
            foreach (Dictionary<int, RemoteUserRelationshipRecord> table in _relationshipRecordsByOwnerCharacterId.Values)
            {
                table.Clear();
            }

            foreach (Dictionary<RemoteRelationshipRecordDispatchKey, int> table in _relationshipRecordOwnerByDispatchKey.Values)
            {
                table.Clear();
            }
        }

        private Dictionary<int, RemoteUserRelationshipRecord> GetRelationshipRecordTable(RemoteRelationshipOverlayType relationshipType)
        {
            EnsureRelationshipRecordTable(relationshipType);
            return _relationshipRecordsByOwnerCharacterId[relationshipType];
        }

        private Dictionary<RemoteRelationshipRecordDispatchKey, int> GetRelationshipRecordDispatchOwnerTable(RemoteRelationshipOverlayType relationshipType)
        {
            EnsureRelationshipRecordTable(relationshipType);
            return _relationshipRecordOwnerByDispatchKey[relationshipType];
        }

        private void RegisterRelationshipRecordDispatchKey(
            RemoteRelationshipOverlayType relationshipType,
            RemoteRelationshipRecordDispatchKey dispatchKey,
            int ownerCharacterId)
        {
            if (!dispatchKey.HasValue || ownerCharacterId <= 0)
            {
                return;
            }

            GetRelationshipRecordDispatchOwnerTable(relationshipType)[dispatchKey] = ownerCharacterId;
        }

        private void RegisterRelationshipRecordDispatchKeys(
            RemoteRelationshipOverlayType relationshipType,
            RemoteRelationshipRecordDispatchKey primaryDispatchKey,
            RemoteUserRelationshipRecord relationshipRecord,
            int ownerCharacterId)
        {
            RegisterRelationshipRecordDispatchKey(relationshipType, primaryDispatchKey, ownerCharacterId);

            if (relationshipType is RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship)
            {
                RegisterRelationshipRecordDispatchKey(
                    relationshipType,
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                        relationshipRecord.ItemSerial,
                        CharacterId: null),
                    ownerCharacterId);
                RegisterRelationshipRecordDispatchKey(
                    relationshipType,
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                        relationshipRecord.PairItemSerial,
                        CharacterId: null),
                    ownerCharacterId);
                return;
            }

            if (relationshipType == RemoteRelationshipOverlayType.NewYearCard)
            {
                RegisterRelationshipRecordDispatchKey(
                    relationshipType,
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.NewYearCardSerial,
                        relationshipRecord.ItemSerial,
                        CharacterId: null),
                    ownerCharacterId);
                return;
            }

            if (relationshipType == RemoteRelationshipOverlayType.Marriage)
            {
                RegisterRelationshipRecordDispatchKey(
                    relationshipType,
                    new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.CharacterId,
                        Serial: null,
                        ownerCharacterId),
                    ownerCharacterId);
            }
        }

        private void RemoveRelationshipRecordDispatchKeysForOwner(RemoteRelationshipOverlayType relationshipType, int ownerCharacterId)
        {
            if (ownerCharacterId <= 0)
            {
                return;
            }

            Dictionary<RemoteRelationshipRecordDispatchKey, int> dispatchTable = GetRelationshipRecordDispatchOwnerTable(relationshipType);
            foreach (RemoteRelationshipRecordDispatchKey dispatchKey in dispatchTable
                .Where(entry => entry.Value == ownerCharacterId)
                .Select(entry => entry.Key)
                .ToArray())
            {
                dispatchTable.Remove(dispatchKey);
            }
        }

        private void RemoveRelationshipRecordOwner(
            RemoteRelationshipOverlayType relationshipType,
            int ownerCharacterId,
            IDictionary<int, RemoteUserRelationshipRecord> recordTable)
        {
            recordTable?.Remove(ownerCharacterId);
            RemoveRelationshipRecordDispatchKeysForOwner(relationshipType, ownerCharacterId);
            if (_actorsById.TryGetValue(ownerCharacterId, out RemoteUserActor ownerActor))
            {
                ownerActor.RelationshipOverlays.Remove(relationshipType);
            }
        }

        private void PurgeRelationshipRecordsForActor(int characterId)
        {
            if (characterId <= 0)
            {
                return;
            }

            EnsureRelationshipRecordTablesInitialized();
            foreach (RemoteRelationshipOverlayType relationshipType in RelationshipRecordOverlayTypes)
            {
                Dictionary<int, RemoteUserRelationshipRecord> recordTable = GetRelationshipRecordTable(relationshipType);
                foreach (KeyValuePair<int, RemoteUserRelationshipRecord> entry in recordTable.ToArray())
                {
                    int ownerCharacterId = entry.Key;
                    RemoteUserRelationshipRecord record = entry.Value;
                    int? pairCharacterId = relationshipType == RemoteRelationshipOverlayType.Marriage
                        ? ResolveMarriagePairCharacterId(ownerCharacterId, record)
                        : record.PairCharacterId;
                    if (ownerCharacterId != characterId
                        && (!pairCharacterId.HasValue || pairCharacterId.Value != characterId))
                    {
                        continue;
                    }

                    RemoveRelationshipRecordOwner(relationshipType, ownerCharacterId, recordTable);
                }

                RefreshRelationshipOverlays(relationshipType, Environment.TickCount);
            }
        }

        private string DescribeRelationshipRecordTableStatus()
        {
            EnsureRelationshipRecordTablesInitialized();
            int coupleCount = GetRelationshipRecordTable(RemoteRelationshipOverlayType.Couple).Count;
            int friendshipCount = GetRelationshipRecordTable(RemoteRelationshipOverlayType.Friendship).Count;
            int newYearCount = GetRelationshipRecordTable(RemoteRelationshipOverlayType.NewYearCard).Count;
            int marriageCount = GetRelationshipRecordTable(RemoteRelationshipOverlayType.Marriage).Count;
            return $"Relationship record tables: couple={coupleCount}, friendship={friendshipCount}, newYear={newYearCount}, marriage={marriageCount}.";
        }

        private static bool DoesRelationshipRecordRemovalMatch(
            RemoteUserRelationshipRecordRemovePacket packet,
            int ownerCharacterId,
            long? itemSerial,
            long? pairItemSerial,
            int? pairCharacterId)
        {
            if (packet.CharacterId.HasValue && packet.CharacterId.Value > 0 && packet.CharacterId.Value == ownerCharacterId)
            {
                return true;
            }

            if (packet.CharacterId.HasValue
                && packet.CharacterId.Value > 0
                && pairCharacterId.HasValue
                && pairCharacterId.Value == packet.CharacterId.Value)
            {
                return true;
            }

            if (!packet.ItemSerial.HasValue)
            {
                return false;
            }

            long packetItemSerial = packet.ItemSerial.Value;
            return itemSerial == packetItemSerial || pairItemSerial == packetItemSerial;
        }

        internal static bool DoRelationshipRecordsMatch(
            RemoteRelationshipOverlayType relationshipType,
            int ownerCharacterId,
            RemoteUserRelationshipRecord ownerRecord,
            int partnerCharacterId,
            RemoteUserRelationshipRecord partnerRecord)
        {
            if (!ownerRecord.IsActive
                || !partnerRecord.IsActive
                || ownerCharacterId <= 0
                || partnerCharacterId <= 0
                || ownerCharacterId == partnerCharacterId)
            {
                return false;
            }

            return relationshipType switch
            {
                RemoteRelationshipOverlayType.Couple => DoRingRelationshipRecordsMatch(
                    ownerCharacterId,
                    ownerRecord,
                    partnerCharacterId,
                    partnerRecord),
                RemoteRelationshipOverlayType.Friendship => DoRingRelationshipRecordsMatch(
                    ownerCharacterId,
                    ownerRecord,
                    partnerCharacterId,
                    partnerRecord),
                RemoteRelationshipOverlayType.NewYearCard => DoNewYearCardRelationshipRecordsMatch(
                    ownerCharacterId,
                    ownerRecord,
                    partnerCharacterId,
                    partnerRecord),
                RemoteRelationshipOverlayType.Marriage => DoMarriageRelationshipRecordsMatch(
                    ownerCharacterId,
                    ownerRecord,
                    partnerCharacterId,
                    partnerRecord),
                _ => false
            };
        }

        private static bool DoRingRelationshipRecordsMatch(
            int ownerCharacterId,
            RemoteUserRelationshipRecord ownerRecord,
            int partnerCharacterId,
            RemoteUserRelationshipRecord partnerRecord)
        {
            return ownerRecord.ItemId > 0
                && ownerRecord.ItemId == partnerRecord.ItemId
                && ownerRecord.ItemSerial.HasValue
                && ownerRecord.PairItemSerial.HasValue
                && partnerRecord.ItemSerial.HasValue
                && partnerRecord.PairItemSerial.HasValue
                && ownerRecord.PairItemSerial.Value == partnerRecord.ItemSerial.Value
                && partnerRecord.PairItemSerial.Value == ownerRecord.ItemSerial.Value
                && RelationshipRecordTargetsCharacter(ownerRecord.PairCharacterId, partnerCharacterId)
                && RelationshipRecordTargetsCharacter(partnerRecord.PairCharacterId, ownerCharacterId);
        }

        private static bool DoNewYearCardRelationshipRecordsMatch(
            int ownerCharacterId,
            RemoteUserRelationshipRecord ownerRecord,
            int partnerCharacterId,
            RemoteUserRelationshipRecord partnerRecord)
        {
            return ownerRecord.ItemId > 0
                && ownerRecord.ItemId == partnerRecord.ItemId
                && ownerRecord.ItemSerial.HasValue
                && partnerRecord.ItemSerial.HasValue
                && ownerRecord.ItemSerial.Value == partnerRecord.ItemSerial.Value
                && RelationshipRecordTargetsCharacter(ownerRecord.PairCharacterId, partnerCharacterId)
                && RelationshipRecordTargetsCharacter(partnerRecord.PairCharacterId, ownerCharacterId);
        }

        private static bool DoMarriageRelationshipRecordsMatch(
            int ownerCharacterId,
            RemoteUserRelationshipRecord ownerRecord,
            int partnerCharacterId,
            RemoteUserRelationshipRecord partnerRecord)
        {
            return ownerRecord.ItemId > 0
                && ownerRecord.ItemId == partnerRecord.ItemId
                && ResolveMarriagePairCharacterId(ownerCharacterId, ownerRecord) == partnerCharacterId
                && ResolveMarriagePairCharacterId(partnerCharacterId, partnerRecord) == ownerCharacterId;
        }

        private static bool RelationshipRecordTargetsCharacter(int? candidateCharacterId, int expectedCharacterId)
        {
            return !candidateCharacterId.HasValue
                || candidateCharacterId.Value <= 0
                || candidateCharacterId.Value == expectedCharacterId;
        }

        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            Vector2[] offsets =
            {
                new Vector2(-1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, -1f),
                new Vector2(0f, 1f)
            };

            foreach (Vector2 offset in offsets)
            {
                spriteBatch.DrawString(font, text, position + offset, shadowColor);
            }

            spriteBatch.DrawString(font, text, position, textColor);
        }

        private static void DrawGuildMarkIcon(
            SpriteBatch spriteBatch,
            Texture2D backgroundTexture,
            Texture2D markTexture,
            Vector2 position)
        {
            if (backgroundTexture != null)
            {
                spriteBatch.Draw(backgroundTexture, position, Color.White);
            }

            if (markTexture == null)
            {
                return;
            }

            if (backgroundTexture == null)
            {
                spriteBatch.Draw(markTexture, position, Color.White);
                return;
            }

            Vector2 markPosition = new(
                position.X + ((backgroundTexture.Width - markTexture.Width) * 0.5f),
                position.Y + ((backgroundTexture.Height - markTexture.Height) * 0.5f));
            spriteBatch.Draw(markTexture, markPosition, Color.White);
        }

        private static bool TryResolveGuildMarkTextures(
            GraphicsDevice device,
            RemoteUserActor actor,
            out Texture2D backgroundTexture,
            out Texture2D markTexture)
        {
            backgroundTexture = null;
            markTexture = null;
            CharacterBuild build = actor?.Build;
            if (device == null
                || build?.GuildMarkBackgroundId is not int backgroundId || backgroundId <= 0
                || build.GuildMarkId is not int markId || markId <= 0)
            {
                return false;
            }

            int backgroundColor = build.GuildMarkBackgroundColor ?? 1;
            int markColor = build.GuildMarkColor ?? 1;
            backgroundTexture = GuildMarkTextureCache.GetBackgroundTexture(device, backgroundId, backgroundColor);
            markTexture = GuildMarkTextureCache.GetMarkTexture(device, markId, markColor);
            return backgroundTexture != null || markTexture != null;
        }

        private static Color ResolveNameColor(RemoteUserActor actor)
        {
            return actor.BattlefieldTeamId switch
            {
                0 => new Color(255, 232, 170),
                1 => new Color(255, 189, 189),
                2 => new Color(185, 229, 255),
                _ => Color.White
            };
        }

        internal static IReadOnlyList<string> BuildWorldActorLabelLines(RemoteUserActor actor)
        {
            List<string> lines = new();
            if (actor == null)
            {
                return lines;
            }

            string guildName = actor.Build?.GuildName?.Trim();
            if (!string.IsNullOrWhiteSpace(guildName))
            {
                lines.Add(guildName);
            }

            string name = actor.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                lines.Add(name);
            }

            return lines;
        }

        internal static IReadOnlyDictionary<int, int> ResolvePortableChairPairings(
            IEnumerable<PortableChairPairParticipant> participants,
            bool preferVisibleOnly)
        {
            IEnumerable<PortableChairPairRecord> pairRecords = participants?
                .Select(static participant => new PortableChairPairRecord(
                    participant.CharacterId,
                    participant.Chair?.ItemId ?? 0,
                    participant.PreferredPairCharacterId));
            return ResolvePortableChairPairings(participants, pairRecords, preferVisibleOnly);
        }

        internal static IReadOnlyDictionary<int, int> ResolvePortableChairPairings(
            IEnumerable<PortableChairPairParticipant> participants,
            IEnumerable<PortableChairPairRecord> pairRecords,
            bool preferVisibleOnly)
        {
            Dictionary<int, int> pairs = new();
            foreach (PortableChairPairRecord record in ResolvePortableChairPairRecords(participants, pairRecords, preferVisibleOnly).Values)
            {
                if (!record.IsActive
                    || !record.PairCharacterId.HasValue
                    || pairs.ContainsKey(record.CharacterId)
                    || pairs.ContainsKey(record.PairCharacterId.Value))
                {
                    continue;
                }

                pairs[record.CharacterId] = record.PairCharacterId.Value;
                pairs[record.PairCharacterId.Value] = record.CharacterId;
            }

            return pairs;
        }

        internal static IReadOnlyDictionary<int, PortableChairPairRecord> ResolvePortableChairPairRecords(
            IEnumerable<PortableChairPairParticipant> participants,
            IEnumerable<PortableChairPairRecord> pairRecords,
            bool preferVisibleOnly)
        {
            List<PortableChairPairRecord> orderedSourceRecords =
                BuildPortableChairSourceRecordsInClientOrder(pairRecords);
            IReadOnlyDictionary<int, PortableChairPairRecord> sourceRecordMap = orderedSourceRecords
                .ToDictionary(static record => record.CharacterId);
            IReadOnlyDictionary<int, PortableChairPairParticipant> participantMap = participants?
                .Where(participant =>
                    participant.CharacterId > 0
                    && participant.Chair?.IsCoupleChair == true
                    && participant.IsChairSessionActive
                    && sourceRecordMap.TryGetValue(participant.CharacterId, out PortableChairPairRecord record)
                    && participant.Chair.ItemId == record.ItemId)
                .GroupBy(static participant => participant.CharacterId)
                .ToDictionary(static group => group.Key, static group => group.Last())
                ?? new Dictionary<int, PortableChairPairParticipant>();
            List<PortableChairPairParticipant> resolvedParticipants = orderedSourceRecords
                .Where(record => participantMap.ContainsKey(record.CharacterId))
                .Select(sourceRecord =>
                {
                    PortableChairPairParticipant participant = participantMap[sourceRecord.CharacterId];
                    int? existingPairCharacterId = ResolvePortableChairExistingPairCharacterIdForParity(sourceRecord);
                    return new PortableChairPairParticipant(
                        participant.CharacterId,
                        participant.Chair,
                        participant.Position,
                        participant.FacingRight,
                        sourceRecord.PreferredPairCharacterId,
                        existingPairCharacterId,
                        participant.IsChairSessionActive,
                        participant.IsVisibleInWorld,
                        participant.IsRelationshipOverlaySuppressed);
                })
                .ToList();
            if (resolvedParticipants.Count < 2)
            {
                return sourceRecordMap.ToDictionary(
                    static entry => entry.Key,
                    static entry => entry.Value with
                    {
                        PairCharacterId = ResolvePortableChairExistingPairCharacterIdForParity(entry.Value),
                        Status = 0
                    });
            }

            Dictionary<int, PortableChairPairRecord> resolvedRecords = sourceRecordMap.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value with
                {
                    PairCharacterId = ResolvePortableChairExistingPairCharacterIdForParity(entry.Value),
                    Status = 0
                });
            Dictionary<int, int> participantIndexByCharacterId = resolvedParticipants
                .Select((participant, index) => new { participant.CharacterId, Index = index })
                .ToDictionary(static entry => entry.CharacterId, static entry => entry.Index);

            // Preserve already-linked mutual pairs first when the authored pair is still viable.
            for (int ownerIndex = 0; ownerIndex < resolvedParticipants.Count; ownerIndex++)
            {
                PortableChairPairParticipant owner = resolvedParticipants[ownerIndex];
                if (!owner.ExistingPairCharacterId.HasValue
                    || owner.ExistingPairCharacterId.Value <= 0
                    || !participantIndexByCharacterId.TryGetValue(owner.ExistingPairCharacterId.Value, out int partnerIndex)
                    || ownerIndex == partnerIndex)
                {
                    continue;
                }

                PortableChairPairParticipant partner = resolvedParticipants[partnerIndex];
                if (partner.ExistingPairCharacterId != owner.CharacterId)
                {
                    continue;
                }

                if (!resolvedRecords.TryGetValue(owner.CharacterId, out PortableChairPairRecord ownerRecord)
                    || ownerRecord.IsActive
                    || !resolvedRecords.TryGetValue(partner.CharacterId, out PortableChairPairRecord partnerRecord)
                    || partnerRecord.IsActive
                    || !TryBuildPortableChairPairCandidate(owner, partner, preferVisibleOnly, out _))
                {
                    continue;
                }

                int status = ResolvePortableChairPairStatus(owner, partner);
                if (status == 0)
                {
                    continue;
                }

                resolvedRecords[owner.CharacterId] = ownerRecord with
                {
                    PairCharacterId = partner.CharacterId,
                    Status = status
                };
                resolvedRecords[partner.CharacterId] = partnerRecord with
                {
                    PairCharacterId = owner.CharacterId,
                    Status = status
                };
            }

            for (int ownerIndex = 0; ownerIndex < resolvedParticipants.Count - 1; ownerIndex++)
            {
                PortableChairPairParticipant owner = resolvedParticipants[ownerIndex];
                if (!resolvedRecords.TryGetValue(owner.CharacterId, out PortableChairPairRecord ownerRecord)
                    || ownerRecord.IsActive)
                {
                    continue;
                }

                if (!TryFindPortableChairPairCandidateIndex(
                        resolvedParticipants,
                        ownerIndex,
                        resolvedRecords,
                        preferVisibleOnly,
                        out int partnerIndex))
                {
                    continue;
                }

                PortableChairPairParticipant partner = resolvedParticipants[partnerIndex];
                if (!resolvedRecords.TryGetValue(partner.CharacterId, out PortableChairPairRecord partnerRecord)
                    || partnerRecord.IsActive)
                {
                    continue;
                }

                int status = ResolvePortableChairPairStatus(owner, partner);
                if (status == 0)
                {
                    continue;
                }

                resolvedRecords[owner.CharacterId] = ownerRecord with
                {
                    PairCharacterId = partner.CharacterId,
                    Status = status
                };
                resolvedRecords[partner.CharacterId] = partnerRecord with
                {
                    PairCharacterId = owner.CharacterId,
                    Status = status
                };
            }

            return resolvedRecords;
        }

        private static List<PortableChairPairRecord> BuildPortableChairSourceRecordsInClientOrder(
            IEnumerable<PortableChairPairRecord> pairRecords)
        {
            if (pairRecords == null)
            {
                return new List<PortableChairPairRecord>();
            }

            // CUserPool::OnCoupleChairRecordAdd removes any existing owner entry
            // before appending the fresh record to the couple-chair list.
            LinkedList<PortableChairPairRecord> orderedRecords = new();
            Dictionary<int, LinkedListNode<PortableChairPairRecord>> nodeByCharacterId = new();
            foreach (PortableChairPairRecord record in pairRecords)
            {
                if (record.CharacterId <= 0 || record.ItemId <= 0)
                {
                    continue;
                }

                if (nodeByCharacterId.TryGetValue(record.CharacterId, out LinkedListNode<PortableChairPairRecord> existingNode))
                {
                    orderedRecords.Remove(existingNode);
                }

                nodeByCharacterId[record.CharacterId] = orderedRecords.AddLast(record);
            }

            return orderedRecords.ToList();
        }

        private static int? ResolvePortableChairExistingPairCharacterIdForParity(
            PortableChairPairRecord sourceRecord)
        {
            if (!sourceRecord.PairCharacterId.HasValue
                || sourceRecord.PairCharacterId.Value <= 0
                || sourceRecord.PairCharacterId.Value == sourceRecord.CharacterId)
            {
                return null;
            }

            // CUserPool::Update checks candidate dwPairCharacterID directly:
            // only 0 or the current owner can be admitted as a new pair.
            return sourceRecord.PairCharacterId.Value;
        }

        private static bool TryFindPortableChairPairCandidateIndex(
            IReadOnlyList<PortableChairPairParticipant> participants,
            int ownerIndex,
            IReadOnlyDictionary<int, PortableChairPairRecord> resolvedRecords,
            bool preferVisibleOnly,
            out int candidateIndex)
        {
            candidateIndex = -1;
            if (participants == null
                || resolvedRecords == null
                || ownerIndex < 0
                || ownerIndex >= participants.Count - 1)
            {
                return false;
            }

            PortableChairPairParticipant owner = participants[ownerIndex];
            int preferredPairCandidateIndex = -1;
            int fallbackCandidateIndex = -1;
            for (int index = ownerIndex + 1; index < participants.Count; index++)
            {
                PortableChairPairParticipant candidate = participants[index];
                if (!resolvedRecords.TryGetValue(candidate.CharacterId, out PortableChairPairRecord candidateRecord)
                    || candidateRecord.IsActive
                    || !TryBuildPortableChairPairCandidate(owner, candidate, preferVisibleOnly, out _))
                {
                    continue;
                }

                if (owner.PreferredPairCharacterId == candidate.CharacterId
                    && preferredPairCandidateIndex < 0)
                {
                    preferredPairCandidateIndex = index;
                }

                if (fallbackCandidateIndex < 0)
                {
                    // CUserPool::Update scans the couple-chair list in order and keeps
                    // the first viable partner unless an explicit pair target is valid.
                    fallbackCandidateIndex = index;
                }
            }

            candidateIndex = preferredPairCandidateIndex >= 0
                ? preferredPairCandidateIndex
                : fallbackCandidateIndex;
            return candidateIndex >= 0;
        }

        private Dictionary<int, int> BuildPortableChairPairMap(PlayerCharacter localPlayer, bool preferVisibleOnly)
        {
            List<PortableChairPairParticipant> participants = new(_actorsById.Count + 1);
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                PortableChair chair = actor?.Build?.ActivePortableChair;
                if (chair?.IsCoupleChair != true)
                {
                    continue;
                }

                participants.Add(new PortableChairPairParticipant(
                    actor.CharacterId,
                    chair,
                    actor.Position,
                    actor.FacingRight,
                    actor.PreferredPortableChairPairCharacterId,
                    null,
                    IsPortableChairPairSessionActive(actor.Build?.ActivePortableChair, actor.ActionName, actor.BaseActionName),
                    actor.IsVisibleInWorld,
                    IsRelationshipOverlaySuppressed(actor)));
            }

            PortableChair localChair = localPlayer?.Build?.ActivePortableChair;
            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            if (localChair?.IsCoupleChair == true
                && localPlayer.IsAlive
                && localCharacterId > 0)
            {
                participants.Add(new PortableChairPairParticipant(
                    localCharacterId,
                    localChair,
                    localPlayer.Position,
                    localPlayer.FacingRight,
                    null,
                    null,
                    localPlayer.State == PlayerState.Sitting
                        && IsPortableChairPairSessionActive(localChair, localPlayer.CurrentActionName),
                    true,
                    IsRelationshipOverlaySuppressed(localPlayer, localCharacterId, ownerCharacterId: 0)));
            }

            IReadOnlyDictionary<int, PortableChairPairRecord> pairRecords = ResolvePortableChairPairRecords(
                participants,
                BuildPortableChairPairRecords(localPlayer),
                preferVisibleOnly);
            SyncResolvedPortableChairPairRecords(pairRecords);

            Dictionary<int, int> pairMap = new();
            foreach ((int characterId, PortableChairPairRecord record) in pairRecords)
            {
                if (!record.IsActive || !record.PairCharacterId.HasValue || pairMap.ContainsKey(characterId))
                {
                    continue;
                }

                pairMap[characterId] = record.PairCharacterId.Value;
            }

            return pairMap;
        }

        private IEnumerable<PortableChairPairRecord> BuildPortableChairPairRecords(PlayerCharacter localPlayer)
        {
            foreach (PortableChairPairRecord record in _portableChairPairRecordsByCharacterId.Values)
            {
                yield return record with
                {
                    PreferredPairCharacterId = ResolvePortableChairPairPreference(record.CharacterId) ?? record.PreferredPairCharacterId
                };
            }

            PortableChair localChair = localPlayer?.Build?.ActivePortableChair;
            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            if (localChair?.IsCoupleChair == true && localCharacterId > 0)
            {
                yield return new PortableChairPairRecord(
                    localCharacterId,
                    localChair.ItemId,
                    _localPortableChairPreferredPairCharacterId);
            }
        }

        private int? ResolvePortableChairPairPreference(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                return actor.PreferredPortableChairPairCharacterId;
            }

            return null;
        }

        private void SyncPortableChairPairRecord(
            int characterId,
            int chairItemId,
            int? preferredPairCharacterId,
            int? pairCharacterId = null,
            int? status = null)
        {
            if (characterId <= 0 || chairItemId <= 0)
            {
                ClearPortableChairPairRecord(characterId);
                return;
            }

            int? resolvedPairCharacterId = pairCharacterId.HasValue
                && pairCharacterId.Value > 0
                && pairCharacterId.Value != characterId
                ? pairCharacterId
                : null;
            int resolvedStatus = Math.Max(0, status ?? 0);
            _portableChairPairRecordsByCharacterId[characterId] = new PortableChairPairRecord(
                characterId,
                chairItemId,
                preferredPairCharacterId,
                resolvedPairCharacterId,
                resolvedStatus);
        }

        private void SyncPortableChairPairRecordFromSeatState(
            int characterId,
            int chairItemId,
            int? preferredPairCharacterId,
            bool allowCreateRecordFromSeatState = true)
        {
            _portableChairPairRecordsByCharacterId.TryGetValue(characterId, out PortableChairPairRecord existingRecord);
            PortableChairPairRecord? resolvedRecord = ResolvePortableChairPairRecordFromSeatStateForParity(
                _portableChairPairRecordsByCharacterId.ContainsKey(characterId)
                    ? existingRecord
                    : null,
                characterId,
                chairItemId,
                preferredPairCharacterId,
                allowCreateRecordFromSeatState);
            if (resolvedRecord.HasValue)
            {
                _portableChairPairRecordsByCharacterId[characterId] = resolvedRecord.Value;
                return;
            }

            ClearPortableChairPairRecord(characterId);
        }

        internal static PortableChairPairRecord? ResolvePortableChairPairRecordFromSeatStateForParity(
            PortableChairPairRecord? existingRecord,
            int characterId,
            int chairItemId,
            int? preferredPairCharacterId,
            bool allowCreateRecordFromSeatState)
        {
            if (characterId <= 0)
            {
                return null;
            }

            if (!IsCoupleChairRecordItemIdForClientParity(chairItemId))
            {
                if (!allowCreateRecordFromSeatState && existingRecord.HasValue)
                {
                    return existingRecord.Value with
                    {
                        PreferredPairCharacterId = preferredPairCharacterId
                    };
                }

                return null;
            }

            if (existingRecord.HasValue)
            {
                PortableChairPairRecord record = existingRecord.Value;
                if (!allowCreateRecordFromSeatState)
                {
                    return record with
                    {
                        PreferredPairCharacterId = preferredPairCharacterId
                    };
                }
            }

            if (!allowCreateRecordFromSeatState)
            {
                return null;
            }

            return new PortableChairPairRecord(
                characterId,
                chairItemId,
                preferredPairCharacterId);
        }

        internal static bool IsCoupleChairRecordItemIdForClientParity(int chairItemId)
        {
            return chairItemId > 0 && chairItemId / 1000 == 3012;
        }

        internal static RemoteUserPortableChairRecordAddPacket NormalizePortableChairRecordAddForClientParity(
            RemoteUserPortableChairRecordAddPacket packet)
        {
            // CUserPool::OnCoupleChairRecordAdd initializes records as:
            // dwPairCharacterID = 0, nStatus = 0.
            return packet with
            {
                PairCharacterId = null,
                Status = 0
            };
        }

        internal static bool ShouldClearPortableChairPartnerRecordOnRemoveForParity(PortableChairPairRecord record)
        {
            // CUserPool::OnCoupleChairRecordRemove gates the partner clear on
            // both nStatus and dwPairCharacterID. Inactive raw locks survive
            // until the owner record itself is removed.
            return record.IsActive;
        }

        private void ClearPortableChairPairRecord(int characterId)
        {
            if (characterId > 0)
            {
                _portableChairPairRecordsByCharacterId.Remove(characterId);
            }
        }

        private void SyncResolvedPortableChairPairRecords(IReadOnlyDictionary<int, PortableChairPairRecord> pairRecords)
        {
            if (pairRecords == null)
            {
                return;
            }

            foreach ((int characterId, PortableChairPairRecord record) in pairRecords)
            {
                if (!_portableChairPairRecordsByCharacterId.TryGetValue(characterId, out PortableChairPairRecord existingRecord))
                {
                    continue;
                }

                _portableChairPairRecordsByCharacterId[characterId] = existingRecord with
                {
                    PairCharacterId = record.PairCharacterId,
                    Status = record.Status
                };
            }
        }

        private static bool TryBuildPortableChairPairCandidate(
            PortableChairPairParticipant left,
            PortableChairPairParticipant right,
            bool preferVisibleOnly,
            out PortableChairPairRecord candidate)
        {
            candidate = default;
            if (left.CharacterId == right.CharacterId
                || left.Chair?.IsCoupleChair != true
                || right.Chair?.IsCoupleChair != true
                || left.Chair.ItemId <= 0
                || left.Chair.ItemId != right.Chair.ItemId
                || !left.IsChairSessionActive
                || !right.IsChairSessionActive
                || (preferVisibleOnly && (!left.IsVisibleInWorld || !right.IsVisibleInWorld))
                || left.IsRelationshipOverlaySuppressed
                || right.IsRelationshipOverlaySuppressed
                || !CanReusePortableChairPairRecord(left, right)
                || !PlayerCharacter.IsPortableChairActualPairActive(
                    left.Chair,
                    left.FacingRight,
                    left.Position.X,
                    left.Position.Y,
                    right.FacingRight,
                    right.Position.X,
                    right.Position.Y))
            {
                return false;
            }

            candidate = new PortableChairPairRecord(
                left.CharacterId,
                left.Chair.ItemId,
                left.PreferredPairCharacterId,
                PairCharacterId: right.CharacterId,
                Status: ResolvePortableChairPairStatus(left, right));
            return true;
        }

        private static bool CanReusePortableChairPairRecord(
            PortableChairPairParticipant owner,
            PortableChairPairParticipant candidate)
        {
            return !candidate.ExistingPairCharacterId.HasValue
                   || candidate.ExistingPairCharacterId.Value <= 0
                   || candidate.ExistingPairCharacterId.Value == owner.CharacterId;
        }

        private static int ResolvePortableChairPairStatus(
            PortableChairPairParticipant left,
            PortableChairPairParticipant right)
        {
            return PlayerCharacter.IsPortableChairActualPairActive(
                left.Chair,
                left.FacingRight,
                left.Position.X,
                left.Position.Y,
                right.FacingRight,
                right.Position.X,
                right.Position.Y)
                ? 3
                : 0;
        }

        internal static bool IsPortableChairPairSessionActive(PortableChair chair, params string[] actionCandidates)
        {
            if (chair?.IsCoupleChair != true)
            {
                return false;
            }

            if (actionCandidates == null || actionCandidates.Length == 0)
            {
                return true;
            }

            foreach (string actionName in actionCandidates)
            {
                if (IsPortableChairSeatAction(actionName, chair))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPortableChairSeatAction(string actionName, PortableChair chair)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (actionName.StartsWith("sit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(
                actionName,
                PlayerCharacter.ResolvePortableChairActionName(chair),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool TryResolvePortableChairPairWithLocalPlayer(
            RemoteUserActor actor,
            PortableChair chair,
            PlayerCharacter localPlayer,
            out Vector2 partnerPosition,
            out bool partnerFacingRight)
        {
            partnerPosition = Vector2.Zero;
            partnerFacingRight = false;
            if (actor == null
                || chair?.IsCoupleChair != true
                || localPlayer?.Build == null
                || !localPlayer.IsAlive)
            {
                return false;
            }

            Dictionary<int, int> pairMap = BuildPortableChairPairMap(localPlayer, preferVisibleOnly: true);
            int localCharacterId = localPlayer.Build.Id;
            if (!pairMap.TryGetValue(actor.CharacterId, out int pairCharacterId)
                || pairCharacterId != localCharacterId)
            {
                return false;
            }

            partnerPosition = localPlayer.Position;
            partnerFacingRight = localPlayer.FacingRight;
            return true;
        }

        private bool TryResolvePortableChairOwnerForLocalPlayer(PlayerCharacter localPlayer, out RemoteUserActor ownerActor)
        {
            return TryResolvePortableChairOwnerForLocalPlayer(
                localPlayer,
                BuildPortableChairPairMap(localPlayer, preferVisibleOnly: true),
                out ownerActor);
        }

        private bool TryResolvePortableChairOwnerForLocalPlayer(
            PlayerCharacter localPlayer,
            IReadOnlyDictionary<int, int> pairMap,
            out RemoteUserActor ownerActor)
        {
            ownerActor = null;
            if (localPlayer?.Build == null || !localPlayer.IsAlive)
            {
                return false;
            }

            int localCharacterId = localPlayer.Build.Id;
            if (localCharacterId <= 0)
            {
                return false;
            }

            return pairMap != null
                   && pairMap.TryGetValue(localCharacterId, out int ownerCharacterId)
                   && ownerCharacterId != localCharacterId
                   && _actorsById.TryGetValue(ownerCharacterId, out ownerActor)
                   && ownerActor.IsVisibleInWorld;
        }

        private RemoteUserActor FindPortableChairPairActor(
            PortableChair chair,
            int ownerCharacterId,
            bool ownerFacingRight,
            float ownerX,
            float ownerY,
            int skipCharacterId,
            bool preferVisibleOnly)
        {
            if (chair?.IsCoupleChair != true)
            {
                return null;
            }

            Dictionary<int, int> pairMap = BuildPortableChairPairMap(localPlayer: null, preferVisibleOnly);
            return pairMap.TryGetValue(ownerCharacterId, out int pairCharacterId)
                   && pairCharacterId != skipCharacterId
                   && _actorsById.TryGetValue(pairCharacterId, out RemoteUserActor pairActor)
                ? pairActor
                : null;
        }

        private void SyncBattlefieldAppearance(RemoteUserActor actor, BattlefieldField battlefield)
        {
            if (actor?.Build == null)
            {
                return;
            }

            int? assignedTeamId = battlefield != null
                && battlefield.TryGetAssignedTeamId(actor.CharacterId, out int resolvedTeamId)
                ? resolvedTeamId
                : actor.BattlefieldTeamId;
            if (battlefield?.IsActive != true)
            {
                RestoreBattlefieldAppearance(actor, clearTeamId: false);
                return;
            }

            if (actor.BattlefieldAppliedTeamId == assignedTeamId
                && (!assignedTeamId.HasValue || actor.BattlefieldOriginalEquipment != null))
            {
                return;
            }

            if (!assignedTeamId.HasValue
                || !battlefield.TryGetAssignedTeamLookPreset(actor.CharacterId, out BattlefieldField.BattlefieldTeamLookPreset preset))
            {
                RestoreBattlefieldAppearance(actor, clearTeamId: false);
                actor.BattlefieldAppliedTeamId = assignedTeamId;
                actor.BattlefieldTeamId = assignedTeamId;
                return;
            }

            EnsureBattlefieldOriginalAppearanceSnapshot(actor);
            if (actor.BattlefieldOriginalSpeed == null)
            {
                actor.BattlefieldOriginalSpeed = actor.Build.Speed;
            }

            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                actor.Build.Unequip(slot);
            }

            if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Longcoat))
            {
                actor.Build.Unequip(EquipSlot.Coat);
                actor.Build.Unequip(EquipSlot.Pants);
            }
            else if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Coat))
            {
                actor.Build.Unequip(EquipSlot.Longcoat);
            }

            foreach (KeyValuePair<EquipSlot, int> entry in preset.EquipmentItemIds)
            {
                CharacterPart part = _loader?.LoadEquipment(entry.Value);
                if (part != null)
                {
                    actor.Build.Equip(part);
                }
            }

            if (preset.MoveSpeed.HasValue)
            {
                actor.Build.Speed = preset.MoveSpeed.Value;
            }

            actor.RefreshAssembler();
            actor.BattlefieldAppliedTeamId = assignedTeamId;
            actor.BattlefieldTeamId = assignedTeamId;
        }

        private static void EnsureBattlefieldOriginalAppearanceSnapshot(RemoteUserActor actor)
        {
            if (actor.BattlefieldOriginalEquipment != null || actor?.Build == null)
            {
                return;
            }

            actor.BattlefieldOriginalEquipment = new Dictionary<EquipSlot, CharacterPart>();
            actor.BattlefieldOriginalSpeed = actor.Build.Speed;
            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                if (actor.Build.Equipment.TryGetValue(slot, out CharacterPart part) && part != null)
                {
                    actor.BattlefieldOriginalEquipment[slot] = part;
                }
            }
        }

        private static void RestoreBattlefieldAppearance(RemoteUserActor actor, bool clearTeamId)
        {
            if (actor?.Build == null)
            {
                return;
            }

            if (actor.BattlefieldOriginalEquipment == null)
            {
                actor.BattlefieldAppliedTeamId = null;
                if (clearTeamId)
                {
                    actor.BattlefieldTeamId = null;
                }
                return;
            }

            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                actor.Build.Unequip(slot);
            }

            foreach (KeyValuePair<EquipSlot, CharacterPart> entry in actor.BattlefieldOriginalEquipment)
            {
                actor.Build.Equip(entry.Value);
            }

            if (actor.BattlefieldOriginalSpeed.HasValue)
            {
                actor.Build.Speed = actor.BattlefieldOriginalSpeed.Value;
            }

            actor.RefreshAssembler();
            ResetBattlefieldAppearanceState(actor);
            if (clearTeamId)
            {
                actor.BattlefieldTeamId = null;
            }
        }

        private static void ResetBattlefieldAppearanceState(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.BattlefieldOriginalEquipment = null;
            actor.BattlefieldOriginalSpeed = null;
            actor.BattlefieldAppliedTeamId = null;
        }

        private static string NormalizeActionName(string actionName, bool allowSitFallback)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return allowSitFallback
                    ? "sit"
                    : CharacterPart.GetActionString(CharacterAction.Stand1);
            }

            return actionName.Trim();
        }

        private void SyncTemporaryStatPresentation(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            RemoteUserTemporaryStatKnownState knownState = actor.TemporaryStats.KnownState;
            int currentTime = Environment.TickCount;
            UpdateRemoteTemporaryStatAffectedLayerShiftCadenceFrameDuration(actor, currentTime);
            int temporaryStatAnimationStartTime = ResolveRemoteTemporaryStatAnimationStartTime(
                currentTime,
                actor.TemporaryStatDelay);
            bool reseedTimelineFromPacket = actor.PendingTemporaryStatTimelineReseed;
            actor.PendingTemporaryStatTimelineReseed = false;
            CharacterPart overrideAvatarPart = null;
            CharacterPart overrideTamingMobPart = null;
            if (knownState.MorphId is int morphTemplateId && morphTemplateId > 0)
            {
                overrideAvatarPart = _loader?.LoadMorph(morphTemplateId);
                if (overrideAvatarPart?.Type != CharacterPartType.Morph)
                {
                    overrideAvatarPart = actor.TemporaryStatAvatarOverridePart?.Type == CharacterPartType.Morph
                        && actor.TemporaryStatAvatarOverridePart.ItemId == morphTemplateId
                        ? actor.TemporaryStatAvatarOverridePart
                        : null;
                }
            }

            if (TryResolveMechanicTamingMobOverrideItemId(knownState, actor.BaseActionName, out int mechanicTamingMobItemId))
            {
                overrideTamingMobPart = _loader?.LoadEquipment(mechanicTamingMobItemId);
                if (overrideTamingMobPart?.Slot != EquipSlot.TamingMob)
                {
                    overrideTamingMobPart = actor.TemporaryStatTamingMobOverridePart?.Slot == EquipSlot.TamingMob
                        && actor.TemporaryStatTamingMobOverridePart.ItemId == mechanicTamingMobItemId
                        ? actor.TemporaryStatTamingMobOverridePart
                        : null;
                }
            }

            actor.TemporaryStatAvatarOverridePart = overrideAvatarPart;
            actor.TemporaryStatTamingMobOverridePart = overrideTamingMobPart;
            int? previousShadowPartnerSkillId = actor.TemporaryStatShadowPartnerSkillId;
            ResolveRemoteShadowPartnerSkill(actor, knownState, out int? shadowPartnerSkillId, out SkillData shadowPartnerSkill);
            actor.TemporaryStatShadowPartnerSkillId = shadowPartnerSkillId;
            actor.TemporaryStatShadowPartnerSkill = shadowPartnerSkill;
            if (previousShadowPartnerSkillId != shadowPartnerSkillId)
            {
                actor.ShadowPartnerPresentation = null;
            }
            ResolveRemoteSoulArrowSkill(actor, knownState, out int? soulArrowSkillId, out SkillData soulArrowSkill);
            actor.TemporaryStatSoulArrowEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor,
                actor.TemporaryStatSoulArrowEffect,
                RemoteTemporaryStatAvatarEffectOwnerFamily.SoulArrow,
                soulArrowSkillId,
                soulArrowSkill,
                temporaryStatAnimationStartTime,
                reseedTimelineFromPacket);
            ResolveRemoteWeaponChargeSkill(actor, knownState, out int? weaponChargeSkillId, out SkillData weaponChargeSkill);
            actor.TemporaryStatWeaponChargeEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor,
                actor.TemporaryStatWeaponChargeEffect,
                RemoteTemporaryStatAvatarEffectOwnerFamily.WeaponCharge,
                weaponChargeSkillId,
                weaponChargeSkill,
                temporaryStatAnimationStartTime,
                reseedTimelineFromPacket);
            ResolveRemoteAuraSkill(actor, knownState, out int? auraSkillId, out SkillData auraSkill);
            RemoteTemporaryStatAvatarEffectState previousAuraState = actor.TemporaryStatAuraEffect;
            actor.TemporaryStatAuraEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor,
                actor.TemporaryStatAuraEffect,
                RemoteTemporaryStatAvatarEffectOwnerFamily.Aura,
                auraSkillId,
                auraSkill,
                temporaryStatAnimationStartTime,
                reseedTimelineFromPacket);
            PruneExpiredRemoteTemporaryStatAvatarEffectTailStates(actor.TemporaryStatAuraEffectTails, currentTime);
            int auraTailShiftCadenceUpdateCount = ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountForAuraTailTransition(
                previousAuraState,
                actor.TemporaryStatAuraEffect,
                actor.TemporaryStatAffectedLayerShiftCadenceUpdateCount);
            actor.TemporaryStatAffectedLayerShiftCadenceUpdateCount = auraTailShiftCadenceUpdateCount;

            if (TryCreateRemoteTemporaryStatAvatarEffectTailState(
                    previousAuraState,
                    actor.TemporaryStatAuraEffect,
                    currentTime,
                    actor.TemporaryStatAffectedLayerShiftCadenceFrameDurationMs,
                    auraTailShiftCadenceUpdateCount,
                    out RemoteTemporaryStatAvatarEffectState auraTailState))
            {
                AppendRemoteTemporaryStatAuraTailState(actor.TemporaryStatAuraEffectTails, auraTailState);
            }

            ResolveRemoteMoreWildDamageUpSkill(knownState, out int? moreWildDamageUpSkillId, out SkillData moreWildDamageUpSkill);
            actor.TemporaryStatMoreWildEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor,
                actor.TemporaryStatMoreWildEffect,
                RemoteTemporaryStatAvatarEffectOwnerFamily.MoreWild,
                moreWildDamageUpSkillId,
                moreWildDamageUpSkill,
                temporaryStatAnimationStartTime,
                reseedTimelineFromPacket);
            ResolveRemoteBarrierSkill(actor, knownState, out int? barrierSkillId, out SkillData barrierSkill);
            actor.TemporaryStatBarrierEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor,
                actor.TemporaryStatBarrierEffect,
                RemoteTemporaryStatAvatarEffectOwnerFamily.Barrier,
                barrierSkillId,
                barrierSkill,
                temporaryStatAnimationStartTime,
                reseedTimelineFromPacket);
            ResolveRemoteBlessingArmorSkill(actor, knownState, out int? blessingArmorSkillId, out SkillData blessingArmorSkill);
            actor.TemporaryStatBlessingArmorEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor,
                actor.TemporaryStatBlessingArmorEffect,
                RemoteTemporaryStatAvatarEffectOwnerFamily.BlessingArmor,
                blessingArmorSkillId,
                blessingArmorSkill,
                temporaryStatAnimationStartTime,
                reseedTimelineFromPacket);
            ResolveRemoteRepeatEffectSkill(actor, knownState, out int? repeatEffectSkillId, out SkillData repeatEffectSkill);
            actor.TemporaryStatRepeatEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor,
                actor.TemporaryStatRepeatEffect,
                RemoteTemporaryStatAvatarEffectOwnerFamily.RepeatEffect,
                repeatEffectSkillId,
                repeatEffectSkill,
                temporaryStatAnimationStartTime,
                reseedTimelineFromPacket);
            ResolveRemoteMagicShieldSkill(actor, knownState, out int? magicShieldSkillId, out SkillData magicShieldSkill);
            actor.TemporaryStatMagicShieldEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor,
                actor.TemporaryStatMagicShieldEffect,
                RemoteTemporaryStatAvatarEffectOwnerFamily.MagicShield,
                magicShieldSkillId,
                magicShieldSkill,
                temporaryStatAnimationStartTime,
                reseedTimelineFromPacket);
            ResolveRemoteFinalCutSkill(actor, knownState, out int? finalCutSkillId, out SkillData finalCutSkill);
            actor.TemporaryStatFinalCutEffect = UpdateRemoteTemporaryStatAvatarEffectState(
                actor,
                actor.TemporaryStatFinalCutEffect,
                RemoteTemporaryStatAvatarEffectOwnerFamily.FinalCut,
                finalCutSkillId,
                finalCutSkill,
                temporaryStatAnimationStartTime,
                reseedTimelineFromPacket);
            actor.HasMorphTemplate = overrideAvatarPart?.Type == CharacterPartType.Morph;
            actor.HiddenLikeClient = knownState.IsHiddenLikeClient;
            actor.ActionName = ResolveClientVisibleActionName(actor.BaseActionName, knownState);
            actor.RidingVehicleId = ResolveRemoteRidingVehicleId(actor);
            actor.RefreshAssembler();
        }

        private static int ResolveRemoteTemporaryStatAnimationStartTime(int currentTime, ushort delayMs)
        {
            return delayMs == 0
                ? currentTime
                : unchecked(currentTime - delayMs);
        }

        private static void UpdateRemoteTemporaryStatAffectedLayerShiftCadenceFrameDuration(RemoteUserActor actor, int currentTime)
        {
            if (actor == null)
            {
                return;
            }

            actor.TemporaryStatAffectedLayerShiftCadenceUpdateCount =
                actor.TemporaryStatAffectedLayerShiftCadenceUpdateCount == int.MaxValue
                    ? 1
                    : actor.TemporaryStatAffectedLayerShiftCadenceUpdateCount + 1;

            int previousSyncTime = actor.TemporaryStatAffectedLayerLastSyncTime;
            actor.TemporaryStatAffectedLayerLastSyncTime = currentTime;
            if (previousSyncTime == int.MinValue)
            {
                return;
            }

            int observedDeltaMs = unchecked(currentTime - previousSyncTime);
            if (observedDeltaMs <= 0 || observedDeltaMs > 1000)
            {
                return;
            }

            actor.TemporaryStatAffectedLayerShiftCadenceFrameDurationMs =
                ResolveRemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationMs(observedDeltaMs);
        }

        private static int ResolveRemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationMs(int observedDeltaMs)
        {
            if (observedDeltaMs <= 0)
            {
                return RemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationFallbackMs;
            }

            return Math.Clamp(
                observedDeltaMs,
                RemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationMinimumMs,
                RemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationMaximumMs);
        }

        private static bool ShouldResetRemoteTemporaryStatAffectedLayerShiftCadenceForAuraReplacement(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState)
        {
            if (currentState == null
                || !HasAnyRemoteTemporaryStatAvatarEffectAnimation(currentState))
            {
                return false;
            }

            return previousState == null || previousState.SkillId != currentState.SkillId;
        }

        private static int ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountAfterForcedShift(
            int currentUpdateCount)
        {
            return 1;
        }

        private static int ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountForAuraTailTransition(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState,
            int currentUpdateCount)
        {
            return ShouldResetRemoteTemporaryStatAffectedLayerShiftCadenceForAuraReplacement(previousState, currentState)
                ? ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountAfterForcedShift(currentUpdateCount)
                : currentUpdateCount;
        }

        internal static int ResolveRemoteTemporaryStatAnimationStartTimeForTesting(int currentTime, ushort delayMs)
        {
            return ResolveRemoteTemporaryStatAnimationStartTime(currentTime, delayMs);
        }

        private static int ResolveRemoteTemporaryStatAvatarEffectAnimationStartTime(
            int existingSkillId,
            int nextSkillId,
            int existingStartTime,
            int nextStartTime,
            bool reseedTimelineFromPacket)
        {
            return existingSkillId == nextSkillId && !reseedTimelineFromPacket
                ? existingStartTime
                : nextStartTime;
        }

        internal static int ResolveRemoteTemporaryStatAvatarEffectAnimationStartTimeForTesting(
            int existingSkillId,
            int nextSkillId,
            int existingStartTime,
            int nextStartTime,
            bool reseedTimelineFromPacket)
        {
            return ResolveRemoteTemporaryStatAvatarEffectAnimationStartTime(
                existingSkillId,
                nextSkillId,
                existingStartTime,
                nextStartTime,
                reseedTimelineFromPacket);
        }

        private static string ResolveRemoteTemporaryStatAvatarEffectOwnerName(RemoteTemporaryStatAvatarEffectOwnerFamily family)
        {
            return family switch
            {
                RemoteTemporaryStatAvatarEffectOwnerFamily.SoulArrow => RemoteTemporaryStatSoulArrowActionOwnerName,
                RemoteTemporaryStatAvatarEffectOwnerFamily.WeaponCharge => RemoteTemporaryStatWeaponChargeActionOwnerName,
                RemoteTemporaryStatAvatarEffectOwnerFamily.Aura => RemoteTemporaryStatAuraActionOwnerName,
                RemoteTemporaryStatAvatarEffectOwnerFamily.MoreWild => RemoteTemporaryStatMoreWildActionOwnerName,
                RemoteTemporaryStatAvatarEffectOwnerFamily.Barrier => RemoteTemporaryStatBarrierActionOwnerName,
                RemoteTemporaryStatAvatarEffectOwnerFamily.BlessingArmor => RemoteTemporaryStatBlessingArmorActionOwnerName,
                RemoteTemporaryStatAvatarEffectOwnerFamily.RepeatEffect => RemoteTemporaryStatRepeatEffectActionOwnerName,
                RemoteTemporaryStatAvatarEffectOwnerFamily.MagicShield => RemoteTemporaryStatMagicShieldActionOwnerName,
                _ => RemoteTemporaryStatFinalCutActionOwnerName
            };
        }

        private static string ResolveRemoteTemporaryStatAvatarEffectActionName(RemoteUserActor actor)
        {
            if (!string.IsNullOrWhiteSpace(actor?.ActionName))
            {
                return actor.ActionName;
            }

            return CharacterPart.GetActionString(CharacterAction.Stand1);
        }

        private static int ResolveRemotePacketOwnedEmotionCounterSkillId(int itemId, int emotionId, bool byItemOption)
        {
            unchecked
            {
                return (itemId * 131)
                    ^ (emotionId * 17)
                    ^ (byItemOption ? 0x4000_0000 : 0);
            }
        }

        private static int ResolveRemoteActiveEffectMotionBlurCounterSkillId(int itemId)
        {
            unchecked
            {
                return (itemId * 131) ^ 0x1A00_0000;
            }
        }

        private static int ResolveRemoteActiveEffectItemEffectCounterSkillId(int itemId)
        {
            unchecked
            {
                return (itemId * 131) ^ 0x1B00_0000;
            }
        }

        private static int ResolveRemoteQuestDeliveryCounterSkillId(int itemId)
        {
            unchecked
            {
                return (itemId * 131) ^ 0x1900_0000;
            }
        }

        private static string ResolveRemoteRelationshipOverlayActionOwnerName(RemoteRelationshipOverlayType relationshipType)
        {
            return relationshipType switch
            {
                RemoteRelationshipOverlayType.Couple => RemoteRelationshipOverlayCoupleActionOwnerName,
                RemoteRelationshipOverlayType.Friendship => RemoteRelationshipOverlayFriendshipActionOwnerName,
                RemoteRelationshipOverlayType.NewYearCard => RemoteRelationshipOverlayNewYearCardActionOwnerName,
                RemoteRelationshipOverlayType.Marriage => RemoteRelationshipOverlayMarriageActionOwnerName,
                _ => RemoteRelationshipOverlayGenericActionOwnerName
            };
        }

        private static int ResolveRemoteRelationshipOverlayCounterSkillId(
            RemoteRelationshipOverlayType relationshipType,
            int itemId)
        {
            unchecked
            {
                return (itemId * 131)
                    ^ ((int)relationshipType * 67)
                    ^ 0x1800_0000;
            }
        }

        private static bool IsRemoteAuxiliaryLayerOwnerCounterContextMatch(
            RemoteAuxiliaryLayerOwnerCounterState ownerCounter,
            int skillId,
            string actionName,
            bool facingRight)
        {
            return ownerCounter != null
                   && ownerCounter.SkillId == skillId
                   && ownerCounter.FacingRight == facingRight
                   && string.Equals(ownerCounter.ActionName, actionName, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryRestoreRemoteAuxiliaryLayerOwnerCounter(
            int characterId,
            string ownerName,
            int skillId,
            string actionName,
            bool facingRight,
            out int animationElapsedMs)
        {
            animationElapsedMs = 0;
            if (characterId <= 0
                || string.IsNullOrWhiteSpace(ownerName)
                || !_remoteAuxiliaryLayerOwnerCountersByCharacterId.TryGetValue(
                    characterId,
                    out Dictionary<string, RemoteAuxiliaryLayerOwnerCounterState> ownerCounters)
                || ownerCounters == null
                || !ownerCounters.TryGetValue(ownerName, out RemoteAuxiliaryLayerOwnerCounterState ownerCounter)
                || !IsRemoteAuxiliaryLayerOwnerCounterContextMatch(ownerCounter, skillId, actionName, facingRight))
            {
                return false;
            }

            animationElapsedMs = Math.Max(0, ownerCounter.AnimationElapsedMs);
            return true;
        }

        private void StoreRemoteAuxiliaryLayerOwnerCounter(
            int characterId,
            string ownerName,
            int skillId,
            string actionName,
            bool facingRight,
            int animationElapsedMs,
            int lastUpdateTimeMs)
        {
            if (characterId <= 0 || string.IsNullOrWhiteSpace(ownerName))
            {
                return;
            }

            if (!_remoteAuxiliaryLayerOwnerCountersByCharacterId.TryGetValue(
                    characterId,
                    out Dictionary<string, RemoteAuxiliaryLayerOwnerCounterState> ownerCounters))
            {
                ownerCounters = new Dictionary<string, RemoteAuxiliaryLayerOwnerCounterState>(
                    StringComparer.OrdinalIgnoreCase);
                _remoteAuxiliaryLayerOwnerCountersByCharacterId[characterId] = ownerCounters;
            }

            if (!ownerCounters.TryGetValue(ownerName, out RemoteAuxiliaryLayerOwnerCounterState ownerCounter))
            {
                ownerCounter = new RemoteAuxiliaryLayerOwnerCounterState();
                ownerCounters[ownerName] = ownerCounter;
            }

            ownerCounter.SkillId = skillId;
            ownerCounter.ActionName = actionName;
            ownerCounter.FacingRight = facingRight;
            ownerCounter.AnimationElapsedMs = Math.Max(0, animationElapsedMs);
            ownerCounter.LastUpdateTimeMs = lastUpdateTimeMs;
        }

        internal static string ResolveRemoteTemporaryStatAvatarEffectOwnerNameForTesting(int familyCode)
        {
            RemoteTemporaryStatAvatarEffectOwnerFamily family = familyCode switch
            {
                0 => RemoteTemporaryStatAvatarEffectOwnerFamily.SoulArrow,
                1 => RemoteTemporaryStatAvatarEffectOwnerFamily.WeaponCharge,
                2 => RemoteTemporaryStatAvatarEffectOwnerFamily.Aura,
                3 => RemoteTemporaryStatAvatarEffectOwnerFamily.MoreWild,
                4 => RemoteTemporaryStatAvatarEffectOwnerFamily.Barrier,
                5 => RemoteTemporaryStatAvatarEffectOwnerFamily.BlessingArmor,
                6 => RemoteTemporaryStatAvatarEffectOwnerFamily.RepeatEffect,
                7 => RemoteTemporaryStatAvatarEffectOwnerFamily.MagicShield,
                _ => RemoteTemporaryStatAvatarEffectOwnerFamily.FinalCut
            };

            return ResolveRemoteTemporaryStatAvatarEffectOwnerName(family);
        }

        internal static string ResolveRemotePacketOwnedEmotionOwnerNameForTesting()
        {
            return RemotePacketOwnedEmotionActionOwnerName;
        }

        internal static string ResolveRemoteEffectByItemOwnerNameForTesting()
        {
            return RemoteEffectByItemActionOwnerName;
        }

        internal static string ResolveRemoteCarryItemEffectOwnerNameForTesting()
        {
            return RemoteCarryItemEffectActionOwnerName;
        }

        internal static string ResolveRemoteCompletedSetItemEffectOwnerNameForTesting()
        {
            return RemoteCompletedSetItemEffectActionOwnerName;
        }

        internal static void UpdateCarryItemEffectLayerAdmissionForTesting(RemoteUserActor actor)
        {
            UpdateCarryItemEffectLayerAdmissionForParity(actor);
        }

        internal static void UpdateCompletedSetItemEffectLayerAdmissionForTesting(RemoteUserActor actor)
        {
            UpdateCompletedSetItemEffectLayerAdmissionForParity(actor);
        }

        internal static string ResolveRemoteQuestDeliveryOwnerNameForTesting()
        {
            return RemoteQuestDeliveryActionOwnerName;
        }

        internal static string ResolveRemoteActiveEffectMotionBlurOwnerNameForTesting()
        {
            return RemoteActiveEffectMotionBlurActionOwnerName;
        }

        internal static string ResolveRemoteActiveEffectItemEffectOwnerNameForTesting()
        {
            return RemoteActiveEffectItemEffectActionOwnerName;
        }

        internal static string ResolveRemoteRelationshipOverlayOwnerNameForTesting(int relationshipType)
        {
            RemoteRelationshipOverlayType family = relationshipType switch
            {
                1 => RemoteRelationshipOverlayType.Couple,
                2 => RemoteRelationshipOverlayType.Friendship,
                3 => RemoteRelationshipOverlayType.NewYearCard,
                4 => RemoteRelationshipOverlayType.Marriage,
                _ => RemoteRelationshipOverlayType.Generic
            };

            return ResolveRemoteRelationshipOverlayActionOwnerName(family);
        }

        internal static int ResolveRemoteRelationshipOverlayCounterSkillIdForTesting(int relationshipType, int itemId)
        {
            RemoteRelationshipOverlayType family = relationshipType switch
            {
                1 => RemoteRelationshipOverlayType.Couple,
                2 => RemoteRelationshipOverlayType.Friendship,
                3 => RemoteRelationshipOverlayType.NewYearCard,
                4 => RemoteRelationshipOverlayType.Marriage,
                _ => RemoteRelationshipOverlayType.Generic
            };

            return ResolveRemoteRelationshipOverlayCounterSkillId(family, itemId);
        }

        internal static int ResolveRemotePacketOwnedEmotionCounterSkillIdForTesting(
            int itemId,
            int emotionId,
            bool byItemOption)
        {
            return ResolveRemotePacketOwnedEmotionCounterSkillId(itemId, emotionId, byItemOption);
        }

        internal static int ResolveRemoteActiveEffectMotionBlurCounterSkillIdForTesting(int itemId)
        {
            return ResolveRemoteActiveEffectMotionBlurCounterSkillId(itemId);
        }

        internal static int ResolveRemoteActiveEffectItemEffectCounterSkillIdForTesting(int itemId)
        {
            return ResolveRemoteActiveEffectItemEffectCounterSkillId(itemId);
        }

        internal static void UpdateRemoteActiveEffectItemEffectLayerAdmissionForTesting(RemoteUserActor actor)
        {
            UpdateRemoteActiveEffectItemEffectLayerAdmission(actor);
        }

        internal static int ResolveRemoteQuestDeliveryCounterSkillIdForTesting(int itemId)
        {
            return ResolveRemoteQuestDeliveryCounterSkillId(itemId);
        }

        internal static bool IsRemoteAuxiliaryLayerOwnerCounterContextMatchForTesting(
            int storedSkillId,
            string storedActionName,
            bool storedFacingRight,
            int requestedSkillId,
            string requestedActionName,
            bool requestedFacingRight)
        {
            return IsRemoteAuxiliaryLayerOwnerCounterContextMatch(
                new RemoteAuxiliaryLayerOwnerCounterState
                {
                    SkillId = storedSkillId,
                    ActionName = storedActionName,
                    FacingRight = storedFacingRight
                },
                requestedSkillId,
                requestedActionName,
                requestedFacingRight);
        }

        private static void SetActorAction(
            RemoteUserActor actor,
            string actionName,
            bool allowSitFallback,
            int currentTime = int.MinValue,
            bool forceReplay = false,
            int? rawActionCode = null)
        {
            if (actor == null)
            {
                return;
            }

            string normalizedActionName = ResolvePortableChairVisibleActionName(
                actor,
                NormalizeActionName(actionName, allowSitFallback),
                allowSitFallback);
            bool baseActionChanged = !string.Equals(actor.BaseActionName, normalizedActionName, StringComparison.OrdinalIgnoreCase);
            bool rawActionChanged = actor.BaseActionRawCode != rawActionCode;

            actor.BaseActionName = normalizedActionName;
            actor.BaseActionRawCode = rawActionCode;
            if (currentTime != int.MinValue && (forceReplay || baseActionChanged || rawActionChanged))
            {
                actor.BaseActionStartTime = currentTime;
            }

            actor.ActionName = ResolveClientVisibleActionName(actor.BaseActionName, actor.TemporaryStats.KnownState);
            actor.RidingVehicleId = ResolveRemoteRidingVehicleId(actor);
        }

        private static string ResolvePortableChairVisibleActionName(
            RemoteUserActor actor,
            string actionName,
            bool allowSitFallback)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (!allowSitFallback
                || chair == null
                || !string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Sit), StringComparison.OrdinalIgnoreCase))
            {
                return actionName;
            }

            return PlayerCharacter.ResolvePortableChairActionName(chair);
        }

        private static int ResolveRemoteRidingVehicleId(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return 0;
            }

            CharacterPart mountPart = null;
            if (actor.Build?.Equipment != null)
            {
                actor.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out mountPart);
            }
            CharacterPart activeMountedRenderOwner = actor.ResolveMountedStateTamingMobPart();
            return ResolveRemoteRidingVehicleIdForTesting(
                mountPart,
                actor.ActionName,
                actor.TemporaryStats.KnownState.MechanicMode,
                activeMountedRenderOwner);
        }

        internal static int ResolveRemoteRidingVehicleIdForTesting(
            CharacterPart equippedMountPart,
            string actionName,
            int? mechanicMode,
            CharacterPart activeMountedRenderOwner)
        {
            return FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                equippedMountPart,
                actionName,
                mechanicMode,
                activeMountedRenderOwner: activeMountedRenderOwner);
        }

        internal static string ResolveClientVisibleActionName(
            string baseActionName,
            RemoteUserTemporaryStatKnownState knownState)
        {
            string normalized = NormalizeActionName(baseActionName, allowSitFallback: false);
            if (TryResolveMechanicVisibleActionName(normalized, knownState.MechanicMode, out string mechanicActionName)
                && !knownState.IsHiddenLikeClient)
            {
                return mechanicActionName;
            }

            if (!knownState.IsHiddenLikeClient)
            {
                return normalized;
            }

            if (string.IsNullOrWhiteSpace(normalized)
                || normalized.StartsWith("ghost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "darksight", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "dead", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return normalized switch
            {
                "stand1" or "stand2" or "alert" => "ghoststand",
                "sit" => "ghostsit",
                "walk1" or "walk2" => "ghostwalk",
                "jump" => "ghostjump",
                "ladder" => "ghostladder",
                "rope" => "ghostrope",
                "swim" or "fly" => "ghostfly",
                "prone" or "proneStab" => "ghostproneStab",
                _ when IsHiddenLikeAttackAction(normalized) => "ghoststand",
                _ => normalized
            };
        }

        internal static bool TryResolveMechanicTamingMobOverrideItemId(
            RemoteUserTemporaryStatKnownState knownState,
            string baseActionName,
            out int tamingMobItemId)
        {
            tamingMobItemId = 0;
            if (!ClientOwnedVehicleSkillClassifier.IsExplicitMechanicVehiclePresentationSkillId(knownState.MechanicMode))
            {
                return false;
            }

            if (!ClientOwnedVehicleSkillClassifier.SupportsExplicitMechanicVehiclePresentationCurrentAction(baseActionName))
            {
                return false;
            }

            tamingMobItemId = MechanicTamingMobItemId;
            return true;
        }

        internal static bool TryResolveMechanicVisibleActionName(
            string baseActionName,
            int? mechanicMode,
            out string actionName)
        {
            actionName = null;
            string normalized = NormalizeActionName(baseActionName, allowSitFallback: false);
            if (string.IsNullOrWhiteSpace(normalized)
                || !TryResolveRemoteMechanicModePresentation(mechanicMode, out RemoteMechanicModePresentation presentation))
            {
                return false;
            }

            if (ClientOwnedVehicleSkillClassifier.IsWzOnlyMechanicVehicleOneTimeActionName(normalized))
            {
                return false;
            }

            if (ClientOwnedVehicleSkillClassifier.IsMechanicVehicleActionName(normalized, includeTransformStates: true))
            {
                actionName = normalized;
                return true;
            }

            if (ClientOwnedVehicleSkillClassifier.IsExplicitMechanicVehiclePresentationActionName(normalized))
            {
                actionName = normalized;
                return true;
            }

            actionName = normalized switch
            {
                "stand1" or "stand2" or "alert" or "sit" => presentation.StandActionName,
                "walk1" or "walk2" => presentation.WalkActionName,
                "jump" => presentation.StandActionName,
                "ladder" => "ladder2",
                "rope" => "rope2",
                "swim" => presentation.StandActionName,
                "fly" => presentation.StandActionName,
                "prone" or "proneStab" => presentation.ProneActionName,
                "hit" => presentation.StandActionName,
                _ when IsHiddenLikeAttackAction(normalized) => presentation.AttackActionName,
                _ => normalized
            };

            if (string.Equals(normalized, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "rope", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return true;
        }

        private static bool TryResolveRemoteMechanicModePresentation(
            int? mechanicMode,
            out RemoteMechanicModePresentation presentation)
        {
            if (mechanicMode.HasValue
                && RemoteMechanicModePresentationBySkillId.TryGetValue(mechanicMode.Value, out presentation))
            {
                return true;
            }

            presentation = default;
            return false;
        }

        private static bool IsHiddenLikeAttackAction(string actionName)
        {
            return actionName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("shot", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("magic", StringComparison.OrdinalIgnoreCase);
        }

        internal static IReadOnlyList<int> EnumerateRemoteShadowPartnerSkillIds(int jobId)
        {
            int preferredSkillId = jobId switch
            {
                >= 1410 and <= 1412 => 14111000,
                >= 420 and <= 422 => 4211008,
                >= 410 and <= 412 => 4111002,
                _ => 0
            };

            var orderedSkillIds = new List<int>(RemoteShadowPartnerSkillIds.Length);
            if (preferredSkillId > 0)
            {
                orderedSkillIds.Add(preferredSkillId);
            }

            for (int i = 0; i < RemoteShadowPartnerSkillIds.Length; i++)
            {
                int skillId = RemoteShadowPartnerSkillIds[i];
                if (skillId != preferredSkillId)
                {
                    orderedSkillIds.Add(skillId);
                }
            }

            return orderedSkillIds;
        }

        internal static string ResolveRemoteShadowPartnerActionName(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string actionName,
            PlayerState state,
            string fallbackActionName = null,
            string weaponType = null,
            int? rawActionCode = null,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return null;
            }

            foreach (string candidate in ShadowPartnerClientActionResolver.EnumerateClientMappedCandidates(
                         actionName,
                         state,
                         fallbackActionName,
                         weaponType,
                         rawActionCode,
                         supportedRawActionNames))
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && actionAnimations.ContainsKey(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static SkillAnimation ResolveRemoteShadowPartnerPlaybackAnimation(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string resolvedActionName,
            string actionName,
            int rawActionCode,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            string rawActionName = null;
            if (rawActionCode >= 0)
            {
                CharacterPart.TryGetActionStringFromCode(rawActionCode, out rawActionName);
            }

            return ShadowPartnerClientActionResolver.ResolvePlaybackAnimation(
                actionAnimations,
                resolvedActionName,
                actionName,
                rawActionName,
                supportedRawActionNames);
        }

        private static bool TryResolveRemoteShadowPartnerAttackAction(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            string observedPlayerActionName,
            PlayerState state,
            string weaponType,
            int? rawActionCode,
            IReadOnlySet<string> supportedRawActionNames,
            out string resolvedActionName,
            out SkillAnimation resolvedPlaybackAnimation)
        {
            resolvedActionName = null;
            resolvedPlaybackAnimation = null;
            if (!ShadowPartnerClientActionResolver.TryResolveAttackIdentityActionName(
                    actionAnimations,
                    observedPlayerActionName,
                    state,
                    out string resolvedAttackActionName,
                    weaponType,
                    rawActionCode,
                    supportedRawActionNames))
            {
                return false;
            }

            SkillAnimation attackPlaybackAnimation = ResolveRemoteShadowPartnerPlaybackAnimation(
                actionAnimations,
                resolvedAttackActionName,
                observedPlayerActionName,
                rawActionCode ?? -1,
                supportedRawActionNames);
            if (attackPlaybackAnimation?.Frames == null || attackPlaybackAnimation.Frames.Count == 0)
            {
                return false;
            }

            resolvedActionName = resolvedAttackActionName;
            resolvedPlaybackAnimation = attackPlaybackAnimation;
            return true;
        }

        private void ResolveRemoteShadowPartnerSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (!knownState.HasShadowPartner || _skillLoader == null)
            {
                return;
            }

            int jobId = actor?.Build?.Job ?? 0;
            IReadOnlyList<int> candidateSkillIds = EnumerateRemoteShadowPartnerSkillIds(jobId);
            for (int i = 0; i < candidateSkillIds.Count; i++)
            {
                int candidateSkillId = candidateSkillIds[i];
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (candidateSkill?.HasShadowPartnerActionAnimations != true)
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        internal static IReadOnlyList<int> EnumerateRemoteSoulArrowSkillIds(int jobId, bool preferSpiritJavelin)
        {
            int preferredSkillId = preferSpiritJavelin
                ? 4121006
                : jobId switch
            {
                >= 3310 and <= 3312 => 33101003,
                >= 1310 and <= 1312 => 13101003,
                >= 320 and <= 322 => 3201004,
                >= 310 and <= 312 => 3101004,
                >= 410 and <= 412 => 4121006,
                _ => 0
            };

            return EnumeratePreferredSkillIds(RemoteSoulArrowSkillIds, preferredSkillId);
        }

        internal static IReadOnlyList<int> EnumerateRemoteWeaponChargeSkillIds(int jobId, int? preferredSkillId)
        {
            int resolvedPreferredSkillId = preferredSkillId.GetValueOrDefault();
            if (resolvedPreferredSkillId <= 0 || !AfterImageChargeSkillResolver.IsKnownChargeSkillId(resolvedPreferredSkillId))
            {
                resolvedPreferredSkillId = jobId switch
                {
                    >= 1210 and <= 1212 => 1211004,
                    >= 1220 and <= 1222 => 1221004,
                    >= 1510 and <= 1512 => 15101006,
                    >= 2110 and <= 2112 => 21111005,
                    _ => 0
                };
            }

            return EnumeratePreferredSkillIds(RemoteWeaponChargeSkillIds, resolvedPreferredSkillId);
        }

        internal static IReadOnlyList<int> EnumerateRemoteBarrierSkillIds(int jobId)
        {
            int preferredSkillId = jobId switch
            {
                >= 2110 and <= 2112 => 21120007,
                >= 2310 and <= 2312 => 23111005,
                >= 2200 and <= 2218 => 20011010,
                >= 2100 and <= 2109 => 20001010,
                >= 1000 and <= 1512 => 10001010,
                2001 => 20011010,
                2000 => 20001010,
                _ => 0
            };

            return EnumeratePreferredSkillIds(RemoteBarrierSkillIds, preferredSkillId);
        }

        internal static IReadOnlyList<int> EnumerateRemoteAuraSkillIds(int jobId, int? preferredSkillId)
        {
            int resolvedPreferredSkillId = ResolvePreferredRemoteAuraSkillId(jobId, preferredSkillId);
            if (resolvedPreferredSkillId <= 0)
            {
                resolvedPreferredSkillId = jobId switch
                {
                    >= 3210 and <= 3212 => RemoteUserTemporaryStatKnownState.BlueAuraSkillId,
                    >= 3200 and <= 3202 => RemoteUserTemporaryStatKnownState.DarkAuraSkillId,
                    _ => 0
                };
            }

            return EnumeratePreferredSkillIds(RemoteAuraSkillIds, resolvedPreferredSkillId);
        }

        private static int ResolvePreferredRemoteAuraSkillId(int jobId, int? preferredSkillId)
        {
            int resolvedPreferredSkillId = preferredSkillId.GetValueOrDefault();
            if (resolvedPreferredSkillId <= 0)
            {
                return 0;
            }

            int normalizedJob = Math.Abs(jobId);
            return resolvedPreferredSkillId switch
            {
                RemoteUserTemporaryStatKnownState.DarkAuraSkillId when normalizedJob == 3212
                    => RemoteUserTemporaryStatKnownState.AdvancedDarkAuraSkillId,
                RemoteUserTemporaryStatKnownState.BlueAuraSkillId when normalizedJob >= 3211 && normalizedJob <= 3212
                    => RemoteUserTemporaryStatKnownState.AdvancedBlueAuraSkillId,
                RemoteUserTemporaryStatKnownState.YellowAuraSkillId when normalizedJob == 3212
                    => RemoteUserTemporaryStatKnownState.AdvancedYellowAuraSkillId,
                _ => resolvedPreferredSkillId
            };
        }

        internal static IReadOnlyList<int> EnumerateRemoteMoreWildDamageUpSkillIds(bool hasMoreWildDamageUp)
        {
            return hasMoreWildDamageUp
                ? RemoteMoreWildDamageUpSkillIds
                : Array.Empty<int>();
        }

        internal static IReadOnlyList<int> EnumerateRemoteBlessingArmorSkillIds(int jobId, int? preferredSkillId)
        {
            int resolvedPreferredSkillId = preferredSkillId.GetValueOrDefault();
            if (resolvedPreferredSkillId <= 0)
            {
                resolvedPreferredSkillId = jobId switch
                {
                    >= 1220 and <= 1222 => RemoteUserTemporaryStatKnownState.PaladinBlessingArmorSkillId,
                    >= 2310 and <= 2312 => RemoteUserTemporaryStatKnownState.BishopBlessingArmorSkillId,
                    _ => 0
                };
            }

            return EnumeratePreferredSkillIds(RemoteBlessingArmorSkillIds, resolvedPreferredSkillId);
        }

        private void ResolveRemoteSoulArrowSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (!knownState.HasSoulArrow || _skillLoader == null)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteSoulArrowSkillIds(actor?.Build?.Job ?? 0, knownState.HasSpiritJavelin))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteWeaponChargeSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (_skillLoader == null)
            {
                return;
            }

            int? resolvedChargeSkillId = ResolveChargeSkillIdFromTemporaryStats(
                actor?.TemporaryStats ?? default,
                ResolvePreferredRemoteWeaponChargeSkillId(actor?.Build?.Job ?? 0));
            bool hasEnergyChargeStyleValue = ShouldTreatRemoteWeaponChargeAsEnergyChargeStyle(
                actor?.TemporaryStats.HasWeaponCharge == true,
                knownState.WeaponChargeValue,
                resolvedChargeSkillId);
            if (hasEnergyChargeStyleValue)
            {
                ResolveRemoteEnergyChargeSkill(actor, knownState, out skillId, out skill);
                return;
            }

            if (resolvedChargeSkillId.HasValue
                && AfterImageChargeSkillResolver.IsKnownChargeSkillId(resolvedChargeSkillId.Value))
            {
                foreach (int candidateSkillId in EnumerateRemoteWeaponChargeSkillIds(actor?.Build?.Job ?? 0, resolvedChargeSkillId))
                {
                    SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                    if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                    {
                        continue;
                    }

                    skillId = candidateSkillId;
                    skill = candidateSkill;
                    return;
                }
            }

            if (!resolvedChargeSkillId.HasValue)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteWeaponChargeSkillIds(actor?.Build?.Job ?? 0, preferredSkillId: null))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteEnergyChargeSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (_skillLoader == null || !knownState.WeaponChargeValue.HasValue)
            {
                return;
            }

            int weaponChargeValue = knownState.WeaponChargeValue.Value;
            if (weaponChargeValue <= 0)
            {
                return;
            }

            int jobId = actor?.Build?.Job ?? 0;
            foreach (int candidateSkillId in EnumerateRemoteEnergyChargeSkillIds(jobId))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!CanUseRemoteEnergyChargeAvatarEffect(candidateSkillId, weaponChargeValue, candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private static IReadOnlyList<int> EnumerateRemoteEnergyChargeSkillIds(int jobId)
        {
            int preferredSkillId = jobId switch
            {
                >= 510 and <= 512 => 5110001,
                >= 1510 and <= 1512 => 15100004,
                _ => 0
            };

            return EnumeratePreferredSkillIds(RemoteEnergyChargeSkillIds, preferredSkillId);
        }

        private static bool CanUseRemoteEnergyChargeAvatarEffect(
            int skillId,
            int weaponChargeValue,
            SkillData skill)
        {
            if (weaponChargeValue <= 0
                || skill?.UsesEnergyChargeRuntime != true
                || string.IsNullOrWhiteSpace(skill.FullChargeEffectName)
                || !HasRemoteTemporaryStatAvatarEffect(skill))
            {
                return false;
            }

            return weaponChargeValue >= ResolveRemoteEnergyChargeMinimumFullChargeValue(skillId, skill);
        }

        private static bool ShouldTreatRemoteWeaponChargeAsEnergyChargeStyle(
            bool hasWeaponChargeMaskBit,
            int? weaponChargeValue,
            int? resolvedChargeSkillId = null)
        {
            int decodedWeaponChargeValue = weaponChargeValue.GetValueOrDefault();
            if (!hasWeaponChargeMaskBit || decodedWeaponChargeValue <= 0)
            {
                return false;
            }

            if (IsKnownRemoteEnergyChargeSkillId(decodedWeaponChargeValue))
            {
                return true;
            }

            return !IsKnownNonEnergyRemoteWeaponChargeSkillId(resolvedChargeSkillId.GetValueOrDefault())
                   && !IsKnownNonEnergyRemoteWeaponChargeSkillId(decodedWeaponChargeValue);
        }

        private static bool IsKnownNonEnergyRemoteWeaponChargeSkillId(int skillId)
        {
            if (!AfterImageChargeSkillResolver.IsKnownChargeSkillId(skillId))
            {
                return false;
            }

            for (int i = 0; i < RemoteEnergyChargeSkillIds.Length; i++)
            {
                if (RemoteEnergyChargeSkillIds[i] == skillId)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsKnownRemoteEnergyChargeSkillId(int skillId)
        {
            for (int i = 0; i < RemoteEnergyChargeSkillIds.Length; i++)
            {
                if (RemoteEnergyChargeSkillIds[i] == skillId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveRemoteEnergyChargeMinimumFullChargeValue(int skillId, SkillData skill)
        {
            int minimumFullChargeValue = skillId switch
            {
                5110001 => BuccaneerEnergyChargeMinimumFullChargeValue,
                15100004 => ThunderBreakerEnergyChargeMinimumFullChargeValue,
                _ => 100
            };
            int resolvedFormulaBoundary = skill?.ResolveEnergyChargeThreshold(1) ?? minimumFullChargeValue;
            return Math.Max(1, Math.Max(minimumFullChargeValue, resolvedFormulaBoundary));
        }

        private void ResolveRemoteMoreWildDamageUpSkill(
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (_skillLoader == null)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteMoreWildDamageUpSkillIds(knownState.ExtendedState.HasMorewildDamageUp))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteBarrierSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (!knownState.HasBarrier || _skillLoader == null)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteBarrierSkillIds(actor?.Build?.Job ?? 0))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteAuraSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (!knownState.ActiveAuraSkillId.HasValue || _skillLoader == null)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteAuraSkillIds(actor?.Build?.Job ?? 0, knownState.ActiveAuraSkillId))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteBlessingArmorSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (_skillLoader == null)
            {
                return;
            }

            int? preferredSkillId = knownState.ResolveBlessingArmorSkillId(actor?.Build?.Job ?? 0);
            if (!preferredSkillId.HasValue || preferredSkillId.Value <= 0)
            {
                return;
            }

            foreach (int candidateSkillId in EnumerateRemoteBlessingArmorSkillIds(actor?.Build?.Job ?? 0, preferredSkillId))
            {
                SkillData candidateSkill = _skillLoader.LoadSkill(candidateSkillId);
                if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
                {
                    continue;
                }

                skillId = candidateSkillId;
                skill = candidateSkill;
                return;
            }
        }

        private void ResolveRemoteRepeatEffectSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            ResolveRemotePayloadDrivenEffectSkill(knownState.RepeatEffectSkillId, out skillId, out skill);
        }

        private void ResolveRemoteMagicShieldSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            ResolveRemotePayloadDrivenEffectSkill(knownState.MagicShieldSkillId, out skillId, out skill);
        }

        private void ResolveRemoteFinalCutSkill(
            RemoteUserActor actor,
            RemoteUserTemporaryStatKnownState knownState,
            out int? skillId,
            out SkillData skill)
        {
            ResolveRemotePayloadDrivenEffectSkill(knownState.FinalCutSkillId, out skillId, out skill);
        }

        private void ResolveRemotePayloadDrivenEffectSkill(
            int? payloadSkillId,
            out int? skillId,
            out SkillData skill)
        {
            skillId = null;
            skill = null;
            if (_skillLoader == null
                || !payloadSkillId.HasValue
                || payloadSkillId.Value <= 0)
            {
                return;
            }

            SkillData candidateSkill = _skillLoader.LoadSkill(payloadSkillId.Value);
            if (!HasRemoteTemporaryStatAvatarEffect(candidateSkill))
            {
                return;
            }

            skillId = payloadSkillId.Value;
            skill = candidateSkill;
        }

        private static IReadOnlyList<int> EnumeratePreferredSkillIds(IReadOnlyList<int> skillIds, int preferredSkillId)
        {
            var orderedSkillIds = new List<int>(skillIds?.Count ?? 0);
            if (preferredSkillId > 0)
            {
                orderedSkillIds.Add(preferredSkillId);
            }

            if (skillIds == null)
            {
                return orderedSkillIds;
            }

            for (int i = 0; i < skillIds.Count; i++)
            {
                int skillId = skillIds[i];
                if (skillId != preferredSkillId)
                {
                    orderedSkillIds.Add(skillId);
                }
            }

            return orderedSkillIds;
        }

        private static bool HasRemoteTemporaryStatAvatarEffect(SkillData skill)
        {
            return skill?.AvatarOverlayEffect != null
                   || skill?.AvatarOverlaySecondaryEffect != null
                   || skill?.AvatarUnderFaceEffect != null
                   || skill?.AvatarUnderFaceSecondaryEffect != null
                   || skill?.AffectedEffect != null
                   || skill?.AffectedSecondaryEffect != null
                   || skill?.Effect != null
                   || skill?.EffectSecondary != null
                   || skill?.RepeatEffect != null
                   || skill?.RepeatSecondaryEffect != null;
        }

        private static bool HasAnyRemoteTemporaryStatAvatarEffectAnimation(RemoteTemporaryStatAvatarEffectState state)
        {
            return state?.OverlayAnimation != null
                   || state?.OverlaySecondaryAnimation != null
                   || state?.UnderFaceAnimation != null
                   || state?.UnderFaceSecondaryAnimation != null;
        }

        private RemoteTemporaryStatAvatarEffectState UpdateRemoteTemporaryStatAvatarEffectState(
            RemoteUserActor actor,
            RemoteTemporaryStatAvatarEffectState existingState,
            RemoteTemporaryStatAvatarEffectOwnerFamily ownerFamily,
            int? skillId,
            SkillData skill,
            int currentTime,
            bool reseedTimelineFromPacket)
        {
            string ownerName = ResolveRemoteTemporaryStatAvatarEffectOwnerName(ownerFamily);
            string ownerActionName = ResolveRemoteTemporaryStatAvatarEffectActionName(actor);
            bool ownerFacingRight = actor?.FacingRight ?? true;
            int ownerCharacterId = actor?.CharacterId ?? 0;

            if (skillId.HasValue
                && skill != null
                && existingState?.SkillId == skillId.Value
                && HasAnyRemoteTemporaryStatAvatarEffectAnimation(existingState))
            {
                existingState.Skill = skill;
                if (reseedTimelineFromPacket)
                {
                    existingState.AnimationStartTime = currentTime;
                }

                return existingState;
            }

            if (existingState != null && ownerCharacterId > 0)
            {
                StoreRemoteAuxiliaryLayerOwnerCounter(
                    ownerCharacterId,
                    ownerName,
                    existingState.SkillId,
                    ownerActionName,
                    ownerFacingRight,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, existingState.AnimationStartTime),
                    currentTime);
            }

            if (!skillId.HasValue
                || skill == null
                || !TryCreateRemoteTemporaryStatAvatarEffectState(
                    skillId.Value,
                    skill,
                    currentTime,
                    out RemoteTemporaryStatAvatarEffectState nextState))
            {
                return null;
            }

            if (!reseedTimelineFromPacket
                && ownerCharacterId > 0
                && TryRestoreRemoteAuxiliaryLayerOwnerCounter(
                    ownerCharacterId,
                    ownerName,
                    nextState.SkillId,
                    ownerActionName,
                    ownerFacingRight,
                    out int restoredAnimationElapsedMs))
            {
                RemoteTemporaryStatAvatarEffectState restoredState = new RemoteTemporaryStatAvatarEffectState
                {
                    SkillId = nextState.SkillId,
                    Skill = nextState.Skill,
                    OverlayAnimation = nextState.OverlayAnimation,
                    OverlaySecondaryAnimation = nextState.OverlaySecondaryAnimation,
                    UnderFaceAnimation = nextState.UnderFaceAnimation,
                    UnderFaceSecondaryAnimation = nextState.UnderFaceSecondaryAnimation,
                    SimulatedAffectedLayerHandleId = nextState.SimulatedAffectedLayerHandleId,
                    AnimationStartTime = unchecked(currentTime - restoredAnimationElapsedMs),
                    TransitionStartTime = int.MinValue,
                    TransitionDurationMs = 0,
                    TransitionStartAlpha = 1f,
                    TransitionEndAlpha = 1f
                };
                restoredState.CaptureAffectedLayerReference();
                return restoredState;
            }

            if (existingState?.SkillId == skillId.Value)
            {
                if (!reseedTimelineFromPacket)
                {
                    // Mirror `CUser::UpdateAr01Effect` ownership: keep the current layer object
                    // alive while the same skill remains active and no packet-owned reseed applies.
                    return existingState;
                }

                RemoteTemporaryStatAvatarEffectState reseededState = new RemoteTemporaryStatAvatarEffectState
                {
                    SkillId = nextState.SkillId,
                    Skill = nextState.Skill,
                    OverlayAnimation = nextState.OverlayAnimation,
                    OverlaySecondaryAnimation = nextState.OverlaySecondaryAnimation,
                    UnderFaceAnimation = nextState.UnderFaceAnimation,
                    UnderFaceSecondaryAnimation = nextState.UnderFaceSecondaryAnimation,
                    SimulatedAffectedLayerHandleId = existingState.SimulatedAffectedLayerHandleId,
                    AnimationStartTime = ResolveRemoteTemporaryStatAvatarEffectAnimationStartTime(
                        existingState.SkillId,
                        nextState.SkillId,
                        existingState.AnimationStartTime,
                        nextState.AnimationStartTime,
                        reseedTimelineFromPacket),
                    TransitionStartTime = int.MinValue,
                    TransitionDurationMs = 0,
                    TransitionStartAlpha = 1f,
                    TransitionEndAlpha = 1f
                };
                reseededState.CaptureAffectedLayerReference();
                return reseededState;
            }

            if (existingState != null)
            {
                ConfigureRemoteTemporaryStatAvatarEffectTransition(
                    nextState,
                    currentTime,
                    RemoteTemporaryStatAffectedLayerTransitionDurationMs,
                    startAlpha: 0f,
                    endAlpha: 1f);
            }

            return nextState;
        }

        private static RemoteTemporaryStatAvatarEffectState UpdateRemoteTemporaryStatAvatarEffectState(
            RemoteTemporaryStatAvatarEffectState existingState,
            int? skillId,
            SkillData skill,
            int currentTime,
            bool reseedTimelineFromPacket)
        {
            if (skillId.HasValue
                && skill != null
                && existingState?.SkillId == skillId.Value
                && HasAnyRemoteTemporaryStatAvatarEffectAnimation(existingState))
            {
                existingState.Skill = skill;
                if (reseedTimelineFromPacket)
                {
                    existingState.AnimationStartTime = currentTime;
                }

                return existingState;
            }

            if (!skillId.HasValue
                || skill == null
                || !TryCreateRemoteTemporaryStatAvatarEffectState(
                    skillId.Value,
                    skill,
                    currentTime,
                    out RemoteTemporaryStatAvatarEffectState nextState))
            {
                return null;
            }

            if (existingState?.SkillId == skillId.Value)
            {
                if (!reseedTimelineFromPacket)
                {
                    return existingState;
                }

                RemoteTemporaryStatAvatarEffectState reseededState = new RemoteTemporaryStatAvatarEffectState
                {
                    SkillId = nextState.SkillId,
                    Skill = nextState.Skill,
                    OverlayAnimation = nextState.OverlayAnimation,
                    OverlaySecondaryAnimation = nextState.OverlaySecondaryAnimation,
                    UnderFaceAnimation = nextState.UnderFaceAnimation,
                    UnderFaceSecondaryAnimation = nextState.UnderFaceSecondaryAnimation,
                    SimulatedAffectedLayerHandleId = existingState.SimulatedAffectedLayerHandleId,
                    AnimationStartTime = ResolveRemoteTemporaryStatAvatarEffectAnimationStartTime(
                        existingState.SkillId,
                        nextState.SkillId,
                        existingState.AnimationStartTime,
                        nextState.AnimationStartTime,
                        reseedTimelineFromPacket),
                    TransitionStartTime = int.MinValue,
                    TransitionDurationMs = 0,
                    TransitionStartAlpha = 1f,
                    TransitionEndAlpha = 1f
                };
                reseededState.CaptureAffectedLayerReference();
                return reseededState;
            }

            if (existingState != null)
            {
                ConfigureRemoteTemporaryStatAvatarEffectTransition(
                    nextState,
                    currentTime,
                    RemoteTemporaryStatAffectedLayerTransitionDurationMs,
                    startAlpha: 0f,
                    endAlpha: 1f);
            }

            return nextState;
        }

        internal static RemoteTemporaryStatAvatarEffectState UpdateRemoteTemporaryStatAvatarEffectStateForTesting(
            RemoteTemporaryStatAvatarEffectState existingState,
            int? skillId,
            SkillData skill,
            int currentTime,
            bool reseedTimelineFromPacket)
        {
            return UpdateRemoteTemporaryStatAvatarEffectState(
                existingState,
                skillId,
                skill,
                currentTime,
                reseedTimelineFromPacket);
        }

        private static void ConfigureRemoteTemporaryStatAvatarEffectTransition(
            RemoteTemporaryStatAvatarEffectState state,
            int transitionStartTime,
            int durationMs,
            float startAlpha,
            float endAlpha)
        {
            if (state == null)
            {
                return;
            }

            state.TransitionStartTime = transitionStartTime;
            state.TransitionDurationMs = Math.Max(0, durationMs);
            state.TransitionStartAlpha = MathHelper.Clamp(startAlpha, 0f, 1f);
            state.TransitionEndAlpha = MathHelper.Clamp(endAlpha, 0f, 1f);
        }

        private static float ResolveRemoteTemporaryStatAvatarEffectTransitionAlpha(
            RemoteTemporaryStatAvatarEffectState state,
            int currentTime)
        {
            if (state == null)
            {
                return 0f;
            }

            float startAlpha = MathHelper.Clamp(state.TransitionStartAlpha, 0f, 1f);
            float endAlpha = MathHelper.Clamp(state.TransitionEndAlpha, 0f, 1f);
            if (state.TransitionDurationMs <= 0 || state.TransitionStartTime == int.MinValue)
            {
                return endAlpha;
            }

            if (currentTime == int.MinValue)
            {
                return startAlpha;
            }

            int elapsedTransitionMs = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.TransitionStartTime);
            float progress = MathHelper.Clamp(
                elapsedTransitionMs / (float)Math.Max(1, state.TransitionDurationMs),
                0f,
                1f);
            return MathHelper.Lerp(startAlpha, endAlpha, progress);
        }

        private static bool IsRemoteTemporaryStatAvatarEffectTransitionExpired(
            RemoteTemporaryStatAvatarEffectState state,
            int currentTime)
        {
            if (state == null
                || currentTime == int.MinValue
                || state.TransitionStartTime == int.MinValue
                || state.TransitionDurationMs <= 0)
            {
                return false;
            }

            return ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.TransitionStartTime) >= state.TransitionDurationMs
                   && state.TransitionEndAlpha <= 0f;
        }

        internal static int ResolveRemoteTemporaryStatTickElapsedMs(int currentTime, int startTime)
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

        private static bool HasRemoteClientOwnedAuxiliaryAvatarLayerDurationElapsed(
            int currentTime,
            int startTime,
            int durationMs)
        {
            return durationMs <= 0
                   || ResolveRemoteTemporaryStatTickElapsedMs(currentTime, startTime) >= durationMs;
        }

        internal static int ResolveRemoteTemporaryStatTickElapsedMsForTesting(int currentTime, int startTime)
        {
            return ResolveRemoteTemporaryStatTickElapsedMs(currentTime, startTime);
        }

        internal static bool HasRemoteClientOwnedAuxiliaryAvatarLayerDurationElapsedForTesting(
            int currentTime,
            int startTime,
            int durationMs)
        {
            return HasRemoteClientOwnedAuxiliaryAvatarLayerDurationElapsed(currentTime, startTime, durationMs);
        }

        private static RemoteTemporaryStatAvatarEffectState CloneRemoteTemporaryStatAvatarEffectState(
            RemoteTemporaryStatAvatarEffectState source)
        {
            if (source == null)
            {
                return null;
            }

            RemoteTemporaryStatAvatarEffectState clone = new RemoteTemporaryStatAvatarEffectState
            {
                SkillId = source.SkillId,
                Skill = source.Skill,
                OverlayAnimation = source.OverlayAnimation,
                OverlaySecondaryAnimation = source.OverlaySecondaryAnimation,
                UnderFaceAnimation = source.UnderFaceAnimation,
                UnderFaceSecondaryAnimation = source.UnderFaceSecondaryAnimation,
                SimulatedAffectedLayerHandleId = source.SimulatedAffectedLayerHandleId,
                AnimationStartTime = source.AnimationStartTime,
                TransitionStartTime = source.TransitionStartTime,
                TransitionDurationMs = source.TransitionDurationMs,
                TransitionStartAlpha = source.TransitionStartAlpha,
                TransitionEndAlpha = source.TransitionEndAlpha
            };
            clone.CaptureAffectedLayerReference();
            return clone;
        }

        private static bool TryCreateRemoteTemporaryStatAvatarEffectTailState(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState,
            int currentTime,
            int shiftCadenceFrameDurationMs,
            int shiftCadenceUpdateCount,
            out RemoteTemporaryStatAvatarEffectState tailState)
        {
            tailState = null;
            if (previousState == null
                || !HasAnyRemoteTemporaryStatAvatarEffectAnimation(previousState))
            {
                return false;
            }

            if (currentState != null
                && previousState.SkillId == currentState.SkillId)
            {
                return false;
            }

            float tailStartAlpha = ResolveRemoteTemporaryStatAvatarEffectTransitionAlpha(previousState, currentTime);
            if (tailStartAlpha <= 0f)
            {
                return false;
            }

            tailState = CloneRemoteTemporaryStatAvatarEffectState(previousState);
            previousState.ReleaseAffectedLayerReference();
            ConfigureRemoteTemporaryStatAvatarEffectTransition(
                tailState,
                currentTime,
                ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationMs(
                    previousState,
                    currentState,
                    shiftCadenceFrameDurationMs,
                    shiftCadenceUpdateCount),
                startAlpha: tailStartAlpha,
                endAlpha: 0f);
            return true;
        }

        private static int ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationMs(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState,
            int shiftCadenceFrameDurationMs,
            int shiftCadenceUpdateCount)
        {
            int leadFrameDelayMs = ResolveRemoteTemporaryStatAvatarEffectLeadFrameDelayMs(currentState);
            if (leadFrameDelayMs <= 0)
            {
                leadFrameDelayMs = ResolveRemoteTemporaryStatAvatarEffectLeadFrameDelayMs(previousState);
            }

            int authoredBlendWindowMs = leadFrameDelayMs > 0
                ? Math.Max(1, leadFrameDelayMs) * 2
                : 0;
            int remainingCadenceUpdates =
                ResolveRemoteTemporaryStatAffectedLayerShiftCadenceRemainingUpdates(shiftCadenceUpdateCount);
            int shiftCadenceDurationMs = remainingCadenceUpdates * Math.Max(1, shiftCadenceFrameDurationMs);
            return Math.Max(
                RemoteTemporaryStatAffectedLayerTransitionDurationMs,
                Math.Max(
                    authoredBlendWindowMs,
                    shiftCadenceDurationMs));
        }

        private static int ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationMs(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState)
        {
            return ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationMs(
                previousState,
                currentState,
                RemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationFallbackMs,
                shiftCadenceUpdateCount: 0);
        }

        private static int ResolveRemoteTemporaryStatAffectedLayerShiftCadenceRemainingUpdates(int shiftCadenceUpdateCount)
        {
            int normalizedUpdateCount = shiftCadenceUpdateCount % RemoteTemporaryStatAffectedLayerShiftCadenceUpdates;
            if (normalizedUpdateCount < 0)
            {
                normalizedUpdateCount += RemoteTemporaryStatAffectedLayerShiftCadenceUpdates;
            }

            if (normalizedUpdateCount == 0)
            {
                return 0;
            }

            int remainingUpdates = RemoteTemporaryStatAffectedLayerShiftCadenceUpdates - normalizedUpdateCount;
            return remainingUpdates <= 0
                ? 0
                : remainingUpdates;
        }

        private static int ResolveRemoteTemporaryStatAvatarEffectLeadFrameDelayMs(
            RemoteTemporaryStatAvatarEffectState state)
        {
            if (state == null)
            {
                return 0;
            }

            int minimumDelayMs = int.MaxValue;
            AccumulateRemoteTemporaryStatAvatarEffectLeadFrameDelayMs(state.OverlayAnimation, ref minimumDelayMs);
            AccumulateRemoteTemporaryStatAvatarEffectLeadFrameDelayMs(state.OverlaySecondaryAnimation, ref minimumDelayMs);
            AccumulateRemoteTemporaryStatAvatarEffectLeadFrameDelayMs(state.UnderFaceAnimation, ref minimumDelayMs);
            AccumulateRemoteTemporaryStatAvatarEffectLeadFrameDelayMs(state.UnderFaceSecondaryAnimation, ref minimumDelayMs);
            return minimumDelayMs == int.MaxValue ? 0 : minimumDelayMs;
        }

        private static void AppendRemoteTemporaryStatAuraTailState(
            List<RemoteTemporaryStatAvatarEffectState> tailStates,
            RemoteTemporaryStatAvatarEffectState tailState)
        {
            if (tailStates == null || tailState == null)
            {
                return;
            }

            tailStates.Add(tailState);

            // `m_lpLayerAffected` handoff continuity keeps a single active tail-owner in this seam.
            if (tailStates.Count > 1)
            {
                for (int i = 0; i < tailStates.Count - 1; i++)
                {
                    tailStates[i]?.ReleaseAffectedLayerReference();
                }

                tailStates.RemoveRange(0, tailStates.Count - 1);
            }
        }

        private static RemoteTemporaryStatAvatarEffectState ResolveLatestRemoteTemporaryStatAuraTailState(
            IReadOnlyList<RemoteTemporaryStatAvatarEffectState> tailStates,
            int currentTime)
        {
            if (tailStates == null || tailStates.Count == 0)
            {
                return null;
            }

            for (int i = tailStates.Count - 1; i >= 0; i--)
            {
                RemoteTemporaryStatAvatarEffectState candidate = tailStates[i];
                if (candidate != null
                    && !IsRemoteTemporaryStatAvatarEffectTransitionExpired(candidate, currentTime))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void AccumulateRemoteTemporaryStatAvatarEffectLeadFrameDelayMs(
            SkillAnimation animation,
            ref int minimumDelayMs)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return;
            }

            int delayMs = animation.Frames[0]?.Delay ?? 0;
            if (delayMs <= 0)
            {
                return;
            }

            if (delayMs < minimumDelayMs)
            {
                minimumDelayMs = delayMs;
            }
        }

        private static void PruneExpiredRemoteTemporaryStatAvatarEffectTailStates(
            List<RemoteTemporaryStatAvatarEffectState> tailStates,
            int currentTime)
        {
            if (tailStates == null || tailStates.Count == 0)
            {
                return;
            }

            for (int i = tailStates.Count - 1; i >= 0; i--)
            {
                if (IsRemoteTemporaryStatAvatarEffectTransitionExpired(tailStates[i], currentTime))
                {
                    tailStates[i]?.ReleaseAffectedLayerReference();
                    tailStates.RemoveAt(i);
                }
            }
        }

        internal static bool TryCreateRemoteTemporaryStatAvatarEffectTailStateForTesting(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState,
            int currentTime,
            out RemoteTemporaryStatAvatarEffectState tailState)
        {
            return TryCreateRemoteTemporaryStatAvatarEffectTailState(
                previousState,
                currentState,
                currentTime,
                RemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationFallbackMs,
                shiftCadenceUpdateCount: 0,
                out tailState);
        }

        internal static bool TryCreateRemoteTemporaryStatAvatarEffectTailStateForTesting(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState,
            int currentTime,
            int shiftCadenceFrameDurationMs,
            out RemoteTemporaryStatAvatarEffectState tailState)
        {
            return TryCreateRemoteTemporaryStatAvatarEffectTailState(
                previousState,
                currentState,
                currentTime,
                shiftCadenceFrameDurationMs,
                shiftCadenceUpdateCount: 0,
                out tailState);
        }

        internal static int ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationForTesting(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState)
        {
            return ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationMs(previousState, currentState);
        }

        internal static int ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationForTesting(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState,
            int shiftCadenceFrameDurationMs)
        {
            return ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationMs(
                previousState,
                currentState,
                shiftCadenceFrameDurationMs,
                shiftCadenceUpdateCount: 0);
        }

        internal static int ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationForTesting(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState,
            int shiftCadenceFrameDurationMs,
            int shiftCadenceUpdateCount)
        {
            return ResolveRemoteTemporaryStatAffectedLayerTailTransitionDurationMs(
                previousState,
                currentState,
                shiftCadenceFrameDurationMs,
                shiftCadenceUpdateCount);
        }

        internal static int ResolveRemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationForTesting(int observedDeltaMs)
        {
            return ResolveRemoteTemporaryStatAffectedLayerShiftCadenceFrameDurationMs(observedDeltaMs);
        }

        internal static int ResolveRemoteTemporaryStatAffectedLayerShiftCadenceRemainingUpdatesForTesting(int shiftCadenceUpdateCount)
        {
            return ResolveRemoteTemporaryStatAffectedLayerShiftCadenceRemainingUpdates(shiftCadenceUpdateCount);
        }

        internal static bool ShouldResetRemoteTemporaryStatAffectedLayerShiftCadenceForAuraReplacementForTesting(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState)
        {
            return ShouldResetRemoteTemporaryStatAffectedLayerShiftCadenceForAuraReplacement(previousState, currentState);
        }

        internal static int ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountAfterForcedShiftForTesting(
            int currentUpdateCount)
        {
            return ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountAfterForcedShift(currentUpdateCount);
        }

        internal static int ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountForAuraTailTransitionForTesting(
            RemoteTemporaryStatAvatarEffectState previousState,
            RemoteTemporaryStatAvatarEffectState currentState,
            int currentUpdateCount)
        {
            return ResolveRemoteTemporaryStatAffectedLayerShiftCadenceUpdateCountForAuraTailTransition(
                previousState,
                currentState,
                currentUpdateCount);
        }

        internal static int ResolveRemoteEnergyChargeMinimumFullChargeValueForTesting(int skillId, SkillData skill)
        {
            return ResolveRemoteEnergyChargeMinimumFullChargeValue(skillId, skill);
        }

        internal static bool CanUseRemoteEnergyChargeAvatarEffectForTesting(
            int skillId,
            int weaponChargeValue,
            SkillData skill)
        {
            return CanUseRemoteEnergyChargeAvatarEffect(skillId, weaponChargeValue, skill);
        }

        internal static bool ShouldTreatRemoteWeaponChargeAsEnergyChargeStyleForTesting(
            bool hasWeaponChargeMaskBit,
            int? weaponChargeValue)
        {
            return ShouldTreatRemoteWeaponChargeAsEnergyChargeStyle(hasWeaponChargeMaskBit, weaponChargeValue);
        }

        internal static bool ShouldTreatRemoteWeaponChargeAsEnergyChargeStyleForTesting(
            bool hasWeaponChargeMaskBit,
            int? weaponChargeValue,
            int? resolvedChargeSkillId)
        {
            return ShouldTreatRemoteWeaponChargeAsEnergyChargeStyle(
                hasWeaponChargeMaskBit,
                weaponChargeValue,
                resolvedChargeSkillId);
        }

        internal static float ResolveRemoteTemporaryStatAvatarEffectTransitionAlphaForTesting(
            RemoteTemporaryStatAvatarEffectState state,
            int currentTime)
        {
            return ResolveRemoteTemporaryStatAvatarEffectTransitionAlpha(state, currentTime);
        }

        internal static int PruneExpiredRemoteTemporaryStatAvatarEffectTailStatesForTesting(
            List<RemoteTemporaryStatAvatarEffectState> tailStates,
            int currentTime)
        {
            int beforeCount = tailStates?.Count ?? 0;
            PruneExpiredRemoteTemporaryStatAvatarEffectTailStates(tailStates, currentTime);
            return tailStates == null ? 0 : Math.Max(0, beforeCount - tailStates.Count);
        }

        internal static int AppendRemoteTemporaryStatAuraTailStateForTesting(
            List<RemoteTemporaryStatAvatarEffectState> tailStates,
            RemoteTemporaryStatAvatarEffectState tailState)
        {
            AppendRemoteTemporaryStatAuraTailState(tailStates, tailState);
            return tailStates?.Count ?? 0;
        }

        internal static IReadOnlyList<int> BuildRemoteTemporaryStatAvatarEffectDrawOrderSkillIdsForTesting(
            RemoteTemporaryStatAvatarEffectState soulArrowState,
            RemoteTemporaryStatAvatarEffectState weaponChargeState,
            RemoteTemporaryStatAvatarEffectState auraState,
            IReadOnlyList<RemoteTemporaryStatAvatarEffectState> auraTailStates,
            RemoteTemporaryStatAvatarEffectState moreWildState,
            RemoteTemporaryStatAvatarEffectState barrierState,
            RemoteTemporaryStatAvatarEffectState blessingArmorState,
            RemoteTemporaryStatAvatarEffectState repeatState,
            RemoteTemporaryStatAvatarEffectState magicShieldState,
            RemoteTemporaryStatAvatarEffectState finalCutState,
            int currentTime = int.MinValue)
        {
            IReadOnlyList<RemoteTemporaryStatAvatarEffectState> orderedStates =
                BuildRemoteTemporaryStatAvatarEffectDrawStates(
                    soulArrowState,
                    weaponChargeState,
                    auraState,
                    auraTailStates,
                    moreWildState,
                    barrierState,
                    blessingArmorState,
                    repeatState,
                    magicShieldState,
                    finalCutState,
                    currentTime);

            var orderedSkillIds = new List<int>(orderedStates.Count);
            for (int i = 0; i < orderedStates.Count; i++)
            {
                orderedSkillIds.Add(orderedStates[i]?.SkillId ?? 0);
            }

            return orderedSkillIds;
        }

        internal static bool TryCreateRemoteTemporaryStatAvatarEffectState(
            int skillId,
            SkillData skill,
            int animationStartTime,
            out RemoteTemporaryStatAvatarEffectState state)
        {
            state = null;
            if (!HasRemoteTemporaryStatAvatarEffect(skill))
            {
                return false;
            }

            SkillAnimation overlayAnimation = null;
            SkillAnimation overlaySecondaryAnimation = null;
            SkillAnimation underFaceAnimation = null;
            SkillAnimation underFaceSecondaryAnimation = null;

            AssignRemoteTemporaryStatAvatarEffectPlane(
                CreateLoopingRemoteTemporaryStatAvatarEffect(skill.AvatarOverlayEffect),
                ref overlayAnimation,
                ref overlaySecondaryAnimation,
                ref underFaceAnimation,
                ref underFaceSecondaryAnimation);
            AssignRemoteTemporaryStatAvatarEffectPlane(
                CreateLoopingRemoteTemporaryStatAvatarEffect(skill.AvatarOverlaySecondaryEffect),
                ref overlayAnimation,
                ref overlaySecondaryAnimation,
                ref underFaceAnimation,
                ref underFaceSecondaryAnimation);
            AssignRemoteTemporaryStatAvatarEffectPlane(
                CreateLoopingRemoteTemporaryStatAvatarEffect(skill.AvatarUnderFaceEffect),
                ref overlayAnimation,
                ref overlaySecondaryAnimation,
                ref underFaceAnimation,
                ref underFaceSecondaryAnimation);
            AssignRemoteTemporaryStatAvatarEffectPlane(
                CreateLoopingRemoteTemporaryStatAvatarEffect(skill.AvatarUnderFaceSecondaryEffect),
                ref overlayAnimation,
                ref overlaySecondaryAnimation,
                ref underFaceAnimation,
                ref underFaceSecondaryAnimation);

            if (skill.AffectedEffect != null || skill.AffectedSecondaryEffect != null)
            {
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.AffectedEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.AffectedSecondaryEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
            }
            else
            {
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.Effect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.EffectSecondary),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.RepeatEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
                AssignRemoteTemporaryStatAvatarEffectPlane(
                    CreateLoopingRemoteTemporaryStatAvatarEffect(skill.RepeatSecondaryEffect),
                    ref overlayAnimation,
                    ref overlaySecondaryAnimation,
                    ref underFaceAnimation,
                    ref underFaceSecondaryAnimation);
            }

            if (overlayAnimation == null
                && overlaySecondaryAnimation == null
                && underFaceAnimation == null
                && underFaceSecondaryAnimation == null)
            {
                return false;
            }

            state = new RemoteTemporaryStatAvatarEffectState
            {
                SkillId = skillId,
                Skill = skill,
                OverlayAnimation = overlayAnimation,
                OverlaySecondaryAnimation = overlaySecondaryAnimation,
                UnderFaceAnimation = underFaceAnimation,
                UnderFaceSecondaryAnimation = underFaceSecondaryAnimation,
                SimulatedAffectedLayerHandleId = NextRemoteTemporaryStatAffectedLayerHandleId(),
                AnimationStartTime = animationStartTime
            };
            state.CaptureAffectedLayerReference();
            return true;
        }

        private static SkillAnimation CreateLoopingRemoteTemporaryStatAvatarEffect(SkillAnimation animation)
        {
            if (animation == null)
            {
                return null;
            }

            return new SkillAnimation
            {
                Name = animation.Name,
                Frames = new List<SkillFrame>(animation.Frames),
                Loop = true,
                Origin = animation.Origin,
                ZOrder = animation.ZOrder,
                PositionCode = animation.PositionCode
            };
        }

        private static void AssignRemoteTemporaryStatAvatarEffectPlane(
            SkillAnimation animation,
            ref SkillAnimation overlayAnimation,
            ref SkillAnimation overlaySecondaryAnimation,
            ref SkillAnimation underFaceAnimation,
            ref SkillAnimation underFaceSecondaryAnimation)
        {
            if (animation == null)
            {
                return;
            }

            if (ClientOwnedAvatarEffectParity.PrefersUnderFaceAvatarEffectPlane(animation))
            {
                underFaceAnimation ??= animation;
                underFaceSecondaryAnimation ??= animation == underFaceAnimation ? null : animation;
                return;
            }

            overlayAnimation ??= animation;
            overlaySecondaryAnimation ??= animation == overlayAnimation ? null : animation;
        }

        private static PlayerState ResolveRemoteShadowPartnerState(string actionName)
        {
            string normalized = NormalizeActionName(actionName, allowSitFallback: false);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return PlayerState.Standing;
            }

            if (normalized.StartsWith("walk", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Walking;
            }

            if (string.Equals(normalized, "jump", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Jumping;
            }

            if (string.Equals(normalized, "ladder", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Ladder;
            }

            if (string.Equals(normalized, "rope", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Rope;
            }

            if (string.Equals(normalized, "sit", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Sitting;
            }

            if (string.Equals(normalized, "prone", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "proneStab", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Prone;
            }

            if (string.Equals(normalized, "swim", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Swimming;
            }

            if (string.Equals(normalized, "fly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fly2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fly2Move", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fly2Skill", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Flying;
            }

            if (string.Equals(normalized, "dead", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Dead;
            }

            if (string.Equals(normalized, "alert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "hit", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Hit;
            }

            if (IsHiddenLikeAttackAction(normalized))
            {
                return PlayerState.Attacking;
            }

            return PlayerState.Standing;
        }

        internal static bool ShouldDrawRemoteShadowPartnerForTesting(
            bool hasShadowPartnerActionAnimations,
            bool hiddenLikeClient,
            bool hasMorphTemplate,
            bool hasMechanicMode,
            int? skillId,
            int? rawActionCode)
        {
            return hasShadowPartnerActionAnimations
                && !hiddenLikeClient
                && !hasMechanicMode
                && ShadowPartnerClientActionResolver.ShouldRenderClientShadowPartner(skillId, rawActionCode);
        }

        internal static bool ShouldUseRemoteShadowPartnerAttackObservationGateForTesting(
            string observedPlayerActionName,
            PlayerState state)
        {
            return ShouldUseRemoteShadowPartnerAttackObservationGate(observedPlayerActionName, state);
        }

        internal static bool ShouldRetryRemoteShadowPartnerAttackResolutionAfterCreateForTesting(
            string currentActionName,
            string pendingActionName,
            string queuedActionName,
            bool currentActionBlockingHoldActive)
        {
            return ShadowPartnerClientActionResolver.ShouldRetryAttackResolutionAfterCreate(
                currentActionName,
                pendingActionName,
                queuedActionName,
                currentActionBlockingHoldActive);
        }

        internal static bool ShouldRefreshRemoteShadowPartnerObservationForTesting(
            string observedPlayerActionName,
            PlayerState observedState,
            bool observedFacingRight,
            int? observedRawActionCode,
            int observedActionTriggerTime,
            string previousObservedPlayerActionName,
            PlayerState previousObservedState,
            bool previousObservedFacingRight,
            int? previousObservedRawActionCode,
            int previousObservedActionTriggerTime)
        {
            return ShouldRefreshRemoteShadowPartnerObservation(
                observedPlayerActionName,
                observedState,
                observedFacingRight,
                observedRawActionCode,
                observedActionTriggerTime,
                previousObservedPlayerActionName,
                previousObservedState,
                previousObservedFacingRight,
                previousObservedRawActionCode,
                previousObservedActionTriggerTime);
        }

        internal static bool ShouldForceRemoteShadowPartnerAttackReplayForTriggerForTesting(
            int observedActionTriggerTime,
            int previousReplayTriggerTime)
        {
            return ShadowPartnerClientActionResolver.ShouldForceReplayForAttackTrigger(
                observedActionTriggerTime,
                previousReplayTriggerTime);
        }

        internal static (int CurrentActionStartTime, bool CurrentFacingRight)
            ApplyRemoteShadowPartnerSameActionPresentationTransitionForTesting(
                int currentActionStartTime,
                bool currentFacingRight,
                int currentTime,
                bool facingRight,
                bool preserveTimingWhenOnlyFacingChanges,
                bool forceRestartWhenSameAction,
                SkillAnimation currentPlaybackAnimation = null)
        {
            var presentation = new RemoteShadowPartnerPresentationState
            {
                CurrentActionStartTime = currentActionStartTime,
                CurrentFacingRight = currentFacingRight,
                CurrentPlaybackAnimation = currentPlaybackAnimation
            };
            ApplyRemoteShadowPartnerSameActionPresentationTransition(
                presentation,
                currentTime,
                facingRight,
                preserveTimingWhenOnlyFacingChanges,
                forceRestartWhenSameAction);
            return (presentation.CurrentActionStartTime, presentation.CurrentFacingRight);
        }

        private static bool ShouldRefreshRemoteShadowPartnerObservation(
            string observedPlayerActionName,
            PlayerState observedState,
            bool observedFacingRight,
            int? observedRawActionCode,
            int observedActionTriggerTime,
            string previousObservedPlayerActionName,
            PlayerState previousObservedState,
            bool previousObservedFacingRight,
            int? previousObservedRawActionCode,
            int previousObservedActionTriggerTime)
        {
            return !string.Equals(observedPlayerActionName, previousObservedPlayerActionName, StringComparison.OrdinalIgnoreCase)
                || observedState != previousObservedState
                || observedFacingRight != previousObservedFacingRight
                || observedRawActionCode != previousObservedRawActionCode
                || observedActionTriggerTime != previousObservedActionTriggerTime;
        }

        private static bool TryMarkRemoteShadowPartnerForcedReplayActionTrigger(
            RemoteShadowPartnerPresentationState presentation,
            int observedActionTriggerTime)
        {
            if (presentation == null
                || !ShadowPartnerClientActionResolver.ShouldForceReplayForAttackTrigger(
                    observedActionTriggerTime,
                    presentation.LastForcedReplayActionTriggerTime))
            {
                return false;
            }

            presentation.LastForcedReplayActionTriggerTime = observedActionTriggerTime;
            return true;
        }

        private static bool ShouldDrawRemoteShadowPartner(RemoteUserActor actor, int? rawActionCode = null)
        {
            return ShouldDrawRemoteShadowPartnerForTesting(
                actor?.TemporaryStatShadowPartnerSkill?.HasShadowPartnerActionAnimations == true,
                actor?.HiddenLikeClient == true,
                actor?.HasMorphTemplate == true,
                actor?.TemporaryStats.KnownState.MechanicMode.HasValue == true,
                actor?.TemporaryStatShadowPartnerSkillId,
                rawActionCode);
        }

        private static void DrawRemoteTemporaryStatAvatarEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            AssembledFrame frame,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (actor == null || actor.HiddenLikeClient)
            {
                return;
            }

            PruneExpiredRemoteTemporaryStatAvatarEffectTailStates(actor.TemporaryStatAuraEffectTails, currentTime);
            IReadOnlyList<RemoteTemporaryStatAvatarEffectState> drawStates =
                BuildRemoteTemporaryStatAvatarEffectDrawStates(
                    actor?.TemporaryStatSoulArrowEffect,
                    actor?.TemporaryStatWeaponChargeEffect,
                    actor?.TemporaryStatAuraEffect,
                    actor?.TemporaryStatAuraEffectTails,
                    actor?.TemporaryStatMoreWildEffect,
                    actor?.TemporaryStatBarrierEffect,
                    actor?.TemporaryStatBlessingArmorEffect,
                    actor?.TemporaryStatRepeatEffect,
                    actor?.TemporaryStatMagicShieldEffect,
                    actor?.TemporaryStatFinalCutEffect,
                    currentTime);
            for (int i = 0; i < drawStates.Count; i++)
            {
                DrawRemoteTemporaryStatAvatarEffectState(
                    spriteBatch,
                    skeletonRenderer,
                    actor,
                    drawStates[i],
                    frame,
                    screenX,
                    screenY,
                    currentTime,
                    drawFrontLayers);
            }
        }

        private static IReadOnlyList<RemoteTemporaryStatAvatarEffectState> BuildRemoteTemporaryStatAvatarEffectDrawStates(
            RemoteTemporaryStatAvatarEffectState soulArrowState,
            RemoteTemporaryStatAvatarEffectState weaponChargeState,
            RemoteTemporaryStatAvatarEffectState auraState,
            IReadOnlyList<RemoteTemporaryStatAvatarEffectState> auraTailStates,
            RemoteTemporaryStatAvatarEffectState moreWildState,
            RemoteTemporaryStatAvatarEffectState barrierState,
            RemoteTemporaryStatAvatarEffectState blessingArmorState,
            RemoteTemporaryStatAvatarEffectState repeatState,
            RemoteTemporaryStatAvatarEffectState magicShieldState,
            RemoteTemporaryStatAvatarEffectState finalCutState,
            int currentTime)
        {
            var orderedStates = new List<RemoteTemporaryStatAvatarEffectState>(11);

            void AddState(RemoteTemporaryStatAvatarEffectState state)
            {
                if (state != null)
                {
                    orderedStates.Add(state);
                }
            }

            // Client owner-family chain from `CUser::Update` / `CUser::UpdateMoreWildEffect`:
            // SoulArrow -> WeaponCharge -> Aura(affected-layer tails + active) -> MoreWild -> Barrier
            // -> BlessingArmor -> Repeat -> MagicShield -> FinalCut.
            AddState(soulArrowState);
            AddState(weaponChargeState);
            AddState(ResolveLatestRemoteTemporaryStatAuraTailState(auraTailStates, currentTime));

            AddState(auraState);
            AddState(moreWildState);
            AddState(barrierState);
            AddState(blessingArmorState);
            AddState(repeatState);
            AddState(magicShieldState);
            AddState(finalCutState);
            return orderedStates;
        }

        private static void DrawRemoteTransientSkillUseAvatarEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            AssembledFrame frame,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (actor?.TransientSkillUseAvatarEffects == null || actor.HiddenLikeClient)
            {
                return;
            }

            for (int i = 0; i < actor.TransientSkillUseAvatarEffects.Count; i++)
            {
                RemoteTransientSkillUseAvatarEffectState state = actor.TransientSkillUseAvatarEffects[i];
                if (state == null)
                {
                    continue;
                }

                int elapsedTime = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.AnimationStartTime);
                SkillAnimation animation = drawFrontLayers
                    ? state.OverlayAnimation
                    : state.UnderFaceAnimation;
                DrawRemoteTemporaryStatAvatarEffectAnimation(
                    spriteBatch,
                    skeletonRenderer,
                    actor,
                    animation,
                    frame,
                    screenX,
                    screenY,
                    elapsedTime);
            }
        }

        private static void DrawRemoteTemporaryStatAvatarEffectState(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            RemoteTemporaryStatAvatarEffectState state,
            AssembledFrame frame,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (state == null)
            {
                return;
            }

            if (ShouldHideRemoteTemporaryStatAvatarEffect(actor, state))
            {
                return;
            }

            float transitionAlpha = ResolveRemoteTemporaryStatAvatarEffectTransitionAlpha(state, currentTime);
            if (transitionAlpha <= 0f)
            {
                return;
            }

            int elapsedTime = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.AnimationStartTime);
            if (drawFrontLayers)
            {
                DrawRemoteTemporaryStatAvatarEffectAnimation(spriteBatch, skeletonRenderer, actor, state.OverlayAnimation, frame, screenX, screenY, elapsedTime, transitionAlpha);
                DrawRemoteTemporaryStatAvatarEffectAnimation(spriteBatch, skeletonRenderer, actor, state.OverlaySecondaryAnimation, frame, screenX, screenY, elapsedTime, transitionAlpha);
                return;
            }

            DrawRemoteTemporaryStatAvatarEffectAnimation(spriteBatch, skeletonRenderer, actor, state.UnderFaceAnimation, frame, screenX, screenY, elapsedTime, transitionAlpha);
            DrawRemoteTemporaryStatAvatarEffectAnimation(spriteBatch, skeletonRenderer, actor, state.UnderFaceSecondaryAnimation, frame, screenX, screenY, elapsedTime, transitionAlpha);
        }

        private static bool ShouldHideRemoteTemporaryStatAvatarEffect(RemoteUserActor actor, RemoteTemporaryStatAvatarEffectState state)
        {
            if (state?.Skill?.HideAvatarEffectOnRotateAction != true)
            {
                return false;
            }

            int? rawActionCode = actor?.BaseActionRawCode;
            if (!rawActionCode.HasValue
                && CharacterPart.TryGetClientRawActionCode(actor?.ActionName, out int resolvedRawActionCode))
            {
                rawActionCode = resolvedRawActionCode;
            }

            return ClientOwnedAvatarEffectParity.ShouldHideDuringPlayerAction(
                rawActionCode,
                actor?.BaseActionName,
                actor?.ActionName);
        }

        internal static bool ShouldHideRemoteTemporaryStatAvatarEffectForTesting(
            bool hideOnRotateAction,
            int? rawActionCode,
            string baseActionName,
            string actionName)
        {
            if (!hideOnRotateAction)
            {
                return false;
            }

            return ClientOwnedAvatarEffectParity.ShouldHideDuringPlayerAction(rawActionCode, baseActionName, actionName);
        }

        private static void DrawRemoteTemporaryStatAvatarEffectAnimation(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            SkillAnimation animation,
            AssembledFrame ownerFrame,
            int screenX,
            int screenY,
            int elapsedTime,
            float alphaMultiplier = 1f)
        {
            if (animation == null)
            {
                return;
            }

            SkillFrame effectFrame = animation.GetFrameAtTime(elapsedTime);
            if (effectFrame?.Texture == null)
            {
                return;
            }

            ClientOwnedAvatarEffectParity.TryResolveFaceOwnedAvatarEffectAnchor(
                ownerFrame,
                actor.FacingRight,
                screenX,
                screenY,
                animation.PositionCode,
                out int anchorX,
                out int anchorY);

            bool shouldFlip = actor.FacingRight ^ effectFrame.Flip;
            int drawX = shouldFlip
                ? anchorX - (effectFrame.Texture.Width - effectFrame.Origin.X)
                : anchorX - effectFrame.Origin.X;
            int drawY = anchorY - effectFrame.Origin.Y;
            Color tint = Color.White * MathHelper.Clamp(alphaMultiplier, 0f, 1f);
            effectFrame.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, drawX, drawY, tint, shouldFlip, null);
        }

        private static void DrawRemotePacketOwnedEmotionEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime)
        {
            RemotePacketOwnedEmotionState state = actor?.PacketOwnedEmotion;
            if (state?.EffectAnimation == null)
            {
                return;
            }

            int elapsedTime = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.AnimationStartTime);
            DrawRemoteTemporaryStatAvatarEffectAnimation(
                spriteBatch,
                skeletonRenderer,
                actor,
                state.EffectAnimation,
                ownerFrame: null,
                screenX,
                screenY,
                elapsedTime);
        }

        private static void DrawRemoteActiveEffectMotionBlur(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            RemoteActiveEffectMotionBlurState state = actor?.ActiveEffectMotionBlur;
            if (state?.Definition.IsValid != true || state.Snapshots.Count == 0)
            {
                return;
            }

            for (int i = 0; i < state.Snapshots.Count; i++)
            {
                RemoteActiveEffectMotionBlurSnapshot snapshot = state.Snapshots[i];
                if (snapshot?.Layers == null || snapshot.Layers.Count == 0)
                {
                    continue;
                }

                int ageMs = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, snapshot.SampleTime);
                byte snapshotAlpha = ResolveRemoteActiveEffectMotionBlurSnapshotAlpha(
                    ageMs,
                    state.Definition.DelayMs,
                    state.Definition.Alpha);
                if (snapshotAlpha == 0)
                {
                    continue;
                }

                Vector2 drawPosition = state.Definition.Follow
                    ? actor.Position
                    : snapshot.Position;
                bool drawFacingRight = state.Definition.Follow
                    ? actor.FacingRight
                    : snapshot.FacingRight;
                int screenX = (int)Math.Round(drawPosition.X) - mapShiftX + centerX;
                int screenY = (int)Math.Round(drawPosition.Y) - mapShiftY + centerY;
                DrawRemoteMotionBlurLayerSnapshots(
                    spriteBatch,
                    skeletonRenderer,
                    snapshot.Layers,
                    screenX,
                    screenY,
                    snapshot.FeetOffset,
                    drawFacingRight,
                    new Color(byte.MaxValue, byte.MaxValue, byte.MaxValue, snapshotAlpha));
            }
        }

        internal static byte ResolveRemoteActiveEffectMotionBlurSnapshotAlpha(
            int ageMs,
            int delayMs,
            byte baseAlpha)
        {
            if (baseAlpha == 0 || delayMs <= 0 || ageMs >= delayMs)
            {
                return 0;
            }

            float progress = MathHelper.Clamp((float)ageMs / delayMs, 0f, 1f);
            return (byte)Math.Clamp((int)MathF.Round(baseAlpha * (1f - progress)), 0, byte.MaxValue);
        }

        private static void DrawRemoteMotionBlurLayerSnapshots(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            IReadOnlyList<RemoteActiveEffectMotionBlurLayerSnapshot> layerSnapshots,
            int screenX,
            int screenY,
            int feetOffset,
            bool facingRight,
            Color tint)
        {
            if (layerSnapshots == null || layerSnapshots.Count == 0)
            {
                return;
            }

            int adjustedY = screenY - feetOffset;
            for (int layerIndex = 0; layerIndex < layerSnapshots.Count; layerIndex++)
            {
                RemoteActiveEffectMotionBlurLayerSnapshot layerSnapshot = layerSnapshots[layerIndex];
                if (layerSnapshot?.Parts == null || layerSnapshot.Parts.Count == 0)
                {
                    continue;
                }

                for (int partIndex = 0; partIndex < layerSnapshot.Parts.Count; partIndex++)
                {
                    DrawRemoteAssembledPart(
                        spriteBatch,
                        skeletonRenderer,
                        layerSnapshot.Parts[partIndex],
                        screenX,
                        adjustedY,
                        facingRight,
                        tint);
                }
            }
        }

        private static void DrawRemoteActorFrame(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            AssembledFrame frame,
            int screenX,
            int screenY,
            int currentTime)
        {
            if (frame?.Parts == null || frame.Parts.Count == 0)
            {
                return;
            }

            int adjustedY = screenY - frame.FeetOffset;
            int underFaceInsertionIndex = GetUnderFaceInsertionIndex(frame.Parts);
            bool underFaceDrawn = false;

            for (int i = 0; i < frame.Parts.Count; i++)
            {
                if (!underFaceDrawn && i == underFaceInsertionIndex)
                {
                    DrawRemoteUnderFaceSeam(
                        spriteBatch,
                        skeletonRenderer,
                        actor,
                        frame,
                        screenX,
                        screenY,
                        currentTime);
                    underFaceDrawn = true;
                }

                DrawRemoteAssembledPart(spriteBatch, skeletonRenderer, frame.Parts[i], screenX, adjustedY, actor.FacingRight, Color.White);
            }

            if (!underFaceDrawn)
            {
                DrawRemoteUnderFaceSeam(
                    spriteBatch,
                    skeletonRenderer,
                    actor,
                    frame,
                    screenX,
                    screenY,
                    currentTime);
            }
        }

        private static void DrawRemoteUnderFaceSeam(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            RemoteUserActor actor,
            AssembledFrame frame,
            int screenX,
            int screenY,
            int currentTime)
        {
            DrawRemoteTemporaryStatAvatarEffects(
                spriteBatch,
                skeletonRenderer,
                actor,
                frame,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers: false);
            DrawRemoteTransientSkillUseAvatarEffects(
                spriteBatch,
                skeletonRenderer,
                actor,
                frame,
                screenX,
                screenY,
                currentTime,
                drawFrontLayers: false);
            DrawRemoteShadowPartner(
                spriteBatch,
                skeletonRenderer,
                actor,
                screenX,
                screenY,
                currentTime);
        }

        internal static IReadOnlyList<string> ResolveRemoteUnderFaceSeamDrawOrderForTesting(
            bool drawTemporaryStatEffects,
            bool drawTransientSkillUseEffects,
            bool drawShadowPartner)
        {
            var order = new List<string>(3);
            if (drawTemporaryStatEffects)
            {
                order.Add("TemporaryStatEffects");
            }

            if (drawTransientSkillUseEffects)
            {
                order.Add("TransientSkillUseEffects");
            }

            if (drawShadowPartner)
            {
                order.Add("ShadowPartner");
            }

            return order;
        }

        private static void DrawRemoteAssembledPart(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            AssembledPart part,
            int screenX,
            int adjustedY,
            bool flip,
            Color tint)
        {
            if (part?.Texture == null || !part.IsVisible)
            {
                return;
            }

            int partX = flip
                ? screenX - part.OffsetX - part.Texture.Width
                : screenX + part.OffsetX;
            int partY = adjustedY + part.OffsetY;
            Color partColor = part.Tint != Color.White ? part.Tint : tint;
            part.Texture.DrawBackground(spriteBatch, skeletonRenderer, null, partX, partY, partColor, flip, null);
        }

        private static int GetUnderFaceInsertionIndex(IReadOnlyList<AssembledPart> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return 0;
            }

            // Keep remote under-face seam insertion on the same render-layer index
            // resolver used by the local lane; when no under-face layer metadata is
            // available, fall back to the legacy part-type heuristic.
            int[] insertionIndices = PlayerCharacter.GetAvatarRenderLayerInsertionIndices(parts);
            int sharedInsertionIndex = PlayerCharacter.ResolveMirrorImageOverlayInsertionIndex(
                insertionIndices,
                parts.Count);
            if (HasUsableAvatarRenderLayerMetadata(parts)
                && sharedInsertionIndex >= 0
                && sharedInsertionIndex <= parts.Count)
            {
                return sharedInsertionIndex;
            }

            return ResolveLegacyUnderFaceInsertionIndex(parts);
        }

        private static bool HasUsableAvatarRenderLayerMetadata(IReadOnlyList<AssembledPart> parts)
        {
            if (parts == null)
            {
                return false;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i]?.RenderLayer != AvatarRenderLayer.OverCharacter)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveLegacyUnderFaceInsertionIndex(IReadOnlyList<AssembledPart> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return 0;
            }

            int fallbackIndex = parts.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                CharacterPartType partType = parts[i].PartType;
                if (partType == CharacterPartType.Head)
                {
                    fallbackIndex = i + 1;
                    continue;
                }

                if (partType == CharacterPartType.Face
                    || partType == CharacterPartType.Hair
                    || partType == CharacterPartType.Cap
                    || partType == CharacterPartType.CapOverHair
                    || partType == CharacterPartType.CapBelowAccessory
                    || partType == CharacterPartType.Accessory
                    || partType == CharacterPartType.AccessoryOverHair
                    || partType == CharacterPartType.Face_Accessory
                    || partType == CharacterPartType.Eye_Accessory
                    || partType == CharacterPartType.Earrings)
                {
                    return i;
                }
            }

            return fallbackIndex;
        }

        internal static int ResolveRemoteUnderFaceInsertionIndexForTesting(IReadOnlyList<AssembledPart> parts)
        {
            return GetUnderFaceInsertionIndex(parts);
        }

        private static void DrawRemoteShadowPartner(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime)
        {
            string baseActionName = string.IsNullOrWhiteSpace(actor.BaseActionName)
                ? ResolveActionName(actor, MoveActionFromRaw(actor.LastMoveActionRaw))
                : actor.BaseActionName;
            PlayerState state = ResolveRemoteShadowPartnerState(baseActionName);
            int? rawActionCode = ResolveRemoteShadowPartnerObservedRawActionCode(actor, state, baseActionName);
            if (!ShouldDrawRemoteShadowPartner(actor, rawActionCode))
            {
                return;
            }

            UpdateRemoteShadowPartnerPresentation(actor, baseActionName, state, currentTime);
            if (ShadowPartnerClientActionResolver.TryGetPlaybackFrameAtTime(
                    actor.ShadowPartnerPresentation?.CurrentPlaybackAnimation,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.ShadowPartnerPresentation.CurrentActionStartTime),
                    out SkillFrame frame,
                    out int frameElapsedMs) != true
                || frame?.Texture == null)
            {
                return;
            }

            bool facingRight = actor.ShadowPartnerPresentation.CurrentFacingRight;
            bool flip = facingRight ^ frame.Flip;
            Point clientOffset = ResolveRemoteShadowPartnerCurrentClientOffset(
                actor,
                baseActionName,
                state,
                actor.FacingRight,
                rawActionCode,
                currentTime);
            int horizontalOffsetPx = ShadowPartnerClientActionResolver.ResolveHorizontalOffsetPx(
                actor.ShadowPartnerPresentation.CurrentPlaybackAnimation,
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerHorizontalOffsetPx);
            int anchorX = screenX + clientOffset.X + (facingRight ? -horizontalOffsetPx : horizontalOffsetPx);
            int anchorY = screenY + clientOffset.Y;
            int actionElapsedMs = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, actor.ShadowPartnerPresentation.CurrentActionStartTime);
            Color frameTint = RemoteShadowPartnerTint * ResolveRemoteShadowPartnerFrameAlpha(
                actor.ShadowPartnerPresentation?.CurrentPlaybackAnimation,
                frame,
                frameElapsedMs,
                actionElapsedMs);
            if (ShadowPartnerClientActionResolver.TryResolveFrameDrawTransform(
                    frame,
                    anchorX,
                    anchorY,
                    flip,
                    out Vector2 drawPosition,
                    out Vector2 drawOrigin,
                    out float rotationRadians,
                    out SpriteEffects drawEffects)
                && frame.Texture.Texture != null)
            {
                spriteBatch.Draw(
                    frame.Texture.Texture,
                    drawPosition,
                    null,
                    frameTint,
                    rotationRadians,
                    drawOrigin,
                    1f,
                    drawEffects,
                    0f);
                return;
            }

            int drawX = flip
                ? anchorX - (frame.Texture.Width - frame.Origin.X)
                : anchorX - frame.Origin.X;
            int drawY = anchorY - frame.Origin.Y;
            frame.Texture.DrawBackground(spriteBatch, skeletonMeshRenderer, null, drawX, drawY, frameTint, flip, null);
        }

        private static void UpdateTransientSkillUseAvatarEffects(RemoteUserActor actor, int currentTime)
        {
            if (actor?.TransientSkillUseAvatarEffects == null || actor.TransientSkillUseAvatarEffects.Count == 0)
            {
                return;
            }

            for (int i = actor.TransientSkillUseAvatarEffects.Count - 1; i >= 0; i--)
            {
                RemoteTransientSkillUseAvatarEffectState state = actor.TransientSkillUseAvatarEffects[i];
                if (state == null || IsTransientSkillUseAvatarEffectExpired(state, currentTime))
                {
                    actor.TransientSkillUseAvatarEffects.RemoveAt(i);
                }
            }
        }

        private static bool IsTransientSkillUseAvatarEffectExpired(RemoteTransientSkillUseAvatarEffectState state, int currentTime)
        {
            int elapsedTime = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.AnimationStartTime);
            bool overlayComplete = state.OverlayAnimation == null || state.OverlayAnimation.IsComplete(elapsedTime);
            bool underFaceComplete = state.UnderFaceAnimation == null || state.UnderFaceAnimation.IsComplete(elapsedTime);
            return overlayComplete && underFaceComplete;
        }

        internal static void UpdateRemoteShadowPartnerPresentation(
            RemoteUserActor actor,
            string observedPlayerActionName,
            PlayerState state,
            int currentTime)
        {
            int? rawActionCode = ResolveRemoteShadowPartnerObservedRawActionCode(actor, state, observedPlayerActionName);
            if (!ShouldDrawRemoteShadowPartner(actor, rawActionCode))
            {
                return;
            }

            actor.ShadowPartnerPresentation ??= new RemoteShadowPartnerPresentationState();
            RemoteShadowPartnerPresentationState presentation = actor.ShadowPartnerPresentation;
            int playbackRawActionCode = ResolveRemoteShadowPartnerPlaybackRawActionCode(actor, rawActionCode);
            int actionTriggerTime = ResolveRemoteShadowPartnerObservedActionTriggerTime(actor, state);
            string fallbackActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
            string resolvedObservedAction = ResolveRemoteShadowPartnerActionName(
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                observedPlayerActionName,
                state,
                fallbackActionName,
                actor.Build?.GetWeapon()?.WeaponType,
                rawActionCode,
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);
            SkillAnimation resolvedObservedPlayback = ResolveRemoteShadowPartnerPlaybackAnimation(
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                resolvedObservedAction,
                observedPlayerActionName,
                playbackRawActionCode,
                actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);

            if (!presentation.IsActionInitialized)
            {
                presentation.IsActionInitialized = true;
                presentation.ObservedPlayerActionName = observedPlayerActionName;
                presentation.ObservedPlayerState = state;
                presentation.ObservedPlayerFacingRight = actor.FacingRight;
                presentation.ObservedRawActionCode = rawActionCode;
                presentation.ObservedPlayerActionTriggerTime = actionTriggerTime;
                TrySeedRemoteShadowPartnerClientOffset(
                    actor,
                    observedPlayerActionName,
                    state,
                    actor.FacingRight,
                    rawActionCode,
                    currentTime,
                    out _);
                bool observedAttackAction = ShouldUseRemoteShadowPartnerAttackObservationGate(
                    observedPlayerActionName,
                    state);
                string resolvedObservedAttackAction = null;
                SkillAnimation resolvedObservedAttackPlayback = null;
                bool hasResolvedObservedAttackAction = observedAttackAction
                                                     && TryResolveRemoteShadowPartnerAttackAction(
                                                         actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                                                         observedPlayerActionName,
                                                         state,
                                                         actor.Build?.GetWeapon()?.WeaponType,
                                                         rawActionCode,
                                                         actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames,
                                                         out resolvedObservedAttackAction,
                                                         out resolvedObservedAttackPlayback);

                string createActionName = ResolveRemoteShadowPartnerCreateActionName(
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                    state);
                if (!string.IsNullOrWhiteSpace(createActionName))
                {
                    SetRemoteShadowPartnerAction(
                        actor,
                        createActionName,
                        currentTime,
                        actor.FacingRight,
                        ResolveRemoteShadowPartnerPlaybackAnimation(
                            actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                            createActionName,
                            observedPlayerActionName,
                            playbackRawActionCode,
                            actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames));

                    string queuedObservedAction = observedAttackAction
                        ? (hasResolvedObservedAttackAction ? resolvedObservedAttackAction : null)
                        : resolvedObservedAction;
                    SkillAnimation queuedObservedPlayback = observedAttackAction
                        ? (hasResolvedObservedAttackAction ? resolvedObservedAttackPlayback : null)
                        : resolvedObservedPlayback;
                    if (!string.IsNullOrWhiteSpace(queuedObservedAction))
                    {
                        presentation.QueuedActionName = queuedObservedAction;
                        presentation.QueuedPlaybackAnimation = queuedObservedPlayback;
                        presentation.QueuedFacingRight = actor.FacingRight;
                        presentation.QueuedForceReplay = observedAttackAction
                            && TryMarkRemoteShadowPartnerForcedReplayActionTrigger(presentation, actionTriggerTime);
                    }

                    return;
                }

                SetRemoteShadowPartnerAction(
                    actor,
                    resolvedObservedAction,
                    currentTime,
                    actor.FacingRight,
                    resolvedObservedPlayback,
                    forceRestartWhenSameAction: observedAttackAction && actionTriggerTime != int.MinValue);
                return;
            }

            if (TryAdvanceRemoteShadowPartnerQueuedAction(actor, currentTime))
            {
                return;
            }

            bool observedChanged = ShouldRefreshRemoteShadowPartnerObservation(
                observedPlayerActionName,
                state,
                actor.FacingRight,
                rawActionCode,
                actionTriggerTime,
                presentation.ObservedPlayerActionName,
                presentation.ObservedPlayerState,
                presentation.ObservedPlayerFacingRight,
                presentation.ObservedRawActionCode,
                presentation.ObservedPlayerActionTriggerTime);
            if (observedChanged)
            {
                presentation.ObservedPlayerActionName = observedPlayerActionName;
                presentation.ObservedPlayerState = state;
                presentation.ObservedPlayerFacingRight = actor.FacingRight;
                presentation.ObservedRawActionCode = rawActionCode;
                presentation.ObservedPlayerActionTriggerTime = actionTriggerTime;

                bool observedAttackAction = ShouldUseRemoteShadowPartnerAttackObservationGate(observedPlayerActionName, state);
                if (observedAttackAction)
                {
                    if (TryResolveRemoteShadowPartnerAttackAction(
                            actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                            observedPlayerActionName,
                            state,
                            actor.Build?.GetWeapon()?.WeaponType,
                            rawActionCode,
                            actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames,
                            out string resolvedObservedAttackAction,
                            out SkillAnimation resolvedObservedAttackPlayback))
                    {
                        presentation.PendingActionName = resolvedObservedAttackAction;
                        presentation.PendingPlaybackAnimation = resolvedObservedAttackPlayback;
                        presentation.PendingActionReadyTime = currentTime + ResolveRemoteShadowPartnerAttackDelayMs(
                            actor,
                            resolvedObservedAttackAction,
                            resolvedObservedAttackPlayback);
                        presentation.PendingFacingRight = actor.FacingRight;
                        presentation.PendingForceReplay = TryMarkRemoteShadowPartnerForcedReplayActionTrigger(
                            presentation,
                            actionTriggerTime);
                    }
                    else
                    {
                        presentation.PendingActionName = null;
                        presentation.PendingPlaybackAnimation = null;
                        presentation.PendingForceReplay = false;
                        presentation.QueuedActionName = null;
                        presentation.QueuedPlaybackAnimation = null;
                        presentation.QueuedForceReplay = false;
                    }
                }
                else
                {
                    if (ShouldHoldRemoteShadowPartnerCurrentAction(actor, currentTime))
                    {
                        presentation.QueuedActionName = resolvedObservedAction;
                        presentation.QueuedPlaybackAnimation = resolvedObservedPlayback;
                        presentation.QueuedFacingRight = actor.FacingRight;
                        presentation.QueuedForceReplay = false;
                    }
                    else
                    {
                        SetRemoteShadowPartnerAction(
                            actor,
                            resolvedObservedAction,
                            currentTime,
                            actor.FacingRight,
                            resolvedObservedPlayback,
                            preserveTimingWhenOnlyFacingChanges: true);
                    }

                    presentation.PendingActionName = null;
                    presentation.PendingPlaybackAnimation = null;
                    presentation.PendingForceReplay = false;
                }
            }

            TryQueuePostCreateRemoteShadowPartnerAttackResolution(
                actor,
                observedPlayerActionName,
                state,
                rawActionCode,
                actionTriggerTime,
                currentTime);

            if (!string.IsNullOrWhiteSpace(presentation.PendingActionName)
                && currentTime >= presentation.PendingActionReadyTime)
            {
                string pendingActionName = presentation.PendingActionName;
                SkillAnimation pendingPlaybackAnimation = presentation.PendingPlaybackAnimation;
                bool pendingFacingRight = presentation.PendingFacingRight;
                bool pendingForceReplay = presentation.PendingForceReplay;
                presentation.PendingActionName = null;
                presentation.PendingPlaybackAnimation = null;
                presentation.PendingForceReplay = false;

                if (ShouldHoldRemoteShadowPartnerCurrentAction(actor, currentTime))
                {
                    presentation.QueuedActionName = pendingActionName;
                    presentation.QueuedPlaybackAnimation = pendingPlaybackAnimation;
                    presentation.QueuedFacingRight = pendingFacingRight;
                    presentation.QueuedForceReplay = pendingForceReplay;
                }
                else
                {
                    SetRemoteShadowPartnerAction(
                        actor,
                        pendingActionName,
                        currentTime,
                        pendingFacingRight,
                        pendingPlaybackAnimation,
                        forceRestartWhenSameAction: pendingForceReplay);
                }
            }

            if (string.IsNullOrWhiteSpace(presentation.CurrentActionName))
            {
                string fallbackAction = ResolveRemoteShadowPartnerFallbackAction(observedPlayerActionName, state);
                string resolvedFallbackAction = ResolveRemoteShadowPartnerActionName(
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                    fallbackAction,
                    state,
                    fallbackActionName,
                    actor.Build?.GetWeapon()?.WeaponType,
                    rawActionCode,
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);
                SetRemoteShadowPartnerAction(
                    actor,
                    resolvedFallbackAction,
                    currentTime,
                    actor.FacingRight,
                    ResolveRemoteShadowPartnerPlaybackAnimation(
                        actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                        resolvedFallbackAction,
                        fallbackAction,
                        playbackRawActionCode,
                        actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames));
            }
        }

        private static Point ResolveRemoteShadowPartnerCurrentClientOffset(
            RemoteUserActor actor,
            string observedPlayerActionName,
            PlayerState state,
            bool facingRight,
            int? rawActionCode,
            int currentTime)
        {
            if (actor == null)
            {
                return Point.Zero;
            }

            if (TrySeedRemoteShadowPartnerClientOffset(
                    actor,
                    observedPlayerActionName,
                    state,
                    facingRight,
                    rawActionCode,
                    currentTime,
                    out Point targetOffset))
            {
                return targetOffset;
            }

            targetOffset = ResolveRemoteShadowPartnerTargetClientOffset(
                actor,
                observedPlayerActionName,
                state,
                facingRight,
                rawActionCode);

            if (targetOffset != actor.ShadowPartnerPresentation.ClientOffsetTargetPx)
            {
                actor.ShadowPartnerPresentation.ClientOffsetStartPx = actor.ShadowPartnerPresentation.CurrentClientOffsetPx;
                actor.ShadowPartnerPresentation.ClientOffsetTargetPx = targetOffset;
                actor.ShadowPartnerPresentation.ClientOffsetTransitionStartTime = currentTime;
            }

            actor.ShadowPartnerPresentation.CurrentClientOffsetPx = ShadowPartnerClientActionResolver.InterpolateClientOffset(
                actor.ShadowPartnerPresentation.ClientOffsetStartPx,
                actor.ShadowPartnerPresentation.ClientOffsetTargetPx,
                actor.ShadowPartnerPresentation.ClientOffsetTransitionStartTime,
                currentTime,
                RemoteShadowPartnerTransitionDurationMs);
            return actor.ShadowPartnerPresentation.CurrentClientOffsetPx;
        }

        private static Point ResolveRemoteShadowPartnerTargetClientOffset(
            RemoteUserActor actor,
            string observedPlayerActionName,
            PlayerState state,
            bool facingRight,
            int? rawActionCode)
        {
            if (actor == null)
            {
                return Point.Zero;
            }

            return ShadowPartnerClientActionResolver.ResolveClientTargetOffset(
                observedPlayerActionName,
                state,
                facingRight,
                RemoteShadowPartnerClientSideOffsetPx,
                RemoteShadowPartnerClientBackActionOffsetYPx,
                rawActionCode,
                actor.HasMorphTemplate,
                HasRemoteGhostActionTransform(actor, observedPlayerActionName));
        }

        private static bool TrySeedRemoteShadowPartnerClientOffset(
            RemoteUserActor actor,
            string observedPlayerActionName,
            PlayerState state,
            bool facingRight,
            int? rawActionCode,
            int currentTime,
            out Point targetOffset)
        {
            targetOffset = Point.Zero;
            if (actor == null)
            {
                return false;
            }

            targetOffset = ResolveRemoteShadowPartnerTargetClientOffset(
                actor,
                observedPlayerActionName,
                state,
                facingRight,
                rawActionCode);
            actor.ShadowPartnerPresentation ??= new RemoteShadowPartnerPresentationState();
            if (actor.ShadowPartnerPresentation.IsInitialized)
            {
                return false;
            }

            SeedRemoteShadowPartnerClientOffset(
                actor.ShadowPartnerPresentation,
                targetOffset,
                currentTime);
            return true;
        }

        private static void SeedRemoteShadowPartnerClientOffset(
            RemoteShadowPartnerPresentationState presentation,
            Point targetOffset,
            int currentTime)
        {
            if (presentation == null)
            {
                return;
            }

            presentation.CurrentClientOffsetPx = targetOffset;
            presentation.ClientOffsetStartPx = targetOffset;
            presentation.ClientOffsetTargetPx = targetOffset;
            presentation.ClientOffsetTransitionStartTime = currentTime;
            presentation.IsInitialized = true;
        }

        internal static RemoteShadowPartnerPresentationState SeedRemoteShadowPartnerClientOffsetForTesting(
            string observedPlayerActionName,
            PlayerState state,
            bool facingRight,
            int? rawActionCode,
            bool hasMorphTransform,
            bool hasGhostTransform,
            int currentTime)
        {
            var presentation = new RemoteShadowPartnerPresentationState();
            Point targetOffset = ShadowPartnerClientActionResolver.ResolveClientTargetOffset(
                observedPlayerActionName,
                state,
                facingRight,
                RemoteShadowPartnerClientSideOffsetPx,
                RemoteShadowPartnerClientBackActionOffsetYPx,
                rawActionCode,
                hasMorphTransform,
                hasGhostTransform);
            SeedRemoteShadowPartnerClientOffset(presentation, targetOffset, currentTime);
            return presentation;
        }

        private static bool HasRemoteGhostActionTransform(RemoteUserActor actor, string observedPlayerActionName)
        {
            return actor?.TemporaryStats.KnownState.GhostId.HasValue == true
                   || IsRelationshipOverlayGhostAction(actor?.ActionName)
                   || IsRelationshipOverlayGhostAction(observedPlayerActionName);
        }

        private static bool TryAdvanceRemoteShadowPartnerQueuedAction(RemoteUserActor actor, int currentTime)
        {
            RemoteShadowPartnerPresentationState presentation = actor?.ShadowPartnerPresentation;
            if (presentation == null || string.IsNullOrWhiteSpace(presentation.QueuedActionName))
            {
                return false;
            }

            if (ShouldHoldRemoteShadowPartnerCurrentAction(actor, currentTime))
            {
                return false;
            }

            string queuedActionName = presentation.QueuedActionName;
            SkillAnimation queuedPlaybackAnimation = presentation.QueuedPlaybackAnimation;
            bool queuedFacingRight = presentation.QueuedFacingRight;
            bool queuedForceReplay = presentation.QueuedForceReplay;
            presentation.QueuedActionName = null;
            presentation.QueuedPlaybackAnimation = null;
            presentation.QueuedForceReplay = false;
            SetRemoteShadowPartnerAction(
                actor,
                queuedActionName,
                currentTime,
                queuedFacingRight,
                queuedPlaybackAnimation,
                forceRestartWhenSameAction: queuedForceReplay);
            return true;
        }

        private static void SetRemoteShadowPartnerAction(
            RemoteUserActor actor,
            string actionName,
            int currentTime,
            bool facingRight,
            SkillAnimation playbackAnimation,
            bool preserveTimingWhenOnlyFacingChanges = false,
            bool forceRestartWhenSameAction = false)
        {
            if (actor?.ShadowPartnerPresentation == null
                || actor.TemporaryStatShadowPartnerSkill?.ShadowPartnerActionAnimations == null
                || string.IsNullOrWhiteSpace(actionName)
                || !actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations.ContainsKey(actionName))
            {
                return;
            }

            RemoteShadowPartnerPresentationState presentation = actor.ShadowPartnerPresentation;
            if (string.Equals(presentation.CurrentActionName, actionName, StringComparison.OrdinalIgnoreCase))
            {
                presentation.CurrentPlaybackAnimation = playbackAnimation
                    ?? ResolveRemoteShadowPartnerPlaybackAnimation(
                        actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                        actionName,
                        presentation.ObservedPlayerActionName,
                        presentation.ObservedRawActionCode.GetValueOrDefault(-1),
                        actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);

                ApplyRemoteShadowPartnerSameActionPresentationTransition(
                    presentation,
                    currentTime,
                    facingRight,
                    preserveTimingWhenOnlyFacingChanges,
                    forceRestartWhenSameAction);
                return;
            }

            presentation.CurrentActionName = actionName;
            presentation.CurrentPlaybackAnimation = playbackAnimation
                ?? ResolveRemoteShadowPartnerPlaybackAnimation(
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                    actionName,
                    presentation.ObservedPlayerActionName,
                    presentation.ObservedRawActionCode.GetValueOrDefault(-1),
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames);
            presentation.CurrentActionStartTime = currentTime;
            presentation.CurrentFacingRight = facingRight;
        }

        private static void ApplyRemoteShadowPartnerSameActionPresentationTransition(
            RemoteShadowPartnerPresentationState presentation,
            int currentTime,
            bool facingRight,
            bool preserveTimingWhenOnlyFacingChanges,
            bool forceRestartWhenSameAction)
        {
            if (presentation == null)
            {
                return;
            }

            if (forceRestartWhenSameAction)
            {
                presentation.CurrentActionStartTime = currentTime;
                presentation.CurrentFacingRight = facingRight;
                return;
            }

            if (presentation.CurrentFacingRight == facingRight)
            {
                return;
            }

            presentation.CurrentFacingRight = facingRight;
            bool preserveTimingForFacingChange = preserveTimingWhenOnlyFacingChanges
                || ShadowPartnerClientActionResolver.ShouldPreserveOneShotAlphaLifetimeOnFacingChange(
                    presentation.CurrentPlaybackAnimation,
                    ResolveRemoteTemporaryStatTickElapsedMs(currentTime, presentation.CurrentActionStartTime));
            if (preserveTimingForFacingChange)
            {
                return;
            }

            presentation.CurrentActionStartTime = currentTime;
        }

        private static bool ShouldHoldRemoteShadowPartnerCurrentAction(RemoteUserActor actor, int currentTime)
        {
            RemoteShadowPartnerPresentationState presentation = actor?.ShadowPartnerPresentation;
            if (presentation == null || string.IsNullOrWhiteSpace(presentation.CurrentActionName))
            {
                return false;
            }

            int elapsedTime = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, presentation.CurrentActionStartTime);
            return ShadowPartnerClientActionResolver.ShouldHoldBlockingAction(
                presentation.CurrentActionName,
                presentation.CurrentPlaybackAnimation,
                actor?.TemporaryStatShadowPartnerSkill?.ShadowPartnerActionAnimations,
                elapsedTime);
        }

        private static int ResolveRemoteShadowPartnerAttackDelayMs(
            RemoteUserActor actor,
            string actionName,
            SkillAnimation playbackAnimation = null)
        {
            return ShadowPartnerClientActionResolver.ResolveAttackDelayMs(
                actor?.TemporaryStatShadowPartnerSkill?.ShadowPartnerActionAnimations,
                actionName,
                playbackAnimation,
                RemoteShadowPartnerAttackDelayMs);
        }

        internal static int? ResolveRemoteShadowPartnerObservedRawActionCode(
            RemoteUserActor actor,
            PlayerState state,
            string observedPlayerActionName)
        {
            if (actor == null)
            {
                return null;
            }

            if (actor.BaseActionRawCode.HasValue
                && (state == PlayerState.Attacking
                    || ShouldPreferRemoteShadowPartnerBaseRawAction(actor, observedPlayerActionName)))
            {
                return actor.BaseActionRawCode;
            }

            return actor.LastMoveActionRaw;
        }

        internal static int ResolveRemoteShadowPartnerPlaybackRawActionCode(RemoteUserActor actor, int? observedRawActionCode)
        {
            return observedRawActionCode ?? actor?.LastMoveActionRaw ?? 0;
        }

        private static bool ShouldPreferRemoteShadowPartnerBaseRawAction(
            RemoteUserActor actor,
            string observedPlayerActionName)
        {
            if (actor == null || !actor.BaseActionRawCode.HasValue)
            {
                return false;
            }

            if (!actor.MovementDrivenActionSelection)
            {
                return true;
            }

            string normalizedObservedActionName = NormalizeActionName(observedPlayerActionName, allowSitFallback: false);
            if (string.IsNullOrWhiteSpace(normalizedObservedActionName))
            {
                return false;
            }

            string normalizedBaseActionName = NormalizeActionName(actor.BaseActionName, allowSitFallback: false);
            if (string.Equals(normalizedBaseActionName, normalizedObservedActionName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string weaponType = actor.Build?.GetWeapon()?.WeaponType;

            if (CharacterPart.TryGetActionStringFromCode(actor.BaseActionRawCode.Value, out string rawActionName))
            {
                string normalizedRawActionName = NormalizeActionName(rawActionName, allowSitFallback: false);
                if (string.Equals(normalizedRawActionName, normalizedObservedActionName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (ShadowPartnerHelperActionFamiliesMatch(
                        rawActionName,
                        observedPlayerActionName,
                        weaponType,
                        actor.BaseActionRawCode,
                        weaponType,
                        actor.BaseActionRawCode,
                        actor.TemporaryStatShadowPartnerSkill?.ShadowPartnerSupportedRawActionNames,
                        actor.TemporaryStatShadowPartnerSkill?.ShadowPartnerSupportedRawActionNames))
                {
                    return true;
                }
            }

            return ShadowPartnerHelperActionFamiliesMatch(
                actor.BaseActionName,
                observedPlayerActionName,
                weaponType,
                actor.BaseActionRawCode,
                weaponType,
                actor.BaseActionRawCode,
                actor.TemporaryStatShadowPartnerSkill?.ShadowPartnerSupportedRawActionNames,
                actor.TemporaryStatShadowPartnerSkill?.ShadowPartnerSupportedRawActionNames);
        }

        internal static bool ShadowPartnerHelperActionFamiliesMatch(
            string leftActionName,
            string rightActionName,
            string leftWeaponType = null,
            int? leftRawActionCode = null,
            string rightWeaponType = null,
            int? rightRawActionCode = null,
            IReadOnlySet<string> leftSupportedRawActionNames = null,
            IReadOnlySet<string> rightSupportedRawActionNames = null)
        {
            if (string.IsNullOrWhiteSpace(leftActionName) || string.IsNullOrWhiteSpace(rightActionName))
            {
                return false;
            }

            var leftCandidates = CollectShadowPartnerHelperActionIdentityCandidates(
                leftActionName,
                leftWeaponType,
                leftRawActionCode,
                leftSupportedRawActionNames);
            var rightCandidates = CollectShadowPartnerHelperActionIdentityCandidates(
                rightActionName,
                rightWeaponType,
                rightRawActionCode,
                rightSupportedRawActionNames);
            return leftCandidates.Overlaps(rightCandidates);
        }

        private static HashSet<string> CollectShadowPartnerHelperActionIdentityCandidates(
            string actionName,
            string weaponType = null,
            int? rawActionCode = null,
            IReadOnlySet<string> supportedRawActionNames = null)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string normalizedActionName = NormalizeActionName(actionName, allowSitFallback: false);
            if (!string.IsNullOrWhiteSpace(normalizedActionName)
                && ShadowPartnerClientActionResolver.IsSupportedRawActionForFamily(normalizedActionName, supportedRawActionNames)
                && !ShadowPartnerClientActionResolver.ShouldSuppressRawBackedGenericAttackIdentityCandidate(
                    normalizedActionName,
                    rawActionCode))
            {
                candidates.Add(normalizedActionName);
            }

            PlayerState state = ResolveShadowPartnerActionIdentityState(normalizedActionName);
            foreach (string candidate in ShadowPartnerClientActionResolver.EnumerateHelperIdentityCandidates(
                         actionName,
                         state,
                         weaponType,
                         rawActionCode,
                         supportedRawActionNames))
            {
                string normalizedCandidate = NormalizeActionName(candidate, allowSitFallback: false);
                if (!string.IsNullOrWhiteSpace(normalizedCandidate))
                {
                    candidates.Add(normalizedCandidate);
                }
            }

            return candidates;
        }

        private static PlayerState ResolveShadowPartnerActionIdentityState(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return PlayerState.Standing;
            }

            if (string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Jumping;
            }

            if (string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Ladder;
            }

            if (string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Rope;
            }

            if (string.Equals(actionName, "sit", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Sitting;
            }

            if (string.Equals(actionName, "prone", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "prone2", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Prone;
            }

            if (string.Equals(actionName, "swim", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Swimming;
            }

            if (string.Equals(actionName, "fly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "fly2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "fly2Move", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "fly2Skill", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "ghostfly", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Flying;
            }

            if (string.Equals(actionName, "dead", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Dead;
            }

            if (actionName.StartsWith("walk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Walking;
            }

            if (actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "hit", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerState.Hit;
            }

            return ShadowPartnerClientActionResolver.IsAttackAction(actionName)
                ? PlayerState.Attacking
                : PlayerState.Standing;
        }

        private static int ResolveRemoteShadowPartnerObservedActionTriggerTime(RemoteUserActor actor, PlayerState state)
        {
            if (actor == null || state != PlayerState.Attacking)
            {
                return int.MinValue;
            }

            return actor.BaseActionStartTime;
        }

        private static string ResolveRemoteShadowPartnerCreateActionName(
            IReadOnlyDictionary<string, SkillAnimation> actionAnimations,
            PlayerState state)
        {
            return ShadowPartnerClientActionResolver.ResolveCreateActionName(actionAnimations, state);
        }

        private static string ResolveRemoteShadowPartnerFallbackAction(string observedPlayerActionName, PlayerState state)
        {
            return state switch
            {
                PlayerState.Walking => "walk1",
                PlayerState.Jumping or PlayerState.Falling => "jump",
                PlayerState.Ladder => "ladder",
                PlayerState.Rope => "rope",
                PlayerState.Swimming or PlayerState.Flying => "fly",
                PlayerState.Sitting => "sit",
                PlayerState.Prone => "prone",
                PlayerState.Dead => "dead",
                PlayerState.Attacking when !string.IsNullOrWhiteSpace(observedPlayerActionName) => observedPlayerActionName,
                _ => "stand1"
            };
        }

        private static bool ShouldUseRemoteShadowPartnerAttackObservationGate(
            string observedPlayerActionName,
            PlayerState state)
        {
            return ShadowPartnerClientActionResolver.ShouldUseAttackIdentityForObservation(observedPlayerActionName, state);
        }

        private static void TryQueuePostCreateRemoteShadowPartnerAttackResolution(
            RemoteUserActor actor,
            string observedPlayerActionName,
            PlayerState state,
            int? rawActionCode,
            int observedActionTriggerTime,
            int currentTime)
        {
            RemoteShadowPartnerPresentationState presentation = actor?.ShadowPartnerPresentation;
            if (presentation == null
                || !ShouldUseRemoteShadowPartnerAttackObservationGate(observedPlayerActionName, state)
                || !ShadowPartnerClientActionResolver.ShouldRetryAttackResolutionAfterCreate(
                    presentation.CurrentActionName,
                    presentation.PendingActionName,
                    presentation.QueuedActionName,
                    ShouldHoldRemoteShadowPartnerCurrentAction(actor, currentTime)))
            {
                return;
            }

            if (!TryResolveRemoteShadowPartnerAttackAction(
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerActionAnimations,
                    observedPlayerActionName,
                    state,
                    actor.Build?.GetWeapon()?.WeaponType,
                    rawActionCode,
                    actor.TemporaryStatShadowPartnerSkill.ShadowPartnerSupportedRawActionNames,
                    out string resolvedAttackAction,
                    out SkillAnimation resolvedAttackPlayback))
            {
                return;
            }

            presentation.PendingActionName = resolvedAttackAction;
            presentation.PendingPlaybackAnimation = resolvedAttackPlayback;
            presentation.PendingActionReadyTime = currentTime + ResolveRemoteShadowPartnerAttackDelayMs(
                actor,
                resolvedAttackAction,
                resolvedAttackPlayback);
            presentation.PendingFacingRight = actor.FacingRight;
            presentation.PendingForceReplay = TryMarkRemoteShadowPartnerForcedReplayActionTrigger(
                presentation,
                observedActionTriggerTime);
        }

        private static float ResolveRemoteShadowPartnerFrameAlpha(
            SkillAnimation animation,
            SkillFrame frame,
            int frameElapsedMs,
            int actionElapsedMs)
        {
            return ShadowPartnerClientActionResolver.ResolveFrameAlphaForPlayback(
                animation,
                frame,
                frameElapsedMs,
                actionElapsedMs);
        }

        private void RegisterMeleeAfterImage(
            RemoteUserActor actor,
            int skillId,
            string actionName,
            int currentTime,
            int masteryPercent,
            int chargeElement,
            int? rawActionCode = null)
        {
            if (_skillLoader == null
                || actor?.Build == null
                || actor.Assembler == null)
            {
                actor?.ClearMeleeAfterImage();
                return;
            }

            WeaponPart weapon = actor.Build.GetWeapon();
            SkillData skill = skillId > 0 ? _skillLoader.LoadSkill(skillId) : null;
            int effectiveChargeElement = AfterImageChargeSkillResolver.IsKnownChargeElement(chargeElement)
                ? chargeElement
                : ResolveRemoteAfterImageChargeElement(
                    actor,
                    chargeSkillId: 0,
                    skillId,
                    skill,
                    admitSkillElementAttribute: skill?.AttackType == SkillAttackType.Melee);
            if (!_skillLoader.TryResolveMeleeAfterImageAction(
                    skill,
                    weapon,
                    EnumerateRemoteAfterImageActionNames(actor, skill, actionName, rawActionCode),
                    actor.Build.Level,
                    masteryPercent,
                    effectiveChargeElement,
                    out MeleeAfterImageAction afterImageAction))
            {
                afterImageAction = _skillLoader.ApplyClientMeleeRangeOverride(null, skillId, rawActionCode, actor.FacingRight);
                if (afterImageAction != null)
                {
                    actor.ApplyMeleeAfterImage(
                        skillId,
                        actionName,
                        afterImageAction,
                        currentTime,
                        actor.FacingRight,
                        GetActionDuration(actor.Assembler, actionName));
                    return;
                }

                if (actor.MeleeAfterImage?.FadeStartTime < 0)
                {
                    actor.ClearMeleeAfterImage();
                }

                return;
            }

            afterImageAction = _skillLoader.ApplyClientMeleeRangeOverride(afterImageAction, skillId, rawActionCode, actor.FacingRight);

            actor.ApplyMeleeAfterImage(
                skillId,
                actionName,
                afterImageAction,
                currentTime,
                actor.FacingRight,
                GetActionDuration(actor.Assembler, actionName));
        }

        private bool TryApplyRemotePreparedSkillRelease(
            RemoteUserActor actor,
            int skillId,
            int? preparedSkillReleaseFollowUpValue,
            out int releasedPreparedSkillId)
        {
            releasedPreparedSkillId = 0;
            if (actor?.PreparedSkill == null
                || !PreparedSkillHudRules.TryResolveRemotePreparedSkillReleaseOwner(
                    skillId,
                    preparedSkillReleaseFollowUpValue,
                    out int preparedSkillId)
                || actor.PreparedSkill.SkillId != preparedSkillId)
            {
                return false;
            }

            releasedPreparedSkillId = actor.PreparedSkill.SkillId;
            actor.PreparedSkill = null;
            return true;
        }

        private void TryRegisterRemotePreparedSkillReleaseSkillUse(
            RemoteUserActor actor,
            int releasedPreparedSkillId,
            int currentTime)
        {
            if (actor == null
                || releasedPreparedSkillId <= 0
                || !RemotePreparedSkillUseEffectSkillIds.Contains(releasedPreparedSkillId))
            {
                return;
            }

            SkillUseRegistered?.Invoke(new RemoteSkillUsePresentation(
                actor.CharacterId,
                releasedPreparedSkillId,
                null,
                actor.FacingRight,
                currentTime,
                ResolveRemotePreparedSkillUseReleaseBranchNames(releasedPreparedSkillId)));
        }

        private IReadOnlyList<string> ResolveRemotePreparedSkillUseStartBranchNames(int skillId, bool isHolding)
        {
            SkillData skill = skillId > 0 ? _skillLoader?.LoadSkill(skillId) : null;
            string branchName = isHolding
                ? skill?.KeydownEffect?.Name
                : skill?.PrepareEffect?.Name;
            if (string.IsNullOrWhiteSpace(branchName))
            {
                branchName = isHolding ? "keydown" : "prepare";
            }

            return new[] { branchName };
        }

        private IReadOnlyList<string> ResolveRemotePreparedSkillUseReleaseBranchNames(int skillId)
        {
            SkillData skill = skillId > 0 ? _skillLoader?.LoadSkill(skillId) : null;
            string branchName = skill?.KeydownEndEffect?.Name;
            if (string.IsNullOrWhiteSpace(branchName))
            {
                branchName = "keydownend";
            }

            return new[] { branchName };
        }

        private string ResolveRemoteMeleeActionName(
            RemoteUserActor actor,
            SkillData skill,
            string actionName,
            int? rawActionCode,
            int masteryPercent,
            int chargeElement)
        {
            if (actor?.Build == null)
            {
                return actionName;
            }

            WeaponPart weapon = actor.Build.GetWeapon();
            foreach (string candidate in EnumerateRemoteActionResolutionCandidates(actor, skill, actionName, rawActionCode))
            {
                if (_skillLoader != null
                    && _skillLoader.TryResolveMeleeAfterImageAction(
                        skill,
                        weapon,
                        candidate,
                        actor.Build.Level,
                        masteryPercent,
                        chargeElement,
                        out _,
                        out string matchedAfterImageActionName))
                {
                    string preferredActionName = ResolvePreferredRemoteMeleeActionName(actor, candidate, matchedAfterImageActionName);
                    if (!string.IsNullOrWhiteSpace(preferredActionName))
                    {
                        return preferredActionName;
                    }
                }

                if (actor.Assembler?.GetAnimation(candidate)?.Length > 0)
                {
                    return candidate;
                }
            }

            if (_skillLoader != null
                && actor?.Build != null
                && _skillLoader.TryResolveUniqueMeleeAfterImageActionName(
                    skill,
                    actor.Build.GetWeapon(),
                    EnumerateRemoteActionResolutionCandidates(actor, skill, actionName, rawActionCode),
                    actor.Build.Level,
                    masteryPercent,
                    chargeElement,
                    out string uniqueAfterImageActionName))
            {
                string preferredActionName = ResolvePreferredRemoteMeleeActionName(actor, actionName, uniqueAfterImageActionName);
                if (!string.IsNullOrWhiteSpace(preferredActionName))
                {
                    return preferredActionName;
                }
            }

            if (_skillLoader != null
                && actor?.Build != null
                && _skillLoader.TryResolveRenderableMeleeAfterImageActionName(
                    skill,
                    actor.Build.GetWeapon(),
                    EnumerateRemoteActionResolutionCandidates(actor, skill, actionName, rawActionCode),
                    actor.Build.Level,
                    masteryPercent,
                    chargeElement,
                    candidate => actor.Assembler?.GetAnimation(candidate)?.Length > 0,
                    out string renderableAfterImageActionName))
            {
                string preferredActionName = ResolvePreferredRemoteMeleeActionName(actor, actionName, renderableAfterImageActionName);
                if (!string.IsNullOrWhiteSpace(preferredActionName))
                {
                    return preferredActionName;
                }
            }

            return actionName;
        }

        private static string ResolvePreferredRemoteMeleeActionName(
            RemoteUserActor actor,
            string requestedActionName,
            string matchedAfterImageActionName)
        {
            string normalizedRequestedActionName = string.IsNullOrWhiteSpace(requestedActionName)
                ? null
                : requestedActionName.Trim();
            string normalizedMatchedActionName = string.IsNullOrWhiteSpace(matchedAfterImageActionName)
                ? null
                : matchedAfterImageActionName.Trim();

            if (!string.IsNullOrWhiteSpace(normalizedMatchedActionName)
                && actor?.Assembler?.GetAnimation(normalizedMatchedActionName)?.Length > 0)
            {
                return normalizedMatchedActionName;
            }

            if (!string.IsNullOrWhiteSpace(normalizedRequestedActionName)
                && actor?.Assembler?.GetAnimation(normalizedRequestedActionName)?.Length > 0)
            {
                return normalizedRequestedActionName;
            }

            return normalizedMatchedActionName ?? normalizedRequestedActionName;
        }

        private static IEnumerable<string> EnumerateRemoteAfterImageActionNames(
            RemoteUserActor actor,
            SkillData skill,
            string actionName,
            int? rawActionCode)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName.Trim()))
            {
                yield return actionName.Trim();
            }

            foreach (string rangeLookupActionName in ClientMeleeAfterimageRangeResolver.EnumerateRangeLookupActionNames(
                         skill?.SkillId ?? 0,
                         rawActionCode))
            {
                if (yielded.Add(rangeLookupActionName))
                {
                    yield return rangeLookupActionName;
                }
            }

            if (skill?.ActionNames != null)
            {
                foreach (string skillActionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(skillActionName) && yielded.Add(skillActionName.Trim()))
                    {
                        yield return skillActionName.Trim();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName) && yielded.Add(skill.ActionName.Trim()))
            {
                yield return skill.ActionName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(actor?.ActionName) && yielded.Add(actor.ActionName.Trim()))
            {
                yield return actor.ActionName.Trim();
            }
        }

        private static IEnumerable<string> EnumerateRemoteActionResolutionCandidates(
            RemoteUserActor actor,
            SkillData skill,
            string actionName,
            int? rawActionCode)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName.Trim()))
            {
                yield return actionName.Trim();
            }

            if (rawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out string resolvedCodeActionName)
                && !string.IsNullOrWhiteSpace(resolvedCodeActionName)
                && yielded.Add(resolvedCodeActionName))
            {
                yield return resolvedCodeActionName;
            }

            foreach (string rangeLookupActionName in ClientMeleeAfterimageRangeResolver.EnumerateRangeLookupActionNames(
                         skill?.SkillId ?? 0,
                         rawActionCode))
            {
                if (yielded.Add(rangeLookupActionName))
                {
                    yield return rangeLookupActionName;
                }
            }

            if (skill?.ActionNames != null)
            {
                foreach (string skillActionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(skillActionName) && yielded.Add(skillActionName.Trim()))
                    {
                        yield return skillActionName.Trim();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName) && yielded.Add(skill.ActionName.Trim()))
            {
                yield return skill.ActionName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(actor?.ActionName) && yielded.Add(actor.ActionName.Trim()))
            {
                yield return actor.ActionName.Trim();
            }
        }

        internal static int ResolveRemoteAfterImageChargeElement(
            RemoteUserActor actor,
            int chargeSkillId,
            int skillId,
            SkillData skill,
            bool admitSkillElementAttribute)
        {
            if (AfterImageChargeSkillResolver.TryGetChargeElement(chargeSkillId, out int explicitChargeElement))
            {
                return explicitChargeElement;
            }

            if (AfterImageChargeSkillResolver.TryGetChargeElement(
                    actor?.TemporaryStats.KnownState.ChargeSkillId ?? 0,
                    out int knownStateChargeElement))
            {
                return knownStateChargeElement;
            }

            RemoteUserTemporaryStatSnapshot temporaryStats = actor?.TemporaryStats ?? default;
            int preferredSkillId = ResolvePreferredRemoteAfterImageChargeSkillId(
                actor?.Build?.Job ?? 0,
                skillId,
                admitSkillElementAttribute ? skill?.ElementAttributeToken : null);
            int? resolvedChargeSkillId = ResolveChargeSkillIdFromTemporaryStats(
                temporaryStats,
                preferredSkillId);
            if (resolvedChargeSkillId.HasValue
                && AfterImageChargeSkillResolver.TryGetChargeElement(
                    resolvedChargeSkillId.Value,
                    out int payloadChargeElement))
            {
                return payloadChargeElement;
            }

            if (TryResolveChargeElementFromTemporaryStats(
                temporaryStats,
                preferredSkillId,
                out int recoveredPayloadChargeElement))
            {
                return recoveredPayloadChargeElement;
            }

            return AfterImageChargeSkillResolver.ResolveEffectiveChargeElementForSkill(
                activeChargeElement: 0,
                skillId,
                skill?.ElementAttributeToken,
                admitSkillElementAttribute);
        }

        internal static int? ResolveChargeSkillIdFromTemporaryStats(
            RemoteUserTemporaryStatSnapshot snapshot,
            int preferredSkillId)
        {
            int effectivePreferredSkillId = AfterImageChargeSkillResolver.ResolvePreferredChargeSkillIdFromWeaponChargeValue(
                preferredSkillId,
                snapshot.KnownState.WeaponChargeValue);
            if (!snapshot.HasWeaponCharge)
            {
                return null;
            }

            if (snapshot.KnownState.ChargeSkillId.HasValue)
            {
                return snapshot.KnownState.ChargeSkillId;
            }

            if (snapshot.RawPayload == null
                || snapshot.RawPayload.Length < (sizeof(int) * 4) + sizeof(int))
            {
                return null;
            }
            bool hasValidMetadataOffset = snapshot.WeaponChargePayloadOffset >= 0
                && snapshot.WeaponChargePayloadOffset <= snapshot.RawPayload.Length - sizeof(int);

            if (!hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromWeaponChargeValue(
                    effectivePreferredSkillId,
                    snapshot.KnownState.WeaponChargeValue,
                    out int knownWeaponChargeSkillId))
            {
                return knownWeaponChargeSkillId;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatMetadata(
                    snapshot.RawPayload,
                    snapshot.WeaponChargePayloadOffset,
                    out int scopedMetadataChargeSkillId))
            {
                return scopedMetadataChargeSkillId;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    snapshot.WeaponChargePayloadOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataScopedScanBytes,
                    out int scopedWindowChargeSkillId))
            {
                return scopedWindowChargeSkillId;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveNearestChargeSkillIdFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    sizeof(int) * 4,
                    snapshot.WeaponChargePayloadOffset,
                    effectivePreferredSkillId,
                    out int nearestMetadataChargeSkillId))
            {
                return nearestMetadataChargeSkillId;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveNearestChargeElementValueFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    sizeof(int) * 4,
                    snapshot.WeaponChargePayloadOffset,
                    effectivePreferredSkillId,
                    out int nearestMetadataChargeElement)
                && AfterImageChargeSkillResolver.TryResolvePreferredChargeSkillIdForElement(
                    effectivePreferredSkillId,
                    nearestMetadataChargeElement,
                    out int nearestMetadataElementChargeSkillId))
            {
                return nearestMetadataElementChargeSkillId;
            }

            int payloadMaskBaseOffset = sizeof(int) * 4;
            int metadataMissingMaskBaseNearestScanBytes = hasValidMetadataOffset
                ? 0
                : AfterImageChargeSkillResolver.ChargeMetadataMissingMaskBaseNearestScanBytes;
            if (AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataScopedScanBytes,
                    out int scopedMaskBaseChargeSkillId))
            {
                return scopedMaskBaseChargeSkillId;
            }

            if (AfterImageChargeSkillResolver.TryResolveChargeElementValueFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataScopedScanBytes,
                    out int scopedMaskBaseChargeElement)
                && AfterImageChargeSkillResolver.TryResolvePreferredChargeSkillIdForElement(
                    effectivePreferredSkillId,
                    scopedMaskBaseChargeElement,
                    out int scopedMaskBaseElementChargeSkillId))
            {
                return scopedMaskBaseElementChargeSkillId;
            }

            if (!hasValidMetadataOffset
                && AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId))
            {
                return effectivePreferredSkillId;
            }

            if (!hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveNearestChargeElementValueFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    metadataMissingMaskBaseNearestScanBytes,
                    out int metadataMissingNearestMaskBaseChargeElement)
                && AfterImageChargeSkillResolver.TryResolvePreferredChargeSkillIdForElement(
                    effectivePreferredSkillId,
                    metadataMissingNearestMaskBaseChargeElement,
                    out int metadataMissingNearestMaskBaseElementChargeSkillId))
            {
                return metadataMissingNearestMaskBaseElementChargeSkillId;
            }

            if (AfterImageChargeSkillResolver.TryResolveNearestChargeSkillIdFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    metadataMissingMaskBaseNearestScanBytes,
                    out int nearestMaskBaseChargeSkillId))
            {
                return nearestMaskBaseChargeSkillId;
            }

            if (AfterImageChargeSkillResolver.TryResolveNearestChargeElementValueFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    metadataMissingMaskBaseNearestScanBytes,
                    out int nearestMaskBaseChargeElement)
                && AfterImageChargeSkillResolver.TryResolvePreferredChargeSkillIdForElement(
                    effectivePreferredSkillId,
                    nearestMaskBaseChargeElement,
                    out int nearestMaskBaseElementChargeSkillId))
            {
                return nearestMaskBaseElementChargeSkillId;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    out int payloadChargeSkillId))
            {
                return payloadChargeSkillId;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementByAdjacentSkillElementPairConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    out int adjacentPairConsensusChargeElement)
                && AfterImageChargeSkillResolver.TryResolvePreferredChargeSkillIdForElement(
                    effectivePreferredSkillId,
                    adjacentPairConsensusChargeElement,
                    out int adjacentPairConsensusChargeSkillId))
            {
                return adjacentPairConsensusChargeSkillId;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementBySeparatedSkillElementPairConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingSeparatedPairMaxDistanceBytes,
                    out int separatedPairConsensusChargeElement)
                && AfterImageChargeSkillResolver.TryResolvePreferredChargeSkillIdForElement(
                    effectivePreferredSkillId,
                    separatedPairConsensusChargeElement,
                    out int separatedPairConsensusChargeSkillId))
            {
                return separatedPairConsensusChargeSkillId;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementCombinedConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    out int combinedConsensusChargeElement)
                && AfterImageChargeSkillResolver.TryResolvePreferredChargeSkillIdForElement(
                    effectivePreferredSkillId,
                    combinedConsensusChargeElement,
                    out int combinedConsensusChargeSkillId))
            {
                return combinedConsensusChargeSkillId;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementByKnownSkillConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    out int consensusChargeElement)
                && AfterImageChargeSkillResolver.TryResolvePreferredChargeSkillIdForElement(
                    effectivePreferredSkillId,
                    consensusChargeElement,
                    out int consensusChargeSkillId))
            {
                return consensusChargeSkillId;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementValueConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    out int elementValueConsensusChargeElement)
                && AfterImageChargeSkillResolver.TryResolvePreferredChargeSkillIdForElement(
                    effectivePreferredSkillId,
                    elementValueConsensusChargeElement,
                    out int elementValueConsensusChargeSkillId))
            {
                return elementValueConsensusChargeSkillId;
            }

            return AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                ? effectivePreferredSkillId
                : null;
        }

        internal static bool TryResolveChargeElementFromTemporaryStats(
            RemoteUserTemporaryStatSnapshot snapshot,
            int preferredSkillId,
            out int chargeElement)
        {
            int effectivePreferredSkillId = AfterImageChargeSkillResolver.ResolvePreferredChargeSkillIdFromWeaponChargeValue(
                preferredSkillId,
                snapshot.KnownState.WeaponChargeValue);
            if (!snapshot.HasWeaponCharge)
            {
                chargeElement = 0;
                return false;
            }

            if (AfterImageChargeSkillResolver.TryGetChargeElement(
                    snapshot.KnownState.ChargeSkillId ?? 0,
                    out chargeElement))
            {
                return true;
            }

            chargeElement = 0;
            if (snapshot.RawPayload == null
                || snapshot.RawPayload.Length < (sizeof(int) * 4) + sizeof(int))
            {
                return false;
            }
            bool hasValidMetadataOffset = snapshot.WeaponChargePayloadOffset >= 0
                && snapshot.WeaponChargePayloadOffset <= snapshot.RawPayload.Length - sizeof(int);

            if (!hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveChargeSkillIdFromWeaponChargeValue(
                    effectivePreferredSkillId,
                    snapshot.KnownState.WeaponChargeValue,
                    out int knownWeaponChargeSkillId)
                && AfterImageChargeSkillResolver.TryGetChargeElement(knownWeaponChargeSkillId, out chargeElement))
            {
                return true;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveChargeElementFromTemporaryStatMetadata(
                    snapshot.RawPayload,
                    snapshot.WeaponChargePayloadOffset,
                    out chargeElement))
            {
                return true;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveChargeElementFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    snapshot.WeaponChargePayloadOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataScopedScanBytes,
                    out chargeElement))
            {
                return true;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveNearestChargeSkillIdFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    sizeof(int) * 4,
                    snapshot.WeaponChargePayloadOffset,
                    effectivePreferredSkillId,
                    out int nearestMetadataChargeSkillId)
                && AfterImageChargeSkillResolver.TryGetChargeElement(nearestMetadataChargeSkillId, out chargeElement))
            {
                return true;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveNearestChargeElementValueFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    sizeof(int) * 4,
                    snapshot.WeaponChargePayloadOffset,
                    effectivePreferredSkillId,
                    out chargeElement))
            {
                return true;
            }

            int payloadMaskBaseOffset = sizeof(int) * 4;
            int metadataMissingMaskBaseNearestScanBytes = hasValidMetadataOffset
                ? 0
                : AfterImageChargeSkillResolver.ChargeMetadataMissingMaskBaseNearestScanBytes;
            if (AfterImageChargeSkillResolver.TryResolveChargeElementFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataScopedScanBytes,
                    out chargeElement))
            {
                return true;
            }

            if (AfterImageChargeSkillResolver.TryResolveChargeElementValueFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataScopedScanBytes,
                    out chargeElement))
            {
                return true;
            }

            if (!hasValidMetadataOffset
                && AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryGetChargeElement(effectivePreferredSkillId, out chargeElement))
            {
                return true;
            }

            if (!hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveNearestChargeElementValueFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    metadataMissingMaskBaseNearestScanBytes,
                    out chargeElement))
            {
                return true;
            }

            if (AfterImageChargeSkillResolver.TryResolveNearestChargeSkillIdFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    metadataMissingMaskBaseNearestScanBytes,
                    out int nearestMaskBaseChargeSkillId)
                && AfterImageChargeSkillResolver.TryGetChargeElement(nearestMaskBaseChargeSkillId, out chargeElement))
            {
                return true;
            }

            if (AfterImageChargeSkillResolver.TryResolveNearestChargeElementValueFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    metadataMissingMaskBaseNearestScanBytes,
                    out chargeElement))
            {
                return true;
            }

            if (hasValidMetadataOffset
                && AfterImageChargeSkillResolver.TryResolveChargeElementFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    out chargeElement))
            {
                return true;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementByAdjacentSkillElementPairConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    out chargeElement))
            {
                return true;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementBySeparatedSkillElementPairConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingSeparatedPairMaxDistanceBytes,
                    out chargeElement))
            {
                return true;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementCombinedConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    out chargeElement))
            {
                return true;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementByKnownSkillConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    out chargeElement))
            {
                return true;
            }

            if (!hasValidMetadataOffset
                && !AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                && AfterImageChargeSkillResolver.TryResolveChargeElementValueConsensusFromTemporaryStatPayload(
                    snapshot.RawPayload,
                    payloadMaskBaseOffset,
                    effectivePreferredSkillId,
                    AfterImageChargeSkillResolver.ChargeMetadataMissingConsensusMinimumMatches,
                    out chargeElement))
            {
                return true;
            }

            return AfterImageChargeSkillResolver.IsKnownChargeSkillId(effectivePreferredSkillId)
                   && AfterImageChargeSkillResolver.TryGetChargeElement(effectivePreferredSkillId, out chargeElement);
        }

        internal static int ResolvePreferredRemoteAfterImageChargeSkillId(
            int jobId,
            int skillId,
            string skillElementAttributeToken)
        {
            if (AfterImageChargeSkillResolver.IsKnownChargeSkillId(skillId))
            {
                return skillId;
            }

            if (AfterImageChargeSkillResolver.TryResolveChargeElementFromElementAttributeToken(
                    skillElementAttributeToken,
                    out int preferredChargeElement))
            {
                foreach (int candidateSkillId in EnumerateRemoteWeaponChargeSkillIds(jobId, preferredSkillId: null))
                {
                    if (AfterImageChargeSkillResolver.TryGetChargeElement(candidateSkillId, out int candidateChargeElement)
                        && candidateChargeElement == preferredChargeElement)
                    {
                        return candidateSkillId;
                    }
                }

                if (AfterImageChargeSkillResolver.TryGetRepresentativeChargeSkillIdForElement(
                        preferredChargeElement,
                        out int representativeSkillId))
                {
                    return representativeSkillId;
                }
            }

            return ResolvePreferredRemoteWeaponChargeSkillId(jobId);
        }

        private static int ResolvePreferredRemoteWeaponChargeSkillId(int jobId)
        {
            IReadOnlyList<int> candidateSkillIds = EnumerateRemoteWeaponChargeSkillIds(jobId, preferredSkillId: null);
            return candidateSkillIds != null && candidateSkillIds.Count > 0
                ? candidateSkillIds[0]
                : 0;
        }

        private static int GetActionDuration(CharacterAssembler assembler, string actionName)
        {
            AssembledFrame[] animation = assembler?.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            int duration = 0;
            foreach (AssembledFrame frame in animation)
            {
                duration += Math.Max(0, frame?.Duration ?? 0);
            }

            return duration;
        }

        private static void DrawMeleeAfterImage(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime)
        {
            if (actor?.Assembler == null || actor.MeleeAfterImage?.AfterImageAction?.FrameSets == null)
            {
                return;
            }

            RemoteMeleeAfterImageState state = actor.MeleeAfterImage;
            if (MeleeAfterimagePlaybackResolver.ShouldDeferUntilActivation(
                    currentTime,
                    state.ActivationStartTime,
                    actor.ActionName,
                    state.ActionName,
                    state.AnimationStartTime,
                    state.ActionDuration,
                    out bool shouldClear))
            {
                if (shouldClear)
                {
                    actor.ClearMeleeAfterImage();
                }

                return;
            }

            bool activeAction = state.FadeStartTime < 0
                && !MeleeAfterimagePlaybackResolver.ShouldBeginFadeForActionBoundary(
                    currentTime,
                    state.AnimationStartTime,
                    state.ActionDuration,
                    actor.ActionName,
                    state.ActionName);
            int frameIndex = state.LastFrameIndex;
            IReadOnlyList<AfterimageRenderableLayer> layers = state.LastResolvedLayers;

            if (activeAction)
            {
                int animationTime = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.AnimationStartTime);
                int lastFrameIndex = state.LastFrameIndex;
                int lastFrameElapsedMs = state.LastFrameElapsedMs;
                IReadOnlyList<AfterimageRenderableLayer> lastResolvedLayers = state.LastResolvedLayers;
                MeleeAfterimagePlaybackResolver.RefreshSnapshotCache(
                    actor.Assembler,
                    state.ActionName,
                    state.AfterImageAction,
                    animationTime,
                    ref lastFrameIndex,
                    ref lastFrameElapsedMs,
                    ref lastResolvedLayers);
                state.LastFrameIndex = lastFrameIndex;
                state.LastFrameElapsedMs = lastFrameElapsedMs;
                state.LastResolvedLayers = lastResolvedLayers;
                frameIndex = state.LastFrameIndex;
                layers = state.LastResolvedLayers;
            }
            else if (state.FadeStartTime < 0)
            {
                actor.BeginMeleeAfterImageFade(currentTime);
                frameIndex = state.LastFrameIndex;
                layers = state.LastResolvedLayers;
            }

            if (!activeAction && state.FadeStartTime >= 0)
            {
                int fadeElapsed = ResolveRemoteTemporaryStatTickElapsedMs(currentTime, state.FadeStartTime);
                layers = MeleeAfterimagePlaybackResolver.ResolveFadingRenderableLayers(
                    state.AfterImageAction,
                    frameIndex,
                    state.LastFrameElapsedMs,
                    fadeElapsed);
                if (layers.Count == 0)
                {
                    actor.ClearMeleeAfterImage();
                    return;
                }
            }

            if (layers == null || layers.Count == 0)
            {
                return;
            }

            foreach (AfterimageRenderableLayer layer in layers)
            {
                SkillFrame frame = layer.Frame;
                if (!MeleeAfterimagePlaybackResolver.TryResolveSpriteBatchDrawParameters(
                        frame,
                        screenX,
                        screenY,
                        state.FacingRight,
                        layer.Alpha,
                        layer.Zoom,
                        Color.White,
                        out MeleeAfterimagePlaybackResolver.SpriteBatchDrawParameters drawParameters))
                {
                    continue;
                }

                spriteBatch.Draw(
                    frame.Texture.Texture,
                    drawParameters.Position,
                    null,
                    drawParameters.Tint,
                    0f,
                    drawParameters.Origin,
                    drawParameters.Scale,
                    drawParameters.Effects,
                    0f);
            }
        }

        private void ApplyMovementSnapshot(RemoteUserActor actor, int currentTime)
        {
            string previousActionName = actor.ActionName;
            PassivePositionSnapshot sampled = actor.MovementSnapshot.SampleAtTime(currentTime);
            actor.Position = new Vector2(sampled.X, sampled.Y);
            actor.FacingRight = sampled.FacingRight;
            actor.CurrentFootholdId = sampled.FootholdId;

            if (actor.MovementDrivenActionSelection)
            {
                SetActorAction(
                    actor,
                    ResolveActionName(actor, sampled.Action),
                    actor.Build.ActivePortableChair != null,
                    currentTime,
                    rawActionCode: actor.LastMoveActionRaw);
                if (!string.Equals(previousActionName, actor.ActionName, StringComparison.OrdinalIgnoreCase))
                {
                    actor.BeginMeleeAfterImageFade(currentTime);
                    RegisterMeleeAfterImage(actor, 0, actor.ActionName, currentTime, 10, 0);
                }
            }
        }

        private void ApplyFollowDriverState(RemoteUserActor actor, PlayerCharacter localPlayer)
        {
            if (actor == null || actor.FollowDriverId <= 0)
            {
                return;
            }

            if (!TryResolveFollowDriverWorldState(actor, localPlayer, out Vector2 position, out bool facingRight))
            {
                return;
            }

            actor.Position = position;
            actor.FacingRight = facingRight;
        }

        private bool TryResolveFollowDriverWorldState(
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            out Vector2 position,
            out bool facingRight)
        {
            position = default;
            facingRight = actor?.FacingRight ?? true;
            if (actor == null || actor.FollowDriverId <= 0)
            {
                return false;
            }

            if (TryResolveLocalFollowDriverState(actor.FollowDriverId, localPlayer, out position, out facingRight))
            {
                return true;
            }

            if (!_actorsById.TryGetValue(actor.FollowDriverId, out RemoteUserActor driverActor))
            {
                return false;
            }

            return TryResolveFollowDriverOffsetPosition(
                driverActor.Position,
                driverActor.FacingRight,
                driverActor.ActionName,
                out position,
                out facingRight);
        }

        private static bool TryResolveLocalFollowDriverState(
            int driverCharacterId,
            PlayerCharacter localPlayer,
            out Vector2 position,
            out bool facingRight)
        {
            position = default;
            facingRight = localPlayer?.FacingRight ?? true;
            if (localPlayer?.Build?.Id != driverCharacterId)
            {
                return false;
            }

            return TryResolveFollowDriverOffsetPosition(
                localPlayer.Position,
                localPlayer.FacingRight,
                localPlayer.CurrentActionName,
                out position,
                out facingRight);
        }

        internal static bool TryResolveFollowDriverOffsetPosition(
            Vector2 driverPosition,
            bool driverFacingRight,
            string driverActionName,
            out Vector2 followerPosition,
            out bool followerFacingRight)
        {
            followerFacingRight = driverFacingRight;
            if (IsFollowDriverLadderOrRopeAction(driverActionName))
            {
                followerPosition = new Vector2(
                    driverPosition.X,
                    driverPosition.Y + FollowDriverLadderRopeVerticalOffset);
                return true;
            }

            followerPosition = new Vector2(
                driverPosition.X + (driverFacingRight ? FollowDriverGroundHorizontalOffset : -FollowDriverGroundHorizontalOffset),
                driverPosition.Y);
            return true;
        }

        private static bool IsFollowDriverLadderOrRopeAction(string actionName)
        {
            return string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase);
        }

        private static MoveAction MoveActionFromRaw(byte moveAction)
        {
            int normalized = (moveAction >> 1) & 0x0F;
            return Enum.IsDefined(typeof(MoveAction), normalized)
                ? (MoveAction)normalized
                : MoveAction.Stand;
        }

        private static bool DecodeFacingRight(byte moveAction)
        {
            return (moveAction & 1) == 0;
        }

        private static string ResolveActionName(RemoteUserActor actor, MoveAction moveAction)
        {
            bool hasPortableChair = actor?.Build?.ActivePortableChair != null;
            return moveAction switch
            {
                MoveAction.Walk => "walk1",
                MoveAction.Jump or MoveAction.Fall => "jump",
                MoveAction.Ladder => "ladder",
                MoveAction.Rope => "rope",
                MoveAction.Swim => "swim",
                MoveAction.Fly => "fly",
                MoveAction.Attack => string.IsNullOrWhiteSpace(actor?.BaseActionName) ? "alert" : actor.BaseActionName,
                MoveAction.Hit => "alert",
                MoveAction.Die => "dead",
                _ => hasPortableChair ? "sit" : CharacterPart.GetActionString(CharacterAction.Stand1)
            };
        }

        private void UpdateNameLookup(string previousName, string currentName, int characterId)
        {
            if (!string.IsNullOrWhiteSpace(previousName)
                && !string.Equals(previousName, currentName, StringComparison.OrdinalIgnoreCase))
            {
                _actorIdsByName.Remove(previousName);
            }

            _actorIdsByName[currentName] = characterId;
        }

        private void ApplyPortableChairMount(RemoteUserActor actor, PortableChair chair)
        {
            if (_loader == null
                || actor?.Build == null
                || chair?.TamingMobItemId is not int tamingMobItemId
                || tamingMobItemId <= 0)
            {
                return;
            }

            CharacterPart mountPart = _loader.LoadEquipment(tamingMobItemId);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return;
            }

            actor.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart previousMount);
            actor.PortableChairPreviousMount = previousMount;
            actor.PortableChairAppliedMount = true;
            actor.Build.Equip(mountPart);
        }

        private static void ClearPortableChairMountState(RemoteUserActor actor)
        {
            if (actor?.Build == null || !actor.PortableChairAppliedMount)
            {
                return;
            }

            if (actor.PortableChairPreviousMount != null)
            {
                actor.Build.Equip(actor.PortableChairPreviousMount);
            }
            else
            {
                actor.Build.Unequip(EquipSlot.TamingMob);
            }

            actor.PortableChairPreviousMount = null;
            actor.PortableChairAppliedMount = false;
        }
    }

    public sealed class RemoteUserActor
    {
        public RemoteUserActor(
            int characterId,
            string name,
            CharacterBuild build,
            Vector2 position,
            bool facingRight,
            string actionName,
            string sourceTag,
            bool isVisibleInWorld)
        {
            CharacterId = characterId;
            Name = name;
            Build = build;
            Position = position;
            FacingRight = facingRight;
            BaseActionName = actionName;
            ActionName = actionName;
            SourceTag = sourceTag;
            IsVisibleInWorld = isVisibleInWorld;
            RefreshAssembler();
        }

        public int CharacterId { get; }
        public string Name { get; set; }
        public CharacterBuild Build { get; set; }
        public CharacterAssembler Assembler { get; private set; }
        public Vector2 Position { get; set; }
        public bool FacingRight { get; set; }
        public string BaseActionName { get; set; }
        public int BaseActionStartTime { get; set; } = int.MinValue;
        public int? BaseActionRawCode { get; set; }
        public string ActionName { get; set; }
        public string SourceTag { get; set; }
        public bool IsVisibleInWorld { get; set; }
        public MinimapUI.HelperMarkerType? HelperMarkerType { get; set; }
        public bool HasPacketAuthoredHelperState { get; set; }
        public bool ShowDirectionOverlay { get; set; } = true;
        public int? BattlefieldTeamId { get; set; }
        public RemotePreparedSkillState PreparedSkill { get; set; }
        public CharacterPart PortableChairPreviousMount { get; set; }
        public bool PortableChairAppliedMount { get; set; }
        public int? PreferredPortableChairPairCharacterId { get; set; }
        public Dictionary<RemoteRelationshipOverlayType, RemoteRelationshipOverlayState> RelationshipOverlays { get; } = new();
        public RemoteUserTemporaryStatSnapshot TemporaryStats { get; set; }
        public ushort TemporaryStatDelay { get; set; }
        public bool PendingTemporaryStatTimelineReseed { get; set; }
        public PlayerMovementSyncSnapshot MovementSnapshot { get; set; }
        public byte LastMoveActionRaw { get; set; }
        public int CurrentFootholdId { get; set; }
        public bool MovementDrivenActionSelection { get; set; }
        public bool HasMorphTemplate { get; set; }
        public bool HiddenLikeClient { get; set; }
        public int RidingVehicleId { get; set; }
        public CharacterPart TemporaryStatAvatarOverridePart { get; set; }
        public CharacterPart TemporaryStatTamingMobOverridePart { get; set; }
        public int? TemporaryStatShadowPartnerSkillId { get; set; }
        public SkillData TemporaryStatShadowPartnerSkill { get; set; }
        public RemoteUserActorPool.RemoteShadowPartnerPresentationState ShadowPartnerPresentation { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatAuraEffect { get; set; }
        public List<RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState> TemporaryStatAuraEffectTails { get; } = new();
        public int TemporaryStatAffectedLayerLastSyncTime { get; set; } = int.MinValue;
        public int TemporaryStatAffectedLayerShiftCadenceFrameDurationMs { get; set; } = 16;
        public int TemporaryStatAffectedLayerShiftCadenceUpdateCount { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatAuraEffectTail
        {
            get => TemporaryStatAuraEffectTails.Count > 0 ? TemporaryStatAuraEffectTails[^1] : null;
            set
            {
                TemporaryStatAuraEffectTails.Clear();
                if (value != null)
                {
                    TemporaryStatAuraEffectTails.Add(value);
                }
            }
        }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatMoreWildEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatSoulArrowEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatWeaponChargeEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatBarrierEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatBlessingArmorEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatRepeatEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatMagicShieldEffect { get; set; }
        public RemoteUserActorPool.RemoteTemporaryStatAvatarEffectState TemporaryStatFinalCutEffect { get; set; }
        public RemoteUserActorPool.RemotePacketOwnedEmotionState PacketOwnedEmotion { get; set; }
        public RemoteUserActorPool.RemoteActiveEffectMotionBlurState ActiveEffectMotionBlur { get; set; }
        public RemoteUserActorPool.RemoteActiveEffectItemEffectState ActiveEffectItemEffect { get; set; }
        public IReadOnlyDictionary<AvatarRenderLayer, int> ActiveEffectMotionBlurLayerHandleIds { get; set; }
        public int ActiveEffectMotionBlurOverlayLayerHandleId { get; set; }
        public List<RemoteUserActorPool.RemoteTransientItemEffectState> TransientItemEffects { get; } = new();
        public List<RemoteUserActorPool.RemoteTransientSkillUseAvatarEffectState> TransientSkillUseAvatarEffects { get; } = new();
        public int MovingShootPreparedSkillId { get; set; }
        public int? PartyCurrentHp { get; set; }
        public int? PartyMaxHp { get; set; }
        public int? PartyHpPercent { get; set; }
            public int? PartyHpGaugePos { get; set; }
            public int? PacketOwnedQuestDeliveryEffectItemId { get; set; }
            public int PacketOwnedQuestDeliveryEffectAppliedTime { get; set; } = int.MinValue;
            public string PacketOwnedQuestDeliveryOwnerActionName { get; set; }
            public bool PacketOwnedQuestDeliveryOwnerFacingRight { get; set; } = true;
            public RemoteUserEffectPacket LastEffect { get; set; }
            public int? LastEffectByItemId { get; set; }
            public RemoteUserActorPool.RemoteHitState LastHit { get; set; }
            public int? LastThrowGrenadeSkillId { get; set; }
        public int? LastThrowGrenadeSkillLevel { get; set; }
        public Point? LastThrowGrenadeTarget { get; set; }
        public int LastThrowGrenadeKeyDownTime { get; set; }
        public int LastThrowGrenadePacketTime { get; set; } = int.MinValue;
        public RemoteUserActorPool.RemoteDragonCompanionPresentationState RemoteDragonCompanion { get; set; }
        public int? RemoteDragonAttackSkillId { get; set; }
        public string RemoteDragonAttackActionName { get; set; }
        public int RemoteDragonAttackStartTime { get; set; } = int.MinValue;
        public int? CarryItemEffectId { get; set; }
        public int CarryItemEffectAppliedTime { get; set; } = int.MinValue;
        public int CarryItemEffectLayerHandleId { get; set; }
        public int CarryItemEffectLayerHandleRefCount { get; set; }
        public bool CarryItemEffectLayerSuppressed { get; set; } = true;
        public int CompletedSetItemId { get; set; }
        public int CompletedSetItemEffectAppliedTime { get; set; } = int.MinValue;
        public int CompletedSetItemEffectLayerHandleId { get; set; }
        public int CompletedSetItemEffectLayerHandleRefCount { get; set; }
        public Dictionary<EquipSlot, CharacterPart> BattlefieldOriginalEquipment { get; set; }
        public float? BattlefieldOriginalSpeed { get; set; }
        public int? BattlefieldAppliedTeamId { get; set; }
        public RemoteMeleeAfterImageState MeleeAfterImage { get; private set; }
        public PacketOwnedUserSummonRegistry PacketOwnedSummons { get; } = new();
        public int FollowDriverId { get; set; }
        public int FollowPassengerId { get; set; }

        public void RefreshAssembler()
        {
            Assembler = new CharacterAssembler(Build);
            if (Assembler != null)
            {
                Assembler.OverrideAvatarPart = TemporaryStatAvatarOverridePart;
                Assembler.OverrideTamingMobPart = TemporaryStatTamingMobOverridePart;
                Assembler.FaceExpressionName = PacketOwnedEmotion?.EmotionName ?? "default";
            }
        }

        internal CharacterPart ResolveMountedStateTamingMobPart()
        {
            if (TemporaryStatTamingMobOverridePart?.Slot == EquipSlot.TamingMob)
            {
                return TemporaryStatTamingMobOverridePart;
            }

            CharacterPart equippedMountPart = null;
            if (Build?.Equipment != null)
            {
                Build.Equipment.TryGetValue(EquipSlot.TamingMob, out equippedMountPart);
            }
            return equippedMountPart;
        }

        public AssembledFrame GetFrameAtTimeForRendering(int currentTime)
        {
            if (Assembler == null)
            {
                return null;
            }

            SyncAssemblerActionLayerContext();
            return Assembler.GetFrameAtTime(ActionName, currentTime)
                ?? Assembler.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), currentTime);
        }

        public void ApplyMeleeAfterImage(
            int skillId,
            string actionName,
            MeleeAfterImageAction afterImageAction,
            int currentTime,
            bool facingRight,
            int actionDuration)
        {
            if (afterImageAction == null
                || string.IsNullOrWhiteSpace(actionName)
                || ((afterImageAction.FrameSets == null || afterImageAction.FrameSets.Count == 0) && !afterImageAction.HasRange))
            {
                MeleeAfterImage = null;
                return;
            }

            MeleeAfterImage = new RemoteMeleeAfterImageState
            {
                SkillId = skillId,
                ActionName = actionName,
                AfterImageAction = afterImageAction,
                AnimationStartTime = currentTime,
                ActivationStartTime = currentTime + ClientMeleeAfterimageRangeResolver.ResolveActivationDelayMs(
                    afterImageAction,
                    Assembler?.GetAnimation(actionName)),
                FacingRight = facingRight,
                ActionDuration = actionDuration
            };
        }

        public void BeginMeleeAfterImageFade(int currentTime)
        {
            if (MeleeAfterImage == null || MeleeAfterImage.FadeStartTime >= 0)
            {
                return;
            }

            int lastFrameIndex = MeleeAfterImage.LastFrameIndex;
            int lastFrameElapsedMs = MeleeAfterImage.LastFrameElapsedMs;
            IReadOnlyList<AfterimageRenderableLayer> lastResolvedLayers = MeleeAfterImage.LastResolvedLayers;
            MeleeAfterimagePlaybackResolver.CaptureFadeSnapshotOrClearCache(
                Assembler,
                MeleeAfterImage.ActionName,
                MeleeAfterImage.AfterImageAction,
                RemoteUserActorPool.ResolveRemoteTemporaryStatTickElapsedMs(currentTime, MeleeAfterImage.AnimationStartTime),
                ref lastFrameIndex,
                ref lastFrameElapsedMs,
                ref lastResolvedLayers);
            MeleeAfterImage.LastFrameIndex = lastFrameIndex;
            MeleeAfterImage.LastFrameElapsedMs = lastFrameElapsedMs;
            MeleeAfterImage.LastResolvedLayers = lastResolvedLayers;

            MeleeAfterImage.FadeStartTime = currentTime;
        }

        public void UpdateMeleeAfterImage(int currentTime)
        {
            if (MeleeAfterImage == null)
            {
                return;
            }

            if (MeleeAfterimagePlaybackResolver.ShouldDeferUntilActivation(
                    currentTime,
                    MeleeAfterImage.ActivationStartTime,
                    ActionName,
                    MeleeAfterImage.ActionName,
                    MeleeAfterImage.AnimationStartTime,
                    MeleeAfterImage.ActionDuration,
                    out bool shouldClear))
            {
                if (shouldClear)
                {
                    MeleeAfterImage = null;
                }

                return;
            }

            if (MeleeAfterImage.FadeStartTime >= 0)
            {
                IReadOnlyList<AfterimageRenderableLayer> fadingLayers = MeleeAfterimagePlaybackResolver.ResolveFadingRenderableLayers(
                    MeleeAfterImage.AfterImageAction,
                    MeleeAfterImage.LastFrameIndex,
                    MeleeAfterImage.LastFrameElapsedMs,
                    RemoteUserActorPool.ResolveRemoteTemporaryStatTickElapsedMs(currentTime, MeleeAfterImage.FadeStartTime));
                if (fadingLayers.Count == 0)
                {
                    MeleeAfterImage = null;
                }

                return;
            }

            if (MeleeAfterimagePlaybackResolver.ShouldBeginFadeForActionBoundary(
                    currentTime,
                    MeleeAfterImage.AnimationStartTime,
                    MeleeAfterImage.ActionDuration,
                    ActionName,
                    MeleeAfterImage.ActionName))
            {
                BeginMeleeAfterImageFade(currentTime);
            }
        }

        private void SyncAssemblerActionLayerContext()
        {
            if (Assembler == null)
            {
                return;
            }

            Assembler.PreparedActionSpeedDegree = Build?.GetEffectiveWeaponAttackSpeed() ?? 6;
            Assembler.PreparedWalkSpeed = (int)Math.Round(Build?.Speed ?? 100f);
            Assembler.HeldActionFrameDelay = false;
            Assembler.CurrentFacingRight = FacingRight;
        }

        public void ClearMeleeAfterImage()
        {
            MeleeAfterImage = null;
        }

        public string Describe()
        {
            string helperText = HasPacketAuthoredHelperState
                ? HelperMarkerType?.ToString() ?? "clear"
                : "none";
            string teamText = BattlefieldTeamId?.ToString() ?? "none";
            string preparedText = PreparedSkill != null ? PreparedSkill.SkillId.ToString() : "none";
            string chairPairText = PreferredPortableChairPairCharacterId?.ToString() ?? "none";
            string itemEffectText = RelationshipOverlays.Count > 0
                ? string.Join(
                    ",",
                    RelationshipOverlays
                        .OrderBy(static entry => entry.Key)
                        .Select(static entry =>
                            $"{entry.Key}:{entry.Value.ItemId}:{entry.Value.PairCharacterId?.ToString() ?? "none"}"))
                : "none";
            string carryItemEffectText = CarryItemEffectId?.ToString() ?? "none";
            string setItemText = CompletedSetItemId > 0 ? CompletedSetItemId.ToString() : "none";
            string activeEffectText = PacketOwnedEmotion != null
                ? $"{PacketOwnedEmotion.ItemId}:{PacketOwnedEmotion.EmotionName}"
                : ActiveEffectItemEffect != null
                    ? $"{ActiveEffectItemEffect.ItemId}:effect"
                    : ActiveEffectMotionBlur != null
                        ? $"{ActiveEffectMotionBlur.ActiveItemId}:spectrum"
                : "none";
            string ridingVehicleText = RidingVehicleId > 0 ? RidingVehicleId.ToString() : "none";
            string shadowPartnerText = TemporaryStatShadowPartnerSkillId?.ToString() ?? "none";
            string followDriverText = FollowDriverId > 0 ? FollowDriverId.ToString() : "none";
            string followPassengerText = FollowPassengerId > 0 ? FollowPassengerId.ToString() : "none";
            return $"{CharacterId}:{Name}@({Position.X:0},{Position.Y:0}) action={ActionName} source={SourceTag} helper={helperText} team={teamText} prep={preparedText} chairPair={chairPairText} itemEffect={itemEffectText} activeEffect={activeEffectText} carry={carryItemEffectText} setItem={setItemText} ridingVehicle={ridingVehicleText} shadowPartner={shadowPartnerText} followDriver={followDriverText} followPassenger={followPassengerText}";
        }
    }

        public sealed class RemoteMeleeAfterImageState
        {
            public int SkillId { get; init; }
            public string ActionName { get; init; }
            public MeleeAfterImageAction AfterImageAction { get; init; }
            public int AnimationStartTime { get; set; }
            public int ActivationStartTime { get; init; }
            public bool FacingRight { get; init; }
            public int ActionDuration { get; init; }
            public int FadeStartTime { get; set; } = -1;
            public int LastFrameIndex { get; set; } = -1;
            public int LastFrameElapsedMs { get; set; }
            public IReadOnlyList<AfterimageRenderableLayer> LastResolvedLayers { get; set; } = Array.Empty<AfterimageRenderableLayer>();
        }

    public sealed class RemotePreparedSkillState
    {
        public int SkillId { get; init; }
        public string SkillName { get; init; }
        public string SkinKey { get; init; } = "KeyDownBar";
        public int DurationMs { get; init; }
        public int PrepareDurationMs { get; init; }
        public int GaugeDurationMs { get; init; }
        public int StartTime { get; init; }
        public bool IsKeydownSkill { get; init; }
        public bool IsHolding { get; init; }
        public bool AutoEnterHold { get; init; }
        public int MaxHoldDurationMs { get; init; }
        public PreparedSkillHudTextVariant TextVariant { get; init; }
        public bool ShowText { get; init; } = true;
        public string DragonActionName { get; set; }
        public int DragonActionStartTime { get; set; } = int.MinValue;
        public int DragonOwnerActionStartTime { get; set; } = int.MinValue;
        public Vector2 DragonVisualAnchor { get; set; }
        public Vector2 DragonFollowVelocity { get; set; }
        public int DragonLastFollowUpdateTime { get; set; } = int.MinValue;
        public bool DragonFollowActive { get; set; }
        public int DragonActiveVerticalFollowState { get; set; }
        public int DragonActiveVerticalCheckCount { get; set; }
        public int DragonActiveFollowReleaseStableFrames { get; set; }
    }

    public sealed class RemoteRelationshipOverlayState
    {
        public RemoteRelationshipOverlayType RelationshipType { get; init; }
        public int ItemId { get; init; }
        public long? ItemSerial { get; init; }
        public long? PairItemSerial { get; init; }
        public int? PairCharacterId { get; init; }
        public ItemEffectAnimationSet Effect { get; init; }
        public int StartTime { get; init; }
        public string OwnerActionName { get; init; }
        public bool OwnerFacingRight { get; init; }
    }

    internal readonly struct RemoteDragonHudFrameMetrics
    {
        public RemoteDragonHudFrameMetrics(int originX, int height, int delayMs)
        {
            OriginX = Math.Max(0, originX);
            Height = Math.Max(1, height);
            DelayMs = Math.Max(1, delayMs);
        }

        public int OriginX { get; }
        public int Height { get; }
        public int DelayMs { get; }
    }

    internal readonly struct RemoteDragonHudAnimationTimeline
    {
        public RemoteDragonHudAnimationTimeline(bool loop, IReadOnlyList<RemoteDragonHudFrameMetrics> frames)
        {
            Loop = loop;
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
            TotalDurationMs = Math.Max(0, frames.Sum(static frame => Math.Max(1, frame.DelayMs)));
        }

        public bool Loop { get; }
        public IReadOnlyList<RemoteDragonHudFrameMetrics> Frames { get; }
        public int TotalDurationMs { get; }

        public int ResolveFrameHeight(int elapsedMs)
        {
            return ResolveFrameMetrics(elapsedMs).Height;
        }

        public int ResolveOriginX(int elapsedMs)
        {
            return ResolveFrameMetrics(elapsedMs).OriginX;
        }

        private RemoteDragonHudFrameMetrics ResolveFrameMetrics(int elapsedMs)
        {
            if (Frames == null || Frames.Count == 0)
            {
                return new RemoteDragonHudFrameMetrics(0, 1, 1);
            }

            if (Frames.Count == 1)
            {
                return Frames[0];
            }

            int remainingMs = Math.Max(0, elapsedMs);
            if (Loop && TotalDurationMs > 0)
            {
                remainingMs %= TotalDurationMs;
            }

            for (int i = 0; i < Frames.Count; i++)
            {
                RemoteDragonHudFrameMetrics frame = Frames[i];
                if (remainingMs < frame.DelayMs || i == Frames.Count - 1)
                {
                    return frame;
                }

                remainingMs -= frame.DelayMs;
            }

            return Frames[Frames.Count - 1];
        }
    }

    internal readonly struct RemoteDragonHudMetadata
    {
        public RemoteDragonHudMetadata(
            int standOriginX,
            int moveOriginX,
            IReadOnlyDictionary<string, RemoteDragonHudAnimationTimeline> actionTimelines)
        {
            StandOriginX = standOriginX;
            MoveOriginX = moveOriginX;
            ActionTimelines = actionTimelines ?? throw new ArgumentNullException(nameof(actionTimelines));
        }

        public int StandOriginX { get; }
        public int MoveOriginX { get; }
        public IReadOnlyDictionary<string, RemoteDragonHudAnimationTimeline> ActionTimelines { get; }

        public bool HasAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                && ActionTimelines.ContainsKey(actionName);
        }

        public int ResolveOriginX(string actionName, int elapsedMs = 0)
        {
            if (!string.IsNullOrWhiteSpace(actionName)
                && ActionTimelines.TryGetValue(actionName, out RemoteDragonHudAnimationTimeline actionTimeline))
            {
                int actionOriginX = actionTimeline.ResolveOriginX(elapsedMs);
                if (actionOriginX > 0)
                {
                    return actionOriginX;
                }
            }

            if (string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase) && MoveOriginX > 0)
            {
                return MoveOriginX;
            }

            return StandOriginX > 0 ? StandOriginX : 79;
        }

        public int ResolveFrameHeight(string actionName, int elapsedMs)
        {
            if (!string.IsNullOrWhiteSpace(actionName)
                && ActionTimelines.TryGetValue(actionName, out RemoteDragonHudAnimationTimeline actionTimeline))
            {
                return actionTimeline.ResolveFrameHeight(elapsedMs);
            }

            if (ActionTimelines.TryGetValue("stand", out RemoteDragonHudAnimationTimeline standTimeline))
            {
                return standTimeline.ResolveFrameHeight(elapsedMs);
            }

            return 1;
        }
    }
}
