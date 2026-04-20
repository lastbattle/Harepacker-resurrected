using HaCreator.MapSimulator;

namespace UnitTest_MapSimulator
{
    public sealed class AnimationDisplayerReservedOwnerParityTests
    {
        [Fact]
        public void ReservedType4RestoreDelay_PrefersMetadataDuration()
        {
            int delayMs = MapSimulator.ResolveAnimationDisplayerReservedRemoteUtilityActionRestoreDelayMs(
                metadataDurationMs: 950,
                actionDurationMs: 350);

            Assert.Equal(950, delayMs);
        }

        [Fact]
        public void ReservedType4RestoreDelay_UsesActionDurationWhenMetadataMissing()
        {
            int delayMs = MapSimulator.ResolveAnimationDisplayerReservedRemoteUtilityActionRestoreDelayMs(
                metadataDurationMs: 0,
                actionDurationMs: 640);

            Assert.Equal(640, delayMs);
        }

        [Fact]
        public void ReservedType4RestoreDelay_FallsBackWhenNoDurationMetadataOrAction()
        {
            int delayMs = MapSimulator.ResolveAnimationDisplayerReservedRemoteUtilityActionRestoreDelayMs(
                metadataDurationMs: 0,
                actionDurationMs: 0);

            Assert.Equal(1200, delayMs);
        }
    }
}
