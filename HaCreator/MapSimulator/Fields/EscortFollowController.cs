using System;
using System.Collections.Generic;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Tracks client-style auto-follow attachment requests for escort mobs.
    /// The real client sends a follow request only after proximity, vertical alignment,
    /// and traversability checks succeed. The simulator mirrors that gate and keeps
    /// the escort attached until the path becomes invalid or the distance meaningfully breaks.
    /// </summary>
    public sealed class EscortFollowController
    {
        // CUserLocal::TryAutoRequestFollowCharacter checks |dx| <= 80 and |dy| <= 30
        // before issuing the follow request.
        internal const float AttachHorizontalRange = 80f;
        internal const float AttachVerticalRange = 30f;
        internal const float ReleaseHorizontalRange = 140f;
        internal const float ReleaseVerticalRange = 60f;
        internal const int FollowRequestThrottleMs = 1000;
        private const float AirborneFootholdCarryTolerance = 24f;
        private const int MaxConnectedFootholds = 512;
        private const float MaxTraversableFootholdHeightDelta = 120f;

        private MobMovementInfo _attachedFollower;
        private int _lastFollowRequestTime = int.MinValue;
        private int _lastUpdateToken = int.MinValue;
        private MobMovementInfo _frameStartingFollower;
        private bool _frameStartingFollowerProcessed;

        public bool UpdateEscortFollow(
            PlayerCharacter player,
            MobMovementInfo movement,
            bool movementLocked = false,
            bool followAllowed = true,
            int currentTime = int.MinValue,
            int updateToken = 0)
        {
            if (movement == null)
            {
                return false;
            }

            BeginUpdatePass(updateToken);

            bool attached = ReferenceEquals(_attachedFollower, movement);
            bool wasFrameStartingFollower = ReferenceEquals(_frameStartingFollower, movement);

            if (!followAllowed)
            {
                if (attached)
                {
                    _attachedFollower = null;
                }

                if (wasFrameStartingFollower)
                {
                    _frameStartingFollowerProcessed = true;
                }

                return false;
            }

            FootholdLine playerFoothold = ResolvePlayerFoothold(player, attached);
            if (!CanEvaluate(player, playerFoothold, movement, movementLocked, attached))
            {
                if (attached)
                {
                    _attachedFollower = null;
                }

                if (wasFrameStartingFollower)
                {
                    _frameStartingFollowerProcessed = true;
                }

                return false;
            }

            float playerFollowY = ResolvePlayerFollowY(player, playerFoothold, attached);
            float dx = MathF.Abs(player.X - movement.X);
            float dy = MathF.Abs(playerFollowY - movement.Y);

            bool withinWindow = attached
                ? dx <= ReleaseHorizontalRange && dy <= ReleaseVerticalRange
                : dx <= AttachHorizontalRange && dy <= AttachVerticalRange;

            if (!withinWindow || !CanTraverseBetween(playerFoothold, movement.CurrentFoothold))
            {
                if (attached)
                {
                    _attachedFollower = null;
                }

                if (wasFrameStartingFollower)
                {
                    _frameStartingFollowerProcessed = true;
                }

                return false;
            }

            if (wasFrameStartingFollower)
            {
                _frameStartingFollowerProcessed = true;
            }
            else if (_attachedFollower != null || (_frameStartingFollower != null && !_frameStartingFollowerProcessed))
            {
                return false;
            }

            if (!attached && !CanSendFollowRequest(currentTime))
            {
                return false;
            }

            _attachedFollower = movement;
            if (!attached)
            {
                _lastFollowRequestTime = currentTime;
            }

            return true;
        }

        public void Clear()
        {
            _attachedFollower = null;
            _lastFollowRequestTime = int.MinValue;
            _frameStartingFollower = null;
            _frameStartingFollowerProcessed = false;
            _lastUpdateToken = int.MinValue;
        }

        private void BeginUpdatePass(int updateToken)
        {
            if (_lastUpdateToken == updateToken)
            {
                return;
            }

            _lastUpdateToken = updateToken;
            _frameStartingFollower = _attachedFollower;
            _frameStartingFollowerProcessed = false;
        }

        private static bool CanEvaluate(
            PlayerCharacter player,
            FootholdLine playerFoothold,
            MobMovementInfo movement,
            bool movementLocked,
            bool attached)
        {
            return (attached
                       ? CanMaintainEscortFollow(player)
                       : CanIssueFollowRequest(player, movementLocked))
                   && playerFoothold != null
                   && movement.CurrentFoothold != null
                   && movement.MoveType != MobMoveType.Fly
                   && !movement.IsInKnockback;
        }

        private static bool CanIssueFollowRequest(PlayerCharacter player, bool movementLocked)
        {
            return CanMaintainEscortFollow(player)
                   && !movementLocked
                   && player.State != PlayerState.Sitting
                   && !player.IsMovementLockedBySkillTransform
                   && !player.HasActiveMorphTransform
                   && !HasActiveRideState(player);
        }

        private static bool CanMaintainEscortFollow(PlayerCharacter player)
        {
            return player?.IsAlive == true
                   && player.Physics != null
                   && !player.GmFlyMode
                   && !player.Physics.IsOnLadderOrRope
                   && !player.Physics.IsUserFlying()
                   && !player.Physics.IsInSwimArea
                   && !string.Equals(player.CurrentActionName, CharacterPart.GetActionString(CharacterAction.Ghost), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool HasActiveRideState(PlayerCharacter player)
        {
            if (player == null)
            {
                return false;
            }

            CharacterPart mountedRenderOwner = player.ResolveMountedStateTamingMobPart();
            return FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
                       mountedRenderOwner,
                       player.CurrentActionName,
                       mechanicMode: null,
                       activeMountedRenderOwner: mountedRenderOwner) > 0;
        }

        private static FootholdLine ResolvePlayerFoothold(PlayerCharacter player, bool allowAirborneCarry)
        {
            if (player?.Physics == null)
            {
                return null;
            }

            if (player.Physics.CurrentFoothold != null)
            {
                return player.Physics.CurrentFoothold;
            }

            if (!allowAirborneCarry)
            {
                return null;
            }

            FootholdLine fallStartFoothold = player.Physics.FallStartFoothold;
            return IsWithinFootholdCarryWindow(fallStartFoothold, player.X)
                ? fallStartFoothold
                : null;
        }

        private static float ResolvePlayerFollowY(PlayerCharacter player, FootholdLine playerFoothold, bool allowAirborneCarry)
        {
            if (player?.Physics == null
                || playerFoothold == null
                || player.Physics.CurrentFoothold != null
                || !allowAirborneCarry)
            {
                return player?.Y ?? 0f;
            }

            return CalculateYOnFoothold(playerFoothold, player.X);
        }

        private static bool IsWithinFootholdCarryWindow(FootholdLine foothold, float x)
        {
            if (foothold == null)
            {
                return false;
            }

            float minX = MathF.Min(foothold.FirstDot.X, foothold.SecondDot.X) - AirborneFootholdCarryTolerance;
            float maxX = MathF.Max(foothold.FirstDot.X, foothold.SecondDot.X) + AirborneFootholdCarryTolerance;
            return x >= minX && x <= maxX;
        }

        private static float CalculateYOnFoothold(FootholdLine foothold, float x)
        {
            float x1 = foothold.FirstDot.X;
            float y1 = foothold.FirstDot.Y;
            float x2 = foothold.SecondDot.X;
            float y2 = foothold.SecondDot.Y;

            if (MathF.Abs(x2 - x1) <= float.Epsilon)
            {
                return (y1 + y2) * 0.5f;
            }

            float t = (x - x1) / (x2 - x1);
            t = Math.Clamp(t, 0f, 1f);
            return y1 + ((y2 - y1) * t);
        }

        public static bool CanTraverseBetween(FootholdLine start, FootholdLine target)
        {
            if (start == null || target == null)
            {
                return false;
            }

            if (ReferenceEquals(start, target))
            {
                return true;
            }

            float targetY = GetMidpointY(target);
            var visited = new HashSet<FootholdLine>();
            var queue = new Queue<FootholdLine>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0 && visited.Count <= MaxConnectedFootholds)
            {
                FootholdLine foothold = queue.Dequeue();
                if (ReferenceEquals(foothold, target))
                {
                    return true;
                }

                if (MathF.Abs(GetMidpointY(foothold) - targetY) > MaxTraversableFootholdHeightDelta)
                {
                    continue;
                }

                EnqueueConnectedFootholds(foothold.FirstDot as FootholdAnchor, target, visited, queue);
                EnqueueConnectedFootholds(foothold.SecondDot as FootholdAnchor, target, visited, queue);
            }

            return false;
        }

        private static void EnqueueConnectedFootholds(
            FootholdAnchor anchor,
            FootholdLine target,
            HashSet<FootholdLine> visited,
            Queue<FootholdLine> queue)
        {
            if (anchor?.connectedLines == null)
            {
                return;
            }

            foreach (MapleLine line in anchor.connectedLines)
            {
                if (line is not FootholdLine foothold || foothold.IsWall || !visited.Add(foothold))
                {
                    continue;
                }

                if (MathF.Abs(GetMidpointY(foothold) - GetMidpointY(target)) > MaxTraversableFootholdHeightDelta)
                {
                    continue;
                }

                queue.Enqueue(foothold);
            }
        }

        private static float GetMidpointY(FootholdLine foothold)
        {
            return (foothold.FirstDot.Y + foothold.SecondDot.Y) * 0.5f;
        }

        private bool CanSendFollowRequest(int currentTime)
        {
            if (currentTime == int.MinValue || _lastFollowRequestTime == int.MinValue)
            {
                return true;
            }

            return currentTime - _lastFollowRequestTime >= FollowRequestThrottleMs;
        }
    }
}
