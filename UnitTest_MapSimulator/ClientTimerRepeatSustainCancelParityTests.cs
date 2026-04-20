using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System.Linq;

namespace UnitTest_MapSimulator
{
    public class ClientTimerRepeatSustainCancelParityTests
    {
        [Fact]
        public void OrderPacketOwnedExpiryFallbackCandidates_PrefersFacingSideDistanceAndSourceOrder()
        {
            ActiveSummon summon = new()
            {
                PositionX = 0f,
                PositionY = 0f,
                FacingRight = true,
                SkillData = new SkillData()
            };

            var ordered = SummonedPool.OrderPacketOwnedExpiryFallbackCandidates(
                    summon,
                    new[]
                    {
                        new PacketOwnedExpiryTargetCandidate(2, new Rectangle(-10, -10, 10, 10), sourceOrder: 0),  // opposite side
                        new PacketOwnedExpiryTargetCandidate(3, new Rectangle(20, -10, 10, 10), sourceOrder: 1),   // preferred side, farther
                        new PacketOwnedExpiryTargetCandidate(4, new Rectangle(10, -10, 10, 10), sourceOrder: 9),   // preferred side, nearer, later source
                        new PacketOwnedExpiryTargetCandidate(1, new Rectangle(10, -10, 10, 10), sourceOrder: 1)    // preferred side, nearer, earlier source
                    },
                    facingRight: true)
                .Select(candidate => candidate.MobObjectId)
                .ToArray();

            Assert.Equal(new[] { 1, 4, 3, 2 }, ordered);
        }

        [Fact]
        public void ResolvePacketOwnedExpiryFindHitMobInRectTargetOrder_FiltersOutOfRangeAfterStableOrdering()
        {
            ActiveSummon summon = new()
            {
                PositionX = 0f,
                PositionY = 0f,
                FacingRight = true,
                SkillData = new SkillData()
            };

            int[] targetOrder = SummonedPool.ResolvePacketOwnedExpiryFindHitMobInRectTargetOrder(
                summon,
                new[]
                {
                    new PacketOwnedExpiryTargetCandidate(10, new Rectangle(160, -5, 10, 10), sourceOrder: 0), // outside fallback range
                    new PacketOwnedExpiryTargetCandidate(11, new Rectangle(30, -5, 10, 10), sourceOrder: 9),  // in range
                    new PacketOwnedExpiryTargetCandidate(12, new Rectangle(10, -5, 10, 10), sourceOrder: 1),  // in range
                    new PacketOwnedExpiryTargetCandidate(13, new Rectangle(-20, -5, 10, 10), sourceOrder: 0)  // opposite side in range
                },
                maxTargets: 2,
                facingRight: true);

            Assert.Equal(new[] { 12, 11 }, targetOrder);
        }
    }
}
