using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const string WeddingRemoteUserSourceTag = "wedding";

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

                _remoteUserPool.TrySetPortableChair(
                    characterId,
                    snapshot.PortableChairItemId,
                    out _,
                    snapshot.PortableChairPairCharacterId);

                if (snapshot.TemporaryStats.HasPayload)
                {
                    _remoteUserPool.TryApplyTemporaryStatSnapshot(
                        characterId,
                        snapshot.TemporaryStats,
                        delay: 0,
                        out _);
                }

                if (snapshot.AvatarModifiedState is RemoteUserAvatarModifiedPacket avatarModifiedState)
                {
                    _remoteUserPool.TryApplyAvatarModified(
                        avatarModifiedState,
                        System.Environment.TickCount,
                        out _);
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
            }

            _remoteUserPool.RemoveBySourceTagExcept(WeddingRemoteUserSourceTag, desiredCharacterIds);
        }

        private void ClearWeddingRemoteActorsFromSharedPool()
        {
            _remoteUserPool.RemoveBySourceTag(WeddingRemoteUserSourceTag);
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
