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
                    isVisibleInWorld: true);
                TryApplyWeddingRemoteProfileMetadata(_remoteUserPool, characterId, build, out _);

                _remoteUserPool.TrySetPortableChair(
                    characterId,
                    snapshot.PortableChairItemId,
                    out _,
                    snapshot.PortableChairPairCharacterId);

                _remoteUserPool.TryApplyTemporaryStatSnapshot(
                    characterId,
                    snapshot.TemporaryStats,
                    delay: 0,
                    out _);
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
        }

        private void ClearWeddingRemoteActorsFromSharedPool()
        {
            _remoteUserPool.RemoveBySourceTag(WeddingRemoteUserSourceTag);
            _weddingRemoteItemEffectRevisionByCharacterId.Clear();
            _weddingRemoteAvatarModifiedRevisionByCharacterId.Clear();
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

            int? level = build.HasAuthoritativeProfileLevel ? Math.Max(1, build.Level) : null;
            int? jobId = build.HasAuthoritativeProfileJob ? Math.Max(0, build.Job) : null;
            string guildName = build.HasAuthoritativeProfileGuild
                ? (build.GuildName ?? string.Empty)
                : null;
            if (!level.HasValue && !jobId.HasValue && guildName == null)
            {
                message = $"Wedding remote user {characterId} does not carry authoritative profile metadata.";
                return false;
            }

            return remoteUserPool.TryApplyProfileMetadata(characterId, level, guildName, jobId, out message);
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
