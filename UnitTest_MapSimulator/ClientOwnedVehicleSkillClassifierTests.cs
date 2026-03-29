using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public class ClientOwnedVehicleSkillClassifierTests
    {
        [Fact]
        public void LooksLikeClientOwnedRideDescriptionBuff_AcceptsInvisibleRideDescriptionOutlier()
        {
            var skill = new SkillData
            {
                IsBuff = true,
                Invisible = true,
                Name = "Yeti",
                Description = "[Master Level : 1]\nAllows one to ride on a Yeti."
            };

            Assert.True(ClientOwnedVehicleSkillClassifier.LooksLikeClientOwnedRideDescriptionBuff(skill));
        }

        [Fact]
        public void LooksLikeClientOwnedRideDescriptionBuff_AcceptsInvisibleRideDescriptionMount()
        {
            var skill = new SkillData
            {
                IsBuff = true,
                Invisible = true,
                Name = "Owl",
                Description = "[Master Level: 1]\nAllows you to ride the Owl."
            };

            Assert.True(ClientOwnedVehicleSkillClassifier.LooksLikeClientOwnedRideDescriptionBuff(skill));
        }

        [Fact]
        public void LooksLikeClientOwnedRideDescriptionBuff_RejectsVisibleNonVehicleBuff()
        {
            var skill = new SkillData
            {
                IsBuff = true,
                Invisible = false,
                ClientInfoType = 0,
                Name = "Fake Ride",
                Description = "Allows you to ride a cloud."
            };

            Assert.False(ClientOwnedVehicleSkillClassifier.LooksLikeClientOwnedRideDescriptionBuff(skill));
        }
    }
}
