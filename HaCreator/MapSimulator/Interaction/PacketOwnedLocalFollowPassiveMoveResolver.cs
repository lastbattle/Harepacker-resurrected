using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketOwnedLocalFollowPassiveMoveResolver
    {
        public static PassivePositionSnapshot ResolveFollowerPassivePosition(
            PlayerMovementSyncSnapshot driverMovementSnapshot,
            Vector2 driverPosition,
            bool driverFacingRight,
            string driverActionName,
            int driverFootholdId,
            int currentTime)
        {
            PassivePositionSnapshot passivePosition = driverMovementSnapshot != null
                ? driverMovementSnapshot.SampleAtTime(currentTime)
                : CreateFallbackPassivePosition(driverPosition, driverFacingRight, driverActionName, driverFootholdId, currentTime);

            Vector2 driverWorldPosition = new(passivePosition.X, passivePosition.Y);
            string sampledActionName = ResolveActionName(passivePosition.Action, driverActionName);
            if (!RemoteUserActorPool.TryResolveFollowDriverOffsetPosition(
                    driverWorldPosition,
                    passivePosition.FacingRight,
                    sampledActionName,
                    out Vector2 followerPosition,
                    out bool followerFacingRight))
            {
                followerPosition = driverWorldPosition;
                followerFacingRight = passivePosition.FacingRight;
            }

            return passivePosition with
            {
                X = (int)MathF.Round(followerPosition.X),
                Y = (int)MathF.Round(followerPosition.Y),
                FacingRight = followerFacingRight
            };
        }

        private static PassivePositionSnapshot CreateFallbackPassivePosition(
            Vector2 driverPosition,
            bool driverFacingRight,
            string driverActionName,
            int driverFootholdId,
            int currentTime)
        {
            return new PassivePositionSnapshot
            {
                X = (int)MathF.Round(driverPosition.X),
                Y = (int)MathF.Round(driverPosition.Y),
                VelocityX = 0,
                VelocityY = 0,
                Action = ResolveMoveAction(driverActionName),
                FootholdId = driverFootholdId,
                TimeStamp = currentTime,
                FacingRight = driverFacingRight
            };
        }

        private static string ResolveActionName(MoveAction moveAction, string fallbackActionName)
        {
            return moveAction switch
            {
                MoveAction.Walk => CharacterPart.GetActionString(CharacterAction.Walk1),
                MoveAction.Jump or MoveAction.Fall => CharacterPart.GetActionString(CharacterAction.Jump),
                MoveAction.Ladder => CharacterPart.GetActionString(CharacterAction.Ladder),
                MoveAction.Rope => CharacterPart.GetActionString(CharacterAction.Rope),
                MoveAction.Swim => CharacterPart.GetActionString(CharacterAction.Swim),
                MoveAction.Fly => CharacterPart.GetActionString(CharacterAction.Fly),
                MoveAction.Die => CharacterPart.GetActionString(CharacterAction.Dead),
                MoveAction.Stand or MoveAction.Attack or MoveAction.Hit => fallbackActionName ?? CharacterPart.GetActionString(CharacterAction.Stand1),
                _ => fallbackActionName ?? CharacterPart.GetActionString(CharacterAction.Stand1)
            };
        }

        private static MoveAction ResolveMoveAction(string actionName)
        {
            if (string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Walk1), StringComparison.OrdinalIgnoreCase))
            {
                return MoveAction.Walk;
            }

            if (string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Jump), StringComparison.OrdinalIgnoreCase))
            {
                return MoveAction.Jump;
            }

            if (string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Ladder), StringComparison.OrdinalIgnoreCase))
            {
                return MoveAction.Ladder;
            }

            if (string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Rope), StringComparison.OrdinalIgnoreCase))
            {
                return MoveAction.Rope;
            }

            if (string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Swim), StringComparison.OrdinalIgnoreCase))
            {
                return MoveAction.Swim;
            }

            if (string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Fly), StringComparison.OrdinalIgnoreCase))
            {
                return MoveAction.Fly;
            }

            if (string.Equals(actionName, CharacterPart.GetActionString(CharacterAction.Dead), StringComparison.OrdinalIgnoreCase))
            {
                return MoveAction.Die;
            }

            return MoveAction.Stand;
        }
    }
}
