using System.Collections.Generic;
using HaCreator.MapSimulator.Combat;

namespace UnitTest_MapSimulator
{
    public sealed class MobAttackEffectPlacementParityTests
    {
        [Fact]
        public void BuildRangePositionXs_RandomPositionsPreserveAuthoredRandomOrder()
        {
            List<float> positions = MobAttackSystem.BuildRangePositionXs(
                left: -300f,
                right: 300f,
                count: 8,
                randomPos: true,
                random: new System.Random(7));

            Assert.Equal(8, positions.Count);
            Assert.DoesNotContain(positions, x => x < -300f || x > 300f);

            bool isMonotonicAscending = true;
            for (int i = 1; i < positions.Count; i++)
            {
                if (positions[i] < positions[i - 1])
                {
                    isMonotonicAscending = false;
                    break;
                }
            }

            Assert.False(isMonotonicAscending);
        }

        [Fact]
        public void BuildRangePositionXsWithSpacing_UsesExactEffectDistanceAcrossSweepRange()
        {
            List<float> positions = MobAttackSystem.BuildRangePositionXsWithSpacing(
                left: -800f,
                right: 800f,
                count: 6,
                spacing: 300f);

            Assert.Equal(new[] { -800f, -500f, -200f, 100f, 400f, 700f }, positions);
        }
    }
}
