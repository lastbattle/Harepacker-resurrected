using HaCreator.MapSimulator.Character;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private string HandleCharacterInfoPopularityRequest(
            UI.UserInfoUI.UserInfoActionContext context,
            UI.UserInfoUI.PopularityChangeDirection direction)
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

            if (direction == UI.UserInfoUI.PopularityChangeDirection.Down && targetBuild.Fame <= 0)
            {
                return $"{context.CharacterName} is already at 0 Fame.";
            }

            int updatedFame = System.Math.Max(0, targetBuild.Fame + (direction == UI.UserInfoUI.PopularityChangeDirection.Up ? 1 : -1));
            targetBuild.Fame = updatedFame;

            if (_remoteUserPool.TryGetActor(context.CharacterId, out var actor) || _remoteUserPool.TryGetActorByName(context.CharacterName, out actor))
            {
                if (actor?.Build != null)
                {
                    actor.Build.Fame = updatedFame;
                }
            }

            string directionLabel = direction == UI.UserInfoUI.PopularityChangeDirection.Up ? "up" : "down";
            return $"Popularity {directionLabel} preview queued for {context.CharacterName}. Local fame preview is now {updatedFame}.";
        }
    }
}
