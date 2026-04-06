using System.Collections.Generic;
using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator
{
    public sealed class PacketOwnedSummonedHitOverlayParityTests
    {
        [Fact]
        public void ClearAttachedHitEffects_RemovesOnlyEffectsOwnedByTheInterruptedSummon()
        {
            var effects = new List<TestHitEffect>
            {
                new(1001),
                new(2002),
                new(1001)
            };

            int removed = PacketOwnedSummonUpdateRules.ClearAttachedHitEffects(
                effects,
                1001,
                static effect => effect.AttachedSummonObjectId);

            Assert.Equal(2, removed);
            Assert.Single(effects);
            Assert.Equal(2002, effects[0].AttachedSummonObjectId);
        }

        [Fact]
        public void ClearAttachedHitEffects_IgnoresInvalidSummonIds()
        {
            var effects = new List<TestHitEffect>
            {
                new(1001)
            };

            int removed = PacketOwnedSummonUpdateRules.ClearAttachedHitEffects(
                effects,
                0,
                static effect => effect.AttachedSummonObjectId);

            Assert.Equal(0, removed);
            Assert.Single(effects);
        }

        private sealed record TestHitEffect(int AttachedSummonObjectId);
    }
}
