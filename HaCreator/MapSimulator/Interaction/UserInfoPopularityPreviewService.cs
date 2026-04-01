using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class UserInfoPopularityPreviewService
    {
        internal static string HandleRequest(
            UserInfoUI.UserInfoActionContext context,
            UserInfoUI.PopularityChangeDirection direction,
            RemoteUserActorPool remoteUserPool)
        {
            if (!context.IsRemoteTarget)
            {
                return "Popularity requests only apply to an inspected target.";
            }

            CharacterBuild targetBuild = context.Build;
            if (targetBuild == null || string.IsNullOrWhiteSpace(context.CharacterName))
            {
                return "Popularity request target is unavailable.";
            }

            if (direction == UserInfoUI.PopularityChangeDirection.Down && targetBuild.Fame <= 0)
            {
                return $"{context.CharacterName} is already at 0 Fame.";
            }

            int delta = direction == UserInfoUI.PopularityChangeDirection.Up ? 1 : -1;
            int updatedFame = System.Math.Max(0, targetBuild.Fame + delta);
            targetBuild.Fame = updatedFame;

            if (remoteUserPool != null &&
                (remoteUserPool.TryGetActor(context.CharacterId, out var actor) || remoteUserPool.TryGetActorByName(context.CharacterName, out actor)) &&
                actor?.Build != null)
            {
                actor.Build.Fame = updatedFame;
            }

            string directionLabel = direction == UserInfoUI.PopularityChangeDirection.Up ? "up" : "down";
            return $"Popularity {directionLabel} preview queued for {context.CharacterName}. Local fame preview is now {updatedFame}.";
        }
    }
}
