using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class MessengerRemoteUserSynchronizer
    {
        private const string MessengerRemoteUserSourceTag = "messenger";

        internal static void Sync(
            RemoteUserActorPool remoteUserPool,
            MessengerRuntime messengerRuntime,
            CharacterBuild localTemplate,
            Vector2 anchorPosition,
            Func<string, string, int> resolveSyntheticRemoteUserId)
        {
            remoteUserPool?.RemoveBySourceTag(MessengerRemoteUserSourceTag);
            if (remoteUserPool == null || messengerRuntime == null || localTemplate == null || resolveSyntheticRemoteUserId == null)
            {
                return;
            }

            foreach (MessengerRemoteParticipantSnapshot snapshot in messengerRuntime.GetRemoteParticipantSnapshots())
            {
                int characterId = resolveSyntheticRemoteUserId(MessengerRemoteUserSourceTag, snapshot.Name);
                CharacterBuild build = localTemplate.Clone();
                build.Name = snapshot.Name;
                build.Level = snapshot.Level > 0 ? snapshot.Level : build.Level;
                build.JobName = string.IsNullOrWhiteSpace(snapshot.JobName) ? build.JobName : snapshot.JobName.Trim();

                if (snapshot.AvatarLook != null)
                {
                    remoteUserPool.TryAddOrUpdateAvatarLook(
                        characterId,
                        snapshot.Name,
                        snapshot.AvatarLook,
                        build,
                        anchorPosition,
                        out _,
                        facingRight: true,
                        actionName: null,
                        sourceTag: MessengerRemoteUserSourceTag,
                        isVisibleInWorld: false);
                    continue;
                }

                remoteUserPool.TryAddOrUpdate(
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
