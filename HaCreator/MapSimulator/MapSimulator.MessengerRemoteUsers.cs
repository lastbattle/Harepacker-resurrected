using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const string MessengerRemoteUserSourceTag = "messenger";

        private void SyncMessengerRemoteActorsToSharedPool()
        {
            _remoteUserPool.RemoveBySourceTag(MessengerRemoteUserSourceTag);

            CharacterBuild template = _playerManager?.Player?.Build?.Clone();
            if (template == null)
            {
                return;
            }

            Vector2 anchorPosition = _playerManager?.Player?.Position ?? Vector2.Zero;
            foreach (MessengerRemoteParticipantSnapshot snapshot in _messengerRuntime.GetRemoteParticipantSnapshots())
            {
                CharacterBuild build = template.Clone();
                build.Name = snapshot.Name;
                build.Level = snapshot.Level > 0 ? snapshot.Level : build.Level;
                build.JobName = string.IsNullOrWhiteSpace(snapshot.JobName) ? build.JobName : snapshot.JobName.Trim();

                int characterId = ResolveSyntheticRemoteUserId("messenger", snapshot.Name);
                _remoteUserPool.TryAddOrUpdate(
                    characterId,
                    build,
                    anchorPosition,
                    out _,
                    facingRight: true,
                    actionName: null,
                    sourceTag: MessengerRemoteUserSourceTag,
                    isVisibleInWorld: false);
            }
        }
    }
}
