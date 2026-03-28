using System.Reflection;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class RocketBoosterParityTests
    {
        [Fact]
        public void TryResolveClientRocketBoosterLaunchSpeed_UsesStoredVerticalLaunchFamily()
        {
            MethodInfo method = typeof(SkillManager).GetMethod(
                "TryResolveClientRocketBoosterLaunchSpeed",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            object[] args = { 20, 0f };
            bool resolved = (bool)method.Invoke(null, args);

            Assert.True(resolved);
            Assert.Equal(350f, (float)args[1]);
        }

        [Fact]
        public void TryResolveClientBoundJumpVelocity_DoesNotTreatRocketBoosterAsGenericFlashJump()
        {
            MethodInfo method = typeof(SkillManager).GetMethod(
                "TryResolveClientBoundJumpVelocity",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            object[] args = { 35101004, 20, true, 0f, 0f };
            bool resolved = (bool)method.Invoke(null, args);

            Assert.False(resolved);
            Assert.Equal(0f, (float)args[3]);
            Assert.Equal(0f, (float)args[4]);
        }
    }
}
