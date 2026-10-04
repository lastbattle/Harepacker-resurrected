using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

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
            if (remoteUserPool == null || messengerRuntime == null || localTemplate == null || resolveSyntheticRemoteUserId == null)
            {
                return;
            }

            IReadOnlyList<MessengerRemoteParticipantSnapshot> snapshots = messengerRuntime.GetRemoteParticipantSnapshots();
            remoteUserPool.RemoveBySourceTagExcept(
                MessengerRemoteUserSourceTag,
                snapshots.BuildMessengerRemoteUserKeepSet(resolveSyntheticRemoteUserId));

            foreach (MessengerRemoteParticipantSnapshot snapshot in snapshots)
            {
                int characterId = resolveSyntheticRemoteUserId(MessengerRemoteUserSourceTag, snapshot.Name);
                remoteUserPool.TryGetActor(characterId, out RemoteUserActor existingActor);
                CharacterBuild build = snapshot.CreateRemoteBuildTemplate(localTemplate, existingActor?.Build);
                if (build == null)
                {
                    continue;
                }

                LoginAvatarLook avatarLook = snapshot.ResolveRemoteAvatarLook();
                if (avatarLook != null)
                {
                    remoteUserPool.TryAddOrUpdateAvatarLook(
                        characterId,
                        snapshot.Name,
                        avatarLook,
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
