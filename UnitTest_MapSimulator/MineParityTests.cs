using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class MineParityTests
    {
        [Fact]
        public void WildHunterMineFallback_RequiresActiveJaguarOwnership()
        {
            bool canDeploy = SkillManager.CanAutoDeployWildHunterMineWithoutVisibleMountedAction(
                actionName: null,
                jaguarRideOwnershipActive: false,
                jaguarMountItemId: 1932030);

            Assert.False(canDeploy);
        }

        [Fact]
        public void WildHunterMineFallback_AllowsActiveJaguarOwnershipWithoutVisibleMountedAction()
        {
            bool canDeploy = SkillManager.CanAutoDeployWildHunterMineWithoutVisibleMountedAction(
                actionName: string.Empty,
                jaguarRideOwnershipActive: true,
                jaguarMountItemId: 1932036);

            Assert.True(canDeploy);
        }

        [Fact]
        public void WildHunterMineFallback_RejectsNonJaguarMounts()
        {
            bool canDeploy = SkillManager.CanAutoDeployWildHunterMineWithoutVisibleMountedAction(
                actionName: null,
                jaguarRideOwnershipActive: true,
                jaguarMountItemId: 1932000);

            Assert.False(canDeploy);
        }
    }
}
