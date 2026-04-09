using HaCreator.MapSimulator.Managers;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class MessengerRuntimeRemoteUserParity
    {
        internal static LoginAvatarLook ResolveRemoteAvatarLook(this MessengerRemoteParticipantSnapshot snapshot)
        {
            return snapshot.AvatarLook != null
                ? LoginAvatarLookCodec.CloneLook(snapshot.AvatarLook)
                : null;
        }

        internal static Character.CharacterBuild CreateRemoteBuildTemplate(
            this MessengerRemoteParticipantSnapshot snapshot,
            Character.CharacterBuild localTemplate,
            Character.CharacterBuild existingBuild)
        {
            Character.CharacterBuild build = existingBuild?.Clone() ?? localTemplate?.Clone();
            if (build == null)
            {
                return null;
            }

            build.Name = snapshot.Name;
            build.Level = snapshot.Level > 0 ? snapshot.Level : build.Level;
            build.JobName = string.IsNullOrWhiteSpace(snapshot.JobName) ? build.JobName : snapshot.JobName.Trim();
            return build;
        }

        internal static IReadOnlySet<int> BuildMessengerRemoteUserKeepSet(
            this IReadOnlyList<MessengerRemoteParticipantSnapshot> snapshots,
            System.Func<string, string, int> resolveSyntheticRemoteUserId)
        {
            var keepIds = new HashSet<int>();
            if (snapshots == null || resolveSyntheticRemoteUserId == null)
            {
                return keepIds;
            }

            foreach (MessengerRemoteParticipantSnapshot snapshot in snapshots)
            {
                if (string.IsNullOrWhiteSpace(snapshot.Name))
                {
                    continue;
                }

                keepIds.Add(resolveSyntheticRemoteUserId("messenger", snapshot.Name));
            }

            return keepIds;
        }
    }
}
