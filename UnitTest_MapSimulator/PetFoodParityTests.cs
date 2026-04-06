using HaCreator.MapSimulator.Companions;

namespace UnitTest_MapSimulator
{
    public sealed class PetFoodParityTests
    {
        [Fact]
        public void TryPlanFoodItemUse_ReturnsNoActivePetsWhenListIsEmpty()
        {
            bool planned = PetController.TryPlanFoodItemUse(
                Array.Empty<PetRuntime>(),
                new[] { 5000000 },
                30,
                out _,
                out PetController.PetFoodItemUseFailureReason failureReason);

            Assert.False(planned);
            Assert.Equal(PetController.PetFoodItemUseFailureReason.NoActivePets, failureReason);
        }

        [Fact]
        public void TryPlanFoodItemUse_ReturnsNoCompatiblePetsWhenSupportedFamilyDoesNotMatch()
        {
            PetRuntime pet = CreatePet(5000000, slotIndex: 0, initialFullness: 60);

            bool planned = PetController.TryPlanFoodItemUse(
                new[] { pet },
                new[] { 5000001 },
                30,
                out _,
                out PetController.PetFoodItemUseFailureReason failureReason);

            Assert.False(planned);
            Assert.Equal(PetController.PetFoodItemUseFailureReason.NoCompatiblePets, failureReason);
        }

        [Fact]
        public void TryPlanFoodItemUse_ReturnsNoHungryCompatiblePetsWhenMatchingPetsAreFull()
        {
            PetRuntime pet = CreatePet(5000000, slotIndex: 0, initialFullness: 100);

            bool planned = PetController.TryPlanFoodItemUse(
                new[] { pet },
                new[] { 5000000 },
                30,
                out _,
                out PetController.PetFoodItemUseFailureReason failureReason);

            Assert.False(planned);
            Assert.Equal(PetController.PetFoodItemUseFailureReason.NoHungryCompatiblePets, failureReason);
        }

        [Fact]
        public void TryExecuteFoodItemUse_IncreasesFullnessAndAdvancesCommandLevel()
        {
            PetRuntime pet = CreatePet(5000000, slotIndex: 0, initialFullness: 60);
            PetController.PetFoodItemUsePlan plan = new()
            {
                SlotIndex = 0,
                FullnessIncrease = 30,
                ConsumeItem = true
            };

            bool handled = PetController.TryExecuteFoodItemUse(new[] { pet }, plan, currentTime: 1000, out int fedSlotIndex);

            Assert.True(handled);
            Assert.Equal(0, fedSlotIndex);
            Assert.Equal(90, pet.Fullness);
            Assert.Equal(1, pet.Tameness);
            Assert.Equal(2, pet.CommandLevel);
        }

        private static PetRuntime CreatePet(int itemId, int slotIndex, int initialFullness)
        {
            PetDefinition definition = new()
            {
                ItemId = itemId,
                Name = $"Pet {itemId}"
            };

            return new PetRuntime(runtimeId: slotIndex + 1, slotIndex, definition, initialFullness);
        }
    }
}
