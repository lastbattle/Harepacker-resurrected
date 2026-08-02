using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const string WeddingRemoteUserSourceTag = "wedding";
        private readonly Dictionary<int, int> _weddingRemoteItemEffectRevisionByCharacterId = new();
        private readonly Dictionary<int, int> _weddingRemoteAvatarModifiedRevisionByCharacterId = new();
        private readonly Dictionary<int, int> _weddingRemoteNameTagRevisionByCharacterId = new();
        private readonly Dictionary<int, int> _weddingRemoteProfileMetadataRevisionByCharacterId = new();
        private readonly Dictionary<int, int> _weddingRemoteGuildMarkRevisionByCharacterId = new();
        private readonly Dictionary<int, int> _weddingRemoteTemporaryStatRevisionByCharacterId = new();

        private void SyncWeddingRemoteActorsToSharedPool(WeddingField field)
        {
            if (field == null || !field.IsActive)
            {
                ClearWeddingRemoteActorsFromSharedPool();
                return;
            }

            field.UseExternalRemoteActorRenderer = true;
            IReadOnlyList<WeddingRemoteParticipantSnapshot> snapshots = field.GetExternalRendererParticipantSnapshots();
            HashSet<int> desiredCharacterIds = new();
            foreach (WeddingRemoteParticipantSnapshot snapshot in snapshots)
            {
                CharacterBuild build = snapshot.Build?.Clone();
                if (build == null)
                {
                    continue;
                }

                int characterId = ResolveWeddingRemoteUserId(snapshot);
                desiredCharacterIds.Add(characterId);
                _remoteUserPool.TryAddOrUpdate(
                    characterId,
                    build,
                    snapshot.Position,
                    out _,
                    snapshot.FacingRight,
                    snapshot.ActionName,
                    WeddingRemoteUserSourceTag,
                    isVisibleInWorld: IsWeddingRemoteSnapshotVisibleLikeClient(snapshot));
                bool shouldSyncNameTag = ShouldSyncWeddingNameTagMetadata(
                    _weddingRemoteNameTagRevisionByCharacterId,
                    characterId,
                    snapshot.NameTagRevision);
                bool shouldSyncProfileMetadata = shouldSyncNameTag
                    || ShouldSyncWeddingProfileMetadata(
                        _weddingRemoteProfileMetadataRevisionByCharacterId,
                        characterId,
                        snapshot.ProfileMetadataRevision);
                bool shouldSyncGuildMarkMetadata = shouldSyncNameTag
                    || ShouldSyncWeddingGuildMarkMetadata(
                        _weddingRemoteGuildMarkRevisionByCharacterId,
                        characterId,
                        snapshot.GuildMarkRevision);

                if (shouldSyncProfileMetadata)
                {
                    TryApplyWeddingRemoteProfileMetadata(_remoteUserPool, characterId, build, out _);
                    _weddingRemoteProfileMetadataRevisionByCharacterId[characterId] = snapshot.ProfileMetadataRevision;
                }

                if (shouldSyncGuildMarkMetadata)
                {
                    TryApplyWeddingRemoteGuildMarkMetadata(_remoteUserPool, characterId, build, out _);
                    _weddingRemoteGuildMarkRevisionByCharacterId[characterId] = snapshot.GuildMarkRevision;
                }

                if (shouldSyncNameTag)
                {
                    _weddingRemoteNameTagRevisionByCharacterId[characterId] = snapshot.NameTagRevision;
                }

                _remoteUserPool.TrySetPortableChair(
                    characterId,
                    snapshot.PortableChairItemId,
                    out _,
                    snapshot.PortableChairPairCharacterId);

                if (ShouldSyncWeddingTemporaryStatMetadata(
                    _weddingRemoteTemporaryStatRevisionByCharacterId,
                    characterId,
                    snapshot.TemporaryStatRevision))
                {
                    ushort temporaryStatDelay = ResolveWeddingTemporaryStatDelayForSharedPool(snapshot);
                    _remoteUserPool.TryApplyTemporaryStatSnapshot(
                        characterId,
                        snapshot.TemporaryStats,
                        delay: temporaryStatDelay,
                        out _);
                    _weddingRemoteTemporaryStatRevisionByCharacterId[characterId] = snapshot.TemporaryStatRevision;
                }
                SyncAnimationDisplayerRemoteUserState(characterId);

                if (snapshot.AvatarModifiedState is RemoteUserAvatarModifiedPacket avatarModifiedState
                    && (!_weddingRemoteAvatarModifiedRevisionByCharacterId.TryGetValue(characterId, out int syncedAvatarRevision)
                        || syncedAvatarRevision != snapshot.AvatarModifiedRevision))
                {
                    _remoteUserPool.TryApplyAvatarModified(
                        avatarModifiedState,
                        System.Environment.TickCount,
                        out _);
                    _weddingRemoteAvatarModifiedRevisionByCharacterId[characterId] = snapshot.AvatarModifiedRevision;
                }

                if (snapshot.MovementSnapshot != null)
                {
                    _remoteUserPool.TryApplyMoveSnapshot(
                        characterId,
                        snapshot.MovementSnapshot,
                        ResolveWeddingMoveAction(snapshot),
                        System.Environment.TickCount,
                        out _);
                }

                if (!_weddingRemoteItemEffectRevisionByCharacterId.TryGetValue(characterId, out int syncedRevision)
                    || syncedRevision != snapshot.PacketOwnedItemEffectRevision)
                {
                    _remoteUserPool.TrySetItemEffect(
                        characterId,
                        snapshot.PacketOwnedItemEffectItemId,
                        pairCharacterId: null,
                        System.Environment.TickCount,
                        out _);
                    _weddingRemoteItemEffectRevisionByCharacterId[characterId] = snapshot.PacketOwnedItemEffectRevision;
                }
            }

            _remoteUserPool.RemoveBySourceTagExcept(WeddingRemoteUserSourceTag, desiredCharacterIds);
            foreach (int characterId in _weddingRemoteItemEffectRevisionByCharacterId.Keys.Except(desiredCharacterIds).ToArray())
            {
                _weddingRemoteItemEffectRevisionByCharacterId.Remove(characterId);
            }

            foreach (int characterId in _weddingRemoteAvatarModifiedRevisionByCharacterId.Keys.Except(desiredCharacterIds).ToArray())
            {
                _weddingRemoteAvatarModifiedRevisionByCharacterId.Remove(characterId);
            }

            foreach (int characterId in _weddingRemoteNameTagRevisionByCharacterId.Keys.Except(desiredCharacterIds).ToArray())
            {
                _weddingRemoteNameTagRevisionByCharacterId.Remove(characterId);
            }

            foreach (int characterId in _weddingRemoteProfileMetadataRevisionByCharacterId.Keys.Except(desiredCharacterIds).ToArray())
            {
                _weddingRemoteProfileMetadataRevisionByCharacterId.Remove(characterId);
            }

            foreach (int characterId in _weddingRemoteGuildMarkRevisionByCharacterId.Keys.Except(desiredCharacterIds).ToArray())
            {
                _weddingRemoteGuildMarkRevisionByCharacterId.Remove(characterId);
            }

            foreach (int characterId in _weddingRemoteTemporaryStatRevisionByCharacterId.Keys.Except(desiredCharacterIds).ToArray())
            {
                _weddingRemoteTemporaryStatRevisionByCharacterId.Remove(characterId);
            }
        }

        private void ClearWeddingRemoteActorsFromSharedPool()
        {
            _remoteUserPool.RemoveBySourceTag(WeddingRemoteUserSourceTag);
            _weddingRemoteItemEffectRevisionByCharacterId.Clear();
            _weddingRemoteAvatarModifiedRevisionByCharacterId.Clear();
            _weddingRemoteNameTagRevisionByCharacterId.Clear();
            _weddingRemoteProfileMetadataRevisionByCharacterId.Clear();
            _weddingRemoteGuildMarkRevisionByCharacterId.Clear();
            _weddingRemoteTemporaryStatRevisionByCharacterId.Clear();
        }

        internal static bool ShouldSyncWeddingNameTagMetadata(
            IReadOnlyDictionary<int, int> syncedNameTagRevisionByCharacterId,
            int characterId,
            int participantNameTagRevision)
        {
            return syncedNameTagRevisionByCharacterId == null
                || !syncedNameTagRevisionByCharacterId.TryGetValue(characterId, out int syncedRevision)
                || syncedRevision != participantNameTagRevision;
        }

        internal static bool ShouldSyncWeddingProfileMetadata(
            IReadOnlyDictionary<int, int> syncedProfileMetadataRevisionByCharacterId,
            int characterId,
            int participantProfileMetadataRevision)
        {
            return syncedProfileMetadataRevisionByCharacterId == null
                || !syncedProfileMetadataRevisionByCharacterId.TryGetValue(characterId, out int syncedRevision)
                || syncedRevision != participantProfileMetadataRevision;
        }

        internal static bool ShouldSyncWeddingGuildMarkMetadata(
            IReadOnlyDictionary<int, int> syncedGuildMarkRevisionByCharacterId,
            int characterId,
            int participantGuildMarkRevision)
        {
            return syncedGuildMarkRevisionByCharacterId == null
                || !syncedGuildMarkRevisionByCharacterId.TryGetValue(characterId, out int syncedRevision)
                || syncedRevision != participantGuildMarkRevision;
        }

        internal static bool ShouldSyncWeddingTemporaryStatMetadata(
            IReadOnlyDictionary<int, int> syncedTemporaryStatRevisionByCharacterId,
            int characterId,
            int participantTemporaryStatRevision)
        {
            return syncedTemporaryStatRevisionByCharacterId == null
                || !syncedTemporaryStatRevisionByCharacterId.TryGetValue(characterId, out int syncedRevision)
                || syncedRevision != participantTemporaryStatRevision;
        }

        internal static ushort ResolveWeddingTemporaryStatDelayForSharedPool(
            WeddingRemoteParticipantSnapshot snapshot)
        {
            return snapshot.TemporaryStatRevision > 0
                ? snapshot.TemporaryStatDelay
                : (ushort)0;
        }

        internal static bool IsWeddingRemoteSnapshotVisibleLikeClient(
            WeddingRemoteParticipantSnapshot snapshot)
        {
            return !snapshot.TemporaryStats.KnownState.IsHiddenLikeClient;
        }

        internal static bool TryApplyWeddingRemoteProfileMetadata(
            RemoteUserActorPool remoteUserPool,
            int characterId,
            CharacterBuild build,
            out string message)
        {
            message = null;
            if (remoteUserPool == null)
            {
                message = "Remote user pool is unavailable.";
                return false;
            }

            if (build == null)
            {
                message = $"Wedding remote user {characterId} does not have a build.";
                return false;
            }

            RemoteUserProfilePacket profilePacket = new(
                characterId,
                build.HasAuthoritativeProfileLevel ? Math.Max(1, build.Level) : null,
                build.HasAuthoritativeProfileJob ? Math.Max(0, build.Job) : null,
                build.HasAuthoritativeProfileGuild ? (build.GuildName ?? string.Empty) : null,
                build.HasAuthoritativeProfileAlliance ? (build.AllianceName ?? string.Empty) : null,
                build.HasAuthoritativeProfileFame ? build.Fame : null,
                build.HasAuthoritativeProfileWorldRank ? Math.Max(0, build.WorldRank) : null,
                build.HasAuthoritativeProfileJobRank ? Math.Max(0, build.JobRank) : null,
                build.HasAuthoritativeProfileRide ? build.HasMonsterRiding : null,
                build.HasAuthoritativeProfilePendantSlot ? build.HasPendantSlotExtension : null,
                build.HasAuthoritativeProfilePocketSlot ? build.HasPocketSlot : null,
                build.HasAuthoritativeProfileTraits ? Math.Max(0, build.TraitCharisma) : null,
                build.HasAuthoritativeProfileTraits ? Math.Max(0, build.TraitInsight) : null,
                build.HasAuthoritativeProfileTraits ? Math.Max(0, build.TraitWill) : null,
                build.HasAuthoritativeProfileTraits ? Math.Max(0, build.TraitCraft) : null,
                build.HasAuthoritativeProfileTraits ? Math.Max(0, build.TraitSense) : null,
                build.HasAuthoritativeProfileTraits ? Math.Max(0, build.TraitCharm) : null,
                build.HasAuthoritativeProfileMedal ? true : null,
                build.HasAuthoritativeProfileCollection ? true : null);

            if (profilePacket.Level == null
                && profilePacket.JobId == null
                && profilePacket.GuildName == null
                && profilePacket.AllianceName == null
                && profilePacket.Fame == null
                && profilePacket.WorldRank == null
                && profilePacket.JobRank == null
                && profilePacket.HasRide == null
                && profilePacket.HasPendantSlot == null
                && profilePacket.HasPocketSlot == null
                && profilePacket.TraitCharisma == null
                && profilePacket.TraitInsight == null
                && profilePacket.TraitWill == null
                && profilePacket.TraitCraft == null
                && profilePacket.TraitSense == null
                && profilePacket.TraitCharm == null
                && profilePacket.HasMedal == null
                && profilePacket.HasCollection == null)
            {
                message = $"Wedding remote user {characterId} does not carry authoritative profile metadata.";
                return false;
            }

            return remoteUserPool.TryApplyProfileMetadata(profilePacket, out message);
        }

        internal static bool TryApplyWeddingRemoteGuildMarkMetadata(
            RemoteUserActorPool remoteUserPool,
            int characterId,
            CharacterBuild build,
            out string message)
        {
            message = null;
            if (remoteUserPool == null)
            {
                message = "Remote user pool is unavailable.";
                return false;
            }

            if (build == null)
            {
                message = $"Wedding remote user {characterId} does not have a build.";
                return false;
            }

            bool hasGuildNameOwner = build.HasAuthoritativeProfileGuild;
            bool hasGuildMarkOwner =
                build.GuildMarkBackgroundId.HasValue
                || build.GuildMarkBackgroundColor.HasValue
                || build.GuildMarkId.HasValue
                || build.GuildMarkColor.HasValue;
            if (!hasGuildNameOwner && !hasGuildMarkOwner)
            {
                message = $"Wedding remote user {characterId} does not carry authoritative guild-mark metadata.";
                return false;
            }

            return remoteUserPool.TryApplyGuildMark(
                characterId,
                build.GuildMarkBackgroundId ?? 0,
                build.GuildMarkBackgroundColor ?? 0,
                build.GuildMarkId ?? 0,
                build.GuildMarkColor ?? 0,
                out message);
        }

        private static int ResolveWeddingRemoteUserId(WeddingRemoteParticipantSnapshot snapshot)
        {
            if (snapshot.CharacterId > 0)
            {
                return snapshot.CharacterId;
            }

            return ResolveSyntheticRemoteUserId("wedding", snapshot.Name);
        }

        private static byte ResolveWeddingMoveAction(WeddingRemoteParticipantSnapshot snapshot)
        {
            int actionCode = snapshot.MovementSnapshot?.PassivePosition.Action switch
            {
                Physics.MoveAction.Walk => 1,
                Physics.MoveAction.Jump => 2,
                Physics.MoveAction.Fall => 3,
                Physics.MoveAction.Ladder => 4,
                Physics.MoveAction.Rope => 5,
                Physics.MoveAction.Swim => 6,
                Physics.MoveAction.Fly => 7,
                Physics.MoveAction.Attack => 8,
                Physics.MoveAction.Hit => 9,
                Physics.MoveAction.Die => 10,
                _ => 0
            };
            return (byte)((actionCode << 1) | (snapshot.FacingRight ? 0 : 1));
        }
    }
}
