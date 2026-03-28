using HaCreator.MapSimulator.Companions;
using System.Collections.Generic;

namespace UnitTest_MapSimulator;

public sealed class PetFoodParityTests
{
    [Fact]
    public void TryPlanFoodItemUse_PrefersCompatiblePetThatCanStillEat()
    {
        PetRuntime fullPet = CreatePetRuntime(5000000, slotIndex: 0, fullness: 100);
        PetRuntime hungryPet = CreatePetRuntime(5000001, slotIndex: 1, fullness: 60);

        bool planned = PetController.TryPlanFoodItemUse(
            new[] { fullPet, hungryPet },
            new[] { 5000000, 5000001 },
            fullnessIncrease: 30,
            out PetController.PetFoodItemUsePlan plan);

        Assert.True(planned);
        Assert.Equal(1, plan.SlotIndex);
        Assert.True(plan.ConsumeItem);

        bool handled = PetController.TryExecuteFoodItemUse(
            new[] { fullPet, hungryPet },
            plan,
            currentTime: 1000,
            out int fedSlotIndex);

        Assert.True(handled);
        Assert.Equal(1, fedSlotIndex);
        Assert.Equal(100, fullPet.Fullness);
        Assert.Equal(90, hungryPet.Fullness);
        Assert.Equal("food-ok", hungryPet.ActiveSpeechText);
    }

    [Fact]
    public void TryPlanFoodItemUse_UsesFailurePathWhenCompatiblePetIsAlreadyFull()
    {
        PetRuntime fullPet = CreatePetRuntime(5000000, slotIndex: 0, fullness: 100);

        bool planned = PetController.TryPlanFoodItemUse(
            new[] { fullPet },
            new[] { 5000000 },
            fullnessIncrease: 30,
            out PetController.PetFoodItemUsePlan plan);

        Assert.True(planned);
        Assert.Equal(0, plan.SlotIndex);
        Assert.False(plan.ConsumeItem);

        bool handled = PetController.TryExecuteFoodItemUse(
            new[] { fullPet },
            plan,
            currentTime: 1000,
            out int fedSlotIndex);

        Assert.True(handled);
        Assert.Equal(0, fedSlotIndex);
        Assert.Equal(100, fullPet.Fullness);
        Assert.Equal("food-full", fullPet.ActiveSpeechText);
    }

    [Fact]
    public void TryPlanFoodItemUse_RespectsFoodWhitelistWhenSelectingTargetPet()
    {
        PetRuntime incompatiblePet = CreatePetRuntime(5000002, slotIndex: 0, fullness: 40);
        PetRuntime compatiblePet = CreatePetRuntime(5000001, slotIndex: 1, fullness: 40);

        bool planned = PetController.TryPlanFoodItemUse(
            new[] { incompatiblePet, compatiblePet },
            new[] { 5000001 },
            fullnessIncrease: 30,
            out PetController.PetFoodItemUsePlan plan);

        Assert.True(planned);
        Assert.Equal(1, plan.SlotIndex);
        Assert.True(plan.ConsumeItem);
    }

    private static PetRuntime CreatePetRuntime(int itemId, int slotIndex, int fullness)
    {
        var definition = new PetDefinition
        {
            ItemId = itemId,
            Name = $"Pet_{itemId}",
            FoodFeedback = new Dictionary<int, PetDialogFeedbackDefinition>
            {
                [1] = new PetDialogFeedbackDefinition
                {
                    SuccessLines = new[] { "food-ok" },
                    FailureLines = new[] { "food-full" }
                }
            },
            FoodFeedbackLevelRanges = new Dictionary<int, (int MinLevel, int MaxLevel)>
            {
                [1] = (1, 30)
            }
        };

        return new PetRuntime(runtimeId: slotIndex + 1, slotIndex, definition, fullness);
    }
}
