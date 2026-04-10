using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class UserInfoPopularityPreviewService
    {
        private const int PendingResolveDelayMs = 900;
        private PendingPopularityRequest? _pendingRequest;

        internal string HandleRequest(UserInfoUI.UserInfoActionContext context, UserInfoUI.PopularityChangeDirection direction, int currentTick)
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

            if (_pendingRequest.HasValue)
            {
                PendingPopularityRequest pending = _pendingRequest.Value;
                string pendingDirection = pending.Direction == UserInfoUI.PopularityChangeDirection.Up ? "up" : "down";
                return string.Equals(pending.TargetName, context.CharacterName, StringComparison.OrdinalIgnoreCase)
                    ? $"Popularity {pendingDirection} request for {pending.TargetName} is already waiting for the delayed result branch."
                    : $"Popularity {pendingDirection} request for {pending.TargetName} is already waiting for the delayed result branch.";
            }

            _pendingRequest = new PendingPopularityRequest(
                context.CharacterId,
                context.CharacterName.Trim(),
                targetBuild,
                direction,
                currentTick + PendingResolveDelayMs);

            string directionLabel = direction == UserInfoUI.PopularityChangeDirection.Up ? "up" : "down";
            return $"Popularity {directionLabel} request for {context.CharacterName} is now pending the delayed result branch.";
        }

        internal string Update(int currentTick, RemoteUserActorPool remoteUserPool)
        {
            if (!_pendingRequest.HasValue)
            {
                return null;
            }

            PendingPopularityRequest pending = _pendingRequest.Value;
            if (unchecked(currentTick - pending.ResolveAtTick) < 0)
            {
                return null;
            }

            _pendingRequest = null;

            CharacterBuild targetBuild = pending.TargetBuild;
            if (targetBuild == null)
            {
                return $"Popularity request for {pending.TargetName} expired before the target build could be resolved.";
            }

            if (pending.Direction == UserInfoUI.PopularityChangeDirection.Down && targetBuild.Fame <= 0)
            {
                return $"{pending.TargetName} is already at 0 Fame.";
            }

            int delta = pending.Direction == UserInfoUI.PopularityChangeDirection.Up ? 1 : -1;
            int updatedFame = Math.Max(0, targetBuild.Fame + delta);
            targetBuild.Fame = updatedFame;

            if (remoteUserPool != null &&
                (remoteUserPool.TryGetActor(pending.CharacterId, out var actor) || remoteUserPool.TryGetActorByName(pending.TargetName, out actor)) &&
                actor?.Build != null &&
                !ReferenceEquals(actor.Build, targetBuild))
            {
                actor.Build.Fame = updatedFame;
            }

            string directionLabel = pending.Direction == UserInfoUI.PopularityChangeDirection.Up ? "up" : "down";
            return $"Popularity {directionLabel} result applied for {pending.TargetName}. Fame is now {updatedFame}.";
        }

        private readonly record struct PendingPopularityRequest(
            int CharacterId,
            string TargetName,
            CharacterBuild TargetBuild,
            UserInfoUI.PopularityChangeDirection Direction,
            int ResolveAtTick);
    }
}
