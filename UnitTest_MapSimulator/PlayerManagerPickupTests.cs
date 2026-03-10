using System.Reflection;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class PlayerManagerPickupTests
    {
        private static bool InvokeShouldAttemptPickup(bool pickupHeld, bool pickupPressed, int currentTime, int lastPickupAttemptTime)
        {
            MethodInfo? method = typeof(PlayerManager).GetMethod("ShouldAttemptPickup", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            return (bool)method!.Invoke(null, new object[] { pickupHeld, pickupPressed, currentTime, lastPickupAttemptTime })!;
        }

        [Fact]
        public void ShouldAttemptPickup_TriggersImmediatelyOnInitialPress()
        {
            bool shouldAttempt = InvokeShouldAttemptPickup(
                pickupHeld: true,
                pickupPressed: true,
                currentTime: 1_000,
                lastPickupAttemptTime: 950);

            Assert.True(shouldAttempt);
        }

        [Fact]
        public void ShouldAttemptPickup_WaitsForPickupDurationBeforeRepeating()
        {
            bool shouldAttempt = InvokeShouldAttemptPickup(
                pickupHeld: true,
                pickupPressed: false,
                currentTime: 1_100,
                lastPickupAttemptTime: 950);

            Assert.False(shouldAttempt);
        }

        [Fact]
        public void ShouldAttemptPickup_RepeatsWhenPickupDurationElapsed()
        {
            bool shouldAttempt = InvokeShouldAttemptPickup(
                pickupHeld: true,
                pickupPressed: false,
                currentTime: 1_150,
                lastPickupAttemptTime: 950);

            Assert.True(shouldAttempt);
        }

        [Fact]
        public void ShouldAttemptPickup_DoesNotRepeatWhenPickupIsReleased()
        {
            bool shouldAttempt = InvokeShouldAttemptPickup(
                pickupHeld: false,
                pickupPressed: false,
                currentTime: 1_500,
                lastPickupAttemptTime: 950);

            Assert.False(shouldAttempt);
        }
    }
}
