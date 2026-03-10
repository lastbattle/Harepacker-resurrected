using System;
using System.Collections.Generic;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Physics;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Synchronizes players and ground mobs onto moving platform and ship-deck footholds.
    /// This keeps the existing physics and state machines on a foothold-backed seam.
    /// </summary>
    public sealed class PassengerSyncController
    {
        private const int SyntheticFootholdBaseId = -1000000;
        private const int TransportDeckFootholdId = SyntheticFootholdBaseId - 1;

        private readonly Dictionary<int, FootholdLine> _dynamicPlatformFootholds = new();
        private FootholdLine _transportDeckFoothold;

        public bool SyncPlayer(PlayerCharacter player, DynamicFootholdSystem dynamicFootholds, TransportationField transportField)
        {
            if (player == null || !player.IsAlive)
            {
                return false;
            }

            if (TryAttachPlayerToDynamicPlatform(player, dynamicFootholds))
            {
                return true;
            }

            if (TryAttachPlayerToTransportDeck(player, transportField))
            {
                return true;
            }

            DetachSyntheticFoothold(player.Physics);
            return false;
        }

        public int SyncGroundMobPassengers(IEnumerable<MobMovementInfo> movementInfos, DynamicFootholdSystem dynamicFootholds)
        {
            if (movementInfos == null)
            {
                return 0;
            }

            int syncedCount = 0;
            foreach (MobMovementInfo movement in movementInfos)
            {
                if (movement == null || movement.MoveType == MobMoveType.Fly)
                {
                    continue;
                }

                int platformId = dynamicFootholds?.GetPlatformAtPoint(movement.X, movement.Y, tolerance: 10f) ?? -1;
                if (platformId < 0)
                {
                    DetachSyntheticFoothold(movement);
                    continue;
                }

                DynamicPlatform platform = dynamicFootholds.GetPlatform(platformId);
                if (platform == null || !platform.IsActive || !platform.IsVisible)
                {
                    DetachSyntheticFoothold(movement);
                    continue;
                }

                FootholdLine foothold = GetOrCreateSyntheticFoothold(_dynamicPlatformFootholds, SyntheticFootholdBaseId - platformId, platform.X, platform.X + platform.Width, platform.Y);
                movement.X += platform.DeltaX;
                movement.Y = platform.Y;
                movement.CurrentFoothold = foothold;
                movement.PlatformLeft = (int)MathF.Round(platform.X);
                movement.PlatformRight = (int)MathF.Round(platform.X + platform.Width);
                movement.VelocityY = 0f;
                if (movement.JumpState != MobJumpState.Jumping)
                {
                    movement.JumpState = MobJumpState.None;
                }

                syncedCount++;
            }

            return syncedCount;
        }

        public void Clear()
        {
            _dynamicPlatformFootholds.Clear();
            _transportDeckFoothold = null;
        }

        private bool TryAttachPlayerToDynamicPlatform(PlayerCharacter player, DynamicFootholdSystem dynamicFootholds)
        {
            if (!CanAttachPlayer(player) || dynamicFootholds == null)
            {
                return false;
            }

            int platformId = dynamicFootholds.GetPlatformAtPoint(player.X, player.Y, tolerance: 12f);
            if (platformId < 0)
            {
                return false;
            }

            DynamicPlatform platform = dynamicFootholds.GetPlatform(platformId);
            if (platform == null || !platform.IsActive || !platform.IsVisible)
            {
                return false;
            }

            FootholdLine foothold = GetOrCreateSyntheticFoothold(_dynamicPlatformFootholds, SyntheticFootholdBaseId - platformId, platform.X, platform.X + platform.Width, platform.Y);
            player.SetPosition(player.X + platform.DeltaX, platform.Y);
            player.Physics.LandOnFoothold(foothold);
            player.Physics.VelocityY = 0;
            return true;
        }

        private bool TryAttachPlayerToTransportDeck(PlayerCharacter player, TransportationField transportField)
        {
            if (!CanAttachPlayer(player) || transportField == null)
            {
                return false;
            }

            if (!transportField.TryGetDeckBounds(out float deckLeft, out float deckRight, out float deckY))
            {
                return false;
            }

            if (!transportField.IsOnShipDeck(player.X, player.Y, deckY, deckRight - deckLeft))
            {
                return false;
            }

            _transportDeckFoothold = GetOrCreateSyntheticFoothold(
                cache: null,
                cacheKey: TransportDeckFootholdId,
                leftX: deckLeft,
                rightX: deckRight,
                y: deckY,
                existing: _transportDeckFoothold);

            var shipDelta = transportField.GetShipDelta();
            player.SetPosition(player.X + shipDelta.X, deckY);
            player.Physics.LandOnFoothold(_transportDeckFoothold);
            player.Physics.VelocityY = 0;
            return true;
        }

        private static bool CanAttachPlayer(PlayerCharacter player)
        {
            return !player.GmFlyMode
                   && !player.Physics.IsOnLadderOrRope
                   && !player.Physics.IsUserFlying()
                   && !player.Physics.IsInSwimArea
                   && player.Physics.VelocityY >= -20;
        }

        private static FootholdLine GetOrCreateSyntheticFoothold(
            Dictionary<int, FootholdLine> cache,
            int cacheKey,
            float leftX,
            float rightX,
            float y,
            FootholdLine existing = null)
        {
            FootholdLine foothold = existing;
            if (foothold == null && cache != null)
            {
                cache.TryGetValue(cacheKey, out foothold);
            }

            if (foothold == null)
            {
                var first = new FootholdAnchor(board: null, x: (int)MathF.Round(leftX), y: (int)MathF.Round(y), layer: 0, zm: 0, user: true);
                var second = new FootholdAnchor(board: null, x: (int)MathF.Round(rightX), y: (int)MathF.Round(y), layer: 0, zm: 0, user: true);
                foothold = new FootholdLine(board: null, first, second)
                {
                    num = cacheKey
                };

                cache?[cacheKey] = foothold;
                return foothold;
            }

            MoveSyntheticAnchor(foothold.FirstDot, leftX, y);
            MoveSyntheticAnchor(foothold.SecondDot, rightX, y);
            foothold.num = cacheKey;
            return foothold;
        }

        private static void MoveSyntheticAnchor(MapleDot dot, float x, float y)
        {
            int roundedX = (int)MathF.Round(x);
            int roundedY = (int)MathF.Round(y);

            if (dot.Board == null)
            {
                dot.MoveSilent(roundedX, roundedY);
                return;
            }

            dot.X = roundedX;
            dot.Y = roundedY;
        }

        private static void DetachSyntheticFoothold(CVecCtrl physics)
        {
            if (physics?.CurrentFoothold != null && physics.CurrentFoothold.num <= SyntheticFootholdBaseId)
            {
                physics.DetachFromFoothold();
            }
        }

        private static void DetachSyntheticFoothold(MobMovementInfo movement)
        {
            if (movement?.CurrentFoothold != null && movement.CurrentFoothold.num <= SyntheticFootholdBaseId)
            {
                movement.CurrentFoothold = null;
            }
        }
    }
}
