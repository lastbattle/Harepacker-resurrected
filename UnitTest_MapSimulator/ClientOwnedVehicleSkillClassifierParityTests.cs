using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class ClientOwnedVehicleSkillClassifierParityTests
    {
        [Theory]
        [InlineData("iceStrike")]
        [InlineData("quadBlow")]
        public void KnownOwnerClassifier_Admits_LoadTamingMobActionPreGateNames_ForBattleship(string actionName)
        {
            bool admitted = ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(1932000, actionName);

            Assert.True(admitted);
        }

        [Theory]
        [InlineData("iceStrike")]
        [InlineData("quadBlow")]
        public void KnownOwnerClassifier_Admits_LoadTamingMobActionPreGateNames_ForMechanic(string actionName)
        {
            bool admitted = ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(1932016, actionName);

            Assert.True(admitted);
        }

        [Theory]
        [InlineData("alert")]
        [InlineData("paralyze")]
        [InlineData("ladder2")]
        [InlineData("rope2")]
        [InlineData("shoot6")]
        [InlineData("arrowRain")]
        public void KnownOwnerClassifier_Admits_LoadTamingMobActionSharedVehicleNames_ForBattleship(string actionName)
        {
            bool admitted = ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(1932000, actionName);

            Assert.True(admitted);
        }

        [Theory]
        [InlineData("alert")]
        [InlineData("paralyze")]
        [InlineData("ladder2")]
        [InlineData("rope2")]
        [InlineData("shoot6")]
        [InlineData("arrowRain")]
        public void KnownOwnerClassifier_Admits_LoadTamingMobActionSharedVehicleNames_ForMechanic(string actionName)
        {
            bool admitted = ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(1932016, actionName);

            Assert.True(admitted);
        }

        [Theory]
        [InlineData("braveslash3")]
        [InlineData("braveslash4")]
        [InlineData("chargeBlow")]
        public void KnownOwnerClassifier_Admits_LoadTamingMobActionRemapNames_ForBattleship(string actionName)
        {
            bool admitted = ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(1932000, actionName);

            Assert.True(admitted);
        }

        [Theory]
        [InlineData("braveslash3")]
        [InlineData("braveslash4")]
        [InlineData("chargeBlow")]
        public void KnownOwnerClassifier_Admits_LoadTamingMobActionRemapNames_ForMechanic(string actionName)
        {
            bool admitted = ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(1932016, actionName);

            Assert.True(admitted);
        }
    }
}
