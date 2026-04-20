using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System.Drawing;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class RemoteGrenadeParityTests
    {
        [Fact]
        public void MonsterBombPresentation_IsGravityFreeWithClientDelays()
        {
            RemoteUserActorPool.RemoteGrenadePresentation presentation =
                RemoteUserActorPool.BuildRemoteThrowGrenadePresentationForParity(
                    characterId: 1,
                    skillId: 4341003,
                    skillLevel: 10,
                    ownerLevel: 120,
                    target: new Point(100, 200),
                    keyDownTime: 700,
                    facingRight: true,
                    currentTime: 5000,
                    maxGaugeTimeMs: 2000);

            Assert.True(presentation.GravityFree);
            Assert.True(presentation.UsesMonsterBombGauge);
            Assert.Equal(450, presentation.InitDelayMs);
            Assert.Equal(950, presentation.ExplosionDelayMs);
            Assert.True(presentation.DragX > 0);
            Assert.True(presentation.DragY > 0);
        }

        [Fact]
        public void NonMonsterBombPresentation_IsNotGravityFree()
        {
            RemoteUserActorPool.RemoteGrenadePresentation presentation =
                RemoteUserActorPool.BuildRemoteThrowGrenadePresentationForParity(
                    characterId: 2,
                    skillId: 5201002,
                    skillLevel: 20,
                    ownerLevel: 100,
                    target: new Point(300, 420),
                    keyDownTime: 900,
                    facingRight: false,
                    currentTime: 7000);

            Assert.False(presentation.GravityFree);
            Assert.False(presentation.UsesMonsterBombGauge);
            Assert.Equal(0, presentation.InitDelayMs);
            Assert.Equal(0, presentation.ExplosionDelayMs);
            Assert.Equal(0, presentation.DragX);
            Assert.Equal(0, presentation.DragY);
        }

        [Fact]
        public void NonGravityFreeTrajectory_AppliesDownwardGravityOffset()
        {
            Vector2 origin = new(0f, 0f);
            Vector2 impact = new(600f, -600f);
            int elapsedMs = 500;
            int durationMs = 1000;

            Vector2 gravityFreePosition = RemoteUserActorPool.ResolveRemoteGrenadePositionForParity(
                origin,
                impact,
                elapsedMs,
                durationMs,
                dragX: 0,
                dragY: 0,
                gravityFree: true);
            Vector2 gravityPosition = RemoteUserActorPool.ResolveRemoteGrenadePositionForParity(
                origin,
                impact,
                elapsedMs,
                durationMs,
                dragX: 0,
                dragY: 0,
                gravityFree: false);

            Assert.Equal(gravityFreePosition.X, gravityPosition.X);
            Assert.True(gravityPosition.Y > gravityFreePosition.Y);
        }
    }
}
