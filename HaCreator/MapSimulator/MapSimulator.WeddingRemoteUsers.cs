using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const string WeddingRemoteUserSourceTag = "wedding";

        private void SyncWeddingRemoteActorsToSharedPool(WeddingField field)
        {
            _remoteUserPool.RemoveBySourceTag(WeddingRemoteUserSourceTag);

            if (field == null)
            {
                return;
            }

            field.UseExternalRemoteActorRenderer = true;
            foreach (WeddingRemoteParticipantSnapshot snapshot in field.GetRemoteParticipantSnapshots())
            {
                CharacterBuild build = snapshot.Build?.Clone();
                if (build == null)
                {
                    continue;
                }

                int characterId = ResolveWeddingRemoteUserId(snapshot);
                _remoteUserPool.TryAddOrUpdate(
                    characterId,
                    build,
                    snapshot.Position,
                    out _,
                    snapshot.FacingRight,
                    snapshot.ActionName,
                    WeddingRemoteUserSourceTag,
                    isVisibleInWorld: true);
            }
        }

        private void ClearWeddingRemoteActorsFromSharedPool()
        {
            _remoteUserPool.RemoveBySourceTag(WeddingRemoteUserSourceTag);
        }

        private static int ResolveWeddingRemoteUserId(WeddingRemoteParticipantSnapshot snapshot)
        {
            if (snapshot.Role != WeddingParticipantRole.Guest && snapshot.CharacterId > 0)
            {
                return snapshot.CharacterId;
            }

            return ResolveSyntheticRemoteUserId("wedding", snapshot.Name);
        }
    }
}
