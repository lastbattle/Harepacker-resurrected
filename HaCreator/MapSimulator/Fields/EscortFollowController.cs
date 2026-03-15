using System;
using System.Collections.Generic;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;

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
        internal const float AttachHorizontalRange = 80f;
        internal const float AttachVerticalRange = 30f;
        internal const float ReleaseHorizontalRange = 140f;
        internal const float ReleaseVerticalRange = 60f;
        private const float AirborneFootholdCarryTolerance = 24f;
        private const int MaxConnectedFootholds = 512;
        private const float MaxTraversableFootholdHeightDelta = 120f;

        private readonly HashSet<MobMovementInfo> _attachedFollowers = new();

        public bool UpdateEscortFollow(PlayerCharacter player, MobMovementInfo movement)
        {
            if (movement == null)
            {
                return false;
            }

            bool attached = _attachedFollowers.Contains(movement);
            FootholdLine playerFoothold = ResolvePlayerFoothold(player, attached);
            if (!CanEvaluate(player, playerFoothold, movement))
            {
                _attachedFollowers.Remove(movement);
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
                _attachedFollowers.Remove(movement);
                return false;
            }

            _attachedFollowers.Add(movement);
            return true;
        }

        public void Clear()
        {
            _attachedFollowers.Clear();
        }

        private static bool CanEvaluate(PlayerCharacter player, FootholdLine playerFoothold, MobMovementInfo movement)
        {
            return player?.IsAlive == true
                   && !player.GmFlyMode
                   && !player.Physics.IsOnLadderOrRope
                   && !player.Physics.IsUserFlying()
                   && !player.Physics.IsInSwimArea
                   && playerFoothold != null
                   && movement.CurrentFoothold != null
                   && movement.MoveType != MobMoveType.Fly
                   && !movement.IsInKnockback;
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
    }
}
