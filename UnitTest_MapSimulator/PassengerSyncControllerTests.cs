using System;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class PassengerSyncControllerTests
    {
        [Fact]
        public void SyncPlayer_AttachesToRegularShipDeckAndCarriesPositionForward()
        {
            var transportField = new TransportationField();
            transportField.Initialize(shipKind: 0, x: 100, y: 100, x0: 0, f: 0, tMove: 2);
            transportField.SetShipVisual(width: 100, height: 100);

            Assert.True(transportField.TryGetDeckBounds(out _, out _, out float deckY));

            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            player.SetPosition(0, deckY);

            var controller = new PassengerSyncController();

            Assert.True(controller.SyncPlayer(player, null, transportField));
            float initialX = player.X;

            transportField.EnterShipMove();
            transportField.Update(unchecked(Environment.TickCount + 100), 0.1f);

            Assert.True(controller.SyncPlayer(player, null, transportField));
            Assert.NotNull(player.Physics.CurrentFoothold);
            Assert.True(player.X > initialX, $"Expected the ship deck sync to move the player forward with the ship, but X stayed at {player.X}.");
        }

        [Fact]
        public void SyncGroundMobPassengers_AttachesGroundMobToRegularShipDeck()
        {
            var transportField = new TransportationField();
            transportField.Initialize(shipKind: 0, x: 100, y: 100, x0: 0, f: 0, tMove: 2);
            transportField.SetShipVisual(width: 100, height: 100);

            Assert.True(transportField.TryGetDeckBounds(out float deckLeft, out float deckRight, out float deckY));

            var movement = new MobMovementInfo
            {
                MoveType = MobMoveType.Move,
                X = (deckLeft + deckRight) / 2f,
                Y = deckY
            };

            var controller = new PassengerSyncController();

            Assert.Equal(1, controller.SyncGroundMobPassengers(new[] { movement }, null, transportField));
            float initialX = movement.X;

            transportField.EnterShipMove();
            transportField.Update(unchecked(Environment.TickCount + 100), 0.1f);

            Assert.Equal(1, controller.SyncGroundMobPassengers(new[] { movement }, null, transportField));
            Assert.NotNull(movement.CurrentFoothold);
            Assert.True(movement.X > initialX, $"Expected the mob to ride the ship deck forward, but X stayed at {movement.X}.");
            Assert.True(movement.PlatformRight > movement.PlatformLeft);
        }

        [Fact]
        public void BalrogShipVisuals_DoNotExposePassengerDeckBounds()
        {
            var transportField = new TransportationField();
            transportField.Initialize(shipKind: 1, x: 100, y: 100, x0: 0, f: 0, tMove: 2);
            transportField.SetShipVisual(width: 100, height: 100);

            Assert.False(transportField.TryGetDeckBounds(out _, out _, out _));

            var player = new PlayerCharacter(device: null, texturePool: null, build: null);
            player.SetPosition(100, 65);

            var controller = new PassengerSyncController();

            Assert.False(controller.SyncPlayer(player, null, transportField));
            Assert.Null(player.Physics.CurrentFoothold);
        }
    }
}
