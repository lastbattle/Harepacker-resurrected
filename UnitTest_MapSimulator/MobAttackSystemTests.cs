using System.Collections.Generic;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Combat;

namespace UnitTest_MapSimulator
{
    public sealed class MobAttackSystemTests
    {
        [Fact]
        public void SelectProjectileLaneIndices_PrefersCenteredSubsetAcrossAllAuthoredSlots()
        {
            MobAttackEntry attack = new MobAttackEntry
            {
                StartOffset = -4,
                AreaCount = 9,
                AttackCount = 5
            };

            List<float> authoredLaneXs = MobAttackSystem.BuildRangeSlotOffsets(attack, attack.AreaCount);
            List<int> selected = MobAttackSystem.SelectProjectileLaneIndices(
                authoredLaneXs,
                new[] { false, false, false, false, false, false, false, false, false },
                new[] { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue },
                requestedCount: 4,
                preferredX: 0f);

            Assert.Equal(new[] { 2, 3, 4, 5 }, selected);
        }

        [Fact]
        public void SelectProjectileLaneIndices_PrefersResolvedTargetsBeforeEmptySlots()
        {
            List<int> selected = MobAttackSystem.SelectProjectileLaneIndices(
                new[] { -4f, -3f, -2f, -1f, 0f, 1f, 2f, 3f, 4f },
                new[] { false, true, false, false, false, false, false, true, false },
                new[] { float.MaxValue, 2f, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, 1f, float.MaxValue },
                requestedCount: 2,
                preferredX: 0f);

            Assert.Equal(new[] { 1, 7 }, selected);
        }

        [Fact]
        public void SelectProjectileLaneIndices_FallsBackToNearestAuthoredSlotsOnPreferredSide()
        {
            MobAttackEntry attack = new MobAttackEntry
            {
                StartOffset = -4,
                AreaCount = 9,
                AttackCount = 5
            };

            List<float> authoredLaneXs = MobAttackSystem.BuildRangeSlotOffsets(attack, attack.AreaCount);
            List<int> selected = MobAttackSystem.SelectProjectileLaneIndices(
                authoredLaneXs,
                new[] { false, false, false, false, false, false, false, false, false },
                new[] { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue },
                requestedCount: 3,
                preferredX: 3.2f);

            Assert.Equal(new[] { 6, 7, 8 }, selected);
        }
    }
}
